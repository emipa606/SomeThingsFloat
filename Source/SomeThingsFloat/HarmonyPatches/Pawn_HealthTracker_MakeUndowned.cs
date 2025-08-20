using HarmonyLib;
using RimWorld;
using Verse;

namespace SomeThingsFloat;

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.MakeUndowned))]
public static class Pawn_HealthTracker_MakeUndowned
{
    public static void Postfix(Pawn ___pawn, Hediff hediff)
    {
        if(hediff?.def == HediffDefOf.STF_LostFooting && hediff is Hediff_LostFooting lostFooting
            && lostFooting.wasInPanicFlee)
        {
            ___pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee);
        }
    }
}