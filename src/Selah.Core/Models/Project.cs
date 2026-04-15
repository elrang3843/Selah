using System.Text.Json.Serialization;

namespace Selah.Core.Models;

/// <summary>
/// 셀라 프로젝트의 루트 데이터 모델.
/// project.json.gz 로 직렬화/역직렬화됩니다.
/// </summary>
public class Project
{
    public string Version { get; set; } = "1.0";
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "새 프로젝트";

    /// <summary>프로젝트 고정 샘플레이트 (32000 / 44100 / 48000 / 96000)</summary>
    public int SampleRate { get; set; } = 48000;

    public TempoMap TempoMap { get; set; } = new();
    public List<AudioSource> AudioSources { get; set; } = new();
    public List<Track> Tracks { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>프로젝트 폴더 경로 (직렬화 제외)</summary>
    [JsonIgnore]
    public string? FilePath { get; set; }

    /// <summary>저장 후 변경이 있었는지 여부 (직렬화 제외)</summary>
    [JsonIgnore]
    public bool IsDirty { get; set; }

    /// <summary>모든 클립의 끝 위치 중 최대값 (프로젝트 SR 프레임).</summary>
    [JsonIgnore]
    public long TotalLengthSamples
    {
        get
        {
            long max = 0;
            foreach (var track in Tracks)
            foreach (var clip in track.Clips)
            {
                // 소스 SR이 다를 경우 프로젝트 SR로 변환
                var src = AudioSources.FirstOrDefault(s => s.Id == clip.SourceId);
                long lenFrames = (src == null || src.SampleRate == SampleRate)
                    ? clip.LengthSamples
                    : (long)(clip.LengthSamples * (double)SampleRate / src.SampleRate);
                long end = clip.TimelineStartSamples + lenFrames;
                if (end > max) max = end;
            }
            return max;
        }
    }

    [JsonIgnore]
    public double TotalLengthSeconds =>
        SampleRate > 0 ? (double)TotalLengthSamples / SampleRate : 0.0;
}
