using HarmonyLib;
using RimWorld;
using Verse;

namespace SomeThingsFloat;

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.MakeDowned))]
public static class Pawn_HealthTracker_MakeDowned
{
    public static bool DownedByLostFooting;

    public static void Prefix(Pawn ___pawn, Hediff hediff)
    {
        if(hediff?.def == HediffDefOf.STF_LostFooting)
        {
            DownedByLostFooting = true;
            // Getting downed also removes panic fleeing mental state, which would stop raiders from running away
            // after they get up again. Not removing it makes RimWorld log an error, so remember it
            // and restore the state when making undowned.
            if(___pawn.MentalStateDef == MentalStateDefOf.PanicFlee && hediff is Hediff_LostFooting lostFooting)
                lostFooting.wasInPanicFlee = true;
        }
    }

    public static void Postfix()
    {
        DownedByLostFooting = false;
    }
}