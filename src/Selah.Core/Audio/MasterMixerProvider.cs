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
            {
                var mixer = new TrackMixerProvider(track, _project, WaveFormat);
                // 재생 중 호출되는 경우 현재 재생 위치에서 시작하도록 동기화합니다.
                // 동기화하지 않으면 새 믹서가 position 0에서 시작해 오디오가 처음부터 재생됩니다.
                mixer.Seek(_positionFrames);
                _trackMixers.Add(mixer);
            }
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

        long currentPosition;
        lock (_lock)
        {
            if (_temp.Length < count)
                _temp = new float[count];

            bool anySolo = _project.Tracks.Any(t => t.Solo);

            foreach (var mixer in _trackMixers)
            {
                var track = _project.Tracks.FirstOrDefault(t => t.Id == mixer.TrackId);
                if (track == null) continue;
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
        }

        // PlayheadAdvanced는 lock 밖에서 발생시킵니다.
        // lock 안에서 호출하면 이벤트 핸들러가 UI 스레드 디스패치를 시도할 때
        // UI 스레드가 Stop()으로 오디오 스레드를 기다리고 있을 경우 교착 상태가 됩니다.
        PlayheadAdvanced?.Invoke(this, currentPosition);

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
