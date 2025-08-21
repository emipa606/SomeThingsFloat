using HarmonyLib;
using Verse;

namespace SomeThingsFloat;

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.MakeDowned))]
public static class Pawn_HealthTracker_MakeDowned
{
    public static bool DownedByLostFooting;

    public static void Prefix(Hediff hediff)
    {
        DownedByLostFooting = hediff?.def == HediffDefOf.STF_LostFooting;
    }

    public static void Postfix()
    {
        DownedByLostFooting = false;
    }
}