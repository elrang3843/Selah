namespace Selah.Core.Models;

/// <summary>
/// OMR(광학 악보 인식)으로 분석한 악보의 구조 프로파일.
/// sheet_music_runner.py가 JSON으로 stdout에 출력하며, SheetMusicService가 역직렬화합니다.
/// </summary>
public class ScoreProfile
{
    /// <summary>보표(staff) 수. 1 = 단일 보표, 2 = 대보표(피아노 등).</summary>
    public int StaffCount { get; set; } = 1;

    /// <summary>각 보표의 음자리표 종류. "treble", "bass", "percussion" 등.</summary>
    public string[] ClefTypes { get; set; } = [];

    /// <summary>화음(동시 여러 음)이 포함된 악보인지 여부.</summary>
    public bool IsPolyphonic { get; set; }

    /// <summary>타악 음자리표(무음고) 포함 여부. true이면 드럼 트랙으로 처리합니다.</summary>
    public bool HasPercussionClef { get; set; }

    /// <summary>전체 박자 중 화음이 있는 박자 비율 (0.0–1.0).</summary>
    public float ChordDensity { get; set; }

    /// <summary>최저 음 MIDI 번호 (0–127). 음역대 하한.</summary>
    public int PitchRangeMin { get; set; }

    /// <summary>최고 음 MIDI 번호 (0–127). 음역대 상한.</summary>
    public int PitchRangeMax { get; set; }

    /// <summary>인식된 전체 음표(단음 + 화음 구성음) 수.</summary>
    public int NoteCount { get; set; }

    /// <summary>악보 재생 예상 길이 (초).</summary>
    public double DurationSeconds { get; set; }

    /// <summary>OMR이 생성한 MIDI 파일의 절대 경로.</summary>
    public string MidiPath { get; set; } = string.Empty;

    /// <summary>
    /// 악보 특성 기반으로 추천하는 악기 키 목록.
    /// SheetMusicService.SuggestInstruments()가 채웁니다.
    /// </summary>
    public string[] SuggestedInstruments { get; set; } = [];
}
