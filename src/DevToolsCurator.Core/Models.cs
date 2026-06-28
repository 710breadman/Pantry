using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace DevToolsCurator.Core;

public enum ToolStatus
{
    Installed_Current,
    Installed_Outdated,
    Installed_NotOnPath,
    Missing_Recommended,
    Missing_Optional,
    Broken,
    Unknown,
    AuthNeeded,
    RebootNeeded
}

public enum InstallStyle
{
    Minimal,
    Recommended,
    FullPowerUser
}

public enum UpdateBehavior
{
    ManualReview,
    OneClickRecommended,
    UnattendedAllowed
}

public sealed class ToolCatalog
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "2.0";

    [JsonPropertyName("source_notes")]
    public List<string> SourceNotes { get; set; } = [];

    [JsonPropertyName("best_dev_stack")]
    public List<string> BestDevStack { get; set; } = [];

    [JsonPropertyName("tools")]
    public List<ToolDefinition> Tools { get; set; } = [];
}

public sealed class ToolDefinition
{
    [JsonPropertyName("tool_id")]
    public string ToolId { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("why_it_matters")]
    public string WhyItMatters { get; set; } = "";

    [JsonPropertyName("used_for")]
    public List<string> UsedFor { get; set; } = [];

    [JsonPropertyName("install_method")]
    public string InstallMethod { get; set; } = "";

    [JsonPropertyName("install_tier")]
    public string InstallTier { get; set; } = "Recommended";

    [JsonPropertyName("is_heavy")]
    public bool IsHeavy { get; set; }

    [JsonPropertyName("importance_score")]
    public int ImportanceScore { get; set; }

    [JsonPropertyName("goal_tags")]
    public List<string> GoalTags { get; set; } = [];

    [JsonPropertyName("winget_ids")]
    public List<string> WingetIds { get; set; } = [];

    [JsonPropertyName("fallback_urls")]
    public List<string> FallbackUrls { get; set; } = [];

    [JsonPropertyName("detection")]
    public DetectionDefinition Detection { get; set; } = new();
}

public sealed class DetectionDefinition
{
    [JsonPropertyName("executables")]
    public List<string> Executables { get; set; } = [];

    [JsonPropertyName("common_paths")]
    public List<string> CommonPaths { get; set; } = [];

    [JsonPropertyName("registry_patterns")]
    public List<string> RegistryPatterns { get; set; } = [];

    [JsonPropertyName("version_commands")]
    public List<VersionCommandDefinition> VersionCommands { get; set; } = [];

    [JsonPropertyName("env_vars")]
    public List<string> EnvVars { get; set; } = [];

    [JsonPropertyName("winget_names")]
    public List<string> WingetNames { get; set; } = [];
}

public sealed class VersionCommandDefinition
{
    [JsonPropertyName("executable")]
    public string Executable { get; set; } = "";

    [JsonPropertyName("arguments")]
    public List<string> Arguments { get; set; } = [];
}

public sealed class DetectionHit
{
    public string Source { get; init; } = "";
    public string Value { get; init; } = "";
    public string Detail { get; init; } = "";
}

public sealed class ToolScanResult
{
    public string ToolId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Category { get; init; } = "";
    public ToolStatus Status { get; set; } = ToolStatus.Unknown;
    public string StatusText => Status switch
    {
        ToolStatus.Installed_Current => "Installed",
        ToolStatus.Installed_Outdated => "Installed, update available",
        ToolStatus.Installed_NotOnPath => "Installed, not on PATH",
        ToolStatus.Missing_Recommended => "Missing",
        ToolStatus.Missing_Optional => "Optional, not installed",
        ToolStatus.Broken => "Broken",
        ToolStatus.Unknown => "Unknown",
        ToolStatus.AuthNeeded => "Action needed",
        ToolStatus.RebootNeeded => "Reboot needed",
        _ => Status.ToString().Replace('_', ' ')
    };
    public string Version { get; set; } = "";
    public string LatestVersion { get; set; } = "";
    public string DetectedPath { get; set; } = "";
    public bool IsOnPath { get; set; }
    public bool IsOptional { get; set; }
    public bool IsHeavy { get; init; }
    public bool IsUpdateAvailable { get; set; }
    public bool IsRecommendedForGoal { get; set; }
    public bool IsRequiredForGoal { get; set; }
    public string SuggestedAction { get; set; } = "";
    public string DetectionSummary { get; set; } = "";
    public string DetectionSource { get; set; } = "";
    public string Diagnostic { get; set; } = "";
    public List<DetectionHit> DetectionHits { get; init; } = [];
    public ToolDefinition? Tool { get; init; }

    public bool CanInstall => Tool is not null && Status is ToolStatus.Missing_Recommended or ToolStatus.Missing_Optional;
    public bool CanUpdate => Tool is not null && Status == ToolStatus.Installed_Outdated;
    public bool CanRepair => Tool is not null && Status == ToolStatus.Broken;
    public bool CanFixPath => Tool is not null && Status == ToolStatus.Installed_NotOnPath && !string.IsNullOrWhiteSpace(DetectedPath);
    public bool CanOpen => !string.IsNullOrWhiteSpace(DetectedPath) && (File.Exists(DetectedPath) || Directory.Exists(DetectedPath));
    public bool CanShowInfo => true;
}

public sealed class GoalProfile
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public List<string> RequiredTools { get; init; } = [];
    public List<string> RecommendedTools { get; init; } = [];
    public List<string> OptionalTools { get; init; } = [];
}

public sealed class WizardSelection
{
    public string GoalProfileId { get; set; } = "ai_codex_ready";
    public ObservableCollection<string> Languages { get; } = [];
    public InstallStyle InstallStyle { get; set; } = InstallStyle.Recommended;
    public bool AllowVisualStudioBuildTools { get; set; }
    public bool AllowAndroidStudio { get; set; }
    public bool AllowDockerDesktop { get; set; }
    public bool AllowWsl2 { get; set; }
    public bool AllowVisualStudioCommunity { get; set; }
    public UpdateBehavior UpdateBehavior { get; set; } = UpdateBehavior.ManualReview;
}

public sealed class InstallPlan
{
    public string GoalProfileId { get; set; } = "";
    public string GoalProfileName { get; set; } = "";
    public List<string> RequiredTools { get; set; } = [];
    public List<string> RecommendedTools { get; set; } = [];
    public List<string> OptionalTools { get; set; } = [];
    public List<string> UnnecessaryTools { get; set; } = [];
    public List<string> Actions { get; set; } = [];
    public string EstimatedDiskImpact { get; set; } = "Moderate";
    public bool AdminRequired { get; set; }
    public bool RebootLikely { get; set; }
}

public sealed class DashboardSummary
{
    public int ReadinessScore { get; init; }
    public int TotalTools { get; init; }
    public int InstalledCurrent { get; init; }
    public int InstalledNotOnPath { get; init; }
    public int Outdated { get; init; }
    public int MissingCritical { get; init; }
    public int MissingOptional { get; init; }
    public int BrokenOrPathIssues { get; init; }
    public int AuthNeeded { get; init; }
    public DateTimeOffset LastScanTime { get; init; }
    public string RecommendedNextAction { get; init; } = "";
}

public sealed class ScanSnapshot
{
    public DateTimeOffset LastScanTime { get; set; } = DateTimeOffset.Now;
    public string SelectedGoalProfileId { get; set; } = "ai_codex_ready";
    public List<ToolScanResult> Tools { get; set; } = [];
    public DashboardSummary Summary { get; set; } = new();
    public List<string> Issues { get; set; } = [];
    public List<string> RepairSuggestions { get; set; } = [];
}
