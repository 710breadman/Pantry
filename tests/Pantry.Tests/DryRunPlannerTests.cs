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

    [Fact]
    public async Task Required_dependency_is_planned_before_selected_dependent()
    {
        var catalog = TestCatalog(
        [
            TestRecipe("dependent-app", "Dependent App", ["runtime-app"]),
            TestRecipe("runtime-app", "Runtime App")
        ],
        [
            new Selection
            {
                AppId = "dependent-app",
                Preselected = true,
                Reason = "Test dependent app."
            }
        ]);
        var planner = new DryRunPlanner();

        var plan = await planner.CreatePlanAsync(new DryRunPlanRequest
        {
            Catalog = catalog,
            Profile = catalog.GetProfile("test-profile")
        });

        var runtimeIndex = plan.Items.ToList().FindIndex(item => item.AppId == "runtime-app");
        var dependentIndex = plan.Items.ToList().FindIndex(item => item.AppId == "dependent-app");
        var runtime = plan.Items[runtimeIndex];

        Assert.True(runtimeIndex >= 0);
        Assert.True(dependentIndex >= 0);
        Assert.True(runtimeIndex < dependentIndex);
        Assert.Equal(DryRunIntent.Install, runtime.Intent);
        Assert.Contains("Required by selected app", runtime.Reason);
    }

    [Fact]
    public async Task Dependency_cycle_does_not_duplicate_review_items()
    {
        var catalog = TestCatalog(
        [
            TestRecipe("alpha", "Alpha", ["beta"]),
            TestRecipe("beta", "Beta", ["alpha"])
        ],
        [
            new Selection
            {
                AppId = "alpha",
                Preselected = true,
                Reason = "Cycle test."
            }
        ]);
        var planner = new DryRunPlanner();

        var plan = await planner.CreatePlanAsync(new DryRunPlanRequest
        {
            Catalog = catalog,
            Profile = catalog.GetProfile("test-profile")
        });

        Assert.Equal(2, plan.Items.Count);
        Assert.Single(plan.Items, item => item.AppId == "alpha");
        Assert.Single(plan.Items, item => item.AppId == "beta");
    }

    [Fact]
    public async Task Selected_conflicts_are_reported_on_both_review_items()
    {
        var catalog = TestCatalog(
        [
            TestRecipe("alpha", "Alpha", conflicts: ["beta"]),
            TestRecipe("beta", "Beta")
        ],
        [
            new Selection
            {
                AppId = "alpha",
                Preselected = true,
                Reason = "First selected app."
            },
            new Selection
            {
                AppId = "beta",
                Preselected = true,
                Reason = "Second selected app."
            }
        ]);
        var planner = new DryRunPlanner();

        var plan = await planner.CreatePlanAsync(new DryRunPlanRequest
        {
            Catalog = catalog,
            Profile = catalog.GetProfile("test-profile")
        });

        var alpha = Assert.Single(plan.Items, item => item.AppId == "alpha");
        var beta = Assert.Single(plan.Items, item => item.AppId == "beta");

        Assert.Contains("Beta", alpha.Conflicts);
        Assert.Contains("Alpha", beta.Conflicts);
        Assert.Contains("Conflicts with selected app", alpha.ConflictSummary);
        Assert.Contains("Conflicts with selected app", beta.ConflictSummary);
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

    private static CatalogSnapshot TestCatalog(IReadOnlyList<Recipe> recipes, IReadOnlyList<Selection> selections)
    {
        return new CatalogSnapshot
        {
            CatalogVersion = "test",
            Recipes = recipes,
            Profiles =
            [
                new Profile
                {
                    Id = "test-profile",
                    Name = "Test Profile",
                    Description = "Used by dry-run planner tests.",
                    Selections = selections
                }
            ]
        };
    }

    private static Recipe TestRecipe(
        string appId,
        string name,
        IReadOnlyList<string>? dependencies = null,
        IReadOnlyList<string>? conflicts = null)
    {
        return new Recipe
        {
            Id = appId,
            RecipeVersion = "1.0.0",
            Catalog = new CatalogEntry
            {
                Name = name,
                ShortDescription = "Test app.",
                Category = "Test",
                Homepage = "https://example.com"
            },
            PreferredProvider = ProviderType.Winget,
            Source = new RecipeSource
            {
                Type = ProviderType.Winget,
                Identifier = appId,
                OfficialUrl = "https://example.com"
            },
            TrustLevel = TrustLevel.Experimental,
            ScopePreference = MachineScopePreference.Preferred,
            AdministratorRequirement = AdministratorRequirement.Required,
            Dependencies = dependencies ?? [],
            Conflicts = conflicts ?? [],
            ExpectedExitCodes = [0],
            RebootBehavior = "No automatic reboot.",
            Detection = new DetectionRecipe
            {
                Rules = [],
                MinimumConfidence = "low"
            },
            Update = new RecipeAction
            {
                Method = "none",
                Supported = false
            },
            Uninstall = new RecipeAction
            {
                Method = "none",
                Supported = false
            },
            Verification = new RecipeVerification
            {
                VerifiedOn = new DateOnly(2026, 6, 26),
                Evidence = "Unit test fixture."
            }
        };
    }
}
