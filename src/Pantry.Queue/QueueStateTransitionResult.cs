namespace Pantry.Queue;

public sealed record QueueStateTransitionResult
{
    public required bool IsAllowed { get; init; }

    public required string Reason { get; init; }
}
