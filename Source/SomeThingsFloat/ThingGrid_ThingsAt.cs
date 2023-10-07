using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;

namespace SomeThingsFloat;

[HarmonyPatch(typeof(ThingGrid), nameof(ThingGrid.ThingsAt))]
public static class ThingGrid_ThingsAt
{
    public static IEnumerable<Thing> Postfix(IEnumerable<Thing> values, IntVec3 c, Map ___map)
    {
        if (!FloatMenuMakerMap_ChoicesAtFor.CheckForFloatingThings)
        {
            foreach (var value in values)
            {
                yield return value;
            }

            yield break;
        }

        if (!SomeThingsFloatMod.instance.Settings.SmoothAnimation)
        {
            foreach (var value in values)
            {
                yield return value;
            }

            yield break;
        }

        if (!SomeThingsFloat.FloatingMapComponents.TryGetValue(___map, out var component))
        {
            foreach (var value in values)
            {
                yield return value;
            }

            yield break;
        }

        foreach (var value in component.GetFloatingThingsNear(c, values.ToList()))
        {
            yield return value;
        }
    }
}