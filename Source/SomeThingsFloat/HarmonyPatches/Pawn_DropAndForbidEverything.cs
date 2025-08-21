using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SomeThingsFloat;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.DropAndForbidEverything))]
public static class Pawn_DropAndForbidEverything
{
    public static bool Prefix()
    {
        // Losing footing even only temporarily would normally result
        // in dropping all inventory, which is annoying and problematic:
        // Colonists would require picking everything up manually (since it's
        // forbidden), friendly traders would spill their goods and not collect
        // them, and raiders would disarm themselves. So do not drop inventory
        // immediately after losing footing, Hediff_Drowning will possibly
        // do it later.
        if(Pawn_HealthTracker_MakeDowned.downedByLostFooting)
            return false;
        return true;
    }
}
