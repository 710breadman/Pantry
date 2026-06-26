using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Pantry.Catalog;
using Pantry.Core;
using Pantry.Detection;
using Pantry.Domain;
using Pantry.Infrastructure;

namespace Pantry.UI.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly BundledCatalogLoader _catalogLoader;
    private readonly AppDetectionService _detectionService;
    private readonly PantryRunModeDetection _runMode;
    private readonly OperationLogStore _operationLogStore;
    private readonly DryRunPlanner _planner;
    private readonly PantryDatabase _database;
    private readonly AppSelectionStore _appSelectionStore;
    private readonly ScanResultStore _scanResultStore;
    private readonly ReviewSessionStore _reviewSessionStore;
    private readonly UserSettingsStore _userSettingsStore;
    private CatalogSnapshot? _catalog;
    private Profile? _selectedProfile;
    private bool _loadingProfile;
    private IReadOnlyDictionary<string, AppDetectionResult> _detectionResults =
        new Dictionary<string, AppDetectionResult>(StringComparer.OrdinalIgnoreCase);
    private string _status = "Loading bundled catalog...";
    private string _catalogSummary = "Catalog: loading";
    private string _selectionSummary = "Selected: 0";
    private string _planSummary = "Plan: pending";
    private string _detectionSummary = "Detection: not scanned";
    private string _modeSummary;
    private string _reviewSessionSummary = "Reviews: 0 saved";
    private string _portableDestination = @"PantryTools";

    public MainViewModel(
        BundledCatalogLoader catalogLoader,
        AppDetectionService detectionService,
        PantryRunModeDetection runMode,
        PantryDatabase database,
        AppSelectionStore appSelectionStore,
        OperationLogStore operationLogStore,
        ScanResultStore scanResultStore,
        ReviewSessionStore reviewSessionStore,
        UserSettingsStore userSettingsStore,
        DryRunPlanner planner)
    {
        _catalogLoader = catalogLoader;
        _detectionService = detectionService;
        _runMode = runMode;
        _database = database;
        _appSelectionStore = appSelectionStore;
        _operationLogStore = operationLogStore;
        _scanResultStore = scanResultStore;
        _reviewSessionStore = reviewSessionStore;
        _userSettingsStore = userSettingsStore;
        _planner = planner;
        _modeSummary = FormatRunMode(runMode);
    }

    public ObservableCollection<Profile> Profiles { get; } = [];

    public ObservableCollection<AppSelectionViewModel> Apps { get; } = [];

    public ObservableCollection<DryRunItemViewModel> ReviewItems { get; } = [];

    public ObservableCollection<OperationLogEntryViewModel> RecentLogs { get; } = [];

    public Profile? SelectedProfile
    {
        get => _selectedProfile;
        private set => SetProperty(ref _selectedProfile, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string CatalogSummary
    {
        get => _catalogSummary;
        private set => SetProperty(ref _catalogSummary, value);
    }

    public string SelectionSummary
    {
        get => _selectionSummary;
        private set => SetProperty(ref _selectionSummary, value);
    }

    public string PlanSummary
    {
        get => _planSummary;
        private set => SetProperty(ref _planSummary, value);
    }

    public string DetectionSummary
    {
        get => _detectionSummary;
        private set => SetProperty(ref _detectionSummary, value);
    }

    public string ModeSummary
    {
        get => _modeSummary;
        private set => SetProperty(ref _modeSummary, value);
    }

    public string ReviewSessionSummary
    {
        get => _reviewSessionSummary;
        private set => SetProperty(ref _reviewSessionSummary, value);
    }

    public string PortableDestination
    {
        get => _portableDestination;
        set
        {
            if (SetProperty(ref _portableDestination, value))
            {
                _ = SavePortableDestinationAndRefreshAsync(value);
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _database.InitializeAsync(cancellationToken).ConfigureAwait(true);

        _catalog = await _catalogLoader
            .LoadAsync(CatalogPathProvider.BundledCatalogRoot(), cancellationToken)
            .ConfigureAwait(true);
        CatalogSummary = $"Catalog {_catalog.CatalogVersion}: {_catalog.Recipes.Count} Recipes";
        ModeSummary = FormatRunMode(_runMode);

        _detectionResults = await _scanResultStore.LoadAsync(cancellationToken).ConfigureAwait(true);
        UpdateDetectionSummary(_detectionResults);
        await UpdateReviewSessionSummaryAsync(cancellationToken).ConfigureAwait(true);
        var settings = await _userSettingsStore.LoadAsync(cancellationToken).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(settings.PortableDestination))
        {
            _portableDestination = settings.PortableDestination;
            OnPropertyChanged(nameof(PortableDestination));
        }

        Profiles.Clear();
        foreach (var profile in _catalog.Profiles)
        {
            Profiles.Add(profile);
        }

        await _operationLogStore
            .AppendAsync("catalog", $"Loaded {_catalog.Recipes.Count} bundled Recipe(s).", cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        await _operationLogStore
            .AppendAsync("startup", $"Run mode: {_runMode.Mode}. {_runMode.Reason}", cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        var selectedProfile = Profiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, settings.SelectedProfileId, StringComparison.OrdinalIgnoreCase))
            ?? Profiles.FirstOrDefault();

        await SelectProfileAsync(selectedProfile, persistChoice: false, cancellationToken).ConfigureAwait(true);
        await RefreshLogsAsync(cancellationToken).ConfigureAwait(true);
    }

    public Task SelectProfileAsync(Profile? profile, CancellationToken cancellationToken = default)
    {
        return SelectProfileAsync(profile, persistChoice: true, cancellationToken);
    }

    private async Task SelectProfileAsync(
        Profile? profile,
        bool persistChoice,
        CancellationToken cancellationToken = default)
    {
        if (_catalog is null || profile is null)
        {
            return;
        }

        SelectedProfile = profile;
        if (persistChoice)
        {
            await _userSettingsStore.SaveSelectedProfileIdAsync(profile.Id, cancellationToken).ConfigureAwait(true);
        }

        var savedSelections = await _appSelectionStore.LoadAsync(profile.Id, cancellationToken).ConfigureAwait(true);
        var selectedIds = profile.Selections
            .Where(selection => selection.Preselected)
            .Select(selection => selection.AppId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Apps.Clear();
        _loadingProfile = true;
        foreach (var recipe in _catalog.Recipes.OrderBy(recipe => recipe.Catalog.Name, StringComparer.OrdinalIgnoreCase))
        {
            var isSelected = savedSelections.TryGetValue(recipe.Id, out var saved)
                ? saved
                : selectedIds.Contains(recipe.Id);

            var app = new AppSelectionViewModel(recipe, isSelected);
            app.PropertyChanged += async (_, args) =>
            {
                if (args.PropertyName == nameof(AppSelectionViewModel.IsSelected))
                {
                    await SaveSelectionAndRefreshAsync(app, cancellationToken).ConfigureAwait(true);
                }
            };

            Apps.Add(app);
        }

        _loadingProfile = false;
        await RefreshPlanAsync(cancellationToken).ConfigureAwait(true);
    }

    public async Task RefreshPlanAsync(CancellationToken cancellationToken = default)
    {
        if (_catalog is null || SelectedProfile is null)
        {
            return;
        }

        var overrides = Apps.ToDictionary(
            app => app.AppId,
            app => app.IsSelected ? AppSelectionState.Selected : AppSelectionState.NotSelected,
            StringComparer.OrdinalIgnoreCase);

        var plan = await _planner.CreatePlanAsync(new DryRunPlanRequest
        {
            Catalog = _catalog,
            Profile = SelectedProfile,
            SelectionOverrides = overrides,
            DetectionResults = _detectionResults,
            PortableDestination = string.IsNullOrWhiteSpace(PortableDestination) ? null : PortableDestination
        }, cancellationToken).ConfigureAwait(true);

        ReviewItems.Clear();
        foreach (var item in plan.Items)
        {
            ReviewItems.Add(new DryRunItemViewModel(item));
        }

        var selectedCount = plan.Items.Count(item => item.Intent is DryRunIntent.Install or DryRunIntent.Update);
        var installCount = plan.Items.Count(item => item.Intent == DryRunIntent.Install);
        var updateCount = plan.Items.Count(item => item.Intent == DryRunIntent.Update);
        var skipCount = plan.Items.Count(item => item.Intent == DryRunIntent.Skip);
        await _reviewSessionStore
            .SaveAsync(plan, _catalog.CatalogVersion, cancellationToken)
            .ConfigureAwait(true);
        await UpdateReviewSessionSummaryAsync(cancellationToken).ConfigureAwait(true);

        SelectionSummary = $"Selected: {selectedCount}";
        PlanSummary = $"Plan: {installCount} install, {updateCount} update, {skipCount} skip";
        Status = $"Loaded {_catalog.Recipes.Count} Recipes from catalog {_catalog.CatalogVersion}. {selectedCount} item(s) selected for dry-run review.";
    }

    public async Task ScanInstalledAppsAsync(CancellationToken cancellationToken = default)
    {
        if (_catalog is null)
        {
            return;
        }

        Status = "Scanning installed apps with read-only checks...";
        _detectionResults = await _detectionService
            .ScanAsync(_catalog.Recipes, PortableDestination, cancellationToken)
            .ConfigureAwait(true);

        await _scanResultStore.SaveAsync(_detectionResults.Values, cancellationToken).ConfigureAwait(true);

        await RefreshPlanAsync(cancellationToken).ConfigureAwait(true);

        var knownCount = _detectionResults.Count(result => result.Value.State != DetectedAppState.Unknown);
        UpdateDetectionSummary(_detectionResults);
        await _operationLogStore
            .AppendAsync("detection", $"Read-only scan complete. Known states: {knownCount}/{_detectionResults.Count}.", cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        await RefreshLogsAsync(cancellationToken).ConfigureAwait(true);

        Status = $"Read-only scan complete. {knownCount} of {_detectionResults.Count} app(s) returned known detection state.";
    }

    public async Task RefreshLogsAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _operationLogStore.ListRecentAsync(8, cancellationToken).ConfigureAwait(true);
        RecentLogs.Clear();
        foreach (var entry in entries)
        {
            RecentLogs.Add(new OperationLogEntryViewModel(entry));
        }
    }

    private async Task SaveSelectionAndRefreshAsync(
        AppSelectionViewModel app,
        CancellationToken cancellationToken = default)
    {
        if (_loadingProfile || SelectedProfile is null)
        {
            return;
        }

        await _appSelectionStore
            .SaveAsync(SelectedProfile.Id, app.AppId, app.IsSelected, cancellationToken)
            .ConfigureAwait(true);

        await RefreshPlanAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task SavePortableDestinationAndRefreshAsync(
        string value,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            await _userSettingsStore.SavePortableDestinationAsync(value, cancellationToken).ConfigureAwait(true);
        }

        await RefreshPlanAsync(cancellationToken).ConfigureAwait(true);
    }

    private void UpdateDetectionSummary(IReadOnlyDictionary<string, AppDetectionResult> detectionResults)
    {
        if (detectionResults.Count == 0)
        {
            DetectionSummary = "Detection: not scanned";
            return;
        }

        var known = detectionResults.Count(result => result.Value.State != DetectedAppState.Unknown);
        var current = detectionResults.Count(result => result.Value.State == DetectedAppState.InstalledCurrent);
        var updates = detectionResults.Count(result => result.Value.State == DetectedAppState.UpdateAvailable);
        DetectionSummary = $"Detection: {known}/{detectionResults.Count} known, {current} current, {updates} updates";
    }

    private static string FormatRunMode(PantryRunModeDetection runMode)
    {
        return $"Mode: {runMode.Mode} | State: {runMode.StateDirectory}";
    }

    private async Task UpdateReviewSessionSummaryAsync(CancellationToken cancellationToken)
    {
        var reviewCount = await _reviewSessionStore.CountAsync(cancellationToken).ConfigureAwait(true);
        ReviewSessionSummary = $"Reviews: {reviewCount} saved";
    }
}
