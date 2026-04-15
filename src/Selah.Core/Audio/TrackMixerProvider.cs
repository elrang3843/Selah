using NAudio.Wave;
using Selah.Core.Models;

namespace Selah.Core.Audio;

/// <summary>
/// 단일 트랙 내의 모든 클립을 믹싱하여 스테레오 샘플을 제공합니다.
/// </summary>
public sealed class TrackMixerProvider : ISampleProvider, IDisposable
{
    private readonly Track _track;
    private readonly Project _project;
    private readonly List<ClipSampleProvider> _clipProviders = new();
    private float[] _temp = Array.Empty<float>();

    public string TrackId => _track.Id;
    public WaveFormat WaveFormat { get; }

    public TrackMixerProvider(Track track, Project project, WaveFormat format)
    {
        _track = track;
        _project = project;
        WaveFormat = format;
        RebuildClips();
    }

    private void RebuildClips()
    {
        foreach (var cp in _clipProviders) cp.Dispose();
        _clipProviders.Clear();

        foreach (var clip in _track.Clips)
        {
            if (clip.Muted) continue;
            var source = _project.AudioSources.FirstOrDefault(s => s.Id == clip.SourceId);
            if (source?.AbsolutePath == null || !File.Exists(source.AbsolutePath)) continue;
            _clipProviders.Add(new ClipSampleProvider(clip, source, WaveFormat));
        }
    }

    public void Seek(long positionFrames)
    {
        foreach (var cp in _clipProviders)
            cp.Seek(positionFrames);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        if (_temp.Length < count)
            _temp = new float[count];

        foreach (var cp in _clipProviders)
        {
            Array.Clear(_temp, 0, count);
            cp.Read(_temp, 0, count);
            for (int i = 0; i < count; i++)
                buffer[offset + i] += _temp[i];
        }

        // 트랙 게인 + 패닝 적용
        float gain = MathF.Pow(10f, _track.GainDb / 20f);
        float pan = Math.Clamp(_track.Pan, -1f, 1f);
        float leftMult = gain * (pan <= 0f ? 1f : 1f - pan);
        float rightMult = gain * (pan >= 0f ? 1f : 1f + pan);

        for (int i = 0; i < count; i += 2)
        {
            buffer[offset + i] *= leftMult;
            buffer[offset + i + 1] *= rightMult;
        }

        return count;
    }

    public void Dispose()
    {
        foreach (var cp in _clipProviders) cp.Dispose();
        _clipProviders.Clear();
    }
}
