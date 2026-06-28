using Microsoft.UI.Xaml;

namespace Grabsy;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Grabsy] Unhandled: {e.Message}");
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services.NotificationService.Init();
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
