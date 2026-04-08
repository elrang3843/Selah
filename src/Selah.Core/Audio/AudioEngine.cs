using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Selah.Core.Models;

namespace Selah.Core.Audio;

/// <summary>
/// 실시간 재생 엔진.
/// WASAPI 공유 모드(기본) → 실패 시 WaveOut 폴백.
/// </summary>
public sealed class AudioEngine : IDisposable
{
    private IWavePlayer? _waveOut;
    private MasterMixerProvider? _masterMixer;
    private VolumeSampleProvider? _volumeProvider;
    private Project? _project;
    private bool _disposed;

    public PlaybackState State => _waveOut?.PlaybackState ?? PlaybackState.Stopped;
    public bool IsPlaying => State == PlaybackState.Playing;

    /// <summary>마스터 볼륨 (0.0 ~ 1.0+)</summary>
    public float MasterVolume
    {
        get => _volumeProvider?.Volume ?? 1f;
        set { if (_volumeProvider != null) _volumeProvider.Volume = Math.Clamp(value, 0f, 2f); }
    }

    public bool MetronomeEnabled
    {
        get => _masterMixer?.MetronomeEnabled ?? false;
        set { if (_masterMixer != null) _masterMixer.MetronomeEnabled = value; }
    }

    /// <summary>재생 중 플레이헤드 위치 변경 이벤트 (프레임 단위)</summary>
    public event EventHandler<long>? PlayheadAdvanced;

    /// <summary>재생 종료 이벤트</summary>
    public event EventHandler? PlaybackStopped;

    public void LoadProject(Project project)
    {
        Stop();
        _masterMixer?.Dispose();

        _project = project;
        _masterMixer = new MasterMixerProvider(project);
        _masterMixer.PlayheadAdvanced += (s, frames) =>
            PlayheadAdvanced?.Invoke(this, frames);
    }

    public void Play()
    {
        if (_project == null || _masterMixer == null) return;
        if (State == PlaybackState.Playing) return;

        if (_waveOut == null)
            InitWaveOut();

        _waveOut?.Play();
    }

    private void InitWaveOut()
    {
        if (_masterMixer == null) return;

        _volumeProvider = new VolumeSampleProvider(_masterMixer) { Volume = 1f };

        try
        {
            var wasapi = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 100);
            wasapi.Init(_volumeProvider);
            wasapi.PlaybackStopped += OnPlaybackStopped;
            _waveOut = wasapi;
        }
        catch
        {
            // WASAPI 실패 시 WaveOut 폴백
            var wo = new WaveOutEvent { DesiredLatency = 150 };
            wo.Init(_volumeProvider);
            wo.PlaybackStopped += OnPlaybackStopped;
            _waveOut = wo;
        }
    }

    private void OnPlaybackStopped(object? s, StoppedEventArgs e)
    {
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;
        _volumeProvider = null;
    }

    public void Seek(long positionFrames)
    {
        bool wasPlaying = IsPlaying;
        if (wasPlaying) Stop();

        _masterMixer?.Seek(positionFrames);

        if (wasPlaying) Play();
    }

    public void RebuildMixers() => _masterMixer?.RebuildMixers();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _masterMixer?.Dispose();
    }
}
