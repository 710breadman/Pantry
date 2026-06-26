using Pantry.Catalog;

namespace Pantry.Tests;

public sealed class CatalogLoadingTests
{
    [Fact]
    public async Task Bundled_catalog_loads_approved_apps_and_profiles()
    {
        var loader = new BundledCatalogLoader(new RecipeValidator());

        var catalog = await loader.LoadAsync(CatalogTestPaths.BundledCatalogRoot());

        Assert.Equal(5, catalog.Recipes.Count);
        Assert.Contains(catalog.Recipes, recipe => recipe.Id == "7zip");
        Assert.Contains(catalog.Recipes, recipe => recipe.Id == "vlc");
        Assert.Contains(catalog.Recipes, recipe => recipe.Id == "steam");
        Assert.Contains(catalog.Recipes, recipe => recipe.Id == "firefox");
        Assert.Contains(catalog.Recipes, recipe => recipe.Id == "sysinternals-autoruns");
        Assert.Contains(catalog.Profiles, profile => profile.Id == "gaming-setup");
        Assert.Contains(catalog.Profiles, profile => profile.Id == "living-room-media-pc");
        Assert.Contains(catalog.Profiles, profile => profile.Id == "repair-toolkit-safe");
    }

    [Fact]
    public async Task Profile_defaults_preselect_strong_choices()
    {
        var loader = new BundledCatalogLoader(new RecipeValidator());
        var catalog = await loader.LoadAsync(CatalogTestPaths.BundledCatalogRoot());

        var gaming = catalog.GetProfile("gaming-setup");
        var media = catalog.GetProfile("living-room-media-pc");
        var repair = catalog.GetProfile("repair-toolkit-safe");

        Assert.Contains(gaming.Selections, selection => selection.AppId == "steam" && selection.Preselected);
        Assert.Contains(gaming.Selections, selection => selection.AppId == "7zip" && selection.Preselected);
        Assert.Contains(media.Selections, selection => selection.AppId == "vlc" && selection.Preselected);
        Assert.Contains(repair.Selections, selection => selection.AppId == "sysinternals-autoruns" && selection.Preselected);
    }
}

