namespace DevToolsCurator.Core;

public sealed class StartupSelfCheckReport
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public bool ConfigWritable { get; init; }
    public bool ReportFolderWritable { get; init; }
    public bool CacheFolderWritable { get; init; }
    public bool CatalogLoaded { get; init; }
    public bool PowerShellAvailable { get; init; }
    public bool WingetAvailable { get; init; }
    public List<string> Warnings { get; init; } = [];
    public List<string> Errors { get; init; } = [];
    public bool IsCriticalFailure => !ConfigWritable || !ReportFolderWritable || !CacheFolderWritable || !CatalogLoaded || Errors.Count > 0;
}

public static class StartupSelfCheck
{
    public static StartupSelfCheckReport Run(DevKitRuntimePaths paths, ToolCatalog? catalog)
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        var configWritable = CanWriteFile(paths.ConfigPath, errors);
        var reportWritable = CanWriteDirectory(paths.ReportDirectory, errors);
        var cacheWritable = CanWriteDirectory(paths.CacheDirectory, errors);
        var powerShellAvailable = PathTools.FindOnPath("pwsh.exe") is not null || PathTools.FindOnPath("powershell.exe") is not null;
        var wingetAvailable = PathTools.FindOnPath("winget.exe") is not null;

        if (!powerShellAvailable)
        {
            warnings.Add("PowerShell executable was not found on PATH. Detection can still run, but setup automation may be limited.");
        }

        if (!wingetAvailable)
        {
            warnings.Add("winget was not found on PATH. Install/update actions will be limited until App Installer is repaired.");
        }

        return new StartupSelfCheckReport
        {
            ConfigWritable = configWritable,
            ReportFolderWritable = reportWritable,
            CacheFolderWritable = cacheWritable,
            CatalogLoaded = catalog?.Tools.Count > 0,
            PowerShellAvailable = powerShellAvailable,
            WingetAvailable = wingetAvailable,
            Warnings = warnings,
            Errors = errors
        };
    }

    private static bool CanWriteDirectory(string directory, List<string> errors)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var testPath = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testPath, "ok");
            File.Delete(testPath);
            return true;
        }
        catch (Exception ex)
        {
            errors.Add($"Cannot write to '{directory}': {ex.Message}");
            return false;
        }
    }

    private static bool CanWriteFile(string filePath, List<string> errors)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            using var stream = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            stream.Flush();
            return true;
        }
        catch (Exception ex)
        {
            errors.Add($"Cannot write config '{filePath}': {ex.Message}");
            return false;
        }
    }
}
