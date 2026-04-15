using System.Text.Json.Serialization;

namespace Selah.Core.Models;

/// <summary>
/// 프로젝트에 임포트된 오디오 소스 파일 정보.
/// 실제 오디오 데이터는 프로젝트 폴더의 audio/ 하위에 WAV로 보관됩니다.
/// </summary>
public class AudioSource
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    /// <summary>프로젝트 폴더 기준 상대 경로 (audio/filename.wav)</summary>
    public string RelPath { get; set; } = string.Empty;

    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public long LengthSamples { get; set; }
    public SourceType SourceType { get; set; } = SourceType.Import;

    /// <summary>절대 경로 (런타임 전용, 직렬화 제외)</summary>
    [JsonIgnore]
    public string? AbsolutePath { get; set; }

    [JsonIgnore]
    public double LengthSeconds => SampleRate > 0 ? (double)LengthSamples / SampleRate : 0.0;
}

public enum SourceType
{
    Import,
    Recording,
    Separated   // 분리 엔진으로 생성된 스템
}
