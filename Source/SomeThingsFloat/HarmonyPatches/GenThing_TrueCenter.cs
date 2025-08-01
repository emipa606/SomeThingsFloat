using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SomeThingsFloat;

[HarmonyPatch(typeof(GenThing), nameof(GenThing.TrueCenter), typeof(Thing))]
public static class GenThing_TrueCenter
{
    public static void Postfix(Thing t, ref Vector3 __result)
    {
        if (!SomeThingsFloatMod.Instance.Settings.SmoothAnimation)
        {
            return;
        }

        if (t?.Map == null)
        {
            return;
        }

        if (!SomeThingsFloat.FloatingMapComponents.TryGetValue(t.Map, out var component))
        {
            return;
        }

        __result = component.GetNewCenter(t, __result);
    }
}

[HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.DrawPos), MethodType.Getter)]
public static class Pawn_DrawTracker_DrawPos
{
    public static void Postfix(Pawn ___pawn, ref Vector3 __result)
    {
        if (!SomeThingsFloatMod.Instance.Settings.SmoothAnimation)
        {
            return;
        }

        if (___pawn?.Map == null)
        {
            return;
        }

        if (!SomeThingsFloat.FloatingMapComponents.TryGetValue(___pawn.Map, out var component))
        {
            return;
        }

        __result = component.GetNewCenter(___pawn, __result);
    }
}