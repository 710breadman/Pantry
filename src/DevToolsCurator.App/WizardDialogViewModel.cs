using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DevToolsCurator.Core;

namespace DevToolsCurator.App;

public sealed class WizardDialogResult
{
    public WizardSelection Selection { get; init; } = new();
    public bool ApplyRecommended { get; init; }
}

public sealed class WizardDialogViewModel : INotifyPropertyChanged
{
    private readonly IReadOnlyList<ToolScanResult> _currentResults;
    private readonly GoalPlanner _planner = new();
    private int _stepIndex;
    private string _selectedGoalId;
    private InstallStyle _installStyle;
    private UpdateBehavior _updateBehavior;
    private bool _allowVisualStudioBuildTools;
    private bool _allowAndroidStudio;
    private bool _allowDockerDesktop;
    private bool _allowWsl2;
    private bool _allowVisualStudioCommunity;

    public WizardDialogViewModel(WizardSelection currentSelection, IReadOnlyList<GoalProfile> profiles, IReadOnlyList<LanguageChoice> languages, InstallPlan currentPlan, IReadOnlyList<ToolScanResult> currentResults)
    {
        _currentResults = currentResults;
        Profiles = new ObservableCollection<GoalProfile>(profiles);
        Languages = new ObservableCollection<LanguageChoice>(languages.Select(x => new LanguageChoice { Name = x.Name, IsSelected = currentSelection.Languages.Contains(x.Name) || x.IsSelected }));
        _selectedGoalId = currentSelection.GoalProfileId;
        _installStyle = currentSelection.InstallStyle;
        _updateBehavior = currentSelection.UpdateBehavior;
        _allowVisualStudioBuildTools = currentSelection.AllowVisualStudioBuildTools;
        _allowAndroidStudio = currentSelection.AllowAndroidStudio;
        _allowDockerDesktop = currentSelection.AllowDockerDesktop;
        _allowWsl2 = currentSelection.AllowWsl2;
        _allowVisualStudioCommunity = currentSelection.AllowVisualStudioCommunity;
        CurrentPlan = currentPlan;

        BackCommand = new RelayCommand(_ => StepIndex--, _ => StepIndex > 0);
        NextCommand = new RelayCommand(_ => StepIndex++, _ => StepIndex < 4);
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(this, null));
        FinishCommand = new RelayCommand(_ => RequestClose?.Invoke(this, new WizardDialogResult { Selection = BuildSelection(), ApplyRecommended = false }), _ => CanFinish);
        ApplyRecommendedCommand = new RelayCommand(_ => RequestClose?.Invoke(this, new WizardDialogResult { Selection = BuildSelection(), ApplyRecommended = true }), _ => CanFinish);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<WizardDialogResult?>? RequestClose;

    public ObservableCollection<GoalProfile> Profiles { get; }
    public ObservableCollection<LanguageChoice> Languages { get; }
    public ObservableCollection<InstallStyle> InstallStyles { get; } = [InstallStyle.Minimal, InstallStyle.Recommended, InstallStyle.FullPowerUser];
    public ObservableCollection<UpdateBehavior> UpdateBehaviors { get; } = [UpdateBehavior.ManualReview, UpdateBehavior.OneClickRecommended, UpdateBehavior.UnattendedAllowed];
    public ICommand BackCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand FinishCommand { get; }
    public ICommand ApplyRecommendedCommand { get; }

    public int StepIndex
    {
        get => _stepIndex;
        set
        {
            if (SetField(ref _stepIndex, Math.Clamp(value, 0, 4)))
            {
                OnPropertyChanged(nameof(StepLabel));
                OnPropertyChanged(nameof(CanFinish));
                (BackCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (NextCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (FinishCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ApplyRecommendedCommand as RelayCommand)?.RaiseCanExecuteChanged();
                RefreshPlan();
            }
        }
    }

    public string StepLabel => $"Step {StepIndex + 1} of 5";
    public bool CanFinish => StepIndex == 4;

    public string SelectedGoalId
    {
        get => _selectedGoalId;
        set
        {
            if (SetField(ref _selectedGoalId, value))
            {
                RefreshPlan();
            }
        }
    }

    public InstallStyle InstallStyle
    {
        get => _installStyle;
        set
        {
            if (SetField(ref _installStyle, value))
            {
                RefreshPlan();
            }
        }
    }

    public UpdateBehavior UpdateBehavior
    {
        get => _updateBehavior;
        set => SetField(ref _updateBehavior, value);
    }

    public bool AllowVisualStudioBuildTools
    {
        get => _allowVisualStudioBuildTools;
        set { if (SetField(ref _allowVisualStudioBuildTools, value)) RefreshPlan(); }
    }

    public bool AllowAndroidStudio
    {
        get => _allowAndroidStudio;
        set { if (SetField(ref _allowAndroidStudio, value)) RefreshPlan(); }
    }

    public bool AllowDockerDesktop
    {
        get => _allowDockerDesktop;
        set { if (SetField(ref _allowDockerDesktop, value)) RefreshPlan(); }
    }

    public bool AllowWsl2
    {
        get => _allowWsl2;
        set { if (SetField(ref _allowWsl2, value)) RefreshPlan(); }
    }

    public bool AllowVisualStudioCommunity
    {
        get => _allowVisualStudioCommunity;
        set { if (SetField(ref _allowVisualStudioCommunity, value)) RefreshPlan(); }
    }

    public InstallPlan CurrentPlan { get; private set; }

    public string RequiredTools => FormatToolIds(CurrentPlan.RequiredTools);
    public string RecommendedTools => FormatToolIds(CurrentPlan.RecommendedTools);
    public string OptionalTools => FormatToolIds(CurrentPlan.OptionalTools);
    public string InstalledTools => FormatStatuses(ToolStatus.Installed_Current, ToolStatus.Installed_NotOnPath, ToolStatus.Installed_Outdated);
    public string MissingTools => FormatStatuses(ToolStatus.Missing_Recommended, ToolStatus.Missing_Optional, ToolStatus.Broken);
    public string WarningText => $"{CurrentPlan.EstimatedDiskImpact}\nAdmin required: {(CurrentPlan.AdminRequired ? "likely" : "not expected")}\nReboot likely: {(CurrentPlan.RebootLikely ? "yes" : "no")}";

    public WizardSelection BuildSelection()
    {
        var selection = new WizardSelection
        {
            GoalProfileId = SelectedGoalId,
            InstallStyle = InstallStyle,
            UpdateBehavior = UpdateBehavior,
            AllowVisualStudioBuildTools = AllowVisualStudioBuildTools,
            AllowAndroidStudio = AllowAndroidStudio,
            AllowDockerDesktop = AllowDockerDesktop,
            AllowWsl2 = AllowWsl2,
            AllowVisualStudioCommunity = AllowVisualStudioCommunity
        };

        foreach (var language in Languages.Where(x => x.IsSelected).Select(x => x.Name))
        {
            selection.Languages.Add(language);
        }

        return selection;
    }

    public void RefreshPlan()
    {
        CurrentPlan = _planner.BuildPlan(new ToolCatalog { Tools = _currentResults.Where(x => x.Tool is not null).Select(x => x.Tool!).DistinctBy(x => x.ToolId).ToList() }, BuildSelection(), _currentResults);
        OnPropertyChanged(nameof(CurrentPlan));
        OnPropertyChanged(nameof(RequiredTools));
        OnPropertyChanged(nameof(RecommendedTools));
        OnPropertyChanged(nameof(OptionalTools));
        OnPropertyChanged(nameof(InstalledTools));
        OnPropertyChanged(nameof(MissingTools));
        OnPropertyChanged(nameof(WarningText));
    }

    private string FormatToolIds(IEnumerable<string> ids)
    {
        var names = ids.Select(id => _currentResults.FirstOrDefault(x => x.ToolId.Equals(id, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? id).ToList();
        return names.Count == 0 ? "None" : string.Join(Environment.NewLine, names.Select(x => "- " + x));
    }

    private string FormatStatuses(params ToolStatus[] statuses)
    {
        var rows = _currentResults.Where(x => statuses.Contains(x.Status) && (CurrentPlan.RequiredTools.Contains(x.ToolId) || CurrentPlan.RecommendedTools.Contains(x.ToolId) || CurrentPlan.OptionalTools.Contains(x.ToolId)))
            .Select(x => $"{x.DisplayName}: {x.StatusText}")
            .ToList();
        return rows.Count == 0 ? "None" : string.Join(Environment.NewLine, rows.Select(x => "- " + x));
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
