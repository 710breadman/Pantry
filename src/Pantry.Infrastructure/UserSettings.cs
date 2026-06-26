namespace Pantry.Infrastructure;

public sealed record UserSettings
{
    public string? SelectedProfileId { get; init; }

    public string? PortableDestination { get; init; }
}

