using Microsoft.Extensions.DependencyInjection;
using Pantry.Catalog;
using Pantry.Core;
using Pantry.Detection;
using Pantry.Infrastructure;
using Pantry.UI.ViewModels;

namespace Pantry.UI;

public static class PantryServiceProvider
{
    public static ServiceProvider Build()
    {
        var runMode = new PantryRunModeDetector(new WindowsAppRuntimeEnvironment()).Detect();
        return Build(runMode);
    }

    public static ServiceProvider Build(Pantry.Domain.PantryRunModeDetection runMode)
    {
        var services = new ServiceCollection();

        services.AddSingleton(runMode);
        services.AddSingleton(new PantryDatabase(PantryDataPaths.DefaultDatabasePath(runMode)));

        services.AddSingleton<RecipeValidator>();
        services.AddSingleton<BundledCatalogLoader>();
        services.AddSingleton<DryRunPlanner>();

        services.AddSingleton<IProcessRunner, WindowsProcessRunner>();
        services.AddSingleton<WingetDetectionProvider>();
        services.AddSingleton<PortableFolderDetectionProvider>();
        services.AddSingleton<IRegistryReader, WindowsRegistryReader>();
        services.AddSingleton<RegistryDetectionProvider>();
        services.AddSingleton<IFileSystemReader, WindowsFileSystemReader>();
        services.AddSingleton<FileDetectionProvider>();
        services.AddSingleton<AppDetectionService>();

        services.AddSingleton<AppSelectionStore>();
        services.AddSingleton<OperationLogStore>();
        services.AddSingleton<ScanResultStore>();
        services.AddSingleton<UserSettingsStore>();

        services.AddTransient<MainViewModel>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
