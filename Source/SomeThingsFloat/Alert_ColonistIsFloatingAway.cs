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
        return "STF.PawnIsFloatingAwayTT".Translate(string.Join(", ",
            floatingPawns().ConvertAll(input => input.NameFullColored)));
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

    private static List<Pawn> floatingPawns()
    {
        if (!SomeThingsFloatMod.Instance.Settings.DownedPawnsFloat)
        {
            return [];
        }

        var map = Find.CurrentMap;
        if (map == null)
        {
            return [];
        }

        var component = SomeThingsFloat.FloatingMapComponents[map];
        return component == null ? [] : component.DownedPawnsInWater();
    }
}