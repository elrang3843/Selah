using System.Windows;

namespace Selah.App.Views;

public partial class NewProjectDialog : Window
{
    public string ProjectName { get; private set; } = string.Empty;
    public int SampleRate { get; private set; } = 48000;

    public NewProjectDialog()
    {
        InitializeComponent();
        TxtName.Text = Loc.Get("NewProject_DefaultName");
        Loaded += (_, _) => TxtName.SelectAll();
        TxtName.Focus();
    }

    private void BtnCreate_Click(object sender, RoutedEventArgs e)
    {
        var name = TxtName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show(
                Loc.Get("NewProject_NameRequired"),
                Loc.Get("NewProject_AppTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        ProjectName = name;
        SampleRate = Rb32k.IsChecked == true ? 32000
                   : Rb44k.IsChecked == true ? 44100
                   : Rb96k.IsChecked == true ? 96000
                   : 48000;

        DialogResult = true;
    }
}
