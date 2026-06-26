namespace Pantry.Domain;

public sealed record AppDetectionResult
{
    public required string AppId { get; init; }

    public required DetectedAppState State { get; init; }

    public required DetectionConfidence Confidence { get; init; }

    public string? InstalledVersion { get; init; }

    public string? AvailableVersion { get; init; }

    public required IReadOnlyList<DetectionEvidence> Evidence { get; init; }

    public required string Summary { get; init; }
}

