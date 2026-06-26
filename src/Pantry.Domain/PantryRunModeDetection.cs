namespace Pantry.Domain;

public sealed record PantryRunModeDetection
{
    public required PantryRunMode Mode { get; init; }

    public required string ApplicationDirectory { get; init; }

    public required string StateDirectory { get; init; }

    public required string PortableMarkerPath { get; init; }

    public required string Reason { get; init; }
}
