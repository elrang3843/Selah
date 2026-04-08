using Selah.Core.Models;

namespace Selah.App.ViewModels;

public class ClipViewModel : ViewModelBase
{
    private readonly Clip _clip;
    private readonly Project _project;
    private bool _isSelected;

    public ClipViewModel(Clip clip, Project project)
    {
        _clip = clip;
        _project = project;
    }

    public string Id => _clip.Id;
    public Clip Model => _clip;

    public long TimelineStartSamples
    {
        get => _clip.TimelineStartSamples;
        set
        {
            _clip.TimelineStartSamples = Math.Max(0, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(TimelineStartSeconds));
            _project.IsDirty = true;
        }
    }

    public long SourceInSamples
    {
        get => _clip.SourceInSamples;
        set { _clip.SourceInSamples = value; OnPropertyChanged(); _project.IsDirty = true; }
    }

    public long SourceOutSamples
    {
        get => _clip.SourceOutSamples;
        set { _clip.SourceOutSamples = value; OnPropertyChanged(); _project.IsDirty = true; }
    }

    public long LengthSamples => _clip.LengthSamples;
    public long TimelineEndSamples => _clip.TimelineEndSamples;

    public long FadeInSamples
    {
        get => _clip.FadeInSamples;
        set { _clip.FadeInSamples = value; OnPropertyChanged(); _project.IsDirty = true; }
    }

    public long FadeOutSamples
    {
        get => _clip.FadeOutSamples;
        set { _clip.FadeOutSamples = value; OnPropertyChanged(); _project.IsDirty = true; }
    }

    public float GainDb
    {
        get => _clip.GainDb;
        set { _clip.GainDb = value; OnPropertyChanged(); _project.IsDirty = true; }
    }

    public bool Muted
    {
        get => _clip.Muted;
        set { _clip.Muted = value; OnPropertyChanged(); _project.IsDirty = true; }
    }

    public double TimelineStartSeconds => (double)_clip.TimelineStartSamples / _project.SampleRate;
    public double LengthSeconds => (double)_clip.LengthSamples / _project.SampleRate;

    public string DisplayName
    {
        get
        {
            var src = _project.AudioSources.FirstOrDefault(s => s.Id == _clip.SourceId);
            return src?.Name ?? "알 수 없는 소스";
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    /// <summary>커서 위치에서 클립을 두 개로 분할합니다.</summary>
    public (ClipViewModel left, ClipViewModel right) Split(long splitTimelineFrame)
    {
        if (splitTimelineFrame <= _clip.TimelineStartSamples ||
            splitTimelineFrame >= _clip.TimelineEndSamples)
            throw new ArgumentOutOfRangeException(nameof(splitTimelineFrame));

        long relFrame = splitTimelineFrame - _clip.TimelineStartSamples;
        long splitSourceFrame = _clip.SourceInSamples + relFrame;

        var leftClip = new Clip
        {
            SourceId = _clip.SourceId,
            TimelineStartSamples = _clip.TimelineStartSamples,
            SourceInSamples = _clip.SourceInSamples,
            SourceOutSamples = splitSourceFrame,
            GainDb = _clip.GainDb,
            FadeInSamples = _clip.FadeInSamples,
            FadeOutSamples = 0
        };

        var rightClip = new Clip
        {
            SourceId = _clip.SourceId,
            TimelineStartSamples = splitTimelineFrame,
            SourceInSamples = splitSourceFrame,
            SourceOutSamples = _clip.SourceOutSamples,
            GainDb = _clip.GainDb,
            FadeInSamples = 0,
            FadeOutSamples = _clip.FadeOutSamples
        };

        return (new ClipViewModel(leftClip, _project), new ClipViewModel(rightClip, _project));
    }
}
