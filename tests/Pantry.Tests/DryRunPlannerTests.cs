using Pantry.Catalog;
using Pantry.Core;
using Pantry.Domain;

namespace Pantry.Tests;

public sealed class DryRunPlannerTests
{
    [Fact]
    public async Task Gaming_profile_creates_install_and_skip_items()
    {
        var catalog = await LoadCatalogAsync();
        var planner = new DryRunPlanner();

        var plan = await planner.CreatePlanAsync(new DryRunPlanRequest
        {
            Catalog = catalog,
            Profile = catalog.GetProfile("gaming-setup")
        });

        var steam = Assert.Single(plan.Items, item => item.AppId == "steam");
        var autoruns = Assert.Single(plan.Items, item => item.AppId == "sysinternals-autoruns");

        Assert.Equal(DryRunIntent.Install, steam.Intent);
        Assert.Equal(ProviderType.Winget, steam.PreferredProvider);
        Assert.Equal(AdministratorRequirement.Required, steam.AdministratorRequirement);
        Assert.Equal(DryRunIntent.Skip, autoruns.Intent);
    }

    [Fact]
    public async Task Update_available_state_creates_update_intent()
    {
        var catalog = await LoadCatalogAsync();
        var planner = new DryRunPlanner();

        var plan = await planner.CreatePlanAsync(new DryRunPlanRequest
        {
            Catalog = catalog,
            Profile = catalog.GetProfile("gaming-setup"),
            DetectionResults = new Dictionary<string, AppDetectionResult>(StringComparer.OrdinalIgnoreCase)
            {
                ["7zip"] = Detection("7zip", DetectedAppState.UpdateAvailable)
            }
        });

        var sevenZip = Assert.Single(plan.Items, item => item.AppId == "7zip");

        Assert.Equal(DryRunIntent.Update, sevenZip.Intent);
    }

    [Fact]
    public async Task Portable_item_includes_destination()
    {
        var catalog = await LoadCatalogAsync();
        var planner = new DryRunPlanner();

        var plan = await planner.CreatePlanAsync(new DryRunPlanRequest
        {
            Catalog = catalog,
            Profile = catalog.GetProfile("repair-toolkit-safe"),
            PortableDestination = @"E:\PantryTools"
        });

        var autoruns = Assert.Single(plan.Items, item => item.AppId == "sysinternals-autoruns");

        Assert.Equal(DryRunIntent.Install, autoruns.Intent);
        Assert.Equal(ProviderType.PortableArchive, autoruns.PreferredProvider);
        Assert.Equal(@"E:\PantryTools", autoruns.PortableDestination);
    }

    private static Task<CatalogSnapshot> LoadCatalogAsync()
    {
        var loader = new BundledCatalogLoader(new RecipeValidator());
        return loader.LoadAsync(CatalogTestPaths.BundledCatalogRoot());
    }

    private static AppDetectionResult Detection(string appId, DetectedAppState state)
    {
        return new AppDetectionResult
        {
            AppId = appId,
            State = state,
            Confidence = DetectionConfidence.High,
            Evidence = [],
            Summary = "Test detection result."
        };
    }
}
