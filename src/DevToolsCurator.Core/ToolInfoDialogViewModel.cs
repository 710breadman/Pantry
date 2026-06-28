namespace DevToolsCurator.Core;

public sealed class ToolInfoDialogViewModel
{
    public string DisplayName { get; init; } = "";
    public string Version { get; init; } = "";
    public string Status { get; init; } = "";
    public string Subtitle => $"{Status} • Version {Version}";
    public string Description { get; init; } = "";
    public string WhyUseful { get; init; } = "";
    public string DetectedPath { get; init; } = "";
    public string DetectionSource { get; init; } = "";
    public string InstallSource { get; init; } = "";
    public string AvailableActions { get; init; } = "";
    public string TroubleshootingNotes { get; init; } = "";

    public string Detected =>
        $"Installed path: {BlankToUnknown(DetectedPath)}{Environment.NewLine}" +
        $"Detection source: {BlankToUnknown(DetectionSource)}{Environment.NewLine}" +
        $"Install source: {BlankToUnknown(InstallSource)}";

    public string AllVisibleText => string.Join(Environment.NewLine, new[]
    {
        DisplayName,
        Subtitle,
        Description,
        WhyUseful,
        Detected,
        AvailableActions,
        TroubleshootingNotes
    });

    public static ToolInfoDialogViewModel FromTool(ToolScanResult result)
    {
        var tool = result.Tool;
        var displayName = FriendlyName(result);
        var version = string.IsNullOrWhiteSpace(result.Version) ? "unknown" : result.Version.Trim();
        var status = string.IsNullOrWhiteSpace(result.StatusText) ? result.Status.ToString().Replace('_', ' ') : result.StatusText;
        var goals = tool is null || tool.GoalTags.Count == 0 ? "General developer setup" : string.Join(", ", tool.GoalTags);
        var versionCommand = tool?.Detection.VersionCommands.FirstOrDefault();
        var versionCommandText = versionCommand is null
            ? "No version command configured."
            : $"{versionCommand.Executable} {string.Join(' ', versionCommand.Arguments)}".Trim();

        return new ToolInfoDialogViewModel
        {
            DisplayName = displayName,
            Version = version,
            Status = status,
            Description = string.IsNullOrWhiteSpace(tool?.Description)
                ? "No description is available for this tool."
                : tool.Description,
            WhyUseful = $"{BlankToUnknown(tool?.WhyItMatters)}{Environment.NewLine}{Environment.NewLine}Needed for: {goals}",
            DetectedPath = result.DetectedPath,
            DetectionSource = BuildDetectionSource(result, versionCommandText),
            InstallSource = BuildInstallSource(tool),
            AvailableActions = BuildActions(result),
            TroubleshootingNotes = string.IsNullOrWhiteSpace(result.Diagnostic)
                ? "No troubleshooting notes for the current status."
                : result.Diagnostic
        };
    }

    private static string FriendlyName(ToolScanResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.DisplayName))
        {
            return result.DisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(result.Tool?.DisplayName))
        {
            return result.Tool.DisplayName.Trim();
        }

        return string.IsNullOrWhiteSpace(result.ToolId) ? "Unknown Tool" : result.ToolId;
    }

    private static string BuildDetectionSource(ToolScanResult result, string versionCommand)
    {
        var source = BlankToUnknown(result.DetectionSource);
        var summary = string.IsNullOrWhiteSpace(result.DetectionSummary) ? "" : $"{Environment.NewLine}{result.DetectionSummary}";
        return $"{source}{summary}{Environment.NewLine}Version command: {versionCommand}".Trim();
    }

    private static string BuildInstallSource(ToolDefinition? tool)
    {
        if (tool is null)
        {
            return "Unknown";
        }

        if (tool.WingetIds.Count > 0)
        {
            return $"winget ({tool.WingetIds[0]})";
        }

        if (!string.IsNullOrWhiteSpace(tool.InstallMethod))
        {
            return tool.InstallMethod;
        }

        return tool.FallbackUrls.Count > 0 ? "Official fallback URL available" : "Not configured";
    }

    private static string BuildActions(ToolScanResult result)
    {
        var actions = new List<string>();
        if (result.CanInstall) actions.Add("Install with the configured package source.");
        if (result.CanUpdate) actions.Add("Update with winget or the configured updater.");
        if (result.CanRepair) actions.Add("Repair by reinstalling or revalidating the executable.");
        if (result.CanFixPath) actions.Add("Fix PATH by adding the install directory to user PATH.");
        if (result.Status == ToolStatus.AuthNeeded && result.ToolId.Equals("github-cli", StringComparison.OrdinalIgnoreCase)) actions.Add("Run: gh auth login");
        if (result.CanOpen) actions.Add("Open the detected install folder.");
        return actions.Count == 0 ? "No direct action is needed for this tool." : string.Join(Environment.NewLine, actions.Select(x => "- " + x));
    }

    private static string BlankToUnknown(string? value) => string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
}
