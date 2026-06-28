using System.Security.Principal;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace DevToolsCurator.Core;

public sealed class SystemSnapshot
{
    public bool IsAdministrator { get; init; }
    public bool RebootPending { get; init; }
    public bool DeveloperModeEnabled { get; init; }
    public List<(string Scope, string Entry)> BrokenPathEntries { get; init; } = [];
    public List<(string Scope, string Entry)> DuplicatePathEntries { get; init; } = [];
    public string WingetPath { get; init; } = "";
    public List<string> WslDistros { get; init; } = [];
}

public sealed class SystemInspector
{
    private readonly ProcessRunner _runner;

    public SystemInspector(ProcessRunner? runner = null)
    {
        _runner = runner ?? new ProcessRunner();
    }

    public async Task<SystemSnapshot> InspectAsync(CancellationToken cancellationToken = default)
    {
        var isWindows = OperatingSystem.IsWindows();
        return new SystemSnapshot
        {
            IsAdministrator = isWindows && IsAdministrator(),
            RebootPending = isWindows && IsRebootPending(),
            DeveloperModeEnabled = isWindows && IsDeveloperModeEnabled(),
            BrokenPathEntries = PathTools.FindBrokenPathEntries(),
            DuplicatePathEntries = PathTools.FindDuplicatePathEntries(),
            WingetPath = PathTools.FindOnPath("winget.exe") ?? "",
            WslDistros = await GetWslDistrosAsync(cancellationToken)
        };
    }

    [SupportedOSPlatform("windows")]
    private static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool IsDeveloperModeEnabled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");
            return Convert.ToInt32(key?.GetValue("AllowDevelopmentWithoutDevLicense") ?? 0) == 1;
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool IsRebootPending()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var cbs = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            using var wu = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            using var session = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
            return cbs is not null || wu is not null || session?.GetValue("PendingFileRenameOperations") is not null;
        }
        catch
        {
            return false;
        }
    }

    private async Task<List<string>> GetWslDistrosAsync(CancellationToken cancellationToken)
    {
        if (PathTools.FindOnPath("wsl.exe") is null && !File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\wsl.exe")))
        {
            return [];
        }

        var result = await _runner.RunAsync("wsl.exe", ["--list", "--quiet"], TimeSpan.FromSeconds(8), cancellationToken);
        if (!result.Success)
        {
            return [];
        }

        return result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim('\0', ' ', '\t'))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }
}
