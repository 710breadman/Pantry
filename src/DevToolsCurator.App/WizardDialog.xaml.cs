using System.Windows;
using DevToolsCurator.Core;

namespace DevToolsCurator.App;

public partial class WizardDialog : Window
{
    private readonly WizardDialogViewModel _viewModel;

    public WizardDialog(WizardSelection currentSelection, IReadOnlyList<GoalProfile> profiles, IReadOnlyList<LanguageChoice> languages, InstallPlan currentPlan, IReadOnlyList<ToolScanResult> currentResults)
    {
        InitializeComponent();
        _viewModel = new WizardDialogViewModel(currentSelection, profiles, languages, currentPlan, currentResults);
        _viewModel.RequestClose += ViewModel_RequestClose;
        DataContext = _viewModel;
    }

    public WizardDialogResult? Result { get; private set; }

    private void ViewModel_RequestClose(object? sender, WizardDialogResult? result)
    {
        Result = result;
        DialogResult = result is not null;
        Close();
    }
}
