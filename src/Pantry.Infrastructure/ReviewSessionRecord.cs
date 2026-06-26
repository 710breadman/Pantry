namespace Pantry.Infrastructure;

public sealed record ReviewSessionRecord
{
    public required string Id { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public required string ProfileId { get; init; }

    public required string ProfileName { get; init; }

    public required string CatalogVersion { get; init; }

    public required int ItemCount { get; init; }

    public required int InstallCount { get; init; }

    public required int UpdateCount { get; init; }

    public required int SkipCount { get; init; }
}
