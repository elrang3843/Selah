using NAudio.Wave;
using Selah.Core.Models;

namespace Selah.Core.Audio;

/// <summary>
/// 모든 트랙 + 메트로놈을 믹싱하고 소프트 리미터를 적용하는 마스터 믹서.
/// NAudio IWavePlayer에 연결하여 WASAPI 출력으로 사용됩니다.
/// </summary>
public sealed class MasterMixerProvider : ISampleProvider, IDisposable
{
    private readonly Project _project;
    private readonly List<TrackMixerProvider> _trackMixers = new();
    private readonly MetronomeProvider _metronome;
    private readonly SoftLimiter _limiter;
    private long _positionFrames;
    private readonly object _lock = new();
    private float[] _temp = Array.Empty<float>();
    private bool _disposed;
    private bool _endReported;
    // PlayheadAdvanced를 매 Read() 호출마다 발생시키면 UI Dispatcher 큐가 포화됩니다.
    // 최대 30 Hz로 스로틀하여 초당 30회만 발생시킵니다.
    private long _lastPlayheadFrames = long.MinValue / 2;
    private readonly long _playheadIntervalFrames;

    public WaveFormat WaveFormat { get; }

    /// <summary>재생 위치가 갱신될 때마다 발생. 인자는 현재 프레임 위치.</summary>
    public event EventHandler<long>? PlayheadAdvanced;

    /// <summary>재생 가능한 클립이 모두 끝났을 때 발생합니다.</summary>
    public event EventHandler? PlaybackEnded;

    public MasterMixerProvider(Project project)
    {
        _project = project;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(project.SampleRate, 2);
        _metronome = new MetronomeProvider(project.TempoMap, project.SampleRate);
        _limiter = new SoftLimiter();
        _playheadIntervalFrames = Math.Max(1, project.SampleRate / 30);
        RebuildMixers();
    }

    /// <summary>트랙 목록이 변경되었을 때 믹서를 재구성합니다.</summary>
    public void RebuildMixers()
    {
        lock (_lock)
        {
            foreach (var m in _trackMixers) m.Dispose();
            _trackMixers.Clear();
            foreach (var track in _project.Tracks)
            {
                var mixer = new TrackMixerProvider(track, _project, WaveFormat);
                // 재생 중 호출되는 경우 현재 재생 위치에서 시작하도록 동기화합니다.
                // 동기화하지 않으면 새 믹서가 position 0에서 시작해 오디오가 처음부터 재생됩니다.
                mixer.Seek(_positionFrames);
                _trackMixers.Add(mixer);
            }
            _endReported = false;
        }
    }

    public void Seek(long positionFrames)
    {
        lock (_lock)
        {
            _positionFrames = positionFrames;
            _endReported = false;
            foreach (var m in _trackMixers) m.Seek(positionFrames);
            _metronome.Seek(positionFrames);
        }
    }

    /// <summary>
    /// 솔로/뮤트를 고려하여 재생 가능한 클립들의 최대 끝 프레임(프로젝트 SR 기준)을 반환합니다.
    /// 재생 가능한 클립이 없으면 0을 반환합니다.
    /// </summary>
    private long GetPlayableEndFrame()
    {
        bool anySolo = _project.Tracks.Any(t => t.Solo);
        long max = 0;
        foreach (var track in _project.Tracks)
        {
            if (!track.IsEnabled) continue;
            if (track.Muted) continue;
            if (anySolo && !track.Solo) continue;
            foreach (var clip in track.Clips)
            {
                if (clip.Muted) continue;
                var src = _project.AudioSources.FirstOrDefault(s => s.Id == clip.SourceId);
                // clip.LengthSamples는 소스 SR 기준 → 프로젝트 SR 기준으로 변환
                long lenProjectFrames = (src == null || src.SampleRate == WaveFormat.SampleRate)
                    ? clip.LengthSamples
                    : (long)(clip.LengthSamples * (double)WaveFormat.SampleRate / src.SampleRate);
                long end = clip.TimelineStartSamples + lenProjectFrames;
                if (end > max) max = end;
            }
        }
        return max;
    }

    public bool MetronomeEnabled
    {
        get => _metronome.Enabled;
        set => _metronome.Enabled = value;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (_disposed)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        Array.Clear(buffer, offset, count);

        long currentPosition;
        bool shouldFireEnded = false;
        bool shouldFirePlayhead = false;
        lock (_lock)
        {
            if (_temp.Length < count)
                _temp = new float[count];

            bool anySolo = _project.Tracks.Any(t => t.Solo);

            foreach (var mixer in _trackMixers)
            {
                var track = _project.Tracks.FirstOrDefault(t => t.Id == mixer.TrackId);
                if (track == null) continue;
                if (!track.IsEnabled) continue;
                if (track.Muted) continue;
                if (anySolo && !track.Solo) continue;

                Array.Clear(_temp, 0, count);
                mixer.Read(_temp, 0, count);
                for (int i = 0; i < count; i++)
                    buffer[offset + i] += _temp[i];
            }

            // 메트로놈 추가
            if (_metronome.Enabled)
            {
                Array.Clear(_temp, 0, count);
                _metronome.Read(_temp, 0, count);
                for (int i = 0; i < count; i++)
                    buffer[offset + i] += _temp[i];
            }

            // 소프트 리미터
            _limiter.Process(buffer, offset, count);

            int frames = count / WaveFormat.Channels;
            _positionFrames += frames;
            currentPosition = _positionFrames;

            // PlayheadAdvanced 스로틀: 30 Hz 초과 시 발생 생략
            if (currentPosition - _lastPlayheadFrames >= _playheadIntervalFrames)
            {
                _lastPlayheadFrames = currentPosition;
                shouldFirePlayhead = true;
            }

            // 재생 가능한 클립이 모두 끝났는지 확인 (메트로놈 제외)
            if (!_endReported)
            {
                long endFrame = GetPlayableEndFrame();
                if (endFrame > 0 && currentPosition >= endFrame)
                {
                    _endReported = true;
                    shouldFireEnded = true;
                }
            }
        }

        // 이벤트는 lock 밖에서 발생시킵니다.
        // lock 안에서 호출하면 이벤트 핸들러가 UI 스레드 디스패치를 시도할 때
        // UI 스레드가 Stop()으로 오디오 스레드를 기다리고 있을 경우 교착 상태가 됩니다.
        if (shouldFirePlayhead) PlayheadAdvanced?.Invoke(this, currentPosition);
        if (shouldFireEnded) PlaybackEnded?.Invoke(this, EventArgs.Empty);

        return count;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_lock)
        {
            foreach (var m in _trackMixers) m.Dispose();
            _trackMixers.Clear();
        }
    }
}
