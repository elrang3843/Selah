using System.Windows;
using Selah.App.ViewModels;
using Selah.App.Views;

namespace Selah.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        // ViewModel 이벤트를 코드 비하인드에서 처리 (다이얼로그 열기 등)
        _vm.NewProjectRequested += OnNewProjectRequested;
        _vm.OpenProjectFolderRequested += OnOpenProjectFolderRequested;
        _vm.SaveProjectFolderRequested += OnSaveProjectFolderRequested;
        _vm.ImportAudioRequested += OnImportAudioRequested;
        _vm.ExportPathRequested += OnExportPathRequested;
        _vm.ErrorOccurred += OnErrorOccurred;
        _vm.SetupGuideRequested += OpenSetupGuide;

        KeyDown += MainWindow_KeyDown;
        Closing += MainWindow_Closing;
        Loaded += MainWindow_Loaded;
    }

    // ── 시작 시 첫 실행 확인 ──

    private static readonly string _setupFlagFile = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Selah", "setup_guide_shown");

    private void MainWindow_Loaded(object s, RoutedEventArgs e)
    {
        if (!System.IO.File.Exists(_setupFlagFile))
        {
            // 첫 실행 — 설치 안내 페이지를 브라우저로 엽니다
            try { System.IO.File.WriteAllText(_setupFlagFile, "1"); }
            catch { /* 무시 */ }
            OpenSetupGuide();
        }
    }

    // ── 설치 안내 페이지 열기 ──

    private static void OpenSetupGuide()
    {
        var html = System.IO.Path.Combine(AppContext.BaseDirectory, "tools_setup.html");
        if (!System.IO.File.Exists(html)) return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(html) { UseShellExecute = true });
        }
        catch { /* 무시 */ }
    }

    // ── 다이얼로그 핸들러 ──

    private Task OnNewProjectRequested((string name, int sampleRate) _)
    {
        var dlg = new NewProjectDialog { Owner = this };
        if (dlg.ShowDialog() == true)
            _vm.CreateProject(dlg.ProjectName, dlg.SampleRate);
        return Task.CompletedTask;
    }

    private Task<string?> OnOpenProjectFolderRequested()
    {
        // WPF에는 FolderBrowserDialog가 없으므로 OpenFileDialog로 project.json.gz를 직접 선택
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.Get("Dialog_Open_Title"),
            Filter = Loc.Get("Dialog_Open_Filter"),
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == true)
            return Task.FromResult<string?>(System.IO.Path.GetDirectoryName(dlg.FileName));
        return Task.FromResult<string?>(null);
    }

    private Task<string?> OnSaveProjectFolderRequested()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = Loc.Get("Dialog_Save_Title"),
            Filter = Loc.Get("Dialog_Save_Filter"),
            DefaultExt = ".selah",
            FileName = _vm.CurrentProject?.Name ?? Loc.Get("Dialog_Save_DefaultName")
        };
        if (dlg.ShowDialog(this) == true)
        {
            // 폴더 경로 = 선택한 파일 이름(확장자 제외)
            var dir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(dlg.FileName)!,
                System.IO.Path.GetFileNameWithoutExtension(dlg.FileName));
            return Task.FromResult<string?>(dir);
        }
        return Task.FromResult<string?>(null);
    }

    private Task<string[]?> OnImportAudioRequested()
    {
        bool ffmpegOk = _vm.IsFFmpegAvailable;
        var filter = ffmpegOk
            ? Loc.Get("Dialog_Import_Filter_Full")
            : Loc.Get("Dialog_Import_Filter_Wav");

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.Get("Dialog_Import_Title"),
            Filter = filter,
            Multiselect = true
        };

        if (!ffmpegOk)
        {
            var hint = MessageBox.Show(
                Loc.Get("Dialog_FFmpegMissing_Message"),
                Loc.Get("Dialog_FFmpegMissing_Title"),
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);
            if (hint == MessageBoxResult.Cancel)
                return Task.FromResult<string[]?>(null);
        }

        if (dlg.ShowDialog(this) == true)
            return Task.FromResult<string[]?>(dlg.FileNames);
        return Task.FromResult<string[]?>(null);
    }

    private Task<string?> OnExportPathRequested()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = Loc.Get("Dialog_Export_Title"),
            Filter = Loc.Get("Dialog_Export_Filter"),
            DefaultExt = ".wav",
            FileName = (_vm.CurrentProject?.Name ?? Loc.Get("Dialog_Export_DefaultName")) + "_MR"
        };
        if (dlg.ShowDialog(this) == true)
            return Task.FromResult<string?>(dlg.FileName);
        return Task.FromResult<string?>(null);
    }

    private void OnErrorOccurred(string message)
    {
        MessageBox.Show(message, Loc.Get("Dialog_Error_Title"), MessageBoxButton.OK, MessageBoxImage.Error);
    }

    // ── 메뉴 핸들러 ──

    private void MenuExit_Click(object s, RoutedEventArgs e) => Close();

    private void MenuModelManager_Click(object s, RoutedEventArgs e)
    {
        var w = new ModelManagerWindow(_vm.ModelManager) { Owner = this };
        w.ShowDialog();
    }

    private void MenuHardwareDiag_Click(object s, RoutedEventArgs e)
    {
        MessageBox.Show(
            Loc.Format("Dialog_HardwareDiag_Message", _vm.HardwareStatusText),
            Loc.Get("Dialog_HardwareDiag_Title"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void MenuFFmpegGuide_Click(object s, RoutedEventArgs e) => OpenSetupGuide();

    private void MenuShortcuts_Click(object s, RoutedEventArgs e)
    {
        MessageBox.Show(
            Loc.Get("Dialog_Shortcuts_Message"),
            Loc.Get("Dialog_Shortcuts_Title"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void MenuEthics_Click(object s, RoutedEventArgs e)
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "docs", "ETHICS.md");
        if (System.IO.File.Exists(path))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
                { UseShellExecute = true });
        else
            MessageBox.Show(
                Loc.Get("Dialog_Ethics_Message"),
                Loc.Get("Dialog_Ethics_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
    }

    private void MenuAbout_Click(object s, RoutedEventArgs e)
    {
        MessageBox.Show(
            Loc.Get("Dialog_About_Message"),
            Loc.Get("Dialog_About_Title"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // ── 언어 전환 ──

    private void MenuLang_Ko_Click(object s, RoutedEventArgs e) => Loc.SetLanguage("ko");
    private void MenuLang_En_Click(object s, RoutedEventArgs e) => Loc.SetLanguage("en");

    private void MenuTheme_Click(object s, RoutedEventArgs e)
    {
        bool dark = (s as System.Windows.Controls.MenuItem)?.Tag as string == "dark";
        Services.SystemThemeService.Apply(dark);
    }

    // ── 키보드 ──

    private void MainWindow_KeyDown(object s, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.F1)
        {
            MenuShortcuts_Click(s, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    // ── 종료 처리 ──

    private void MainWindow_Closing(object? s, System.ComponentModel.CancelEventArgs e)
    {
        if (_vm.CurrentProject?.IsDirty == true)
        {
            var result = MessageBox.Show(
                Loc.Format("Dialog_UnsavedChanges_Message", _vm.CurrentProject.Name),
                Loc.Get("Dialog_UnsavedChanges_Title"),
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (result == MessageBoxResult.Yes)
            {
                _vm.SaveProjectCommand.Execute(null);
            }
        }
        _vm.Dispose();
    }
}
