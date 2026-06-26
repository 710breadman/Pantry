using Pantry.Domain;

namespace Pantry.Detection;

public sealed class WingetDetectionProvider
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(12);
    private readonly IProcessRunner _processRunner;

    public WingetDetectionProvider(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<AppDetectionResult> DetectAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        if (recipe.Source.Type != ProviderType.Winget)
        {
            return Unknown(recipe.Id, "Recipe does not use Winget detection.");
        }

        var result = await _processRunner.RunAsync(
            "winget",
            ["list", "--id", recipe.Source.Identifier, "--exact", "--disable-interactivity"],
            DefaultTimeout,
            cancellationToken).ConfigureAwait(false);

        if (result.TimedOut)
        {
            return Unknown(recipe.Id, "Winget detection timed out.");
        }

        if (result.ExitCode != 0 && string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return Unknown(recipe.Id, $"Winget detection failed: {result.StandardError}");
        }

        return WingetListParser.Parse(recipe.Id, recipe.Source.Identifier, result.StandardOutput);
    }

    private static AppDetectionResult Unknown(string appId, string summary)
    {
        return new AppDetectionResult
        {
            AppId = appId,
            State = DetectedAppState.Unknown,
            Confidence = DetectionConfidence.Unknown,
            Evidence = [],
            Summary = summary
        };
    }
}

