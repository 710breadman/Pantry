using Pantry.Domain;

namespace Pantry.Catalog;

public sealed record ProfileFile
{
    public required string CatalogVersion { get; init; }

    public required IReadOnlyList<Profile> Profiles { get; init; }
}

