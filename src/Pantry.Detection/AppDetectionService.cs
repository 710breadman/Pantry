using Pantry.Domain;

namespace Pantry.Detection;

public sealed class AppDetectionService
{
    private readonly WingetDetectionProvider _wingetProvider;
    private readonly PortableFolderDetectionProvider _portableProvider;
    private readonly RegistryDetectionProvider _registryProvider;

    public AppDetectionService(
        WingetDetectionProvider wingetProvider,
        PortableFolderDetectionProvider portableProvider,
        RegistryDetectionProvider registryProvider)
    {
        _wingetProvider = wingetProvider;
        _portableProvider = portableProvider;
        _registryProvider = registryProvider;
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
                : await DetectInstalledAppAsync(recipe, cancellationToken).ConfigureAwait(false);

            results[recipe.Id] = result;
        }

        return results;
    }

    private async Task<AppDetectionResult> DetectInstalledAppAsync(
        Recipe recipe,
        CancellationToken cancellationToken)
    {
        var wingetResult = await _wingetProvider.DetectAsync(recipe, cancellationToken).ConfigureAwait(false);
        if (wingetResult.State is DetectedAppState.InstalledCurrent or DetectedAppState.UpdateAvailable)
        {
            return wingetResult;
        }

        var registryResult = await _registryProvider.DetectAsync(recipe, cancellationToken).ConfigureAwait(false);
        if (registryResult.State is DetectedAppState.InstalledCurrent or DetectedAppState.UpdateAvailable)
        {
            return registryResult;
        }

        return wingetResult.State == DetectedAppState.Unknown ? wingetResult : registryResult;
    }
}
