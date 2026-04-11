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
                Loc.Format("App_UnexpectedError", ex.Exception.Message),
                Loc.Get("App_Error_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            ex.Handled = true;
        };
    }
}
