using RimWorld;
using Verse;

namespace SomeThingsFloat;

/// <summary>
///     Definition of the settings for the mod
/// </summary>
internal class SomeThingsFloatSettings : ModSettings
{
    public bool AllowOnStuck = true;
    public bool Bobbing = true;
    public bool DebugLogging;
    public bool DespawnAtMapEdge = true;
    public bool DownedPawnsDrown = true;
    public bool DownedPawnsFloat = true;
    public bool FloatUnderBridges = true;
    public bool FloatUnderBridgesInfo = true;
    public bool ForbidSpawningItems;
    public bool ForbidWhenMoving = true;
    public bool HaulUrgently;
    public float ManipulationThreshold = 0.99f;
    public float MaxSpawnValue = 50f;
    public float MinTimeBetweenItems = GenDate.TicksPerHour * 18;
    public bool NotifyOfSpawningItems = true;
    public bool PawnsCanFall = true;
    public float RelativeChanceInShallows = 0.5f;
    public float RelativeFloatSpeed = 1f;
    public bool ReservedItemsWillNotMove;
    public bool SmoothAnimation = true;
    public bool SpawnFertilizedEggs;
    public bool SpawnInOceanTiles;
    public bool SpawnLivingPawns = true;
    public bool SpawnNewItems = true;
    public bool VerboseLogging;
    public bool WarnForAllFriendlyPawns;

    /// <summary>
    ///     Saving and loading the values
    /// </summary>
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref VerboseLogging, "VerboseLogging");
        Scribe_Values.Look(ref DebugLogging, "DebugLogging");
        Scribe_Values.Look(ref ForbidSpawningItems, "ForbidSpawningItems");
        Scribe_Values.Look(ref HaulUrgently, "HaulUrgently");
        Scribe_Values.Look(ref ReservedItemsWillNotMove, "ReservedItemsWillNotMove");
        Scribe_Values.Look(ref WarnForAllFriendlyPawns, "WarnForAllFriendlyPawns");
        Scribe_Values.Look(ref SpawnInOceanTiles, "SpawnInOceanTiles");
        Scribe_Values.Look(ref RelativeFloatSpeed, "RelativeFloatSpeed", 1f);
        Scribe_Values.Look(ref ManipulationThreshold, "ManipulationThreshold", 0.99f);
        Scribe_Values.Look(ref ForbidWhenMoving, "ForbidWhenMoving", true);
        Scribe_Values.Look(ref AllowOnStuck, "AllowOnStuck", true);
        Scribe_Values.Look(ref SmoothAnimation, "SmoothAnimation", true);
        Scribe_Values.Look(ref Bobbing, "Bobbing", true);
        Scribe_Values.Look(ref FloatUnderBridges, "FloatUnderBridges", true);
        Scribe_Values.Look(ref FloatUnderBridgesInfo, "FloatUnderBridgesInfo", true);
        Scribe_Values.Look(ref DespawnAtMapEdge, "DespawnAtMapEdge", true);
        Scribe_Values.Look(ref SpawnNewItems, "SpawnNewItems", true);
        Scribe_Values.Look(ref DownedPawnsFloat, "DownedPawnsFloat", true);
        Scribe_Values.Look(ref DownedPawnsDrown, "DownedPawnsDrown", true);
        Scribe_Values.Look(ref PawnsCanFall, "PawnsCanFall", true);
        Scribe_Values.Look(ref RelativeChanceInShallows, "RelativeChanceInShallows", 0.5f);
        Scribe_Values.Look(ref SpawnLivingPawns, "SpawnLivingPawns", true);
        Scribe_Values.Look(ref SpawnFertilizedEggs, "SpawnFertilizedEggs");
        Scribe_Values.Look(ref NotifyOfSpawningItems, "NotifyOfSpawningItems", true);
        Scribe_Values.Look(ref MaxSpawnValue, "MaxSpawnValue", 50f);
        Scribe_Values.Look(ref MinTimeBetweenItems, "MinTimeBetweenItems", GenDate.TicksPerHour * 18);
    }
}