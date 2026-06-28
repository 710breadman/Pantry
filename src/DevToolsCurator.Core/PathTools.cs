namespace DevToolsCurator.Core;

public static class PathTools
{
    private static readonly string[] ExecutableExtensions = [".exe", ".cmd", ".bat", ".ps1"];

    public static IReadOnlyList<string> UserPathEntries()
    {
        var user = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
        return SplitPath(user);
    }

    public static IReadOnlyList<string> MachinePathEntries()
    {
        var machine = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine) ?? "";
        return SplitPath(machine);
    }

    public static IReadOnlyList<string> ProcessPathEntries()
    {
        return SplitPath(Environment.GetEnvironmentVariable("Path") ?? "");
    }

    public static IReadOnlyList<string> EffectivePathEntries()
    {
        var rows = new List<string>();
        rows.AddRange(ProcessPathEntries());
        rows.AddRange(MachinePathEntries());
        rows.AddRange(UserPathEntries());
        return rows
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .DistinctBy(NormalizePath)
            .ToList();
    }

    public static void RefreshProcessPathFromPersistedEnvironment()
    {
        var machinePath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine) ?? "";
        var userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
        var processOnly = ProcessPathEntries()
            .Where(entry => !PathContains(machinePath, entry) && !PathContains(userPath, entry))
            .ToList();
        var refreshed = string.Join(';', new[] { machinePath, userPath }.Concat(processOnly).Where(x => !string.IsNullOrWhiteSpace(x)));
        Environment.SetEnvironmentVariable("Path", refreshed, EnvironmentVariableTarget.Process);
    }

    public static List<string> SplitPath(string pathValue)
    {
        return pathValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    public static string NormalizePath(string value)
    {
        var expanded = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
        try
        {
            return Path.GetFullPath(expanded).TrimEnd('\\').ToUpperInvariant();
        }
        catch
        {
            return expanded.TrimEnd('\\').ToUpperInvariant();
        }
    }

    public static bool PathContains(string pathValue, string entry)
    {
        var normalized = NormalizePath(entry);
        return SplitPath(pathValue).Any(x => NormalizePath(x) == normalized);
    }

    public static string AddPathEntry(string pathValue, string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            return pathValue;
        }

        var entries = SplitPath(pathValue);
        if (entries.Any(x => NormalizePath(x) == NormalizePath(entry)))
        {
            return string.Join(';', entries);
        }

        entries.Add(entry);
        return string.Join(';', entries);
    }

    public static string? FindOnPath(string executable)
    {
        foreach (var directory in EffectivePathEntries())
        {
            var expanded = Environment.ExpandEnvironmentVariables(directory);
            var direct = Path.Combine(expanded, executable);
            if (File.Exists(direct))
            {
                return direct;
            }

            if (!Path.HasExtension(executable))
            {
                foreach (var extension in ExecutableExtensions)
                {
                    var candidate = Path.Combine(expanded, executable + extension);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }

    public static IEnumerable<string> ExpandCommonPathPattern(string pattern)
    {
        var expanded = Environment.ExpandEnvironmentVariables(pattern);
        if (!expanded.Contains('*'))
        {
            yield return expanded;
            yield break;
        }

        var directory = Path.GetDirectoryName(expanded);
        var fileName = Path.GetFileName(expanded);
        if (string.IsNullOrWhiteSpace(directory))
        {
            yield break;
        }

        var root = directory;
        while (root.Contains('*'))
        {
            var parent = Path.GetDirectoryName(root);
            if (string.IsNullOrWhiteSpace(parent))
            {
                yield break;
            }
            root = parent;
        }

        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).Take(24))
        {
            yield return file;
        }
    }

    public static List<(string Scope, string Entry)> FindBrokenPathEntries()
    {
        var rows = new List<(string Scope, string Entry)>();
        foreach (var entry in UserPathEntries())
        {
            var expanded = Environment.ExpandEnvironmentVariables(entry);
            if (!Directory.Exists(expanded))
            {
                rows.Add(("User", entry));
            }
        }

        foreach (var entry in MachinePathEntries())
        {
            var expanded = Environment.ExpandEnvironmentVariables(entry);
            if (!Directory.Exists(expanded))
            {
                rows.Add(("Machine", entry));
            }
        }

        return rows;
    }

    public static List<(string Scope, string Entry)> FindDuplicatePathEntries()
    {
        var rows = new List<(string Scope, string Entry)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in UserPathEntries().Select(x => (Scope: "User", Entry: x)).Concat(MachinePathEntries().Select(x => (Scope: "Machine", Entry: x))))
        {
            var normalized = NormalizePath(pair.Entry);
            if (!seen.Add(normalized))
            {
                rows.Add(pair);
            }
        }

        return rows;
    }

    public static bool IsOnPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        var normalizedDirectory = NormalizePath(directory);
        return EffectivePathEntries().Any(x => NormalizePath(x) == normalizedDirectory);
    }

    public static string? FindInEntries(string executable, IEnumerable<string> entries)
    {
        foreach (var directory in entries)
        {
            var expanded = Environment.ExpandEnvironmentVariables(directory);
            var direct = Path.Combine(expanded, executable);
            if (File.Exists(direct))
            {
                return direct;
            }

            if (!Path.HasExtension(executable))
            {
                foreach (var extension in ExecutableExtensions)
                {
                    var candidate = Path.Combine(expanded, executable + extension);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }
}
