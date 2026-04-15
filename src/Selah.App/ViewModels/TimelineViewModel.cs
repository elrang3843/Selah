namespace Selah.App.ViewModels;

public class TimelineViewModel : ViewModelBase
{
    private double _pixelsPerSecond = 100.0;
    private long _playheadFrames;
    private double _scrollOffsetX;
    private bool _isPlaying;
    private bool _snapEnabled = true;
    private SnapMode _snapMode = SnapMode.Bar;
    private bool _showBarBeat = true;

    public double PixelsPerSecond
    {
        get => _pixelsPerSecond;
        set => SetField(ref _pixelsPerSecond, Math.Clamp(value, 5.0, 4000.0));
    }

    public long PlayheadFrames
    {
        get => _playheadFrames;
        set
        {
            if (SetField(ref _playheadFrames, Math.Max(0, value)))
                OnPropertyChanged(nameof(PlayheadSeconds));
        }
    }

    public double PlayheadSeconds { get; private set; }

    public double ScrollOffsetX
    {
        get => _scrollOffsetX;
        set => SetField(ref _scrollOffsetX, Math.Max(0, value));
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetField(ref _isPlaying, value);
    }

    public bool SnapEnabled
    {
        get => _snapEnabled;
        set => SetField(ref _snapEnabled, value);
    }

    public SnapMode SnapMode
    {
        get => _snapMode;
        set => SetField(ref _snapMode, value);
    }

    public bool ShowBarBeat
    {
        get => _showBarBeat;
        set => SetField(ref _showBarBeat, value);
    }

    // ── 변환 헬퍼 ──

    public double FramesToPixels(long frames, int sampleRate)
        => frames / (double)sampleRate * PixelsPerSecond;

    public long PixelsToFrames(double pixels, int sampleRate)
        => (long)(pixels / PixelsPerSecond * sampleRate);

    public double SecondsToPixels(double seconds)
        => seconds * PixelsPerSecond;

    public double PixelsToSeconds(double pixels)
        => pixels / PixelsPerSecond;

    // ── 줌 ──

    public void ZoomIn(double pivotPixel = 0)
    {
        double oldPps = PixelsPerSecond;
        PixelsPerSecond = Math.Min(PixelsPerSecond * 1.5, 4000);
        AdjustScrollForZoom(oldPps, PixelsPerSecond, pivotPixel);
    }

    public void ZoomOut(double pivotPixel = 0)
    {
        double oldPps = PixelsPerSecond;
        PixelsPerSecond = Math.Max(PixelsPerSecond / 1.5, 5);
        AdjustScrollForZoom(oldPps, PixelsPerSecond, pivotPixel);
    }

    private void AdjustScrollForZoom(double oldPps, double newPps, double pivotPixel)
    {
        // 피벗 픽셀 위치의 시간이 유지되도록 스크롤 조정
        double pivotTime = (ScrollOffsetX + pivotPixel) / oldPps;
        ScrollOffsetX = Math.Max(0, pivotTime * newPps - pivotPixel);
    }

    // ── 플레이헤드 갱신 ──

    public void UpdatePlayhead(long frames, int sampleRate)
    {
        _playheadFrames = frames;
        PlayheadSeconds = (double)frames / sampleRate;
        OnPropertyChanged(nameof(PlayheadFrames));
        OnPropertyChanged(nameof(PlayheadSeconds));
    }

    // ── 스냅 ──

    public long SnapFrame(long frame, int sampleRate,
        Selah.Core.Models.TempoMap tempoMap)
    {
        if (!SnapEnabled) return frame;

        return SnapMode switch
        {
            SnapMode.Millisecond10 => RoundToNearest(frame, sampleRate / 100),
            SnapMode.Millisecond100 => RoundToNearest(frame, sampleRate / 10),
            SnapMode.Second => RoundToNearest(frame, sampleRate),
            SnapMode.Beat => RoundToNearest(frame, tempoMap.SamplesPerBeat(sampleRate)),
            SnapMode.HalfBeat => RoundToNearest(frame, tempoMap.SamplesPerBeat(sampleRate) / 2),
            SnapMode.Bar => RoundToNearest(frame,
                tempoMap.SamplesPerBeat(sampleRate) * tempoMap.GetNumerator()),
            _ => frame
        };
    }

    private static long RoundToNearest(long value, long unit)
    {
        if (unit <= 0) return value;
        return (value + unit / 2) / unit * unit;
    }
}

public enum SnapMode
{
    None,
    Millisecond10,
    Millisecond100,
    Second,
    HalfBeat,
    Beat,
    Bar
}
