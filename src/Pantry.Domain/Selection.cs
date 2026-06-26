namespace Pantry.Domain;

public sealed record Selection
{
    public required string AppId { get; init; }

    public bool Preselected { get; init; }

    public required string Reason { get; init; }
}

