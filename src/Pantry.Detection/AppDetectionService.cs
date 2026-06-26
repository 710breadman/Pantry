using Pantry.Domain;

namespace Pantry.Detection;

public sealed class AppDetectionService
{
    private readonly WingetDetectionProvider _wingetProvider;
    private readonly PortableFolderDetectionProvider _portableProvider;

    public AppDetectionService(WingetDetectionProvider wingetProvider, PortableFolderDetectionProvider portableProvider)
    {
        _wingetProvider = wingetProvider;
        _portableProvider = portableProvider;
    }

    public async Task<IReadOnlyDictionary<string, AppDetectionResult>> ScanAsync(
        IReadOnlyCollection<Recipe> recipes,
        string? portableDestination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipes);

        var results = new Dictionary<string, AppDetectionResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var recipe in recipes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = recipe.Catalog.IsPortable
                ? await _portableProvider.DetectAsync(recipe, portableDestination, cancellationToken).ConfigureAwait(false)
                : await _wingetProvider.DetectAsync(recipe, cancellationToken).ConfigureAwait(false);

            results[recipe.Id] = result;
        }

        return results;
    }
}

