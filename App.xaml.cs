using StarRuptureSaveFixer.Services;
using System.Windows;

namespace StarRuptureSaveFixer;

public partial class App : Application
{
    private readonly LoggingService _logger = LoggingService.Instance;

    [System.STAThreadAttribute()]
    public static void Main()
    {
        var app = new App();
        app.InitializeComponent();

        app.Exit += App_Exit;
        app.DispatcherUnhandledException += App_DispatcherUnhandledException;

        var mainWindow = new MainWindow();
        app.Run(mainWindow);
    }

    private static void App_Exit(object? sender, ExitEventArgs e)
    {
        var logger = LoggingService.Instance;
        logger.LogInfo($"Application exiting with code: {e.ApplicationExitCode}");
        logger.Dispose();
    }

    private static void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        var logger = LoggingService.Instance;
        logger.LogError("Unhandled exception in dispatcher", e.Exception);

        MessageBox.Show(
         $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe error has been logged.",
      "Error",
      MessageBoxButton.OK,
    MessageBoxImage.Error);

        // Prevent application crash
        e.Handled = true;
    }
}
