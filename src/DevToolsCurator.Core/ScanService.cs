namespace DevToolsCurator.Core;

public sealed class ScanService
{
    private readonly ToolDetector _detector;
    private readonly GoalPlanner _planner;
    private readonly SystemInspector _systemInspector;
    private readonly WingetCache _winget;

    public ScanService(ToolDetector? detector = null, GoalPlanner? planner = null, SystemInspector? systemInspector = null, WingetCache? winget = null)
    {
        _planner = planner ?? new GoalPlanner();
        _winget = winget ?? new WingetCache();
        _detector = detector ?? new ToolDetector(winget: _winget);
        _systemInspector = systemInspector ?? new SystemInspector();
    }

    public async Task<ScanSnapshot> ScanAsync(ToolCatalog catalog, WizardSelection selection, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        PathTools.RefreshProcessPathFromPersistedEnvironment();
        _winget.Clear();
        var plan = _planner.BuildPlan(catalog, selection);
        var options = new DetectionOptions { Plan = plan };
        var results = new List<ToolScanResult>();
        var throttler = new SemaphoreSlim(8, 8);
        var resultLock = new object();

        progress?.Report("Scanning tools");
        var tasks = catalog.Tools.Select(async tool =>
        {
            await throttler.WaitAsync(cancellationToken);
            try
            {
                progress?.Report($"Checking {tool.DisplayName}");
                var result = await _detector.ScanAsync(tool, options, cancellationToken);
                await _detector.ApplyGithubAuthOverlayAsync(result, cancellationToken);
                lock (resultLock)
                {
                    results.Add(result);
                }
            }
            finally
            {
                throttler.Release();
            }
        });

        await Task.WhenAll(tasks);
        results = results
            .OrderBy(x => CategoryOrder(x.Category))
            .ThenByDescending(x => x.IsRequiredForGoal)
            .ThenByDescending(x => x.IsRecommendedForGoal)
            .ThenByDescending(x => x.Tool?.ImportanceScore ?? 0)
            .ThenBy(x => x.DisplayName)
            .ToList();

        var system = await _systemInspector.InspectAsync(cancellationToken);
        AddSyntheticChecks(results, system, plan);
        ApplyPlanHints(results, plan);

        var snapshot = new ScanSnapshot
        {
            LastScanTime = DateTimeOffset.Now,
            SelectedGoalProfileId = plan.GoalProfileId,
            Tools = results,
            Summary = BuildSummary(results),
            Issues = BuildIssues(results, system),
            RepairSuggestions = BuildRepairSuggestions(results, system)
        };

        progress?.Report("Scan complete");
        return snapshot;
    }

    public DashboardSummary BuildSummary(IReadOnlyList<ToolScanResult> results)
    {
        var relevant = results.Where(x => !x.IsOptional || x.IsRecommendedForGoal || x.IsRequiredForGoal).ToList();
        var installedGood = relevant.Count(x => x.Status == ToolStatus.Installed_Current);
        var denominator = Math.Max(1, relevant.Count);
        var readiness = (int)Math.Round(installedGood * 100.0 / denominator);
        var missingCritical = results.Count(x => x.Status == ToolStatus.Missing_Recommended && (x.IsRequiredForGoal || x.IsRecommendedForGoal));
        var outdated = results.Count(x => x.Status == ToolStatus.Installed_Outdated);
        var issues = results.Count(x => x.Status is ToolStatus.Broken or ToolStatus.Installed_NotOnPath or ToolStatus.RebootNeeded);
        var auth = results.Count(x => x.Status == ToolStatus.AuthNeeded);

        var next = missingCritical > 0
            ? "Install recommended tools for the selected goal."
            : issues > 0
                ? "Repair PATH or broken install issues."
                : outdated > 0
                    ? "Update installed tools."
                    : auth > 0
                        ? "Run the shown auth command for GitHub."
                        : "Ready for app creation.";

        return new DashboardSummary
        {
            ReadinessScore = readiness,
            TotalTools = results.Count,
            InstalledCurrent = results.Count(x => x.Status == ToolStatus.Installed_Current),
            InstalledNotOnPath = results.Count(x => x.Status == ToolStatus.Installed_NotOnPath),
            Outdated = outdated,
            MissingCritical = missingCritical,
            MissingOptional = results.Count(x => x.Status == ToolStatus.Missing_Optional),
            BrokenOrPathIssues = issues,
            AuthNeeded = auth,
            LastScanTime = DateTimeOffset.Now,
            RecommendedNextAction = next
        };
    }

    private static void ApplyPlanHints(IEnumerable<ToolScanResult> results, InstallPlan plan)
    {
        foreach (var result in results)
        {
            result.IsRequiredForGoal = plan.RequiredTools.Contains(result.ToolId, StringComparer.OrdinalIgnoreCase);
            result.IsRecommendedForGoal = result.IsRequiredForGoal || plan.RecommendedTools.Contains(result.ToolId, StringComparer.OrdinalIgnoreCase);
            result.IsOptional = !result.IsRecommendedForGoal;
        }
    }

    private static void AddSyntheticChecks(List<ToolScanResult> results, SystemSnapshot system, InstallPlan plan)
    {
        var git = results.FirstOrDefault(x => x.ToolId == "git");
        if (git is not null && git.Status is not ToolStatus.Missing_Recommended and not ToolStatus.Missing_Optional)
        {
            results.Add(new ToolScanResult
            {
                ToolId = "git-config",
                DisplayName = "Git Identity Config",
                Category = "GitHub Workflow",
                Status = HasGitIdentity() ? ToolStatus.Installed_Current : ToolStatus.AuthNeeded,
                SuggestedAction = HasGitIdentity() ? "None" : "Set user.name and user.email",
                Diagnostic = HasGitIdentity()
                    ? "Git user.name and user.email are already configured."
                    : "Git identity is missing. The app will not overwrite it automatically.",
                DetectionSource = "git config",
                DetectionSummary = "Read-only check of global Git identity.",
                IsRecommendedForGoal = plan.RecommendedTools.Contains("git", StringComparer.OrdinalIgnoreCase) || plan.RequiredTools.Contains("git", StringComparer.OrdinalIgnoreCase),
                IsRequiredForGoal = false
            });

            results.Add(new ToolScanResult
            {
                ToolId = "git-longpaths",
                DisplayName = "Git Long Paths",
                Category = "GitHub Workflow",
                Status = IsGitLongPathsEnabled() ? ToolStatus.Installed_Current : ToolStatus.AuthNeeded,
                SuggestedAction = IsGitLongPathsEnabled() ? "None" : "Enable core.longpaths",
                Diagnostic = IsGitLongPathsEnabled()
                    ? "Git core.longpaths is enabled."
                    : "Recommended on Windows for deep package trees. Safe command: git config --global core.longpaths true",
                DetectionSource = "git config",
                DetectionSummary = "Checks git config --global core.longpaths.",
                IsRecommendedForGoal = true,
                IsRequiredForGoal = false
            });
        }

        if (system.RebootPending)
        {
            results.Add(new ToolScanResult
            {
                ToolId = "windows-reboot",
                DisplayName = "Windows Reboot Pending",
                Category = "System Core",
                Status = ToolStatus.RebootNeeded,
                SuggestedAction = "Reboot when convenient",
                Diagnostic = "Windows reports a pending reboot, likely from installs or updates.",
                DetectionSource = "registry",
                DetectionSummary = "Pending reboot keys detected."
            });
        }
    }

    private static bool HasGitIdentity()
    {
        var name = RunGitConfig("user.name");
        var email = RunGitConfig("user.email");
        return !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(email);
    }

    private static bool IsGitLongPathsEnabled()
    {
        var value = RunGitConfig("core.longpaths");
        return value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static string RunGitConfig(string key)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("config");
            psi.ArgumentList.Add("--global");
            psi.ArgumentList.Add("--get");
            psi.ArgumentList.Add(key);
            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null)
            {
                return "";
            }
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1500);
            return output.Trim();
        }
        catch
        {
            return "";
        }
    }

    private static List<string> BuildIssues(IReadOnlyList<ToolScanResult> results, SystemSnapshot system)
    {
        var issues = new List<string>();
        issues.AddRange(results
            .Where(x => x.Status is ToolStatus.Broken or ToolStatus.Installed_NotOnPath or ToolStatus.AuthNeeded or ToolStatus.RebootNeeded)
            .Select(x => $"{x.DisplayName}: {x.Diagnostic}"));

        issues.AddRange(system.BrokenPathEntries.Take(10).Select(x => $"Broken {x.Scope} PATH entry: {x.Entry}"));
        issues.AddRange(system.DuplicatePathEntries.Take(10).Select(x => $"Duplicate {x.Scope} PATH entry: {x.Entry}"));

        if (!system.DeveloperModeEnabled)
        {
            issues.Add("Developer Mode is not enabled. This is optional, but useful for symlinks and app workflows.");
        }

        return issues;
    }

    private static List<string> BuildRepairSuggestions(IReadOnlyList<ToolScanResult> results, SystemSnapshot system)
    {
        var suggestions = new List<string>();
        suggestions.AddRange(results.Where(x => x.Status == ToolStatus.Installed_NotOnPath)
            .Select(x => $"Add the install folder for {x.DisplayName} to PATH if you want command-line access: {Path.GetDirectoryName(x.DetectedPath)}"));
        suggestions.AddRange(results.Where(x => x.Status == ToolStatus.AuthNeeded)
            .Select(x => x.ToolId == "github-cli" ? "Run gh auth login from a terminal. No tokens are stored by this app." : x.Diagnostic));
        suggestions.AddRange(system.BrokenPathEntries.Take(10).Select(x => $"Remove or repair broken {x.Scope} PATH entry: {x.Entry}"));
        suggestions.AddRange(system.DuplicatePathEntries.Take(10).Select(x => $"Remove duplicate {x.Scope} PATH entry: {x.Entry}"));
        return suggestions.Distinct().ToList();
    }

    private static int CategoryOrder(string category)
    {
        return category switch
        {
            "System Core" => 0,
            "Python" => 1,
            ".NET / C#" => 2,
            "Java" => 3,
            "Node / TypeScript" => 4,
            "GitHub Workflow" => 5,
            "Android" => 6,
            "Linux / Cross-platform" => 7,
            "Logic & Quality Tools" => 8,
            _ => 99
        };
    }
}
