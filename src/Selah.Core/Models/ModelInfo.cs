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

    /// <summary>모델 파일 다운로드 URL (null이면 수동 설치)</summary>
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// audio-separator 엔진에서 사용하는 모델 파일명.
    /// (예: "UVR-MDX-NET-Voc_FT.onnx")
    /// OnnxRuntime 엔진에서는 사용하지 않습니다.
    /// </summary>
    public string? ModelFilename { get; set; }

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
    NoiseReduction,
    SheetMusicOmr       // 광학 악보 인식 (OMR) — oemer + music21
}

public enum StemType
{
    TwoStem,    // vocals + no_vocals
    FourStem,   // vocals + drums + bass + other
    SixStem
}

public enum ModelEngine
{
    PythonCLI,        // Demucs CLI (python subprocess) — 레거시
    OnnxRuntime,      // ONNX Runtime + FFmpeg (권장)
    AudioSeparator    // audio-separator 패키지 (MDX-Net 보컬 특화, 모든 음역대 지원)
}
