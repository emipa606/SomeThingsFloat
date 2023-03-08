using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SomeThingsFloat;

[StaticConstructorOnStartup]
public class SomeThingsFloat
{
    public static readonly List<ThingDef> ThingsToCreate;

    public static readonly Dictionary<Map, FloatingThings_MapComponent> FloatingMapComponents;

    public static readonly DesignationDef HaulUrgentlyDef;

    public static readonly List<ThingDef> ApparelThatPreventDrowning;

    public static readonly List<ThingDef> PawnsThatBreathe;

    public static readonly List<ThingDef> PawnsThatFloat;

    static SomeThingsFloat()
    {
        ThingsToCreate = DefDatabase<ThingDef>.AllDefsListForReading
            .Where(def => TryGetSpecialFloatingValue(def, out var floatingValue) && floatingValue > 0)
            .ToList();
        ApparelThatPreventDrowning = DefDatabase<ThingDef>.AllDefsListForReading.Where(def =>
            def.IsApparel && def.apparel.tags.Contains("EVA") &&
            def.apparel.layers.Contains(ApparelLayerDefOf.Overhead)).ToList();
        PawnsThatBreathe = DefDatabase<ThingDef>.AllDefsListForReading.Where(def =>
            def.race is { IsFlesh: true } &&
            PawnCapacityUtility.BodyCanEverDoCapacity(def.race.body, PawnCapacityDefOf.Breathing)).ToList();
        PawnsThatFloat = DefDatabase<ThingDef>.AllDefsListForReading.Where(def =>
            def.race is { IsFlesh: true }).ToList();
        FloatingMapComponents = new Dictionary<Map, FloatingThings_MapComponent>();
        HaulUrgentlyDef = DefDatabase<DesignationDef>.GetNamedSilentFail("HaulUrgentlyDesignation");
    }

    public static float GetFloatingValue(Thing thing)
    {
        var actualThing = thing;
        switch (thing)
        {
            case null:
                return 0;
            case Corpse corpse when corpse.InnerPawn.RaceProps.IsFlesh:
                return 0.75f;
            case Pawn pawn:
                if (SomeThingsFloatMod.instance.Settings.DownedPawnsFloat && PawnsThatFloat.Contains(pawn.def) &&
                    pawn.Downed && pawn.Awake())
                {
                    return 0.5f;
                }

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
        if (actualThing.def.CostList.NullOrEmpty() && actualThing.def.stuffCategories.NullOrEmpty())
        {
            return 0;
        }

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

    public static IEnumerable<Thing> GetThingsAndPawns(IntVec3 c, Map map)
    {
        var thingList = map.thingGrid.ThingsListAt(c);
        int num;
        for (var i = 0; i < thingList.Count; i = num + 1)
        {
            if (thingList[i].def.category is ThingCategory.Item or ThingCategory.Pawn)
            {
                yield return thingList[i];
            }

            num = i;
        }
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


    public static float CalculateDrowningValue(Pawn pawn)
    {
        var breathing = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Breathing) ?? 1;
        var manipulation = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Manipulation) ?? 1;
        var minimumCapacity = 0.1;
        var hediffBaseValue = 0.025f;
        var breathingFactor = 0.6f;
        var manipulationFactor = 0.4f;
        var capacties = (breathingFactor * (float)Math.Max(breathing, minimumCapacity)) +
                        (manipulationFactor * (float)Math.Max(manipulation, minimumCapacity));
        var drownValue = hediffBaseValue * (1 / capacties);
        LogMessage(
            $"Drowning value for {pawn}: {drownValue}. Breathing: {breathing}, Manipulation: {manipulation}, Capacities: {capacties}");
        return drownValue;
    }

    public static void LogMessage(string message)
    {
        if (SomeThingsFloatMod.instance.Settings.VerboseLogging)
        {
            Log.Message($"[SomeThingsFloat]: {message}");
        }
    }
}