namespace DevToolsCurator.Core;

public sealed class DevKitRuntimePaths
{
    private const string AppFolderName = "RecipeCard";

    public string AppBaseDirectory { get; init; } = "";
    public bool IsPortable { get; init; }
    public string ConfigPath { get; init; } = "";
    public string ReportDirectory { get; init; } = "";
    public string CacheDirectory { get; init; } = "";
    public string? CatalogOverridePath { get; init; }
    public string ModeName => IsPortable ? "Portable" : "AppData";

    public static DevKitRuntimePaths Resolve(string appBaseDirectory)
    {
        var baseDirectory = Path.GetFullPath(appBaseDirectory);
        var portableMarker = File.Exists(Path.Combine(baseDirectory, ".portable")) ||
                             File.Exists(Path.Combine(baseDirectory, "RecipeCard.portable")) ||
                             File.Exists(Path.Combine(baseDirectory, "DevKit.portable")) ||
                             File.Exists(Path.Combine(baseDirectory, "config.json")) ||
                             Directory.Exists(Path.Combine(baseDirectory, "reports")) ||
                             Directory.Exists(Path.Combine(baseDirectory, "cache"));

        var roamingRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppFolderName);
        var localRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName);

        var configPath = portableMarker
            ? Path.Combine(baseDirectory, "config.json")
            : Path.Combine(roamingRoot, "config.json");
        var reportDirectory = portableMarker
            ? Path.Combine(baseDirectory, "reports")
            : Path.Combine(roamingRoot, "reports");
        var cacheDirectory = portableMarker
            ? Path.Combine(baseDirectory, "cache")
            : Path.Combine(localRoot, "cache");

        var catalogOverridePath = FirstExisting(
            Path.Combine(baseDirectory, "tool_catalog.json"),
            portableMarker ? null : Path.Combine(roamingRoot, "tool_catalog.json"));

        return new DevKitRuntimePaths
        {
            AppBaseDirectory = baseDirectory,
            IsPortable = portableMarker,
            ConfigPath = configPath,
            ReportDirectory = reportDirectory,
            CacheDirectory = cacheDirectory,
            CatalogOverridePath = catalogOverridePath
        };
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        Directory.CreateDirectory(ReportDirectory);
        Directory.CreateDirectory(CacheDirectory);
    }

    public async Task EnsureDefaultConfigAsync(string? defaultConfigSourcePath = null, CancellationToken cancellationToken = default)
    {
        EnsureDirectories();
        if (File.Exists(ConfigPath))
        {
            return;
        }

        var defaultConfig = "{}";
        if (!string.IsNullOrWhiteSpace(defaultConfigSourcePath) && File.Exists(defaultConfigSourcePath))
        {
            defaultConfig = await File.ReadAllTextAsync(defaultConfigSourcePath, cancellationToken);
        }

        await File.WriteAllTextAsync(ConfigPath, defaultConfig, cancellationToken);
    }

    private static string? FirstExisting(params string?[] paths)
    {
        foreach (var path in paths)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
