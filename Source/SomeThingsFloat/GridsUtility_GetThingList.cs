using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace SomeThingsFloat;

[HarmonyPatch(typeof(GridsUtility), nameof(GridsUtility.GetThingList))]
public static class GridsUtility_GetThingList
{
    public static void Postfix(ref List<Thing> __result, IntVec3 c, Map map)
    {
        if (!FloatMenuMakerMap_ChoicesAtFor.CheckForFloatingThings)
        {
            return;
        }

        if (!SomeThingsFloatMod.instance.Settings.SmoothAnimation)
        {
            return;
        }

        if (!SomeThingsFloat.FloatingMapComponents.TryGetValue(map, out var component))
        {
            return;
        }

        __result = component.GetFloatingThingsNear(c, __result);
    }
}