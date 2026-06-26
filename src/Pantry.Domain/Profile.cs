namespace Pantry.Domain;

public sealed record Profile
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<Selection> Selections { get; init; }
}

