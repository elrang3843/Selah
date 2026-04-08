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

        KeyDown += MainWindow_KeyDown;
        Closing += MainWindow_Closing;
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
            Title = "셀라 프로젝트 열기",
            Filter = "셀라 프로젝트 (project.json.gz)|project.json.gz",
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
            Title = "프로젝트 저장 위치 선택",
            Filter = "셀라 프로젝트 폴더|*.selah",
            DefaultExt = ".selah",
            FileName = _vm.CurrentProject?.Name ?? "새 프로젝트"
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
            ? "오디오/영상 파일|*.wav;*.mp3;*.flac;*.m4a;*.aac;*.ogg;*.mp4;*.mkv;*.mov|모든 파일|*.*"
            : "WAV 파일 (FFmpeg 미설치)|*.wav|모든 파일|*.*";

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "오디오/영상 파일 불러오기",
            Filter = filter,
            Multiselect = true
        };

        if (!ffmpegOk)
        {
            var hint = MessageBox.Show(
                "FFmpeg가 설치되어 있지 않습니다.\n" +
                "WAV 파일만 직접 불러올 수 있습니다.\n\n" +
                "MP3/MP4 등을 지원하려면 '도구 > FFmpeg 설치 안내'를 참고하세요.\n\n" +
                "계속 진행하시겠습니까?",
                "FFmpeg 미설치", MessageBoxButton.OKCancel, MessageBoxImage.Information);
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
            Title = "WAV로 내보내기",
            Filter = "WAV 파일 (24-bit)|*.wav",
            DefaultExt = ".wav",
            FileName = (_vm.CurrentProject?.Name ?? "믹스") + "_MR"
        };
        if (dlg.ShowDialog(this) == true)
            return Task.FromResult<string?>(dlg.FileName);
        return Task.FromResult<string?>(null);
    }

    private void OnErrorOccurred(string message)
    {
        MessageBox.Show(message, "셀라 오류", MessageBoxButton.OK, MessageBoxImage.Error);
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
        var hw = _vm.HardwareStatusText;
        MessageBox.Show(
            $"하드웨어 가속 진단 결과:\n\n{hw}\n\n" +
            "설정 > 가속 백엔드에서 강제로 CPU 모드로 변경할 수 있습니다.",
            "하드웨어 진단", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MenuFFmpegGuide_Click(object s, RoutedEventArgs e)
    {
        MessageBox.Show(
            "FFmpeg 설치 안내\n\n" +
            "1. https://ffmpeg.org/download.html 에서 Windows 빌드를 다운로드합니다.\n" +
            "2. 압축을 해제하고 bin 폴더를 시스템 PATH에 추가합니다.\n" +
            "3. 명령 프롬프트에서 'ffmpeg -version' 으로 설치를 확인합니다.\n\n" +
            "FFmpeg 설치 후 셀라를 재시작하면 MP3, MP4 등 다양한 형식을 불러올 수 있습니다.",
            "FFmpeg 설치 안내", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MenuShortcuts_Click(object s, RoutedEventArgs e)
    {
        MessageBox.Show(
            "셀라(Selah) 단축키 목록\n\n" +
            "── 파일 ──\n" +
            "  Ctrl+N          새 프로젝트\n" +
            "  Ctrl+O          열기\n" +
            "  Ctrl+S          저장\n" +
            "  Ctrl+Shift+S    다른 이름으로 저장\n" +
            "  Ctrl+E          WAV 내보내기\n\n" +
            "── 재생 ──\n" +
            "  Space           재생 / 정지\n" +
            "  Home            처음으로 이동\n\n" +
            "── 편집 ──\n" +
            "  S               커서 위치에서 분할\n" +
            "  Delete          선택 삭제\n" +
            "  Ctrl+Z          실행 취소\n" +
            "  Ctrl+Y          다시 실행\n\n" +
            "── 도구 ──\n" +
            "  M               메트로놈 켜기/끄기\n" +
            "  N               스냅 켜기/끄기\n" +
            "  Ctrl+마우스휠   타임라인 줌 인/아웃\n" +
            "  F1              이 단축키 목록",
            "단축키 목록 (F1)", MessageBoxButton.OK, MessageBoxImage.Information);
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
                "셀라(Selah) 윤리적 사용 가이드라인\n\n" +
                "이 소프트웨어는 기독교회 예배를 섬기는 사역자를 위해 제작되었습니다.\n\n" +
                "허용되는 사용:\n" +
                "  • 교회 예배용 반주 음악(MR) 제작\n" +
                "  • 저작권이 없거나 이용허락을 받은 음원 처리\n" +
                "  • CCL 라이선스 범위 내의 교회 음악\n\n" +
                "금지되는 사용:\n" +
                "  • 저작권 있는 음원의 무단 분리 및 재배포\n" +
                "  • 상업적 목적의 저작권 침해\n" +
                "  • 원저작자의 의도에 반하는 변형·왜곡\n\n" +
                "음원 처리 전 항상 해당 음원의 저작권 상태를 확인하십시오.",
                "윤리적 사용 가이드라인", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MenuAbout_Click(object s, RoutedEventArgs e)
    {
        MessageBox.Show(
            "셀라 (Selah)\n찬양사역용 MR편집기\n\n" +
            "버전: 0.1.0 (1차 프로토타입)\n" +
            "라이선스: GPLv3\n" +
            "소스코드: https://github.com/elrang3843/Selah\n\n" +
            "이 소프트웨어는 소형 교회와 선교 현장의 예배 사역자를 위해\n" +
            "오픈소스(GPLv3)로 자유롭게 배포됩니다.\n\n" +
            "사용된 핵심 오픈소스 라이브러리:\n" +
            "  • NAudio (Microsoft License / MIT)\n" +
            "  • Demucs — Meta AI (MIT)\n" +
            "  • FFmpeg (LGPL/GPL, 선택 설치)\n\n" +
            "모든 영광을 하나님께.",
            "셀라(Selah) 정보", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── 키보드 ──

    private void MainWindow_KeyDown(object s, System.Windows.Input.KeyEventArgs e)
    {
        // F1 단축키 처리
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
                $"'{_vm.CurrentProject.Name}' 프로젝트에 저장하지 않은 변경사항이 있습니다.\n저장하시겠습니까?",
                "셀라", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

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
