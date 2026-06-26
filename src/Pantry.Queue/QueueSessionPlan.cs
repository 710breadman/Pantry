namespace Pantry.Queue;

public sealed record QueueSessionPlan
{
    public required string Id { get; init; }

    public required string ProfileId { get; init; }

    public required string ProfileName { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public required IReadOnlyList<QueueJobPlan> Jobs { get; init; }
}
