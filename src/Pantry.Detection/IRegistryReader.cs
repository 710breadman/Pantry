namespace Pantry.Detection;

public interface IRegistryReader
{
    Task<IReadOnlyList<RegistryAppEntry>> ReadInstalledAppsAsync(CancellationToken cancellationToken = default);
}

