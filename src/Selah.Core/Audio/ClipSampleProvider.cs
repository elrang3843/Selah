using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Selah.Core.Models;

namespace Selah.Core.Audio;

/// <summary>
/// 단일 클립을 타임라인 위치에 따라 샘플을 제공합니다.
/// 클립 범위 밖의 구간은 무음(0)을 반환합니다.
/// </summary>
public sealed class ClipSampleProvider : ISampleProvider, IDisposable
{
    private readonly Clip _clip;
    private readonly AudioSource _source;
    private WaveFileReader? _reader;
    private ISampleProvider? _sampleProvider;

    // 현재 타임라인 위치 (프레임, 모노 기준 = 채널 수로 나눈 값)
    private long _positionFrames;

    public WaveFormat WaveFormat { get; }

    public ClipSampleProvider(Clip clip, AudioSource source, WaveFormat targetFormat)
    {
        _clip = clip;
        _source = source;
        WaveFormat = targetFormat;
        OpenReader();
    }

    private void OpenReader()
    {
        _reader?.Dispose();
        _reader = null;
        _sampleProvider = null;

        if (_source.AbsolutePath == null || !File.Exists(_source.AbsolutePath))
            return;

        try
        {
            _reader = new WaveFileReader(_source.AbsolutePath);
            ISampleProvider raw = _reader.ToSampleProvider();

            // 샘플레이트 변환
            if (_reader.WaveFormat.SampleRate != WaveFormat.SampleRate)
                raw = new WdlResamplingSampleProvider(raw, WaveFormat.SampleRate);

            // 채널 수 변환
            if (_reader.WaveFormat.Channels == 1 && WaveFormat.Channels == 2)
                raw = new MonoToStereoSampleProvider(raw);
            else if (_reader.WaveFormat.Channels > 2 && WaveFormat.Channels == 2)
                raw = new MultiplexingSampleProvider(new[] { raw }, 2);

            _sampleProvider = raw;
        }
        catch
        {
            _reader?.Dispose();
            _reader = null;
        }
    }

    /// <summary>재생 위치를 타임라인 절대 프레임으로 이동합니다.</summary>
    public void Seek(long timelineFrames)
    {
        _positionFrames = timelineFrames;

        if (_reader == null) return;

        long relFrame = timelineFrames - _clip.TimelineStartSamples;
        long srcFrame = _clip.SourceInSamples + relFrame;

        if (srcFrame < _clip.SourceInSamples || srcFrame >= _clip.SourceOutSamples)
            return; // 범위 밖 — 다음 Read 에서 무음 반환

        // WaveFileReader는 바이트 단위로 Seek
        int srcChannels = _reader.WaveFormat.Channels;
        int bytesPerSample = _reader.WaveFormat.BitsPerSample / 8;
        long bytePos = srcFrame * srcChannels * bytesPerSample;
        bytePos = Math.Clamp(bytePos, 0, _reader.Length);
        _reader.Position = bytePos;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        // count는 stereo float 샘플 수 (채널 포함). 프레임 = count / channels
        int channels = WaveFormat.Channels;
        int requestedFrames = count / channels;

        Array.Clear(buffer, offset, count);

        long clipStartFrame = _clip.TimelineStartSamples;
        long clipEndFrame = _clip.TimelineStartSamples + _clip.LengthSamples;

        // 현재 블록이 클립과 겹치는지 확인
        if (_positionFrames >= clipEndFrame || _positionFrames + requestedFrames <= clipStartFrame)
        {
            _positionFrames += requestedFrames;
            return count; // 무음 반환
        }

        // 겹치는 구간 계산
        long overlapStartFrame = Math.Max(_positionFrames, clipStartFrame);
        long overlapEndFrame = Math.Min(_positionFrames + requestedFrames, clipEndFrame);
        int overlapFrames = (int)(overlapEndFrame - overlapStartFrame);

        int bufferFrameOffset = (int)(overlapStartFrame - _positionFrames);
        int bufSampleOffset = offset + bufferFrameOffset * channels;

        // 소스 파일 내 위치 설정
        long srcFrame = _clip.SourceInSamples + (overlapStartFrame - clipStartFrame);
        SeekReaderToFrame(srcFrame);

        if (_sampleProvider != null && overlapFrames > 0)
        {
            int readSamples = overlapFrames * channels;
            int actualRead = _sampleProvider.Read(buffer, bufSampleOffset, readSamples);

            // 페이드 인
            if (_clip.FadeInSamples > 0)
                ApplyFadeIn(buffer, bufSampleOffset, actualRead, overlapStartFrame - clipStartFrame, channels);

            // 페이드 아웃
            if (_clip.FadeOutSamples > 0)
                ApplyFadeOut(buffer, bufSampleOffset, actualRead, overlapStartFrame - clipStartFrame, channels);

            // 클립 게인
            if (_clip.GainDb != 0f)
            {
                float gain = MathF.Pow(10f, _clip.GainDb / 20f);
                for (int i = bufSampleOffset; i < bufSampleOffset + actualRead; i++)
                    buffer[i] *= gain;
            }
        }

        _positionFrames += requestedFrames;
        return count;
    }

    private void SeekReaderToFrame(long srcFrame)
    {
        if (_reader == null) return;
        int srcChannels = _reader.WaveFormat.Channels;
        int bps = _reader.WaveFormat.BitsPerSample / 8;
        long bytePos = Math.Clamp(srcFrame * srcChannels * bps, 0, _reader.Length);
        _reader.Position = bytePos;
    }

    private void ApplyFadeIn(float[] buf, int off, int count, long relStartFrame, int channels)
    {
        long fadeEndFrame = _clip.FadeInSamples;
        for (int i = 0; i < count / channels; i++)
        {
            long frame = relStartFrame + i;
            if (frame >= fadeEndFrame) break;
            float t = (float)frame / fadeEndFrame;
            float mult = ApplyCurve(t);
            for (int ch = 0; ch < channels; ch++)
                buf[off + i * channels + ch] *= mult;
        }
    }

    private void ApplyFadeOut(float[] buf, int off, int count, long relStartFrame, int channels)
    {
        long fadeStartFrame = _clip.LengthSamples - _clip.FadeOutSamples;
        for (int i = 0; i < count / channels; i++)
        {
            long frame = relStartFrame + i;
            if (frame < fadeStartFrame) continue;
            float t = 1f - (float)(frame - fadeStartFrame) / _clip.FadeOutSamples;
            float mult = ApplyCurve(Math.Clamp(t, 0f, 1f));
            for (int ch = 0; ch < channels; ch++)
                buf[off + i * channels + ch] *= mult;
        }
    }

    private float ApplyCurve(float t) => _clip.FadeCurve switch
    {
        FadeCurve.EqualPower => MathF.Sqrt(t),
        FadeCurve.Logarithmic => t <= 0 ? 0f : MathF.Log(1f + t * 9f) / MathF.Log(10f),
        _ => t  // Linear
    };

    public void Dispose()
    {
        _reader?.Dispose();
        _reader = null;
    }
}
