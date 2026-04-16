using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Selah.App.Services;

namespace Selah.App.Views;

public partial class HelpWindow : Window
{
    private static readonly string[] _anchors = ["shortcuts", "userguide", "ethics", "about"];
    private bool _loaded;

    // tab: 0=단축키, 1=사용가이드, 2=윤리, 3=정보
    public HelpWindow(int initialTab = 0)
    {
        InitializeComponent();
        NavList.SelectedIndex = Math.Clamp(initialTab, 0, _anchors.Length - 1);
        LoadPage();
    }

    private void LoadPage()
    {
        _loaded = false;
        var lang = Loc.CurrentLanguage;
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, $"help_{lang}.html");
        if (!System.IO.File.Exists(path))
            path = System.IO.Path.Combine(AppContext.BaseDirectory, "help_en.html");
        if (System.IO.File.Exists(path))
            HelpBrowser.Navigate(new Uri(path));
    }

    private void HelpBrowser_LoadCompleted(object sender, NavigationEventArgs e)
    {
        _loaded = true;
        Scroll();
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e) => Scroll();

    private void Scroll()
    {
        if (!_loaded) return;
        try { HelpBrowser.InvokeScript("scrollToSection", _anchors[NavList.SelectedIndex]); }
        catch { }
    }
}
