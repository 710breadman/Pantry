using Pantry.Domain;

namespace Pantry.Queue;

public sealed class QueuePlanner
{
    public Task<QueueSessionPlan> CreatePlanAsync(
        DryRunPlan dryRunPlan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dryRunPlan);
        cancellationToken.ThrowIfCancellationRequested();

        var jobs = dryRunPlan.Items
            .Where(item => item.Intent is DryRunIntent.Install or DryRunIntent.Update)
            .Select((item, index) => CreateJob(item, index + 1))
            .ToArray();

        return Task.FromResult(new QueueSessionPlan
        {
            Id = Guid.NewGuid().ToString("N"),
            ProfileId = dryRunPlan.ProfileId,
            ProfileName = dryRunPlan.ProfileName,
            CreatedUtc = DateTimeOffset.UtcNow,
            Jobs = jobs
        });
    }

    private static QueueJobPlan CreateJob(DryRunPlanItem item, int order)
    {
        var reviewReason = ResolveReviewReason(item);

        return new QueueJobPlan
        {
            Order = order,
            AppId = item.AppId,
            AppName = item.AppName,
            Action = item.Intent == DryRunIntent.Update ? QueueJobAction.Update : QueueJobAction.Install,
            Provider = item.PreferredProvider,
            TrustLevel = item.TrustLevel,
            ScopePreference = item.ScopePreference,
            AdministratorRequirement = item.AdministratorRequirement,
            Dependencies = item.Dependencies,
            Conflicts = item.Conflicts,
            ReviewState = reviewReason is null ? QueueJobReviewState.Ready : QueueJobReviewState.ReviewRequired,
            ReviewReason = reviewReason ?? "Ready for future queue review."
        };
    }

    private static string? ResolveReviewReason(DryRunPlanItem item)
    {
        if (item.Conflicts.Count > 0)
        {
            return $"Selected conflict warning: {item.ConflictSummary}";
        }

        if (item.TrustLevel != TrustLevel.VerifiedUnattended)
        {
            return $"Trust is {item.TrustLevel}; unattended execution is not allowed yet.";
        }

        return null;
    }
}
