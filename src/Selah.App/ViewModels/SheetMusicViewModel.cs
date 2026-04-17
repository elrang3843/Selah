using System.Collections.ObjectModel;
using System.Windows.Input;
using Selah.Core.Models;
using Selah.Core.Services;

namespace Selah.App.ViewModels;

/// <summary>
/// 악보 인식 다이얼로그의 ViewModel.
/// RecognizeAsync: 이미지 전처리 + OMR + ScoreProfile 분석 (sheet_music_runner.py)
/// 악기 선택 후 OK → MainViewModel.ImportSheetMusicAsync()가 합성 수행
/// </summary>
public class SheetMusicViewModel : ViewModelBase
{
    private readonly SheetMusicService _service;

    private string _imagePath        = string.Empty;
    private bool   _isRecognizing;
    private double _recognitionPercent;
    private string _recognitionStatus = string.Empty;
    private ScoreProfile? _profile;
    private string _scoreInfoText    = string.Empty;
    private bool   _hasProfile;
    private bool   _canInsert;

    public SheetMusicViewModel(SheetMusicService service)
    {
        _service = service;
        Instruments = new ObservableCollection<InstrumentOption>(BuildInstruments());
        RecognizeCommand = new AsyncRelayCommand(RecognizeAsync,
            () => !string.IsNullOrWhiteSpace(ImagePath) && !IsRecognizing);
    }

    // ── 입력 이미지 ───────────────────────────────────────────────────────────

    public string ImagePath
    {
        get => _imagePath;
        set
        {
            SetField(ref _imagePath, value);
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    // ── 인식 상태 ─────────────────────────────────────────────────────────────

    public bool IsRecognizing
    {
        get => _isRecognizing;
        private set
        {
            SetField(ref _isRecognizing, value);
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    public double RecognitionPercent
    {
        get => _recognitionPercent;
        private set => SetField(ref _recognitionPercent, value);
    }

    public string RecognitionStatus
    {
        get => _recognitionStatus;
        private set => SetField(ref _recognitionStatus, value);
    }

    // ── 인식 결과 ─────────────────────────────────────────────────────────────

    public ScoreProfile? Profile
    {
        get => _profile;
        private set => SetField(ref _profile, value);
    }

    public string ScoreInfoText
    {
        get => _scoreInfoText;
        private set => SetField(ref _scoreInfoText, value);
    }

    public bool HasProfile
    {
        get => _hasProfile;
        private set => SetField(ref _hasProfile, value);
    }

    public bool CanInsert
    {
        get => _canInsert;
        private set => SetField(ref _canInsert, value);
    }

    // ── 악기 목록 ─────────────────────────────────────────────────────────────

    public ObservableCollection<InstrumentOption> Instruments { get; }

    /// <summary>사용자가 체크한 악기 키 목록. OK 클릭 시 MainViewModel이 읽습니다.</summary>
    public string[] SelectedInstrumentKeys =>
        [.. Instruments.Where(i => i.IsChecked).Select(i => i.Key)];

    // ── 커맨드 ───────────────────────────────────────────────────────────────

    public ICommand RecognizeCommand { get; }

    /// <summary>OMR 출력 디렉터리. SheetMusicDialog 생성자가 프로젝트 경로 기반으로 설정합니다.</summary>
    public string OutputDir { get; set; } = string.Empty;

    // ── 인식 실행 ─────────────────────────────────────────────────────────────

    private async Task RecognizeAsync()
    {
        IsRecognizing      = true;
        RecognitionPercent = 0;
        RecognitionStatus  = "인식 시작...";
        HasProfile         = false;
        CanInsert          = false;

        // 이전 선택 초기화
        foreach (var inst in Instruments)
        {
            inst.IsChecked     = false;
            inst.IsRecommended = false;
        }

        try
        {
            var progress = new Progress<SheetMusicProgress>(p =>
            {
                RecognitionStatus  = p.Phase;
                RecognitionPercent = p.Percent * 100.0;
            });

            var result = await _service.RecognizeAsync(ImagePath, OutputDir, progress);

            if (!result.Success || result.Profile == null)
            {
                RecognitionStatus = result.IsOmerMissing
                    ? "oemer 미설치 — 터미널에서 'pip install oemer'를 실행하세요."
                    : result.IsMusic21Missing
                        ? "music21 미설치 — 터미널에서 'pip install music21'를 실행하세요."
                        : result.IsOmrFailed
                            ? "인식할 수 없는 악보입니다."
                            : $"오류: {result.Error ?? "알 수 없는 오류"}";
                return;
            }

            Profile      = result.Profile;
            HasProfile   = true;
            ScoreInfoText = BuildScoreInfoText(result.Profile);

            // 추천 악기 반영
            var suggested = result.Profile.SuggestedInstruments;
            foreach (var inst in Instruments)
            {
                inst.IsRecommended = suggested.Contains(inst.Key, StringComparer.OrdinalIgnoreCase);
                inst.IsChecked     = inst.IsRecommended;
            }

            RecognitionStatus  = $"인식 완료 — {result.Profile.NoteCount}개 음표, " +
                                 $"{result.Profile.DurationSeconds:F1}초";
            RecognitionPercent = 100;
            CanInsert          = true;
        }
        catch (Exception ex)
        {
            RecognitionStatus = $"오류: {ex.Message}";
        }
        finally
        {
            IsRecognizing = false;
        }
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────────

    private static string BuildScoreInfoText(ScoreProfile p)
    {
        var clefs = p.ClefTypes.Length > 0 ? string.Join(", ", p.ClefTypes) : "—";
        var poly  = p.IsPolyphonic ? "화음 포함" : "단성";
        var perc  = p.HasPercussionClef ? " (타악기 보표)" : string.Empty;
        return $"보표 {p.StaffCount}개  |  음자리표: {clefs}{perc}  |  {poly}  |  " +
               $"음표 {p.NoteCount}개  |  {p.DurationSeconds:F1}초";
    }

    private static IEnumerable<InstrumentOption> BuildInstruments() =>
    [
        new() { Key = "Piano",          DisplayName = "피아노 (Piano)" },
        new() { Key = "AcousticGuitar", DisplayName = "어쿠스틱 기타 (Acoustic Guitar)" },
        new() { Key = "ElectricGuitar", DisplayName = "일렉트릭 기타 (Electric Guitar)" },
        new() { Key = "BassGuitar",     DisplayName = "베이스 기타 (Bass Guitar)" },
        new() { Key = "Drums",          DisplayName = "드럼 (Drums)" },
        new() { Key = "Synthesizer",    DisplayName = "신디사이저 (Synthesizer)" },
        new() { Key = "Saxophone",      DisplayName = "색소폰 (Saxophone)" },
        new() { Key = "Flute",          DisplayName = "플루트 (Flute)" },
    ];
}

/// <summary>악기 선택 항목. IsRecommended는 악보 특성 분석 결과로 설정됩니다.</summary>
public class InstrumentOption : ViewModelBase
{
    private bool _isChecked;
    private bool _isRecommended;

    public string Key         { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    public bool IsChecked
    {
        get => _isChecked;
        set => SetField(ref _isChecked, value);
    }

    public bool IsRecommended
    {
        get => _isRecommended;
        set => SetField(ref _isRecommended, value);
    }
}
