using System.Windows;
using Selah.App.ViewModels;
using Selah.App.Views;

namespace Selah.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private ProgressWindow? _progressWindow;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        // ViewModel 이벤트를 코드 비하인드에서 처리 (다이얼로그 열기 등)
        _vm.NewProjectRequested       += OnNewProjectRequested;
        _vm.OpenProjectFolderRequested += OnOpenProjectFolderRequested;
        _vm.SaveProjectFolderRequested += OnSaveProjectFolderRequested;
        _vm.ImportAudioRequested      += OnImportAudioRequested;
        _vm.ExportPathRequested       += OnExportPathRequested;
        _vm.ErrorOccurred             += OnErrorOccurred;
        _vm.SetupGuideRequested       += OpenSetupGuide;
        _vm.ProgressStarted           += OnProgressStarted;
        _vm.ProgressFinished          += OnProgressFinished;
        _vm.ImportSheetMusicRequested += OnImportSheetMusicRequested;

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

    // ── 진행 팝업 ──

    private void OnProgressStarted(string _)
    {
        _progressWindow = new ProgressWindow { DataContext = _vm, Owner = this };
        IsEnabled = false;
        _progressWindow.Show();
    }

    private void OnProgressFinished()
    {
        if (_progressWindow != null)
        {
            _progressWindow.AllowClose = true;
            _progressWindow.Close();
            _progressWindow = null;
        }
        IsEnabled = true;
    }

    // ── 메뉴 핸들러 ──

    private Task<SheetMusicDialogResult?> OnImportSheetMusicRequested()
    {
        // OMR 중간 결과 디렉터리 결정 (프로젝트 경로 또는 임시 폴더)
        var proj = _vm.CurrentProject;
        string basePath = proj?.Model.FilePath
            ?? System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "Selah",
                proj?.Model.Id ?? "omr");
        var omrDir = System.IO.Path.Combine(basePath, "audio", "sheetmusic", "omr",
            DateTime.Now.ToString("yyyyMMddHHmmss"));

        var dlg = new Views.SheetMusicDialog(_vm.SheetMusicService, omrDir) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SelectedInstruments.Length > 0)
        {
            return Task.FromResult<SheetMusicDialogResult?>(new SheetMusicDialogResult
            {
                MidiPath            = dlg.MidiPath,
                SelectedInstruments = dlg.SelectedInstruments
            });
        }
        return Task.FromResult<SheetMusicDialogResult?>(null);
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

    private void MenuShortcuts_Click(object s, RoutedEventArgs e) =>
        OpenHelp(0);

    private void MenuUserGuide_Click(object s, RoutedEventArgs e) =>
        OpenHelp(1);

    private void MenuEthics_Click(object s, RoutedEventArgs e) =>
        OpenHelp(2);

    private void MenuAbout_Click(object s, RoutedEventArgs e) =>
        OpenHelp(3);

    private void OpenHelp(int tab)
    {
        var w = new Views.HelpWindow(tab) { Owner = this };
        w.ShowDialog();
    }

    // ── 언어 전환 ──

    private void MenuLang_Click(object s, RoutedEventArgs e)
    {
        if (s is System.Windows.Controls.MenuItem item && item.Tag is string code)
            Loc.SetLanguage(code);
    }

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
            OpenHelp(0);
            e.Handled = true;
            return;
        }

        bool ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0;

        if (ctrl)
        {
            switch (e.Key)
            {
                case System.Windows.Input.Key.C:
                    if (_vm.CopyCommand.CanExecute(null)) { _vm.CopyCommand.Execute(null); e.Handled = true; }
                    break;
                case System.Windows.Input.Key.V:
                    if (_vm.PasteCommand.CanExecute(null)) { _vm.PasteCommand.Execute(null); e.Handled = true; }
                    break;
                case System.Windows.Input.Key.X:
                    if (_vm.CutCommand.CanExecute(null)) { _vm.CutCommand.Execute(null); e.Handled = true; }
                    break;
                case System.Windows.Input.Key.M:
                    if (_vm.MergeCommand.CanExecute(null)) { _vm.MergeCommand.Execute(null); e.Handled = true; }
                    break;
                case System.Windows.Input.Key.J:
                    if (_vm.MoveAfterPreviousCommand.CanExecute(null)) { _vm.MoveAfterPreviousCommand.Execute(null); e.Handled = true; }
                    break;
            }
        }
        else if (e.Key == System.Windows.Input.Key.Delete)
        {
            if (_vm.DeleteCommand.CanExecute(null)) { _vm.DeleteCommand.Execute(null); e.Handled = true; }
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
