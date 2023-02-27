using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SomeThingsFloat;

[StaticConstructorOnStartup]
public class SomeThingsFloat
{
    public static readonly List<ThingDef> ThingsToCreate;

    static SomeThingsFloat()
    {
        new Harmony("Mlie.SomeThingsFloat").PatchAll(Assembly.GetExecutingAssembly());
        ThingsToCreate = DefDatabase<ThingDef>.AllDefsListForReading
            .Where(def => def.IsStuff && TryGetSpecialFloatingValue(def, out var floatingValue) && floatingValue > 0)
            .ToList();
    }

    public static float GetFloatingValue(Thing thing)
    {
        var actualThing = thing;
        switch (thing)
        {
            case null:
                return 0;
            // Organic corpses float, mechs do not
            case Corpse corpse when corpse.InnerPawn.RaceProps.IsFlesh:
                return 0.75f;
            // Living things do not float
            case Pawn:
                return 0;
            case Building:
                return 0;
            case MinifiedThing minifiedThing:
                actualThing = minifiedThing.InnerThing;
                break;
        }

        // Manually defined items
        if (thing.def.HasModExtension<FloatingThing_ModExtension>())
        {
            return thing.def.GetModExtension<FloatingThing_ModExtension>().floatingValue;
        }

        // Check if its a special thing
        if (TryGetSpecialFloatingValue(actualThing.def, out var floatingValue))
        {
            return floatingValue;
        }

        // Check floatability based on ingredients
        if (!actualThing.def.CostList.NullOrEmpty() || !actualThing.def.stuffCategories.NullOrEmpty())
        {
            var totalIngredients = 0f;
            var totalValue = 0f;
            if (!actualThing.def.CostList.NullOrEmpty())
            {
                foreach (var thingDefCountClass in actualThing.def.CostList)
                {
                    TryGetSpecialFloatingValue(thingDefCountClass.thingDef, out floatingValue);
                    totalIngredients += thingDefCountClass.count;
                    totalValue += thingDefCountClass.count * floatingValue;
                }
            }

            if (!actualThing.def.stuffCategories.NullOrEmpty())
            {
                TryGetSpecialFloatingValue(actualThing.Stuff, out floatingValue);
                totalIngredients += actualThing.def.CostStuffCount;
                totalValue += actualThing.def.CostStuffCount * floatingValue;
            }

            totalValue /= totalIngredients;
            return totalValue;
        }

        return 0;
    }

    public static bool TryToFindNewPostition(Thing thing, out IntVec3 resultingCell, List<IntVec3> waterCells)
    {
        resultingCell = thing.Position;
        var originalFlow = thing.Map.waterInfo.GetWaterMovement(resultingCell.ToVector3Shifted());
        LogMessage($"Flow at {thing} position: {originalFlow}");

        var possibleCellsToRecheck = new List<IntVec3>();

        foreach (var adjacentCell in GenAdj.AdjacentCells.InRandomOrder())
        {
            if (originalFlow != Vector3.zero)
            {
                if (adjacentCell.x > 0 != originalFlow.x > 0 || adjacentCell.z > 0 != originalFlow.z > 0)
                {
                    possibleCellsToRecheck.Add(adjacentCell);
                    LogMessage(
                        $"{adjacentCell} position compared to original flow {originalFlow} is not the right way");
                    continue;
                }
            }

            var currentCell = thing.Position + adjacentCell;
            if (!currentCell.InBounds(thing.Map))
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

            if (GenPlace.HaulPlaceBlockerIn(thing, currentCell, thing.Map, true) != null)
            {
                continue;
            }

            LogMessage($"Cell {currentCell} for {thing} was valid");
            resultingCell = currentCell;
            return true;
        }

        foreach (var adjacentCell in possibleCellsToRecheck)
        {
            if (originalFlow != Vector3.zero)
            {
                if (adjacentCell.x > 0 != originalFlow.x > 0 && adjacentCell.z > 0 != originalFlow.z > 0)
                {
                    LogMessage(
                        $"{adjacentCell} position compared to original flow {originalFlow} is really not the right way");
                    continue;
                }
            }

            var currentCell = thing.Position + adjacentCell;
            if (!currentCell.InBounds(thing.Map))
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

            if (GenPlace.HaulPlaceBlockerIn(thing, currentCell, thing.Map, true) != null)
            {
                continue;
            }

            LogMessage($"Cell {currentCell} for {thing} was valid");
            resultingCell = currentCell;
            return true;
        }

        return false;
    }

    private static bool TryGetSpecialFloatingValue(ThingDef thingDef, out float floatingValue)
    {
        floatingValue = 1;

        if (thingDef.IsStuff)
        {
            if (thingDef.stuffProps.categories?.Contains(StuffCategoryDefOf.Woody) == true)
            {
                return true;
            }

            if (thingDef.stuffProps.categories?.Contains(StuffCategoryDefOf.Fabric) == true)
            {
                floatingValue = 0.5f;
                return true;
            }

            if (thingDef.stuffProps.categories?.Contains(StuffCategoryDefOf.Leathery) == true)
            {
                floatingValue = 0.5f;
                return true;
            }

            floatingValue = 0;
            return true;
        }

        if (thingDef.IsMeat)
        {
            floatingValue = 0.75f;
            return true;
        }

        if (thingDef.IsDrug || thingDef.IsMedicine)
        {
            return true;
        }

        if (thingDef.IsLeather || thingDef.IsWool)
        {
            floatingValue = 0.5f;
            return true;
        }

        if (thingDef.ingestible?.HumanEdible == true)
        {
            floatingValue = 0.75f;
            return true;
        }

        floatingValue = 0;
        return false;
    }

    public static void LogMessage(string message)
    {
        if (SomeThingsFloatMod.instance.Settings.VerboseLogging)
        {
            Log.Message($"[SomeThingsFloat]: {message}");
        }
    }
}