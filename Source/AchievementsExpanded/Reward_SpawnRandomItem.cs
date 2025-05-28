using AchievementsExpanded;
using Verse;

namespace SomeThingsFloat;

public class Reward_SpawnRandomItem : AchievementReward
{
    public override string Disabled
    {
        get
        {
            var reason = base.Disabled;
            if (Find.CurrentMap is null)
            {
                reason += "\n" + "NoValidMap".Translate();
            }

            if (!SomeThingsFloatMod.Instance.Settings.SpawnNewItems)
            {
                reason += "\n" + "STF.SpawnNewItemsDisabled".Translate();
            }

            return reason;
        }
    }

    public override bool TryExecuteEvent()
    {
        var map = Find.CurrentMap;
        if (map == null)
        {
            Log.Error("Failed to find map to spawn item. Canceling request.");
            return false;
        }

        if (SomeThingsFloat.FloatingMapComponents.TryGetValue(map, out var mapComponent))
        {
            if (mapComponent.TrySpawnThingAtMapEdge(true))
            {
                return true;
            }

            Log.Error(
                "Failed to spawn item, reason will be shown if verbose logging is turned on in the mod-settings. Canceling request.");
            return false;
        }

        Log.Error("No mapcomponent found for current map. Canceling request.");
        return false;
    }
}