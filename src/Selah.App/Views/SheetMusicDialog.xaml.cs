using System.Windows;
using Microsoft.Win32;
using Selah.App.ViewModels;
using Selah.Core.Services;

namespace Selah.App.Views;

public partial class SheetMusicDialog : Window
{
    private readonly SheetMusicViewModel _vm;

    /// <summary>
    /// 사용자가 OK를 클릭한 경우 선택된 악기 키 목록.
    /// </summary>
    public string[] SelectedInstruments => _vm.SelectedInstrumentKeys;

    /// <summary>
    /// OMR이 생성한 MIDI 파일 절대 경로. Profile이 null이면 빈 문자열.
    /// </summary>
    public string MidiPath => _vm.Profile?.MidiPath ?? string.Empty;

    /// <param name="service">SheetMusicService 인스턴스.</param>
    /// <param name="omrOutputDir">OMR 중간 결과를 저장할 디렉터리 (프로젝트 audio/sheetmusic/omr/).</param>
    public SheetMusicDialog(SheetMusicService service, string omrOutputDir)
    {
        InitializeComponent();
        _vm = new SheetMusicViewModel(service) { OutputDir = omrOutputDir };
        DataContext = _vm;
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────────────────────────

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "악보 이미지 선택",
            Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.tiff;*.tif;*.bmp;*.gif|모든 파일|*.*"
        };
        if (dlg.ShowDialog(this) == true)
            _vm.ImagePath = dlg.FileName;
    }

    private void InsertButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedInstrumentKeys.Length == 0)
        {
            MessageBox.Show(
                "하나 이상의 악기를 선택해 주세요.",
                "악기 선택 필요",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
