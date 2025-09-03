using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace SomeThingsFloat;

public class FloatingThings_MapComponent : MapComponent
{
    private static readonly List<IntVec3> validDirections =
    [
        IntVec3.North,
        IntVec3.NorthEast,
        IntVec3.East,
        IntVec3.SouthEast,
        IntVec3.South,
        IntVec3.SouthWest,
        IntVec3.West,
        IntVec3.NorthWest
    ];

    private readonly HashSet<AltitudeLayer> ignoredAltitudeLayers;
    private readonly HashSet<IntVec3> mapEdgeCells;

    // ReSharper disable once ChangeFieldTypeToSystemThreadingLock
    private readonly object updateValuesLock = new();
    private bool allCellsDirty;
    private readonly HashSet<IntVec3> cellsEmptyHash;
    private HashSet<IntVec3> cellsWithNothing;
    private HashSet<IntVec3> cellsWithOcean;
    private HashSet<IntVec3> cellsWithRiver;
    private HashSet<IntVec3> cellsWithWater;
    private HashSet<IntVec3> dirtyCells;
    public int EnemyPawnsDrowned;
    private Dictionary<Thing, float> floatingValues;
    private Dictionary<Thing, IntVec3> hiddenPositions;
    private List<Thing> hiddenPositionsKeys;
    private List<IntVec3> hiddenPositionsValues;
    private bool isSpace;
    private Dictionary<Thing, Tuple<int, IntVec3>> lastPositions;
    private int lastSpawnTick;
    private List<Pawn> mapPawns;
    private Dictionary<Thing, IntVec3> spaceDirections;
    private List<Thing> spaceDirectionsKeys;
    private List<IntVec3> spaceDirectionsValues;
    private readonly ConcurrentQueue<(Pawn pawn, float initialSeverity, bool notify)> toAddDrowning;
    private readonly ConcurrentQueue<(Pawn pawn, float severity)> toAddLostFooting;
    private readonly ConcurrentQueue<Pawn> toClearDrowning;
    private readonly ConcurrentQueue<Pawn> toClearLostFooting;
    private readonly ConcurrentQueue<(Pawn pawn, float delta)> toIncreaseDrowning;
    private readonly ConcurrentQueue<(Pawn pawn, float severity)> toSetLostFooting;
    private readonly ConcurrentQueue<(Pawn pawn, float value, bool notifyPlayer)> toStartFloating;
    private HashSet<IntVec3> underCellsWithWater;
    private Dictionary<int, Thing> updateValues;
    private List<int> updateValuesKeys;
    private List<Thing> updateValuesValues;
    public int WastePacksFloated;

    public FloatingThings_MapComponent(Map map) : base(map)
    {
        SomeThingsFloat.FloatingMapComponents[map] = this;
        underCellsWithWater = [];
        cellsWithWater = [];
        cellsWithOcean = [];
        cellsWithRiver = [];
        cellsWithNothing = [];
        dirtyCells = [];
        cellsEmptyHash = [];
        allCellsDirty = true;
        spaceDirections = [];
        mapEdgeCells = [];
        floatingValues = new Dictionary<Thing, float>();
        updateValues = new Dictionary<int, Thing>();
        lastPositions = new Dictionary<Thing, Tuple<int, IntVec3>>();
        updateValuesKeys = [];
        updateValuesValues = [];
        mapPawns = [];
        hiddenPositions = new Dictionary<Thing, IntVec3>();
        hiddenPositionsKeys = [];
        hiddenPositionsValues = [];
        toClearLostFooting = [];
        toSetLostFooting = [];
        toAddLostFooting = [];
        toStartFloating = [];
        toClearDrowning = [];
        toIncreaseDrowning = [];
        toAddDrowning = [];
        ignoredAltitudeLayers =
        [
            AltitudeLayer.Blueprint,
            AltitudeLayer.Conduits,
            AltitudeLayer.Filth,
            AltitudeLayer.Gas,
            AltitudeLayer.FogOfWar,
            AltitudeLayer.SmallWire,
            AltitudeLayer.Weather
        ];
        lastSpawnTick = 0;
        isSpace = map.Tile.LayerDef.isSpace;
        map.events.TerrainChanged += terrainChanged;
    }

    public override void MapComponentTick()
    {
        var ticksGame = GenTicks.TicksGame;
        if (map.IsHashIntervalTick(GenTicks.TickLongInterval))
        {
            SomeThingsFloat.LogMessage("Doing delta update of cells", debug: true);
            updateListOfFloatCells();
        }
        else
        {
            if (map.IsHashIntervalTick(GenDate.TicksPerDay))
            {
                SomeThingsFloat.LogMessage("Doing full update of cells", debug: true);
                allCellsDirty = true;
                updateListOfFloatCells();
            }
        }

        if (ticksGame % GenTicks.TickLongInterval == 500)
        {
            SomeThingsFloat.LogMessage("Doing TrySpawnThingAtMapEdge call", debug: true);
            TrySpawnThingAtMapEdge();
        }

        if (ticksGame % GenTicks.TickLongInterval == 1000)
        {
            SomeThingsFloat.LogMessage("Doing updateListOfFloatingThings call", debug: true);
            updateListOfFloatingThings();
        }

        if (mapPawns == null || ticksGame % GenTicks.TickLongInterval == 1500)
        {
            mapPawns = map.mapPawns.AllPawns;
        }

        if (SomeThingsFloatMod.Instance.Settings.PawnsCanFall)
        {
            checkForPawnsThatCanFall();
        }

        if (SomeThingsFloatMod.Instance.Settings.DownedPawnsDrown)
        {
            checkForPawnsThatCanDrown();
        }

        if (hiddenPositions.Any())
        {
            SomeThingsFloat.LogMessage("Found items in hiddenPositions", debug: true);
            var thingsDestroyed = new List<Thing>();
            var thingsToRespawn = new List<Thing>();
            foreach (var hiddenPositionsKey in hiddenPositions.Keys)
            {
                hiddenPositionsKey.Tick();
                if (hiddenPositionsKey.Destroyed)
                {
                    thingsDestroyed.Add(hiddenPositionsKey);
                }
                else
                {
                    if (floatingValues.TryGetValue(hiddenPositionsKey, out var floatValue) && floatValue == 0)
                    {
                        thingsToRespawn.Add(hiddenPositionsKey);
                    }
                }
            }

            foreach (var destroyedThing in thingsDestroyed)
            {
                SomeThingsFloat.LogMessage($"{destroyedThing} is destroyed, removing");
                hiddenPositions.Remove(destroyedThing);
                lastPositions.Remove(destroyedThing);
            }

            foreach (var respawningThing in thingsToRespawn)
            {
                var radius = 1;
                IntVec3 spawnCell;
                if (!hiddenPositions.TryGetValue(respawningThing, out var respawnPos))
                {
                    continue;
                }

                while (!CellFinder.TryFindRandomCellNear(respawnPos, map, radius, cellsWithWater.Contains,
                           out spawnCell))
                {
                    radius++;
                }

                if (GenPlace.TryPlaceThing(respawningThing, spawnCell, map, ThingPlaceMode.Direct))
                {
                    lastPositions[respawningThing] = new Tuple<int, IntVec3>(ticksGame, spawnCell);
                }

                hiddenPositions.Remove(respawningThing);
            }
        }

        if ((SomeThingsFloatMod.Instance.Settings.Bobbing || SomeThingsFloatMod.Instance.Settings.SmoothAnimation) &&
            updateValues.Any())
        {
            foreach (var thingToUpdate in updateValues.Values)
            {
                if (thingToUpdate == null)
                {
                    continue;
                }

                if (thingToUpdate.def.drawerType != DrawerType.RealtimeOnly)
                {
                    thingToUpdate.DirtyMapMesh(map);
                }
            }
        }

        if (!updateValues.Remove(ticksGame, out var thing))
        {
            return;
        }

        if (thing == null)
        {
            return;
        }

        lastPositions.Remove(thing);

        if (!isSpace && (!VerifyThingIsInWater(thing) || thing is Pawn { Downed: false }))
        {
            SomeThingsFloat.LogMessage($"{thing} is no longer floating");
            floatingValues.Remove(thing);
            return;
        }


        if (SomeThingsFloatMod.Instance.Settings.ReservedItemsWillNotMove)
        {
            if (map.reservationManager.AllReservedThings().Contains(thing))
            {
                SomeThingsFloat.LogMessage($"{thing} will not move since its reserved");
                setNextUpdateTime(thing, true);
                return;
            }

            SomeThingsFloat.LogMessage($"{thing} is not reserved", debug: true);
        }

        if (!tryToFindNewPosition(thing, out var newPosition))
        {
            SomeThingsFloat.LogMessage($"{thing} cannot find a new postition");
            setNextUpdateTime(thing, true);
            return;
        }

        lastPositions[thing] = new Tuple<int, IntVec3>(ticksGame, thing.Position);

        var wasInStorage = false;
        var wasUnspawned = false;
        var wasSelected = Find.Selector.IsSelected(thing);
        if (!hiddenPositions.Remove(thing, out var originalPosition))
        {
            originalPosition = thing.Position;
            if (SomeThingsFloatMod.Instance.Settings.ForbidWhenMoving)
            {
                wasInStorage = thing.IsInValidStorage();
            }

            if (thing.Spawned)
            {
                thing.DeSpawn();
            }
        }
        else
        {
            wasUnspawned = true;
        }

        if (newPosition == IntVec3.Invalid)
        {
            if (thing is Pawn { IsColonist: true } pawn)
            {
                if (!SomeThingsFloatMod.Instance.Settings.NoNotifications)
                {
                    Find.LetterStack.ReceiveLetter("STF.PawnIsLostTitle".Translate(pawn.NameFullColored),
                        "STF.PawnIsLostMessage".Translate(pawn.NameFullColored), LetterDefOf.Death);
                }

                PawnDiedOrDownedThoughtsUtility.TryGiveThoughts(pawn, null, PawnDiedOrDownedThoughtsKind.Lost);
            }

            floatingValues?.Remove(thing);

            if (thing.def == RimWorld.ThingDefOf.Wastepack)
            {
                WastePacksFloated += thing.stackCount;
                var neighbor = ((SurfaceTile)Find.World.grid[map.Tile]).Rivers.FirstOrDefault().neighbor;
                if (neighbor == 0)
                {
                    neighbor = Find.World.grid.FindMostReasonableAdjacentTileForDisplayedPathCost(map.Tile);
                }

                var goodwillEffecter = thing.TryGetComp<CompDissolutionEffect_Goodwill>();
                var pollutionEffecter = thing.TryGetComp<CompDissolutionEffect_Pollution>();
                if (goodwillEffecter != null)
                {
                    goodwillEffecter.DoDissolutionEffectWorld(thing.stackCount, neighbor);
                    SomeThingsFloat.LogMessage(
                        $"Triggering goodwillEffecter for {thing} on map-tile {neighbor}");
                }

                if (pollutionEffecter != null)
                {
                    pollutionEffecter.DoDissolutionEffectWorld(thing.stackCount, neighbor);
                    SomeThingsFloat.LogMessage(
                        $"Triggering pollutionEffecter for {thing} on map-tile {neighbor}");
                }
            }

            lastPositions.Remove(thing);

            thing.Destroy();
            return;
        }

        setNextUpdateTime(thing);

        if (underCellsWithWater.Contains(newPosition))
        {
            hiddenPositions[thing] = newPosition;
            if (thing.Spawned)
            {
                thing.DeSpawn();
            }

            lastPositions.Remove(thing);

            return;
        }

        if (!GenPlace.TryPlaceThing(thing, newPosition, map, ThingPlaceMode.Direct))
        {
            SomeThingsFloat.LogMessage($"{thing} could not be placed at its new position");
            if (wasUnspawned)
            {
                hiddenPositions[thing] = originalPosition;
                return;
            }

            GenPlace.TryPlaceThing(thing, originalPosition, map, ThingPlaceMode.Direct);
        }
        else
        {
            if (SomeThingsFloatMod.Instance.Settings.HaulUrgently && wasUnspawned &&
                SomeThingsFloat.HaulUrgentlyDef != null)
            {
                map.designationManager.AddDesignation(new Designation(thing, SomeThingsFloat.HaulUrgentlyDef));
            }

            if (SomeThingsFloatMod.Instance.Settings.AllowOnStuck)
            {
                var buidingDef = newPosition.GetFirstBuilding(map)?.def;
                if (buidingDef != null && (buidingDef == ThingDefOf.STF_Bars && SomeThingsFloat.IsLargeThing(thing) ||
                                           buidingDef == ThingDefOf.STF_Net))
                {
                    thing.SetForbidden(false, false);
                }
            }

            if (isSpace && !cellsWithNothing.Contains(newPosition))
            {
                floatingValues.Remove(thing);
                lastPositions.Remove(thing);
                updateValues.RemoveAll(pair => pair.Value == thing);
                SomeThingsFloat.LogMessage($"{thing} is no longer floating in space");
            }
        }

        if (SomeThingsFloatMod.Instance.Settings.ForbidWhenMoving && wasInStorage != thing.IsInValidStorage())
        {
            thing.SetForbidden(wasInStorage, false);
        }

        if (wasSelected)
        {
            Find.Selector.Select(thing, false);
        }
    }

    public override void MapGenerated()
    {
        base.MapGenerated();
        allCellsDirty = true;
        updateListOfFloatCells();
        updateListOfFloatingThings();
    }

    private void terrainChanged(IntVec3 cell)
    {
        dirtyCells.Add(cell);
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref lastSpawnTick, "lastSpawnTick");
        Scribe_Values.Look(ref isSpace, "isSpace");
        Scribe_Values.Look(ref WastePacksFloated, "WastePacksFloated");
        lock (this)
        {
            Scribe_Values.Look(ref EnemyPawnsDrowned, "EnemyPawnsDrowned");
        }

        Scribe_Collections.Look(ref cellsWithWater, "cellsWithWater", LookMode.Value);
        Scribe_Collections.Look(ref cellsWithNothing, "cellsWithNothing", LookMode.Value);
        Scribe_Collections.Look(ref cellsWithRiver, "cellsWithRiver", LookMode.Value);
        Scribe_Collections.Look(ref cellsWithOcean, "cellsWithOcean", LookMode.Value);
        Scribe_Collections.Look(ref dirtyCells, "dirtyCells", LookMode.Value);
        Scribe_Values.Look(ref allCellsDirty, "allCellsDirty", true);
        Scribe_Collections.Look(ref underCellsWithWater, "underCellsWithWater", LookMode.Value);
        Scribe_Collections.Look(ref spaceDirections, "spaceDirections", LookMode.Deep, LookMode.Value,
            ref spaceDirectionsKeys, ref spaceDirectionsValues);
        Scribe_Collections.Look(ref updateValues, "updateValues", LookMode.Value, LookMode.Reference,
            ref updateValuesKeys, ref updateValuesValues);
        Scribe_Collections.Look(ref hiddenPositions, "hiddenPositions", LookMode.Deep, LookMode.Value,
            ref hiddenPositionsKeys, ref hiddenPositionsValues);
        if (Scribe.mode != LoadSaveMode.ResolvingCrossRefs)
        {
            return;
        }

        floatingValues ??= new Dictionary<Thing, float>();

        hiddenPositions ??= new Dictionary<Thing, IntVec3>();

        spaceDirections ??= new Dictionary<Thing, IntVec3>();

        updateValues ??= new Dictionary<int, Thing>();

        lastPositions ??= new Dictionary<Thing, Tuple<int, IntVec3>>();

        cellsWithWater ??= [];

        cellsWithNothing ??= [];

        cellsWithRiver ??= [];

        cellsWithOcean ??= [];

        dirtyCells ??= [];

        underCellsWithWater ??= [];

        if (!isSpace)
        {
            isSpace = map.Tile.LayerDef.isSpace;
        }

        updateListOfFloatCells();
        updateListOfFloatingThings();
    }

    private void updateListOfFloatCells()
    {
        SomeThingsFloat.LogMessage("Updating water-cells", debug: true);
        switch (allCellsDirty)
        {
            case false when !dirtyCells.Any():
                return;
            case true:
                // Clear collections before processing
                cellsWithWater = [];
                cellsWithNothing = [];
                cellsWithRiver = [];
                cellsWithOcean = [];
                underCellsWithWater = [];
                // Use Parallel.For to iterate over terrain grid
                Parallel.For(0, map.terrainGrid.topGrid.Length, i => processCell(i, map.cellIndices.IndexToCell(i)));
                break;
            default:
            {
                // Remote dirty cells before they are possibly generated again.
                foreach (var cell in dirtyCells)
                {
                    cellsWithWater.Remove(cell);
                    cellsWithNothing.Remove(cell);
                    cellsWithRiver.Remove(cell);
                    cellsWithOcean.Remove(cell);
                    underCellsWithWater.Remove(cell);
                }

                if (dirtyCells.Count < 100)
                {
                    foreach (var cell in dirtyCells)
                    {
                        processCell(map.cellIndices.CellToIndex(cell), cell);
                    }
                }
                else
                {
                    Parallel.ForEach(dirtyCells, cell => processCell(map.cellIndices.CellToIndex(cell), cell));
                }

                break;
            }
        }

        allCellsDirty = false;
        dirtyCells = [];

        // Log results
        SomeThingsFloat.LogMessage($"Found {cellsWithWater.Count} water-cells");
        SomeThingsFloat.LogMessage($"Found {cellsWithRiver.Count} river-cells");
        SomeThingsFloat.LogMessage($"Found {cellsWithOcean.Count} ocean-cells");
        SomeThingsFloat.LogMessage($"Found {cellsWithNothing.Count} space-cells");
        SomeThingsFloat.LogMessage($"Found {underCellsWithWater.Count} water-cells under bridges");
        return;

        void processCell(int i, IntVec3 cell)
        {
            var upperTerrain = map.terrainGrid.topGrid[i];
            var lowerTerrain = map.terrainGrid.underGrid[i];
            var foundationTerrain = map.terrainGrid.foundationGrid[i];
            var tempTerrain = map.terrainGrid.tempGrid[i];

            // Check for bridges or foundations
            if (upperTerrain is { bridge: true } || foundationTerrain is { isFoundation: true } ||
                upperTerrain?.defName.ToLower().Contains("bridge") == true ||
                tempTerrain?.tags?.Contains("Ice") == true)
            {
                if (!SomeThingsFloatMod.Instance.Settings.FloatUnderBridges)
                {
                    return;
                }

                if (upperTerrain is not { IsWater: true } && lowerTerrain is not { IsWater: true })
                {
                    return;
                }

                lock (underCellsWithWater)
                {
                    underCellsWithWater.Add(cell);
                }

                return;
            }

            // Check for water cells
            if (upperTerrain is { IsWater: true } || tempTerrain is { IsWater: true })
            {
                lock (cellsWithWater)
                {
                    cellsWithWater.Add(cell);
                }
            }

            // Check for river cells
            if (upperTerrain is { IsRiver: true } || tempTerrain is { IsRiver: true })
            {
                lock (cellsWithRiver)
                {
                    cellsWithRiver.Add(cell);
                }
            }

            // Check for ocean cells
            if (upperTerrain is { IsOcean: true } || tempTerrain is { IsOcean: true })
            {
                lock (cellsWithOcean)
                {
                    cellsWithOcean.Add(cell);
                }
            }

            // Check for space cells
            if (!isSpace || upperTerrain is not { defName: "Space" })
            {
                return;
            }

            lock (cellsWithNothing)
            {
                cellsWithNothing.Add(cell);
            }
        }
    }

    public bool TrySpawnThingAtMapEdge(bool force = false)
    {
        if (!SomeThingsFloatMod.Instance.Settings.SpawnNewItems)
        {
            return false;
        }

        if ((!isSpace || !cellsWithNothing.Any()) && !cellsWithWater.Any())
        {
            SomeThingsFloat.LogMessage("No cells to spawn in", debug: true);
            return false;
        }

        switch (force)
        {
            case false when Rand.Value < 0.9f:
                return false;
            case false when lastSpawnTick + SomeThingsFloatMod.Instance.Settings.MinTimeBetweenItems >
                            GenTicks.TicksGame:
                SomeThingsFloat.LogMessage(
                    $"Not time to spawn yet, next spawn: {lastSpawnTick + SomeThingsFloatMod.Instance.Settings.MinTimeBetweenItems}, current time {GenTicks.TicksGame}",
                    debug: true);
                return false;
        }

        if (!mapEdgeCells.Any())
        {
            var possibleMapEdgeCells = cellsWithWater.Intersect(CellRect.WholeMap(map).EdgeCells);
            if (isSpace)
            {
                possibleMapEdgeCells = cellsWithNothing.Intersect(CellRect.WholeMap(map).EdgeCells);
            }

            var edgeCells = possibleMapEdgeCells as IntVec3[] ?? possibleMapEdgeCells.ToArray();
            if (!edgeCells.Any())
            {
                SomeThingsFloat.LogMessage("No possible edge cells to spawn in", debug: true);
                return false;
            }

            foreach (var mapEdgeCell in edgeCells)
            {
                if (SomeThingsFloatMod.Instance.Settings.SpawnInOceanTiles &&
                    mapEdgeCell.GetTerrain(map)?.defName.ToLower().Contains("ocean") == true ||
                    isSpace && mapEdgeCell.GetTerrain(map)?.defName == "Space")
                {
                    mapEdgeCells.Add(mapEdgeCell);
                    continue;
                }

                if (mapEdgeCell.GetTerrain(map)?.defName.ToLower().Contains("moving") != true)
                {
                    SomeThingsFloat.LogMessage($"{mapEdgeCell} ! moving", debug: true);
                    continue;
                }

                var flowAtCell = map.waterInfo.GetWaterMovement(mapEdgeCell.ToVector3Shifted());
                if (flowAtCell == Vector3.zero)
                {
                    SomeThingsFloat.LogMessage($"{mapEdgeCell} ! {flowAtCell}", debug: true);
                    continue;
                }

                if (mapEdgeCell.x == 0 && flowAtCell.x < 0 ||
                    mapEdgeCell.z == 0 && flowAtCell.z < 0)
                {
                    SomeThingsFloat.LogMessage($"{mapEdgeCell} < {flowAtCell}");
                    continue;
                }

                if (mapEdgeCell.x == map.Size.x - 1 && flowAtCell.x > 0 ||
                    mapEdgeCell.z == map.Size.z - 1 && flowAtCell.z > 0)
                {
                    SomeThingsFloat.LogMessage($"{mapEdgeCell} > {flowAtCell}", debug: true);
                    continue;
                }

                mapEdgeCells.Add(mapEdgeCell);
            }
        }

        if (!mapEdgeCells.Any())
        {
            SomeThingsFloat.LogMessage("Found no valid map-edge cells");
            return false;
        }

        var cellToPlaceIt = mapEdgeCells.RandomElement();

        // Sometimes we spawn a pawn or corpse
        if (!isSpace && Rand.Value > 0.9f)
        {
            var currentMarketValue = 0f;
            var pawnKindDef = (from kindDef in DefDatabase<PawnKindDef>.AllDefs
                    where kindDef.RaceProps.IsFlesh && kindDef.defaultFactionDef is not { isPlayer: true }
                                                    && !SomeThingsFloat.AquaticRaces.Contains(kindDef.race)
                                                    && !SomeThingsFloat.Vehicles.Contains(kindDef.race)
                    select kindDef)
                .RandomElement();
            Faction faction = null;
            if (pawnKindDef.defaultFactionDef != null)
            {
                faction = Find.World.factionManager.AllFactions
                    .Where(factionType => factionType.def == pawnKindDef.defaultFactionDef).RandomElement();
            }

            var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(pawnKindDef, faction, allowDead: false));
            if (pawn.equipment?.HasAnything() == true)
            {
                pawn.equipment.DestroyAllEquipment();
            }

            if (pawn.inventory?.innerContainer?.Any() == true)
            {
                pawn.inventory.DestroyAll();
            }

            if (!SomeThingsFloatMod.Instance.Settings.SpawnLivingPawns || Rand.Value > 0.1f)
            {
                if (!pawn.Dead)
                {
                    pawn.Kill(null);
                }

                if (pawn.apparel?.WornApparel?.Any() == true)
                {
                    // ReSharper disable once ForCanBeConvertedToForeach
                    for (var index = 0; index < pawn.apparel.WornApparel.Count; index++)
                    {
                        var apparel = pawn.apparel.WornApparel[index];
                        if (apparel.MarketValue + currentMarketValue <
                            SomeThingsFloatMod.Instance.Settings.MaxSpawnValue)
                        {
                            currentMarketValue += apparel.MarketValue;
                            continue;
                        }

                        SomeThingsFloat.LogMessage(
                            $"Destroying worn apparel {apparel} since it has too high value ({apparel.MarketValue})");
                        apparel.Destroy();
                    }
                }

                pawn.Corpse.Age = Rand.Range(1, 900000);
                GenSpawn.Spawn(pawn.Corpse, cellToPlaceIt, map);
                pawn.Corpse.GetComp<CompRottable>().RotProgress += pawn.Corpse.Age;
                if (!force)
                {
                    lastSpawnTick = GenTicks.TicksGame;
                }

                pawn.Corpse.SetForbidden(SomeThingsFloatMod.Instance.Settings.ForbidSpawningItems, false);
                if (SomeThingsFloat.HaulUrgentlyDef != null && SomeThingsFloatMod.Instance.Settings.HaulUrgently)
                {
                    map.designationManager.AddDesignation(new Designation(pawn.Corpse,
                        SomeThingsFloat.HaulUrgentlyDef));
                }

                if (!SomeThingsFloatMod.Instance.Settings.NoNotifications &&
                    SomeThingsFloatMod.Instance.Settings.NotifyOfSpawningItems)
                {
                    Messages.Message(
                        cellToPlaceIt.GetTerrain(map)?.defName.ToLower().Contains("ocean") == true
                            ? "STF.ThingsFloatedInFromTheOcean".Translate(pawn.Corpse.LabelCap)
                            : "STF.ThingsFloatedIntoTheMap".Translate(pawn.Corpse.LabelCap), pawn,
                        MessageTypeDefOf.NeutralEvent);
                }

                return true;
            }

            HealthUtility.DamageUntilDowned(pawn);
            GenSpawn.Spawn(pawn, cellToPlaceIt, map);
            if (!SomeThingsFloatMod.Instance.Settings.NoNotifications)
            {
                if (!pawn.RaceProps.Animal)
                {
                    Find.LetterStack.ReceiveLetter("STF.PawnSpawnedTitle".Translate(),
                        "STF.PawnSpawnedMessage".Translate(),
                        LetterDefOf.NeutralEvent, pawn);
                }
                else
                {
                    if (SomeThingsFloatMod.Instance.Settings.NotifyOfSpawningItems)
                    {
                        Messages.Message("STF.ThingsFloatedIntoTheMap".Translate(pawn.NameFullColored), pawn,
                            MessageTypeDefOf.NeutralEvent);
                    }
                }
            }

            if (!force)
            {
                lastSpawnTick = GenTicks.TicksGame;
            }

            return true;
        }

        var thingToMake = SomeThingsFloat.ThingsToCreate
            .Where(def =>
            {
                if (!SomeThingsFloatMod.Instance.Settings.SpawnFertilizedEggs)
                {
                    return def.BaseMarketValue <= SomeThingsFloatMod.Instance.Settings.MaxSpawnValue &&
                           def.thingCategories?.Contains(ThingCategoryDefOf.EggsFertilized) == false;
                }

                return def.BaseMarketValue <= SomeThingsFloatMod.Instance.Settings.MaxSpawnValue;
            }).RandomElementByWeight(def => def.generateCommonality);
        var amountToSpawn =
            (int)Math.Floor(SomeThingsFloatMod.Instance.Settings.MaxSpawnValue / thingToMake.BaseMarketValue);

        if (isSpace)
        {
            thingToMake = ThingCategoryDefOf.Chunks.DescendantThingDefs.RandomElement();
            amountToSpawn = 1;
        }

        if (amountToSpawn == 0)
        {
            SomeThingsFloat.LogMessage($"Value of {thingToMake} too high, could not spawn");
            return false;
        }

        var thing = ThingMaker.MakeThing(thingToMake);
        if (thing is Corpse corpse && (corpse.Bugged || !corpse.InnerPawn.RaceProps.IsFlesh))
        {
            return false;
        }

        if (GenPlace.HaulPlaceBlockerIn(thing, cellToPlaceIt, map, true) != null)
        {
            SomeThingsFloat.LogMessage(
                $"{thing} could not be created at map edge: {cellToPlaceIt}, something in the way");
            return false;
        }

        if (thing.def.stackLimit > 1)
        {
            thing.stackCount = Rand.RangeInclusive(1, Math.Min(thing.def.stackLimit, amountToSpawn));
        }

        if (thing.TryGetComp<CompHatcher>() is { } compHatcher)
        {
            compHatcher.hatcheeFaction = Faction.OfPlayerSilentFail;
        }

        if (!GenPlace.TryPlaceThing(thing, cellToPlaceIt, map, ThingPlaceMode.Direct))
        {
            SomeThingsFloat.LogMessage($"{thing} could not be created at map edge: {cellToPlaceIt}");
            return false;
        }

        thing.SetForbidden(SomeThingsFloatMod.Instance.Settings.ForbidSpawningItems, false);
        if (SomeThingsFloat.HaulUrgentlyDef != null && SomeThingsFloatMod.Instance.Settings.HaulUrgently)
        {
            map.designationManager.AddDesignation(new Designation(thing, SomeThingsFloat.HaulUrgentlyDef));
        }

        if (!SomeThingsFloatMod.Instance.Settings.NoNotifications &&
            SomeThingsFloatMod.Instance.Settings.NotifyOfSpawningItems)
        {
            if (cellToPlaceIt.GetTerrain(map)?.defName.ToLower().Contains("ocean") == true)
            {
                Messages.Message(
                    "STF.ThingsFloatedInFromTheOcean".Translate(thing.LabelCap), thing,
                    MessageTypeDefOf.NeutralEvent);
            }
            else if (isSpace)
            {
                Messages.Message(
                    "STF.ThingsFloatedInFromSpace".Translate(thing.LabelCap), thing,
                    MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Messages.Message(
                    "STF.ThingsFloatedIntoTheMap".Translate(thing.LabelCap), thing,
                    MessageTypeDefOf.NeutralEvent);
            }
        }

        lastPositions[thing] = new Tuple<int, IntVec3>(GenTicks.TicksGame, thing.Position);

        if (!force)
        {
            lastSpawnTick = GenTicks.TicksGame;
        }

        if (!isSpace)
        {
            return true;
        }

        spaceDirections[thing] =
            validDirections.Where(vec3 => (cellToPlaceIt + vec3).InBounds(map)).RandomElement();
        floatingValues[thing] = Rand.Range(1f, 3f);

        return true;
    }

    private void updateListOfFloatingThings()
    {
        floatingValues ??= new Dictionary<Thing, float>();
        SomeThingsFloat.LogMessage("Updating floating things", debug: true);

        // Staging queues to collect results from parallel loops
        var scheduled = new ConcurrentQueue<(Thing thing, float value)>();
        var spaceToProcess = new ConcurrentQueue<Thing>();

        // Inputs that are iterated from multiple threads (must be accessed read-only)
        // Should ideally be IReadOnlySet and IReadOnlyCollection, but not available in this .NET version.
        var waterCells = cellsWithWater;
        var nothingCells = isSpace ? cellsWithNothing : cellsEmptyHash;
        var hiddenSnapshot = hiddenPositions;

        // 1) Cells with water: compute candidates and enqueue
        Parallel.ForEach(waterCells, vec3 =>
        {
            foreach (var possibleThing in SomeThingsFloat.GetThingsAndPawns(vec3, map))
            {
                if (possibleThing == null)
                {
                    continue;
                }

                if (possibleThing is not Pawn && floatingValues.ContainsKey(possibleThing))
                {
                    continue;
                }

                if (possibleThing is Pawn pawn && (SomeThingsFloat.AquaticRaces.Contains(pawn.def) ||
                                                   SomeThingsFloat.Vehicles.Contains(pawn.def)))
                {
                    continue;
                }

                var floatValue = SomeThingsFloat.GetFloatingValue(possibleThing);
                if (floatValue > 0)
                {
                    scheduled.Enqueue((possibleThing, floatValue));
                }
            }
        });

        // 2) Hidden positions: compute for hidden things and enqueue
        Parallel.ForEach(hiddenSnapshot, pair =>
        {
            var possibleThing = pair.Key;
            if (possibleThing == null || possibleThing is not Pawn && floatingValues.ContainsKey(possibleThing))
            {
                return;
            }

            var floatValue = SomeThingsFloat.GetFloatingValue(possibleThing);
            if (floatValue > 0)
            {
                scheduled.Enqueue((possibleThing, floatValue));
            }
        });

        // 3) Space: only collect candidates in parallel; do mutations on main thread
        if (isSpace && nothingCells.Count > 0)
        {
            Parallel.ForEach(nothingCells, vec3 =>
            {
                foreach (var possibleThing in SomeThingsFloat.GetThingsAndPawns(vec3, map, true))
                {
                    if (possibleThing != null)
                    {
                        spaceToProcess.Enqueue(possibleThing);
                    }
                }
            });
        }

        // COMMIT PHASE (main thread): mutate dictionaries safely

        // Commit space items: ensure direction, set random float, and schedule
        while (spaceToProcess.TryDequeue(out var spaceThing))
        {
            if (!spaceDirections.ContainsKey(spaceThing))
            {
                // Choose a valid in-bounds direction
                spaceDirections[spaceThing] =
                    validDirections.Where(vec3 => (spaceThing.Position + vec3).InBounds(map)).RandomElement();
            }

            var floatValue = Rand.Range(1f, 3f);
            floatingValues[spaceThing] = floatValue;
            setNextUpdateTime(spaceThing, floatValue);
        }

        // Commit water/hidden items
        while (scheduled.TryDequeue(out var item))
        {
            var thing = item.thing;
            var value = item.value;

            floatingValues[thing] = value;
            setNextUpdateTime(thing, value);
        }

        SomeThingsFloat.LogMessage($"Found {floatingValues.Count} items in floatable terrain");
    }

    private void setNextUpdateTime(Thing thing, float floatValue, bool longTime = false)
    {
        if (thing == null)
        {
            return;
        }

        var timeIncrease = longTime ? 5 : 1;
        var nextUpdate = GenTicks.TicksGame +
                         (int)Math.Round(
                             (GenTicks.TickRareInterval / floatValue /
                                 SomeThingsFloatMod.Instance.Settings.RelativeFloatSpeed * timeIncrease) +
                             Rand.Range(-10, 10));

        lock (updateValuesLock)
        {
            if (updateValues.ContainsValue(thing))
            {
                return;
            }

            while (updateValues.ContainsKey(nextUpdate))
            {
                nextUpdate++;
            }

            updateValues[nextUpdate] = thing;
        }

        SomeThingsFloat.LogMessage($"Current tick: {GenTicks.TicksGame}, {thing} next update: {nextUpdate}",
            debug: true);
    }

    private void setNextUpdateTime(Thing thing, bool longTime = false)
    {
        if (thing == null)
        {
            return;
        }

        if (!floatingValues.TryGetValue(thing, out var floatValue))
        {
            return;
        }

        setNextUpdateTime(thing, floatValue, longTime);
    }

    private void checkForPawnsThatCanFall()
    {
        // Staging queues
        toClearLostFooting.Clear();
        toSetLostFooting.Clear();
        toAddLostFooting.Clear();
        toStartFloating.Clear();

        // River cells accessed from multiple threads, must be accessed read-only.
        // Again, ideally these should be IReadOnlySet.
        var riverCellsSnapshot = cellsWithRiver;
        var oceanCellsSnapshot = cellsWithOcean;

        Parallel.ForEach(mapPawns, pawn =>
        {
            if (pawn == null ||
                !pawn.IsHashIntervalTick(GenTicks.TickRareInterval) ||
                SomeThingsFloat.AquaticRaces.Contains(pawn.def) ||
                SomeThingsFloat.Vehicles.Contains(pawn.def))
            {
                return;
            }

            var lostFootingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.STF_LostFooting);
            if (pawn is not { Spawned: true } || pawn.Dead || pawn.CarriedBy != null ||
                (!riverCellsSnapshot.Any() || !riverCellsSnapshot.Contains(pawn.Position)) &&
                (!oceanCellsSnapshot.Any() || !oceanCellsSnapshot.Contains(pawn.Position)))
            {
                if (lostFootingHediff != null)
                {
                    toClearLostFooting.Enqueue(pawn);
                }

                return;
            }

            if (pawn.CurJobDef?.defName.ToLower().Contains("swim") == true)
            {
                if (lostFootingHediff != null)
                {
                    toClearLostFooting.Enqueue(pawn);
                }

                SomeThingsFloat.LogMessage($"{pawn} is swimming, ignoring fall check");
                return;
            }

            var manipulation = Math.Max(pawn.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation), 0.1f);

            if (manipulation >= SomeThingsFloatMod.Instance.Settings.ManipulationThreshold)
            {
                if (lostFootingHediff != null)
                {
                    toClearLostFooting.Enqueue(pawn);
                }

                SomeThingsFloat.LogMessage($"{pawn} has too high manipulation value: {manipulation}");
                return;
            }

            var manipulationFiltered = Math.Max(Math.Min(manipulation, 0.999f), 0.5f);

            if (SomeThingsFloat.ShallowTerrainDefs.Contains(pawn.Position.GetTerrain(map)))
            {
                SomeThingsFloat.LogMessage($"{pawn} is in shallow waters");
                if (SomeThingsFloatMod.Instance.Settings.RelativeChanceInShallows == 0)
                {
                    if (lostFootingHediff != null)
                    {
                        toClearLostFooting.Enqueue(pawn);
                    }

                    return;
                }

                manipulationFiltered += (1 - manipulationFiltered) *
                                        (1 - SomeThingsFloatMod.Instance.Settings.RelativeChanceInShallows);
            }

            var rand = Rand.Value;
            if (rand < manipulationFiltered ||
                pawn.story?.traits?.HasTrait(TraitDef.Named("Nimble")) == true && Rand.Bool)
            {
                if (lostFootingHediff != null)
                {
                    toClearLostFooting.Enqueue(pawn);
                }

                return;
            }

            SomeThingsFloat.LogMessage($"{pawn} failed the Manipulation-check ({manipulationFiltered}/{rand})");

            if (lostFootingHediff != null)
            {
                var newSeverity = Math.Min(1f, lostFootingHediff.Severity + (0.05f / manipulationFiltered));
                toSetLostFooting.Enqueue((pawn, newSeverity));
            }
            else
            {
                var initialSeverity = 0.1f / manipulationFiltered;
                toAddLostFooting.Enqueue((pawn, initialSeverity));
            }

            // If downed and awake, don't start floating
            if (pawn.Downed && pawn.Awake())
            {
                return;
            }

            if (!pawn.RaceProps.IsFlesh)
            {
                return;
            }

            var floatValue = SomeThingsFloat.GetFloatingValue(pawn);
            if (!(floatValue > 0))
            {
                return;
            }

            var notify = !SomeThingsFloatMod.Instance.Settings.NoNotifications && pawn.Faction?.IsPlayer == true;
            toStartFloating.Enqueue((pawn, floatValue, notify));
        });

        // COMMIT PHASE (main thread)
        while (toClearLostFooting.TryDequeue(out var pawnToClear))
        {
            var h = pawnToClear?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.STF_LostFooting);
            if (h != null)
            {
                h.Severity = 0;
            }
        }

        while (toSetLostFooting.TryDequeue(out var entry))
        {
            var (pawn, severity) = entry;
            var h = pawn?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.STF_LostFooting);
            if (h != null)
            {
                h.Severity = severity;
            }
        }

        while (toAddLostFooting.TryDequeue(out var entryAdd))
        {
            var (pawn, severity) = entryAdd;
            var h = pawn?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.STF_LostFooting);
            if (h != null || pawn?.health == null)
            {
                continue;
            }

            var hediff = HediffMaker.MakeHediff(HediffDefOf.STF_LostFooting, pawn);
            hediff.Severity = severity;
            pawn.health.AddHediff(hediff);
        }

        while (toStartFloating.TryDequeue(out var f))
        {
            var (pawn, value, notify) = f;
            floatingValues[pawn] = value;
            setNextUpdateTime(pawn, value);
            if (notify)
            {
                Messages.Message("STF.PawnHasFallenAndFloats".Translate(pawn.NameFullColored), pawn,
                    MessageTypeDefOf.NegativeEvent);
            }
        }
    }

    private void checkForPawnsThatCanDrown()
    {
        // Staging queues
        toClearDrowning.Clear();
        toIncreaseDrowning.Clear();
        toAddDrowning.Clear();

        // Water cells accessed from multiple threads, must be accessed read-only.
        // Again, ideally these should be IReadOnlySet.
        var waterCellsSnapshot = cellsWithWater;

        Parallel.ForEach(mapPawns, pawn =>
        {
            if (pawn == null || !pawn.IsHashIntervalTick(GenTicks.TickRareInterval))
            {
                return;
            }

            if (pawn.Dead ||
                SomeThingsFloat.PawnsThatBreathe?.Contains(pawn.def) == false ||
                SomeThingsFloat.AquaticRaces.Contains(pawn.def) ||
                SomeThingsFloat.Vehicles.Contains(pawn.def) ||
                !pawn.Downed)
            {
                return;
            }

            var inShallowWater = false;
            var drowningHediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.STF_Drowning);
            if (pawn.Spawned)
            {
                if (!waterCellsSnapshot.Any() || !waterCellsSnapshot.Contains(pawn.Position))
                {
                    if (drowningHediff != null)
                    {
                        toClearDrowning.Enqueue(pawn);
                    }

                    return;
                }

                inShallowWater = SomeThingsFloat.ShallowTerrainDefs.Contains(pawn.Position.GetTerrain(map));
            }
            else
            {
                if (hiddenPositions?.TryGetValue(pawn, out _) == false)
                {
                    return;
                }
            }

            var cannotDrown =
                pawn.apparel?.WornApparel?.Any(apparel =>
                    SomeThingsFloat.ApparelThatPreventDrowning.Contains(apparel.def)) == true || !pawn.HarmedByVacuum;

            var isSwimming = pawn.CurJobDef == JobDefOf.GoSwimming;

            if (drowningHediff != null)
            {
                if (cannotDrown || inShallowWater || isSwimming)
                {
                    toClearDrowning.Enqueue(pawn);
                    return;
                }

                var delta = SomeThingsFloat.CalculateDrowningValue(pawn);
                toIncreaseDrowning.Enqueue((pawn, delta));
            }
            else
            {
                if (cannotDrown || inShallowWater || isSwimming)
                {
                    return;
                }

                var initial = SomeThingsFloat.CalculateDrowningValue(pawn);
                var shouldNotify = !SomeThingsFloatMod.Instance.Settings.NoNotifications && (
                    SomeThingsFloatMod.Instance.Settings.WarnForAllFriendlyPawns && pawn.Faction?.IsPlayer == true ||
                    !SomeThingsFloatMod.Instance.Settings.WarnForAllFriendlyPawns && pawn.Faction?.IsPlayer == true);

                // The original condition was a bit nuanced; above approximates same behavior:
                // notify only for player pawns, honoring the setting for "all friendly".
                toAddDrowning.Enqueue((pawn, initial, shouldNotify));
            }
        });

        // COMMIT PHASE (main thread)
        while (toClearDrowning.TryDequeue(out var pawnToClear))
        {
            var h = pawnToClear?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.STF_Drowning);
            if (h != null)
            {
                h.Severity = 0;
            }

            // Count after commit
            var chk = pawnToClear?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.STF_Drowning);
            if (!(chk?.Severity >= 1))
            {
                continue;
            }

            lock (this)
            {
                EnemyPawnsDrowned++;
            }
        }

        while (toIncreaseDrowning.TryDequeue(out var inc))
        {
            var (pawn, delta) = inc;
            var h = pawn?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.STF_Drowning);
            if (h != null)
            {
                h.Severity += delta;
            }

            var chk = pawn?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.STF_Drowning);
            if (!(chk?.Severity >= 1))
            {
                continue;
            }

            lock (this)
            {
                EnemyPawnsDrowned++;
            }
        }

        while (toAddDrowning.TryDequeue(out var add))
        {
            var (pawn, initial, notify) = add;
            if (pawn?.health != null)
            {
                var h = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.STF_Drowning);
                if (h == null)
                {
                    var hediff = HediffMaker.MakeHediff(HediffDefOf.STF_Drowning, pawn);
                    hediff.Severity = initial;
                    pawn.health.AddHediff(hediff);

                    if (notify && pawn.Faction?.IsPlayer == true)
                    {
                        Find.TickManager.slower.SignalForceNormalSpeed();
                        Messages.Message("STF.PawnIsDrowning".Translate(pawn.NameFullColored), pawn,
                            MessageTypeDefOf.ThreatBig);
                    }
                }
            }

            var chk = pawn?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.STF_Drowning);
            if (!(chk?.Severity >= 1))
            {
                continue;
            }

            lock (this)
            {
                EnemyPawnsDrowned++;
            }
        }
    }

    private bool tryToFindNewPosition(Thing thing, out IntVec3 resultingCell)
    {
        if (isSpace)
        {
            if (!spaceDirections.TryGetValue(thing, out var direction))
            {
                spaceDirections[thing] =
                    validDirections.Where(vec3 => (vec3 + thing.Position).InBounds(map)).RandomElement();
                direction = spaceDirections[thing];
            }

            resultingCell = thing.Position + direction;
            if (!resultingCell.InBounds(map))
            {
                resultingCell = IntVec3.Invalid;
                return true;
            }

            if (GenPlace.HaulPlaceBlockerIn(thing, resultingCell, map, true) == null)
            {
                return true;
            }

            SomeThingsFloat.LogMessage($"{resultingCell} position has stuff in the way, bouncing", debug: true);
            spaceDirections[thing] =
                validDirections[(validDirections.IndexOf(spaceDirections[thing]) + 4) % 8];
            resultingCell = thing.Position + spaceDirections[thing];
            return true;
        }

        if (hiddenPositions == null || !hiddenPositions.TryGetValue(thing, out var originalPosition))
        {
            originalPosition = thing.Position;
            var foundBuilding = originalPosition.GetFirstBuilding(map);
            if (foundBuilding != null &&
                (foundBuilding.def != ThingDefOf.STF_Bars || SomeThingsFloat.IsLargeThing(thing)) &&
                !foundBuilding.def.IsBlueprint &&
                !foundBuilding.def.IsFrame &&
                !ignoredAltitudeLayers.Contains(foundBuilding.def.altitudeLayer))
            {
                SomeThingsFloat.LogMessage($"{thing} is on something else, assuming it should not move");
                resultingCell = originalPosition;
                return false;
            }
        }

        resultingCell = originalPosition;

        var originalFlow = Vector3.zero;
        if (cellsWithRiver.Contains(originalPosition) || hiddenPositions?.Values.Contains(originalPosition) == true)
        {
            originalFlow = map.waterInfo.GetWaterMovement(resultingCell.ToVector3Shifted());
        }

        SomeThingsFloat.LogMessage($"Flow at {thing} position: {originalFlow}", debug: true);

        var possibleCellsToRecheck = new List<IntVec3>();

        var surroundingCells = new CellRect(originalPosition.x, originalPosition.z, 1, 1).AdjacentCells;

        foreach (var adjacentCell in surroundingCells.InRandomOrder())
        {
            var adjacentCellRelative = adjacentCell - originalPosition;
            if (originalFlow != Vector3.zero)
            {
                if (adjacentCellRelative.x * originalFlow.x < 0 || adjacentCellRelative.z * originalFlow.z < 0)
                {
                    possibleCellsToRecheck.Add(adjacentCell);
                    SomeThingsFloat.LogMessage(
                        $"{adjacentCell} position compared to original flow {originalFlow} is not the right way",
                        debug: true);
                    continue;
                }
            }

            if (!adjacentCell.InBounds(map))
            {
                if (SomeThingsFloatMod.Instance.Settings.DespawnAtMapEdge &&
                    !Messages.liveMessages.Any(message =>
                        message.lookTargets?.targets.Contains(thing) == true))
                {
                    resultingCell = IntVec3.Invalid;
                    return true;
                }

                continue;
            }

            if (!cellsWithWater.Contains(adjacentCell) && !underCellsWithWater.Contains(adjacentCell) &&
                !cellsWithNothing.Contains(adjacentCell))
            {
                SomeThingsFloat.LogMessage($"{adjacentCell} is not floatable", debug: true);

                if (SomeThingsFloatMod.Instance.Settings.AllowStranding && Rand.Chance(0.05f))
                {
                    if (GenPlace.HaulPlaceBlockerIn(thing, adjacentCell, map, true) == null)
                    {
                        SomeThingsFloat.LogMessage($"Will strand on {adjacentCell}", debug: true);
                        resultingCell = adjacentCell;
                        return true;
                    }
                }

                continue;
            }

            if (GenPlace.HaulPlaceBlockerIn(thing, adjacentCell, map, true) != null &&
                !underCellsWithWater.Contains(adjacentCell))
            {
                var buildingDef = adjacentCell.GetFirstBuilding(map)?.def;

                if (buildingDef != null && buildingDef != ThingDefOf.STF_Bars && buildingDef != ThingDefOf.STF_Net &&
                    !buildingDef.IsBlueprint && !buildingDef.IsFrame)
                {
                    SomeThingsFloat.LogMessage($"{adjacentCell} position has stuff in the way", debug: true);
                    continue;
                }
            }

            var itemInCell = adjacentCell.GetFirstItem(map);
            if (itemInCell != null && (itemInCell.def != thing.def || thing.def.stackLimit < 2))
            {
                SomeThingsFloat.LogMessage($"{adjacentCell} position has an unstackable item in the way", debug: true);
                continue;
            }

            SomeThingsFloat.LogMessage($"Cell {adjacentCell} for {thing} was valid", debug: true);
            resultingCell = adjacentCell;
            return true;
        }

        foreach (var adjacentCell in possibleCellsToRecheck.InRandomOrder())
        {
            var adjacentCellRelative = adjacentCell - originalPosition;
            if (originalFlow != Vector3.zero)
            {
                if (adjacentCellRelative.x * originalFlow.x < 0 && adjacentCellRelative.z * originalFlow.z < 0)
                {
                    SomeThingsFloat.LogMessage(
                        $"{adjacentCell} position compared to original flow {originalFlow} is really not the right way",
                        debug: true);
                    continue;
                }
            }

            if (!adjacentCell.InBounds(map))
            {
                if (SomeThingsFloatMod.Instance.Settings.DespawnAtMapEdge)
                {
                    resultingCell = IntVec3.Invalid;
                    return true;
                }

                continue;
            }

            if (!cellsWithWater.Contains(adjacentCell) && !underCellsWithWater.Contains(adjacentCell) &&
                !cellsWithNothing.Contains(adjacentCell))
            {
                SomeThingsFloat.LogMessage($"{adjacentCell} is not in water", debug: true);
                continue;
            }

            if (GenPlace.HaulPlaceBlockerIn(thing, adjacentCell, map, true) != null)
            {
                var buildingDef = adjacentCell.GetFirstBuilding(map)?.def;

                if (buildingDef != null && buildingDef != ThingDefOf.STF_Bars && buildingDef != ThingDefOf.STF_Net &&
                    !buildingDef.IsBlueprint && !buildingDef.IsFrame)
                {
                    SomeThingsFloat.LogMessage($"{adjacentCell} position has stuff in the way", debug: true);
                    continue;
                }
            }

            SomeThingsFloat.LogMessage($"Cell {adjacentCell} for {thing} was valid", debug: true);
            resultingCell = adjacentCell;
            return true;
        }

        return false;
    }

    public void UnSpawnedDeterioration(IntVec3 c)
    {
        var (thing, _) = hiddenPositions.FirstOrFallback(pair => pair.Value == c,
            new KeyValuePair<Thing, IntVec3>(null, IntVec3.Invalid));
        if (thing == null)
        {
            return;
        }

        float num;
        if (thing is Corpse corpse && corpse.InnerPawn.apparel != null)
        {
            var wornApparel = corpse.InnerPawn.apparel.WornApparel;
            // ReSharper disable once ForCanBeConvertedToForeach Can destroy
            for (var i = 0; i < wornApparel.Count; i++)
            {
                var apparel = wornApparel[i];
                if (!apparel.def.CanEverDeteriorate)
                {
                    continue;
                }

                num = unspawnedDeteriorationRate(thing);

                if (num < 0.001f)
                {
                    continue;
                }

                if (Rand.Chance(num / 36f))
                {
                    thing.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, 1f));
                }
            }
        }

        if (!thing.def.CanEverDeteriorate)
        {
            return;
        }

        if (ModsConfig.BiotechActive && thing is Genepack { Deteriorating: false })
        {
            return;
        }

        num = unspawnedDeteriorationRate(thing);
        if (num < 0.001f)
        {
            return;
        }

        thing.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, 1f));
    }

    private float unspawnedDeteriorationRate(Thing thing)
    {
        if (thing == null)
        {
            return 0f;
        }

        var num = thing.GetStatValue(StatDefOf.DeteriorationRate, false);

        if (!thing.def.deteriorateFromEnvironmentalEffects || hiddenPositions == null ||
            !hiddenPositions.TryGetValue(thing, out var position))
        {
            return num;
        }

        var underTerrain = map.terrainGrid?.UnderTerrainAt(position);

        num += StatDefOf.DeteriorationRate.GetStatPart<StatPart_EnvironmentalEffects>().factorOffsetUnroofed;
        num += StatDefOf.DeteriorationRate.GetStatPart<StatPart_EnvironmentalEffects>().factorOffsetOutdoors;
        if (underTerrain != null)
        {
            num *= underTerrain.extraDeteriorationFactor;
        }

        return num;
    }

    public void ClearEdgeCells()
    {
        mapEdgeCells.Clear();
    }

    public List<Pawn> DownedPawnsInWater()
    {
        return mapPawns.Where(pawn =>
            pawn is { Downed: true } && pawn.Awake() && floatingValues.ContainsKey(pawn) &&
            (pawn.Spawned && cellsWithRiver?.Contains(pawn.Position) == true ||
             cellsWithOcean?.Contains(pawn.Position) == true ||
             hiddenPositions?.TryGetValue(pawn, out _) == true)).ToList();
    }

    public Dictionary<Thing, IntVec3> ThingsUnderBridge()
    {
        return hiddenPositions ?? new Dictionary<Thing, IntVec3>();
    }

    public bool VerifyThingIsInWater(Thing thing)
    {
        if (thing == null)
        {
            return false;
        }

        if (hiddenPositions?.ContainsKey(thing) == true)
        {
            return true;
        }

        if (thing is not { Spawned: true })
        {
            return false;
        }

        if (!thing.Position.InBounds(thing.Map))
        {
            return false;
        }

        return cellsWithWater?.Contains(thing.Position) == true;
    }

    public Vector3 GetNewCenter(Thing thing, Vector3 center)
    {
        var newPosition = center;
        if (thing == null)
        {
            return newPosition;
        }

        var lastPosition = lastPositions.TryGetValue(thing);
        if (lastPosition == null)
        {
            return newPosition;
        }

        if (lastPosition.Item2 == thing.Position)
        {
            return SomeThingsFloat.AddWave(newPosition, thing.thingIDNumber);
        }

        var xDifference = thing.Position.x - lastPosition.Item2.x;
        var zDifference = thing.Position.z - lastPosition.Item2.z;

        if (xDifference > 1.9f || xDifference < -1.9f || zDifference > 1.9f || zDifference < -1.9f)
        {
            return SomeThingsFloat.AddWave(newPosition, thing.thingIDNumber);
        }

        var nextMove =
            updateValues.FirstOrFallback(pair => pair.Value == thing, new KeyValuePair<int, Thing>(-1, null));

        if (nextMove.Value == null)
        {
            return SomeThingsFloat.AddWave(newPosition, thing.thingIDNumber);
        }

        var percentMoved = 1 - ((GenTicks.TicksGame - lastPosition.Item1) / (float)(nextMove.Key - lastPosition.Item1));

        newPosition.x -= xDifference * percentMoved;
        newPosition.z -= zDifference * percentMoved;

        newPosition = SomeThingsFloat.AddWave(newPosition, thing.thingIDNumber);

        return newPosition;
    }

    public List<Thing> GetFloatingThingsNear(Vector3 clickPoint, List<Thing> currentList,
        TargetingParameters clickParams, ITargetingSource source)
    {
        foreach (var thing in lastPositions.Keys)
        {
            if (currentList.Contains(thing))
            {
                continue;
            }

            var difference = clickPoint - thing.TrueCenter();
            if (!(GenMath.Sqrt((difference.x * difference.x) + (difference.z * difference.z)) < 0.5f))
            {
                continue;
            }

            if (!clickParams.CanTarget(thing, source))
            {
                continue;
            }

            currentList.Add(thing);
        }

        return currentList;
    }
}