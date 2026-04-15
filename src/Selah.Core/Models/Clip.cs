namespace Selah.Core.Models;

/// <summary>
/// 비파괴(Non-destructive) 클립.
/// 원본 파일(AudioSource)을 참조하고 타임라인 위치/구간 정보만 저장합니다.
/// </summary>
public class Clip
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>참조하는 AudioSource.Id</summary>
    public string SourceId { get; set; } = string.Empty;

    /// <summary>타임라인에서 클립이 시작하는 절대 위치 (샘플, 모노 기준)</summary>
    public long TimelineStartSamples { get; set; }

    /// <summary>소스 파일 내에서 사용할 시작 위치 (샘플)</summary>
    public long SourceInSamples { get; set; }

    /// <summary>소스 파일 내에서 사용할 끝 위치 (샘플, exclusive)</summary>
    public long SourceOutSamples { get; set; }

    public float GainDb { get; set; } = 0f;
    public float Pan { get; set; } = 0f;
    public bool Muted { get; set; } = false;

    public long FadeInSamples { get; set; } = 0;
    public long FadeOutSamples { get; set; } = 0;
    public FadeCurve FadeCurve { get; set; } = FadeCurve.Linear;

    // Computed
    public long LengthSamples => SourceOutSamples - SourceInSamples;
    public long TimelineEndSamples => TimelineStartSamples + LengthSamples;
}

public enum FadeCurve
{
    Linear,
    EqualPower,
    Logarithmic
}
