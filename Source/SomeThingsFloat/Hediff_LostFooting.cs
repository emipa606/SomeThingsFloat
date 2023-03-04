using Verse;

namespace SomeThingsFloat;

public class Hediff_LostFooting : HediffWithComps
{
    public override void Tick()
    {
        base.Tick();

        if (pawn.Map == null)
        {
            return;
        }

        if (SomeThingsFloat.FloatingMapComponents[pawn.Map].VerifyThingIsInWater(pawn))
        {
            return;
        }

        Severity = 0;
    }
}