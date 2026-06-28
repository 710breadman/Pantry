namespace DevToolsCurator.Core;

public sealed class AppStateService
{
    public ToolCatalog ToolCatalog { get; private set; } = new();
    public ScanSnapshot LastScan { get; private set; } = new();
    public InstallPlan InstallPlan { get; private set; } = new();
    public List<string> ActiveOperations { get; } = [];
    public string ReportDirectory { get; private set; } = "";
    public DateTimeOffset LastUpdated { get; private set; } = DateTimeOffset.Now;

    public IReadOnlyList<ToolScanResult> DetectedTools => LastScan.Tools;
    public IReadOnlyList<string> Issues => LastScan.Issues;

    public event EventHandler? StateChanged;

    public void ConfigureReports(string reportDirectory)
    {
        ReportDirectory = reportDirectory;
        Touch();
    }

    public void SetCatalog(ToolCatalog catalog)
    {
        ToolCatalog = catalog;
        Touch();
    }

    public void SetInstallPlan(InstallPlan plan)
    {
        InstallPlan = plan;
        Touch();
    }

    public void ApplySnapshot(ScanSnapshot snapshot, InstallPlan plan)
    {
        LastScan = snapshot;
        InstallPlan = plan;
        Touch();
    }

    public void AddOperation(string operation)
    {
        if (!string.IsNullOrWhiteSpace(operation))
        {
            ActiveOperations.Add(operation);
            Touch();
        }
    }

    public void ClearOperations()
    {
        ActiveOperations.Clear();
        Touch();
    }

    private void Touch()
    {
        LastUpdated = DateTimeOffset.Now;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
