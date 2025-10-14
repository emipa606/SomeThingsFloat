using HarmonyLib;
using RimWorld;

namespace SomeThingsFloat;

[HarmonyPatch(typeof(Building_GravEngine), nameof(Building_GravEngine.PostSwapMap))]
public static class Building_GravEngine_PostSwapMap
{
    public static void Postfix(Building_GravEngine __instance)
    {
        if (!SomeThingsFloat.FloatingMapComponents.TryGetValue(__instance.Map, out var component))
        {
            return;
        }

        component.ForceFloatCellUpdate();
    }
}