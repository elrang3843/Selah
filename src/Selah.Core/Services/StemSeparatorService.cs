using System.Diagnostics;
using Selah.Core.Models;

namespace Selah.Core.Services;

/// <summary>
/// 음원 분리 엔진 서비스.
/// 1차 프로토타입: Python Demucs CLI를 외부 프로세스로 호출.
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

    private void DetectPython()
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            foreach (var name in new[] { "python", "python3", "python.exe", "python3.exe" })
            {
                var full = Path.Combine(dir, name);
                if (File.Exists(full)) { _pythonPath = full; return; }
            }
        }
        // Windows py launcher
        var py = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "py.exe");
        if (File.Exists(py)) _pythonPath = py;
    }

    public async Task<SeparationResult> SeparateAsync(
        string inputWavPath,
        string outputDir,
        ModelInfo model,
        StemType stemType,
        IProgress<SeparationProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (_pythonPath == null)
            throw new InvalidOperationException("Python이 설치되어 있지 않습니다.\n" +
                "python.org 에서 Python 3.10 이상을 설치해 주세요.");
        if (!model.IsInstalled)
            throw new InvalidOperationException($"모델 '{model.Name}'이 설치되지 않았습니다.\n" +
                "모델 관리자에서 먼저 설치해 주세요.");

        Directory.CreateDirectory(outputDir);

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "demucs_runner.py");
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("demucs_runner.py를 찾을 수 없습니다.", scriptPath);

        var stems = stemType == StemType.TwoStem ? "2" : "4";
        var args = $"\"{scriptPath}\" " +
                   $"--input \"{inputWavPath}\" " +
                   $"--output \"{outputDir}\" " +
                   $"--model {model.Id} " +
                   $"--stems {stems}";

        progress?.Report(new SeparationProgress { Phase = "분리 시작 중...", Percent = 0 });

        int exitCode = await RunSeparationAsync(_pythonPath, args, progress, ct);

        if (exitCode != 0)
        {
            return new SeparationResult
            {
                Success = false,
                Error = $"분리 엔진 종료 코드: {exitCode}"
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
            Success = outputs.Count > 0,
            OutputFiles = outputs,
            OutputDir = outputDir
        };
    }

    private static IEnumerable<string> GetExpectedStems(StemType stemType) =>
        stemType == StemType.TwoStem
            ? ["vocals", "no_vocals"]
            : ["vocals", "drums", "bass", "other"];

    public static IReadOnlyList<string> StemKeys(StemType stemType) =>
        GetExpectedStems(stemType).ToList();

    private static async Task<int> RunSeparationAsync(
        string python, string args,
        IProgress<SeparationProgress>? progress,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo(python, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = new Process { StartInfo = psi };

        double lastPct = 0;

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
                {
                    Phase = "분리 중...",
                    Percent = pct / 100.0
                });
            }
            else if (e.Data.StartsWith("STEM:", StringComparison.Ordinal))
            {
                // Format: STEM:<key>=<absolute-path>
                int eq = e.Data.IndexOf('=');
                if (eq > 5)
                    progress?.Report(new SeparationProgress
                    {
                        Phase   = "분리 중...",
                        Percent = lastPct / 100.0,
                        StemKey  = e.Data[5..eq],
                        StemPath = e.Data[(eq + 1)..],
                    });
            }
        };

        // CRITICAL: drain stderr asynchronously — without this, when demucs writes
        // enough debug output to fill the OS pipe buffer the process deadlocks.
        proc.ErrorDataReceived += (_, _) => { };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync(ct);
        return proc.ExitCode;
    }
}

public class SeparationProgress
{
    public string Phase   { get; set; } = string.Empty;
    public double Percent { get; set; }
    /// <summary>Stem key (e.g. "vocals", "drums"). Non-null when a stem WAV is ready.</summary>
    public string? StemKey  { get; set; }
    /// <summary>Absolute path to the ready stem WAV file.</summary>
    public string? StemPath { get; set; }
}

public class SeparationResult
{
    public bool Success { get; set; }
    public Dictionary<string, string> OutputFiles { get; set; } = new();
    public string OutputDir { get; set; } = string.Empty;
    public string? Error { get; set; }
}
