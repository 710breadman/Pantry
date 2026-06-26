namespace Pantry.Domain;

public sealed record CatalogSnapshot
{
    public required string CatalogVersion { get; init; }

    public required IReadOnlyList<Recipe> Recipes { get; init; }

    public required IReadOnlyList<Profile> Profiles { get; init; }

    public Recipe GetRecipe(string appId)
    {
        return Recipes.First(recipe => string.Equals(recipe.Id, appId, StringComparison.OrdinalIgnoreCase));
    }

    public Profile GetProfile(string profileId)
    {
        return Profiles.First(profile => string.Equals(profile.Id, profileId, StringComparison.OrdinalIgnoreCase));
    }
}

