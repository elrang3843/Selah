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
    private bool _disposed;

    public WaveFormat WaveFormat { get; }

    /// <summary>재생 위치가 갱신될 때마다 발생. 인자는 현재 프레임 위치.</summary>
    public event EventHandler<long>? PlayheadAdvanced;

    public MasterMixerProvider(Project project)
    {
        _project = project;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(project.SampleRate, 2);
        _metronome = new MetronomeProvider(project.TempoMap, project.SampleRate);
        _limiter = new SoftLimiter();
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
                _trackMixers.Add(new TrackMixerProvider(track, _project, WaveFormat));
        }
    }

    public void Seek(long positionFrames)
    {
        lock (_lock)
        {
            _positionFrames = positionFrames;
            foreach (var m in _trackMixers) m.Seek(positionFrames);
            _metronome.Seek(positionFrames);
        }
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
        var temp = new float[count];

        lock (_lock)
        {
            bool anySolo = _project.Tracks.Any(t => t.Solo);

            foreach (var mixer in _trackMixers)
            {
                var track = _project.Tracks.FirstOrDefault(t => t.Id == mixer.TrackId);
                if (track == null) continue;
                if (track.Muted) continue;
                if (anySolo && !track.Solo) continue;

                Array.Clear(temp, 0, count);
                mixer.Read(temp, 0, count);
                for (int i = 0; i < count; i++)
                    buffer[offset + i] += temp[i];
            }

            // 메트로놈 추가
            if (_metronome.Enabled)
            {
                Array.Clear(temp, 0, count);
                _metronome.Read(temp, 0, count);
                for (int i = 0; i < count; i++)
                    buffer[offset + i] += temp[i];
            }

            // 소프트 리미터
            _limiter.Process(buffer, offset, count);

            int frames = count / WaveFormat.Channels;
            _positionFrames += frames;
            PlayheadAdvanced?.Invoke(this, _positionFrames);
        }

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
