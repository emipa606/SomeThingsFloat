using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SomeThingsFloat;

public class FloatingThings_MapComponent : MapComponent
{
    private readonly List<IntVec3> mapEdgeCells;
    private List<IntVec3> cellsWithRiver;
    private List<IntVec3> cellsWithWater;
    private Dictionary<Thing, float> floatingValues;
    private Dictionary<Thing, IntVec3> hiddenPositions;
    private List<Thing> hiddenPositionsKeys;
    private List<IntVec3> hiddenPositionsValues;
    private int lastSpawnTick;
    private List<IntVec3> underCellsWithWater;
    private Dictionary<int, Thing> updateValues;
    private List<int> updateValuesKeys;
    private List<Thing> updateValuesValues;

    public FloatingThings_MapComponent(Map map) : base(map)
    {
        this.map = map;
        SomeThingsFloat.FloatingMapComponents[map] = this;
        underCellsWithWater = new List<IntVec3>();
        cellsWithWater = new List<IntVec3>();
        cellsWithRiver = new List<IntVec3>();
        mapEdgeCells = new List<IntVec3>();
        floatingValues = new Dictionary<Thing, float>();
        updateValues = new Dictionary<int, Thing>();
        updateValuesKeys = new List<int>();
        updateValuesValues = new List<Thing>();
        hiddenPositions = new Dictionary<Thing, IntVec3>();
        hiddenPositionsKeys = new List<Thing>();
        hiddenPositionsValues = new List<IntVec3>();
        lastSpawnTick = 0;
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

        if (ticksGame % GenTicks.TickRareInterval == 0)
        {
            if (SomeThingsFloatMod.instance.Settings.PawnsCanFall)
            {
                checkForPawnsThatCanFall();
            }

            if (SomeThingsFloatMod.instance.Settings.DownedPawnsDrown)
            {
                checkForPawnsThatCanDrown();
            }
        }

        if (hiddenPositions.Any())
        {
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

                GenPlace.TryPlaceThing(respawningThing, spawnCell, map, ThingPlaceMode.Direct);
                hiddenPositions.Remove(respawningThing);
            }
        }

        if (!updateValues.ContainsKey(ticksGame))
        {
            return;
        }

        var thing = updateValues[ticksGame];
        updateValues.Remove(ticksGame);
        if (!VerifyThingIsInWater(thing))
        {
            SomeThingsFloat.LogMessage($"{thing} is no longer floating");
            if (thing != null)
            {
                floatingValues.Remove(thing);
            }

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

            SomeThingsFloat.LogMessage($"{thing} is not reserved");
        }

        if (!tryToFindNewPostition(thing, out var newPosition))
        {
            SomeThingsFloat.LogMessage($"{thing} cannot find a new postition");
            setNextUpdateTime(thing, true);
            return;
        }

        var wasInStorage = false;
        var wasUnspawned = false;
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
            if (thing.Spawned)
            {
                thing.DeSpawn();
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
            if (wasUnspawned && SomeThingsFloat.HaulUrgentlyDef != null &&
                SomeThingsFloatMod.instance.Settings.HaulUrgently)
            {
                map.designationManager.AddDesignation(new Designation(thing, SomeThingsFloat.HaulUrgentlyDef));
            }
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

        Scribe_Values.Look(ref lastSpawnTick, "lastSpawnTick");
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
        if (cellsWithWater == null || cellsWithRiver == null || !cellsWithWater.Any() || forceUpdate)
        {
            cellsWithWater = map.AllCells.Where(vec3 => vec3.GetTerrain(map).IsWater).ToList();
            cellsWithRiver = map.AllCells.Where(vec3 => vec3.GetTerrain(map).IsRiver).ToList();
            SomeThingsFloat.LogMessage($"Found {cellsWithWater.Count} water-cells");
            SomeThingsFloat.LogMessage($"Found {cellsWithRiver.Count} river-cells");
        }

        if (!SomeThingsFloatMod.instance.Settings.FloatUnderBridges)
        {
            return;
        }

        if (underCellsWithWater != null && underCellsWithWater.Any() && !forceUpdate)
        {
            return;
        }

        underCellsWithWater = map.AllCells
            .Where(vec3 => map.terrainGrid.UnderTerrainAt(vec3)?.IsWater == true).ToList();
        SomeThingsFloat.LogMessage($"Found {underCellsWithWater.Count} water-cells under bridges");
    }

    private void spawnThingAtMapEdge()
    {
        if (!SomeThingsFloatMod.instance.Settings.SpawnNewItems)
        {
            return;
        }

        if (!cellsWithWater.Any())
        {
            SomeThingsFloat.LogMessage("No water cells to spawn in");
            return;
        }

        if (Rand.Value < 0.9f)
        {
            return;
        }

        if (lastSpawnTick + SomeThingsFloatMod.instance.Settings.MinTimeBetweenItems > GenTicks.TicksGame)
        {
            SomeThingsFloat.LogMessage(
                $"Not time to spawn yet, next spawn: {lastSpawnTick + SomeThingsFloatMod.instance.Settings.MinTimeBetweenItems}, current time {GenTicks.TicksGame}");
            return;
        }

        if (!mapEdgeCells.Any())
        {
            var possibleMapEdgeCells = cellsWithWater.Intersect(CellRect.WholeMap(map).EdgeCells);

            if (!possibleMapEdgeCells.Any())
            {
                return;
            }

            foreach (var mapEdgeCell in possibleMapEdgeCells)
            {
                if (SomeThingsFloatMod.instance.Settings.SpawnInOceanTiles &&
                    mapEdgeCell.GetTerrain(map)?.defName.ToLower().Contains("ocean") == true)
                {
                    mapEdgeCells.Add(mapEdgeCell);
                    continue;
                }

                var flowAtCell = map.waterInfo.GetWaterMovement(mapEdgeCell.ToVector3Shifted());
                if (flowAtCell == Vector3.zero)
                {
                    SomeThingsFloat.LogMessage($"{mapEdgeCell} ! {flowAtCell}");
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
                lastSpawnTick = GenTicks.TicksGame;

                pawn.SetForbidden(SomeThingsFloatMod.instance.Settings.ForbidSpawningItems, false);
                if (SomeThingsFloat.HaulUrgentlyDef != null && SomeThingsFloatMod.instance.Settings.HaulUrgently)
                {
                    map.designationManager.AddDesignation(new Designation(pawn, SomeThingsFloat.HaulUrgentlyDef));
                }

                if (SomeThingsFloatMod.instance.Settings.NotifyOfSpawningItems)
                {
                    Messages.Message(
                        cellToPlaceIt.GetTerrain(map)?.defName.ToLower().Contains("ocean") == true
                            ? "STF.ThingsFloatedInFromTheOcean".Translate(pawn.LabelCap)
                            : "STF.ThingsFloatedIntoTheMap".Translate(pawn.LabelCap), pawn,
                        MessageTypeDefOf.NeutralEvent);
                }

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
            else
            {
                if (SomeThingsFloatMod.instance.Settings.NotifyOfSpawningItems)
                {
                    Messages.Message("STF.ThingsFloatedIntoTheMap".Translate(pawn.NameFullColored), pawn,
                        MessageTypeDefOf.NeutralEvent);
                }
            }

            lastSpawnTick = GenTicks.TicksGame;
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

        lastSpawnTick = GenTicks.TicksGame;
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
                if (possibleThing == null || possibleThing is not Pawn && floatingValues.ContainsKey(possibleThing))
                {
                    continue;
                }

                SomeThingsFloat.LogMessage($"{possibleThing} float-value?");
                floatingValues[possibleThing] = SomeThingsFloat.GetFloatingValue(possibleThing);
                SomeThingsFloat.LogMessage($"{possibleThing} float-value: {floatingValues[possibleThing]}");
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

            SomeThingsFloat.LogMessage($"{possibleThing} float-value?");
            floatingValues[possibleThing] = SomeThingsFloat.GetFloatingValue(possibleThing);
            SomeThingsFloat.LogMessage($"{possibleThing} float-value: {floatingValues[possibleThing]}");
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

        SomeThingsFloat.LogMessage($"Current tick: {GenTicks.TicksGame}, {thing} next update: {nextupdate}");
        updateValues[nextupdate] = thing;
    }


    private void checkForPawnsThatCanFall()
    {
        foreach (var pawn in map.mapPawns.AllPawns)
        {
            if (pawn is not { Spawned: true } || pawn.Dead || pawn.CarriedBy != null)
            {
                continue;
            }

            if (cellsWithRiver?.Contains(pawn.Position) == false)
            {
                continue;
            }

            var manipulation = Math.Max(pawn.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation), 0.1f);
            var manipulationFiltered = Math.Max(Math.Min(manipulation, 0.999f), 0.5f);
            var rand = Rand.Value;
            if (rand < manipulationFiltered)
            {
                continue;
            }

            if (pawn.story?.traits?.HasTrait(TraitDef.Named("Nimble")) == true && Rand.Bool)
            {
                continue;
            }

            SomeThingsFloat.LogMessage($"{pawn} failed the Manipulation-check ({manipulationFiltered}/{rand})");

            var lostFootingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.STF_LostFooting);
            if (lostFootingHediff != null)
            {
                lostFootingHediff.Severity = Math.Min(1f, lostFootingHediff.Severity + (0.05f / manipulation));
            }
            else
            {
                var hediff = HediffMaker.MakeHediff(HediffDefOf.STF_LostFooting, pawn);
                hediff.Severity = 0.05f / manipulation;
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
        for (var index = 0; index < map.mapPawns.AllPawns.Count; index++)
        {
            var pawn = map.mapPawns.AllPawns[index];
            if (pawn == null || pawn.Dead || SomeThingsFloat.PawnsThatBreathe?.Contains(pawn.def) == false ||
                !pawn.Downed ||
                !pawn.Awake())
            {
                continue;
            }

            if (pawn.Spawned)
            {
                if (cellsWithWater?.Contains(pawn.Position) == false)
                {
                    continue;
                }
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
                if (cannotDrown)
                {
                    drowningHediff.Severity = 0;
                    return;
                }

                drowningHediff.Severity += SomeThingsFloat.CalculateDrowningValue(pawn);
            }
            else
            {
                if (cannotDrown)
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
        }
    }


    private bool tryToFindNewPostition(Thing thing, out IntVec3 resultingCell)
    {
        if (hiddenPositions == null || !hiddenPositions.TryGetValue(thing, out var originalPosition))
        {
            originalPosition = thing.Position;
            var foundBuilding = originalPosition.GetFirstBuilding(map);
            if (foundBuilding != null &&
                (foundBuilding.def != ThingDefOf.STF_Bars || SomeThingsFloat.IsLargeThing(thing)))
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

        SomeThingsFloat.LogMessage($"Flow at {thing} position: {originalFlow}");

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
                        $"{adjacentCell} position compared to original flow {originalFlow} is not the right way");
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
                SomeThingsFloat.LogMessage($"{adjacentCell} is not in water");
                continue;
            }

            if (GenPlace.HaulPlaceBlockerIn(thing, adjacentCell, map, true) != null &&
                !underCellsWithWater.Contains(adjacentCell))
            {
                var foundBuilding = adjacentCell.GetFirstBuilding(map);
                if (foundBuilding == null ||
                    foundBuilding.def != ThingDefOf.STF_Bars && foundBuilding.def != ThingDefOf.STF_Net)
                {
                    SomeThingsFloat.LogMessage($"{adjacentCell} position has stuff in the way");
                    continue;
                }
            }

            SomeThingsFloat.LogMessage($"Cell {adjacentCell} for {thing} was valid");
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
                        $"{adjacentCell} position compared to original flow {originalFlow} is really not the right way");
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
                SomeThingsFloat.LogMessage($"{adjacentCell} is not in water");
                continue;
            }

            if (GenPlace.HaulPlaceBlockerIn(thing, adjacentCell, map, true) != null)
            {
                SomeThingsFloat.LogMessage($"{adjacentCell} position has stuff in the way");
                continue;
            }

            SomeThingsFloat.LogMessage($"Cell {adjacentCell} for {thing} was valid");
            resultingCell = adjacentCell;
            return true;
        }

        return false;
    }


    public void UnSpawnedDeterioration(IntVec3 c)
    {
        if (!hiddenPositions.Values.Contains(c))
        {
            return;
        }

        var thing = hiddenPositions.First(pair => pair.Value == c).Key;
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
        var num = thing.GetStatValue(StatDefOf.DeteriorationRate, false);

        if (!thing.def.deteriorateFromEnvironmentalEffects)
        {
            return num;
        }

        num += StatDefOf.DeteriorationRate.GetStatPart<StatPart_EnvironmentalEffects>().factorOffsetUnroofed;
        num += StatDefOf.DeteriorationRate.GetStatPart<StatPart_EnvironmentalEffects>().factorOffsetOutdoors;
        num *= map.terrainGrid.UnderTerrainAt(hiddenPositions[thing]).extraDeteriorationFactor;
        return num;
    }

    public void ClearEdgeCells()
    {
        mapEdgeCells.Clear();
    }

    public List<Pawn> DownedPawnsInWater()
    {
        return map.mapPawns.AllPawns.Where(pawn =>
            pawn.Downed && pawn.Awake() && floatingValues.ContainsKey(pawn) &&
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
}