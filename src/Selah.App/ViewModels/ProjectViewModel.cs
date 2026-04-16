using System.Collections.ObjectModel;
using NAudio.Wave;
using Selah.Core.Models;
using Selah.Core.Services;

namespace Selah.App.ViewModels;

public class ProjectViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly ProjectService _projectService;
    private readonly FFmpegService _ffmpegService;

    public ProjectViewModel(
        Project project,
        ProjectService projectService,
        FFmpegService ffmpegService)
    {
        _project = project;
        _projectService = projectService;
        _ffmpegService = ffmpegService;

        Tracks = new ObservableCollection<TrackViewModel>(
            project.Tracks.Select(t => new TrackViewModel(t, project)));
    }

    public Project Model => _project;

    public string Name
    {
        get => _project.Name;
        set { _project.Name = value; _project.IsDirty = true; OnPropertyChanged(); OnPropertyChanged(nameof(IsDirty)); }
    }

    public int SampleRate => _project.SampleRate;
    public bool IsDirty => _project.IsDirty;

    public string TotalLengthDisplay
    {
        get
        {
            var ts = TimeSpan.FromSeconds(_project.TotalLengthSeconds);
            return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 100}";
        }
    }

    public ObservableCollection<TrackViewModel> Tracks { get; }

    // ── 트랙 관리 ──

    public TrackViewModel AddTrack(string? name = null)
    {
        var track = new Track
        {
            Name = name ?? (_project.Tracks.Count + 1).ToString(),
            TrackIndex = _project.Tracks.Count,
            Color = TrackColors[_project.Tracks.Count % TrackColors.Length]
        };
        _project.Tracks.Add(track);
        var vm = new TrackViewModel(track, _project);
        Tracks.Add(vm);
        _project.IsDirty = true;
        OnPropertyChanged(nameof(IsDirty));
        return vm;
    }

    public void RemoveTrack(TrackViewModel trackVm)
    {
        _project.Tracks.Remove(trackVm.Model);
        Tracks.Remove(trackVm);
        _project.IsDirty = true;
        OnPropertyChanged(nameof(IsDirty));
    }

    // ── 오디오 임포트 ──

    /// <summary>
    /// 오디오/영상 파일을 프로젝트에 임포트합니다.
    /// WAV이면 직접 복사, 다른 형식이면 FFmpeg로 변환.
    /// </summary>
    public async Task<AudioSource> ImportAudioAsync(
        string filePath,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (_project.FilePath == null)
            throw new InvalidOperationException(Loc.Get("Project_SaveFirst"));

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var audioDir = Path.Combine(_project.FilePath, "audio");
        Directory.CreateDirectory(audioDir);

        string destWav;
        long lengthSamples;
        int sr, ch;

        if (ext == ".wav")
        {
            // 동일 포맷이면 복사, 다른 포맷이면 재샘플링
            var destName = MakeUniqueFileName(audioDir, Path.GetFileNameWithoutExtension(filePath) + ".wav");
            destWav = Path.Combine(audioDir, destName);

            using var reader = new WaveFileReader(filePath);
            if (reader.WaveFormat.SampleRate == _project.SampleRate &&
                reader.WaveFormat.Channels == 2 &&
                reader.WaveFormat.BitsPerSample == 16)
            {
                File.Copy(filePath, destWav, true);
                sr = reader.WaveFormat.SampleRate;
                ch = reader.WaveFormat.Channels;
                lengthSamples = reader.SampleCount;
            }
            else
            {
                // 리샘플링
                if (_ffmpegService.IsAvailable)
                {
                    await _ffmpegService.DecodeToWavAsync(filePath, destWav,
                        _project.SampleRate, 2, progress, ct);
                }
                else
                {
                    File.Copy(filePath, destWav, true);
                }
                using var r2 = new WaveFileReader(destWav);
                sr = r2.WaveFormat.SampleRate;
                ch = r2.WaveFormat.Channels;
                lengthSamples = r2.SampleCount;
            }
        }
        else if (_ffmpegService.IsAvailable)
        {
            var destName = MakeUniqueFileName(audioDir,
                Path.GetFileNameWithoutExtension(filePath) + ".wav");
            destWav = Path.Combine(audioDir, destName);

            var videoExts = new[] { ".mp4", ".mkv", ".mov", ".avi", ".m4v" };
            if (videoExts.Contains(ext))
                await _ffmpegService.ExtractAudioAsync(filePath, destWav, _project.SampleRate, 2, ct);
            else
                await _ffmpegService.DecodeToWavAsync(filePath, destWav, _project.SampleRate, 2, progress, ct);

            using var r3 = new WaveFileReader(destWav);
            sr = r3.WaveFormat.SampleRate;
            ch = r3.WaveFormat.Channels;
            lengthSamples = r3.SampleCount;
        }
        else
        {
            throw new NotSupportedException(Loc.Format("Project_FFmpegRequired", ext));
        }

        var source = new AudioSource
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            RelPath = Path.GetRelativePath(_project.FilePath, destWav),
            AbsolutePath = destWav,
            SampleRate = sr,
            Channels = ch,
            LengthSamples = lengthSamples,
            SourceType = SourceType.Import
        };
        _project.AudioSources.Add(source);
        _project.IsDirty = true;
        OnPropertyChanged(nameof(IsDirty));
        return source;
    }

    /// <summary>
    /// 파일을 프로브하여 단일 스테레오이면 <see cref="ImportAudioAsync"/>와 동일하게 처리하고,
    /// 멀티채널(>2ch) 또는 멀티스트림이면 채널/스트림별로 분리된 AudioSource 목록을 반환합니다.
    /// </summary>
    public async Task<IReadOnlyList<AudioSource>> ImportAudioAutoAsync(
        string filePath,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (_project.FilePath == null)
            throw new InvalidOperationException(Loc.Get("Project_SaveFirst"));

        // FFmpeg가 없으면 단일 임포트로 폴백
        IReadOnlyList<AudioStreamInfo> streams = [];
        if (_ffmpegService.IsAvailable)
        {
            try { streams = await _ffmpegService.ProbeAudioStreamsAsync(filePath, ct); }
            catch { /* 프로브 실패 → 단일 임포트 */ }
        }

        bool needsSplit = streams.Count > 1 || (streams.Count == 1 && streams[0].Channels > 2);
        if (!needsSplit)
            return [await ImportAudioAsync(filePath, progress, ct)];

        var audioDir = Path.Combine(_project.FilePath, "audio");
        Directory.CreateDirectory(audioDir);
        var baseName = Path.GetFileNameWithoutExtension(filePath);
        var results  = new List<AudioSource>();

        int totalOps  = streams.Sum(s => s.Channels > 2 ? s.Channels : 1);
        int completed = 0;

        foreach (var stream in streams)
        {
            if (stream.Channels <= 2)
            {
                // 개별 스트림 → 스테레오(또는 모노) WAV 추출
                string label = !string.IsNullOrEmpty(stream.Title)
                    ? stream.Title
                    : $"Stream {stream.AudioStreamIndex + 1}";
                var destName = MakeUniqueFileName(audioDir,
                    $"{baseName}_{label.Replace(' ', '_')}.wav");
                var destWav = Path.Combine(audioDir, destName);

                await _ffmpegService.ExtractStreamAsync(
                    filePath, destWav, stream.AudioStreamIndex,
                    _project.SampleRate, stream.Channels, ct);

                results.Add(CreateAudioSourceFromWav(destWav,
                    streams.Count > 1 ? $"{baseName} — {label}" : baseName));
                progress?.Report((double)++completed / totalOps);
            }
            else
            {
                // 멀티채널 스트림 → 채널별 모노 WAV 추출
                for (int ch = 0; ch < stream.Channels; ch++)
                {
                    string chLabel = streams.Count > 1
                        ? $"Stream {stream.AudioStreamIndex + 1} Ch {ch + 1}"
                        : $"Ch {ch + 1}";
                    var destName = MakeUniqueFileName(audioDir,
                        $"{baseName}_{chLabel.Replace(' ', '_')}.wav");
                    var destWav = Path.Combine(audioDir, destName);

                    await _ffmpegService.ExtractChannelAsync(
                        filePath, destWav, stream.AudioStreamIndex,
                        ch, _project.SampleRate, ct);

                    results.Add(CreateAudioSourceFromWav(destWav, $"{baseName} — {chLabel}"));
                    progress?.Report((double)++completed / totalOps);
                }
            }
        }

        _project.IsDirty = true;
        OnPropertyChanged(nameof(IsDirty));
        return results;
    }

    private AudioSource CreateAudioSourceFromWav(string wavPath, string name)
    {
        using var r = new WaveFileReader(wavPath);
        var src = new AudioSource
        {
            Name          = name,
            RelPath       = Path.GetRelativePath(_project.FilePath!, wavPath),
            AbsolutePath  = wavPath,
            SampleRate    = r.WaveFormat.SampleRate,
            Channels      = r.WaveFormat.Channels,
            LengthSamples = r.SampleCount,
            SourceType    = SourceType.Import
        };
        _project.AudioSources.Add(src);
        return src;
    }

    // ── 저장 ──

    public async Task SaveAsync()
    {
        if (_project.FilePath == null)
            throw new InvalidOperationException(Loc.Get("Project_NoSavePath"));
        await _projectService.SaveAsync(_project, _project.FilePath);
        OnPropertyChanged(nameof(IsDirty));
    }

    // ── 헬퍼 ──

    private static string MakeUniqueFileName(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path)) return fileName;
        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (int i = 2; ; i++)
        {
            var candidate = $"{name}_{i}{ext}";
            if (!File.Exists(Path.Combine(dir, candidate))) return candidate;
        }
    }

    private static readonly string[] TrackColors =
    {
        "#4A9EFF", "#FF6B6B", "#51CF66", "#FFD43B",
        "#CC5DE8", "#FF922B", "#20C997", "#F06595"
    };
}
