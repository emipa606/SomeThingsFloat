using HarmonyLib;
using RimWorld;

namespace SomeThingsFloat;

[HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.ChoicesAtFor))]
public static class FloatMenuMakerMap_ChoicesAtFor
{
    public static bool CheckForFloatingThings;

    public static void Prefix()
    {
        if (!SomeThingsFloatMod.instance.Settings.SmoothAnimation)
        {
            return;
        }

        CheckForFloatingThings = true;
    }

    public static void Postfix()
    {
        CheckForFloatingThings = false;
    }
}