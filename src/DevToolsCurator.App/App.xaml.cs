using System.Windows;
using System.IO;
using DevToolsCurator.Core;

namespace DevToolsCurator.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AsyncRelayCommand.ExecutionFailed += HandleCommandFailure;
        var runtimePaths = DevKitRuntimePaths.Resolve(AppContext.BaseDirectory);

        if (e.Args.Any(x => x.Equals("--smoke-test", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                runtimePaths.EnsureDirectories();
                await runtimePaths.EnsureDefaultConfigAsync(FindDefaultConfigSource(runtimePaths.AppBaseDirectory));
                var catalogResult = await new CatalogService().LoadWithFallbackAsync(runtimePaths.CatalogOverridePath);
                var startup = StartupSelfCheck.Run(runtimePaths, catalogResult.Catalog);
                if (startup.IsCriticalFailure)
                {
                    Shutdown(2);
                    return;
                }

                Shutdown(0);
            }
            catch
            {
                Shutdown(1);
            }

            return;
        }

        if (e.Args.Any(x => x.Equals("--ui-self-check", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                runtimePaths.EnsureDirectories();
                var catalogResult = await new CatalogService().LoadWithFallbackAsync(runtimePaths.CatalogOverridePath);
                var catalog = catalogResult.Catalog;
                var syntheticResults = catalog.Tools.Select(tool => new ToolScanResult
                {
                    ToolId = tool.ToolId,
                    DisplayName = tool.DisplayName,
                    Category = tool.Category,
                    Status = ToolStatus.Unknown,
                    Tool = tool
                }).ToList();
                var report = UiSelfCheckService.Run(syntheticResults);
                var reportDir = runtimePaths.ReportDirectory;
                Directory.CreateDirectory(reportDir);
                await File.WriteAllTextAsync(Path.Combine(reportDir, "ui_audit.json"), System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                Shutdown(report.CriticalIssueCount == 0 ? 0 : 3);
            }
            catch
            {
                Shutdown(1);
            }

            return;
        }

        if (e.Args.Any(x => x.Equals("--contract-self-check", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                runtimePaths.EnsureDirectories();
                await runtimePaths.EnsureDefaultConfigAsync(FindDefaultConfigSource(runtimePaths.AppBaseDirectory));
                var catalogResult = await new CatalogService().LoadWithFallbackAsync(runtimePaths.CatalogOverridePath);
                var report = DevKitContractSelfCheck.Run(runtimePaths, catalogResult.Catalog);
                Directory.CreateDirectory(runtimePaths.ReportDirectory);
                await File.WriteAllTextAsync(
                    Path.Combine(runtimePaths.ReportDirectory, "recipe_card_contract_self_check.json"),
                    System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                Shutdown(report.Passed ? 0 : 4);
            }
            catch
            {
                Shutdown(1);
            }

            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private static void HandleCommandFailure(Exception exception)
    {
        System.Diagnostics.Trace.TraceError(exception.ToString());
        MessageBox.Show(
            "The operation failed. No further actions were started.\n\n" + exception.Message,
            "Recipe Card operation failed",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static string? FindDefaultConfigSource(string appBaseDirectory)
    {
        var baseDefault = Path.Combine(appBaseDirectory, "config.default.json");
        if (File.Exists(baseDefault))
        {
            return baseDefault;
        }

        if (CatalogService.TryFindProjectRoot(appBaseDirectory, out var root))
        {
            var repoConfig = Path.Combine(root, "config.json");
            if (File.Exists(repoConfig))
            {
                return repoConfig;
            }
        }

        return null;
    }
}
