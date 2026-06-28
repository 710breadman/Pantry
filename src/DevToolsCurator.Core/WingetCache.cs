namespace DevToolsCurator.Core;

public sealed class WingetPackageRow
{
    public string Name { get; init; } = "";
    public string Id { get; init; } = "";
    public string Version { get; init; } = "";
    public string Available { get; init; } = "";
    public string Source { get; init; } = "";
}

public sealed class WingetCache
{
    private readonly ProcessRunner _runner;
    private List<WingetPackageRow>? _installed;
    private List<WingetPackageRow>? _upgrades;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public WingetCache(ProcessRunner? runner = null)
    {
        _runner = runner ?? new ProcessRunner();
    }

    public bool WingetAvailable => PathTools.FindOnPath("winget.exe") is not null ||
                                   File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\winget.exe"));

    public async Task<IReadOnlyList<WingetPackageRow>> GetInstalledAsync(CancellationToken cancellationToken = default)
    {
        if (_installed is not null)
        {
            return _installed;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_installed is null)
            {
                _installed = await RunTableAsync(["list", "--accept-source-agreements", "--disable-interactivity"], cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }

        return _installed;
    }

    public async Task<IReadOnlyList<WingetPackageRow>> GetUpgradesAsync(CancellationToken cancellationToken = default)
    {
        if (_upgrades is not null)
        {
            return _upgrades;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_upgrades is null)
            {
                _upgrades = await RunTableAsync(["upgrade", "--accept-source-agreements", "--disable-interactivity"], cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }

        return _upgrades;
    }

    public async Task<bool> ValidateIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id) || !WingetAvailable)
        {
            return false;
        }

        var result = await _runner.RunAsync("winget.exe", ["show", "--id", id, "--exact", "--accept-source-agreements", "--disable-interactivity"], TimeSpan.FromSeconds(20), cancellationToken);
        return result.Success;
    }

    public async Task<WingetPackageRow?> FindInstalledAsync(ToolDefinition tool, CancellationToken cancellationToken = default)
    {
        var rows = await GetInstalledAsync(cancellationToken);
        return FindMatch(rows, tool);
    }

    public async Task<WingetPackageRow?> FindUpgradeAsync(ToolDefinition tool, CancellationToken cancellationToken = default)
    {
        var rows = await GetUpgradesAsync(cancellationToken);
        return FindMatch(rows, tool);
    }

    public void Clear()
    {
        _installed = null;
        _upgrades = null;
    }

    private async Task<List<WingetPackageRow>> RunTableAsync(string[] arguments, CancellationToken cancellationToken)
    {
        if (!WingetAvailable)
        {
            return [];
        }

        var result = await _runner.RunAsync("winget.exe", arguments, TimeSpan.FromSeconds(60), cancellationToken);
        if (!result.Success)
        {
            return [];
        }

        return ParseTable(result.Output);
    }

    public static List<WingetPackageRow> ParseTable(string output)
    {
        var rows = new List<WingetPackageRow>();
        var rawLines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.TrimEnd())
            .ToList();
        var header = rawLines.FirstOrDefault(x => x.StartsWith("Name ", StringComparison.OrdinalIgnoreCase) && x.Contains(" Id ", StringComparison.Ordinal));
        if (header is not null)
        {
            var nameStart = header.IndexOf("Name", StringComparison.Ordinal);
            var idStart = header.IndexOf("Id", StringComparison.Ordinal);
            var versionStart = header.IndexOf("Version", StringComparison.Ordinal);
            var availableStart = header.IndexOf("Available", StringComparison.Ordinal);
            var sourceStart = header.IndexOf("Source", StringComparison.Ordinal);
            var dataStarted = false;

            foreach (var line in rawLines)
            {
                if (line.Contains("----", StringComparison.Ordinal))
                {
                    dataStarted = true;
                    continue;
                }

                if (!dataStarted || string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var idEnd = versionStart > idStart ? versionStart : line.Length;
                var versionEnd = availableStart > versionStart ? availableStart : sourceStart > versionStart ? sourceStart : line.Length;
                var availableEnd = sourceStart > availableStart ? sourceStart : line.Length;
                var name = SliceColumn(line, nameStart, idStart);
                var id = SliceColumn(line, idStart, idEnd);
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                rows.Add(new WingetPackageRow
                {
                    Name = name,
                    Id = id,
                    Version = versionStart >= 0 ? SliceColumn(line, versionStart, versionEnd) : "",
                    Available = availableStart >= 0 ? SliceColumn(line, availableStart, availableEnd) : "",
                    Source = sourceStart >= 0 ? SliceColumn(line, sourceStart, line.Length) : ""
                });
            }

            return rows;
        }

        var lines = rawLines
            .Where(x => !x.Contains("----", StringComparison.Ordinal))
            .ToList();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) ||
                line.StartsWith("Name ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("No installed package", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("The following packages", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var idIndex = Array.FindIndex(parts, x => x.Contains('.') || x.Contains('/'));
            if (idIndex <= 0)
            {
                continue;
            }

            var name = string.Join(' ', parts.Take(idIndex));
            var id = parts[idIndex];
            var source = parts.LastOrDefault(x => x.Equals("winget", StringComparison.OrdinalIgnoreCase) || x.Equals("msstore", StringComparison.OrdinalIgnoreCase)) ?? "";
            var values = string.IsNullOrWhiteSpace(source) ? parts : parts.Take(parts.Length - 1).ToArray();
            var version = idIndex + 1 < values.Length ? values[idIndex + 1] : "";
            var available = idIndex + 2 < values.Length ? values[idIndex + 2] : "";

            rows.Add(new WingetPackageRow
            {
                Name = name,
                Id = id,
                Version = version,
                Available = available,
                Source = source
            });
        }

        return rows;
    }

    private static string SliceColumn(string line, int start, int end)
    {
        if (start < 0 || start >= line.Length)
        {
            return "";
        }

        var safeEnd = Math.Min(Math.Max(end, start), line.Length);
        return line[start..safeEnd].Trim();
    }

    private static WingetPackageRow? FindMatch(IEnumerable<WingetPackageRow> rows, ToolDefinition tool)
    {
        return rows.FirstOrDefault(row =>
            tool.WingetIds.Any(id => row.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ||
            tool.Detection.WingetNames.Any(name => row.Name.Contains(name, StringComparison.OrdinalIgnoreCase)) ||
            tool.Detection.WingetNames.Any(name => row.Id.Contains(name, StringComparison.OrdinalIgnoreCase)));
    }
}
