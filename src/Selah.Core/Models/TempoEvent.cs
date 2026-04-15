namespace Selah.Core.Models;

/// <summary>
/// 특정 마디/박자 위치에서의 템포 변경 이벤트.
/// </summary>
public class TempoEvent
{
    /// <summary>이 이벤트가 적용되는 마디 번호 (1-based)</summary>
    public int Bar { get; set; } = 1;

    /// <summary>이 이벤트가 적용되는 박자 번호 (1-based)</summary>
    public int Beat { get; set; } = 1;

    /// <summary>BPM (beats per minute)</summary>
    public double Bpm { get; set; } = 120.0;

    /// <summary>박자표 분자 (예: 4/4 에서 4)</summary>
    public int Numerator { get; set; } = 4;

    /// <summary>박자표 분모 (예: 4/4 에서 4)</summary>
    public int Denominator { get; set; } = 4;
}
