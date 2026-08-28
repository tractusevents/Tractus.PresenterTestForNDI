using System.Windows;

namespace Tractus.PresenterTest;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Any(arg => arg.Equals("--headless", StringComparison.OrdinalIgnoreCase)) ||
            e.Args.Any(arg => arg.Equals("--preview", StringComparison.OrdinalIgnoreCase)) ||
            e.Args.Any(arg => arg is "--help" or "-h" or "/?"))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = Task.Run(async () =>
            {
                var exitCode = await HeadlessRunner.RunAsync(e.Args);
                Dispatcher.Invoke(() => Shutdown(exitCode));
            });
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
