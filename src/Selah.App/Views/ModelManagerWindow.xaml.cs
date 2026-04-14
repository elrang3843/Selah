using System.ComponentModel;
using System.Windows;
using Selah.App.ViewModels;

namespace Selah.App.Views;

public partial class ModelManagerWindow : Window
{
    public ModelManagerWindow(ModelManagerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModelManagerViewModel.InstallLog))
            LogBox.ScrollToEnd();
    }
}
