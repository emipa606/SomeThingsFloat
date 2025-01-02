using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SomeThingsFloat;

public class FloatingThings_MapComponent : MapComponent
{
    private readonly HashSet<AltitudeLayer> ignoredAltitudeLayers;
    private readonly HashSet<IntVec3> mapEdgeCells;
    private HashSet<IntVec3> cellsWithRiver;
    private HashSet<IntVec3> cellsWithWater;
    public int EnemyPawnsDrowned;
    private Dictionary<Thing, float> floatingValues;
    private Dictionary<Thing, IntVec3> hiddenPositions;
    private List<Thing> hiddenPositionsKeys;
    private List<IntVec3> hiddenPositionsValues;
    private Dictionary<Thing, Tuple<int, IntVec3>> lastPositions;
    private int lastSpawnTick;
    private List<Pawn> mapPawns;
    private HashSet<IntVec3> underCellsWithWater;
    private Dictionary<int, Thing> updateValues;
    private List<int> updateValuesKeys;
    private List<Thing> updateValuesValues;
    public int WastePacksFloated;

    public FloatingThings_MapComponent(Map map) : base(map)
    {
        this.map = map;
        SomeThingsFloat.FloatingMapComponents[map] = this;
        underCellsWithWater = [];
        cellsWithWater = [];
        cellsWithRiver = [];
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
    }

    public override void MapComponentTick()
    {
        var ticksGame = GenTicks.TicksGame;
        if (ticksGame % GenTicks.TickLongInterval == 0)
        {
            SomeThingsFloat.LogMessage("Doing TickLongInterval calls", debug: true);
            updateListOfWaterCells();
            TrySpawnThingAtMapEdge();
            updateListOfFloatingThings();
        }

        if (mapPawns == null || ticksGame % GenTicks.TickLongInterval == 0)
        {
            mapPawns = map.mapPawns.AllPawns;
        }

        if (SomeThingsFloatMod.instance.Settings.PawnsCanFall)
        {
            checkForPawnsThatCanFall();
        }

        if (SomeThingsFloatMod.instance.Settings.DownedPawnsDrown)
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
                    if (floatingValues[hiddenPositionsKey] == 0)
                    {
                        thingsToRespawn.Add(hiddenPositionsKey);
                    }
                }
            }

            foreach (var destroyedThing in thingsDestroyed)
            {
                SomeThingsFloat.LogMessage($"{destroyedThing} is destroyed, removing");
                hiddenPositions.Remove(destroyedThing);
                if (lastPositions.ContainsKey(destroyedThing))
                {
                    lastPositions.Remove(destroyedThing);
                }
            }

            foreach (var respawningThing in thingsToRespawn)
            {
                var radius = 1;
                IntVec3 spawnCell;
                while (!CellFinder.TryFindRandomCellNear(hiddenPositions[respawningThing], map, radius,
                           cellsWithWater.Contains, out spawnCell))
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

        if ((SomeThingsFloatMod.instance.Settings.Bobbing || SomeThingsFloatMod.instance.Settings.SmoothAnimation) &&
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

        if (lastPositions.ContainsKey(thing))
        {
            lastPositions.Remove(thing);
        }

        if (!VerifyThingIsInWater(thing))
        {
            SomeThingsFloat.LogMessage($"{thing} is no longer floating");
            floatingValues.Remove(thing);
            return;
        }


        if (SomeThingsFloatMod.instance.Settings.ReservedItemsWillNotMove)
        {
            if (map.reservationManager.AllReservedThings().Contains(thing))
            {
                SomeThingsFloat.LogMessage($"{thing} will not move since its reserved");
                setNextUpdateTime(thing, true);
                return;
            }

            SomeThingsFloat.LogMessage($"{thing} is not reserved", debug: true);
        }

        if (!tryToFindNewPostition(thing, out var newPosition))
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
            if (SomeThingsFloatMod.instance.Settings.ForbidWhenMoving)
            {
                wasInStorage = thing.IsInValidStorage();
            }

            thing.DeSpawn();
        }
        else
        {
            wasUnspawned = true;
        }

        if (newPosition == IntVec3.Invalid)
        {
            if (thing is Pawn { IsColonist: true } pawn)
            {
                Find.LetterStack.ReceiveLetter("STF.PawnIsLostTitle".Translate(pawn.NameFullColored),
                    "STF.PawnIsLostMessage".Translate(pawn.NameFullColored), LetterDefOf.Death);

                PawnDiedOrDownedThoughtsUtility.TryGiveThoughts(pawn, null, PawnDiedOrDownedThoughtsKind.Lost);
            }

            floatingValues?.Remove(thing);

            if (thing.def == RimWorld.ThingDefOf.Wastepack)
            {
                WastePacksFloated += thing.stackCount;
                var neighbor = Find.World.grid[map.Tile].Rivers.FirstOrDefault().neighbor;
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

            if (lastPositions.ContainsKey(thing))
            {
                lastPositions.Remove(thing);
            }

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

            if (lastPositions.ContainsKey(thing))
            {
                lastPositions.Remove(thing);
            }

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
            if (SomeThingsFloatMod.instance.Settings.HaulUrgently && wasUnspawned &&
                SomeThingsFloat.HaulUrgentlyDef != null)
            {
                map.designationManager.AddDesignation(new Designation(thing, SomeThingsFloat.HaulUrgentlyDef));
            }

            if (SomeThingsFloatMod.instance.Settings.AllowOnStuck)
            {
                var buidingDef = newPosition.GetFirstBuilding(map)?.def;
                if (buidingDef != null && (buidingDef == ThingDefOf.STF_Bars && SomeThingsFloat.IsLargeThing(thing) ||
                                           buidingDef == ThingDefOf.STF_Net))
                {
                    thing.SetForbidden(false, false);
                }
            }
        }

        if (SomeThingsFloatMod.instance.Settings.ForbidWhenMoving && wasInStorage != thing.IsInValidStorage())
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
        updateListOfWaterCells();
        updateListOfFloatingThings();
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref lastSpawnTick, "lastSpawnTick");
        Scribe_Values.Look(ref WastePacksFloated, "WastePacksFloated");
        Scribe_Values.Look(ref EnemyPawnsDrowned, "EnemyPawnsDrowned");
        Scribe_Collections.Look(ref cellsWithWater, "cellsWithWater", LookMode.Value);
        Scribe_Collections.Look(ref cellsWithRiver, "cellsWithRiver", LookMode.Value);
        Scribe_Collections.Look(ref underCellsWithWater, "underCellsWithWater", LookMode.Value);
        Scribe_Collections.Look(ref updateValues, "updateValues", LookMode.Value, LookMode.Reference,
            ref updateValuesKeys, ref updateValuesValues);
        Scribe_Collections.Look(ref hiddenPositions, "hiddenPositions", LookMode.Deep, LookMode.Value,
            ref hiddenPositionsKeys, ref hiddenPositionsValues);
        if (Scribe.mode != LoadSaveMode.ResolvingCrossRefs)
        {
            return;
        }

        if (floatingValues == null)
        {
            floatingValues = new Dictionary<Thing, float>();
        }

        if (hiddenPositions == null)
        {
            hiddenPositions = new Dictionary<Thing, IntVec3>();
        }

        if (updateValues == null)
        {
            updateValues = new Dictionary<int, Thing>();
        }

        if (lastPositions == null)
        {
            lastPositions = new Dictionary<Thing, Tuple<int, IntVec3>>();
        }

        if (cellsWithWater == null)
        {
            cellsWithWater = [];
        }

        if (cellsWithRiver == null)
        {
            cellsWithRiver = [];
        }

        if (underCellsWithWater == null)
        {
            underCellsWithWater = [];
        }

        updateListOfWaterCells();
        updateListOfFloatingThings();
    }

    private void updateListOfWaterCells()
    {
        SomeThingsFloat.LogMessage("Updating water-cells", debug: true);
        cellsWithWater = [];
        cellsWithRiver = [];
        underCellsWithWater = [];

        for (var i = 0; i < map.terrainGrid.topGrid.Length; i++)
        {
            if (map.terrainGrid.topGrid[i].defName.ToLower().Contains("bridge"))
            {
                if (!SomeThingsFloatMod.instance.Settings.FloatUnderBridges)
                {
                    continue;
                }

                if (map.terrainGrid.topGrid[i].IsWater || map.terrainGrid.underGrid[i].IsWater)
                {
                    underCellsWithWater.Add(map.cellIndices.IndexToCell(i));
                }

                continue;
            }

            if (map.terrainGrid.topGrid[i].IsWater)
            {
                cellsWithWater.Add(map.cellIndices.IndexToCell(i));
            }

            if (map.terrainGrid.topGrid[i].IsRiver)
            {
                cellsWithRiver.Add(map.cellIndices.IndexToCell(i));
            }
        }

        SomeThingsFloat.LogMessage($"Found {cellsWithWater.Count} water-cells");
        SomeThingsFloat.LogMessage($"Found {cellsWithRiver.Count} river-cells");
        SomeThingsFloat.LogMessage($"Found {underCellsWithWater.Count} water-cells under bridges");
    }

    public bool TrySpawnThingAtMapEdge(bool force = false)
    {
        if (!SomeThingsFloatMod.instance.Settings.SpawnNewItems)
        {
            return false;
        }

        if (!cellsWithWater.Any())
        {
            SomeThingsFloat.LogMessage("No water cells to spawn in", debug: true);
            return false;
        }

        if (!force && Rand.Value < 0.9f)
        {
            return false;
        }

        if (!force && lastSpawnTick + SomeThingsFloatMod.instance.Settings.MinTimeBetweenItems > GenTicks.TicksGame)
        {
            SomeThingsFloat.LogMessage(
                $"Not time to spawn yet, next spawn: {lastSpawnTick + SomeThingsFloatMod.instance.Settings.MinTimeBetweenItems}, current time {GenTicks.TicksGame}",
                debug: true);
            return false;
        }

        if (!mapEdgeCells.Any())
        {
            var possibleMapEdgeCells = cellsWithWater.Intersect(CellRect.WholeMap(map).EdgeCells);

            if (!possibleMapEdgeCells.Any())
            {
                SomeThingsFloat.LogMessage("No possible edge cells to spawn in", debug: true);
                return false;
            }

            foreach (var mapEdgeCell in possibleMapEdgeCells)
            {
                if (SomeThingsFloatMod.instance.Settings.SpawnInOceanTiles &&
                    mapEdgeCell.GetTerrain(map)?.defName.ToLower().Contains("ocean") == true)
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
            SomeThingsFloat.LogMessage("Found no map-edge cells with river");
            return false;
        }

        var cellToPlaceIt = mapEdgeCells.RandomElement();

        // Sometimes we spawn a pawn or corpse
        if (Rand.Value > 0.9f)
        {
            var currentMarketValue = 0f;
            var pawnKindDef = (from kindDef in DefDatabase<PawnKindDef>.AllDefs
                    where kindDef.RaceProps.IsFlesh && kindDef.defaultFactionType is not { isPlayer: true }
                                                    && !SomeThingsFloat.AquaticRaces.Contains(kindDef.race)
                                                    && !SomeThingsFloat.Vehicles.Contains(kindDef.race)
                    select kindDef)
                .RandomElement();
            Faction faction = null;
            if (pawnKindDef.defaultFactionType != null)
            {
                faction = Find.World.factionManager.AllFactions
                    .Where(factionType => factionType.def == pawnKindDef.defaultFactionType).RandomElement();
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

            if (!SomeThingsFloatMod.instance.Settings.SpawnLivingPawns || Rand.Value > 0.1f)
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
                            SomeThingsFloatMod.instance.Settings.MaxSpawnValue)
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

                pawn.Corpse.SetForbidden(SomeThingsFloatMod.instance.Settings.ForbidSpawningItems, false);
                if (SomeThingsFloat.HaulUrgentlyDef != null && SomeThingsFloatMod.instance.Settings.HaulUrgently)
                {
                    map.designationManager.AddDesignation(new Designation(pawn.Corpse,
                        SomeThingsFloat.HaulUrgentlyDef));
                }

                if (SomeThingsFloatMod.instance.Settings.NotifyOfSpawningItems)
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
            if (!pawn.RaceProps.Animal)
            {
                Find.LetterStack.ReceiveLetter("STF.PawnSpawnedTitle".Translate(), "STF.PawnSpawnedMessage".Translate(),
                    LetterDefOf.NeutralEvent, pawn);
            }
            else
            {
                if (SomeThingsFloatMod.instance.Settings.NotifyOfSpawningItems)
                {
                    Messages.Message("STF.ThingsFloatedIntoTheMap".Translate(pawn.NameFullColored), pawn,
                        MessageTypeDefOf.NeutralEvent);
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
                if (!SomeThingsFloatMod.instance.Settings.SpawnFertilizedEggs)
                {
                    return def.BaseMarketValue <= SomeThingsFloatMod.instance.Settings.MaxSpawnValue &&
                           def.thingCategories?.Contains(ThingCategoryDefOf.EggsFertilized) == false;
                }

                return def.BaseMarketValue <= SomeThingsFloatMod.instance.Settings.MaxSpawnValue;
            }).RandomElementByWeight(def => def.generateCommonality);
        var amountToSpawn =
            (int)Math.Floor(SomeThingsFloatMod.instance.Settings.MaxSpawnValue / thingToMake.BaseMarketValue);

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

        thing.SetForbidden(SomeThingsFloatMod.instance.Settings.ForbidSpawningItems, false);
        if (SomeThingsFloat.HaulUrgentlyDef != null && SomeThingsFloatMod.instance.Settings.HaulUrgently)
        {
            map.designationManager.AddDesignation(new Designation(thing, SomeThingsFloat.HaulUrgentlyDef));
        }

        if (SomeThingsFloatMod.instance.Settings.NotifyOfSpawningItems)
        {
            Messages.Message(
                cellToPlaceIt.GetTerrain(map)?.defName.ToLower().Contains("ocean") == true
                    ? "STF.ThingsFloatedInFromTheOcean".Translate(thing.LabelCap)
                    : "STF.ThingsFloatedIntoTheMap".Translate(thing.LabelCap), thing,
                MessageTypeDefOf.NeutralEvent);
        }


        lastPositions[thing] = new Tuple<int, IntVec3>(GenTicks.TicksGame, thing.Position);

        if (!force)
        {
            lastSpawnTick = GenTicks.TicksGame;
        }

        return true;
    }

    private void updateListOfFloatingThings()
    {
        if (floatingValues == null)
        {
            floatingValues = new Dictionary<Thing, float>();
        }

        foreach (var possibleThings in cellsWithWater.Select(vec3 => SomeThingsFloat.GetThingsAndPawns(vec3, map)))
        {
            foreach (var possibleThing in possibleThings)
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

                floatingValues[possibleThing] = SomeThingsFloat.GetFloatingValue(possibleThing);
                SomeThingsFloat.LogMessage($"{possibleThing} float-value: {floatingValues[possibleThing]}",
                    debug: true);
                if (!(floatingValues[possibleThing] > 0))
                {
                    continue;
                }

                setNextUpdateTime(possibleThing);
            }
        }

        foreach (var hiddenPosition in hiddenPositions)
        {
            var possibleThing = hiddenPosition.Key;
            if (possibleThing == null || possibleThing is not Pawn && floatingValues.ContainsKey(possibleThing))
            {
                continue;
            }

            SomeThingsFloat.LogMessage($"{possibleThing} float-value?", debug: true);
            floatingValues[possibleThing] = SomeThingsFloat.GetFloatingValue(possibleThing);
            SomeThingsFloat.LogMessage($"{possibleThing} float-value: {floatingValues[possibleThing]}", debug: true);
            if (!(floatingValues[possibleThing] > 0))
            {
                continue;
            }

            setNextUpdateTime(possibleThing);
        }

        SomeThingsFloat.LogMessage($"Found {floatingValues.Count} items in water");
    }

    private void setNextUpdateTime(Thing thing, bool longTime = false)
    {
        if (thing == null)
        {
            return;
        }

        if (!floatingValues.ContainsKey(thing))
        {
            return;
        }

        if (updateValues.ContainsValue(thing))
        {
            return;
        }

        var timeIncrease = longTime ? 5 : 1;
        var nextupdate = GenTicks.TicksGame +
                         (int)Math.Round(
                             (GenTicks.TickRareInterval / floatingValues[thing] /
                                 SomeThingsFloatMod.instance.Settings.RelativeFloatSpeed * timeIncrease) +
                             Rand.Range(-10, 10));
        while (updateValues.ContainsKey(nextupdate))
        {
            nextupdate++;
        }

        SomeThingsFloat.LogMessage($"Current tick: {GenTicks.TicksGame}, {thing} next update: {nextupdate}",
            debug: true);
        updateValues[nextupdate] = thing;
    }

    private void checkForPawnsThatCanFall()
    {
        // ReSharper disable once ForCanBeConvertedToForeach, May change during execution
        for (var index = 0; index < mapPawns.Count; index++)
        {
            var pawn = mapPawns[index];

            if (!pawn.IsHashIntervalTick(GenTicks.TickRareInterval))
            {
                continue;
            }

            if (SomeThingsFloat.AquaticRaces.Contains(pawn.def) || SomeThingsFloat.Vehicles.Contains(pawn.def))
            {
                continue;
            }

            var lostFootingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.STF_LostFooting);
            if (pawn is not { Spawned: true } || pawn.Dead || pawn.CarriedBy != null)
            {
                if (lostFootingHediff != null)
                {
                    lostFootingHediff.Severity = 0;
                }

                continue;
            }

            if (cellsWithRiver?.Contains(pawn.Position) == false)
            {
                if (lostFootingHediff != null)
                {
                    lostFootingHediff.Severity = 0;
                }

                continue;
            }

            if (pawn.CurJobDef?.defName.ToLower().Contains("swim") == true)
            {
                if (lostFootingHediff != null)
                {
                    lostFootingHediff.Severity = 0;
                }

                SomeThingsFloat.LogMessage($"{pawn} is swimming, ignoring fall check");
                continue;
            }

            var manipulation = Math.Max(pawn.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation), 0.1f);

            if (manipulation >= SomeThingsFloatMod.instance.Settings.ManipulationThreshold)
            {
                if (lostFootingHediff != null)
                {
                    lostFootingHediff.Severity = 0;
                }

                SomeThingsFloat.LogMessage($"{pawn} has too high manipulation value: {manipulation}");
                continue;
            }

            var manipulationFiltered = Math.Max(Math.Min(manipulation, 0.999f), 0.5f);

            if (SomeThingsFloat.ShallowTerrainDefs.Contains(pawn.Position.GetTerrain(map)))
            {
                SomeThingsFloat.LogMessage($"{pawn} is in shallow waters");
                if (SomeThingsFloatMod.instance.Settings.RelativeChanceInShallows == 0)
                {
                    if (lostFootingHediff != null)
                    {
                        lostFootingHediff.Severity = 0;
                    }

                    continue;
                }

                manipulationFiltered += (1 - manipulationFiltered) *
                                        (1 - SomeThingsFloatMod.instance.Settings.RelativeChanceInShallows);
            }

            var rand = Rand.Value;
            if (rand < manipulationFiltered)
            {
                if (lostFootingHediff != null)
                {
                    lostFootingHediff.Severity = 0;
                }

                continue;
            }

            if (pawn.story?.traits?.HasTrait(TraitDef.Named("Nimble")) == true && Rand.Bool)
            {
                if (lostFootingHediff != null)
                {
                    lostFootingHediff.Severity = 0;
                }

                continue;
            }

            SomeThingsFloat.LogMessage($"{pawn} failed the Manipulation-check ({manipulationFiltered}/{rand})");

            if (lostFootingHediff != null)
            {
                lostFootingHediff.Severity = Math.Min(1f, lostFootingHediff.Severity + (0.05f / manipulationFiltered));
            }
            else
            {
                var hediff = HediffMaker.MakeHediff(HediffDefOf.STF_LostFooting, pawn);
                hediff.Severity = 0.1f / manipulationFiltered;
                pawn.health.AddHediff(hediff);
            }

            if (pawn.Downed && pawn.Awake())
            {
                continue;
            }

            if (!pawn.RaceProps.IsFlesh)
            {
                if (pawn.Faction?.IsPlayer == true)
                {
                    Messages.Message("STF.PawnHasFallen".Translate(pawn.NameFullColored), pawn,
                        MessageTypeDefOf.NegativeEvent);
                }

                continue;
            }

            floatingValues[pawn] = SomeThingsFloat.GetFloatingValue(pawn);
            setNextUpdateTime(pawn);

            if (pawn.Faction?.IsPlayer == true)
            {
                Messages.Message("STF.PawnHasFallenAndFloats".Translate(pawn.NameFullColored), pawn,
                    MessageTypeDefOf.NegativeEvent);
            }
        }
    }

    private void checkForPawnsThatCanDrown()
    {
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var index = 0; index < mapPawns.Count; index++)
        {
            var pawn = mapPawns[index];

            if (!pawn.IsHashIntervalTick(GenTicks.TickRareInterval))
            {
                continue;
            }

            if (pawn == null || pawn.Dead || SomeThingsFloat.PawnsThatBreathe?.Contains(pawn.def) == false ||
                SomeThingsFloat.AquaticRaces.Contains(pawn.def) || SomeThingsFloat.Vehicles.Contains(pawn.def) ||
                !pawn.Downed || !pawn.Awake())
            {
                continue;
            }

            var inShallowWater = false;
            if (pawn.Spawned)
            {
                if (cellsWithWater?.Contains(pawn.Position) == false)
                {
                    continue;
                }

                inShallowWater = SomeThingsFloat.ShallowTerrainDefs.Contains(pawn.Position.GetTerrain(map));
            }
            else
            {
                if (hiddenPositions?.TryGetValue(pawn, out _) == false)
                {
                    continue;
                }
            }


            var cannotDrown =
                pawn.apparel?.WornApparel?.Any(apparel =>
                    SomeThingsFloat.ApparelThatPreventDrowning.Contains(apparel.def)) == true;

            var drowningHediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.STF_Drowning);
            if (drowningHediff != null)
            {
                if (cannotDrown || inShallowWater)
                {
                    drowningHediff.Severity = 0;
                    return;
                }

                drowningHediff.Severity += SomeThingsFloat.CalculateDrowningValue(pawn);
            }
            else
            {
                if (cannotDrown || inShallowWater)
                {
                    return;
                }

                var hediff = HediffMaker.MakeHediff(HediffDefOf.STF_Drowning, pawn);
                hediff.Severity = SomeThingsFloat.CalculateDrowningValue(pawn);
                pawn.health?.AddHediff(hediff);
                if (!SomeThingsFloatMod.instance.Settings.WarnForAllFriendlyPawns && pawn.Faction?.IsPlayer != true ||
                    SomeThingsFloatMod.instance.Settings.WarnForAllFriendlyPawns &&
                    pawn.Faction.HostileTo(Faction.OfPlayer))
                {
                    continue;
                }

                Find.TickManager.TogglePaused();
                Messages.Message("STF.PawnIsDrowning".Translate(pawn.NameFullColored), pawn,
                    MessageTypeDefOf.ThreatBig);
            }

            if (drowningHediff?.Severity >= 1)
            {
                EnemyPawnsDrowned++;
            }
        }
    }

    private bool tryToFindNewPostition(Thing thing, out IntVec3 resultingCell)
    {
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
        if (cellsWithRiver.Contains(originalPosition) ||
            hiddenPositions?.Values.ToList().Contains(originalPosition) == true)
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
                if (SomeThingsFloatMod.instance.Settings.DespawnAtMapEdge &&
                    !Messages.liveMessages.Any(message =>
                        message.lookTargets?.targets.Contains(thing) == true))
                {
                    resultingCell = IntVec3.Invalid;
                    return true;
                }

                continue;
            }

            if (!cellsWithWater.Contains(adjacentCell) && !underCellsWithWater.Contains(adjacentCell))
            {
                SomeThingsFloat.LogMessage($"{adjacentCell} is not in water", debug: true);
                continue;
            }

            if (GenPlace.HaulPlaceBlockerIn(thing, adjacentCell, map, true) != null &&
                !underCellsWithWater.Contains(adjacentCell))
            {
                var buidingDef = adjacentCell.GetFirstBuilding(map)?.def;

                if (buidingDef != null && buidingDef != ThingDefOf.STF_Bars && buidingDef != ThingDefOf.STF_Net &&
                    !buidingDef.IsBlueprint && !buidingDef.IsFrame)
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
                if (SomeThingsFloatMod.instance.Settings.DespawnAtMapEdge)
                {
                    resultingCell = IntVec3.Invalid;
                    return true;
                }

                continue;
            }

            if (!cellsWithWater.Contains(adjacentCell) && !underCellsWithWater.Contains(adjacentCell))
            {
                SomeThingsFloat.LogMessage($"{adjacentCell} is not in water", debug: true);
                continue;
            }

            if (GenPlace.HaulPlaceBlockerIn(thing, adjacentCell, map, true) != null)
            {
                var buidingDef = adjacentCell.GetFirstBuilding(map)?.def;

                if (buidingDef != null && buidingDef != ThingDefOf.STF_Bars && buidingDef != ThingDefOf.STF_Net &&
                    !buidingDef.IsBlueprint && !buidingDef.IsFrame)
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
        var hiddenThing = hiddenPositions.FirstOrFallback(pair => pair.Value == c,
            new KeyValuePair<Thing, IntVec3>(null, IntVec3.Invalid));
        if (hiddenThing.Key == null)
        {
            return;
        }

        var thing = hiddenThing.Key;
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

        if (Rand.Chance(num / 36f))
        {
            thing.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, 1f));
        }
    }

    private float unspawnedDeteriorationRate(Thing thing)
    {
        if (thing == null)
        {
            return 0f;
        }

        var num = thing.GetStatValue(StatDefOf.DeteriorationRate, false);

        if (!thing.def.deteriorateFromEnvironmentalEffects)
        {
            return num;
        }

        if (hiddenPositions == null || !hiddenPositions.TryGetValue(thing, out var position))
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