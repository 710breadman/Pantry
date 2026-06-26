using Pantry.Catalog;
using Pantry.Detection;
using Pantry.Domain;

namespace Pantry.Tests;

public sealed class PortableFolderDetectionProviderTests
{
    [Fact]
    public async Task Existing_portable_folder_returns_installed_current()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"pantry-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        try
        {
            var recipe = await LoadAutorunsRecipeAsync();
            var provider = new PortableFolderDetectionProvider();

            var result = await provider.DetectAsync(recipe, folder);

            Assert.Equal(DetectedAppState.InstalledCurrent, result.State);
            Assert.Equal(DetectionConfidence.Medium, result.Confidence);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Missing_portable_folder_returns_not_installed()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"pantry-test-{Guid.NewGuid():N}");
        var recipe = await LoadAutorunsRecipeAsync();
        var provider = new PortableFolderDetectionProvider();

        var result = await provider.DetectAsync(recipe, folder);

        Assert.Equal(DetectedAppState.NotInstalled, result.State);
        Assert.Equal(DetectionConfidence.Medium, result.Confidence);
    }

    private static async Task<Recipe> LoadAutorunsRecipeAsync()
    {
        var loader = new BundledCatalogLoader(new RecipeValidator());
        var catalog = await loader.LoadAsync(CatalogTestPaths.BundledCatalogRoot());
        return catalog.GetRecipe("sysinternals-autoruns");
    }
}

