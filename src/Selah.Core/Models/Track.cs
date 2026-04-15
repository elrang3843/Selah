namespace Selah.Core.Models;

/// <summary>
/// 타임라인 트랙. 여러 클립을 가질 수 있는 오디오 채널.
/// </summary>
public class Track
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "트랙";
    public int TrackIndex { get; set; }

    /// <summary>트랙 볼륨 (dB, 0 = 0dBFS)</summary>
    public float GainDb { get; set; } = 0f;

    /// <summary>패닝 (-1.0 = 왼쪽, 0 = 중앙, 1.0 = 오른쪽)</summary>
    public float Pan { get; set; } = 0f;

    public bool Muted { get; set; } = false;
    public bool Solo { get; set; } = false;

    /// <summary>타임라인에서 이 트랙을 표시할 색상 (hex)</summary>
    public string Color { get; set; } = "#4A9EFF";

    public float HeightPixels { get; set; } = 80f;

    public List<Clip> Clips { get; set; } = new();
}
