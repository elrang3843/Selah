using System.Windows;
using Selah.App.ViewModels;

namespace Selah.App.Views;

public partial class ModelManagerWindow : Window
{
    public ModelManagerWindow(ModelManagerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
