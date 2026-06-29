using System.Text.RegularExpressions;

namespace DevToolsCurator.Core;

public sealed class CatalogValidationResult
{
    public List<string> Errors { get; init; } = [];
    public bool IsValid => Errors.Count == 0;
}

public static partial class CatalogValidator
{
    public const string SupportedSchemaVersion = "2.0";

    private static readonly HashSet<string> InstallMethods = new(StringComparer.Ordinal)
    {
        "builtin",
        "manual",
        "npm-global",
        "pipx",
        "python-user",
        "windows-feature",
        "winget"
    };

    private static readonly HashSet<string> InstallTiers = new(StringComparer.Ordinal)
    {
        "Core",
        "Recommended",
        "Optional"
    };

    public static CatalogValidationResult Validate(ToolCatalog catalog)
    {
        var errors = new List<string>();

        if (!string.Equals(catalog.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            errors.Add($"schema_version must be '{SupportedSchemaVersion}', found '{catalog.SchemaVersion}'.");
        }

        RequireNonEmptyUnique(catalog.SourceNotes, "source_notes", errors);
        RequireNonEmptyUnique(catalog.BestDevStack, "best_dev_stack", errors);

        if (catalog.Tools.Count == 0)
        {
            errors.Add("tools must contain at least one tool.");
        }

        var duplicateIds = catalog.Tools
            .Where(tool => !string.IsNullOrWhiteSpace(tool.ToolId))
            .GroupBy(tool => tool.ToolId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        foreach (var duplicateId in duplicateIds)
        {
            errors.Add($"tools contains duplicate tool_id '{duplicateId}'.");
        }

        var toolIds = catalog.Tools
            .Select(tool => tool.ToolId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var stackId in catalog.BestDevStack.Where(id => !toolIds.Contains(id)))
        {
            errors.Add($"best_dev_stack references unknown tool_id '{stackId}'.");
        }

        foreach (var tool in catalog.Tools)
        {
            ValidateTool(tool, errors);
        }

        return new CatalogValidationResult { Errors = errors };
    }

    public static void ThrowIfInvalid(ToolCatalog catalog, string source)
    {
        var result = Validate(catalog);
        if (!result.IsValid)
        {
            throw new InvalidDataException(
                $"Tool catalog '{source}' failed schema validation:{Environment.NewLine}- " +
                string.Join(Environment.NewLine + "- ", result.Errors));
        }
    }

    private static void ValidateTool(ToolDefinition tool, List<string> errors)
    {
        var label = string.IsNullOrWhiteSpace(tool.ToolId) ? "<missing-id>" : tool.ToolId;
        if (string.IsNullOrWhiteSpace(tool.ToolId) || !ToolIdPattern().IsMatch(tool.ToolId))
        {
            errors.Add($"tool '{label}' has invalid tool_id; expected lowercase kebab-case.");
        }

        RequireText(tool.DisplayName, label, "display_name", errors);
        RequireText(tool.Category, label, "category", errors);
        RequireText(tool.Description, label, "description", errors);
        RequireText(tool.WhyItMatters, label, "why_it_matters", errors);
        RequireNonEmptyUnique(tool.UsedFor, $"tool '{label}'.used_for", errors);
        RequireNonEmptyUnique(tool.GoalTags, $"tool '{label}'.goal_tags", errors);
        RequireUnique(tool.WingetIds, $"tool '{label}'.winget_ids", errors);
        RequireUnique(tool.FallbackUrls, $"tool '{label}'.fallback_urls", errors);

        if (!InstallMethods.Contains(tool.InstallMethod))
        {
            errors.Add($"tool '{label}' has unsupported install_method '{tool.InstallMethod}'.");
        }

        if (!InstallTiers.Contains(tool.InstallTier))
        {
            errors.Add($"tool '{label}' has unsupported install_tier '{tool.InstallTier}'.");
        }

        if (tool.ImportanceScore is < 0 or > 100)
        {
            errors.Add($"tool '{label}' importance_score must be between 0 and 100.");
        }

        if (tool.InstallMethod == "winget" && tool.WingetIds.Count == 0)
        {
            errors.Add($"tool '{label}' uses winget but has no winget_ids.");
        }

        foreach (var url in tool.FallbackUrls)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps)
            {
                errors.Add($"tool '{label}' fallback URL must be absolute HTTPS: '{url}'.");
            }
        }

        ValidateDetection(tool, label, errors);
    }

    private static void ValidateDetection(ToolDefinition tool, string label, List<string> errors)
    {
        var detection = tool.Detection;
        RequireUnique(detection.Executables, $"tool '{label}'.detection.executables", errors);
        RequireUnique(detection.CommonPaths, $"tool '{label}'.detection.common_paths", errors);
        RequireUnique(detection.RegistryPatterns, $"tool '{label}'.detection.registry_patterns", errors);
        RequireUnique(detection.EnvVars, $"tool '{label}'.detection.env_vars", errors);
        RequireUnique(detection.WingetNames, $"tool '{label}'.detection.winget_names", errors);

        var signalCount = detection.Executables.Count +
                          detection.CommonPaths.Count +
                          detection.RegistryPatterns.Count +
                          detection.VersionCommands.Count +
                          detection.EnvVars.Count +
                          detection.WingetNames.Count;
        if (signalCount == 0)
        {
            errors.Add($"tool '{label}' must define at least one detection signal.");
        }

        for (var index = 0; index < detection.VersionCommands.Count; index++)
        {
            var command = detection.VersionCommands[index];
            if (string.IsNullOrWhiteSpace(command.Executable))
            {
                errors.Add($"tool '{label}'.detection.version_commands[{index}].executable is required.");
            }

            RequireUnique(command.Arguments, $"tool '{label}'.detection.version_commands[{index}].arguments", errors);
        }
    }

    private static void RequireText(string value, string toolId, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"tool '{toolId}'.{field} is required.");
        }
    }

    private static void RequireNonEmptyUnique(IEnumerable<string> values, string field, List<string> errors)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            errors.Add($"{field} must contain at least one value.");
        }

        RequireUnique(list, field, errors);
    }

    private static void RequireUnique(IEnumerable<string> values, string field, List<string> errors)
    {
        var list = values.ToList();
        if (list.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add($"{field} cannot contain blank values.");
        }

        if (list.Distinct(StringComparer.OrdinalIgnoreCase).Count() != list.Count)
        {
            errors.Add($"{field} cannot contain duplicate values.");
        }
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ToolIdPattern();
}
