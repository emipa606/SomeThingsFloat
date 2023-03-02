using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SomeThingsFloat;

public class FloatingThings_MapComponent : MapComponent
{
    private readonly Dictionary<Thing, float> floatingValues;
    private readonly List<IntVec3> mapEdgeCells;
    private List<IntVec3> cellsWithWater;
    private Dictionary<Thing, IntVec3> hiddenPositions;
    private List<Thing> hiddenPositionsKeys;
    private List<IntVec3> hiddenPositionsValues;
    private List<IntVec3> totalCellsWithWater;
    private List<IntVec3> underCellsWithWater;
    private Dictionary<int, Thing> updateValues;
    private List<int> updateValuesKeys;
    private List<Thing> updateValuesValues;

    public FloatingThings_MapComponent(Map map) : base(map)
    {
        this.map = map;
        SomeThingsFloat.FloatingMapComponents[map] = this;
        underCellsWithWater = new List<IntVec3>();
        totalCellsWithWater = new List<IntVec3>();
        cellsWithWater = new List<IntVec3>();
        mapEdgeCells = new List<IntVec3>();
        floatingValues = new Dictionary<Thing, float>();
        updateValues = new Dictionary<int, Thing>();
        updateValuesKeys = new List<int>();
        updateValuesValues = new List<Thing>();
        hiddenPositions = new Dictionary<Thing, IntVec3>();
        hiddenPositionsKeys = new List<Thing>();
        hiddenPositionsValues = new List<IntVec3>();
    }

    public override void MapComponentTick()
    {
        var ticksGame = GenTicks.TicksGame;
        if (ticksGame % GenTicks.TickLongInterval == 0)
        {
            updateListOfWaterCells();
            spawnThingAtMapEdge();
            updateListOfFloatingThings();
        }

        if (!updateValues.ContainsKey(ticksGame))
        {
            return;
        }

        var thing = updateValues[ticksGame];
        updateValues.Remove(ticksGame);
        if (!verifyThingIsInWater(thing))
        {
            SomeThingsFloat.LogMessage($"{thing} is no longer floating");
            if (thing != null)
            {
                floatingValues.Remove(thing);
            }

            return;
        }

        if (!tryToFindNewPostition(thing, out var newPosition, totalCellsWithWater))
        {
            SomeThingsFloat.LogMessage($"{thing} cannot find a new postition");
            setNextUpdateTime(thing);
            return;
        }

        var wasInStorage = false;
        if (!hiddenPositions.TryGetValue(thing, out var originalPosition))
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
            hiddenPositions.Remove(thing);
        }

        if (newPosition == IntVec3.Invalid)
        {
            if (thing is Pawn { IsColonist: true } pawn)
            {
                Find.LetterStack.ReceiveLetter("STF.PawnIsLostTitle".Translate(pawn.NameFullColored),
                    "STF.PawnIsLostMessage".Translate(pawn.NameFullColored), LetterDefOf.Death);

                PawnDiedOrDownedThoughtsUtility.TryGiveThoughts(pawn, null, PawnDiedOrDownedThoughtsKind.Lost);
            }

            floatingValues.Remove(thing);

            if (thing.def == ThingDefOf.Wastepack)
            {
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

            thing.Destroy();
            return;
        }

        setNextUpdateTime(thing);

        if (underCellsWithWater.Contains(newPosition))
        {
            hiddenPositions[thing] = newPosition;
            return;
        }

        if (!GenPlace.TryPlaceThing(thing, newPosition, map, ThingPlaceMode.Direct))
        {
            SomeThingsFloat.LogMessage($"{thing} could not be placed at its new position");
            GenPlace.TryPlaceThing(thing, originalPosition, map, ThingPlaceMode.Direct);
        }

        if (!SomeThingsFloatMod.instance.Settings.ForbidWhenMoving)
        {
            return;
        }

        if (wasInStorage != thing.IsInValidStorage())
        {
            thing.SetForbidden(wasInStorage, false);
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

        Scribe_Collections.Look(ref cellsWithWater, "cellsWithWater", LookMode.Value);
        Scribe_Collections.Look(ref totalCellsWithWater, "totalCellsWithWater", LookMode.Value);
        Scribe_Collections.Look(ref underCellsWithWater, "underCellsWithWater", LookMode.Value);
        Scribe_Collections.Look(ref updateValues, "updateValues", LookMode.Value, LookMode.Reference,
            ref updateValuesKeys, ref updateValuesValues);
        Scribe_Collections.Look(ref hiddenPositions, "hiddenPositions", LookMode.Reference, LookMode.Value,
            ref hiddenPositionsKeys, ref hiddenPositionsValues);
        if (Scribe.mode != LoadSaveMode.ResolvingCrossRefs)
        {
            return;
        }

        updateListOfFloatingThings();
        if (hiddenPositions == null)
        {
            hiddenPositions = new Dictionary<Thing, IntVec3>();
        }
    }

    private void updateListOfWaterCells()
    {
        var forceUpdate = Rand.Bool;
        SomeThingsFloat.LogMessage("Updating water-cells");
        if (cellsWithWater == null || !cellsWithWater.Any() || forceUpdate)
        {
            cellsWithWater = map.AllCells.Where(vec3 => vec3.GetTerrain(map).defName.Contains("Water")).ToList();
            SomeThingsFloat.LogMessage($"Found {cellsWithWater.Count} water-cells");
        }

        if (!SomeThingsFloatMod.instance.Settings.FloatUnderBridges)
        {
            totalCellsWithWater = cellsWithWater;
            return;
        }

        if (underCellsWithWater != null && underCellsWithWater.Any() && !forceUpdate)
        {
            return;
        }

        underCellsWithWater = map.AllCells
            .Where(vec3 => map.terrainGrid.UnderTerrainAt(vec3)?.defName.Contains("Water") == true).ToList();
        SomeThingsFloat.LogMessage($"Found {underCellsWithWater.Count} water-cells under bridges");
        if (underCellsWithWater.Any())
        {
            totalCellsWithWater = cellsWithWater.Union(underCellsWithWater).ToList();
        }
    }

    private void spawnThingAtMapEdge()
    {
        if (!SomeThingsFloatMod.instance.Settings.SpawnNewItems)
        {
            return;
        }

        if (!totalCellsWithWater.Any())
        {
            SomeThingsFloat.LogMessage("No water cells to spawn in");
            return;
        }

        if (Rand.Value < 0.97f)
        {
            return;
        }

        if (!mapEdgeCells.Any())
        {
            var possibleMapEdgeCells = totalCellsWithWater.Where(vec3 =>
                vec3.x == 0 || vec3.x == map.Size.x - 1 || vec3.z == 0 || vec3.z == map.Size.z - 1).ToList();

            if (!possibleMapEdgeCells.Any())
            {
                return;
            }

            foreach (var mapEdgeCell in possibleMapEdgeCells)
            {
                var flowAtCell = map.waterInfo.GetWaterMovement(mapEdgeCell.ToVector3Shifted());
                if (flowAtCell == Vector3.zero)
                {
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
                    SomeThingsFloat.LogMessage($"{mapEdgeCell} > {flowAtCell}");
                    continue;
                }

                mapEdgeCells.Add(mapEdgeCell);
            }
        }

        if (!mapEdgeCells.Any())
        {
            SomeThingsFloat.LogMessage("Found no map-edge cells with river");
            return;
        }

        var cellToPlaceIt = mapEdgeCells.RandomElement();

        // Sometimes we spawn a pawn or corpse
        if (Rand.Value > 0.9f)
        {
            var pawnKindDef = (from kindDef in DefDatabase<PawnKindDef>.AllDefs
                    where kindDef.RaceProps.IsFlesh && kindDef.defaultFactionType is not { isPlayer: true }
                    select kindDef)
                .RandomElement();
            Faction faction = null;
            if (pawnKindDef.defaultFactionType != null)
            {
                faction = Find.World.factionManager.AllFactions
                    .Where(factionType => factionType.def == pawnKindDef.defaultFactionType).RandomElement();
            }

            var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(pawnKindDef, faction, allowDead: false));

            if (!SomeThingsFloatMod.instance.Settings.SpawnLivingPawns || Rand.Value > 0.1f)
            {
                if (!pawn.Dead)
                {
                    pawn.Kill(null);
                }

                pawn.Corpse.Age = Rand.Range(1, 900000);
                GenSpawn.Spawn(pawn.Corpse, cellToPlaceIt, map);
                pawn.Corpse.GetComp<CompRottable>().RotProgress += pawn.Corpse.Age;
                return;
            }

            pawn.equipment?.DestroyAllEquipment();
            HealthUtility.DamageUntilDowned(pawn);
            GenSpawn.Spawn(pawn, cellToPlaceIt, map);
            if (!pawn.RaceProps.Animal)
            {
                Find.LetterStack.ReceiveLetter("STF.PawnSpawnedTitle".Translate(), "STF.PawnSpawnedMessage".Translate(),
                    LetterDefOf.NeutralEvent, pawn);
            }

            return;
        }

        var thingToMake = SomeThingsFloat.ThingsToCreate
            .Where(def => def.BaseMarketValue <= SomeThingsFloatMod.instance.Settings.MaxSpawnValue).RandomElement();
        var amountToSpawn =
            (int)Math.Floor(SomeThingsFloatMod.instance.Settings.MaxSpawnValue / thingToMake.BaseMarketValue);

        if (amountToSpawn == 0)
        {
            SomeThingsFloat.LogMessage($"Value of {thingToMake} too high, could not spawn");
            return;
        }

        var thing = ThingMaker.MakeThing(thingToMake);
        if (thing is Corpse corpse && (corpse.Bugged || !corpse.InnerPawn.RaceProps.IsFlesh))
        {
            return;
        }

        if (GenPlace.HaulPlaceBlockerIn(thing, cellToPlaceIt, map, true) != null)
        {
            SomeThingsFloat.LogMessage(
                $"{thing} could not be created at map edge: {cellToPlaceIt}, something in the way");
            return;
        }

        if (thing.def.stackLimit > 1)
        {
            thing.stackCount = Rand.RangeInclusive(1, Math.Min(thing.def.stackLimit, amountToSpawn));
        }

        if (!GenPlace.TryPlaceThing(thing, cellToPlaceIt, map, ThingPlaceMode.Direct))
        {
            SomeThingsFloat.LogMessage($"{thing} could not be created at map edge: {cellToPlaceIt}");
            return;
        }

        thing.SetForbidden(SomeThingsFloatMod.instance.Settings.ForbidSpawningItems, false);
    }

    private void updateListOfFloatingThings()
    {
        foreach (var possibleThings in totalCellsWithWater.Select(vec3 => SomeThingsFloat.GetThingsAndPawns(vec3, map)))
        {
            foreach (var possibleThing in possibleThings)
            {
                if (floatingValues.ContainsKey(possibleThing))
                {
                    continue;
                }

                floatingValues[possibleThing] = SomeThingsFloat.GetFloatingValue(possibleThing);
                SomeThingsFloat.LogMessage($"{possibleThing} float-value: {floatingValues[possibleThing]}");
                if (!(floatingValues[possibleThing] > 0))
                {
                    continue;
                }

                setNextUpdateTime(possibleThing);
                if (possibleThing is Pawn { Faction.IsPlayer: true } pawn)
                {
                    Messages.Message("STF.PawnIsFloatingAway".Translate(pawn.NameFullColored), pawn,
                        MessageTypeDefOf.NegativeEvent);
                }
            }
        }

        SomeThingsFloat.LogMessage($"Found {floatingValues.Count} items in water");
    }

    private void setNextUpdateTime(Thing thing)
    {
        if (thing == null)
        {
            return;
        }

        if (!floatingValues.ContainsKey(thing))
        {
            return;
        }

        var nextupdate = GenTicks.TicksGame +
                         (int)Math.Round(
                             (GenTicks.TickRareInterval / floatingValues[thing]) +
                             Rand.Range(-10, 10));
        while (updateValues.ContainsKey(nextupdate))
        {
            nextupdate++;
        }

        SomeThingsFloat.LogMessage($"Current tick: {GenTicks.TicksGame}, {thing} next update: {nextupdate}");
        updateValues[nextupdate] = thing;
    }


    private bool tryToFindNewPostition(Thing thing, out IntVec3 resultingCell, List<IntVec3> waterCells)
    {
        if (hiddenPositions == null || !hiddenPositions.TryGetValue(thing, out var originalPosition))
        {
            originalPosition = thing.Position;
        }

        resultingCell = originalPosition;

        var originalFlow = map.waterInfo.GetWaterMovement(resultingCell.ToVector3Shifted());
        SomeThingsFloat.LogMessage($"Flow at {thing} position: {originalFlow}");

        var possibleCellsToRecheck = new List<IntVec3>();

        foreach (var adjacentCell in GenAdj.AdjacentCells.InRandomOrder())
        {
            if (originalFlow != Vector3.zero)
            {
                if (adjacentCell.x > 0 != originalFlow.x > 0 || adjacentCell.z > 0 != originalFlow.z > 0)
                {
                    possibleCellsToRecheck.Add(adjacentCell);
                    SomeThingsFloat.LogMessage(
                        $"{adjacentCell} position compared to original flow {originalFlow} is not the right way");
                    continue;
                }
            }

            var currentCell = originalPosition + adjacentCell;
            if (!currentCell.InBounds(map))
            {
                if (SomeThingsFloatMod.instance.Settings.DespawnAtMapEdge)
                {
                    resultingCell = IntVec3.Invalid;
                    return true;
                }

                continue;
            }

            if (!waterCells.Contains(currentCell))
            {
                continue;
            }

            if (GenPlace.HaulPlaceBlockerIn(thing, currentCell, map, true) != null)
            {
                continue;
            }

            SomeThingsFloat.LogMessage($"Cell {currentCell} for {thing} was valid");
            resultingCell = currentCell;
            return true;
        }

        foreach (var adjacentCell in possibleCellsToRecheck)
        {
            if (originalFlow != Vector3.zero)
            {
                if (adjacentCell.x > 0 != originalFlow.x > 0 && adjacentCell.z > 0 != originalFlow.z > 0)
                {
                    SomeThingsFloat.LogMessage(
                        $"{adjacentCell} position compared to original flow {originalFlow} is really not the right way");
                    continue;
                }
            }

            var currentCell = originalPosition + adjacentCell;
            if (!currentCell.InBounds(map))
            {
                if (SomeThingsFloatMod.instance.Settings.DespawnAtMapEdge)
                {
                    resultingCell = IntVec3.Invalid;
                    return true;
                }

                continue;
            }

            if (!waterCells.Contains(currentCell))
            {
                continue;
            }

            if (GenPlace.HaulPlaceBlockerIn(thing, currentCell, map, true) != null)
            {
                continue;
            }

            SomeThingsFloat.LogMessage($"Cell {currentCell} for {thing} was valid");
            resultingCell = currentCell;
            return true;
        }

        return false;
    }

    private bool verifyThingIsInWater(Thing thing)
    {
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

        return totalCellsWithWater?.Contains(thing.Position) == true;
    }
}