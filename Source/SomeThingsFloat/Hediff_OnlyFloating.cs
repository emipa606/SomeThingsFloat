using Verse;

namespace SomeThingsFloat;

public class Hediff_OnlyFloating : HediffWithComps
{
    public override void Tick()
    {
        base.Tick();

        if (pawn?.Map == null)
        {
            Severity = 0;
            return;
        }

        if (!pawn.IsHashIntervalTick(50))
        {
            return;
        }

        if (!SomeThingsFloat.FloatingMapComponents[pawn.Map].VerifyThingIsInWater(pawn))
        {
            Severity = 0;
        }

        if (!pawn.Downed)
        {
            Severity = 0;
        }

        if (pawn.CarriedBy != null)
        {
            Severity = 0;
        }
    }
}