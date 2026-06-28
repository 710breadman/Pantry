using System.Windows;
using DevToolsCurator.Core;

namespace DevToolsCurator.App;

public interface IDialogService
{
    bool Confirm(string title, string message);
    void ShowInfo(ToolScanResult result);
    WizardDialogResult? ShowWizard(WizardSelection currentSelection, IReadOnlyList<GoalProfile> profiles, IReadOnlyList<LanguageChoice> languages, InstallPlan currentPlan, IReadOnlyList<ToolScanResult> currentResults);
    void ShowError(string title, string message);
}

public sealed class DialogService : IDialogService
{
    private readonly Window _owner;

    public DialogService(Window owner)
    {
        _owner = owner;
    }

    public bool Confirm(string title, string message)
    {
        return MessageBox.Show(_owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public void ShowInfo(ToolScanResult result)
    {
        var dialog = new ToolInfoDialog(result) { Owner = _owner };
        dialog.ShowDialog();
    }

    public WizardDialogResult? ShowWizard(WizardSelection currentSelection, IReadOnlyList<GoalProfile> profiles, IReadOnlyList<LanguageChoice> languages, InstallPlan currentPlan, IReadOnlyList<ToolScanResult> currentResults)
    {
        var dialog = new WizardDialog(currentSelection, profiles, languages, currentPlan, currentResults) { Owner = _owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public void ShowError(string title, string message)
    {
        MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
