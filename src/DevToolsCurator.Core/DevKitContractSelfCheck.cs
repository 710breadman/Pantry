namespace DevToolsCurator.Core;

public sealed class DevKitContractSelfCheckReport
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public bool PopupFormattingOk { get; init; }
    public bool AppStateRefreshOk { get; init; }
    public bool RuntimePathsOk { get; init; }
    public bool CatalogLoaded { get; init; }
    public bool StartupWritable { get; init; }
    public string PopupTitle { get; init; } = "";
    public string PopupSubtitle { get; init; } = "";
    public List<string> Errors { get; init; } = [];
    public bool Passed => PopupFormattingOk && AppStateRefreshOk && RuntimePathsOk && CatalogLoaded && StartupWritable && Errors.Count == 0;
}

public static class DevKitContractSelfCheck
{
    private const string ForbiddenPopupText = "help windows terminal 1.24.11321.0";

    public static DevKitContractSelfCheckReport Run(DevKitRuntimePaths paths, ToolCatalog catalog)
    {
        var errors = new List<string>();
        var popup = BuildWindowsTerminalPopupModel();
        var popupFormattingOk =
            popup.DisplayName == "Windows Terminal" &&
            popup.Version == "1.24.11321.0" &&
            popup.Subtitle == "Installed • Version 1.24.11321.0" &&
            !popup.AllVisibleText.Contains(ForbiddenPopupText, StringComparison.OrdinalIgnoreCase) &&
            !popup.DisplayName.Contains("help", StringComparison.OrdinalIgnoreCase);

        if (!popupFormattingOk)
        {
            errors.Add($"Popup formatting failed. Title='{popup.DisplayName}', Subtitle='{popup.Subtitle}'.");
        }

        var stateRefreshOk = VerifyStateRefresh();
        if (!stateRefreshOk)
        {
            errors.Add("State refresh failed: Missing -> Installed did not update dashboard counts and issues.");
        }

        var startup = StartupSelfCheck.Run(paths, catalog);
        var runtimePathsOk = !string.IsNullOrWhiteSpace(paths.ConfigPath) &&
                             !string.IsNullOrWhiteSpace(paths.ReportDirectory) &&
                             !string.IsNullOrWhiteSpace(paths.CacheDirectory);
        if (!runtimePathsOk)
        {
            errors.Add("Runtime paths are incomplete.");
        }

        foreach (var error in startup.Errors)
        {
            errors.Add(error);
        }

        return new DevKitContractSelfCheckReport
        {
            PopupFormattingOk = popupFormattingOk,
            AppStateRefreshOk = stateRefreshOk,
            RuntimePathsOk = runtimePathsOk,
            CatalogLoaded = catalog.Tools.Count > 0,
            StartupWritable = !startup.IsCriticalFailure,
            PopupTitle = popup.DisplayName,
            PopupSubtitle = popup.Subtitle,
            Errors = errors
        };
    }

    private static ToolInfoDialogViewModel BuildWindowsTerminalPopupModel()
    {
        var tool = new ToolDefinition
        {
            ToolId = "windows-terminal",
            DisplayName = "Windows Terminal",
            Category = "System Core",
            Description = "Modern terminal host for shells and command-line tools.",
            WhyItMatters = "Keeps PowerShell, WSL, and command prompts usable in one reliable window.",
            InstallMethod = "winget",
            WingetIds = ["Microsoft.WindowsTerminal"],
            GoalTags = ["windows_cli_scripting", "linux_crossplatform"],
            Detection = new DetectionDefinition
            {
                VersionCommands =
                [
                    new VersionCommandDefinition { Executable = "wt.exe", Arguments = ["--version"] }
                ]
            }
        };

        return ToolInfoDialogViewModel.FromTool(new ToolScanResult
        {
            ToolId = "windows-terminal",
            DisplayName = "Windows Terminal",
            Category = "System Core",
            Status = ToolStatus.Installed_Current,
            Version = "1.24.11321.0",
            DetectedPath = @"C:\Program Files\WindowsApps\Microsoft.WindowsTerminal\wt.exe",
            DetectionSource = "PATH",
            DetectionSummary = "Detected by executable lookup.",
            Tool = tool
        });
    }

    private static bool VerifyStateRefresh()
    {
        var state = new AppStateService();
        var scan = new ScanService();
        var missing = new ToolScanResult
        {
            ToolId = "windows-terminal",
            DisplayName = "Windows Terminal",
            Status = ToolStatus.Missing_Recommended,
            IsRecommendedForGoal = true,
            Diagnostic = "Missing"
        };
        state.ApplySnapshot(new ScanSnapshot
        {
            Tools = [missing],
            Summary = scan.BuildSummary([missing]),
            Issues = ["Windows Terminal: Missing"]
        }, new InstallPlan { RecommendedTools = ["windows-terminal"] });

        var installed = new ToolScanResult
        {
            ToolId = "windows-terminal",
            DisplayName = "Windows Terminal",
            Status = ToolStatus.Installed_Current,
            IsRecommendedForGoal = true,
            Version = "1.24.11321.0"
        };
        state.ApplySnapshot(new ScanSnapshot
        {
            Tools = [installed],
            Summary = scan.BuildSummary([installed]),
            Issues = []
        }, new InstallPlan { RecommendedTools = ["windows-terminal"] });

        return state.LastScan.Summary.MissingCritical == 0 &&
               state.LastScan.Summary.InstalledCurrent == 1 &&
               state.Issues.Count == 0 &&
               state.DetectedTools[0].Status == ToolStatus.Installed_Current;
    }
}
