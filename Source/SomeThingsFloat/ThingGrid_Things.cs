using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace SomeThingsFloat;

[HarmonyPatch]
public static class ThingGrid_Things
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ThingGrid), nameof(ThingGrid.ThingsListAt));
        yield return AccessTools.Method(typeof(ThingGrid), nameof(ThingGrid.ThingsListAtFast),
            new[] { typeof(IntVec3) });
    }

    public static void Postfix(ref List<Thing> __result, IntVec3 c, Map ___map)
    {
        if (!FloatMenuMakerMap_ChoicesAtFor.CheckForFloatingThings)
        {
            return;
        }

        if (!SomeThingsFloatMod.instance.Settings.SmoothAnimation)
        {
            return;
        }

        if (!SomeThingsFloat.FloatingMapComponents.TryGetValue(___map, out var component))
        {
            return;
        }

        __result = component.GetFloatingThingsNear(c, __result);
    }
}