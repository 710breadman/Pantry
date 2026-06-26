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

        var reviewState = reviewReason is null ? QueueJobReviewState.Ready : QueueJobReviewState.ReviewRequired;

        return new QueueJobPlan
        {
            Order = order,
            AppId = item.AppId,
            AppName = item.AppName,
            Action = item.Intent == DryRunIntent.Update ? QueueJobAction.Update : QueueJobAction.Install,
            Status = reviewState == QueueJobReviewState.Ready
                ? QueueJobStatus.Planned
                : QueueJobStatus.WaitingForReview,
            RetryMode = QueueRetryMode.ManualOnly,
            MaxRetryAttempts = 0,
            CancellationBehavior = QueueCancellationBehavior.CancelBeforeStartOnly,
            FailureBehavior = QueueFailureBehavior.PauseDependentsContinueUnrelated,
            Provider = item.PreferredProvider,
            TrustLevel = item.TrustLevel,
            ScopePreference = item.ScopePreference,
            AdministratorRequirement = item.AdministratorRequirement,
            Dependencies = item.Dependencies,
            Conflicts = item.Conflicts,
            ReviewState = reviewState,
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
