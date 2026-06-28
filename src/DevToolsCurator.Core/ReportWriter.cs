using System.Text;
using System.Text.Json;

namespace DevToolsCurator.Core;

public sealed class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string ReportDirectory { get; }

    public ReportWriter(string projectRoot)
    {
        ReportDirectory = Path.Combine(projectRoot, "devtools_setup_report");
    }

    private ReportWriter(string reportDirectory, bool useExactDirectory)
    {
        ReportDirectory = useExactDirectory ? reportDirectory : Path.Combine(reportDirectory, "devtools_setup_report");
    }

    public static ReportWriter ForReportDirectory(string reportDirectory)
    {
        return new ReportWriter(reportDirectory, useExactDirectory: true);
    }

    public async Task WriteAsync(ScanSnapshot snapshot, InstallPlan plan, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ReportDirectory);
        await File.WriteAllTextAsync(Path.Combine(ReportDirectory, "summary.md"), BuildSummary(snapshot, plan), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(ReportDirectory, "tools.csv"), BuildToolsCsv(snapshot), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(ReportDirectory, "issues.json"), JsonSerializer.Serialize(snapshot.Issues, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(ReportDirectory, "install_plan.json"), JsonSerializer.Serialize(plan, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(ReportDirectory, "last_scan.json"), JsonSerializer.Serialize(snapshot, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(ReportDirectory, "repair_suggestions.md"), BuildRepairSuggestions(snapshot), cancellationToken);
    }

    private static string BuildSummary(ScanSnapshot snapshot, InstallPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Windows DevTools Curator Summary");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"Goal profile: {plan.GoalProfileName}");
        sb.AppendLine($"Readiness: {snapshot.Summary.ReadinessScore}%");
        sb.AppendLine($"Verdict: {(snapshot.Summary.MissingCritical == 0 && snapshot.Summary.BrokenOrPathIssues == 0 ? "ready" : "not ready")}");
        sb.AppendLine();
        sb.AppendLine("## Counts");
        sb.AppendLine();
        sb.AppendLine($"- Total tools: {snapshot.Summary.TotalTools}");
        sb.AppendLine($"- Installed/current: {snapshot.Summary.InstalledCurrent}");
        sb.AppendLine($"- Installed but not on PATH: {snapshot.Summary.InstalledNotOnPath}");
        sb.AppendLine($"- Outdated: {snapshot.Summary.Outdated}");
        sb.AppendLine($"- Missing critical/recommended: {snapshot.Summary.MissingCritical}");
        sb.AppendLine($"- Missing optional: {snapshot.Summary.MissingOptional}");
        sb.AppendLine($"- Broken/path/reboot issues: {snapshot.Summary.BrokenOrPathIssues}");
        sb.AppendLine($"- Auth/action needed: {snapshot.Summary.AuthNeeded}");
        sb.AppendLine();
        sb.AppendLine("## Recommended Next Action");
        sb.AppendLine();
        sb.AppendLine(snapshot.Summary.RecommendedNextAction);
        sb.AppendLine();
        sb.AppendLine("## Failed Or Needs Action");
        sb.AppendLine();
        foreach (var tool in snapshot.Tools.Where(x => x.Status is ToolStatus.Missing_Recommended or ToolStatus.Broken or ToolStatus.Installed_NotOnPath or ToolStatus.AuthNeeded or ToolStatus.RebootNeeded))
        {
            sb.AppendLine($"- {tool.DisplayName}: {tool.StatusText}. {tool.Diagnostic}");
        }

        if (snapshot.Tools.Any(x => x.ToolId == "github-cli" && x.Status == ToolStatus.AuthNeeded))
        {
            sb.AppendLine();
            sb.AppendLine("GitHub login needed: run `gh auth login`.");
        }

        return sb.ToString();
    }

    private static string BuildToolsCsv(ScanSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("tool_id,display_name,category,status,version,latest,path,source,suggested_action");
        foreach (var tool in snapshot.Tools)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                Csv(tool.ToolId),
                Csv(tool.DisplayName),
                Csv(tool.Category),
                Csv(tool.StatusText),
                Csv(tool.Version),
                Csv(tool.LatestVersion),
                Csv(tool.DetectedPath),
                Csv(tool.DetectionSource),
                Csv(tool.SuggestedAction)
            }));
        }

        return sb.ToString();
    }

    private static string BuildRepairSuggestions(ScanSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Repair Suggestions");
        sb.AppendLine();
        if (snapshot.RepairSuggestions.Count == 0)
        {
            sb.AppendLine("No repair suggestions at this time.");
            return sb.ToString();
        }

        foreach (var suggestion in snapshot.RepairSuggestions)
        {
            sb.AppendLine($"- {suggestion}");
        }

        return sb.ToString();
    }

    private static string Csv(string value)
    {
        value ??= "";
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
