using NAudio.Wave;
using Selah.Core.Models;

namespace Selah.Core.Audio;

/// <summary>
/// 메트로놈/클릭 트랙 샘플 프로바이더.
/// 사인파 합성으로 클릭음을 생성하므로 외부 파일 불필요 (라이선스 이슈 없음).
/// </summary>
public class MetronomeProvider : ISampleProvider
{
    private readonly TempoMap _tempoMap;
    private long _positionFrames;
    private bool _enabled;

    // 클릭 사운드 파라미터
    private readonly int _clickFrames;      // ~20ms, 샘플레이트에 비례
    private const float DownbeatFreq = 1400f;
    private const float BeatFreq = 880f;
    private const float ClickGain = 0.22f;

    public WaveFormat WaveFormat { get; }

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public MetronomeProvider(TempoMap tempoMap, int sampleRate)
    {
        _tempoMap = tempoMap;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
        _clickFrames = sampleRate / 50;  // 20ms
    }

    public void Seek(long positionFrames) => _positionFrames = positionFrames;

    public int Read(float[] buffer, int offset, int count)
    {
        int channels = WaveFormat.Channels;
        int frames = count / channels;

        if (!_enabled)
        {
            Array.Clear(buffer, offset, count);
            _positionFrames += frames;
            return count;
        }

        int sr = WaveFormat.SampleRate;
        double bpm = _tempoMap.GetBpm();
        int numerator = _tempoMap.GetNumerator();
        double samplesPerBeat = sr * 60.0 / bpm;
        double samplesPerBar = samplesPerBeat * numerator;

        for (int f = 0; f < frames; f++)
        {
            long absFrame = _positionFrames + f;
            float click = GenerateClickSample(absFrame, samplesPerBeat, samplesPerBar, numerator, sr, _clickFrames);
            for (int ch = 0; ch < channels; ch++)
                buffer[offset + f * channels + ch] = click;
        }

        _positionFrames += frames;
        return count;
    }

    private static float GenerateClickSample(
        long frame, double samplesPerBeat, double samplesPerBar, int numerator, int sampleRate, int clickFrames)
    {
        // 현재 마디 내 위치 (소수 포함)
        double posInBar = frame % samplesPerBar;

        // 어느 박자인지 확인
        for (int beat = 0; beat < numerator; beat++)
        {
            double beatStart = beat * samplesPerBeat;
            if (posInBar >= beatStart && posInBar < beatStart + clickFrames)
            {
                float posInClick = (float)(posInBar - beatStart);
                float freq = beat == 0 ? DownbeatFreq : BeatFreq;
                float t = posInClick / sampleRate;
                float decay = MathF.Exp(-t * 200f);
                return MathF.Sin(2f * MathF.PI * freq * t) * decay * ClickGain;
            }
        }

        return 0f;
    }
}
