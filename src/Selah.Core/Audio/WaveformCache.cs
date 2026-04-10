using NAudio.Wave;
using Selah.Core.Models;
using System.Collections.Concurrent;

namespace Selah.Core.Audio;

/// <summary>
/// 오디오 소스의 웨이브폼 피크 데이터를 비동기로 계산하고 캐시합니다.
/// 렌더링 스레드가 캐시를 요청하면, 백그라운드에서 계산 후 onReady 콜백을 호출합니다.
/// </summary>
public class WaveformCache
{
    /// <summary>피크 1개가 커버하는 소스 프레임 수 (48kHz 기준 약 5.3ms)</summary>
    public const int FramesPerPeak = 256;

    private readonly ConcurrentDictionary<string, float[]> _peaks = new();
    private readonly ConcurrentDictionary<string, bool> _pending = new();

    /// <summary>
    /// 캐시된 피크 배열을 반환합니다.
    /// 아직 계산되지 않은 경우 백그라운드 계산을 시작하고 null을 반환합니다.
    /// 계산 완료 시 onReady가 호출됩니다.
    /// </summary>
    public float[]? GetOrRequest(AudioSource source, Action onReady)
    {
        if (_peaks.TryGetValue(source.Id, out var peaks))
            return peaks;

        if (_pending.TryAdd(source.Id, true))
        {
            Task.Run(() =>
            {
                var computed = ComputePeaks(source);
                _peaks[source.Id] = computed;
                _pending.TryRemove(source.Id, out _);
                onReady();
            });
        }

        return null;
    }

    public void Remove(string sourceId)
    {
        _peaks.TryRemove(sourceId, out _);
        _pending.TryRemove(sourceId, out _);
    }

    private static float[] ComputePeaks(AudioSource source)
    {
        if (source.AbsolutePath == null || !File.Exists(source.AbsolutePath))
            return Array.Empty<float>();

        try
        {
            using var reader = new WaveFileReader(source.AbsolutePath);
            int channels = reader.WaveFormat.Channels;
            long totalFrames = reader.SampleCount;
            int peakCount = (int)(totalFrames / FramesPerPeak) + 1;
            var result = new float[peakCount];

            var sp = reader.ToSampleProvider();
            var buf = new float[FramesPerPeak * channels];

            for (int i = 0; i < peakCount; i++)
            {
                int remaining = (int)Math.Min(
                    (long)FramesPerPeak * channels,
                    (totalFrames - (long)i * FramesPerPeak) * channels);
                if (remaining <= 0) break;

                int read = sp.Read(buf, 0, remaining);
                if (read <= 0) break;

                float max = 0f;
                for (int j = 0; j < read; j++)
                    max = MathF.Max(max, MathF.Abs(buf[j]));
                result[i] = max;
            }

            return result;
        }
        catch
        {
            return Array.Empty<float>();
        }
    }
}
