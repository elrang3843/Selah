using NAudio.Wave;
using Selah.Core.Models;

namespace Selah.Core.Audio;

/// <summary>
/// 오프라인 렌더 엔진 (Export).
/// 실시간이 아닌 파일 → 파일 변환이므로 CPU 부하와 무관하게 정밀한 출력이 가능합니다.
/// </summary>
public class AudioRenderer
{
    private const int BlockFrames = 4096;

    /// <summary>
    /// 프로젝트를 WAV 파일로 렌더링합니다.
    /// </summary>
    /// <param name="project">대상 프로젝트</param>
    /// <param name="outputPath">출력 WAV 경로</param>
    /// <param name="bitDepth">비트 심도 (16 또는 24)</param>
    /// <param name="includeMetronome">메트로놈 포함 여부</param>
    /// <param name="progress">진행률 (0~1)</param>
    public async Task RenderToWavAsync(
        Project project,
        string outputPath,
        int bitDepth = 24,
        bool includeMetronome = false,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        long totalFrames = project.TotalLengthSamples;
        if (totalFrames <= 0)
            throw new InvalidOperationException("렌더할 클립이 없습니다.");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        using var mixer = new MasterMixerProvider(project);
        mixer.MetronomeEnabled = includeMetronome;
        mixer.Seek(0);

        int channels = 2;
        int sampleRate = project.SampleRate;
        var outputFormat = new WaveFormat(sampleRate, bitDepth, channels);

        await using var writer = new WaveFileWriter(outputPath, outputFormat);

        int blockSamples = BlockFrames * channels;
        var floatBuf = new float[blockSamples];
        long framesRendered = 0;

        await Task.Run(() =>
        {
            while (framesRendered < totalFrames && !ct.IsCancellationRequested)
            {
                long remaining = totalFrames - framesRendered;
                int framesToRender = (int)Math.Min(BlockFrames, remaining);
                int samplesToRender = framesToRender * channels;

                Array.Clear(floatBuf, 0, samplesToRender);
                mixer.Read(floatBuf, 0, samplesToRender);

                WriteToWav(writer, floatBuf, samplesToRender, bitDepth);

                framesRendered += framesToRender;
                progress?.Report((double)framesRendered / totalFrames);
            }
        }, ct);

        ct.ThrowIfCancellationRequested();
    }

    private static void WriteToWav(WaveFileWriter writer, float[] buf, int count, int bitDepth)
    {
        if (bitDepth == 24)
        {
            var bytes = new byte[count * 3];
            for (int i = 0; i < count; i++)
            {
                int s = (int)(Math.Clamp(buf[i], -1f, 1f) * 8388607f);
                bytes[i * 3] = (byte)(s & 0xFF);
                bytes[i * 3 + 1] = (byte)((s >> 8) & 0xFF);
                bytes[i * 3 + 2] = (byte)((s >> 16) & 0xFF);
            }
            writer.Write(bytes, 0, bytes.Length);
        }
        else // 16-bit
        {
            var shorts = new short[count];
            for (int i = 0; i < count; i++)
                shorts[i] = (short)(Math.Clamp(buf[i], -1f, 1f) * 32767f);
            var bytes = new byte[count * 2];
            Buffer.BlockCopy(shorts, 0, bytes, 0, bytes.Length);
            writer.Write(bytes, 0, bytes.Length);
        }
    }
}
