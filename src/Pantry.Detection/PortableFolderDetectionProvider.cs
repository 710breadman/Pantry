using Pantry.Domain;

namespace Pantry.Detection;

public sealed class PortableFolderDetectionProvider
{
    public Task<AppDetectionResult> DetectAsync(
        Recipe recipe,
        string? portableDestination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        cancellationToken.ThrowIfCancellationRequested();

        if (!recipe.Catalog.IsPortable)
        {
            return Task.FromResult(Unknown(recipe.Id, "Recipe is not a portable app."));
        }

        var destination = string.IsNullOrWhiteSpace(portableDestination)
            ? recipe.PortableDestinationHint
            : portableDestination;

        if (string.IsNullOrWhiteSpace(destination))
        {
            return Task.FromResult(Unknown(recipe.Id, "No portable destination is set."));
        }

        var fullPath = Path.GetFullPath(destination);
        if (!Directory.Exists(fullPath))
        {
            return Task.FromResult(new AppDetectionResult
            {
                AppId = recipe.Id,
                State = DetectedAppState.NotInstalled,
                Confidence = DetectionConfidence.Medium,
                Evidence =
                [
                    new DetectionEvidence
                    {
                        Source = "Portable folder",
                        Detail = $"Folder not found: {fullPath}"
                    }
                ],
                Summary = "Portable destination folder was not found."
            });
        }

        return Task.FromResult(new AppDetectionResult
        {
            AppId = recipe.Id,
            State = DetectedAppState.InstalledCurrent,
            Confidence = DetectionConfidence.Medium,
            Evidence =
            [
                new DetectionEvidence
                {
                    Source = "Portable folder",
                    Detail = $"Folder found: {fullPath}"
                }
            ],
            Summary = "Portable destination folder exists."
        });
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

