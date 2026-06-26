using Pantry.Domain;

namespace Pantry.Detection;

public sealed class FileDetectionProvider
{
    private const string FilePathRulePrefix = "filePath:";
    private readonly IFileSystemReader _fileSystemReader;

    public FileDetectionProvider(IFileSystemReader fileSystemReader)
    {
        _fileSystemReader = fileSystemReader;
    }

    public Task<AppDetectionResult> DetectAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        cancellationToken.ThrowIfCancellationRequested();

        var paths = recipe.Detection.Rules
            .Where(rule => rule.StartsWith(FilePathRulePrefix, StringComparison.OrdinalIgnoreCase))
            .Select(rule => rule[FilePathRulePrefix.Length..].Trim())
            .Where(path => path.Length > 0)
            .Select(Environment.ExpandEnvironmentVariables)
            .ToArray();

        if (paths.Length == 0)
        {
            return Task.FromResult(Unknown(recipe.Id, "Recipe does not include file path detection rules."));
        }

        foreach (var path in paths)
        {
            if (!_fileSystemReader.FileExists(path))
            {
                continue;
            }

            return Task.FromResult(new AppDetectionResult
            {
                AppId = recipe.Id,
                State = DetectedAppState.InstalledCurrent,
                Confidence = DetectionConfidence.Medium,
                InstalledVersion = _fileSystemReader.TryGetFileVersion(path),
                Evidence =
                [
                    new DetectionEvidence
                    {
                        Source = "File path",
                        Detail = $"File found: {path}"
                    }
                ],
                Summary = $"File detection found {Path.GetFileName(path)}."
            });
        }

        return Task.FromResult(new AppDetectionResult
        {
            AppId = recipe.Id,
            State = DetectedAppState.NotInstalled,
            Confidence = DetectionConfidence.Low,
            Evidence =
            [
                new DetectionEvidence
                {
                    Source = "File path",
                    Detail = $"No configured file path was found: {string.Join(", ", paths)}"
                }
            ],
            Summary = "File detection did not find a configured path."
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

