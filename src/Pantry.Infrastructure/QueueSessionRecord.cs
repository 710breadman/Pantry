namespace Pantry.Infrastructure;

public sealed record QueueSessionRecord
{
    public required string Id { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public required string ProfileId { get; init; }

    public required string ProfileName { get; init; }

    public required int JobCount { get; init; }

    public required int ReviewRequiredCount { get; init; }
}
