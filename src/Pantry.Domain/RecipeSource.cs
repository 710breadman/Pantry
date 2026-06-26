namespace Pantry.Domain;

public sealed record RecipeSource
{
    public required ProviderType Type { get; init; }

    public required string Identifier { get; init; }

    public required string OfficialUrl { get; init; }
}

