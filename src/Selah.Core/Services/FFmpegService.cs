using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Selah.Core.Services;

/// <summary>
/// FFmpeg/FFprobe 외부 프로세스 래퍼.
/// FFmpeg 없이도 WAV/FLAC은 기본 지원, 나머지는 FFmpeg 필요.
/// </summary>
public partial class FFmpegService
{
    private string? _ffmpegPath;
    private string? _ffprobePath;

    public bool IsAvailable => _ffmpegPath != null;
    public string? FFmpegPath => _ffmpegPath;

    public void Detect()
    {
        _ffmpegPath = FindExecutable("ffmpeg");
        _ffprobePath = FindExecutable("ffprobe");
    }

    private static string? FindExecutable(string name)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            foreach (var suffix in new[] { ".exe", "" })
            {
                var full = Path.Combine(dir, name + suffix);
                if (File.Exists(full)) return full;
            }
        }

        // 앱 번들 내 ffmpeg 폴더
        var appBase = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appBase, "ffmpeg", "bin", name + ".exe"),
            Path.Combine(appBase, "ffmpeg", name + ".exe"),
            Path.Combine(appBase, name + ".exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         "ffmpeg", "bin", name + ".exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>입력 파일의 오디오 정보를 조회합니다.</summary>
    public async Task<AudioFileInfo> ProbeAsync(string inputPath,
        CancellationToken ct = default)
    {
        if (_ffprobePath == null)
            throw new InvalidOperationException("FFprobe가 없습니다. WAV 파일만 지원됩니다.");

        var args = $"-v quiet -print_format json -show_streams \"{inputPath}\"";
        var output = await RunProcessAsync(_ffprobePath, args, ct);
        return ParseProbeJson(output, inputPath);
    }

    /// <summary>입력 파일을 PCM WAV로 변환합니다.</summary>
    public async Task DecodeToWavAsync(
        string inputPath,
        string outputWavPath,
        int sampleRate,
        int channels = 2,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (_ffmpegPath == null)
            throw new InvalidOperationException("FFmpeg가 없습니다. WAV 파일만 지원됩니다.");

        Directory.CreateDirectory(Path.GetDirectoryName(outputWavPath)!);

        // 진행률 확인용 duration 먼저 조회
        double duration = 0;
        if (progress != null && _ffprobePath != null)
        {
            try
            {
                var info = await ProbeAsync(inputPath, ct);
                duration = info.DurationSeconds;
            }
            catch { /* 무시 */ }
        }

        var args = $"-y -i \"{inputPath}\" -ar {sampleRate} -ac {channels} " +
                   $"-sample_fmt s16 \"{outputWavPath}\"";

        if (duration > 0 && progress != null)
            await RunProcessWithProgressAsync(_ffmpegPath, args, duration, progress, ct);
        else
            await RunProcessAsync(_ffmpegPath, args, ct);
    }

    /// <summary>영상 파일에서 오디오를 추출합니다.</summary>
    public async Task ExtractAudioAsync(
        string videoPath,
        string outputWavPath,
        int sampleRate,
        int channels = 2,
        CancellationToken ct = default)
    {
        if (_ffmpegPath == null)
            throw new InvalidOperationException("FFmpeg가 없습니다.");

        Directory.CreateDirectory(Path.GetDirectoryName(outputWavPath)!);
        var args = $"-y -i \"{videoPath}\" -vn -ar {sampleRate} -ac {channels} " +
                   $"-sample_fmt s16 \"{outputWavPath}\"";
        await RunProcessAsync(_ffmpegPath, args, ct);
    }

    /// <summary>WAV를 다른 포맷으로 인코딩합니다 (flac, mp3 등).</summary>
    public async Task EncodeAsync(
        string inputWavPath,
        string outputPath,
        CancellationToken ct = default)
    {
        if (_ffmpegPath == null)
            throw new InvalidOperationException("FFmpeg가 없습니다.");
        var args = $"-y -i \"{inputWavPath}\" \"{outputPath}\"";
        await RunProcessAsync(_ffmpegPath, args, ct);
    }

    // ──────────────────────────────────────────────

    private static async Task<string> RunProcessAsync(
        string exe, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = new Process { StartInfo = psi };
        proc.Start();
        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0)
            throw new Exception($"FFmpeg 오류 (code {proc.ExitCode}):\n{stderr.Trim()}");
        return stdout;
    }

    private static async Task RunProcessWithProgressAsync(
        string exe, string args, double totalSeconds,
        IProgress<double> progress, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = new Process { StartInfo = psi };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            var m = TimeRegex().Match(e.Data);
            if (m.Success && TimeSpan.TryParse(m.Value, out var ts))
                progress.Report(Math.Min(ts.TotalSeconds / totalSeconds, 1.0));
        };
        proc.Start();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync(ct);
        progress.Report(1.0);
    }

    private static AudioFileInfo ParseProbeJson(string json, string path)
    {
        var info = new AudioFileInfo { Path = path };
        var srm = SampleRateRegex().Match(json);
        if (srm.Success) info.SampleRate = int.Parse(srm.Groups[1].Value);
        var chm = ChannelsRegex().Match(json);
        if (chm.Success) info.Channels = int.Parse(chm.Groups[1].Value);
        var dm = DurationRegex().Match(json);
        if (dm.Success && double.TryParse(dm.Groups[1].Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d))
            info.DurationSeconds = d;
        info.LengthSamples = (long)(info.DurationSeconds * info.SampleRate);
        return info;
    }

    [GeneratedRegex(@"time=(\d{2}:\d{2}:\d{2}\.\d+)")]
    private static partial Regex TimeRegex();
    [GeneratedRegex(@"""sample_rate""\s*:\s*""(\d+)""")]
    private static partial Regex SampleRateRegex();
    [GeneratedRegex(@"""channels""\s*:\s*(\d+)")]
    private static partial Regex ChannelsRegex();
    [GeneratedRegex(@"""duration""\s*:\s*""([0-9.]+)""")]
    private static partial Regex DurationRegex();
}

public record AudioFileInfo
{
    public string Path { get; set; } = string.Empty;
    public int SampleRate { get; set; } = 44100;
    public int Channels { get; set; } = 2;
    public double DurationSeconds { get; set; }
    public long LengthSamples { get; set; }
}
