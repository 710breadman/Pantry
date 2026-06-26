using Microsoft.Win32;
using System.Runtime.Versioning;

namespace Pantry.Detection;

public sealed class WindowsRegistryReader : IRegistryReader
{
    private const string UninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public Task<IReadOnlyList<RegistryAppEntry>> ReadInstalledAppsAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<IReadOnlyList<RegistryAppEntry>>([]);
        }

        var apps = new List<RegistryAppEntry>();
        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry64, apps, cancellationToken);
        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry32, apps, cancellationToken);
        ReadHive(RegistryHive.CurrentUser, RegistryView.Registry64, apps, cancellationToken);
        ReadHive(RegistryHive.CurrentUser, RegistryView.Registry32, apps, cancellationToken);

        return Task.FromResult<IReadOnlyList<RegistryAppEntry>>(apps);
    }

    [SupportedOSPlatform("windows")]
    private static void ReadHive(
        RegistryHive hive,
        RegistryView view,
        List<RegistryAppEntry> apps,
        CancellationToken cancellationToken)
    {
        using var root = RegistryKey.OpenBaseKey(hive, view);
        using var uninstallKey = root.OpenSubKey(UninstallKeyPath, writable: false);
        if (uninstallKey is null)
        {
            return;
        }

        foreach (var subKeyName in uninstallKey.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var appKey = uninstallKey.OpenSubKey(subKeyName, writable: false);
            var displayName = appKey?.GetValue("DisplayName") as string;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            apps.Add(new RegistryAppEntry
            {
                DisplayName = displayName,
                DisplayVersion = appKey?.GetValue("DisplayVersion") as string,
                RegistryPath = $@"{hive}\{view}\{UninstallKeyPath}\{subKeyName}"
            });
        }
    }
}
