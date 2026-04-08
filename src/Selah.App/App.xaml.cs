using System.Windows;

namespace Selah.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show(
                $"예기치 않은 오류가 발생했습니다:\n\n{ex.Exception.Message}",
                "셀라(Selah) 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            ex.Handled = true;
        };
    }
}
