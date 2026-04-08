namespace Selah.Core.Audio;

/// <summary>
/// 마스터 출력 클리핑 방지용 소프트 리미터.
/// ±0.95 이상에서 부드럽게 압축하여 클리핑 아티팩트를 방지합니다.
/// </summary>
public class SoftLimiter
{
    private const float Threshold = 0.93f;
    private const float KneeWidth = 0.1f;
    private const float HardClip = 0.999f;

    public void Process(float[] buffer, int offset, int count)
    {
        for (int i = offset; i < offset + count; i++)
            buffer[i] = SoftClip(buffer[i]);
    }

    private static float SoftClip(float x)
    {
        float abs = MathF.Abs(x);

        if (abs <= Threshold - KneeWidth * 0.5f)
            return x; // 리니어 구간

        if (abs >= HardClip)
            return MathF.CopySign(HardClip, x); // 하드 클리핑

        // 소프트 니 구간: tanh 기반 부드러운 압축
        float knee_start = Threshold - KneeWidth * 0.5f;
        float t = (abs - knee_start) / (HardClip - knee_start);
        float compressed = knee_start + (HardClip - knee_start) * MathF.Tanh(t * 1.5f) / MathF.Tanh(1.5f);
        return MathF.CopySign(compressed, x);
    }
}
