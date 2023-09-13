using System;
using Mlie;
using RimWorld;
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

    //private Vector2 scrollPosition;

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
        var viewContainer = rect.ContractedBy(10);
        //var innerContainer = viewContainer.ContractedBy(6);
        //innerContainer.width -= 20;
        //innerContainer.height += 180;
        //Widgets.BeginScrollView(viewContainer, ref scrollPosition, innerContainer);
        var listing_Standard = new Listing_Standard();
        listing_Standard.Begin(viewContainer);
        listing_Standard.ColumnWidth = (viewContainer.width / 2) - 20;
        Settings.RelativeFloatSpeed = listing_Standard.SliderLabeled(
            "STF.RelativeFloatSpeed".Translate(Settings.RelativeFloatSpeed.ToStringPercent()),
            Settings.RelativeFloatSpeed, 0.1f, 2.5f, 0.5f, "STF.RelativeFloatSpeedTT".Translate());
        listing_Standard.CheckboxLabeled("STF.ForbidWhenMoving".Translate(), ref Settings.ForbidWhenMoving,
            "STF.ForbidWhenMovingTT".Translate());
        listing_Standard.CheckboxLabeled("STF.FloatUnderBridges".Translate(), ref Settings.FloatUnderBridges,
            "STF.FloatUnderBridgesTT".Translate());
        if (Settings.FloatUnderBridges)
        {
            listing_Standard.CheckboxLabeled("STF.FloatUnderBridgesInfo".Translate(),
                ref Settings.FloatUnderBridgesInfo,
                "STF.FloatUnderBridgesInfoTT".Translate());
        }

        listing_Standard.CheckboxLabeled("STF.SmoothAnimation".Translate(), ref Settings.SmoothAnimation,
            "STF.SmoothAnimationTT".Translate());
        if (Settings.SmoothAnimation)
        {
            listing_Standard.CheckboxLabeled("STF.Bobbing".Translate(), ref Settings.Bobbing,
                "STF.BobbingTT".Translate());
        }

        listing_Standard.CheckboxLabeled("STF.DownedPawnsFloat".Translate(), ref Settings.DownedPawnsFloat,
            "STF.DownedPawnsFloatTT".Translate());
        listing_Standard.CheckboxLabeled("STF.DownedPawnsDrown".Translate(), ref Settings.DownedPawnsDrown,
            "STF.DownedPawnsDrownTT".Translate());
        listing_Standard.CheckboxLabeled("STF.WarnForAllFriendlyPawns".Translate(),
            ref Settings.WarnForAllFriendlyPawns,
            "STF.WarnForAllFriendlyPawnsTT".Translate());
        listing_Standard.CheckboxLabeled("STF.PawnsCanFall".Translate(), ref Settings.PawnsCanFall,
            "STF.PawnsCanFallTT".Translate());
        if (Settings.PawnsCanFall)
        {
            listing_Standard.Label(
                "STF.ManipulationThreshold".Translate(Settings.ManipulationThreshold.ToStringPercent()), -1,
                "STF.ManipulationThresholdTT".Translate());
            Settings.ManipulationThreshold = listing_Standard.Slider(Settings.ManipulationThreshold, 0, 1f);

            listing_Standard.Label(
                "STF.RelativeChanceInShallows".Translate(Settings.RelativeChanceInShallows.ToStringPercent()), -1,
                "STF.RelativeChanceInShallowsTT".Translate());
            Settings.RelativeChanceInShallows = listing_Standard.Slider(Settings.RelativeChanceInShallows, 0, 1f);
        }

        listing_Standard.NewColumn();
        listing_Standard.CheckboxLabeled("STF.ReservedItemsWillNotMove".Translate(),
            ref Settings.ReservedItemsWillNotMove,
            "STF.ReservedItemsWillNotMoveTT".Translate());
        listing_Standard.CheckboxLabeled("STF.AllowOnStuck".Translate(),
            ref Settings.AllowOnStuck,
            "STF.AllowOnStuckTT".Translate());
        listing_Standard.CheckboxLabeled("STF.DespawnAtMapEdge".Translate(), ref Settings.DespawnAtMapEdge,
            "STF.DespawnAtMapEdgeTT".Translate());
        listing_Standard.CheckboxLabeled("STF.SpawnNewItems".Translate(), ref Settings.SpawnNewItems,
            "STF.SpawnNewItemsTT".Translate());
        if (Settings.SpawnNewItems)
        {
            listing_Standard.Label("STF.MaxSpawnValue".Translate(Settings.MaxSpawnValue.ToStringMoney()), -1,
                "STF.MaxSpawnValueTT".Translate());
            Settings.MaxSpawnValue = listing_Standard.Slider(Settings.MaxSpawnValue, 0, 500f);
            listing_Standard.Label(
                "STF.MinTimeBetweenItems".Translate(
                    ((int)Settings.MinTimeBetweenItems).ToStringTicksToPeriodVague(false)),
                -1,
                "STF.MinTimeBetweenItemsTT".Translate());
            Settings.MinTimeBetweenItems =
                (float)Math.Round(listing_Standard.Slider(Settings.MinTimeBetweenItems, 0, GenDate.TicksPerDay * 7));

            var originalSpawnInOceanTilesValue = Settings.SpawnInOceanTiles;
            listing_Standard.CheckboxLabeled("STF.SpawnInOceanTiles".Translate(), ref Settings.SpawnInOceanTiles,
                "STF.SpawnInOceanTilesTT".Translate());
            if (originalSpawnInOceanTilesValue != Settings.SpawnInOceanTiles)
            {
                foreach (var floatingThingsMapComponent in SomeThingsFloat.FloatingMapComponents)
                {
                    floatingThingsMapComponent.Value.ClearEdgeCells();
                }
            }

            if (SomeThingsFloat.HaulUrgentlyDef != null)
            {
                var originalHaulValue = Settings.HaulUrgently;
                listing_Standard.CheckboxLabeled("STF.HaulUrgently".Translate(), ref Settings.HaulUrgently,
                    "STF.HaulUrgentlyTT".Translate());
                if (originalHaulValue != Settings.HaulUrgently && Settings.HaulUrgently)
                {
                    Settings.ForbidSpawningItems = false;
                }
            }

            var originalForbidValue = Settings.ForbidSpawningItems;
            listing_Standard.CheckboxLabeled("STF.ForbidSpawningItems".Translate(), ref Settings.ForbidSpawningItems,
                "STF.ForbidSpawningItemsTT".Translate());
            if (originalForbidValue != Settings.ForbidSpawningItems && Settings.ForbidSpawningItems)
            {
                Settings.HaulUrgently = false;
            }

            listing_Standard.CheckboxLabeled("STF.SpawnLivingPawns".Translate(), ref Settings.SpawnLivingPawns,
                "STF.SpawnLivingPawnsTT".Translate());
            listing_Standard.CheckboxLabeled("STF.SpawnFertilizedEggs".Translate(), ref Settings.SpawnFertilizedEggs,
                "STF.SpawnFertilizedEggsTT".Translate());
            listing_Standard.CheckboxLabeled("STF.NotifyOfSpawningItems".Translate(),
                ref Settings.NotifyOfSpawningItems,
                "STF.NotifyOfSpawningItemsTT".Translate());
        }

        listing_Standard.CheckboxLabeled("STF.VerboseLogging".Translate(), ref Settings.VerboseLogging,
            "STF.VerboseLoggingTT".Translate());
        if (Settings.VerboseLogging)
        {
            listing_Standard.CheckboxLabeled("STF.DebugLogging".Translate(), ref Settings.DebugLogging,
                "STF.DebugLoggingTT".Translate());
        }

        if (currentVersion != null)
        {
            listing_Standard.Gap();
            GUI.contentColor = Color.gray;
            listing_Standard.Label("STF.CurrentModVersion".Translate(currentVersion));
            GUI.contentColor = Color.white;
        }

        listing_Standard.End();
        //Widgets.EndScrollView();
    }
}