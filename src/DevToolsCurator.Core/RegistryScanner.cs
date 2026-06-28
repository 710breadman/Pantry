using Microsoft.Win32;
using System.Runtime.Versioning;

namespace DevToolsCurator.Core;

public sealed class RegistryInstallEntry
{
    public string Hive { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string DisplayVersion { get; init; } = "";
    public string InstallLocation { get; init; } = "";
    public string Publisher { get; init; } = "";
}

public sealed class RegistryScanner
{
    private static readonly string[] UninstallSubKeys =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    ];

    private readonly Lazy<List<RegistryInstallEntry>> _entries = new(LoadEntries);

    public IReadOnlyList<RegistryInstallEntry> Entries => _entries.Value;

    public RegistryInstallEntry? FindByPatterns(IEnumerable<string> patterns)
    {
        var patternList = patterns.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (patternList.Count == 0)
        {
            return null;
        }

        return Entries.FirstOrDefault(entry =>
            patternList.Any(pattern =>
                entry.DisplayName.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                entry.Publisher.Contains(pattern, StringComparison.OrdinalIgnoreCase)));
    }

    private static List<RegistryInstallEntry> LoadEntries()
    {
        var rows = new List<RegistryInstallEntry>();
        if (!OperatingSystem.IsWindows())
        {
            return rows;
        }

        ReadHive(RegistryHive.LocalMachine, rows);
        ReadHive(RegistryHive.CurrentUser, rows);
        return rows;
    }

    [SupportedOSPlatform("windows")]
    private static void ReadHive(RegistryHive hive, List<RegistryInstallEntry> rows)
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                foreach (var subKeyPath in UninstallSubKeys)
                {
                    using var uninstall = baseKey.OpenSubKey(subKeyPath);
                    if (uninstall is null)
                    {
                        continue;
                    }

                    foreach (var name in uninstall.GetSubKeyNames())
                    {
                        using var appKey = uninstall.OpenSubKey(name);
                        if (appKey is null)
                        {
                            continue;
                        }

                        var displayName = Convert.ToString(appKey.GetValue("DisplayName")) ?? "";
                        if (string.IsNullOrWhiteSpace(displayName))
                        {
                            continue;
                        }

                        rows.Add(new RegistryInstallEntry
                        {
                            Hive = $"{hive}/{view}",
                            DisplayName = displayName,
                            DisplayVersion = Convert.ToString(appKey.GetValue("DisplayVersion")) ?? "",
                            InstallLocation = Convert.ToString(appKey.GetValue("InstallLocation")) ?? "",
                            Publisher = Convert.ToString(appKey.GetValue("Publisher")) ?? ""
                        });
                    }
                }
            }
            catch
            {
                // Registry visibility varies by process bitness and policy; missing views are not fatal.
            }
        }
    }
}
