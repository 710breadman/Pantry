using Pantry.Domain;

namespace Pantry.Detection;

public sealed class RegistryDetectionProvider
{
    private const string RulePrefix = "uninstallRegistryDisplayName:";
    private readonly IRegistryReader _registryReader;

    public RegistryDetectionProvider(IRegistryReader registryReader)
    {
        _registryReader = registryReader;
    }

    public async Task<AppDetectionResult> DetectAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        var names = recipe.Detection.Rules
            .Where(rule => rule.StartsWith(RulePrefix, StringComparison.OrdinalIgnoreCase))
            .Select(rule => rule[RulePrefix.Length..].Trim())
            .Where(name => name.Length > 0)
            .ToArray();

        if (names.Length == 0)
        {
            return Unknown(recipe.Id, "Recipe does not include uninstall registry detection rules.");
        }

        var apps = await _registryReader.ReadInstalledAppsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var expectedName in names)
        {
            var match = apps.FirstOrDefault(app =>
                app.DisplayName.Contains(expectedName, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return new AppDetectionResult
                {
                    AppId = recipe.Id,
                    State = DetectedAppState.InstalledCurrent,
                    Confidence = DetectionConfidence.Medium,
                    InstalledVersion = match.DisplayVersion,
                    Evidence =
                    [
                        new DetectionEvidence
                        {
                            Source = "Uninstall registry",
                            Detail = $"{match.DisplayName} at {match.RegistryPath}"
                        }
                    ],
                    Summary = $"Registry found {match.DisplayName}."
                };
            }
        }

        return new AppDetectionResult
        {
            AppId = recipe.Id,
            State = DetectedAppState.NotInstalled,
            Confidence = DetectionConfidence.Medium,
            Evidence =
            [
                new DetectionEvidence
                {
                    Source = "Uninstall registry",
                    Detail = $"No uninstall entry matched: {string.Join(", ", names)}"
                }
            ],
            Summary = "Registry did not find a matching uninstall entry."
        };
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

