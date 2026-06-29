using System.Text.Json;
using System.Reflection;

namespace DevToolsCurator.Core;

public sealed class CatalogLoadResult
{
    public ToolCatalog Catalog { get; init; } = new();
    public string Source { get; init; } = "";
    public bool UsedEmbeddedFallback { get; init; }
    public List<string> Warnings { get; init; } = [];
}

public sealed class CatalogService
{
    public const string EmbeddedCatalogResourceName = "RecipeCard.tool_catalog.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string FindProjectRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tool_catalog.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate tool_catalog.json from " + startDirectory);
    }

    public static bool TryFindProjectRoot(string startDirectory, out string projectRoot)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tool_catalog.json")))
            {
                projectRoot = directory.FullName;
                return true;
            }

            directory = directory.Parent;
        }

        projectRoot = "";
        return false;
    }

    public async Task<ToolCatalog> LoadAsync(string catalogPath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(catalogPath);
        return await LoadFromStreamAsync(stream, catalogPath, cancellationToken);
    }

    public async Task<CatalogLoadResult> LoadWithFallbackAsync(string? externalCatalogPath, CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        if (!string.IsNullOrWhiteSpace(externalCatalogPath) && File.Exists(externalCatalogPath))
        {
            try
            {
                var catalog = await LoadAsync(externalCatalogPath, cancellationToken);
                return new CatalogLoadResult
                {
                    Catalog = catalog,
                    Source = externalCatalogPath,
                    UsedEmbeddedFallback = false,
                    Warnings = warnings
                };
            }
            catch (Exception ex)
            {
                warnings.Add($"External tool catalog could not be loaded: {ex.Message}");
            }
        }

        await using var embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedCatalogResourceName);
        if (embedded is null)
        {
            throw new FileNotFoundException($"Embedded catalog resource '{EmbeddedCatalogResourceName}' was not found.");
        }

        var fallbackCatalog = await LoadFromStreamAsync(embedded, EmbeddedCatalogResourceName, cancellationToken);
        return new CatalogLoadResult
        {
            Catalog = fallbackCatalog,
            Source = EmbeddedCatalogResourceName,
            UsedEmbeddedFallback = true,
            Warnings = warnings
        };
    }

    public async Task SaveAsync(string path, ToolCatalog catalog, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, catalog, JsonOptions, cancellationToken);
    }

    private static async Task<ToolCatalog> LoadFromStreamAsync(Stream stream, string source, CancellationToken cancellationToken)
    {
        var catalog = await JsonSerializer.DeserializeAsync<ToolCatalog>(stream, JsonOptions, cancellationToken);
        if (catalog is null || catalog.Tools.Count == 0)
        {
            throw new InvalidDataException($"Tool catalog '{source}' is empty or invalid.");
        }

        CatalogValidator.ThrowIfInvalid(catalog, source);
        return catalog;
    }
}
