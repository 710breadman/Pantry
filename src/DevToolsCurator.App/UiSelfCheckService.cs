using DevToolsCurator.Core;
using System.IO;

namespace DevToolsCurator.App;

public sealed class UiSelfCheckReport
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public List<string> BrokenBindings { get; init; } = [];
    public List<string> ButtonsWithoutCommand { get; init; } = [];
    public List<string> CommandsWithoutHandler { get; init; } = [];
    public List<string> HiddenExceptions { get; init; } = [];
    public List<string> MissingTooltips { get; init; } = [];
    public List<string> UnreadableThemeControls { get; init; } = [];
    public List<string> FailedDetectionCases { get; init; } = [];
    public int CriticalIssueCount => BrokenBindings.Count + ButtonsWithoutCommand.Count + CommandsWithoutHandler.Count + UnreadableThemeControls.Count + FailedDetectionCases.Count;
}

public static class UiSelfCheckService
{
    private static readonly string[] RequiredCommands =
    [
        "NavigateCommand",
        "StartWizardCommand",
        "RescanCommand",
        "InstallRecommendedCommand",
        "InstallBestStackCommand",
        "UpdateInstalledCommand",
        "RepairIssuesCommand",
        "ExportSummaryCommand",
        "OpenReportFolderCommand",
        "InstallToolCommand",
        "UpdateToolCommand",
        "RepairToolCommand",
        "FixPathCommand",
        "OpenToolCommand",
        "ShowInfoCommand",
        "CopyDiagnosticsCommand",
        "RunUiSelfCheckCommand"
    ];

    private static readonly string[] RequiredThemeTargets =
    [
        "ComboBox",
        "ComboBoxItem",
        "ListBox",
        "ListBoxItem",
        "ContextMenu",
        "MenuItem",
        "ToolTip",
        "TextBox"
    ];

    public static UiSelfCheckReport Run(IReadOnlyList<ToolScanResult> tools)
    {
        var report = new UiSelfCheckReport();
        if (!TryReadSourceFiles(out var mainXaml, out var appXaml, out var vm))
        {
            report.HiddenExceptions.Add("Source XAML was unavailable; ran runtime-safe self check only.");
            AddDetectionChecks(tools, report);
            return report;
        }

        foreach (var command in RequiredCommands)
        {
            if (!mainXaml.Contains(command, StringComparison.Ordinal) &&
                !vm.Contains(command, StringComparison.Ordinal))
            {
                report.CommandsWithoutHandler.Add(command);
            }
        }

        foreach (var target in RequiredThemeTargets)
        {
            if (!appXaml.Contains($"TargetType=\"{{x:Type {target}}}\"", StringComparison.Ordinal))
            {
                report.UnreadableThemeControls.Add($"{target} has no explicit dark theme style.");
            }
        }

        if (mainXaml.Contains("ToolActionCommand", StringComparison.Ordinal) || mainXaml.Contains("ApplyWizardCommand", StringComparison.Ordinal))
        {
            report.BrokenBindings.Add("Removed command still referenced in MainWindow.xaml.");
        }

        if (mainXaml.Contains("help windows terminal 1.24.11321.0", StringComparison.OrdinalIgnoreCase) ||
            vm.Contains("help windows terminal 1.24.11321.0", StringComparison.OrdinalIgnoreCase))
        {
            report.BrokenBindings.Add("Raw help/debug popup text is present in UI source.");
        }

        if (mainXaml.Contains("Content=\"Repair\"", StringComparison.Ordinal) && !mainXaml.Contains("RepairToolCommand", StringComparison.Ordinal))
        {
            report.ButtonsWithoutCommand.Add("Repair button text found without RepairToolCommand.");
        }

        AddDetectionChecks(tools, report);
        return report;
    }

    private static void AddDetectionChecks(IReadOnlyList<ToolScanResult> tools, UiSelfCheckReport report)
    {
        var sevenZip = tools.FirstOrDefault(x => x.ToolId == "7zip");
        if (sevenZip is not null && sevenZip.Status == ToolStatus.Missing_Recommended && !string.IsNullOrWhiteSpace(sevenZip.DetectedPath))
        {
            report.FailedDetectionCases.Add("7-Zip has a detected path but is marked missing.");
        }
    }

    private static bool TryReadSourceFiles(out string mainXaml, out string appXaml, out string vm)
    {
        mainXaml = "";
        appXaml = "";
        vm = "";

        if (!CatalogService.TryFindProjectRoot(AppContext.BaseDirectory, out var root))
        {
            return false;
        }

        var mainPath = Path.Combine(root, "src", "DevToolsCurator.App", "MainWindow.xaml");
        var appPath = Path.Combine(root, "src", "DevToolsCurator.App", "App.xaml");
        var vmPath = Path.Combine(root, "src", "DevToolsCurator.App", "MainViewModel.cs");
        if (!File.Exists(mainPath) || !File.Exists(appPath) || !File.Exists(vmPath))
        {
            return false;
        }

        mainXaml = File.ReadAllText(mainPath);
        appXaml = File.ReadAllText(appPath);
        vm = File.ReadAllText(vmPath);
        return true;
    }
}
