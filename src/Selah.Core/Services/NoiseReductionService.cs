using System.Diagnostics;

namespace Selah.Core.Services;

/// <summary>
/// 노이즈 감소 서비스 — noise_reducer.py (noisereduce 라이브러리 기반 스펙트럴 게이팅)를 실행합니다.
/// 별도 모델 파일 없이 pip install noisereduce 만으로 사용할 수 있습니다.
/// </summary>
public class NoiseReductionService
{
    private readonly string? _pythonPath;

    public bool IsPythonAvailable => _pythonPath != null;

    public NoiseReductionService()
    {
        _pythonPath = ModelManagerService.FindPython();
    }

    /// <param name="inputWavPath">소스 WAV 파일 경로 (16-bit PCM)</param>
    /// <param name="outputWavPath">출력 WAV 파일 경로</param>
    /// <param name="strength">노이즈 감소 강도 0.0–1.0 (기본값 0.75)</param>
    /// <param name="stationary">정적 노이즈 모드 — 일정한 험/히스 소음에 더 빠름</param>
    public async Task<NoiseReductionResult> ReduceNoiseAsync(
        string inputWavPath,
        string outputWavPath,
        double strength = 0.75,
        bool stationary = false,
        IProgress<NoiseReductionProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (_pythonPath == null)
            throw new InvalidOperationException("Python이 설치되어 있지 않습니다.");

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "noise_reducer.py");
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("noise_reducer.py를 찾을 수 없습니다.", scriptPath);

        Directory.CreateDirectory(Path.GetDirectoryName(outputWavPath)!);

        var strengthStr = strength.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        var args = $"\"{scriptPath}\" " +
                   $"--input \"{inputWavPath}\" " +
                   $"--output \"{outputWavPath}\" " +
                   $"--strength {strengthStr}" +
                   (stationary ? " --stationary" : "");

        var psi = new ProcessStartInfo(_pythonPath, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        using var proc = new Process { StartInfo = psi };

        double lastPct   = 0;
        var logLines     = new List<string>();
        var stderrLines  = new List<string>();
        var logLock      = new object();

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;

            if (e.Data.StartsWith("PROGRESS:", StringComparison.Ordinal) &&
                double.TryParse(e.Data[9..],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var pct))
            {
                lastPct = pct;
                progress?.Report(new NoiseReductionProgress
                    { Phase = "처리 중...", Percent = pct / 100.0 });
            }
            else if (e.Data.StartsWith("LOG:", StringComparison.Ordinal))
            {
                var msg = e.Data[4..];
                lock (logLock) logLines.Add(msg);
                progress?.Report(new NoiseReductionProgress
                    { Phase = msg, Percent = lastPct / 100.0 });
            }
        };

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

        bool noiseReduceMissing = errorDetail.Contains("NOISEREDUCE_MISSING", StringComparison.Ordinal);
        bool outputExists       = File.Exists(outputWavPath);

        return new NoiseReductionResult
        {
            Success              = proc.ExitCode == 0 && outputExists,
            OutputPath           = outputExists ? outputWavPath : null,
            IsNoiseReduceMissing = noiseReduceMissing,
            Error = proc.ExitCode != 0
                ? (string.IsNullOrWhiteSpace(errorDetail)
                    ? $"노이즈 감소 종료 코드: {proc.ExitCode}"
                    : errorDetail)
                : null
        };
    }
}

public class NoiseReductionProgress
{
    public string Phase   { get; set; } = string.Empty;
    public double Percent { get; set; }
}

public class NoiseReductionResult
{
    public bool    Success              { get; set; }
    public string? OutputPath           { get; set; }
    /// <summary>noisereduce Python 패키지가 설치되지 않은 경우 true.</summary>
    public bool    IsNoiseReduceMissing { get; set; }
    public string? Error                { get; set; }
}
