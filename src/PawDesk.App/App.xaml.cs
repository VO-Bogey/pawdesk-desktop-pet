using System.Windows;
using PawDesk.App.Services;

namespace PawDesk.App;

public partial class App : System.Windows.Application
{
    private TrayService? _trayService;
    private SettingsWindow? _settingsWindow;
    private OnnxBackgroundRemovalService? _backgroundRemovalService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = new AppPathService();
        var logService = new LogService(paths);
        DispatcherUnhandledException += (_, args) =>
        {
            logService.Error(args.Exception, "Unhandled UI exception.");
            System.Windows.MessageBox.Show("PawDesk 遇到错误，但会尽量继续运行。详情已写入日志。", "PawDesk");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                logService.Error(exception, "Unhandled application exception.");
            }
        };

        var settingsService = new AppSettingsService(paths);
        _backgroundRemovalService = new OnnxBackgroundRemovalService(logService);
        var petImageService = new PetImageService(paths, _backgroundRemovalService);
        var startupService = new StartupService();
        var settings = settingsService.Load();

        PetWindow? petWindow = null;
        petWindow = new PetWindow(settings, settingsService, petImageService, startupService, OpenSettings);
        _trayService = new TrayService(
            petWindow.Show,
            petWindow.Hide,
            petWindow.ChangeImage,
            OpenSettings,
            startupService.IsEnabled,
            enabled =>
            {
                settings.StartWithWindows = enabled;
                startupService.SetEnabled(enabled);
                settingsService.Save(settings);
            },
            Shutdown);

        petWindow.Show();

        void OpenSettings()
        {
            if (petWindow is null)
            {
                return;
            }

            if (_settingsWindow is null || !_settingsWindow.IsLoaded)
            {
                _settingsWindow = new SettingsWindow(settings, settingsService, startupService, petWindow);
            }

            _settingsWindow.RefreshView();
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayService?.Dispose();
        _backgroundRemovalService?.Dispose();
        base.OnExit(e);
    }
}
