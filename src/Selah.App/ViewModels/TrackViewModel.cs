using System.Collections.ObjectModel;
using System.Windows.Input;
using Selah.Core.Models;

namespace Selah.App.ViewModels;

public class TrackViewModel : ViewModelBase
{
    private readonly Track _track;
    private readonly Project _project;
    public TrackViewModel(Track track, Project project)
    {
        _track = track;
        _project = project;
        Clips = new ObservableCollection<ClipViewModel>(
            track.Clips.Select(c => new ClipViewModel(c, project)));

        MuteCommand = new RelayCommand(() => Muted = !Muted);
        SoloCommand = new RelayCommand(() => Solo = !Solo);
    }

    public string Id => _track.Id;
    public Track Model => _track;

    public string Name
    {
        get => _track.Name;
        set { _track.Name = value; OnPropertyChanged(); _project.IsDirty = true; }
    }

    public float GainDb
    {
        get => _track.GainDb;
        set
        {
            _track.GainDb = Math.Clamp(value, -60f, 12f);
            OnPropertyChanged();
            _project.IsDirty = true;
        }
    }

    public float Pan
    {
        get => _track.Pan;
        set
        {
            _track.Pan = Math.Clamp(value, -1f, 1f);
            OnPropertyChanged();
            _project.IsDirty = true;
        }
    }

    public bool Muted
    {
        get => _track.Muted;
        set { _track.Muted = value; OnPropertyChanged(); _project.IsDirty = true; }
    }

    public bool Solo
    {
        get => _track.Solo;
        set { _track.Solo = value; OnPropertyChanged(); _project.IsDirty = true; }
    }

    public string Color
    {
        get => _track.Color;
        set { _track.Color = value; OnPropertyChanged(); _project.IsDirty = true; }
    }

    public float HeightPixels
    {
        get => _track.HeightPixels;
        set { _track.HeightPixels = Math.Clamp(value, 40f, 300f); OnPropertyChanged(); }
    }

    public bool IsEnabled
    {
        get => _track.IsEnabled;
        set { _track.IsEnabled = value; OnPropertyChanged(); _project.IsDirty = true; }
    }

    public ObservableCollection<ClipViewModel> Clips { get; }

    public ICommand MuteCommand { get; }
    public ICommand SoloCommand { get; }

    public void AddClip(ClipViewModel clip)
    {
        _track.Clips.Add(clip.Model);
        Clips.Add(clip);
        _project.IsDirty = true;
    }

    public void RemoveClip(ClipViewModel clip)
    {
        _track.Clips.Remove(clip.Model);
        Clips.Remove(clip);
        _project.IsDirty = true;
    }

    /// <summary>
    /// 선택된 클립들을 하나로 합칩니다.
    /// 첫 번째 클립의 소스를 기준으로 SourceIn~마지막 클립의 SourceOut 범위를 사용합니다.
    /// </summary>
    public void MergeClips(IList<ClipViewModel> clipsToMerge)
    {
        if (clipsToMerge.Count < 2) return;
        var ordered = clipsToMerge.OrderBy(c => c.TimelineStartSamples).ToList();
        int insertIdx = Clips.IndexOf(ordered[0]);

        var mergedModel = new Clip
        {
            SourceId             = ordered[0].Model.SourceId,
            TimelineStartSamples = ordered[0].TimelineStartSamples,
            SourceInSamples      = ordered[0].Model.SourceInSamples,
            SourceOutSamples     = ordered[^1].Model.SourceOutSamples,
            GainDb               = ordered[0].Model.GainDb,
            Pan                  = ordered[0].Model.Pan,
            Muted                = ordered[0].Model.Muted
        };

        foreach (var clip in ordered)
        {
            _track.Clips.Remove(clip.Model);
            Clips.Remove(clip);
        }

        int idx = Math.Min(insertIdx, Clips.Count);
        var mergedVm = new ClipViewModel(mergedModel, _project) { IsSelected = true };
        _track.Clips.Insert(idx, mergedModel);
        Clips.Insert(idx, mergedVm);
        _project.IsDirty = true;
    }

    /// <summary>커서 위치의 클립을 분할합니다 (트랙에서 처음 발견되는 클립).</summary>
    public bool SplitClipAt(long timelineFrame)
    {
        var target = Clips.FirstOrDefault(c =>
            timelineFrame > c.TimelineStartSamples && timelineFrame < c.TimelineEndProjectFrame);
        return target != null && SplitSpecificClip(target, timelineFrame);
    }

    /// <summary>지정한 클립을 timelineFrame 위치에서 분할합니다.</summary>
    public bool SplitSpecificClip(ClipViewModel clip, long timelineFrame)
    {
        if (timelineFrame <= clip.TimelineStartSamples || timelineFrame >= clip.TimelineEndProjectFrame)
            return false;

        var (left, right) = clip.Split(timelineFrame);
        int idx = Clips.IndexOf(clip);

        _track.Clips.Remove(clip.Model);
        Clips.Remove(clip);

        _track.Clips.Insert(idx, left.Model);
        Clips.Insert(idx, left);
        _track.Clips.Insert(idx + 1, right.Model);
        Clips.Insert(idx + 1, right);

        _project.IsDirty = true;
        return true;
    }
}
