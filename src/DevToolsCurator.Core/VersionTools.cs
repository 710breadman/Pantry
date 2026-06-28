using System.Text.RegularExpressions;

namespace DevToolsCurator.Core;

public static partial class VersionTools
{
    public static string ExtractVersion(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var match = VersionRegex().Match(text);
        if (match.Success)
        {
            return match.Value;
        }

        return text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? text.Trim();
    }

    public static int CompareLoose(string current, string latest)
    {
        var c = ParseParts(current);
        var l = ParseParts(latest);
        var count = Math.Max(c.Count, l.Count);
        for (var i = 0; i < count; i++)
        {
            var left = i < c.Count ? c[i] : 0;
            var right = i < l.Count ? l[i] : 0;
            var comparison = left.CompareTo(right);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private static List<int> ParseParts(string value)
    {
        var match = VersionRegex().Match(value ?? "");
        if (!match.Success)
        {
            return [];
        }

        return match.Value
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var number) ? number : 0)
            .ToList();
    }

    [GeneratedRegex(@"\d+(?:\.\d+){0,4}")]
    private static partial Regex VersionRegex();
}
