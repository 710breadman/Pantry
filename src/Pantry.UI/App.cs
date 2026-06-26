using Microsoft.UI.Xaml;

namespace Pantry.UI;

public sealed class App : Application
{
    private Window? _window;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}

