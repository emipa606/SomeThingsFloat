using System;
using AchievementsExpanded;
using Verse;

namespace SomeThingsFloat;

public class EnemyDrownedTracker : TrackerBase
{
    private int count = 1;

    [Unsaved] private int triggeredCount; //Only for display

    public EnemyDrownedTracker()
    {
    }

    public EnemyDrownedTracker(EnemyDrownedTracker reference) : base(reference)
    {
        count = reference.count;
    }

    public override string Key
    {
        get => "EnemyDrownedCurrentTracker";
        set { }
    }

    public override Func<bool> AttachToLongTick => Trigger;

    protected override string[] DebugText => [$"Count: {count}"];

    public override (float percent, string text) PercentComplete => count > 1
        ? ((float)triggeredCount / count, $"{triggeredCount} / {count}")
        : base.PercentComplete;


    public override bool UnlockOnStartup => Trigger();


    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref count, "count", 1);
    }

    public override bool Trigger()
    {
        base.Trigger();
        triggeredCount = SomeThingsFloat.GetEnemyPawnsDrowned();

        return triggeredCount >= count;
    }
}