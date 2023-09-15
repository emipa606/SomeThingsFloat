using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SomeThingsFloat;

[HarmonyPatch(typeof(SteadyEnvironmentEffects), nameof(SteadyEnvironmentEffects.DoCellSteadyEffects))]
public static class SteadyEnvironmentEffects_DoCellSteadyEffects
{
    public static void Postfix(IntVec3 c, Map ___map)
    {
        if (SomeThingsFloat.FloatingMapComponents.TryGetValue(___map, out var component))
        {
            component.UnSpawnedDeterioration(c);
        }
    }
}

[HarmonyPatch(typeof(GenUI), nameof(GenUI.ThingsUnderMouse))]
public static class GenUI_ThingsUnderMouse
{
    public static void Postfix(ref List<Thing> __result, Vector3 clickPos)
    {
        if (!SomeThingsFloatMod.instance.Settings.SmoothAnimation)
        {
            return;
        }

        if (!SomeThingsFloat.FloatingMapComponents.TryGetValue(Find.CurrentMap, out var component))
        {
            return;
        }

        __result = component.GetFloatingThingsNear(clickPos, __result);
    }
}