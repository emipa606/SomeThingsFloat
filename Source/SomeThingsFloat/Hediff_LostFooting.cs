using Verse;

namespace SomeThingsFloat;

public class Hediff_LostFooting : Hediff_OnlyFloating
{
    public bool wasInPanicFlee;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref wasInPanicFlee, "wasInPanicFlee", false);
    }
}
