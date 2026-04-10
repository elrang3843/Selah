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

        // 재생할 콘텐츠가 없으면 오디오 디바이스를 열지 않음 (드라이버 초기화 노이즈 방지)
        bool hasContent = _project.Tracks.Any(t => !t.Muted && t.Clips.Any(c => !c.Muted))
                          || MetronomeEnabled;
        if (!hasContent) return;

        if (_waveOut == null)
            InitWaveOut();

        _waveOut?.Play();
    }

    private void InitWaveOut()
    {
        if (_masterMixer == null) return;

        _volumeProvider = new VolumeSampleProvider(_masterMixer) { Volume = 1f };

        // WASAPI: 기기의 믹스 포맷을 조회하여 SR이 다르면 소프트웨어 리샘플링 후 전달합니다.
        // 이렇게 하지 않으면 NAudio가 closestMatchFormat으로 기기를 초기화해도
        // 우리 프로바이더는 여전히 projectSR로 데이터를 공급하므로
        // 샘플 해석 오류 → 저음역 잡음이 발생합니다.
        try
        {
            ISampleProvider outputForWasapi = _volumeProvider;
            try
            {
                using var deviceEnum = new NAudio.CoreAudioApi.MMDeviceEnumerator();
                using var device = deviceEnum.GetDefaultAudioEndpoint(
                    NAudio.CoreAudioApi.DataFlow.Render,
                    NAudio.CoreAudioApi.Role.Multimedia);
                int mixSR = device.AudioClient.MixFormat.SampleRate;
                if (mixSR != _masterMixer.WaveFormat.SampleRate)
                    outputForWasapi = new WdlResamplingSampleProvider(_volumeProvider, mixSR);
            }
            catch { /* 믹스 포맷 조회 실패 시 리샘플링 없이 시도 */ }

            var wasapi = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 100);
            wasapi.Init(outputForWasapi);
            wasapi.PlaybackStopped += OnPlaybackStopped;
            _waveOut = wasapi;
        }
        catch
        {
            // WASAPI 실패 시 WaveOut 폴백.
            // 16-bit PCM으로 명시적 변환하여 드라이버의 float 포맷 지원 여부와 무관하게
            // 안정적으로 동작하도록 합니다.
            try
            {
                var wo = new WaveOutEvent { DesiredLatency = 150 };
                wo.Init(new SampleToWaveProvider16(_volumeProvider));
                wo.PlaybackStopped += OnPlaybackStopped;
                _waveOut = wo;
            }
            catch { /* 오디오 디바이스 없음 */ }
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
