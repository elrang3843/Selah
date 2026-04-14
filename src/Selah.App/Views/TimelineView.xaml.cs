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
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

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
