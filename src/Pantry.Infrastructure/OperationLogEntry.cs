namespace Pantry.Infrastructure;

public sealed record OperationLogEntry
{
    public required string Id { get; init; }

    public required DateTimeOffset TimestampUtc { get; init; }

    public required string Category { get; init; }

    public required string Message { get; init; }

    public string? DetailsJson { get; init; }
}

