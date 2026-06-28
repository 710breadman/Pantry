namespace DevToolsCurator.Core;

public sealed record ToolCommand(string FileName, IReadOnlyList<string> Arguments, string Description, bool RequiresAdmin = false);

public sealed class ToolOperationEvent
{
    public string ToolId { get; init; } = "";
    public string ToolName { get; init; } = "";
    public string Action { get; init; } = "";
    public string Message { get; init; } = "";
    public bool Success { get; init; }
}

public sealed class ToolOperationService
{
    private readonly ProcessRunner _runner;
    private readonly WingetCache _winget;

    public ToolOperationService(ProcessRunner? runner = null, WingetCache? winget = null)
    {
        _runner = runner ?? new ProcessRunner();
        _winget = winget ?? new WingetCache(_runner);
    }

    public ToolCommand? BuildInstallCommand(ToolDefinition tool)
    {
        if (tool.WingetIds.Count > 0)
        {
            var id = tool.WingetIds[0];
            return new ToolCommand(
                "winget.exe",
                ["install", "--id", id, "--exact", "--source", "winget", "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity"],
                $"Install {tool.DisplayName} with winget",
                tool.IsHeavy);
        }

        return tool.InstallMethod switch
        {
            "pipx" => new ToolCommand("pipx.exe", ["install", PackageName(tool)], $"Install {tool.DisplayName} with pipx"),
            "npm-global" => new ToolCommand("npm.cmd", ["install", "-g", PackageName(tool)], $"Install {tool.DisplayName} globally with npm"),
            "python-user" => new ToolCommand("python.exe", ["-m", "pip", "install", "--user", PackageName(tool)], $"Install {tool.DisplayName} with Python user pip"),
            _ => null
        };
    }

    public ToolCommand? BuildUpdateCommand(ToolDefinition tool)
    {
        if (tool.WingetIds.Count > 0)
        {
            var id = tool.WingetIds[0];
            return new ToolCommand(
                "winget.exe",
                ["upgrade", "--id", id, "--exact", "--source", "winget", "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity"],
                $"Update {tool.DisplayName} with winget",
                tool.IsHeavy);
        }

        return tool.InstallMethod switch
        {
            "pipx" => new ToolCommand("pipx.exe", ["upgrade", PackageName(tool)], $"Update {tool.DisplayName} with pipx"),
            "npm-global" => new ToolCommand("npm.cmd", ["update", "-g", PackageName(tool)], $"Update {tool.DisplayName} globally with npm"),
            _ => null
        };
    }

    public IReadOnlyList<ToolScanResult> GetInstallRecommendedTargets(IEnumerable<ToolScanResult> results)
    {
        return results
            .Where(x => x.Tool is not null && x.IsRecommendedForGoal && (x.Status == ToolStatus.Missing_Recommended || x.Status == ToolStatus.Broken))
            .Where(x => !IsHeavyOptional(x.ToolId))
            .OrderByDescending(x => x.Tool!.ImportanceScore)
            .ToList();
    }

    public IReadOnlyList<ToolScanResult> GetBestStackTargets(ToolCatalog catalog, IEnumerable<ToolScanResult> results)
    {
        var best = catalog.BestDevStack.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return results
            .Where(x => x.Tool is not null && best.Contains(x.ToolId) && (x.Status == ToolStatus.Missing_Recommended || x.Status == ToolStatus.Missing_Optional || x.Status == ToolStatus.Broken))
            .Where(x => !IsHeavyOptional(x.ToolId))
            .OrderBy(x => catalog.BestDevStack.FindIndex(id => id.Equals(x.ToolId, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public IReadOnlyList<ToolScanResult> GetUpdateTargets(IEnumerable<ToolScanResult> results)
    {
        return results
            .Where(x => x.Tool is not null && x.Status == ToolStatus.Installed_Outdated && BuildUpdateCommand(x.Tool) is not null)
            .OrderByDescending(x => x.Tool!.ImportanceScore)
            .ToList();
    }

    public IReadOnlyList<ToolScanResult> GetRepairTargets(IEnumerable<ToolScanResult> results)
    {
        return results
            .Where(x => x.Tool is not null && x.Status is ToolStatus.Broken or ToolStatus.Installed_NotOnPath)
            .OrderByDescending(x => x.Tool!.ImportanceScore)
            .ToList();
    }

    public async Task RunInstallQueueAsync(IEnumerable<ToolScanResult> targets, IProgress<ToolOperationEvent>? progress = null, CancellationToken cancellationToken = default)
    {
        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (target.Tool is null)
            {
                continue;
            }

            var command = BuildInstallCommand(target.Tool);
            if (command is null)
            {
                progress?.Report(new ToolOperationEvent
                {
                    ToolId = target.ToolId,
                    ToolName = target.DisplayName,
                    Action = "Install",
                    Message = "No automatic installer is configured. Use the official link in details.",
                    Success = false
                });
                continue;
            }

            await RunCommandAsync(target, "Install", command, progress, cancellationToken);
        }
    }

    public async Task RunUpdateQueueAsync(IEnumerable<ToolScanResult> targets, IProgress<ToolOperationEvent>? progress = null, CancellationToken cancellationToken = default)
    {
        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (target.Tool is null)
            {
                continue;
            }

            var command = BuildUpdateCommand(target.Tool);
            if (command is null)
            {
                progress?.Report(new ToolOperationEvent
                {
                    ToolId = target.ToolId,
                    ToolName = target.DisplayName,
                    Action = "Update",
                    Message = "No automatic updater is configured.",
                    Success = false
                });
                continue;
            }

            await RunCommandAsync(target, "Update", command, progress, cancellationToken);
        }
    }

    public async Task RunAggregateUpdatesAsync(IProgress<ToolOperationEvent>? progress = null, CancellationToken cancellationToken = default)
    {
        var aggregateCommands = new[]
        {
            new ToolCommand("pipx.exe", ["upgrade-all"], "Update all pipx-managed tools"),
            new ToolCommand("npm.cmd", ["update", "-g"], "Update npm global tools"),
            new ToolCommand("dotnet.exe", ["tool", "update", "--global", "dotnet-format"], "Update dotnet-format if installed")
        };

        foreach (var command in aggregateCommands)
        {
            if (PathTools.FindOnPath(command.FileName) is null)
            {
                continue;
            }

            progress?.Report(new ToolOperationEvent { Action = "Update", ToolName = command.FileName, Message = command.Description, Success = true });
            var result = await _runner.RunAsync(command.FileName, command.Arguments, TimeSpan.FromMinutes(10), cancellationToken);
            progress?.Report(new ToolOperationEvent
            {
                Action = "Update",
                ToolName = command.FileName,
                Message = result.Success ? "Complete" : ShortError(result),
                Success = result.Success
            });
        }
    }

    public async Task<bool> ValidateWingetIdAsync(ToolDefinition tool, CancellationToken cancellationToken = default)
    {
        return tool.WingetIds.Count == 0 || await _winget.ValidateIdAsync(tool.WingetIds[0], cancellationToken);
    }

    private async Task RunCommandAsync(ToolScanResult target, string action, ToolCommand command, IProgress<ToolOperationEvent>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new ToolOperationEvent
        {
            ToolId = target.ToolId,
            ToolName = target.DisplayName,
            Action = action,
            Message = command.Description,
            Success = true
        });

        var result = await _runner.RunAsync(command.FileName, command.Arguments, TimeSpan.FromMinutes(30), cancellationToken);
        progress?.Report(new ToolOperationEvent
        {
            ToolId = target.ToolId,
            ToolName = target.DisplayName,
            Action = action,
            Message = result.Success ? "Complete" : ShortError(result),
            Success = result.Success
        });
    }

    private static string ShortError(ProcessResult result)
    {
        var text = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
        var firstLine = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstLine) ? $"Failed with exit code {result.ExitCode}" : firstLine;
    }

    private static string PackageName(ToolDefinition tool)
    {
        return tool.ToolId switch
        {
            "typescript" => "typescript",
            "eslint" => "eslint",
            "prettier" => "prettier",
            "vitest" => "vitest",
            "yarn" => "yarn",
            "pyright" => "pyright",
            _ => tool.ToolId
        };
    }

    private static bool IsHeavyOptional(string toolId)
    {
        return toolId is "android-studio" or "docker-desktop" or "wsl2" or "visual-studio-community";
    }
}
