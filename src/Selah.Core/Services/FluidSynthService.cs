namespace Selah.Core.Services;

/// <summary>
/// FluidSynth 탐지 서비스.
/// FFmpegService와 동일한 패턴으로 PATH 및 번들·표준 경로를 검색합니다.
///
/// 합성 경로 (우선순위 순):
///   1. Python fluidsynth 패키지 (pip install fluidsynth) — libfluidsynth DLL 필요
///   2. fluidsynth.exe 실행 파일 (fluidsynth.org 또는 PATH에 설치)
///
/// SoundFont(.sf2) 배치 위치 (우선순위 순):
///   %AppData%\Selah\soundfonts\*.sf2
///   앱 번들\soundfonts\*.sf2
///   C:\Program Files\FluidSynth\*.sf2
///   C:\soundfonts\*.sf2
/// </summary>
public class FluidSynthService
{
    private string? _fluidsynthPath;
    private string? _soundFontPath;
    private bool    _pythonPkgAvailable;

    /// <summary>FluidSynth(Python 패키지 또는 exe)와 SoundFont 모두 탐지된 경우 true.</summary>
    public bool IsAvailable      => IsFluidSynthFound && _soundFontPath != null;
    /// <summary>Python fluidsynth 패키지 또는 fluidsynth.exe 중 하나라도 사용 가능한 경우 true.</summary>
    public bool IsFluidSynthFound => _pythonPkgAvailable || _fluidsynthPath != null;
    public bool IsSoundFontFound  => _soundFontPath != null;

    /// <summary>fluidsynth.exe 경로 (없으면 null — Python 패키지로 대체 가능).</summary>
    public string? FluidSynthPath => _fluidsynthPath;
    public string? SoundFontPath  => _soundFontPath;

    public void Detect()
    {
        _fluidsynthPath     = FindFluidSynth();
        _soundFontPath      = FindSoundFont();
        _pythonPkgAvailable = ModelManagerService.IsFluidSynthPkgInstalled();
    }

    // ── 실행 파일 탐지 ────────────────────────────────────────────────────────

    private static string? FindFluidSynth()
    {
        // 1. PATH 직접 스캔
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            foreach (var name in new[] { "fluidsynth.exe", "fluidsynth" })
            {
                var full = Path.Combine(dir, name);
                if (File.Exists(full)) return full;
            }
        }

        // 2. 앱 번들 및 표준 설치 경로
        var appBase  = AppContext.BaseDirectory;
        var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        var candidates = new[]
        {
            Path.Combine(appBase,      "fluidsynth", "bin", "fluidsynth.exe"),
            Path.Combine(appBase,      "fluidsynth", "fluidsynth.exe"),
            Path.Combine(progFiles,    "FluidSynth",  "bin", "fluidsynth.exe"),
            Path.Combine(progFilesX86, "FluidSynth",  "bin", "fluidsynth.exe"),
            @"C:\tools\fluidsynth\bin\fluidsynth.exe",   // Chocolatey C:\tools\ 레이아웃
            @"C:\FluidSynth\bin\fluidsynth.exe",
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    // ── SoundFont(.sf2) 탐지 ─────────────────────────────────────────────────

    private static string? FindSoundFont()
    {
        var appData   = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appBase   = AppContext.BaseDirectory;
        var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        // 검색 폴더 목록 (우선순위 순)
        var searchDirs = new[]
        {
            Path.Combine(appData,   "Selah", "soundfonts"),
            Path.Combine(appData,   "Selah", "fluidsynth"),
            Path.Combine(appBase,   "soundfonts"),
            Path.Combine(appBase,   "fluidsynth"),
            Path.Combine(progFiles, "FluidSynth"),
            @"C:\soundfonts",
            @"C:\FluidSynth",
        };

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;
            var sf2 = Directory
                .EnumerateFiles(dir, "*.sf2", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (sf2 != null) return sf2;
        }
        return null;
    }

    // ── 유틸리티 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 사용자 AppData SoundFont 저장 디렉터리를 반환합니다 (없으면 생성).
    /// SF2 파일을 이 폴더에 복사하면 자동으로 탐지됩니다.
    /// </summary>
    public static string GetSoundFontsDir()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Selah", "soundfonts");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
