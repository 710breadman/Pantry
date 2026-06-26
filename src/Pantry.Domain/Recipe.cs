namespace Pantry.Domain;

public sealed record Recipe
{
    public required string Id { get; init; }

    public required string RecipeVersion { get; init; }

    public required CatalogEntry Catalog { get; init; }

    public required ProviderType PreferredProvider { get; init; }

    public required RecipeSource Source { get; init; }

    public required TrustLevel TrustLevel { get; init; }

    public required MachineScopePreference ScopePreference { get; init; }

    public required AdministratorRequirement AdministratorRequirement { get; init; }

    public string? PortableDestinationHint { get; init; }

    public required IReadOnlyList<string> Dependencies { get; init; }

    public required IReadOnlyList<string> Conflicts { get; init; }

    public required IReadOnlyList<int> ExpectedExitCodes { get; init; }

    public required string RebootBehavior { get; init; }

    public required DetectionRecipe Detection { get; init; }

    public required RecipeAction Update { get; init; }

    public required RecipeAction Uninstall { get; init; }

    public required RecipeVerification Verification { get; init; }
}

