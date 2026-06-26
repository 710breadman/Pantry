using Pantry.Catalog;
using Pantry.Detection;
using Pantry.Domain;

namespace Pantry.Tests;

public sealed class WingetDetectionProviderTests
{
    [Fact]
    public async Task Winget_provider_uses_list_command_only()
    {
        var runner = new CapturingProcessRunner(new ProcessRunResult
        {
            ExitCode = 0,
            StandardOutput = """
Name   Id          Version  Source
----------------------------------
7-Zip  7zip.7zip  24.09    winget
""",
            StandardError = string.Empty
        });

        var recipe = await LoadRecipeAsync("7zip");
        var provider = new WingetDetectionProvider(runner);

        var result = await provider.DetectAsync(recipe);

        Assert.Equal(DetectedAppState.InstalledCurrent, result.State);
        Assert.Equal("winget", runner.FileName);
        Assert.Equal(["list", "--id", "7zip.7zip", "--exact", "--disable-interactivity"], runner.Arguments);
        Assert.DoesNotContain("install", runner.Arguments, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("upgrade", runner.Arguments, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("uninstall", runner.Arguments, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Winget_timeout_returns_unknown()
    {
        var runner = new CapturingProcessRunner(new ProcessRunResult
        {
            ExitCode = -1,
            StandardOutput = string.Empty,
            StandardError = "Process timed out.",
            TimedOut = true
        });

        var recipe = await LoadRecipeAsync("7zip");
        var provider = new WingetDetectionProvider(runner);

        var result = await provider.DetectAsync(recipe);

        Assert.Equal(DetectedAppState.Unknown, result.State);
        Assert.Equal(DetectionConfidence.Unknown, result.Confidence);
    }

    private static async Task<Recipe> LoadRecipeAsync(string appId)
    {
        var loader = new BundledCatalogLoader(new RecipeValidator());
        var catalog = await loader.LoadAsync(CatalogTestPaths.BundledCatalogRoot());
        return catalog.GetRecipe(appId);
    }

    private sealed class CapturingProcessRunner : IProcessRunner
    {
        private readonly ProcessRunResult _result;

        public CapturingProcessRunner(ProcessRunResult result)
        {
            _result = result;
        }

        public string? FileName { get; private set; }

        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public Task<ProcessRunResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            FileName = fileName;
            Arguments = arguments;
            return Task.FromResult(_result);
        }
    }
}

