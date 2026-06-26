using Pantry.Catalog;

namespace Pantry.Tests;

public sealed class RecipeValidationTests
{
    [Fact]
    public async Task Valid_recipe_passes_schema_validation()
    {
        var catalogRoot = CatalogTestPaths.BundledCatalogRoot();
        var schemaPath = Path.Combine(catalogRoot, "recipe.schema.json");
        var recipePath = Path.Combine(catalogRoot, "recipes", "7zip.json");
        var json = await File.ReadAllTextAsync(recipePath);

        var validator = new RecipeValidator();

        await validator.ValidateAsync(json, schemaPath);
    }

    [Fact]
    public async Task Malformed_recipe_is_rejected()
    {
        var catalogRoot = CatalogTestPaths.BundledCatalogRoot();
        var schemaPath = Path.Combine(catalogRoot, "recipe.schema.json");
        const string malformedRecipe = """{"id":"broken"}""";

        var validator = new RecipeValidator();

        await Assert.ThrowsAsync<RecipeValidationException>(() =>
            validator.ValidateAsync(malformedRecipe, schemaPath));
    }
}

