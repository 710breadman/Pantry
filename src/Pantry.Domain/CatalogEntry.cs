namespace Pantry.Domain;

public sealed record CatalogEntry
{
    public required string Name { get; init; }

    public required string ShortDescription { get; init; }

    public required string Category { get; init; }

    public required string Homepage { get; init; }

    public bool IsPortable { get; init; }
}

