using System.Windows;
using System.Windows.Controls;
using Selah.App.ViewModels;

namespace Selah.App.Views;

public partial class TransportBar : UserControl
{
    public TransportBar()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        => ViewModel?.Timeline.ZoomIn();

    private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        => ViewModel?.Timeline.ZoomOut();
}
