using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Selah.App.ViewModels;

namespace Selah.App.Views;

public partial class TimelineView : UserControl
{
    public TimelineView()
    {
        InitializeComponent();
        Canvas.ClipSelected += Canvas_ClipSelected;
        Canvas.ClipMoved += Canvas_ClipMoved;

        Loaded   += (_, _) => SubscribeTimeline(ViewModel);
        Unloaded += (_, _) => UnsubscribeTimeline(ViewModel);
        DataContextChanged += (_, e) =>
        {
            UnsubscribeTimeline(e.OldValue as MainViewModel);
            SubscribeTimeline(e.NewValue as MainViewModel);
        };
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void SubscribeTimeline(MainViewModel? vm)
    {
        if (vm == null) return;
        vm.Timeline.PropertyChanged += Timeline_PropertyChanged;
    }

    private void UnsubscribeTimeline(MainViewModel? vm)
    {
        if (vm == null) return;
        vm.Timeline.PropertyChanged -= Timeline_PropertyChanged;
    }

    /// <summary>재생 중 플레이헤드가 뷰포트 우측으로 나가면 자동 스크롤합니다.</summary>
    private void Timeline_PropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TimelineViewModel.PlayheadFrames)) return;
        var vm = ViewModel;
        if (vm?.IsPlaying != true) return;

        var tl = vm.Timeline;
        var proj = vm.CurrentProject;
        if (proj == null) return;

        double pxPos   = (double)tl.PlayheadFrames / proj.SampleRate * tl.PixelsPerSecond;
        double viewW   = TimelineScroll.ViewportWidth;
        double scrollL = TimelineScroll.HorizontalOffset;

        // 플레이헤드가 우측 여백(40px) 안으로 들어오거나 벗어나면 스크롤
        if (pxPos > scrollL + viewW - 40)
            TimelineScroll.ScrollToHorizontalOffset(Math.Max(0, pxPos - viewW * 0.15));
        else if (pxPos < scrollL)
            TimelineScroll.ScrollToHorizontalOffset(Math.Max(0, pxPos - viewW * 0.15));
    }

    private void TimelineScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (ViewModel?.Timeline is TimelineViewModel tl)
            tl.ScrollOffsetX = e.HorizontalOffset;
    }

    private void Canvas_PlayheadSeeked(object? sender, long frames)
    {
        if (ViewModel == null) return;
        int sr = ViewModel.CurrentProject?.SampleRate ?? 48000;
        ViewModel.Timeline.UpdatePlayhead(frames, sr);
        if (ViewModel.IsPlaying)
            ViewModel.AudioEngine.Seek(frames);
    }

    private void Canvas_ClipSelected(object? sender,
        (Selah.App.ViewModels.TrackViewModel? Track, Selah.App.ViewModels.ClipViewModel? Clip) e)
    {
        if (ViewModel == null) return;
        if (e.Track != null) ViewModel.SelectedTrack = e.Track;
        ViewModel.SelectedClip = e.Clip;
    }

    private void Canvas_ClipMoved(object? sender, EventArgs e)
    {
        ViewModel?.AudioEngine.RebuildMixers();
    }

    private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var tl = ViewModel?.Timeline;
        if (tl == null) return;

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            double pivotX = e.GetPosition(Canvas).X;
            if (e.Delta > 0) tl.ZoomIn(pivotX);
            else tl.ZoomOut(pivotX);
            e.Handled = true;
        }
    }
}
