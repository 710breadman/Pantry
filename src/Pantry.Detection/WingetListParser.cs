using System.Text.RegularExpressions;
using Pantry.Domain;

namespace Pantry.Detection;

public static partial class WingetListParser
{
    public static AppDetectionResult Parse(string appId, string packageId, string output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("Name ", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            var columns = ColumnSeparator().Split(trimmed);
            if (columns.Length < 4 || !string.Equals(columns[1], packageId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var installedVersion = columns[2];
            var hasAvailableVersion = columns.Length >= 5;

            return new AppDetectionResult
            {
                AppId = appId,
                State = hasAvailableVersion ? DetectedAppState.UpdateAvailable : DetectedAppState.InstalledCurrent,
                Confidence = DetectionConfidence.High,
                InstalledVersion = installedVersion,
                AvailableVersion = hasAvailableVersion ? columns[3] : null,
                Evidence =
                [
                    new DetectionEvidence
                    {
                        Source = "Winget list",
                        Detail = trimmed
                    }
                ],
                Summary = hasAvailableVersion
                    ? $"Winget found {packageId}; update appears available."
                    : $"Winget found {packageId}; no update column was present."
            };
        }

        return new AppDetectionResult
        {
            AppId = appId,
            State = DetectedAppState.NotInstalled,
            Confidence = DetectionConfidence.Medium,
            Evidence =
            [
                new DetectionEvidence
                {
                    Source = "Winget list",
                    Detail = $"Package ID not found: {packageId}"
                }
            ],
            Summary = $"Winget did not list {packageId}."
        };
    }

    [GeneratedRegex("\\s{2,}")]
    private static partial Regex ColumnSeparator();
}

