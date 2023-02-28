using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SomeThingsFloat;

public class FloatingThings_MapComponent : MapComponent
{
    private readonly Dictionary<Thing, float> floatingValues = new Dictionary<Thing, float>();
    private readonly List<IntVec3> mapEdgeCells = new List<IntVec3>();
    private List<IntVec3> cellsWithWater = new List<IntVec3>();
    private List<Thing> floatingValuesKeys = new List<Thing>();
    private List<float> floatingValuesValues = new List<float>();
    private Dictionary<int, Thing> updateValues = new Dictionary<int, Thing>();
    private List<int> updateValuesKeys = new List<int>();
    private List<Thing> updateValuesValues = new List<Thing>();

    public FloatingThings_MapComponent(Map map) : base(map)
    {
        this.map = map;
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
        if (!verifyThingIsStillFloating(thing))
        {
            SomeThingsFloat.LogMessage($"{thing} is no longer floating");
            if (thing != null)
            {
                floatingValues.Remove(thing);
            }

            return;
        }

        if (!SomeThingsFloat.TryToFindNewPostition(thing, out var newPosition, cellsWithWater))
        {
            SomeThingsFloat.LogMessage($"{thing} cannot find a new postition");
            setNextUpdateTime(thing);
            return;
        }

        var originalPosition = thing.Position;
        thing.DeSpawn();
        if (newPosition == IntVec3.Invalid)
        {
            if (thing is Pawn { IsColonist: true } pawn)
            {
                Find.LetterStack.ReceiveLetter("STF.PawnIsLostTitle".Translate(pawn.NameFullColored),
                    "STF.PawnIsLostMessage".Translate(pawn.NameFullColored), LetterDefOf.Death);

                PawnDiedOrDownedThoughtsUtility.TryGiveThoughts(pawn, null, PawnDiedOrDownedThoughtsKind.Lost);
            }

            floatingValues.Remove(thing);
            thing.Destroy();
            return;
        }

        setNextUpdateTime(thing);

        if (!GenPlace.TryPlaceThing(thing, newPosition, map, ThingPlaceMode.Direct))
        {
            SomeThingsFloat.LogMessage($"{thing} could not be placed at its new position");
            GenPlace.TryPlaceThing(thing, originalPosition, map, ThingPlaceMode.Direct);
        }

        if (SomeThingsFloatMod.instance.Settings.ForbidWhenMoving)
        {
            thing.SetForbidden(!thing.IsInValidStorage(), false);
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
        Scribe_Collections.Look(ref updateValues, "updateValues", LookMode.Value, LookMode.Reference,
            ref updateValuesKeys, ref updateValuesValues);
        if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
        {
            updateListOfFloatingThings();
        }
    }

    private void updateListOfWaterCells()
    {
        if (cellsWithWater.Any() && Rand.Bool)
        {
            return;
        }

        cellsWithWater = map.AllCells.Where(vec3 => vec3.GetTerrain(map).defName.Contains("Water")).ToList();
        SomeThingsFloat.LogMessage($"Found {cellsWithWater.Count} water-cells");
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

        if (Rand.Value < 0.95f)
        {
            return;
        }

        if (!mapEdgeCells.Any())
        {
            var possibleMapEdgeCells = cellsWithWater.Where(vec3 =>
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
        }
    }

    private void updateListOfFloatingThings()
    {
        foreach (var possibleThings in cellsWithWater.Select(vec3 => SomeThingsFloat.GetThingsAndPawns(vec3, map)))
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


    private bool verifyThingIsStillFloating(Thing thing)
    {
        if (thing is not { Spawned: true })
        {
            SomeThingsFloat.LogMessage($"{thing} no longer exists");
            return false;
        }

        if (!thing.Position.InBounds(thing.Map))
        {
            SomeThingsFloat.LogMessage($"{thing} no longer in bounds of map");
            return false;
        }

        if (!cellsWithWater.Contains(thing.Position))
        {
            SomeThingsFloat.LogMessage($"{thing} is no longer in water");
            return false;
        }

        return true;
    }
}