using Verse;

namespace SomeThingsFloat;

/// <summary>
///     Definition of the settings for the mod
/// </summary>
internal class SomeThingsFloatSettings : ModSettings
{
    public bool DespawnAtMapEdge = true;
    public bool ForbidWhenMoving = true;
    public float MaxSpawnValue = 50f;
    public bool SpawnNewItems = true;
    public bool VerboseLogging;

    /// <summary>
    ///     Saving and loading the values
    /// </summary>
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref VerboseLogging, "VerboseLogging");
        Scribe_Values.Look(ref ForbidWhenMoving, "ForbidWhenMoving", true);
        Scribe_Values.Look(ref DespawnAtMapEdge, "DespawnAtMapEdge", true);
        Scribe_Values.Look(ref SpawnNewItems, "SpawnNewItems", true);
        Scribe_Values.Look(ref MaxSpawnValue, "MaxSpawnValue", 50f);
    }
}