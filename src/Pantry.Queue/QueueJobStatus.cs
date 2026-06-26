namespace Pantry.Queue;

public enum QueueJobStatus
{
    Planned,
    WaitingForReview,
    WaitingForDependencies,
    Running,
    Succeeded,
    Failed,
    Cancelled,
    Skipped
}
