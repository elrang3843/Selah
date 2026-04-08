namespace Selah.Core.Models;

/// <summary>
/// AI 음원 분리 모델의 메타데이터 (Model Manager 카탈로그 항목).
/// </summary>
public class ModelInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>라이선스 한 줄 요약 (예: "MIT")</summary>
    public string License { get; set; } = string.Empty;
    public string LicenseUrl { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;

    public ModelType ModelType { get; set; }
    public StemType StemType { get; set; }
    public ModelEngine Engine { get; set; }

    public long SizeBytes { get; set; }

    public bool IsInstalled { get; set; }
    public string? LocalPath { get; set; }

    /// <summary>예배 영상 공개 배포에 적합 여부</summary>
    public bool SuitableForPublicWorship { get; set; }

    /// <summary>예배 사역자를 위한 저작권 주의 메시지</summary>
    public string WorshipNote { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new();

    public string SizeDisplay =>
        SizeBytes >= 1024 * 1024 * 1024
            ? $"{SizeBytes / 1024.0 / 1024.0 / 1024.0:F1} GB"
            : $"{SizeBytes / 1024.0 / 1024.0:F0} MB";
}

public enum ModelType
{
    VocalSeparation,    // 보컬/반주 2-stem
    StemSeparation,     // 드럼/베이스/기타/보컬 4-stem
    NoiseReduction
}

public enum StemType
{
    TwoStem,    // vocals + no_vocals
    FourStem,   // vocals + drums + bass + other
    SixStem
}

public enum ModelEngine
{
    PythonCLI,      // Demucs CLI (python subprocess)
    OnnxRuntime     // ONNX Runtime (DirectML/CUDA/CPU)
}
