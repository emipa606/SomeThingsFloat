using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SomeThingsFloat;

[HarmonyPatch(typeof(GenUI), nameof(GenUI.ThingsUnderMouse))]
public static class GenUI_ThingsUnderMouse
{
    public static void Postfix(ref List<Thing> __result, Vector3 clickPos, TargetingParameters clickParams,
        ITargetingSource source)
    {
        if (!SomeThingsFloatMod.Instance.Settings.SmoothAnimation)
        {
            return;
        }

        if (!SomeThingsFloat.FloatingMapComponents.TryGetValue(Find.CurrentMap, out var component))
        {
            return;
        }

        __result = component.GetFloatingThingsNear(clickPos, __result, clickParams, source);
    }
}