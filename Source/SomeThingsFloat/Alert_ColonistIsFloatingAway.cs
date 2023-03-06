using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SomeThingsFloat;

public class Alert_ColonistIsFloatingAway : Alert
{
    public Alert_ColonistIsFloatingAway()
    {
        defaultLabel = "STF.PawnIsFloatingAway".Translate();
        defaultPriority = AlertPriority.High;
    }

    public override TaggedString GetExplanation()
    {
        return "STF.PawnIsFloatingAwayTT".Translate(string.Join(", ", floatingPawns()));
    }

    public override AlertReport GetReport()
    {
        var pawns = floatingPawns();
        return new AlertReport
        {
            active = pawns.Count > 0,
            culpritsPawns = pawns
        };
    }

    private List<Pawn> floatingPawns()
    {
        if (!SomeThingsFloatMod.instance.Settings.DownedPawnsFloat)
        {
            return new List<Pawn>();
        }

        var map = Find.CurrentMap;
        var component = SomeThingsFloat.FloatingMapComponents[map];
        return component == null ? new List<Pawn>() : component.DownedPawnsInWater();
    }
}