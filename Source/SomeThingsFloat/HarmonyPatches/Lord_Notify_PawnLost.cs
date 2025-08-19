using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace SomeThingsFloat;

// Do not remove a pawn from a lord when losing footing, that causes e.g. raiders
// be removed from a raid, and it also counts as them being lost (and thus counts
// towards the entire group starting to flee), even though they'll most likely
// get up to feet again quickly. Do this only for incapacitations, killing should
// of course count.
[HarmonyPatch(typeof(Lord), nameof(Lord.Notify_PawnLost))]
public static class Lord_Notify_PawnLost
{
    public static bool Prefix(PawnLostCondition cond, DamageInfo? dinfo)
    {
        if(cond == PawnLostCondition.Incapped && Pawn_HealthTracker_MakeDowned.downedByLostFooting)
            return false;
        return true;
    }
}
