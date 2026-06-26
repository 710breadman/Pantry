namespace Pantry.Queue;

public enum QueueJobStatus
{
    Planned,
    WaitingForReview,
    WaitingForDependencies
}
