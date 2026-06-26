using Pantry.Domain;
using Pantry.Queue;

namespace Pantry.Tests;

public sealed class QueuePlannerTests
{
    [Fact]
    public async Task Queue_plan_includes_only_install_and_update_items()
    {
        var planner = new QueuePlanner();

        var plan = await planner.CreatePlanAsync(new DryRunPlan
        {
            ProfileId = "gaming-setup",
            ProfileName = "Gaming Setup",
            Items =
            [
                Item("7zip", "7-Zip", DryRunIntent.Skip),
                Item("steam", "Steam", DryRunIntent.Install),
                Item("vlc", "VLC", DryRunIntent.Update)
            ]
        });

        Assert.Equal(2, plan.Jobs.Count);
        Assert.Equal("steam", plan.Jobs[0].AppId);
        Assert.Equal(QueueJobAction.Install, plan.Jobs[0].Action);
        Assert.Equal("vlc", plan.Jobs[1].AppId);
        Assert.Equal(QueueJobAction.Update, plan.Jobs[1].Action);
    }

    [Fact]
    public async Task Queue_plan_keeps_dry_run_order()
    {
        var planner = new QueuePlanner();

        var plan = await planner.CreatePlanAsync(new DryRunPlan
        {
            ProfileId = "test",
            ProfileName = "Test",
            Items =
            [
                Item("runtime", "Runtime", DryRunIntent.Install),
                Item("app", "App", DryRunIntent.Install, dependencies: ["runtime"])
            ]
        });

        Assert.Equal("runtime", plan.Jobs[0].AppId);
        Assert.Equal(1, plan.Jobs[0].Order);
        Assert.Equal("app", plan.Jobs[1].AppId);
        Assert.Equal(2, plan.Jobs[1].Order);
    }

    [Fact]
    public async Task Queue_plan_requires_review_for_non_verified_or_conflicting_jobs()
    {
        var planner = new QueuePlanner();

        var plan = await planner.CreatePlanAsync(new DryRunPlan
        {
            ProfileId = "test",
            ProfileName = "Test",
            Items =
            [
                Item("safe", "Safe", DryRunIntent.Install, trustLevel: TrustLevel.VerifiedUnattended),
                Item("manual", "Manual", DryRunIntent.Install, trustLevel: TrustLevel.ManualOfficial),
                Item("conflict", "Conflict", DryRunIntent.Install, conflicts: ["Other App"])
            ]
        });

        Assert.Equal(QueueJobReviewState.Ready, plan.Jobs[0].ReviewState);
        Assert.Equal(QueueJobReviewState.ReviewRequired, plan.Jobs[1].ReviewState);
        Assert.Contains("unattended execution is not allowed", plan.Jobs[1].ReviewReason);
        Assert.Equal(QueueJobReviewState.ReviewRequired, plan.Jobs[2].ReviewState);
        Assert.Contains("conflict", plan.Jobs[2].ReviewReason, StringComparison.OrdinalIgnoreCase);
    }

    private static DryRunPlanItem Item(
        string appId,
        string appName,
        DryRunIntent intent,
        TrustLevel trustLevel = TrustLevel.Experimental,
        IReadOnlyList<string>? dependencies = null,
        IReadOnlyList<string>? conflicts = null)
    {
        return new DryRunPlanItem
        {
            AppId = appId,
            AppName = appName,
            Intent = intent,
            PreferredProvider = ProviderType.Winget,
            TrustLevel = trustLevel,
            ScopePreference = MachineScopePreference.Preferred,
            AdministratorRequirement = AdministratorRequirement.Required,
            DetectionState = DetectedAppState.Unknown,
            DetectionConfidence = DetectionConfidence.Unknown,
            DetectionSummary = "Test.",
            Dependencies = dependencies ?? [],
            Conflicts = conflicts ?? [],
            ConflictSummary = conflicts is { Count: > 0 }
                ? $"Conflicts with selected app(s): {string.Join(", ", conflicts)}"
                : "None",
            PortableDestination = null,
            Reason = "Test."
        };
    }
}
