using System.Windows;

namespace Selah.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // OS 테마를 감지해 적절한 테마 딕셔너리를 로드하고 변경 이벤트를 구독합니다.
        SystemThemeService.Initialize();

        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show(
                Loc.Format("App_UnexpectedError", ex.Exception.Message),
                Loc.Get("App_Error_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            ex.Handled = true;
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemThemeService.Shutdown();
        base.OnExit(e);
    }
}
