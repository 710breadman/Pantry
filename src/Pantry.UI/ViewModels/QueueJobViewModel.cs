using Pantry.Infrastructure;

namespace Pantry.UI.ViewModels;

public sealed class QueueJobViewModel
{
    public QueueJobViewModel(QueueJobRecord job)
    {
        Title = $"{job.Order}. {job.Action}: {job.AppName}";
        Status = $"{job.Status} | Review: {job.ReviewState}";
        Provider = $"{job.Provider} | Trust: {job.TrustLevel}";
        var blockers = job.BlockedByAppIds.Count > 0
            ? $" Blocked by: {string.Join(", ", job.BlockedByAppIds)}."
            : string.Empty;
        Reason = $"{job.ReviewReason}{blockers} Retry: {job.RetryMode} ({job.MaxRetryAttempts} auto). Cancel: {job.CancellationBehavior}. Failure: {job.FailureBehavior}.";
    }

    public string Title { get; }

    public string Status { get; }

    public string Provider { get; }

    public string Reason { get; }
}
