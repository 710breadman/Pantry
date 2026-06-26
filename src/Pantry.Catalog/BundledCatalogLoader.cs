using System.Text.Json;
using Pantry.Domain;

namespace Pantry.Catalog;

public sealed class BundledCatalogLoader
{
    private readonly RecipeValidator _validator;

    public BundledCatalogLoader(RecipeValidator validator)
    {
        _validator = validator;
    }

    public async Task<CatalogSnapshot> LoadAsync(string catalogRoot, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogRoot);

        var root = Path.GetFullPath(catalogRoot);
        var schemaPath = Path.Combine(root, "recipe.schema.json");
        var recipeDirectory = Path.Combine(root, "recipes");
        var profilePath = Path.Combine(root, "profiles.json");

        if (!File.Exists(schemaPath))
        {
            throw new CatalogLoadException($"Recipe schema was not found at '{schemaPath}'.");
        }

        if (!Directory.Exists(recipeDirectory))
        {
            throw new CatalogLoadException($"Recipe directory was not found at '{recipeDirectory}'.");
        }

        if (!File.Exists(profilePath))
        {
            throw new CatalogLoadException($"Profile file was not found at '{profilePath}'.");
        }

        var recipePaths = Directory
            .EnumerateFiles(recipeDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (recipePaths.Length == 0)
        {
            throw new CatalogLoadException("Bundled catalog does not contain any Recipe files.");
        }

        var recipes = new List<Recipe>();
        foreach (var recipePath in recipePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var json = await File.ReadAllTextAsync(recipePath, cancellationToken).ConfigureAwait(false);
            await _validator.ValidateAsync(json, schemaPath, cancellationToken).ConfigureAwait(false);

            var recipe = JsonSerializer.Deserialize<Recipe>(json, RecipeJson.Options)
                ?? throw new CatalogLoadException($"Recipe '{recipePath}' could not be read.");

            recipes.Add(recipe);
        }

        EnsureUniqueRecipeIds(recipes);

        var profileJson = await File.ReadAllTextAsync(profilePath, cancellationToken).ConfigureAwait(false);
        var profileFile = JsonSerializer.Deserialize<ProfileFile>(profileJson, RecipeJson.Options)
            ?? throw new CatalogLoadException($"Profile file '{profilePath}' could not be read.");

        EnsureProfilesReferenceKnownRecipes(profileFile.Profiles, recipes);

        return new CatalogSnapshot
        {
            CatalogVersion = profileFile.CatalogVersion,
            Recipes = recipes,
            Profiles = profileFile.Profiles
        };
    }

    private static void EnsureUniqueRecipeIds(IEnumerable<Recipe> recipes)
    {
        var duplicates = recipes
            .GroupBy(recipe => recipe.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new CatalogLoadException($"Duplicate Recipe IDs found: {string.Join(", ", duplicates)}.");
        }
    }

    private static void EnsureProfilesReferenceKnownRecipes(IEnumerable<Profile> profiles, IEnumerable<Recipe> recipes)
    {
        var recipeIds = recipes.Select(recipe => recipe.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownSelections = profiles
            .SelectMany(profile => profile.Selections.Select(selection => new { profile.Id, selection.AppId }))
            .Where(selection => !recipeIds.Contains(selection.AppId))
            .Select(selection => $"{selection.Id}:{selection.AppId}")
            .ToArray();

        if (unknownSelections.Length > 0)
        {
            throw new CatalogLoadException($"Profiles reference unknown app IDs: {string.Join(", ", unknownSelections)}.");
        }
    }
}

