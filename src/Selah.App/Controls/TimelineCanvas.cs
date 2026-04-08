using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Selah.App.ViewModels;

namespace Selah.App.Controls;

/// <summary>
/// 타임라인 캔버스 커스텀 컨트롤.
/// 눈금자(Ruler), 트랙 클립, 플레이헤드를 직접 렌더링합니다.
/// </summary>
public class TimelineCanvas : FrameworkElement
{
    public const double RulerHeight = 28;
    public const double TrackDefaultHeight = 80;

    // ── 의존성 프로퍼티 ──

    public static readonly DependencyProperty ProjectViewModelProperty =
        DependencyProperty.Register(nameof(ProjectViewModel), typeof(ProjectViewModel),
            typeof(TimelineCanvas), new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender, OnProjectChanged));

    public static readonly DependencyProperty TimelineViewModelProperty =
        DependencyProperty.Register(nameof(TimelineViewModel), typeof(TimelineViewModel),
            typeof(TimelineCanvas), new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender, OnTimelineChanged));

    public ProjectViewModel? ProjectViewModel
    {
        get => (ProjectViewModel?)GetValue(ProjectViewModelProperty);
        set => SetValue(ProjectViewModelProperty, value);
    }

    public TimelineViewModel? TimelineViewModel
    {
        get => (TimelineViewModel?)GetValue(TimelineViewModelProperty);
        set => SetValue(TimelineViewModelProperty, value);
    }

    private static void OnProjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TimelineCanvas)d).InvalidateVisual();

    private static void OnTimelineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (TimelineCanvas)d;
        if (e.OldValue is TimelineViewModel old)
            old.PropertyChanged -= canvas.OnTimelinePropertyChanged;
        if (e.NewValue is TimelineViewModel nw)
            nw.PropertyChanged += canvas.OnTimelinePropertyChanged;
        canvas.InvalidateVisual();
    }

    private void OnTimelinePropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TimelineViewModel.PlayheadFrames)
                           or nameof(TimelineViewModel.PixelsPerSecond)
                           or nameof(TimelineViewModel.ScrollOffsetX))
            InvalidateVisual();
    }

    // ── 브러시 ──

    private static readonly Brush RulerBg = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x25));
    private static readonly Brush RulerText = new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8));
    private static readonly Brush RulerTick = new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A));
    private static readonly Brush TrackBg = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E));
    private static readonly Brush TrackSep = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44));
    private static readonly Brush PlayheadBrush = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));
    private static readonly Brush ClipText = new SolidColorBrush(Colors.White);
    private static readonly Pen PlayheadPen = new(PlayheadBrush, 1.5);
    private static readonly Pen TrackSepPen = new(TrackSep, 1);
    private static readonly Pen RulerTickPen = new(RulerTick, 1);

    static TimelineCanvas()
    {
        PlayheadPen.Freeze();
        TrackSepPen.Freeze();
        RulerTickPen.Freeze();
        RulerBg.Freeze(); RulerText.Freeze(); RulerTick.Freeze();
        TrackBg.Freeze(); TrackSep.Freeze(); PlayheadBrush.Freeze();
        ClipText.Freeze();
    }

    // ── 렌더링 ──

    protected override void OnRender(DrawingContext dc)
    {
        var tl = TimelineViewModel;
        var proj = ProjectViewModel;
        double w = ActualWidth;
        double h = ActualHeight;

        // 배경
        dc.DrawRectangle(TrackBg, null, new Rect(0, 0, w, h));

        // 눈금자
        DrawRuler(dc, tl, proj, w);

        // 트랙 + 클립
        if (proj != null && tl != null)
            DrawTracks(dc, proj, tl, w, h);

        // 플레이헤드
        if (tl != null && proj != null)
            DrawPlayhead(dc, tl, proj, h);
    }

    private static void DrawRuler(DrawingContext dc, TimelineViewModel? tl,
        ProjectViewModel? proj, double w)
    {
        dc.DrawRectangle(RulerBg, null, new Rect(0, 0, w, RulerHeight));

        if (tl == null || proj == null) return;

        double pps = tl.PixelsPerSecond;
        double scrollX = tl.ScrollOffsetX;
        int sr = proj.SampleRate;

        // 눈금 간격 결정 (픽셀 밀도에 따라 적응형)
        double minTickPx = 60;
        double[] intervals = { 0.01, 0.05, 0.1, 0.5, 1, 2, 5, 10, 30, 60, 120, 300 };
        double tickInterval = intervals.FirstOrDefault(iv => iv * pps >= minTickPx);
        if (tickInterval == 0) tickInterval = 300;

        double startTime = scrollX / pps;
        double endTime = (scrollX + w) / pps;

        for (double t = Math.Floor(startTime / tickInterval) * tickInterval;
             t <= endTime; t += tickInterval)
        {
            double x = t * pps - scrollX;
            if (x < 0 || x > w) continue;

            bool isMajor = Math.Abs(t % (tickInterval * 5)) < tickInterval * 0.01;
            double tickH = isMajor ? 14 : 7;
            dc.DrawLine(RulerTickPen, new Point(x, RulerHeight - tickH), new Point(x, RulerHeight));

            if (isMajor)
            {
                var ts = TimeSpan.FromSeconds(t);
                string label = ts.TotalMinutes >= 1
                    ? $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}"
                    : $"{t:G3}s";

                var ft = new FormattedText(label, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 10, RulerText, 96);
                dc.DrawText(ft, new Point(x + 2, 4));
            }
        }
    }

    private static void DrawTracks(DrawingContext dc, ProjectViewModel proj,
        TimelineViewModel tl, double w, double h)
    {
        double y = RulerHeight;
        double scrollX = tl.ScrollOffsetX;
        double pps = tl.PixelsPerSecond;
        int sr = proj.SampleRate;

        foreach (var track in proj.Tracks)
        {
            double trackH = track.HeightPixels;

            // 트랙 배경
            var trackBg = track.Muted
                ? new SolidColorBrush(Color.FromArgb(40, 0x45, 0x47, 0x5A))
                : new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x3A));
            dc.DrawRectangle(trackBg, null, new Rect(0, y, w, trackH));

            // 클립 그리기
            foreach (var clip in track.Clips)
            {
                double clipStartPx = (double)clip.TimelineStartSamples / sr * pps - scrollX;
                double clipWidthPx = (double)clip.LengthSamples / sr * pps;

                if (clipStartPx + clipWidthPx < 0 || clipStartPx > w) continue;

                double cx = Math.Max(clipStartPx, 0);
                double cw = clipWidthPx - (cx - clipStartPx);
                cw = Math.Min(cw, w - cx);

                if (cw < 1) continue;

                var clipRect = new Rect(cx, y + 2, cw, trackH - 4);

                // 클립 색상
                var clipColor = ParseHex(track.Color);
                var clipBg = new SolidColorBrush(
                    Color.FromArgb(clip.Muted ? (byte)80 : (byte)200,
                        clipColor.R, clipColor.G, clipColor.B));
                var clipBorder = new Pen(new SolidColorBrush(clipColor), 1);

                dc.DrawRectangle(clipBg, clipBorder, clipRect);

                // 페이드 인 시각화
                if (clip.FadeInSamples > 0)
                {
                    double fadeW = (double)clip.FadeInSamples / sr * pps;
                    fadeW = Math.Min(fadeW, cw);
                    var fadeGeom = new PathGeometry();
                    var figure = new PathFigure { StartPoint = new Point(cx, y + trackH - 4) };
                    figure.Segments.Add(new LineSegment(new Point(cx, y + 2), true));
                    figure.Segments.Add(new LineSegment(new Point(cx + fadeW, y + 2), true));
                    figure.IsClosed = true;
                    fadeGeom.Figures.Add(figure);
                    dc.DrawGeometry(
                        new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
                        null, fadeGeom);
                }

                // 클립 이름
                if (cw > 40)
                {
                    var ft = new FormattedText(clip.DisplayName,
                        CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"), 11, ClipText, 96);
                    ft.MaxTextWidth = cw - 4;
                    ft.Trimming = TextTrimming.CharacterEllipsis;
                    dc.DrawText(ft, new Point(cx + 4, y + 5));
                }
            }

            // 트랙 구분선
            dc.DrawLine(TrackSepPen, new Point(0, y + trackH), new Point(w, y + trackH));
            y += trackH;
        }
    }

    private static void DrawPlayhead(DrawingContext dc, TimelineViewModel tl,
        ProjectViewModel proj, double h)
    {
        double x = (double)tl.PlayheadFrames / proj.SampleRate * tl.PixelsPerSecond
                   - tl.ScrollOffsetX;
        if (x < 0) return;

        dc.DrawLine(PlayheadPen, new Point(x, 0), new Point(x, h));

        // 플레이헤드 삼각형 표시
        var tri = new StreamGeometry();
        using (var ctx = tri.Open())
        {
            ctx.BeginFigure(new Point(x - 5, 0), true, true);
            ctx.LineTo(new Point(x + 5, 0), true, false);
            ctx.LineTo(new Point(x, 10), true, false);
        }
        tri.Freeze();
        dc.DrawGeometry(PlayheadBrush, null, tri);
    }

    private static Color ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            byte r = System.Convert.ToByte(hex[..2], 16);
            byte g = System.Convert.ToByte(hex[2..4], 16);
            byte b = System.Convert.ToByte(hex[4..6], 16);
            return Color.FromRgb(r, g, b);
        }
        return Colors.SteelBlue;
    }

    // ── 마우스 인터랙션 ──

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var pos = e.GetPosition(this);
        if (pos.Y <= RulerHeight) // 눈금자 클릭 → 플레이헤드 이동
        {
            SeekToPixel(pos.X);
            CaptureMouse();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
            SeekToPixel(e.GetPosition(this).X);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        ReleaseMouseCapture();
    }

    private void SeekToPixel(double px)
    {
        var tl = TimelineViewModel;
        var proj = ProjectViewModel;
        if (tl == null || proj == null) return;

        long frame = tl.PixelsToFrames(px + tl.ScrollOffsetX, proj.SampleRate);
        frame = Math.Max(0, frame);
        if (tl.SnapEnabled)
            frame = tl.SnapFrame(frame, proj.SampleRate, proj.Model.TempoMap);

        tl.PlayheadFrames = frame;

        // MainViewModel에 알림 (데이터바인딩을 통해 연결)
        PlayheadSeeked?.Invoke(this, frame);
    }

    public event EventHandler<long>? PlayheadSeeked;

    // ── 크기 계산 ──

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = availableSize.Width;
        double h = RulerHeight;
        if (ProjectViewModel != null)
            h += ProjectViewModel.Tracks.Sum(t => t.HeightPixels);
        h = Math.Max(h, availableSize.Height);
        return new Size(w, h);
    }
}
