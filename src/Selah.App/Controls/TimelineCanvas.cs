using System.Collections.Specialized;
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
    public const double MetronomeTrackHeight = 40;

    // ── 의존성 프로퍼티 ──

    public static readonly DependencyProperty ProjectViewModelProperty =
        DependencyProperty.Register(nameof(ProjectViewModel), typeof(ProjectViewModel),
            typeof(TimelineCanvas), new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender, OnProjectChanged));

    public static readonly DependencyProperty TimelineViewModelProperty =
        DependencyProperty.Register(nameof(TimelineViewModel), typeof(TimelineViewModel),
            typeof(TimelineCanvas), new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender, OnTimelineChanged));

    public static readonly DependencyProperty IsMetronomeVisibleProperty =
        DependencyProperty.Register(nameof(IsMetronomeVisible), typeof(bool),
            typeof(TimelineCanvas), new FrameworkPropertyMetadata(false,
                FrameworkPropertyMetadataOptions.AffectsRender,
                (d, _) => { var c = (TimelineCanvas)d; c.InvalidateMeasure(); c.InvalidateVisual(); }));

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

    public bool IsMetronomeVisible
    {
        get => (bool)GetValue(IsMetronomeVisibleProperty);
        set => SetValue(IsMetronomeVisibleProperty, value);
    }

    // ── 구독 관리 ──

    private ProjectViewModel? _subscribedProject;

    private static void OnProjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (TimelineCanvas)d;
        canvas.UnsubscribeFromProject(canvas._subscribedProject);
        canvas._subscribedProject = (ProjectViewModel?)e.NewValue;
        canvas.SubscribeToProject(canvas._subscribedProject);
        canvas.InvalidateMeasure();
        canvas.InvalidateVisual();
    }

    private void SubscribeToProject(ProjectViewModel? proj)
    {
        if (proj == null) return;
        proj.Tracks.CollectionChanged += OnTracksChanged;
        foreach (var track in proj.Tracks)
            track.Clips.CollectionChanged += OnClipsChanged;
    }

    private void UnsubscribeFromProject(ProjectViewModel? proj)
    {
        if (proj == null) return;
        proj.Tracks.CollectionChanged -= OnTracksChanged;
        foreach (var track in proj.Tracks)
            track.Clips.CollectionChanged -= OnClipsChanged;
    }

    private void OnTracksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (TrackViewModel track in e.NewItems)
                track.Clips.CollectionChanged += OnClipsChanged;
        if (e.OldItems != null)
            foreach (TrackViewModel track in e.OldItems)
                track.Clips.CollectionChanged -= OnClipsChanged;

        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnClipsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

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
    private static readonly Brush ClipLabelBg = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0));
    private static readonly Brush MetronomeBg = new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x20));
    private static readonly Brush SelectedClipBorderBrush = new SolidColorBrush(Colors.White);
    private static readonly Pen PlayheadPen = new(PlayheadBrush, 1.5);
    private static readonly Pen TrackSepPen = new(TrackSep, 1);
    private static readonly Pen RulerTickPen = new(RulerTick, 1);
    private static readonly Pen SelectedClipPen = new(SelectedClipBorderBrush, 2);

    static TimelineCanvas()
    {
        PlayheadPen.Freeze();
        TrackSepPen.Freeze();
        RulerTickPen.Freeze();
        SelectedClipPen.Freeze();
        RulerBg.Freeze(); RulerText.Freeze(); RulerTick.Freeze();
        TrackBg.Freeze(); TrackSep.Freeze(); PlayheadBrush.Freeze();
        ClipText.Freeze(); ClipLabelBg.Freeze();
        MetronomeBg.Freeze(); SelectedClipBorderBrush.Freeze();
    }

    // ── 선택/드래그 상태 ──

    private ClipViewModel? _selectedClip;
    private TrackViewModel? _selectedClipTrack;
    private bool _isDraggingClip;
    private double _dragStartX;
    private long _dragClipOrigStart;

    // ── 이벤트 ──

    public event EventHandler<long>? PlayheadSeeked;
    public event EventHandler<(TrackViewModel? Track, ClipViewModel? Clip)>? ClipSelected;
    public event EventHandler? ClipMoved;
    public event EventHandler<(TrackViewModel Track, ClipViewModel Clip)>? ClipDeleteRequested;

    // ── 렌더링 ──

    protected override void OnRender(DrawingContext dc)
    {
        var tl = TimelineViewModel;
        var proj = ProjectViewModel;
        double w = ActualWidth;
        double h = ActualHeight;

        dc.DrawRectangle(TrackBg, null, new Rect(0, 0, w, h));
        DrawRuler(dc, tl, proj, w);

        if (proj != null && tl != null)
            DrawTracks(dc, proj, tl, w, h);

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

    private void DrawTracks(DrawingContext dc, ProjectViewModel proj,
        TimelineViewModel tl, double w, double h)
    {
        double y = RulerHeight;
        double scrollX = tl.ScrollOffsetX;
        double pps = tl.PixelsPerSecond;
        int sr = proj.SampleRate;

        // 메트로놈 트랙 (#0) — 활성화 시에만 배경 렌더링
        if (IsMetronomeVisible)
        {
            dc.DrawRectangle(MetronomeBg, null, new Rect(0, y, w, MetronomeTrackHeight));
            dc.DrawLine(TrackSepPen, new Point(0, y + MetronomeTrackHeight),
                new Point(w, y + MetronomeTrackHeight));
            y += MetronomeTrackHeight;
        }

        foreach (var track in proj.Tracks)
        {
            double trackH = track.HeightPixels;

            var trackBg = track.Muted
                ? new SolidColorBrush(Color.FromArgb(40, 0x45, 0x47, 0x5A))
                : new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x3A));
            dc.DrawRectangle(trackBg, null, new Rect(0, y, w, trackH));

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

                var clipColor = ParseHex(track.Color);
                var clipBg = new SolidColorBrush(
                    Color.FromArgb(clip.Muted ? (byte)80 : (byte)200,
                        clipColor.R, clipColor.G, clipColor.B));

                bool isSelected = clip == _selectedClip;
                var borderPen = isSelected
                    ? SelectedClipPen
                    : new Pen(new SolidColorBrush(clipColor), 1);

                dc.DrawRectangle(clipBg, borderPen, clipRect);

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
                    dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), null, fadeGeom);
                }

                // 클립 이름 (충분한 너비일 때만)
                if (cw > 40)
                {
                    var ft = new FormattedText(clip.DisplayName,
                        CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"), 11, ClipText, 96);
                    ft.MaxTextWidth = cw - 10;
                    ft.Trimming = TextTrimming.CharacterEllipsis;

                    double labelX = cx + 4;
                    double labelY = y + 5;
                    double labelW = Math.Min(ft.Width + 6, cw - 6);
                    double labelH = ft.Height + 2;
                    dc.DrawRectangle(ClipLabelBg, null, new Rect(labelX - 2, labelY - 1, labelW, labelH));
                    dc.DrawText(ft, new Point(labelX, labelY));
                }
            }

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

    // ── 히트 테스트 ──

    private (TrackViewModel? track, ClipViewModel? clip) HitTestClip(double mouseX, double mouseY)
    {
        var proj = ProjectViewModel;
        var tl = TimelineViewModel;
        if (proj == null || tl == null) return (null, null);

        double trackY = RulerHeight;
        double scrollX = tl.ScrollOffsetX;
        double pps = tl.PixelsPerSecond;
        int sr = proj.SampleRate;

        if (IsMetronomeVisible)
            trackY += MetronomeTrackHeight;

        foreach (var track in proj.Tracks)
        {
            double trackH = track.HeightPixels;
            if (mouseY >= trackY && mouseY < trackY + trackH)
            {
                foreach (var clip in track.Clips)
                {
                    double clipStartPx = (double)clip.TimelineStartSamples / sr * pps - scrollX;
                    double clipWidthPx = (double)clip.LengthSamples / sr * pps;
                    if (mouseX >= clipStartPx && mouseX <= clipStartPx + clipWidthPx)
                        return (track, clip);
                }
                return (track, null);
            }
            trackY += trackH;
        }
        return (null, null);
    }

    // ── 마우스 인터랙션 ──

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var pos = e.GetPosition(this);

        if (pos.Y <= RulerHeight)
        {
            SeekToPixel(pos.X);
            CaptureMouse();
            return;
        }

        var (track, clip) = HitTestClip(pos.X, pos.Y);
        _selectedClip = clip;
        _selectedClipTrack = track;
        ClipSelected?.Invoke(this, (track, clip));

        if (clip != null)
        {
            _isDraggingClip = true;
            _dragStartX = pos.X;
            _dragClipOrigStart = clip.TimelineStartSamples;
            CaptureMouse();
        }

        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!IsMouseCaptured) return;

        var pos = e.GetPosition(this);

        if (_isDraggingClip && _selectedClip != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var tl = TimelineViewModel;
            var proj = ProjectViewModel;
            if (tl == null || proj == null) return;

            double deltaX = pos.X - _dragStartX;
            long deltaFrames = (long)(deltaX * proj.SampleRate / tl.PixelsPerSecond);
            long newStart = Math.Max(0, _dragClipOrigStart + deltaFrames);

            if (tl.SnapEnabled)
                newStart = tl.SnapFrame(newStart, proj.SampleRate, proj.Model.TempoMap);

            _selectedClip.TimelineStartSamples = newStart;
            InvalidateVisual();
        }
        else if (e.LeftButton == MouseButtonState.Pressed && !_isDraggingClip)
        {
            SeekToPixel(pos.X);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_isDraggingClip)
            ClipMoved?.Invoke(this, EventArgs.Empty);

        _isDraggingClip = false;
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
        PlayheadSeeked?.Invoke(this, frame);
    }

    // ── 크기 계산 ──

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = 1000;
        var tl = TimelineViewModel;
        var proj = ProjectViewModel;
        if (proj != null && tl != null && proj.Model.TotalLengthSamples > 0)
            w = (double)proj.Model.TotalLengthSamples / proj.SampleRate * tl.PixelsPerSecond + 200;
        if (!double.IsPositiveInfinity(availableSize.Width))
            w = Math.Max(w, availableSize.Width);

        double h = RulerHeight;
        if (IsMetronomeVisible) h += MetronomeTrackHeight;
        if (proj != null)
            h += proj.Tracks.Sum(t => t.HeightPixels);
        if (!double.IsPositiveInfinity(availableSize.Height))
            h = Math.Max(h, availableSize.Height);

        return new Size(w, h);
    }
}
