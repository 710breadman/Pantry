using Pantry.Catalog;
using Pantry.Detection;
using Pantry.Domain;

namespace Pantry.Tests;

public sealed class FileDetectionProviderTests
{
    [Fact]
    public async Task File_provider_finds_configured_file_path()
    {
        var recipe = await LoadRecipeAsync("7zip");
        var provider = new FileDetectionProvider(new FakeFileSystemReader(
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\7-Zip\7zFM.exe"),
            "24.09"));

        var result = await provider.DetectAsync(recipe);

        Assert.Equal(DetectedAppState.InstalledCurrent, result.State);
        Assert.Equal(DetectionConfidence.Medium, result.Confidence);
        Assert.Equal("24.09", result.InstalledVersion);
    }

    [Fact]
    public async Task File_provider_returns_low_confidence_not_installed_when_path_missing()
    {
        var recipe = await LoadRecipeAsync("7zip");
        var provider = new FileDetectionProvider(new FakeFileSystemReader());

        var result = await provider.DetectAsync(recipe);

        Assert.Equal(DetectedAppState.NotInstalled, result.State);
        Assert.Equal(DetectionConfidence.Low, result.Confidence);
    }

    [Fact]
    public async Task App_detection_service_uses_file_when_winget_and_registry_miss()
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
            new RegistryDetectionProvider(new FakeRegistryReader()),
            new FileDetectionProvider(new FakeFileSystemReader(
                Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\7-Zip\7zFM.exe"),
                "24.09")));

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

    private sealed class FakeFileSystemReader : IFileSystemReader
    {
        private readonly string? _existingPath;
        private readonly string? _version;

        public FakeFileSystemReader(string? existingPath = null, string? version = null)
        {
            _existingPath = existingPath;
            _version = version;
        }

        public bool FileExists(string path)
        {
            return string.Equals(path, _existingPath, StringComparison.OrdinalIgnoreCase);
        }

        public string? TryGetFileVersion(string path)
        {
            return FileExists(path) ? _version : null;
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

    private sealed class FakeRegistryReader : IRegistryReader
    {
        public Task<IReadOnlyList<RegistryAppEntry>> ReadInstalledAppsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RegistryAppEntry>>([]);
        }
    }
}

