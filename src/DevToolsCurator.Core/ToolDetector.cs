namespace DevToolsCurator.Core;

public sealed class DetectionOptions
{
    public InstallPlan Plan { get; init; } = new();
    public bool TreatRecommendedAsMissing { get; init; } = true;
}

public sealed class ToolDetector
{
    private readonly RegistryScanner _registry;
    private readonly WingetCache _winget;
    private readonly ProcessRunner _runner;

    public ToolDetector(RegistryScanner? registry = null, WingetCache? winget = null, ProcessRunner? runner = null)
    {
        _registry = registry ?? new RegistryScanner();
        _runner = runner ?? new ProcessRunner();
        _winget = winget ?? new WingetCache(_runner);
    }

    public async Task<ToolScanResult> ScanAsync(ToolDefinition tool, DetectionOptions options, CancellationToken cancellationToken = default)
    {
        var hits = new List<DetectionHit>();
        var pathHit = FindPathHit(tool);
        if (pathHit is not null)
        {
            hits.Add(pathHit);
        }

        foreach (var hit in FindCommonPathHits(tool))
        {
            AddUnique(hits, hit);
        }

        var registryHit = FindRegistryHit(tool);
        if (registryHit is not null)
        {
            AddUnique(hits, registryHit);
        }

        foreach (var hit in FindEnvironmentHits(tool))
        {
            AddUnique(hits, hit);
        }

        WingetPackageRow? wingetInstalled = null;
        if (tool.Detection.WingetNames.Count > 0 || tool.WingetIds.Count > 0)
        {
            wingetInstalled = await _winget.FindInstalledAsync(tool, cancellationToken);
            if (wingetInstalled is not null)
            {
                AddUnique(hits, new DetectionHit
                {
                    Source = "winget list",
                    Value = wingetInstalled.Id,
                    Detail = $"{wingetInstalled.Name} {wingetInstalled.Version}".Trim()
                });
            }
        }

        var required = options.Plan.RequiredTools.Contains(tool.ToolId, StringComparer.OrdinalIgnoreCase);
        var recommended = required || options.Plan.RecommendedTools.Contains(tool.ToolId, StringComparer.OrdinalIgnoreCase) ||
                          (options.Plan.RequiredTools.Count == 0 && tool.InstallTier.Equals("Core", StringComparison.OrdinalIgnoreCase));
        var optionalForGoal = options.Plan.OptionalTools.Contains(tool.ToolId, StringComparer.OrdinalIgnoreCase) || tool.InstallTier.Equals("Optional", StringComparison.OrdinalIgnoreCase);
        var detectedPath = ChooseDetectedPath(hits);
        var isOnPath = !string.IsNullOrWhiteSpace(pathHit?.Value) || (!string.IsNullOrWhiteSpace(detectedPath) && PathTools.IsOnPath(detectedPath));

        var result = new ToolScanResult
        {
            ToolId = tool.ToolId,
            DisplayName = tool.DisplayName,
            Category = tool.Category,
            Tool = tool,
            DetectionHits = hits,
            DetectedPath = detectedPath,
            IsOnPath = isOnPath,
            IsOptional = optionalForGoal && !required && !recommended,
            IsHeavy = tool.IsHeavy,
            IsRequiredForGoal = required,
            IsRecommendedForGoal = recommended,
            DetectionSource = hits.FirstOrDefault()?.Source ?? "not detected",
            DetectionSummary = BuildDetectionSummary(hits),
        };

        if (hits.Count == 0)
        {
            result.Status = recommended ? ToolStatus.Missing_Recommended : ToolStatus.Missing_Optional;
            result.SuggestedAction = recommended ? "Install" : "Optional";
            result.Diagnostic = recommended
                ? "Not detected through PATH, common paths, registry, environment variables, or winget list."
                : "Optional for the selected goal and not detected.";
            return result;
        }

        var versionResult = await TryReadVersionAsync(tool, detectedPath, cancellationToken);
        if (versionResult.Version.Length > 0)
        {
            result.Version = versionResult.Version;
        }
        else if (registryHit is not null && registryHit.Detail.Length > 0)
        {
            result.Version = VersionTools.ExtractVersion(registryHit.Detail);
        }
        else if (wingetInstalled is not null)
        {
            result.Version = wingetInstalled.Version;
        }

        if (versionResult.Failed && HasRunnableCommand(tool, detectedPath))
        {
            result.Status = ToolStatus.Broken;
            result.SuggestedAction = "Repair";
            result.Diagnostic = versionResult.Error.Length > 0
                ? versionResult.Error
                : "Detected, but the validation command failed.";
            return result;
        }

        var upgrade = await _winget.FindUpgradeAsync(tool, cancellationToken);
        if (upgrade is not null)
        {
            result.LatestVersion = string.IsNullOrWhiteSpace(upgrade.Available) ? upgrade.Version : upgrade.Available;
            result.IsUpdateAvailable = !string.IsNullOrWhiteSpace(result.LatestVersion);
        }

        if (!isOnPath && tool.Detection.Executables.Count > 0 && HasExecutableHit(hits))
        {
            result.Status = ToolStatus.Installed_NotOnPath;
            result.SuggestedAction = "Fix PATH";
            result.Diagnostic = "Installed but the command is not available from the current PATH.";
            return result;
        }

        if (result.IsUpdateAvailable)
        {
            result.Status = ToolStatus.Installed_Outdated;
            result.SuggestedAction = "Update";
            result.Diagnostic = "winget reports an available update.";
            return result;
        }

        result.Status = ToolStatus.Installed_Current;
        result.SuggestedAction = "Open";
        result.Diagnostic = "Detected and validation passed where available.";
        return result;
    }

    public async Task ApplyGithubAuthOverlayAsync(ToolScanResult result, CancellationToken cancellationToken = default)
    {
        if (result.ToolId != "github-cli" || result.Status is ToolStatus.Missing_Recommended or ToolStatus.Missing_Optional or ToolStatus.Broken)
        {
            return;
        }

        var executable = string.IsNullOrWhiteSpace(result.DetectedPath) ? "gh.exe" : result.DetectedPath;
        var auth = await _runner.RunAsync(executable, ["auth", "status"], TimeSpan.FromSeconds(10), cancellationToken);
        if (!auth.Success)
        {
            result.Status = ToolStatus.AuthNeeded;
            result.SuggestedAction = "Run gh auth login";
            result.Diagnostic = "GitHub CLI is installed, but no valid auth session was detected. Run: gh auth login";
        }
    }

    private DetectionHit? FindPathHit(ToolDefinition tool)
    {
        foreach (var executable in tool.Detection.Executables)
        {
            var path = PathTools.FindOnPath(executable);
            if (!string.IsNullOrWhiteSpace(path))
            {
                return new DetectionHit { Source = "PATH", Value = path, Detail = executable };
            }
        }

        return null;
    }

    private static IEnumerable<DetectionHit> FindCommonPathHits(ToolDefinition tool)
    {
        foreach (var pattern in tool.Detection.CommonPaths)
        {
            foreach (var expanded in PathTools.ExpandCommonPathPattern(pattern))
            {
                var path = Environment.ExpandEnvironmentVariables(expanded);
                if (File.Exists(path) || Directory.Exists(path))
                {
                    yield return new DetectionHit { Source = "common path", Value = path, Detail = pattern };
                }
            }
        }
    }

    private DetectionHit? FindRegistryHit(ToolDefinition tool)
    {
        var entry = _registry.FindByPatterns(tool.Detection.RegistryPatterns);
        if (entry is null)
        {
            return null;
        }

        return new DetectionHit
        {
            Source = "registry",
            Value = string.IsNullOrWhiteSpace(entry.InstallLocation) ? entry.DisplayName : entry.InstallLocation,
            Detail = $"{entry.DisplayName} {entry.DisplayVersion}".Trim()
        };
    }

    private static IEnumerable<DetectionHit> FindEnvironmentHits(ToolDefinition tool)
    {
        foreach (var envVar in tool.Detection.EnvVars)
        {
            var value = Environment.GetEnvironmentVariable(envVar, EnvironmentVariableTarget.Process) ??
                        Environment.GetEnvironmentVariable(envVar, EnvironmentVariableTarget.User) ??
                        Environment.GetEnvironmentVariable(envVar, EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrWhiteSpace(value))
            {
                var expanded = Environment.ExpandEnvironmentVariables(value);
                if (File.Exists(expanded) || Directory.Exists(expanded) || value.Length > 0)
                {
                    yield return new DetectionHit { Source = "environment", Value = value, Detail = envVar };
                }
            }
        }
    }

    private async Task<(string Version, bool Failed, string Error)> TryReadVersionAsync(ToolDefinition tool, string detectedPath, CancellationToken cancellationToken)
    {
        foreach (var command in tool.Detection.VersionCommands)
        {
            var executable = ResolveExecutable(command.Executable, detectedPath);
            if (string.IsNullOrWhiteSpace(executable))
            {
                continue;
            }

            var result = await _runner.RunAsync(executable, command.Arguments, TimeSpan.FromSeconds(12), cancellationToken);
            var text = string.IsNullOrWhiteSpace(result.Output) ? result.Error : result.Output;
            if (result.Success)
            {
                return (VersionTools.ExtractVersion(text), false, "");
            }

            return ("", true, text.Trim());
        }

        return ("", false, "");
    }

    private static string ResolveExecutable(string commandExecutable, string detectedPath)
    {
        if (!string.IsNullOrWhiteSpace(detectedPath) &&
            Path.GetFileName(detectedPath).Equals(commandExecutable, StringComparison.OrdinalIgnoreCase))
        {
            return detectedPath;
        }

        var path = PathTools.FindOnPath(commandExecutable);
        if (!string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return !string.IsNullOrWhiteSpace(detectedPath) && File.Exists(detectedPath)
            ? detectedPath
            : "";
    }

    private static bool HasRunnableCommand(ToolDefinition tool, string detectedPath)
    {
        if (tool.Detection.VersionCommands.Count == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(detectedPath) && File.Exists(detectedPath))
        {
            return true;
        }

        return tool.Detection.VersionCommands.Any(x => PathTools.FindOnPath(x.Executable) is not null);
    }

    private static bool HasExecutableHit(IEnumerable<DetectionHit> hits)
    {
        return hits.Any(x => x.Source is "PATH" or "common path");
    }

    private static string ChooseDetectedPath(IEnumerable<DetectionHit> hits)
    {
        var pathHit = hits.FirstOrDefault(x => x.Source == "PATH" && File.Exists(Environment.ExpandEnvironmentVariables(x.Value)));
        if (pathHit is not null)
        {
            return pathHit.Value;
        }

        var commonHit = hits.FirstOrDefault(x => x.Source == "common path" && File.Exists(Environment.ExpandEnvironmentVariables(x.Value)));
        if (commonHit is not null)
        {
            return commonHit.Value;
        }

        var environmentHit = hits.FirstOrDefault(x => x.Source == "environment");
        if (environmentHit is not null)
        {
            return environmentHit.Value;
        }

        return hits.FirstOrDefault()?.Value ?? "";
    }

    private static string BuildDetectionSummary(List<DetectionHit> hits)
    {
        if (hits.Count == 0)
        {
            return "No detection source matched.";
        }

        return string.Join("; ", hits.Take(4).Select(x => $"{x.Source}: {x.Value}"));
    }

    private static void AddUnique(List<DetectionHit> hits, DetectionHit hit)
    {
        if (!hits.Any(x => x.Source == hit.Source && x.Value.Equals(hit.Value, StringComparison.OrdinalIgnoreCase)))
        {
            hits.Add(hit);
        }
    }
}
