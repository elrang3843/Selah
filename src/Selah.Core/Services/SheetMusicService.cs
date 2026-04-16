using System.Diagnostics;
using System.Text.Json;
using Selah.Core.Models;

namespace Selah.Core.Services;

/// <summary>
/// 악보 이미지 인식(OMR) 및 MIDI 합성 서비스.
///
/// 파이프라인:
///   1. RecognizeAsync  — sheet_music_runner.py → 이미지 전처리 + oemer OMR + music21 분석
///                        출력: ScoreProfile JSON + score.mid
///   2. SynthesizeAsync — midi_synthesizer.py   → MIDI 패치 교체 + FluidSynth 합성
///                        출력: 악기별 WAV
///
/// stdout 프로토콜 (Python → C#):
///   PROGRESS:&lt;0-100&gt;   진행률
///   LOG:&lt;message&gt;       상태/오류 메시지
///   PROFILE:&lt;json&gt;      ScoreProfile JSON (RecognizeAsync 전용)
/// </summary>
public class SheetMusicService
{
    private readonly FluidSynthService _fluidSynth;
    private readonly string? _pythonPath;

    public bool IsPythonAvailable => _pythonPath != null;

    /// <summary>악기 키 → General MIDI 프로그램 번호 (-1 = 드럼/채널 10).</summary>
    public static readonly IReadOnlyDictionary<string, int> GmPatchMap =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Piano"]          =  0,   // Acoustic Grand Piano
            ["AcousticGuitar"] = 25,   // Acoustic Guitar (Steel)
            ["ElectricGuitar"] = 27,   // Electric Guitar (Clean)
            ["BassGuitar"]     = 33,   // Electric Bass (Finger)
            ["Drums"]          = -1,   // 타악: MIDI 채널 9
            ["Synthesizer"]    = 80,   // Lead 1 (Square)
            ["Saxophone"]      = 66,   // Alto Sax
            ["Flute"]          = 73,   // Flute
        };

    public SheetMusicService(FluidSynthService fluidSynth)
    {
        _fluidSynth = fluidSynth;
        _pythonPath = ModelManagerService.FindPython();
    }

    // ── 악보 인식 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 악보 이미지를 OMR로 인식합니다.
    /// sheet_music_runner.py를 Python 서브프로세스로 실행하여
    /// MIDI 파일과 ScoreProfile을 반환합니다.
    /// </summary>
    public async Task<SheetMusicResult> RecognizeAsync(
        string imagePath,
        string outputDir,
        IProgress<SheetMusicProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (_pythonPath == null)
            throw new InvalidOperationException(
                "Python이 설치되어 있지 않습니다.\n" +
                "python.org에서 Python 3.10 이상을 설치해 주세요.");

        Directory.CreateDirectory(outputDir);

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "sheet_music_runner.py");
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("sheet_music_runner.py를 찾을 수 없습니다.", scriptPath);

        var args = $"\"{scriptPath}\" --input \"{imagePath}\" --output-dir \"{outputDir}\"";
        progress?.Report(new SheetMusicProgress { Phase = "악보 인식 시작...", Percent = 0 });

        var (exitCode, profile, errorDetail) = await RunRunnerAsync(_pythonPath, args, progress, ct);

        if (exitCode != 0 || profile == null)
        {
            return new SheetMusicResult
            {
                Success          = false,
                IsOmerMissing    = errorDetail.Contains("OEMER_MISSING",   StringComparison.Ordinal),
                IsMusic21Missing = errorDetail.Contains("MUSIC21_MISSING", StringComparison.Ordinal),
                Error = string.IsNullOrWhiteSpace(errorDetail)
                    ? $"OMR 엔진 종료 코드: {exitCode}"
                    : errorDetail
            };
        }

        // C# 쪽에서 악기 추천을 채웁니다
        profile.SuggestedInstruments = SuggestInstruments(profile);

        return new SheetMusicResult { Success = true, Profile = profile };
    }

    // ── MIDI 합성 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 악보 MIDI를 지정 악기로 합성하여 WAV 파일로 저장합니다.
    /// midi_synthesizer.py + FluidSynth를 경유합니다.
    /// </summary>
    public async Task<string> SynthesizeAsync(
        string midiPath,
        string instrument,
        string outputWavPath,
        int sampleRate,
        IProgress<SheetMusicProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (_pythonPath == null)
            throw new InvalidOperationException("Python이 없습니다.");

        if (!_fluidSynth.IsFluidSynthFound)
            throw new InvalidOperationException(
                "FluidSynth를 찾을 수 없습니다.\n" +
                "pip install fluidsynth 으로 Python 패키지를 설치하거나,\n" +
                "fluidsynth.org에서 실행 파일을 설치하고 PATH에 추가해 주세요.");

        if (!_fluidSynth.IsSoundFontFound)
            throw new InvalidOperationException(
                "SoundFont(.sf2) 파일을 찾을 수 없습니다.\n" +
                $".sf2 파일을 {FluidSynthService.GetSoundFontsDir()} 에 넣어 주세요.");

        if (!GmPatchMap.TryGetValue(instrument, out int patch))
            patch = 0;

        Directory.CreateDirectory(Path.GetDirectoryName(outputWavPath)!);

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "midi_synthesizer.py");
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("midi_synthesizer.py를 찾을 수 없습니다.", scriptPath);

        // --fluidsynth은 선택적 — exe가 없을 때 빈 문자열로 전달하면 Python API 경로만 사용됩니다
        var args = $"\"{scriptPath}\" " +
                   $"--midi \"{midiPath}\" " +
                   $"--soundfont \"{_fluidSynth.SoundFontPath}\" " +
                   $"--fluidsynth \"{_fluidSynth.FluidSynthPath ?? string.Empty}\" " +
                   $"--instrument \"{instrument}\" " +
                   $"--patch {patch} " +
                   $"--output \"{outputWavPath}\" " +
                   $"--sample-rate {sampleRate}";

        progress?.Report(new SheetMusicProgress { Phase = $"{instrument} 합성 중...", Percent = 0 });

        var (exitCode, errorDetail) = await RunSynthesizerAsync(_pythonPath, args, progress, ct);

        if (exitCode != 0)
            throw new Exception($"MIDI 합성 실패 ({instrument}):\n{errorDetail}");

        return outputWavPath;
    }

    // ── 악기 추천 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// ScoreProfile 특성을 분석하여 8개 악기 중 추천 악기 키 목록을 반환합니다.
    ///
    /// 판단 기준:
    ///   타악 음자리표       → Drums
    ///   대보표(2-staff)     → Piano, Synthesizer
    ///   화음 밀도 > 30%     → AcousticGuitar, ElectricGuitar
    ///   최고음 ≤ MIDI 52    → BassGuitar (낮은 음역)
    ///   단성, 중고음역      → Saxophone, Flute
    ///   항상               → Piano, Synthesizer (모든 악보 연주 가능)
    /// </summary>
    public static string[] SuggestInstruments(ScoreProfile profile)
    {
        if (profile.HasPercussionClef)
            return ["Drums"];

        var suggestions = new List<string>();

        if (profile.StaffCount >= 2)
        {
            suggestions.Add("Piano");
            suggestions.Add("Synthesizer");
        }

        if (profile.ChordDensity > 0.3f)
        {
            suggestions.Add("AcousticGuitar");
            suggestions.Add("ElectricGuitar");
        }

        // MIDI 52 = E3 — 베이스 음역
        if (profile.PitchRangeMax > 0 && profile.PitchRangeMax <= 52)
            suggestions.Add("BassGuitar");

        // 단성(monophonic), MIDI 55(G3) 이상의 중고음역
        if (!profile.IsPolyphonic && profile.PitchRangeMin >= 55)
        {
            suggestions.Add("Saxophone");
            suggestions.Add("Flute");
        }

        // 어떤 악보든 키보드 계열은 기본 포함
        if (!suggestions.Contains("Piano"))       suggestions.Add("Piano");
        if (!suggestions.Contains("Synthesizer")) suggestions.Add("Synthesizer");

        return [.. suggestions.Distinct()];
    }

    // ── 서브프로세스 실행 ─────────────────────────────────────────────────────

    private static async Task<(int ExitCode, ScoreProfile? Profile, string Error)> RunRunnerAsync(
        string python, string args,
        IProgress<SheetMusicProgress>? progress,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo(python, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        using var proc = new Process { StartInfo = psi };
        double lastPct = 0;
        ScoreProfile? profile = null;
        var logLines    = new List<string>();
        var stderrLines = new List<string>();
        var lockObj     = new object();

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;

            if (e.Data.StartsWith("PROGRESS:", StringComparison.Ordinal) &&
                double.TryParse(e.Data[9..],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var pct))
            {
                lastPct = pct;
                progress?.Report(new SheetMusicProgress
                    { Phase = "인식 중...", Percent = pct / 100.0 });
            }
            else if (e.Data.StartsWith("PROFILE:", StringComparison.Ordinal))
            {
                try
                {
                    profile = JsonSerializer.Deserialize<ScoreProfile>(
                        e.Data[8..],
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch { /* JSON 파싱 실패 무시 */ }
            }
            else if (e.Data.StartsWith("LOG:", StringComparison.Ordinal))
            {
                var msg = e.Data[4..];
                lock (lockObj) logLines.Add(msg);
                progress?.Report(new SheetMusicProgress
                    { Phase = msg, Percent = lastPct / 100.0 });
            }
        };

        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                lock (lockObj) stderrLines.Add(e.Data);
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync(ct);

        string error;
        lock (lockObj)
        {
            // 오류 메시지: 가장 최근 LOG 줄 1개만 사용.
            // LOG 줄은 진행 상태("이미지 전처리 중...", "OMR 실행 중...") + 최종 오류를 포함하므로
            // 모두 합치면 "오류: 이미지 전처리 중...\nOMR 실행 중..." 같은 오해를 낳음.
            // MISSING 플래그(OEMER_MISSING 등)는 항상 마지막 LOG 줄이라 Contains 검사에 안전.
            var lastLog    = logLines.Count > 0 ? logLines[^1] : null;
            var stderrText = stderrLines.Count > 0
                ? string.Join("\n", stderrLines.TakeLast(5))
                : null;
            error = lastLog ?? stderrText ?? string.Empty;
        }
        return (proc.ExitCode, profile, error);
    }

    private static async Task<(int ExitCode, string Error)> RunSynthesizerAsync(
        string python, string args,
        IProgress<SheetMusicProgress>? progress,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo(python, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        using var proc = new Process { StartInfo = psi };
        double lastPct = 0;
        var logLines    = new List<string>();
        var stderrLines = new List<string>();
        var lockObj     = new object();

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;

            if (e.Data.StartsWith("PROGRESS:", StringComparison.Ordinal) &&
                double.TryParse(e.Data[9..],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var pct))
            {
                lastPct = pct;
                progress?.Report(new SheetMusicProgress
                    { Phase = "합성 중...", Percent = pct / 100.0 });
            }
            else if (e.Data.StartsWith("LOG:", StringComparison.Ordinal))
            {
                var msg = e.Data[4..];
                lock (lockObj) logLines.Add(msg);
                progress?.Report(new SheetMusicProgress
                    { Phase = msg, Percent = lastPct / 100.0 });
            }
        };

        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                lock (lockObj) stderrLines.Add(e.Data);
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync(ct);

        string error;
        lock (lockObj)
        {
            error = logLines.Count > 0
                ? string.Join("\n", logLines.TakeLast(15))
                : string.Join("\n", stderrLines.TakeLast(15));
        }
        return (proc.ExitCode, error);
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public class SheetMusicProgress
{
    public string Phase   { get; set; } = string.Empty;
    public double Percent { get; set; }
}

public class SheetMusicResult
{
    public bool Success          { get; set; }
    public ScoreProfile? Profile { get; set; }
    public string? Error         { get; set; }

    /// <summary>oemer 패키지가 설치되지 않은 경우.</summary>
    public bool IsOmerMissing    { get; set; }
    /// <summary>music21 패키지가 설치되지 않은 경우.</summary>
    public bool IsMusic21Missing { get; set; }
}
