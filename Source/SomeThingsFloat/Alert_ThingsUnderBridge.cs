using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace SomeThingsFloat;

public class Alert_ThingsUnderBridge : Alert
{
    public Alert_ThingsUnderBridge()
    {
        defaultLabel = "STF.ThingsUnderBridge".Translate();
        defaultPriority = AlertPriority.Medium;
    }

    public override TaggedString GetExplanation()
    {
        return "STF.ThingsUnderBridgeTT".Translate(string.Join("\n",
            thingsUnderBridge(out _).Keys.ToList().ConvertAll(input =>
                input.def.useHitPoints
                    ? $"{input.LabelCap} ({input.HitPoints}/{input.MaxHitPoints})"
                    : input.LabelCap)));
    }

    public override AlertReport GetReport()
    {
        var things = thingsUnderBridge(out var map);
        if (things == null || !things.Any())
        {
            return false;
        }

        return new AlertReport
        {
            active = true,
            culpritsTargets = things.Values.ToList().ConvertAll(input => new GlobalTargetInfo(input, map))
        };
    }

    private Dictionary<Thing, IntVec3> thingsUnderBridge(out Map map)
    {
        map = null;
        if (!SomeThingsFloatMod.instance.Settings.DownedPawnsFloat)
        {
            return new Dictionary<Thing, IntVec3>();
        }

        map = Find.CurrentMap;
        var component = SomeThingsFloat.FloatingMapComponents[map];
        return component == null ? new Dictionary<Thing, IntVec3>() : component.ThingsUnderBridge();
    }
}