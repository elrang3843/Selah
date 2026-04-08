namespace Selah.Core.Models;

/// <summary>
/// 프로젝트 전체의 템포/박자표 맵.
/// 템포가 도중에 바뀌는 경우를 대비해 이벤트 리스트로 설계.
/// </summary>
public class TempoMap
{
    public List<TempoEvent> Events { get; set; } = new()
    {
        new TempoEvent { Bar = 1, Beat = 1, Bpm = 120.0, Numerator = 4, Denominator = 4 }
    };

    /// <summary>현재 구현(MVP): 첫 번째 이벤트의 BPM 반환</summary>
    public double GetBpm() => Events.Count > 0 ? Events[0].Bpm : 120.0;

    public int GetNumerator() => Events.Count > 0 ? Events[0].Numerator : 4;
    public int GetDenominator() => Events.Count > 0 ? Events[0].Denominator : 4;

    /// <summary>샘플 위치를 마디:박:틱 으로 변환 (96 ticks per beat)</summary>
    public (int bar, int beat, int tick) SamplesToBarBeat(long samples, int sampleRate)
    {
        if (Events.Count == 0 || sampleRate == 0) return (1, 1, 0);

        var evt = Events[0];
        double seconds = (double)samples / sampleRate;
        double beatsPerSecond = evt.Bpm / 60.0;
        double totalBeats = seconds * beatsPerSecond;

        int totalBeatInt = (int)totalBeats;
        int tick = (int)((totalBeats - totalBeatInt) * 96);
        int bar = totalBeatInt / evt.Numerator + 1;
        int beat = totalBeatInt % evt.Numerator + 1;

        return (bar, beat, tick);
    }

    /// <summary>마디:박 → 샘플 위치 변환</summary>
    public long BarBeatToSamples(int bar, int beat, int sampleRate)
    {
        if (Events.Count == 0 || sampleRate == 0) return 0;
        var evt = Events[0];
        double beatsPerSecond = evt.Bpm / 60.0;
        int totalBeats = (bar - 1) * evt.Numerator + (beat - 1);
        double seconds = totalBeats / beatsPerSecond;
        return (long)(seconds * sampleRate);
    }

    /// <summary>한 박자(quarter note)의 샘플 수</summary>
    public long SamplesPerBeat(int sampleRate)
    {
        double bps = GetBpm() / 60.0;
        return (long)(sampleRate / bps);
    }
}
