using System.Windows;
using System.Windows.Controls;

namespace Selah.App.Views;

public partial class HelpWindow : Window
{
    private ScrollViewer[] _panels = null!;

    // tab: 0=단축키, 1=사용가이드, 2=윤리, 3=정보
    public HelpWindow(int initialTab = 0)
    {
        InitializeComponent();
        _panels = [PanelShortcuts, PanelUserGuide, PanelEthics, PanelAbout];
        NavList.SelectedIndex = Math.Clamp(initialTab, 0, _panels.Length - 1);
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_panels is null) return;
        int idx = NavList.SelectedIndex;
        for (int i = 0; i < _panels.Length; i++)
            _panels[i].Visibility = i == idx ? Visibility.Visible : Visibility.Collapsed;
    }
}
