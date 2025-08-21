using Verse;

namespace SomeThingsFloat;

public class Hediff_Drowning : Hediff_OnlyFloating
{
    public bool inventoryDropped;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref inventoryDropped, "inventoryDropped", false);
    }

    public override void Tick()
    {
        base.Tick();
        // See Pawn_DropAndForbidEverything.
        if( Severity >= def.stages[1].minSeverity ) // 'serious' drowning, 0.5 severity
        {
            inventoryDropped = true;
            pawn.DropAndForbidEverything();
        }
    }
}
