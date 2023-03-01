using Mlie;
using UnityEngine;
using Verse;

namespace SomeThingsFloat;

[StaticConstructorOnStartup]
internal class SomeThingsFloatMod : Mod
{
    /// <summary>
    ///     The instance of the settings to be read by the mod
    /// </summary>
    public static SomeThingsFloatMod instance;

    private static string currentVersion;

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="content"></param>
    public SomeThingsFloatMod(ModContentPack content) : base(content)
    {
        instance = this;
        Settings = GetSettings<SomeThingsFloatSettings>();
        currentVersion = VersionFromManifest.GetVersionFromModMetaData(content.ModMetaData);
    }

    /// <summary>
    ///     The instance-settings for the mod
    /// </summary>
    internal SomeThingsFloatSettings Settings { get; }

    /// <summary>
    ///     The title for the mod-settings
    /// </summary>
    /// <returns></returns>
    public override string SettingsCategory()
    {
        return "Some Things Float";
    }

    /// <summary>
    ///     The settings-window
    ///     For more info: https://rimworldwiki.com/wiki/Modding_Tutorials/ModSettings
    /// </summary>
    /// <param name="rect"></param>
    public override void DoSettingsWindowContents(Rect rect)
    {
        var listing_Standard = new Listing_Standard();
        listing_Standard.Begin(rect);
        listing_Standard.Gap();
        listing_Standard.CheckboxLabeled("STF.ForbidWhenMoving".Translate(), ref Settings.ForbidWhenMoving,
            "STF.ForbidWhenMovingTT".Translate());
        listing_Standard.CheckboxLabeled("STF.FloatUnderBridges".Translate(), ref Settings.FloatUnderBridges,
            "STF.FloatUnderBridgesTT".Translate());
        listing_Standard.CheckboxLabeled("STF.DownedPawnsFloat".Translate(), ref Settings.DownedPawnsFloat,
            "STF.DownedPawnsFloatTT".Translate());
        listing_Standard.CheckboxLabeled("STF.DespawnAtMapEdge".Translate(), ref Settings.DespawnAtMapEdge,
            "STF.DespawnAtMapEdgeTT".Translate());
        listing_Standard.CheckboxLabeled("STF.SpawnNewItems".Translate(), ref Settings.SpawnNewItems,
            "STF.SpawnNewItemsTT".Translate());
        if (Settings.SpawnNewItems)
        {
            listing_Standard.Label("STF.MaxSpawnValue".Translate(Settings.MaxSpawnValue.ToStringMoney()), -1,
                "STF.MaxSpawnValueTT".Translate());
            Settings.MaxSpawnValue = listing_Standard.Slider(Settings.MaxSpawnValue, 0, 500f);
            listing_Standard.CheckboxLabeled("STF.SpawnLivingPawns".Translate(), ref Settings.SpawnLivingPawns,
                "STF.SpawnLivingPawnsTT".Translate());
        }

        listing_Standard.CheckboxLabeled("STF.VerboseLogging".Translate(), ref Settings.VerboseLogging,
            "STF.VerboseLoggingTT".Translate());
        if (currentVersion != null)
        {
            listing_Standard.Gap();
            GUI.contentColor = Color.gray;
            listing_Standard.Label("STF.CurrentModVersion".Translate(currentVersion));
            GUI.contentColor = Color.white;
        }

        listing_Standard.End();
    }
}