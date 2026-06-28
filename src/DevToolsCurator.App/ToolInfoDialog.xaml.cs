using System.Windows;
using DevToolsCurator.Core;

namespace DevToolsCurator.App;

public partial class ToolInfoDialog : Window
{
    public ToolInfoDialog(ToolScanResult result)
    {
        InitializeComponent();
        DataContext = ToolInfoDialogViewModel.FromTool(result);
    }
}
