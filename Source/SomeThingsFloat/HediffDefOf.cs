using RimWorld;
using Verse;

namespace SomeThingsFloat;

[DefOf]
public static class HediffDefOf
{
    public static HediffDef STF_LostFooting;
    public static HediffDef STF_Drowning;

    static HediffDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(HediffDefOf));
    }
}