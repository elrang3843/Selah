using System.Diagnostics;
using System.Net.Http;
using Selah.Core.Models;

namespace Selah.Core.Services;

/// <summary>
/// AI 모델 카탈로그 관리 및 설치/다운로드 서비스.
/// 모델 파일은 %AppData%\Selah\models\ 에 저장됩니다.
///
/// ── 라이선스 고지 ───────────────────────────────────────────────
/// htdemucs / htdemucs_ft 모델 가중치
///     MIT License — Copyright (C) Meta AI Research
///     원본: https://github.com/facebookresearch/demucs
///
/// ONNX 내보내기 (MrCitron/demucs-v4-onnx)
///     HuggingFace: https://huggingface.co/MrCitron/demucs-v4-onnx
///     가중치 MIT 라이선스가 적용되는 것으로 간주합니다.
///
/// ONNX Runtime   : MIT License (Microsoft Corporation)
/// FFmpeg         : LGPL v2.1+  (https://ffmpeg.org/legal.html)
/// numpy / scipy  : BSD 3-Clause
/// ────────────────────────────────────────────────────────────────
/// </summary>
public class ModelManagerService
{
    private readonly string _modelsDir;
    private List<ModelInfo> _catalog = new();

    public string ModelsDir => _modelsDir;

    // HuggingFace 모델 저장소 기준 URL
    private const string HfBase =
        "https://huggingface.co/MrCitron/demucs-v4-onnx/resolve/main/";

    public ModelManagerService()
    {
        _modelsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Selah", "models");
        Directory.CreateDirectory(_modelsDir);
        BuildCatalog();
        RefreshInstallStatus();
    }

    public IReadOnlyList<ModelInfo> GetCatalog() => _catalog.AsReadOnly();

    public ModelInfo? GetById(string id) =>
        _catalog.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    // ── 카탈로그 ─────────────────────────────────────────────────

    private void BuildCatalog()
    {
        _catalog = new List<ModelInfo>
        {
            // ── MDX-Net (audio-separator) — 모든 음역대 보컬 특화 ──────────────
            new()
            {
                Id            = "uvr-mdx-net-voc-ft",
                Name          = "UVR MDX-NET Vocal FT",
                ModelFilename = "UVR-MDX-NET-Voc_FT.onnx",
                Description   =
                    "MDX-Net 기반 보컬/반주 2-stem 분리 모델.\n" +
                    "남성·여성·어린이·비서양권 창법 등 모든 음역대의 목소리를 인식합니다.\n" +
                    "Demucs보다 보컬 커버리지가 넓고 분리 정확도가 높습니다.\n" +
                    "audio-separator 패키지로 실행 — ONNX Runtime 별도 설치 불필요.",
                License    = "MIT",
                LicenseUrl = "https://github.com/nomadkaraoke/python-audio-separator/blob/main/LICENSE",
                SourceUrl  = "https://github.com/nomadkaraoke/python-audio-separator",
                Version    = "MDX-NET-Voc_FT (UVR / Anjok07)",
                ModelType  = ModelType.VocalSeparation,
                StemType   = StemType.TwoStem,
                Engine     = ModelEngine.AudioSeparator,
                SizeBytes  = 65L * 1024 * 1024,
                SuitableForPublicWorship = true,
                WorshipNote =
                    "모델 가중치: MIT 라이선스 (Anjok07 / Ultimate Vocal Remover)\n" +
                    "패키지: MIT 라이선스 (nomadkaraoke/python-audio-separator)\n\n" +
                    "처리 대상 음원(찬양곡)의 저작권은 별도로 확인하세요.\n" +
                    "CCL 또는 원저작자 허락 여부를 먼저 확인하십시오.",
                Tags = new List<string> { "추천", "2-stem", "보컬 특화", "모든 음역대", "MIT" }
            },
            new()
            {
                Id          = "htdemucs",
                Name        = "Hybrid Transformer Demucs",
                Description =
                    "Meta AI의 htdemucs 4-stem 분리 모델 (ONNX 버전).\n" +
                    "드럼·베이스·기타·보컬 4-stem 및 보컬/반주 2-stem 분리 지원.\n" +
                    "ONNX Runtime으로 실행 — PyTorch·TorchAudio 불필요.\n" +
                    "CPU 전용으로도 동작하지만 처리 시간이 걸릴 수 있습니다.",
                License    = "MIT",
                LicenseUrl = "https://github.com/facebookresearch/demucs/blob/main/LICENSE",
                SourceUrl  = "https://huggingface.co/MrCitron/demucs-v4-onnx",
                Version    = "4.0 (ONNX export by MrCitron)",
                ModelType  = ModelType.StemSeparation,
                StemType   = StemType.FourStem,
                Engine     = ModelEngine.OnnxRuntime,
                SizeBytes  = 303L * 1024 * 1024,
                DownloadUrl = HfBase + "htdemucs.onnx",
                SuitableForPublicWorship = true,
                WorshipNote =
                    "모델 가중치: MIT 라이선스 (Meta AI Research)\n" +
                    "ONNX 내보내기: MrCitron (HuggingFace)\n" +
                    "런타임: ONNX Runtime (MIT, Microsoft)\n" +
                    "오디오 I/O: FFmpeg (LGPL v2.1+)\n\n" +
                    "처리 대상 음원(찬양곡)의 저작권은 별도로 확인하세요.\n" +
                    "CCL 또는 원저작자 허락 여부를 먼저 확인하십시오.",
                Tags = new List<string> { "추천", "4-stem", "ONNX", "MIT" }
            },
            new()
            {
                Id          = "htdemucs_ft",
                Name        = "Hybrid Transformer Demucs (Fine-tuned)",
                Description =
                    "htdemucs의 Fine-tuning 버전 (ONNX).\n" +
                    "일부 장르에서 분리 품질 향상.\n" +
                    "용량이 htdemucs와 동일하지만 품질이 더 높습니다.",
                License    = "MIT",
                LicenseUrl = "https://github.com/facebookresearch/demucs/blob/main/LICENSE",
                SourceUrl  = "https://huggingface.co/MrCitron/demucs-v4-onnx",
                Version    = "4.0 ft (ONNX export by MrCitron)",
                ModelType  = ModelType.StemSeparation,
                StemType   = StemType.FourStem,
                Engine     = ModelEngine.OnnxRuntime,
                SizeBytes  = 303L * 1024 * 1024,
                DownloadUrl = HfBase + "htdemucs_ft.onnx",
                SuitableForPublicWorship = true,
                WorshipNote =
                    "모델 가중치: MIT 라이선스 (Meta AI Research)\n" +
                    "ONNX 내보내기: MrCitron (HuggingFace)\n" +
                    "런타임: ONNX Runtime (MIT, Microsoft)\n" +
                    "오디오 I/O: FFmpeg (LGPL v2.1+)\n\n" +
                    "처리 대상 음원의 저작권은 사용자가 직접 확인하세요.",
                Tags = new List<string> { "4-stem", "고품질", "ONNX", "MIT" }
            }
        };
    }

    // ── 설치 상태 확인 ────────────────────────────────────────────

    public void RefreshInstallStatus()
    {
        bool onnxRuntimeOk    = CheckPythonPackage("onnxruntime");
        bool audioSeparatorOk = CheckPythonPackage("audio_separator");

        foreach (var model in _catalog)
        {
            switch (model.Engine)
            {
                case ModelEngine.OnnxRuntime:
                {
                    var onnxFile = Path.Combine(_modelsDir, model.Id + ".onnx");
                    model.IsInstalled = onnxRuntimeOk && File.Exists(onnxFile);
                    model.LocalPath   = model.IsInstalled ? onnxFile : null;
                    break;
                }
                case ModelEngine.AudioSeparator:
                {
                    // audio-separator 패키지만 설치되면 사용 가능.
                    // 모델 파일은 첫 실행 시 자동 다운로드됩니다.
                    model.IsInstalled = audioSeparatorOk;
                    model.LocalPath   = audioSeparatorOk
                        ? Path.Combine(GetAudioSeparatorCacheDir(),
                                       model.ModelFilename ?? "")
                        : null;
                    break;
                }
            }
        }
    }

    public bool IsOnnxRuntimeInstalled()    => CheckPythonPackage("onnxruntime");
    public bool IsAudioSeparatorInstalled() => CheckPythonPackage("audio_separator");

    /// <summary>oemer OMR 패키지 설치 여부를 확인합니다.</summary>
    public bool IsOmerInstalled()   => CheckPythonPackage("oemer");
    /// <summary>music21 악보 분석 패키지 설치 여부를 확인합니다.</summary>
    public bool IsMusic21Installed() => CheckPythonPackage("music21");
    /// <summary>mido MIDI 조작 패키지 설치 여부를 확인합니다.</summary>
    public bool IsMidoInstalled()   => CheckPythonPackage("mido");
    /// <summary>fluidsynth Python 패키지 설치 여부를 확인합니다 (pip install fluidsynth).</summary>
    public static bool IsFluidSynthPkgInstalled() => CheckPythonPackage("fluidsynth");

    /// <summary>audio-separator 모델 캐시 폴더 경로 (플랫폼 무관).</summary>
    public static string GetAudioSeparatorCacheDir() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache", "audio-separator");

    private static bool CheckPythonPackage(string package)
    {
        try
        {
            var psi = new ProcessStartInfo("python",
                $"-c \"import {package}; print('ok')\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5_000);
            return proc.ExitCode == 0 && output.Contains("ok");
        }
        catch { return false; }
    }

    // ── Python 탐지 ───────────────────────────────────────────────

    /// <summary>
    /// 시스템 PATH 및 Windows py.exe 런처에서 Python 실행 파일을 찾습니다.
    /// 검색 순서:
    ///   1. PATH 직접 스캔 — Windows Store App Execution Alias 제외
    ///      (%LOCALAPPDATA%\Microsoft\WindowsApps\ 는 CreateNoWindow=true 환경에서
    ///       9009를 반환하는 스텁이므로 건너뜁니다)
    ///   2. Windows py.exe 런처 (C:\Windows\py.exe)
    ///   3. cmd.exe 셸 폴백 — Windows Store Python 포함, 실제 .exe 경로 반환
    /// </summary>
    internal static string? FindPython()
    {
        // Windows Store 스텁 디렉터리 — CreateNoWindow 환경에서 9009 반환
        var windowsAppsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps");

        // ── 1. PATH 직접 스캔 ────────────────────────────────────
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            if (dir.StartsWith(windowsAppsDir, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var name in new[] { "python", "python3", "python.exe", "python3.exe" })
            {
                var full = Path.Combine(dir, name);
                if (File.Exists(full)) return full;
            }
        }

        // ── 2. Windows py.exe 런처 ───────────────────────────────
        var py = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "py.exe");
        if (File.Exists(py)) return py;

        // ── 3. cmd.exe 셸 폴백 (Windows Store Python 포함) ──────
        //    cmd.exe는 App Execution Alias를 올바르게 처리하며,
        //    sys.executable은 스텁이 아닌 실제 .exe 절대 경로를 반환합니다.
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var psi = new ProcessStartInfo(
                    "cmd.exe",
                    "/c python -c \"import sys; print(sys.executable)\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    var exePath = p.StandardOutput.ReadLine()?.Trim();
                    p.WaitForExit(5_000);
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        return exePath;
                }
            }
            catch { /* Python 없음 */ }
        }

        return null;
    }

    // ── pip 런타임 설치 ───────────────────────────────────────────

    /// <summary>
    /// ONNX Runtime 및 의존 패키지를 "python -m pip"으로 설치합니다.
    /// (pip 단독 실행 파일 대신 python -m pip 사용 → Windows PATH 문제 우회)
    /// </summary>
    public async Task InstallRuntimeAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var python = FindPython()
            ?? throw new InvalidOperationException(
                "Python을 찾을 수 없습니다.\n" +
                "python.org에서 Python 3.10 이상을 설치하고 PATH에 추가하세요.");
        await RunPipInstallAsync(python, "onnxruntime numpy scipy", progress, ct);
    }

    // ── 모델 다운로드 ─────────────────────────────────────────────

    /// <summary>
    /// HuggingFace에서 .onnx 모델 파일을 다운로드합니다.
    /// progress: "BYTES:received/total" 형식 또는 메시지 문자열
    /// </summary>
    public async Task DownloadModelAsync(
        ModelInfo model,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(model.DownloadUrl))
            throw new InvalidOperationException($"모델 '{model.Id}'의 다운로드 URL이 없습니다.");

        var destPath = Path.Combine(_modelsDir, model.Id + ".onnx");
        var tempPath = destPath + ".tmp";

        progress?.Report($"다운로드 시작: {model.Name} ({model.SizeDisplay})");

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Selah/1.0");
        client.Timeout = TimeSpan.FromHours(2);

        using var response = await client.GetAsync(
            model.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? model.SizeBytes;

        await using var src  = await response.Content.ReadAsStreamAsync(ct);
        await using var dest = File.Create(tempPath);

        var buffer   = new byte[81_920];
        long received = 0;
        int  read;

        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, read), ct);
            received += read;

            if (totalBytes > 0)
            {
                double pct = received * 100.0 / totalBytes;
                progress?.Report(
                    $"BYTES:{received}/{totalBytes}  ({pct:F0}%)");
            }
        }

        await dest.FlushAsync(ct);
        dest.Close();

        // 완료 후 임시 파일 → 최종 파일로 원자 이동
        if (File.Exists(destPath)) File.Delete(destPath);
        File.Move(tempPath, destPath);

        progress?.Report($"✓ 다운로드 완료: {Path.GetFileName(destPath)}");
        RefreshInstallStatus();
    }

    // ── 통합 셋업 (런타임 + 모델 다운로드) ────────────────────────

    /// <summary>
    /// 엔진 유형에 따라 적절한 설치 흐름을 실행합니다.
    ///   AudioSeparator → pip install audio-separator[cpu]
    ///   OnnxRuntime    → pip install onnxruntime numpy scipy → 모델 다운로드
    /// </summary>
    public async Task SetupModelAsync(
        ModelInfo model,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (model.Engine == ModelEngine.AudioSeparator)
        {
            await SetupAudioSeparatorModelAsync(model, progress, ct);
            return;
        }

        // ── OnnxRuntime 기존 흐름 ──────────────────────────────────────────────

        // Step 1: pip install onnxruntime numpy scipy
        if (!IsOnnxRuntimeInstalled())
        {
            progress?.Report("─── Step 1: ONNX Runtime 설치 ───");
            await InstallRuntimeAsync(progress, ct);
            progress?.Report(string.Empty);
        }
        else
        {
            progress?.Report("✓ ONNX Runtime 이미 설치됨 — 건너뜁니다.");
        }

        // Step 2: 모델 파일 다운로드
        var onnxFile = Path.Combine(_modelsDir, model.Id + ".onnx");
        if (!File.Exists(onnxFile))
        {
            progress?.Report($"─── Step 2: {model.Name} 다운로드 ───");
            await DownloadModelAsync(model, progress, ct);
        }
        else
        {
            progress?.Report($"✓ {model.Name} 이미 다운로드됨 — 건너뜁니다.");
        }

        RefreshInstallStatus();
    }

    /// <summary>
    /// audio-separator 엔진 설치 흐름.
    ///   Step 1: pip install "audio-separator[cpu]"
    ///   Step 2: 안내 메시지 (모델은 첫 분리 실행 시 자동 다운로드됨)
    /// </summary>
    private async Task SetupAudioSeparatorModelAsync(
        ModelInfo model,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var python = FindPython()
            ?? throw new InvalidOperationException(
                "Python을 찾을 수 없습니다.\n" +
                "python.org에서 Python 3.10 이상을 설치하고 PATH에 추가하세요.");

        // Step 1: audio-separator 설치
        if (!IsAudioSeparatorInstalled())
        {
            progress?.Report("─── Step 1: audio-separator 설치 ───");
            await InstallAudioSeparatorAsync(python, progress, ct);
            progress?.Report(string.Empty);
        }
        else
        {
            progress?.Report("✓ audio-separator 이미 설치됨 — 건너뜁니다.");
        }

        // Step 2: 안내
        var modelFilename = model.ModelFilename ?? "";
        var cacheFile = Path.Combine(GetAudioSeparatorCacheDir(), modelFilename);
        if (File.Exists(cacheFile))
            progress?.Report($"✓ {model.Name} 모델 캐시 확인됨: {cacheFile}");
        else
            progress?.Report(
                $"ℹ {model.Name} 모델({modelFilename})은 처음 분리 실행 시 자동 다운로드됩니다. (~65 MB)");

        RefreshInstallStatus();
    }

    /// <summary>pip install audio-separator[cpu]를 실행합니다.</summary>
    public Task InstallAudioSeparatorAsync(
        string python,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
        => RunPipInstallAsync(python, "\"audio-separator[cpu]\"", progress, ct);

    // ── OMR 패키지 설치 ──────────────────────────────────────────────

    /// <summary>
    /// 악보 인식(OMR)에 필요한 Python 패키지를 설치합니다.
    ///   pip install oemer music21 mido Pillow scipy
    /// </summary>
    public async Task InstallOmerPackagesAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var python = FindPython()
            ?? throw new InvalidOperationException(
                "Python을 찾을 수 없습니다.\n" +
                "python.org에서 Python 3.10 이상을 설치하고 PATH에 추가하세요.");
        await RunPipInstallAsync(python, "oemer music21 mido Pillow scipy", progress, ct);
    }

    // ── 공통 pip 실행 헬퍼 ───────────────────────────────────────────

    private static async Task RunPipInstallAsync(
        string python, string packages,
        IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report($"pip install {packages}");
        var psi = new ProcessStartInfo(python, $"-m pip install {packages}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };
        using var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) progress?.Report(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) progress?.Report(e.Data); };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0)
            throw new Exception($"pip install 실패 (코드 {proc.ExitCode})");
    }

    // ── 레거시 호환 ───────────────────────────────────────────────

    /// <summary>
    /// 하위 호환용: 첫 번째 ONNX 모델을 설치합니다.
    /// </summary>
    public Task InstallDemucsAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var model = _catalog.FirstOrDefault(m => m.Engine == ModelEngine.OnnxRuntime);
        return model != null
            ? SetupModelAsync(model, progress, ct)
            : Task.CompletedTask;
    }
}
