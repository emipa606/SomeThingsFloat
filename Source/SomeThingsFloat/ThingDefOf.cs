using RimWorld;
using Verse;

namespace SomeThingsFloat;

[DefOf]
public static class ThingDefOf
{
    public static ThingDef STF_Bars;

    static ThingDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ThingDefOf));
    }
}