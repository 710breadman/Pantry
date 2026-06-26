namespace Pantry.Detection;

public sealed record RegistryAppEntry
{
    public required string DisplayName { get; init; }

    public string? DisplayVersion { get; init; }

    public required string RegistryPath { get; init; }
}

