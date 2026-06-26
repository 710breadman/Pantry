using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Pantry.Catalog;
using Pantry.Core;
using Pantry.Detection;
using Pantry.Domain;

namespace Pantry.UI.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly BundledCatalogLoader _catalogLoader;
    private readonly AppDetectionService _detectionService;
    private readonly DryRunPlanner _planner;
    private CatalogSnapshot? _catalog;
    private Profile? _selectedProfile;
    private IReadOnlyDictionary<string, AppDetectionResult> _detectionResults =
        new Dictionary<string, AppDetectionResult>(StringComparer.OrdinalIgnoreCase);
    private string _status = "Loading bundled catalog...";
    private string _portableDestination = @"PantryTools";

    public MainViewModel(BundledCatalogLoader catalogLoader, AppDetectionService detectionService, DryRunPlanner planner)
    {
        _catalogLoader = catalogLoader;
        _detectionService = detectionService;
        _planner = planner;
    }

    public ObservableCollection<Profile> Profiles { get; } = [];

    public ObservableCollection<AppSelectionViewModel> Apps { get; } = [];

    public ObservableCollection<DryRunItemViewModel> ReviewItems { get; } = [];

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

    public string PortableDestination
    {
        get => _portableDestination;
        set
        {
            if (SetProperty(ref _portableDestination, value))
            {
                _ = RefreshPlanAsync();
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _catalog = await _catalogLoader
            .LoadAsync(CatalogPathProvider.BundledCatalogRoot(), cancellationToken)
            .ConfigureAwait(true);

        Profiles.Clear();
        foreach (var profile in _catalog.Profiles)
        {
            Profiles.Add(profile);
        }

        await SelectProfileAsync(Profiles.FirstOrDefault(), cancellationToken).ConfigureAwait(true);
    }

    public async Task SelectProfileAsync(Profile? profile, CancellationToken cancellationToken = default)
    {
        if (_catalog is null || profile is null)
        {
            return;
        }

        SelectedProfile = profile;
        var selectedIds = profile.Selections
            .Where(selection => selection.Preselected)
            .Select(selection => selection.AppId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Apps.Clear();
        foreach (var recipe in _catalog.Recipes.OrderBy(recipe => recipe.Catalog.Name, StringComparer.OrdinalIgnoreCase))
        {
            var app = new AppSelectionViewModel(recipe, selectedIds.Contains(recipe.Id));
            app.PropertyChanged += async (_, args) =>
            {
                if (args.PropertyName == nameof(AppSelectionViewModel.IsSelected))
                {
                    await RefreshPlanAsync(cancellationToken).ConfigureAwait(true);
                }
            };

            Apps.Add(app);
        }

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

        await RefreshPlanAsync(cancellationToken).ConfigureAwait(true);

        var knownCount = _detectionResults.Count(result => result.Value.State != DetectedAppState.Unknown);
        Status = $"Read-only scan complete. {knownCount} of {_detectionResults.Count} app(s) returned known detection state.";
    }
}
