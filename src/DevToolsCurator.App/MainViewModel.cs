using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using DevToolsCurator.Core;

namespace DevToolsCurator.App;

public sealed class DashboardCard
{
    public string Title { get; init; } = "";
    public string Value { get; init; } = "";
    public string Detail { get; init; } = "";
}

public sealed class ToolCategoryGroup
{
    public string Name { get; init; } = "";
    public ObservableCollection<ToolScanResult> Tools { get; init; } = [];
}

public sealed class LanguageChoice
{
    public string Name { get; init; } = "";
    public bool IsSelected { get; set; }
}

public sealed class ActionResultRecord
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public string ToolId { get; init; } = "";
    public string ToolName { get; init; } = "";
    public string Action { get; init; } = "";
    public bool Success { get; init; }
    public string Message { get; init; } = "";
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly CatalogService _catalogService = new();
    private readonly GoalPlanner _goalPlanner = new();
    private readonly WingetCache _winget = new();
    private readonly ToolOperationService _operations;
    private readonly PathRepairService _pathRepair = new();
    private readonly DevKitRuntimePaths _runtimePaths = DevKitRuntimePaths.Resolve(AppContext.BaseDirectory);
    private readonly AppStateService _state = new();
    private ScanService _scanService;
    private ReportWriter? _reportWriter;
    private IDialogService? _dialogService;
    private ToolCatalog _catalog = new();
    private CatalogLoadResult? _catalogLoadResult;
    private CancellationTokenSource? _scanCts;
    private string _currentView = "Dashboard";
    private string _toolFilter = "";
    private string _toolFilterMode = "all";
    private bool _isBusy;
    private string _statusMessage = "Ready";
    private ToolScanResult? _selectedTool;
    private ScanSnapshot _snapshot = new();
    private InstallPlan _installPlan = new();

    public MainViewModel()
    {
        _operations = new ToolOperationService(winget: _winget);
        _scanService = new ScanService(winget: _winget);
        Selection.AllowVisualStudioBuildTools = true;

        NavigateCommand = new RelayCommand(p => CurrentView = p?.ToString() ?? "Dashboard", _ => !IsBusy);
        StartWizardCommand = new AsyncRelayCommand(_ => StartWizardAsync(), _ => !IsBusy && _snapshot.Tools.Count > 0);
        RescanCommand = new AsyncRelayCommand(_ => ScanAsync(force: true), _ => !IsBusy);
        InstallRecommendedCommand = new AsyncRelayCommand(_ => InstallRecommendedAsync(), _ => !IsBusy && _operations.GetInstallRecommendedTargets(_snapshot.Tools).Count > 0);
        InstallBestStackCommand = new AsyncRelayCommand(_ => InstallBestStackAsync(), _ => !IsBusy && _operations.GetBestStackTargets(_catalog, _snapshot.Tools).Count > 0);
        UpdateInstalledCommand = new AsyncRelayCommand(_ => UpdateInstalledAsync(), _ => !IsBusy && _operations.GetUpdateTargets(_snapshot.Tools).Count > 0);
        RepairIssuesCommand = new AsyncRelayCommand(_ => RepairIssuesAsync(), _ => !IsBusy && HasRepairableIssues());
        ExportSummaryCommand = new AsyncRelayCommand(_ => ExportSummaryAsync(), _ => !IsBusy);
        OpenReportFolderCommand = new RelayCommand(_ => OpenReportFolder(), _ => _reportWriter is not null && !IsBusy);
        InstallToolCommand = new AsyncRelayCommand(p => InstallToolAsync(p as ToolScanResult), p => p is ToolScanResult result && result.CanInstall && !IsBusy);
        UpdateToolCommand = new AsyncRelayCommand(p => UpdateToolAsync(p as ToolScanResult), p => p is ToolScanResult result && result.CanUpdate && !IsBusy);
        RepairToolCommand = new AsyncRelayCommand(p => RepairToolAsync(p as ToolScanResult), p => p is ToolScanResult result && result.CanRepair && !IsBusy);
        FixPathCommand = new AsyncRelayCommand(p => FixPathAsync(p as ToolScanResult), p => p is ToolScanResult result && result.CanFixPath && !IsBusy);
        OpenToolCommand = new RelayCommand(p => OpenTool(p as ToolScanResult), p => p is ToolScanResult result && result.CanOpen && !IsBusy);
        ShowInfoCommand = new RelayCommand(p => ShowToolInfo(p as ToolScanResult), p => p is ToolScanResult && !IsBusy);
        CopyDiagnosticsCommand = new RelayCommand(_ => CopyDiagnostics(), _ => _snapshot.Tools.Count > 0);
        RunUiSelfCheckCommand = new AsyncRelayCommand(_ => RunUiSelfCheckAsync(), _ => !IsBusy);

        foreach (var profile in _goalPlanner.Profiles)
        {
            GoalProfiles.Add(profile);
        }

        foreach (var language in new[] { "Python", "C#", "Java", "JavaScript/TypeScript", "C/C++", "Kotlin", "Go", "Rust", "Undecided" })
        {
            Languages.Add(new LanguageChoice { Name = language });
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DashboardCard> Cards { get; } = [];
    public ObservableCollection<ToolCategoryGroup> ToolGroups { get; } = [];
    public ObservableCollection<ToolScanResult> Updates { get; } = [];
    public ObservableCollection<ToolScanResult> Issues { get; } = [];
    public ObservableCollection<string> IssueMessages { get; } = [];
    public ObservableCollection<string> EventStream { get; } = [];
    public ObservableCollection<GoalProfile> GoalProfiles { get; } = [];
    public ObservableCollection<LanguageChoice> Languages { get; } = [];
    public ObservableCollection<string> ToolFilterModes { get; } = ["all", "missing", "outdated", "errors", "installed", "optional", "selected"];
    public ObservableCollection<InstallStyle> InstallStyles { get; } = [InstallStyle.Minimal, InstallStyle.Recommended, InstallStyle.FullPowerUser];
    public ObservableCollection<UpdateBehavior> UpdateBehaviors { get; } = [UpdateBehavior.ManualReview, UpdateBehavior.OneClickRecommended, UpdateBehavior.UnattendedAllowed];
    public WizardSelection Selection { get; } = new();

    public ICommand NavigateCommand { get; }
    public ICommand StartWizardCommand { get; }
    public ICommand RescanCommand { get; }
    public ICommand InstallRecommendedCommand { get; }
    public ICommand InstallBestStackCommand { get; }
    public ICommand UpdateInstalledCommand { get; }
    public ICommand RepairIssuesCommand { get; }
    public ICommand ExportSummaryCommand { get; }
    public ICommand OpenReportFolderCommand { get; }
    public ICommand InstallToolCommand { get; }
    public ICommand UpdateToolCommand { get; }
    public ICommand RepairToolCommand { get; }
    public ICommand FixPathCommand { get; }
    public ICommand OpenToolCommand { get; }
    public ICommand ShowInfoCommand { get; }
    public ICommand CopyDiagnosticsCommand { get; }
    public ICommand RunUiSelfCheckCommand { get; }

    public string CurrentView
    {
        get => _currentView;
        set => SetField(ref _currentView, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetField(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string ToolFilter
    {
        get => _toolFilter;
        set
        {
            if (SetField(ref _toolFilter, value))
            {
                RefreshToolGroups();
            }
        }
    }

    public string ToolFilterMode
    {
        get => _toolFilterMode;
        set
        {
            if (SetField(ref _toolFilterMode, value))
            {
                RefreshToolGroups();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public ToolScanResult? SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (SetField(ref _selectedTool, value))
            {
                OnPropertyChanged(nameof(SelectedToolDetails));
            }
        }
    }

    public string SelectedToolDetails
    {
        get
        {
            if (SelectedTool is null)
            {
                return "Select a tool for concise detection details.";
            }

            var tool = SelectedTool.Tool;
            var goals = tool is null ? "" : string.Join(", ", tool.GoalTags);
            return $"{SelectedTool.DisplayName}\n\nWhat this is: {tool?.Description}\n\nWhy it matters: {tool?.WhyItMatters}\n\nNeeded for: {goals}\n\nInstalled version/path: {SelectedTool.Version} {SelectedTool.DetectedPath}\n\nDetection: {SelectedTool.DetectionSummary}\n\nSuggested action: {SelectedTool.SuggestedAction}. {SelectedTool.Diagnostic}";
        }
    }

    public string SelectedGoalName => _goalPlanner.GetProfile(Selection.GoalProfileId).Name;
    public string PlanSummary => $"{_installPlan.RequiredTools.Count} required, {_installPlan.RecommendedTools.Count} recommended, {_installPlan.OptionalTools.Count} optional. Disk impact: {_installPlan.EstimatedDiskImpact}.";
    public string PlanActions => _installPlan.Actions.Count == 0 ? "No immediate action needed." : string.Join(Environment.NewLine, _installPlan.Actions.Take(12).Select(x => "- " + x));
    public List<ActionResultRecord> ActionResults { get; } = [];

    public void SetDialogService(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    public async Task InitializeAsync()
    {
        _runtimePaths.EnsureDirectories();
        await _runtimePaths.EnsureDefaultConfigAsync(FindDefaultConfigSource());
        _reportWriter = ReportWriter.ForReportDirectory(_runtimePaths.ReportDirectory);
        _state.ConfigureReports(_runtimePaths.ReportDirectory);

        _catalogLoadResult = await _catalogService.LoadWithFallbackAsync(_runtimePaths.CatalogOverridePath);
        _catalog = _catalogLoadResult.Catalog;
        _state.SetCatalog(_catalog);
        var startupSelfCheck = StartupSelfCheck.Run(_runtimePaths, _catalog);
        _installPlan = _goalPlanner.BuildPlan(_catalog, Selection);
        _state.SetInstallPlan(_installPlan);
        var startupEvents = new List<ToolOperationEvent>();
        if (_catalogLoadResult.UsedEmbeddedFallback)
        {
            startupEvents.Add(new ToolOperationEvent
            {
                Action = "Startup",
                ToolName = "Catalog",
                Message = $"Loaded embedded catalog fallback. Runtime mode: {_runtimePaths.ModeName}.",
                Success = true
            });
        }

        foreach (var warning in _catalogLoadResult.Warnings)
        {
            startupEvents.Add(new ToolOperationEvent
            {
                Action = "Startup",
                ToolName = "Catalog",
                Message = warning,
                Success = false
            });
        }

        foreach (var warning in startupSelfCheck.Warnings)
        {
            startupEvents.Add(new ToolOperationEvent
            {
                Action = "Startup",
                ToolName = "Self Check",
                Message = warning,
                Success = true
            });
        }

        foreach (var error in startupSelfCheck.Errors)
        {
            startupEvents.Add(new ToolOperationEvent
            {
                Action = "Startup",
                ToolName = "Self Check",
                Message = error,
                Success = false
            });
        }

        await ScanAsync(force: true);
        foreach (var evt in startupEvents)
        {
            AddEvent(evt);
        }

        await WriteActionResultsAsync();
    }

    private async Task ScanAsync(bool force)
    {
        if (IsBusy)
        {
            return;
        }

        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        IsBusy = true;
        StatusMessage = "Scanning system";
        EventStream.Clear();
        try
        {
            var progress = new Progress<string>(message => StatusMessage = message);
            _installPlan = _goalPlanner.BuildPlan(_catalog, Selection);
            _snapshot = await _scanService.ScanAsync(_catalog, Selection, progress, _scanCts.Token);
            _installPlan = _goalPlanner.BuildPlan(_catalog, Selection, _snapshot.Tools);
            _state.ApplySnapshot(_snapshot, _installPlan);
            UpdateCollections();
            NotifyDashboardBindings();
            if (_reportWriter is not null)
            {
                await _reportWriter.WriteAsync(_snapshot, _installPlan, _scanCts.Token);
                await WriteActionResultsAsync();
            }

            StatusMessage = "Scan complete";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan canceled";
        }
        finally
        {
            IsBusy = false;
            NotifyDashboardBindings();
        }
    }

    private async Task InstallRecommendedAsync()
    {
        var targets = _operations.GetInstallRecommendedTargets(_snapshot.Tools);
        await RunQueueAndRescanAsync(targets, "Installing recommended tools", install: true);
    }

    private async Task InstallBestStackAsync()
    {
        var targets = _operations.GetBestStackTargets(_catalog, _snapshot.Tools);
        await RunQueueAndRescanAsync(targets, "Installing Best Dev Stack", install: true);
    }

    private async Task UpdateInstalledAsync()
    {
        var targets = _operations.GetUpdateTargets(_snapshot.Tools);
        await RunQueueAndRescanAsync(targets, "Updating installed tools", install: false);
        IsBusy = true;
        StatusMessage = "Updating managed toolchains";
        try
        {
            await _operations.RunAggregateUpdatesAsync(new Progress<ToolOperationEvent>(AddEvent));
            await WriteActionResultsAsync();
        }
        finally
        {
            IsBusy = false;
        }

        await ScanAsync(force: true);
    }

    private async Task StartWizardAsync()
    {
        if (_dialogService is null)
        {
            StatusMessage = "Wizard unavailable: dialog service was not initialized";
            return;
        }

        var result = _dialogService.ShowWizard(Selection, GoalProfiles, Languages, _installPlan, _snapshot.Tools);
        if (result is null)
        {
            StatusMessage = "Wizard canceled";
            return;
        }

        CopySelection(result.Selection);
        _installPlan = _goalPlanner.BuildPlan(_catalog, Selection, _snapshot.Tools);
        _state.SetInstallPlan(_installPlan);
        NotifyDashboardBindings();
        UpdateCollections();
        await ExportReportsOnlyAsync();
        StatusMessage = "Wizard plan saved";

        if (result.ApplyRecommended)
        {
            await InstallRecommendedAsync();
        }
    }

    private async Task RepairIssuesAsync()
    {
        var pathTargets = _operations.GetRepairTargets(_snapshot.Tools).Where(x => x.CanFixPath).ToList();
        if (pathTargets.Count > 0)
        {
            foreach (var tool in pathTargets)
            {
                await FixPathAsync(tool);
            }
        }

        var targets = _operations.GetRepairTargets(_snapshot.Tools)
            .Where(x => x.Status == ToolStatus.Broken)
            .ToList();
        foreach (var authTool in _snapshot.Tools.Where(x => x.Status == ToolStatus.AuthNeeded))
        {
            var message = authTool.ToolId == "github-cli"
                ? "Manual action needed: run gh auth login. No tokens are stored by this app."
                : authTool.Diagnostic;
            AddActionResult(authTool, "Repair", true, message);
            AddEvent(new ToolOperationEvent { ToolId = authTool.ToolId, ToolName = authTool.DisplayName, Action = "Repair", Message = message, Success = true });
        }

        if (targets.Count == 0)
        {
            await WriteActionResultsAsync();
            await ScanAsync(force: true);
            return;
        }

        await RunQueueAndRescanAsync(targets, "Repairing broken tools", install: true);
    }

    private async Task InstallToolAsync(ToolScanResult? tool)
    {
        if (tool is null)
        {
            return;
        }

        await RunQueueAndRescanAsync([tool], $"Installing {tool.DisplayName}", install: true);
    }

    private async Task UpdateToolAsync(ToolScanResult? tool)
    {
        if (tool is null)
        {
            return;
        }

        await RunQueueAndRescanAsync([tool], $"Updating {tool.DisplayName}", install: false);
    }

    private async Task RepairToolAsync(ToolScanResult? tool)
    {
        if (tool?.Tool is null)
        {
            return;
        }

        if (tool.CanFixPath)
        {
            await FixPathAsync(tool);
            return;
        }

        if (tool.CanRepair)
        {
            await RunQueueAndRescanAsync([tool], $"Repairing {tool.DisplayName}", install: true);
        }
    }

    private async Task FixPathAsync(ToolScanResult? tool)
    {
        if (tool?.Tool is null || !tool.CanFixPath)
        {
            return;
        }

        var directory = Path.GetDirectoryName(tool.DetectedPath);
        var message = $"Add this directory to your user PATH?\n\n{directory}\n\nTool: {tool.DisplayName}\n\nThe app will avoid duplicates and validate from a fresh shell.";
        if (_dialogService?.Confirm("Fix PATH", message) != true)
        {
            AddActionResult(tool, "Fix PATH", false, "User canceled PATH repair.");
            StatusMessage = "PATH repair canceled";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Fixing PATH for {tool.DisplayName}";
        try
        {
            var result = await _pathRepair.FixUserPathAsync(PathRepairService.FromTool(tool));
            AddActionResult(tool, "Fix PATH", result.Success, result.Message);
            AddEvent(new ToolOperationEvent { ToolId = tool.ToolId, ToolName = tool.DisplayName, Action = "Fix PATH", Message = result.Message, Success = result.Success });
            await WriteActionResultsAsync();
        }
        catch (Exception ex)
        {
            AddActionResult(tool, "Fix PATH", false, ex.Message);
            _dialogService?.ShowError("Fix PATH failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        await ScanAsync(force: true);
    }

    private void OpenTool(ToolScanResult? tool)
    {
        if (tool is null || !tool.CanOpen)
        {
            return;
        }

        var target = Directory.Exists(tool.DetectedPath) ? tool.DetectedPath : Path.GetDirectoryName(tool.DetectedPath);
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
        AddActionResult(tool, "Open", true, $"Opened {target}");
    }

    private void ShowToolInfo(ToolScanResult? tool)
    {
        if (tool is null)
        {
            return;
        }

        _dialogService?.ShowInfo(tool);
    }

    private async Task RunQueueAndRescanAsync(IReadOnlyList<ToolScanResult> targets, string message, bool install)
    {
        if (targets.Count == 0)
        {
            StatusMessage = "Nothing to do";
            return;
        }

        IsBusy = true;
        StatusMessage = message;
        EventStream.Clear();
        try
        {
            var progress = new Progress<ToolOperationEvent>(AddEvent);
            if (install)
            {
                await _operations.RunInstallQueueAsync(targets, progress);
            }
            else
            {
                await _operations.RunUpdateQueueAsync(targets, progress);
            }
            await WriteActionResultsAsync();
        }
        catch (Exception ex)
        {
            var messageText = $"{message} failed: {ex.Message}";
            StatusMessage = messageText;
            AddEvent(new ToolOperationEvent { Action = install ? "Install" : "Update", ToolName = "Queue", Message = messageText, Success = false });
            _dialogService?.ShowError("Action failed", messageText);
        }
        finally
        {
            IsBusy = false;
        }

        await ScanAsync(force: true);
    }

    private async Task ExportSummaryAsync()
    {
        await ExportReportsOnlyAsync();
        StatusMessage = "Summary exported";
        OpenReportFolder();
    }

    private async Task ExportReportsOnlyAsync()
    {
        if (_reportWriter is null)
        {
            return;
        }

        await _reportWriter.WriteAsync(_snapshot, _installPlan);
        await WriteActionResultsAsync();
    }

    private async Task RunUiSelfCheckAsync()
    {
        if (_reportWriter is null)
        {
            return;
        }

        var audit = UiSelfCheckService.Run(_snapshot.Tools);
        var path = Path.Combine(_reportWriter.ReportDirectory, "ui_audit.json");
        Directory.CreateDirectory(_reportWriter.ReportDirectory);
        await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(audit, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        StatusMessage = audit.CriticalIssueCount == 0 ? "UI self-check passed" : $"UI self-check found {audit.CriticalIssueCount} critical issues";
        AddEvent(new ToolOperationEvent { Action = "UI Self Check", ToolName = "Controls", Message = StatusMessage, Success = audit.CriticalIssueCount == 0 });
        await WriteActionResultsAsync();
    }

    private void OpenReportFolder()
    {
        if (_reportWriter is null)
        {
            return;
        }

        Directory.CreateDirectory(_reportWriter.ReportDirectory);
        Process.Start(new ProcessStartInfo { FileName = _reportWriter.ReportDirectory, UseShellExecute = true });
    }

    private void CopyDiagnostics()
    {
        var text = string.Join(Environment.NewLine, _snapshot.Tools
            .Where(x => x.Status is ToolStatus.Broken or ToolStatus.Installed_NotOnPath or ToolStatus.Missing_Recommended or ToolStatus.AuthNeeded)
            .Select(x => $"{x.DisplayName}: {x.StatusText} - {x.Diagnostic}"));
        Clipboard.SetText(string.IsNullOrWhiteSpace(text) ? "No diagnostics." : text);
        StatusMessage = "Diagnostics copied";
    }

    private void AddEvent(ToolOperationEvent evt)
    {
        EventStream.Insert(0, $"{DateTime.Now:HH:mm:ss} {evt.Action} {evt.ToolName}: {evt.Message}");
        _state.AddOperation($"{evt.Action} {evt.ToolName}: {evt.Message}");
        ActionResults.Add(new ActionResultRecord
        {
            ToolId = evt.ToolId,
            ToolName = evt.ToolName,
            Action = evt.Action,
            Success = evt.Success,
            Message = evt.Message
        });
        while (EventStream.Count > 80)
        {
            EventStream.RemoveAt(EventStream.Count - 1);
        }
    }

    private void AddActionResult(ToolScanResult tool, string action, bool success, string message)
    {
        ActionResults.Add(new ActionResultRecord
        {
            ToolId = tool.ToolId,
            ToolName = tool.DisplayName,
            Action = action,
            Success = success,
            Message = message
        });
    }

    private async Task WriteActionResultsAsync()
    {
        if (_reportWriter is null)
        {
            return;
        }

        Directory.CreateDirectory(_reportWriter.ReportDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(_reportWriter.ReportDirectory, "action_results.json"),
            System.Text.Json.JsonSerializer.Serialize(ActionResults, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private void UpdateCollections()
    {
        var selectedToolId = SelectedTool?.ToolId;
        Cards.Clear();
        Cards.Add(new DashboardCard { Title = "Readiness", Value = _snapshot.Summary.ReadinessScore + "%", Detail = _snapshot.Summary.RecommendedNextAction });
        Cards.Add(new DashboardCard { Title = "Goal Profile", Value = SelectedGoalName, Detail = PlanSummary });
        Cards.Add(new DashboardCard { Title = "Critical Missing", Value = _snapshot.Summary.MissingCritical.ToString(), Detail = "Required or recommended for this goal" });
        Cards.Add(new DashboardCard { Title = "Updates Available", Value = _snapshot.Summary.Outdated.ToString(), Detail = "Detected through winget upgrade when available" });
        Cards.Add(new DashboardCard { Title = "Issues To Fix", Value = _snapshot.Summary.BrokenOrPathIssues.ToString(), Detail = "Broken, PATH, or reboot-needed items" });
        Cards.Add(new DashboardCard { Title = "Recommended Next Action", Value = ShortNextAction(_snapshot.Summary.RecommendedNextAction), Detail = _snapshot.Summary.LastScanTime.ToLocalTime().ToString("g") });

        RefreshToolGroups();

        Updates.Clear();
        foreach (var tool in _snapshot.Tools.Where(x => x.Status == ToolStatus.Installed_Outdated))
        {
            Updates.Add(tool);
        }

        Issues.Clear();
        foreach (var tool in _snapshot.Tools.Where(x => x.Status is ToolStatus.Broken or ToolStatus.Installed_NotOnPath or ToolStatus.AuthNeeded or ToolStatus.RebootNeeded or ToolStatus.Missing_Recommended))
        {
            Issues.Add(tool);
        }

        IssueMessages.Clear();
        foreach (var issue in _snapshot.Issues)
        {
            IssueMessages.Add(issue);
        }

        SelectedTool = !string.IsNullOrWhiteSpace(selectedToolId)
            ? _snapshot.Tools.FirstOrDefault(x => x.ToolId.Equals(selectedToolId, StringComparison.OrdinalIgnoreCase)) ?? _snapshot.Tools.FirstOrDefault()
            : _snapshot.Tools.FirstOrDefault();
        OnPropertyChanged(nameof(Cards));
        OnPropertyChanged(nameof(ToolGroups));
        OnPropertyChanged(nameof(Updates));
        OnPropertyChanged(nameof(Issues));
        OnPropertyChanged(nameof(IssueMessages));
        RaiseCommandStates();
    }

    private void RefreshToolGroups()
    {
        ToolGroups.Clear();
        var query = _snapshot.Tools.Where(x => !x.ToolId.StartsWith("windows-reboot", StringComparison.OrdinalIgnoreCase));
        query = ToolFilterMode switch
        {
            "missing" => query.Where(x => x.Status is ToolStatus.Missing_Recommended or ToolStatus.Missing_Optional),
            "outdated" => query.Where(x => x.Status == ToolStatus.Installed_Outdated),
            "errors" => query.Where(x => x.Status is ToolStatus.Broken or ToolStatus.Installed_NotOnPath or ToolStatus.AuthNeeded or ToolStatus.RebootNeeded),
            "installed" => query.Where(x => x.Status is ToolStatus.Installed_Current or ToolStatus.Installed_Outdated or ToolStatus.Installed_NotOnPath),
            "optional" => query.Where(x => x.IsOptional),
            "selected" => query.Where(x => x.IsRecommendedForGoal || x.IsRequiredForGoal),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(ToolFilter))
        {
            var filter = ToolFilter.Trim();
            query = query.Where(x =>
                x.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                x.Category.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                x.StatusText.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (x.Tool?.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        foreach (var group in query.GroupBy(x => x.Category))
        {
            ToolGroups.Add(new ToolCategoryGroup
            {
                Name = $"{group.Key} ({group.Count()})",
                Tools = new ObservableCollection<ToolScanResult>(group)
            });
        }
    }

    private static string ShortNextAction(string value)
    {
        return value.Length <= 26 ? value : value[..26] + "...";
    }

    private void NotifyDashboardBindings()
    {
        OnPropertyChanged(nameof(SelectedGoalName));
        OnPropertyChanged(nameof(PlanSummary));
        OnPropertyChanged(nameof(PlanActions));
        OnPropertyChanged(nameof(SelectedToolDetails));
    }

    private static string? FindDefaultConfigSource()
    {
        var baseDefault = Path.Combine(AppContext.BaseDirectory, "config.default.json");
        if (File.Exists(baseDefault))
        {
            return baseDefault;
        }

        if (CatalogService.TryFindProjectRoot(AppContext.BaseDirectory, out var root))
        {
            var repoConfig = Path.Combine(root, "config.json");
            if (File.Exists(repoConfig))
            {
                return repoConfig;
            }
        }

        return null;
    }

    private bool HasRepairableIssues()
    {
        return _snapshot.Tools.Any(x => x.CanFixPath || x.CanRepair || x.Status == ToolStatus.AuthNeeded);
    }

    private void CopySelection(WizardSelection selection)
    {
        Selection.GoalProfileId = selection.GoalProfileId;
        Selection.InstallStyle = selection.InstallStyle;
        Selection.UpdateBehavior = selection.UpdateBehavior;
        Selection.AllowVisualStudioBuildTools = selection.AllowVisualStudioBuildTools;
        Selection.AllowAndroidStudio = selection.AllowAndroidStudio;
        Selection.AllowDockerDesktop = selection.AllowDockerDesktop;
        Selection.AllowWsl2 = selection.AllowWsl2;
        Selection.AllowVisualStudioCommunity = selection.AllowVisualStudioCommunity;
        Selection.Languages.Clear();
        foreach (var language in selection.Languages)
        {
            Selection.Languages.Add(language);
        }

        foreach (var choice in Languages)
        {
            choice.IsSelected = Selection.Languages.Contains(choice.Name);
        }
    }

    private void RaiseCommandStates()
    {
        foreach (var command in new ICommand[]
        {
            NavigateCommand,
            StartWizardCommand,
            RescanCommand,
            InstallRecommendedCommand,
            InstallBestStackCommand,
            UpdateInstalledCommand,
            RepairIssuesCommand,
            ExportSummaryCommand,
            OpenReportFolderCommand,
            InstallToolCommand,
            UpdateToolCommand,
            RepairToolCommand,
            FixPathCommand,
            OpenToolCommand,
            ShowInfoCommand,
            CopyDiagnosticsCommand,
            RunUiSelfCheckCommand
        })
        {
            switch (command)
            {
                case RelayCommand relay:
                    relay.RaiseCanExecuteChanged();
                    break;
                case AsyncRelayCommand asyncRelay:
                    asyncRelay.RaiseCanExecuteChanged();
                    break;
            }
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
