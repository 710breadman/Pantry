namespace Pantry.Domain;

public sealed record DetectionEvidence
{
    public required string Source { get; init; }

    public required string Detail { get; init; }
}

