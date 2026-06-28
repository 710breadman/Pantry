using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevToolsCurator.Core;

namespace DevToolsCurator.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.SetDialogService(new DialogService(this));
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Startup failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ToolRow_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ToolScanResult result })
        {
            _viewModel.SelectedTool = result;
        }
    }
}
