using Pantry.Catalog;
using Pantry.Detection;
using Pantry.Domain;

namespace Pantry.Tests;

public sealed class RegistryDetectionProviderTests
{
    [Fact]
    public async Task Registry_provider_matches_uninstall_display_name()
    {
        var recipe = await LoadRecipeAsync("7zip");
        var provider = new RegistryDetectionProvider(new FakeRegistryReader(
        [
            new RegistryAppEntry
            {
                DisplayName = "7-Zip 24.09 (x64)",
                DisplayVersion = "24.09",
                RegistryPath = @"LocalMachine\Software\...\7-Zip"
            }
        ]));

        var result = await provider.DetectAsync(recipe);

        Assert.Equal(DetectedAppState.InstalledCurrent, result.State);
        Assert.Equal(DetectionConfidence.Medium, result.Confidence);
        Assert.Equal("24.09", result.InstalledVersion);
    }

    [Fact]
    public async Task Registry_provider_returns_not_installed_when_no_rule_matches()
    {
        var recipe = await LoadRecipeAsync("7zip");
        var provider = new RegistryDetectionProvider(new FakeRegistryReader(
        [
            new RegistryAppEntry
            {
                DisplayName = "Other App",
                RegistryPath = @"LocalMachine\Software\...\Other"
            }
        ]));

        var result = await provider.DetectAsync(recipe);

        Assert.Equal(DetectedAppState.NotInstalled, result.State);
        Assert.Equal(DetectionConfidence.Medium, result.Confidence);
    }

    [Fact]
    public async Task App_detection_service_uses_registry_when_winget_misses_installed_app()
    {
        var recipe = await LoadRecipeAsync("7zip");
        var service = new AppDetectionService(
            new WingetDetectionProvider(new FakeProcessRunner(new ProcessRunResult
            {
                ExitCode = 0,
                StandardOutput = string.Empty,
                StandardError = string.Empty
            })),
            new PortableFolderDetectionProvider(),
            new RegistryDetectionProvider(new FakeRegistryReader(
            [
                new RegistryAppEntry
                {
                    DisplayName = "7-Zip 24.09 (x64)",
                    DisplayVersion = "24.09",
                    RegistryPath = @"LocalMachine\Software\...\7-Zip"
                }
            ])));

        var results = await service.ScanAsync([recipe], portableDestination: null);

        Assert.Equal(DetectedAppState.InstalledCurrent, results["7zip"].State);
        Assert.Equal("24.09", results["7zip"].InstalledVersion);
    }

    private static async Task<Recipe> LoadRecipeAsync(string appId)
    {
        var loader = new BundledCatalogLoader(new RecipeValidator());
        var catalog = await loader.LoadAsync(CatalogTestPaths.BundledCatalogRoot());
        return catalog.GetRecipe(appId);
    }

    private sealed class FakeRegistryReader : IRegistryReader
    {
        private readonly IReadOnlyList<RegistryAppEntry> _entries;

        public FakeRegistryReader(IReadOnlyList<RegistryAppEntry> entries)
        {
            _entries = entries;
        }

        public Task<IReadOnlyList<RegistryAppEntry>> ReadInstalledAppsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_entries);
        }
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        private readonly ProcessRunResult _result;

        public FakeProcessRunner(ProcessRunResult result)
        {
            _result = result;
        }

        public Task<ProcessRunResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }
}

