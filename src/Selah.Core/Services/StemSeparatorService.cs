using System.Diagnostics;
using Selah.Core.Models;

namespace Selah.Core.Services;

/// <summary>
/// 음원 분리 엔진 서비스.
///   OnnxRuntime  → onnx_runner.py  (ONNX Runtime + FFmpeg)
///   PythonCLI    → demucs_runner.py (레거시 Demucs CLI)
/// </summary>
public class StemSeparatorService
{
    private readonly ModelManagerService _modelManager;
    private string? _pythonPath;

    public bool IsPythonAvailable => _pythonPath != null;

    public StemSeparatorService(ModelManagerService modelManager)
    {
        _modelManager = modelManager;
        DetectPython();
    }

    private void DetectPython() =>
        _pythonPath = ModelManagerService.FindPython();

    public async Task<SeparationResult> SeparateAsync(
        string inputWavPath,
        string outputDir,
        ModelInfo model,
        StemType stemType,
        IProgress<SeparationProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (_pythonPath == null)
            throw new InvalidOperationException(
                "Python이 설치되어 있지 않습니다.\n" +
                "python.org 에서 Python 3.10 이상을 설치해 주세요.");

        if (!model.IsInstalled)
            throw new InvalidOperationException(
                $"모델 '{model.Name}'이 설치되지 않았습니다.\n" +
                "모델 관리자에서 먼저 설치해 주세요.");

        Directory.CreateDirectory(outputDir);

        // ── 스크립트 선택 ───────────────────────────────────────
        var scriptName = model.Engine == ModelEngine.OnnxRuntime
            ? "onnx_runner.py"
            : "demucs_runner.py";

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", scriptName);
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException($"{scriptName}를 찾을 수 없습니다.", scriptPath);

        var stems = stemType == StemType.TwoStem ? "2" : "4";
        var args  = $"\"{scriptPath}\" " +
                    $"--input \"{inputWavPath}\" " +
                    $"--output \"{outputDir}\" " +
                    $"--model {model.Id} " +
                    $"--stems {stems}";

        progress?.Report(new SeparationProgress { Phase = "분리 시작 중...", Percent = 0 });

        var (exitCode, errorDetail) = await RunSeparationAsync(
            _pythonPath, args, model, progress, ct);

        if (exitCode != 0)
        {
            // OnnxRuntime 전용 오류
            bool onnxRuntimeMissing =
                errorDetail.Contains("ONNX_RUNTIME_MISSING", StringComparison.Ordinal);
            bool onnxModelMissing =
                errorDetail.Contains("ONNX_MODEL_MISSING:", StringComparison.Ordinal);

            // PythonCLI 레거시 오류 (demucs_runner.py 경로)
            bool torchcodecMissing =
                errorDetail.Contains("TORCHCODEC_MISSING", StringComparison.Ordinal);
            bool torchcodecBroken =
                errorDetail.Contains("TORCHCODEC_BROKEN", StringComparison.Ordinal);

            return new SeparationResult
            {
                Success              = false,
                IsOnnxRuntimeMissing = onnxRuntimeMissing,
                IsOnnxModelMissing   = onnxModelMissing,
                IsTorchCodecMissing  = torchcodecMissing,
                IsTorchCodecBroken   = torchcodecBroken,
                Error = string.IsNullOrWhiteSpace(errorDetail)
                    ? $"분리 엔진 종료 코드: {exitCode}"
                    : $"분리 엔진 오류 (코드 {exitCode}):\n\n{errorDetail}"
            };
        }

        var outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var stem in GetExpectedStems(stemType))
        {
            var stemFile = Path.Combine(outputDir, stem + ".wav");
            if (File.Exists(stemFile)) outputs[stem] = stemFile;
        }

        progress?.Report(new SeparationProgress { Phase = "완료", Percent = 1.0 });

        return new SeparationResult
        {
            Success    = outputs.Count > 0,
            OutputFiles = outputs,
            OutputDir  = outputDir
        };
    }

    private static IEnumerable<string> GetExpectedStems(StemType stemType) =>
        stemType == StemType.TwoStem
            ? ["vocals", "no_vocals"]
            : ["vocals", "drums", "bass", "other"];

    public static IReadOnlyList<string> StemKeys(StemType stemType) =>
        GetExpectedStems(stemType).ToList();

    private async Task<(int ExitCode, string ErrorDetail)> RunSeparationAsync(
        string python,
        string args,
        ModelInfo model,
        IProgress<SeparationProgress>? progress,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo(python, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        // ONNX 모델 경로 환경 변수 전달
        if (model.Engine == ModelEngine.OnnxRuntime)
            psi.Environment["SELAH_MODELS_DIR"] = _modelManager.ModelsDir;

        using var proc = new Process { StartInfo = psi };

        double lastPct = 0;
        var logLines  = new List<string>();
        var stderrLines = new List<string>();
        var logLock   = new object();

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;

            if (e.Data.StartsWith("PROGRESS:", StringComparison.Ordinal) &&
                double.TryParse(e.Data[9..],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var pct))
            {
                lastPct = pct;
                progress?.Report(new SeparationProgress
                    { Phase = "분리 중...", Percent = pct / 100.0 });
            }
            else if (e.Data.StartsWith("STEM:", StringComparison.Ordinal))
            {
                int eq = e.Data.IndexOf('=');
                if (eq > 5)
                    progress?.Report(new SeparationProgress
                    {
                        Phase    = "분리 중...",
                        Percent  = lastPct / 100.0,
                        StemKey  = e.Data[5..eq],
                        StemPath = e.Data[(eq + 1)..],
                    });
            }
            else if (e.Data.StartsWith("LOG:", StringComparison.Ordinal))
            {
                var msg = e.Data[4..];
                lock (logLock) logLines.Add(msg);
                progress?.Report(new SeparationProgress
                    { Phase = msg, Percent = lastPct / 100.0 });
            }
        };

        // stderr 캡처 — 파이프 버퍼 교착 방지 + 오류 진단용
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (logLock) stderrLines.Add(e.Data);
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync(ct);

        string errorDetail;
        lock (logLock)
        {
            errorDetail = logLines.Count > 0
                ? string.Join("\n", logLines.TakeLast(15))
                : string.Join("\n", stderrLines.TakeLast(15));
        }
        return (proc.ExitCode, errorDetail);
    }
}

// ── 공용 DTO ─────────────────────────────────────────────────────

public class SeparationProgress
{
    public string Phase   { get; set; } = string.Empty;
    public double Percent { get; set; }
    /// <summary>완료된 스템 키 (예: "vocals", "drums"). 준비 완료 시만 non-null.</summary>
    public string? StemKey  { get; set; }
    /// <summary>준비 완료된 스템 WAV 절대 경로.</summary>
    public string? StemPath { get; set; }
}

public class SeparationResult
{
    public bool Success { get; set; }
    public Dictionary<string, string> OutputFiles { get; set; } = new();
    public string OutputDir { get; set; } = string.Empty;
    public string? Error { get; set; }

    // ── OnnxRuntime 오류 ──
    /// <summary>onnxruntime 패키지가 설치되지 않은 경우.</summary>
    public bool IsOnnxRuntimeMissing { get; set; }
    /// <summary>ONNX 모델 파일(.onnx)이 존재하지 않는 경우.</summary>
    public bool IsOnnxModelMissing   { get; set; }

    // ── PythonCLI 레거시 오류 ──
    /// <summary>TorchCodec 패키지 미설치 (demucs_runner.py 경로).</summary>
    public bool IsTorchCodecMissing { get; set; }
    /// <summary>TorchCodec DLL 로드 실패 (demucs_runner.py 경로).</summary>
    public bool IsTorchCodecBroken  { get; set; }
}
