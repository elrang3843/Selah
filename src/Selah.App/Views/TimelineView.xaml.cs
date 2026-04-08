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
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void TimelineScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // 스크롤 오프셋을 TimelineViewModel에 동기화
        if (ViewModel?.Timeline is TimelineViewModel tl)
            tl.ScrollOffsetX = e.HorizontalOffset;
    }

    private void Canvas_PlayheadSeeked(object? sender, long frames)
    {
        if (ViewModel == null) return;
        int sr = ViewModel.CurrentProject?.SampleRate ?? 48000;
        ViewModel.Timeline.UpdatePlayhead(frames, sr);
        // 재생 중이면 엔진 위치도 이동
        if (ViewModel.IsPlaying)
            ViewModel.AudioEngine.Seek(frames);
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
