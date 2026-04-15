namespace Selah.Core.Audio;

/// <summary>
/// 마스터 출력 클리핑 방지용 소프트 리미터.
/// Threshold를 넘는 신호를 HardClip 이하로 부드럽게 압축합니다.
///
/// 수학적 보장:
///   - abs ≤ Threshold  → 선형 통과 (왜곡 없음)
///   - abs > Threshold  → compress(abs) &lt; abs (압축, 확장 아님)
///   - compress(Threshold) = Threshold (연속)
///   - compress'(Threshold) = 1 (미분 연속, 꺾임 없음)
///   - compress(abs) → HardClip as abs → ∞
///
/// 구현: compress(abs) = Threshold + Range × (1 − exp(−overshoot / Range))
///   where overshoot = abs − Threshold, Range = HardClip − Threshold
/// </summary>
public class SoftLimiter
{
    private const float Threshold = 0.88f;
    private const float HardClip  = 0.999f;
    private const float Range     = HardClip - Threshold;   // 0.119
    private const float InvRange  = 1f / Range;             // ≈ 8.403

    public void Process(float[] buffer, int offset, int count)
    {
        for (int i = offset; i < offset + count; i++)
            buffer[i] = SoftClip(buffer[i]);
    }

    private static float SoftClip(float x)
    {
        float abs = MathF.Abs(x);
        if (abs <= Threshold) return x;

        float overshoot   = abs - Threshold;
        float compressed  = Threshold + Range * (1f - MathF.Exp(-overshoot * InvRange));
        return MathF.CopySign(compressed, x);
    }
}
