namespace DevToolsCurator.Core;

public interface IEnvironmentPathStore
{
    string GetPath(EnvironmentVariableTarget target);
    void SetPath(string value, EnvironmentVariableTarget target);
}

public sealed class SystemEnvironmentPathStore : IEnvironmentPathStore
{
    public string GetPath(EnvironmentVariableTarget target)
    {
        return Environment.GetEnvironmentVariable("Path", target) ?? "";
    }

    public void SetPath(string value, EnvironmentVariableTarget target)
    {
        Environment.SetEnvironmentVariable("Path", value, target);
    }
}

public sealed class PathRepairRequest
{
    public string ToolId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string DetectedExecutablePath { get; init; } = "";
    public VersionCommandDefinition? ValidationCommand { get; init; }
}

public sealed class PathRepairResult
{
    public string ToolId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string AddedDirectory { get; init; } = "";
    public bool Changed { get; init; }
    public bool Success { get; init; }
    public string Message { get; init; } = "";
}

public sealed class PathRepairService
{
    private readonly ProcessRunner _runner;
    private readonly IEnvironmentPathStore _environmentPaths;

    public PathRepairService(ProcessRunner? runner = null, IEnvironmentPathStore? environmentPaths = null)
    {
        _runner = runner ?? new ProcessRunner();
        _environmentPaths = environmentPaths ?? new SystemEnvironmentPathStore();
    }

    public async Task<PathRepairResult> FixUserPathAsync(PathRepairRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DetectedExecutablePath))
        {
            return Failed(request, "", "No detected executable path was provided.");
        }

        var executable = Environment.ExpandEnvironmentVariables(request.DetectedExecutablePath);
        if (!File.Exists(executable))
        {
            return Failed(request, "", $"Executable does not exist: {request.DetectedExecutablePath}");
        }

        var directory = Path.GetDirectoryName(executable);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return Failed(request, "", $"Install directory does not exist: {directory}");
        }

        var userPath = _environmentPaths.GetPath(EnvironmentVariableTarget.User);
        var machinePath = _environmentPaths.GetPath(EnvironmentVariableTarget.Machine);
        var alreadyPresent = PathTools.PathContains(userPath, directory) || PathTools.PathContains(machinePath, directory);
        var updatedUserPath = alreadyPresent ? userPath : PathTools.AddPathEntry(userPath, directory);

        if (!alreadyPresent)
        {
            _environmentPaths.SetPath(updatedUserPath, EnvironmentVariableTarget.User);
        }

        RefreshProcessPath(machinePath, updatedUserPath);

        var validation = await ValidateFromFreshShellAsync(request, directory, cancellationToken);
        return new PathRepairResult
        {
            ToolId = request.ToolId,
            DisplayName = request.DisplayName,
            AddedDirectory = directory,
            Changed = !alreadyPresent,
            Success = validation.Success,
            Message = validation.Success
                ? alreadyPresent
                    ? $"PATH already contained {directory}; validation succeeded."
                    : $"Added {directory} to user PATH; validation succeeded."
                : validation.Message
        };
    }

    public static PathRepairRequest FromTool(ToolScanResult result)
    {
        return new PathRepairRequest
        {
            ToolId = result.ToolId,
            DisplayName = result.DisplayName,
            DetectedExecutablePath = result.DetectedPath,
            ValidationCommand = result.Tool?.Detection.VersionCommands.FirstOrDefault()
        };
    }

    private async Task<(bool Success, string Message)> ValidateFromFreshShellAsync(PathRepairRequest request, string directory, CancellationToken cancellationToken)
    {
        var command = request.ValidationCommand;
        if (command is null)
        {
            return (true, "PATH updated. No validation command is configured for this tool.");
        }

        var exeName = Path.GetFileName(request.DetectedExecutablePath);
        var args = string.Join(' ', command.Arguments.Select(QuotePowerShellArgument));
        var script = string.Join(Environment.NewLine, [
            "$env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')",
            "$cmd = Get-Command '" + EscapePowerShellSingleQuote(exeName) + "' -ErrorAction SilentlyContinue",
            "if (-not $cmd) { exit 66 }",
            "& $cmd.Source " + args + " *> $null",
            "exit $LASTEXITCODE"
        ]);

        var tempScript = Path.Combine(Path.GetTempPath(), "devtools-path-validate-" + Guid.NewGuid().ToString("N") + ".ps1");
        await File.WriteAllTextAsync(tempScript, script, cancellationToken);
        try
        {
            var pwsh = PathTools.FindOnPath("pwsh.exe") ?? PathTools.FindOnPath("powershell.exe") ?? "powershell.exe";
            var result = await _runner.RunAsync(pwsh, ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", tempScript], TimeSpan.FromSeconds(30), cancellationToken);
            return result.Success
                ? (true, "Validation succeeded from a fresh shell.")
                : (false, $"PATH was updated with {directory}, but fresh-shell validation failed for {request.DisplayName}. Exit code {result.ExitCode}.");
        }
        finally
        {
            try { File.Delete(tempScript); } catch { }
        }
    }

    private void RefreshProcessPath(string machinePath, string userPath)
    {
        _environmentPaths.SetPath(
            string.Join(';', new[] { machinePath, userPath }.Where(x => !string.IsNullOrWhiteSpace(x))),
            EnvironmentVariableTarget.Process);
    }

    private static PathRepairResult Failed(PathRepairRequest request, string directory, string message)
    {
        return new PathRepairResult
        {
            ToolId = request.ToolId,
            DisplayName = request.DisplayName,
            AddedDirectory = directory,
            Changed = false,
            Success = false,
            Message = message
        };
    }

    private static string QuotePowerShellArgument(string value)
    {
        return "'" + EscapePowerShellSingleQuote(value) + "'";
    }

    private static string EscapePowerShellSingleQuote(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }
}
