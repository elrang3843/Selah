using System.Windows.Input;
using NAudio.Wave;
using Selah.Core.Audio;
using Selah.Core.Models;
using Selah.Core.Services;

namespace Selah.App.ViewModels;

/// <summary>
/// 메인 윈도우 ViewModel. 앱 전반 상태와 최상위 커맨드를 관리합니다.
/// </summary>
public class MainViewModel : ViewModelBase, IDisposable
{
    // ── 서비스 ──
    public readonly ProjectService ProjectService = new();
    public readonly FFmpegService FFmpegService = new();
    public readonly ModelManagerService ModelManagerService = new();
    public readonly HardwareDetectionService HardwareDetectionService = new();
    public readonly AudioEngine AudioEngine = new();
    public readonly AudioRenderer AudioRenderer = new();
    public readonly StemSeparatorService StemSeparator;
    public readonly NoiseReductionService NoiseReducer;
    public readonly FluidSynthService FluidSynthService = new();
    public readonly SheetMusicService SheetMusicService;

    private ProjectViewModel? _currentProject;
    private string _statusMessage;
    private bool _isBusy;
    private string _progressTitle = string.Empty;
    private double _progressPercent = -1;
    private HardwareInfo? _hardwareInfo;
    private bool _disposed;

    // ── 요청 이벤트 (코드 비하인드에서 다이얼로그를 띄움) ──
    public event Func<(string name, int sampleRate), Task>? NewProjectRequested;
    public event Func<Task<string?>>? OpenProjectFolderRequested;
    public event Func<Task<string?>>? SaveProjectFolderRequested;
    public event Func<Task<string[]?>>? ImportAudioRequested;
    public event Func<Task<string?>>? ExportPathRequested;
    public event Action<string>? ErrorOccurred;
    /// <summary>도구 미설치 오류 발생 시 설치 안내 페이지 열기를 요청합니다.</summary>
    public event Action? SetupGuideRequested;
    /// <summary>악보 인식 다이얼로그 열기를 요청합니다. null 반환 시 취소.</summary>
    public event Func<Task<SheetMusicDialogResult?>>? ImportSheetMusicRequested;
    /// <summary>시간이 걸리는 작업 시작 시 발생. 인수: 작업 제목.</summary>
    public event Action<string>? ProgressStarted;
    /// <summary>시간이 걸리는 작업 완료(또는 실패) 시 발생.</summary>
    public event Action? ProgressFinished;

    public MainViewModel()
    {
        _statusMessage = Loc.Get("Status_Ready");

        FFmpegService.Detect();
        FluidSynthService.Detect();
        _hardwareInfo    = HardwareDetectionService.Detect();
        StemSeparator    = new StemSeparatorService(ModelManagerService);
        NoiseReducer     = new NoiseReductionService();
        SheetMusicService = new SheetMusicService(FluidSynthService);

        Timeline = new TimelineViewModel();
        ModelManager = new ModelManagerViewModel(ModelManagerService);

        // ── 커맨드 초기화 ──
        NewProjectCommand = new AsyncRelayCommand(OnNewProject);
        OpenProjectCommand = new AsyncRelayCommand(OnOpenProject);
        SaveProjectCommand = new AsyncRelayCommand(OnSaveProject, () => CurrentProject != null);
        SaveAsCommand = new AsyncRelayCommand(OnSaveAs, () => CurrentProject != null);
        ImportAudioCommand = new AsyncRelayCommand(OnImportAudio, () => CurrentProject != null);
        AddTrackCommand = new RelayCommand(OnAddTrack, () => CurrentProject != null);
        DeleteSelectedTrackCommand = new RelayCommand(OnDeleteSelectedTrack, () => SelectedTrack?.IsEnabled == true);
        PlayCommand = new RelayCommand(OnTogglePlay, () => CurrentProject != null);
        StopCommand = new RelayCommand(OnStop, () => CurrentProject != null);
        ReturnToStartCommand = new RelayCommand(OnReturnToStart, () => CurrentProject != null);
        ExportCommand = new AsyncRelayCommand(OnExport, () => CurrentProject != null);
        SplitAtPlayheadCommand = new RelayCommand(OnSplitAtPlayhead, () => CurrentProject != null && SelectedTrack?.IsEnabled == true);
        ToggleMetronomeCommand = new RelayCommand(OnToggleMetronome, () => CurrentProject != null);
        ToggleSnapCommand = new RelayCommand(() => Timeline.SnapEnabled = !Timeline.SnapEnabled);
        DeleteCommand = new RelayCommand(OnDelete, () => CurrentProject != null && SelectedTrack?.IsEnabled == true);
        SeparateClipCommand = new AsyncRelayCommand(SeparateClipAsync,
            () => CurrentProject != null && SelectedClip != null && SelectedTrack?.IsEnabled == true);
        ReduceNoiseCommand = new AsyncRelayCommand(ReduceNoiseClipAsync,
            () => CurrentProject != null && SelectedClip != null && SelectedTrack?.IsEnabled == true);
        ImportSheetMusicCommand = new AsyncRelayCommand(ImportSheetMusicAsync,
            () => CurrentProject != null);
        CopyCommand = new RelayCommand(OnCopy,
            () => CurrentProject != null && SelectedClip != null);
        PasteCommand = new RelayCommand(OnPaste,
            () => CurrentProject != null && _clipboard?.Count > 0);
        CutCommand = new RelayCommand(OnCut,
            () => CurrentProject != null && SelectedClip != null && SelectedTrack?.IsEnabled == true);
        MergeCommand = new RelayCommand(OnMerge,
            () => CurrentProject != null && SelectedClip != null);
        MoveAfterPreviousCommand = new RelayCommand(OnMoveAfterPrevious,
            () => CurrentProject != null && SelectedClip != null && SelectedTrack != null);

        AudioEngine.PlayheadAdvanced += OnPlayheadAdvanced;
        AudioEngine.PlaybackStopped += OnPlaybackStopped;
        AudioEngine.ContentEnded += OnContentEnded;

        Loc.LanguageChanged += OnLanguageChanged;
    }

    // ── 프로퍼티 ──

    public ProjectViewModel? CurrentProject
    {
        get => _currentProject;
        private set
        {
            SetField(ref _currentProject, value);
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(HasProject));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    private TrackViewModel? _selectedTrack;
    public TrackViewModel? SelectedTrack
    {
        get => _selectedTrack;
        set
        {
            SetField(ref _selectedTrack, value);
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    private ClipViewModel? _selectedClip;
    public ClipViewModel? SelectedClip
    {
        get => _selectedClip;
        set
        {
            SetField(ref _selectedClip, value);
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    public TimelineViewModel Timeline { get; }
    public ModelManagerViewModel ModelManager { get; }

    public string WindowTitle =>
        CurrentProject == null
            ? Loc.Get("WindowTitle_Default")
            : $"{CurrentProject.Name}{(CurrentProject.IsDirty ? " *" : "")}{Loc.Get("WindowTitle_Suffix")}";

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public string ProgressTitle
    {
        get => _progressTitle;
        private set => SetField(ref _progressTitle, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set
        {
            SetField(ref _progressPercent, value);
            OnPropertyChanged(nameof(IsProgressIndeterminate));
        }
    }

    public bool IsProgressIndeterminate => _progressPercent < 0;

    public bool HasProject => CurrentProject != null;
    public bool IsFFmpegAvailable => FFmpegService.IsAvailable;

    public string HardwareStatusText =>
        _hardwareInfo?.BackendDisplayName ?? Loc.Get("Status_Detecting");

    public string SampleRateText =>
        CurrentProject != null ? $"{CurrentProject.SampleRate / 1000.0:G4} kHz" : "";

    // ── 클립보드 ──

    private sealed record ClipboardEntry(
        string SourceId, long SourceInSamples, long SourceOutSamples,
        float GainDb, float Pan, bool Muted, FadeCurve FadeCurve,
        long FadeInSamples, long FadeOutSamples,
        long RelativeStart, string TrackId);

    private static List<ClipboardEntry>? _clipboard;

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetField(ref _isPlaying, value);
    }

    private bool _isMetronomeOn;
    public bool IsMetronomeOn
    {
        get => _isMetronomeOn;
        private set => SetField(ref _isMetronomeOn, value);
    }

    public string PlayheadTimeDisplay
    {
        get
        {
            if (CurrentProject == null) return "00:00.000";
            var ts = TimeSpan.FromSeconds(Timeline.PlayheadSeconds);
            return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }
    }

    // ── 커맨드 ──

    public ICommand NewProjectCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand SaveProjectCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand ImportAudioCommand { get; }
    public ICommand AddTrackCommand { get; }
    public ICommand DeleteSelectedTrackCommand { get; }
    public ICommand PlayCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ReturnToStartCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand SplitAtPlayheadCommand { get; }
    public ICommand ToggleMetronomeCommand { get; }
    public ICommand ToggleSnapCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand SeparateClipCommand        { get; }
    public ICommand ReduceNoiseCommand         { get; }
    public ICommand ImportSheetMusicCommand    { get; }
    public ICommand CopyCommand                { get; }
    public ICommand PasteCommand               { get; }
    public ICommand CutCommand                 { get; }
    public ICommand MergeCommand               { get; }
    public ICommand MoveAfterPreviousCommand   { get; }

    // ── 커맨드 핸들러 ──

    private async Task OnNewProject()
    {
        if (NewProjectRequested != null)
            await NewProjectRequested((string.Empty, 0));
    }

    public void CreateProject(string name, int sampleRate)
    {
        var project = ProjectService.NewProject(name, sampleRate);
        project.Tracks.Add(new Track { Name = "1", TrackIndex = 0 });
        var vm = new ProjectViewModel(project, ProjectService, FFmpegService);
        CurrentProject = vm;
        SelectedTrack = vm.Tracks.FirstOrDefault();
        AudioEngine.LoadProject(project);
        Timeline.PlayheadFrames = 0;
        Timeline.IsPlaying = false;
        StatusMessage = Loc.Format("Status_NewProject", name, sampleRate / 1000.0);
    }

    private async Task OnOpenProject()
    {
        if (OpenProjectFolderRequested == null) return;
        var folder = await OpenProjectFolderRequested();
        if (folder == null) return;
        await LoadProjectFromFolderAsync(folder);
    }

    public async Task LoadProjectFromFolderAsync(string folder)
    {
        IsBusy = true;
        StatusMessage = Loc.Get("Status_Loading");
        try
        {
            var project = await ProjectService.LoadAsync(folder);
            var vm = new ProjectViewModel(project, ProjectService, FFmpegService);
            CurrentProject = vm;
            SelectedTrack = vm.Tracks.FirstOrDefault();
            AudioEngine.LoadProject(project);
            Timeline.PlayheadFrames = 0;
            StatusMessage = Loc.Format("Status_LoadComplete", project.Name);
        }
        catch (Exception ex)
        {
            StatusMessage = Loc.Format("Status_OpenFailed", ex.Message);
            ErrorOccurred?.Invoke(ex.Message);
        }
        finally { IsBusy = false; }
    }

    private async Task OnSaveProject()
    {
        if (CurrentProject == null) return;
        if (CurrentProject.Model.FilePath == null) { await OnSaveAs(); return; }
        await SaveCurrentProjectAsync();
    }

    private async Task OnSaveAs()
    {
        if (CurrentProject == null) return;
        if (SaveProjectFolderRequested == null) return;
        var folder = await SaveProjectFolderRequested();
        if (folder == null) return;
        CurrentProject.Model.FilePath = folder;
        await SaveCurrentProjectAsync();
    }

    private async Task SaveCurrentProjectAsync()
    {
        if (CurrentProject == null) return;
        IsBusy = true;
        StatusMessage = Loc.Get("Status_Saving");
        try
        {
            await CurrentProject.SaveAsync();
            OnPropertyChanged(nameof(WindowTitle));
            StatusMessage = Loc.Get("Status_Saved");
        }
        catch (Exception ex)
        {
            StatusMessage = Loc.Format("Status_SaveFailed", ex.Message);
            ErrorOccurred?.Invoke(ex.Message);
        }
        finally { IsBusy = false; }
    }

    private async Task OnImportAudio()
    {
        if (CurrentProject == null || ImportAudioRequested == null) return;
        var files = await ImportAudioRequested();
        if (files == null || files.Length == 0) return;

        // 프로젝트 폴더가 없으면 임시 경로 사용
        if (CurrentProject.Model.FilePath == null)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "Selah", CurrentProject.Model.Id);
            CurrentProject.Model.FilePath = tempDir;
            Directory.CreateDirectory(tempDir);
        }

        BeginProgress(Loc.Get("Progress_Title_Import"));
        int imported = 0;

        // 삽입 대상 트랙 (단일 소스 임포트 시만 사용)
        var targetTrack = (SelectedTrack?.IsEnabled == true) ? SelectedTrack : CurrentProject.AddTrack();
        long insertPosition = ResolveInsertPosition(targetTrack, Timeline.PlayheadFrames);

        foreach (var file in files)
        {
            StatusMessage = Loc.Format("Status_Importing", Path.GetFileName(file));
            try
            {
                var progress = new Progress<double>(p =>
                {
                    StatusMessage   = Loc.Format("Status_Converting", p.ToString("P0"));
                    ProgressPercent = p * 100;
                });

                var sources = await CurrentProject.ImportAudioAutoAsync(file, progress);

                if (sources.Count == 1)
                {
                    // 단일 소스 — 기존 동작: 대상 트랙에 순차 삽입
                    var source = sources[0];
                    source.AbsolutePath ??= Path.Combine(CurrentProject.Model.FilePath!, source.RelPath);
                    targetTrack.AddClip(new ClipViewModel(new Clip
                    {
                        SourceId             = source.Id,
                        TimelineStartSamples = insertPosition,
                        SourceInSamples      = 0,
                        SourceOutSamples     = source.LengthSamples
                    }, CurrentProject.Model));
                    insertPosition += source.LengthSamples;
                }
                else
                {
                    // 멀티채널/멀티스트림 — 소스마다 새 트랙에 삽입
                    long startPos = Timeline.PlayheadFrames;
                    foreach (var source in sources)
                    {
                        source.AbsolutePath ??= Path.Combine(CurrentProject.Model.FilePath!, source.RelPath);
                        var track = CurrentProject.AddTrack(source.Name);
                        track.AddClip(new ClipViewModel(new Clip
                        {
                            SourceId             = source.Id,
                            TimelineStartSamples = startPos,
                            SourceInSamples      = 0,
                            SourceOutSamples     = source.LengthSamples
                        }, CurrentProject.Model));
                    }
                }
                imported++;
            }
            catch (Exception ex)
            {
                StatusMessage = Loc.Format("Status_Error", ex.Message);
                ErrorOccurred?.Invoke(ex.Message);
            }
        }
        AudioEngine.RebuildMixers();
        EndProgress();
        StatusMessage = Loc.Format("Status_ImportComplete", imported);
    }

    private void OnAddTrack()
    {
        var track = CurrentProject?.AddTrack();
        if (track != null) SelectedTrack = track;
    }

    private void OnDeleteSelectedTrack()
    {
        if (CurrentProject == null || SelectedTrack == null || !SelectedTrack.IsEnabled) return;
        CurrentProject.RemoveTrack(SelectedTrack);
        SelectedTrack = CurrentProject.Tracks.LastOrDefault();
        AudioEngine.RebuildMixers();
    }

    private void OnDelete()
    {
        if (CurrentProject == null || SelectedTrack?.IsEnabled != true) return;
        var selected = GetSelectedClips();
        if (selected.Count > 0)
        {
            foreach (var (track, clip) in selected)
                track.RemoveClip(clip);
            SelectedClip = null;
            AudioEngine.RebuildMixers();
        }
        else if (SelectedTrack != null)
        {
            OnDeleteSelectedTrack();
        }
    }

    private void OnTogglePlay()
    {
        if (AudioEngine.IsPlaying)
        {
            AudioEngine.Stop();
            IsPlaying = false;
            Timeline.IsPlaying = false;
            StatusMessage = Loc.Get("Status_Stopped");
        }
        else
        {
            AudioEngine.Seek(Timeline.PlayheadFrames);
            AudioEngine.Play();
            bool playing = AudioEngine.IsPlaying;
            IsPlaying = playing;
            Timeline.IsPlaying = playing;
            StatusMessage = playing ? Loc.Get("Status_Playing") : Loc.Get("Status_NoContent");
        }
    }

    private void OnStop()
    {
        AudioEngine.Stop();
        AudioEngine.Seek(0);
        Timeline.UpdatePlayhead(0, CurrentProject?.SampleRate ?? 48000);
        IsPlaying = false;
        Timeline.IsPlaying = false;
        StatusMessage = Loc.Get("Status_Stopped");
        OnPropertyChanged(nameof(PlayheadTimeDisplay));
    }

    private void OnReturnToStart()
    {
        bool wasPlaying = AudioEngine.IsPlaying;
        if (wasPlaying) AudioEngine.Stop();
        Timeline.UpdatePlayhead(0, CurrentProject?.SampleRate ?? 48000);
        AudioEngine.Seek(0);
        if (wasPlaying) { AudioEngine.Play(); IsPlaying = true; }
        OnPropertyChanged(nameof(PlayheadTimeDisplay));
    }

    private async Task OnExport()
    {
        if (CurrentProject == null || ExportPathRequested == null) return;
        var path = await ExportPathRequested();
        if (path == null) return;

        BeginProgress(Loc.Get("Progress_Title_Export"));
        StatusMessage = Loc.Get("Status_Rendering");
        try
        {
            await AudioRenderer.RenderToWavAsync(
                CurrentProject.Model,
                path,
                bitDepth: 24,
                includeMetronome: false,
                progress: new Progress<double>(p =>
                {
                    StatusMessage   = Loc.Format("Status_RenderProgress", p.ToString("P0"));
                    ProgressPercent = p * 100;
                }));
            StatusMessage = Loc.Format("Status_ExportComplete", Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            StatusMessage = Loc.Format("Status_ExportFailed", ex.Message);
            ErrorOccurred?.Invoke(ex.Message);
        }
        finally { EndProgress(); }
    }

    private void OnSplitAtPlayhead()
    {
        if (CurrentProject == null) return;
        long frame = Timeline.PlayheadFrames;
        var selected = GetSelectedClips();
        bool didSplit = false;

        if (selected.Count > 0)
        {
            // 선택된 클립 중 플레이헤드가 통과하는 모든 클립 분할
            foreach (var (track, clip) in selected)
                if (track.IsEnabled && frame > clip.TimelineStartSamples && frame < clip.TimelineEndProjectFrame)
                    didSplit |= track.SplitSpecificClip(clip, frame);
        }

        if (!didSplit && SelectedTrack?.IsEnabled == true)
            didSplit = SelectedTrack.SplitClipAt(frame);

        if (didSplit) AudioEngine.RebuildMixers();
    }

    // ── 클립보드 커맨드 ────────────────────────────────────────────────────

    /// <summary>선택된 모든 클립을 (트랙, 클립) 쌍 목록으로 반환합니다.</summary>
    private IReadOnlyList<(TrackViewModel Track, ClipViewModel Clip)> GetSelectedClips()
    {
        if (CurrentProject == null) return [];
        return CurrentProject.Tracks
            .SelectMany(t => t.Clips.Where(c => c.IsSelected).Select(c => (t, c)))
            .OrderBy(x => x.c.TimelineStartSamples)
            .ToList();
    }

    private void OnCopy()
    {
        var selected = GetSelectedClips();
        if (selected.Count == 0 && SelectedClip != null && SelectedTrack != null)
            selected = new List<(TrackViewModel Track, ClipViewModel Clip)> { (SelectedTrack, SelectedClip) };
        if (selected.Count == 0) return;

        long minStart = selected.Min(x => x.Clip.TimelineStartSamples);
        _clipboard = selected.Select(x => new ClipboardEntry(
            x.Clip.Model.SourceId,
            x.Clip.Model.SourceInSamples,
            x.Clip.Model.SourceOutSamples,
            x.Clip.Model.GainDb,
            x.Clip.Model.Pan,
            x.Clip.Model.Muted,
            x.Clip.Model.FadeCurve,
            x.Clip.Model.FadeInSamples,
            x.Clip.Model.FadeOutSamples,
            x.Clip.TimelineStartSamples - minStart,
            x.Track.Id
        )).ToList();
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        StatusMessage = Loc.Format("Status_Copied", selected.Count);
    }

    private void OnPaste()
    {
        if (CurrentProject == null || _clipboard == null || _clipboard.Count == 0) return;
        long pasteStart = Timeline.PlayheadFrames;
        ClipViewModel? lastVm = null;
        TrackViewModel? lastTrack = null;

        foreach (var entry in _clipboard)
        {
            var targetTrack = CurrentProject.Tracks.FirstOrDefault(t => t.Id == entry.TrackId)
                           ?? SelectedTrack
                           ?? CurrentProject.Tracks.FirstOrDefault();
            if (targetTrack == null) continue;

            var newClip = new Clip
            {
                SourceId             = entry.SourceId,
                TimelineStartSamples = pasteStart + entry.RelativeStart,
                SourceInSamples      = entry.SourceInSamples,
                SourceOutSamples     = entry.SourceOutSamples,
                GainDb               = entry.GainDb,
                Pan                  = entry.Pan,
                Muted                = entry.Muted,
                FadeCurve            = entry.FadeCurve,
                FadeInSamples        = entry.FadeInSamples,
                FadeOutSamples       = entry.FadeOutSamples
            };
            var vm = new ClipViewModel(newClip, CurrentProject.Model);
            targetTrack.AddClip(vm);
            lastVm = vm;
            lastTrack = targetTrack;
        }

        if (lastVm != null) SelectedClip = lastVm;
        if (lastTrack != null) SelectedTrack = lastTrack;
        AudioEngine.RebuildMixers();
        CurrentProject.Model.IsDirty = true;
        StatusMessage = Loc.Format("Status_Pasted", _clipboard.Count);
    }

    private void OnCut()
    {
        OnCopy();
        var selected = GetSelectedClips();
        if (selected.Count == 0 && SelectedClip != null && SelectedTrack != null)
            selected = new List<(TrackViewModel Track, ClipViewModel Clip)> { (SelectedTrack, SelectedClip) };
        foreach (var (track, clip) in selected)
            track.RemoveClip(clip);
        SelectedClip = null;
        AudioEngine.RebuildMixers();
    }

    private void OnMerge()
    {
        var selected = GetSelectedClips();
        if (selected.Count < 2) return;

        var trackGroups = selected.GroupBy(x => x.Track.Id).ToList();
        if (trackGroups.Count > 1)
        {
            StatusMessage = Loc.Get("Status_MergeMultiTrackError");
            return;
        }

        var track = selected[0].Track;
        track.MergeClips(selected.Select(x => x.Clip).ToList());
        SelectedClip = track.Clips.FirstOrDefault(c => c.IsSelected);
        AudioEngine.RebuildMixers();
        StatusMessage = Loc.Get("Status_Merged");
    }

    private void OnMoveAfterPrevious()
    {
        if (CurrentProject == null || SelectedClip == null || SelectedTrack == null) return;
        ClipViewModel primaryClip = SelectedClip;
        TrackViewModel primaryTrack = SelectedTrack;

        var selected = GetSelectedClips()
            .Where(x => x.Track.Id == primaryTrack.Id)
            .Select(x => x.Clip)
            .OrderBy(c => c.TimelineStartSamples)
            .ToList();
        if (selected.Count == 0)
            selected = new List<ClipViewModel> { primaryClip };

        long groupStart = selected[0].TimelineStartSamples;
        var ordered = primaryTrack.Clips.OrderBy(c => c.TimelineStartSamples).ToList();
        var prevClip = ordered.LastOrDefault(c => c.TimelineEndProjectFrame <= groupStart && !selected.Contains(c));
        if (prevClip == null) return;

        long delta = prevClip.TimelineEndProjectFrame - groupStart;
        foreach (var clip in selected)
            clip.TimelineStartSamples = Math.Max(0, clip.TimelineStartSamples + delta);

        AudioEngine.RebuildMixers();
        StatusMessage = Loc.Get("Status_MovedAfterPrevious");
    }

    /// <summary>
    /// 플레이헤드 위치가 기존 클립 내부에 있으면 해당 클립의 끝 위치를 반환하고,
    /// 그렇지 않으면 playheadFrame을 그대로 반환합니다.
    /// </summary>
    private static long ResolveInsertPosition(TrackViewModel track, long playheadFrame)
    {
        var overlapping = track.Clips.FirstOrDefault(c =>
            playheadFrame >= c.TimelineStartSamples && playheadFrame < c.TimelineEndProjectFrame);
        return overlapping?.TimelineEndProjectFrame ?? playheadFrame;
    }

    private void OnToggleMetronome()
    {
        IsMetronomeOn = !IsMetronomeOn;
        AudioEngine.MetronomeEnabled = IsMetronomeOn;
        StatusMessage = IsMetronomeOn ? Loc.Get("Status_MetronomeOn") : Loc.Get("Status_MetronomeOff");
    }

    private void OnPlayheadAdvanced(object? s, long frames)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            int sr = CurrentProject?.SampleRate ?? 48000;
            Timeline.UpdatePlayhead(frames, sr);
            OnPropertyChanged(nameof(PlayheadTimeDisplay));
        });
    }

    private void OnPlaybackStopped(object? s, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            IsPlaying = false;
            Timeline.IsPlaying = false;
        });
    }

    private void OnContentEnded(object? s, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (!AudioEngine.IsPlaying) return;
            AudioEngine.Stop();
            IsPlaying = false;
            Timeline.IsPlaying = false;
            StatusMessage = Loc.Get("Status_PlaybackEnded");
            OnPropertyChanged(nameof(PlayheadTimeDisplay));
        });
    }

    // ── 진행 팝업 헬퍼 ─────────────────────────────────────────────────

    private void BeginProgress(string title)
    {
        ProgressTitle   = title;
        ProgressPercent = -1;
        IsBusy          = true;
        ProgressStarted?.Invoke(title);
    }

    private void EndProgress()
    {
        IsBusy = false;
        ProgressFinished?.Invoke();
    }

    // ── 스템 분리 ─────────────────────────────────────────────────────

    private async Task SeparateClipAsync()
    {
        if (CurrentProject == null || SelectedClip == null) return;

        var clip = SelectedClip;
        var proj = CurrentProject;

        // 소스 파일 확인
        var src = proj.Model.AudioSources.FirstOrDefault(s => s.Id == clip.Model.SourceId);
        if (src?.AbsolutePath == null || !File.Exists(src.AbsolutePath))
        {
            ErrorOccurred?.Invoke(Loc.Get("Status_Separate_NoSource"));
            return;
        }

        // Python 가용성 확인
        if (!StemSeparator.IsPythonAvailable)
        {
            StatusMessage = Loc.Get("Status_Separate_NoPython");
            SetupGuideRequested?.Invoke();
            return;
        }

        // 설치된 모델 탐색 — 우선순위:
        //   1. AudioSeparator (MDX-Net): 모든 음역대 보컬 인식
        //   2. OnnxRuntime 파인튜닝 (htdemucs_ft)
        //   3. 설치된 모델 중 첫 번째
        var installed = ModelManagerService.GetCatalog().Where(m => m.IsInstalled).ToList();
        var model = installed.FirstOrDefault(m => m.Engine == ModelEngine.AudioSeparator)
                 ?? installed.FirstOrDefault(m => m.Id == "htdemucs_ft")
                 ?? installed.FirstOrDefault();
        if (model == null)
        {
            StatusMessage = Loc.Get("Status_Separate_NoModel");
            SetupGuideRequested?.Invoke();
            return;
        }

        // 프로젝트 저장 경로 확보
        if (proj.Model.FilePath == null)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "Selah", proj.Model.Id);
            proj.Model.FilePath = tempDir;
            Directory.CreateDirectory(tempDir);
        }

        var outputDir = Path.Combine(proj.Model.FilePath, "audio", "stems",
            Path.GetFileNameWithoutExtension(src.Name) + "_" +
            DateTime.Now.ToString("yyyyMMddHHmmss"));

        // Pre-create stem tracks immediately so the user sees them before demucs finishes
        var stemKeys = StemSeparatorService.StemKeys(model.StemType);
        var stemTracks = new Dictionary<string, TrackViewModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var stemKey in stemKeys)
        {
            string trackName = GetStemTrackName(stemKey);
            var tv = proj.Tracks.FirstOrDefault(t => t.Name == trackName)
                     ?? proj.AddTrack(trackName);
            stemTracks[stemKey] = tv;
        }

        BeginProgress(Loc.Get("Status_Separating"));
        StatusMessage = Loc.Get("Status_Separating");

        string origName = src.Name;
        int stemIndex = 1;
        var addedStems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var progress = new Progress<SeparationProgress>(p =>
            {
                StatusMessage   = $"{p.Phase} {p.Percent:P0}";
                ProgressPercent = p.Percent * 100;

                // Add clip to its track the moment each stem WAV is ready
                if (p.StemKey != null && p.StemPath != null
                    && !addedStems.Contains(p.StemKey)
                    && stemTracks.TryGetValue(p.StemKey, out var trackVm))
                {
                    addedStems.Add(p.StemKey);
                    try
                    {
                        string stemName = $"{origName}-{stemIndex++}";
                        long stemLengthSamples;
                        int stemSr, stemCh;
                        using (var wr = new WaveFileReader(p.StemPath))
                        {
                            stemLengthSamples = wr.SampleCount;
                            stemSr = wr.WaveFormat.SampleRate;
                            stemCh = wr.WaveFormat.Channels;
                        }
                        var stemSource = new AudioSource
                        {
                            Name = stemName,
                            RelPath = Path.GetRelativePath(proj.Model.FilePath!, p.StemPath),
                            AbsolutePath = p.StemPath,
                            SampleRate = stemSr,
                            Channels = stemCh,
                            LengthSamples = stemLengthSamples,
                            SourceType = SourceType.Separated
                        };
                        proj.Model.AudioSources.Add(stemSource);
                        var stemClip = new Clip
                        {
                            SourceId = stemSource.Id,
                            TimelineStartSamples = clip.TimelineStartSamples,
                            SourceInSamples = clip.SourceInSamples,
                            SourceOutSamples = Math.Min(clip.SourceOutSamples, stemLengthSamples)
                        };
                        trackVm.AddClip(new ClipViewModel(stemClip, proj.Model));
                        AudioEngine.RebuildMixers();
                    }
                    catch { /* ignore per-stem errors */ }
                }
            });

            var result = await StemSeparator.SeparateAsync(
                src.AbsolutePath, outputDir, model, model.StemType, progress);

            if (!result.Success && addedStems.Count == 0)
            {
                if (result.IsOnnxRuntimeMissing || result.IsOnnxModelMissing ||
                    result.IsTorchCodecMissing  || result.IsTorchCodecBroken ||
                    result.IsAudioSeparatorMissing)
                {
                    // 도구/패키지 미설치 → 설치 안내 페이지 열기
                    if (result.IsAudioSeparatorMissing)
                        StatusMessage = Loc.Get("Status_Separate_AudioSeparatorMissing");
                    else
                        StatusMessage = Loc.Get("Status_SetupRequired");
                    SetupGuideRequested?.Invoke();
                }
                else
                {
                    ErrorOccurred?.Invoke(Loc.Format("Status_SeparateFailed", result.Error ?? ""));
                }
                return;
            }

            // Fallback: add any stems not already added via STEM: progress
            foreach (var (stemKey, stemPath) in result.OutputFiles)
            {
                if (addedStems.Contains(stemKey)) continue;
                if (!stemTracks.TryGetValue(stemKey, out var trackVm)) continue;
                try
                {
                    string stemName = $"{origName}-{stemIndex++}";
                    long stemLengthSamples;
                    int stemSr, stemCh;
                    using (var wr = new WaveFileReader(stemPath))
                    {
                        stemLengthSamples = wr.SampleCount;
                        stemSr = wr.WaveFormat.SampleRate;
                        stemCh = wr.WaveFormat.Channels;
                    }
                    var stemSource = new AudioSource
                    {
                        Name = stemName,
                        RelPath = Path.GetRelativePath(proj.Model.FilePath!, stemPath),
                        AbsolutePath = stemPath,
                        SampleRate = stemSr,
                        Channels = stemCh,
                        LengthSamples = stemLengthSamples,
                        SourceType = SourceType.Separated
                    };
                    proj.Model.AudioSources.Add(stemSource);
                    var stemClip = new Clip
                    {
                        SourceId = stemSource.Id,
                        TimelineStartSamples = clip.TimelineStartSamples,
                        SourceInSamples = clip.SourceInSamples,
                        SourceOutSamples = Math.Min(clip.SourceOutSamples, stemLengthSamples)
                    };
                    trackVm.AddClip(new ClipViewModel(stemClip, proj.Model));
                }
                catch { }
            }

            proj.Model.IsDirty = true;
            AudioEngine.RebuildMixers();
            int total = addedStems.Count + result.OutputFiles.Keys.Count(k => !addedStems.Contains(k));
            StatusMessage = Loc.Format("Status_SeparateComplete", origName, total);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
            StatusMessage = Loc.Format("Status_Error", ex.Message);
        }
        finally
        {
            EndProgress();
        }
    }

    private async Task ReduceNoiseClipAsync()
    {
        if (CurrentProject == null || SelectedClip == null) return;

        var clip = SelectedClip;
        var proj = CurrentProject;

        // 소스 파일 확인
        var src = proj.Model.AudioSources.FirstOrDefault(s => s.Id == clip.Model.SourceId);
        if (src?.AbsolutePath == null || !File.Exists(src.AbsolutePath))
        {
            ErrorOccurred?.Invoke(Loc.Get("Status_Separate_NoSource"));
            return;
        }

        // Python 가용성 확인
        if (!NoiseReducer.IsPythonAvailable)
        {
            StatusMessage = Loc.Get("Status_Separate_NoPython");
            SetupGuideRequested?.Invoke();
            return;
        }

        // 프로젝트 저장 경로 확보
        if (proj.Model.FilePath == null)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "Selah", proj.Model.Id);
            proj.Model.FilePath = tempDir;
            Directory.CreateDirectory(tempDir);
        }

        var outputDir  = Path.Combine(proj.Model.FilePath, "audio", "processed");
        var outputName = Path.GetFileNameWithoutExtension(src.Name) + "_nr_" +
                         DateTime.Now.ToString("yyyyMMddHHmmss") + ".wav";
        var outputPath = Path.Combine(outputDir, outputName);

        BeginProgress(Loc.Get("Status_ReducingNoise"));
        StatusMessage = Loc.Get("Status_ReducingNoise");
        string origName = src.Name;

        try
        {
            var progress = new Progress<NoiseReductionProgress>(p =>
            {
                StatusMessage   = $"{Loc.Get("Status_ReducingNoise")} {p.Percent:P0}";
                ProgressPercent = p.Percent * 100;
            });

            var result = await NoiseReducer.ReduceNoiseAsync(
                src.AbsolutePath, outputPath,
                strength: 0.75,
                progress: progress);

            if (!result.Success)
            {
                if (result.IsNoiseReduceMissing)
                {
                    StatusMessage = Loc.Get("Status_NoiseReduce_Missing_Short");
                    SetupGuideRequested?.Invoke();
                }
                else
                {
                    ErrorOccurred?.Invoke(Loc.Format("Status_NoiseReduceFailed", result.Error ?? ""));
                }
                return;
            }

            // WAV 메타데이터 읽기
            long lengthSamples;
            int outSr, outCh;
            using (var wr = new WaveFileReader(result.OutputPath!))
            {
                lengthSamples = wr.SampleCount;
                outSr = wr.WaveFormat.SampleRate;
                outCh = wr.WaveFormat.Channels;
            }

            var nrSource = new AudioSource
            {
                Name          = Path.GetFileNameWithoutExtension(outputName),
                RelPath       = Path.GetRelativePath(proj.Model.FilePath!, result.OutputPath!),
                AbsolutePath  = result.OutputPath,
                SampleRate    = outSr,
                Channels      = outCh,
                LengthSamples = lengthSamples,
                SourceType    = SourceType.Processed
            };
            proj.Model.AudioSources.Add(nrSource);

            string trackName = $"NR: {origName}";
            var trackVm = proj.Tracks.FirstOrDefault(t => t.Name == trackName)
                          ?? proj.AddTrack(trackName);

            var nrClip = new Clip
            {
                SourceId             = nrSource.Id,
                TimelineStartSamples = clip.TimelineStartSamples,
                SourceInSamples      = clip.SourceInSamples,
                SourceOutSamples     = Math.Min(clip.SourceOutSamples, lengthSamples)
            };
            trackVm.AddClip(new ClipViewModel(nrClip, proj.Model));

            proj.Model.IsDirty = true;
            AudioEngine.RebuildMixers();
            StatusMessage = Loc.Format("Status_NoiseReduceComplete", origName);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
            StatusMessage = Loc.Format("Status_Error", ex.Message);
        }
        finally
        {
            EndProgress();
        }
    }

    // ── 악보 인식 ─────────────────────────────────────────────────────────────

    private async Task ImportSheetMusicAsync()
    {
        if (CurrentProject == null) return;

        // Python 가용성 확인
        if (!SheetMusicService.IsPythonAvailable)
        {
            StatusMessage = Loc.Get("Status_SheetMusic_NoPython");
            SetupGuideRequested?.Invoke();
            return;
        }

        // FluidSynth + SoundFont 확인
        if (!FluidSynthService.IsAvailable)
        {
            var msg = !FluidSynthService.IsFluidSynthFound
                ? Loc.Get("Status_SheetMusic_NoFluidSynth")
                : Loc.Get("Status_SheetMusic_NoSoundFont");
            ErrorOccurred?.Invoke(msg);
            return;
        }

        // 악보 인식 다이얼로그 열기 (코드 비하인드에서 처리)
        if (ImportSheetMusicRequested == null) return;
        var dlgResult = await ImportSheetMusicRequested();
        if (dlgResult == null) return;   // 취소

        if (dlgResult.SelectedInstruments.Length == 0) return;

        // 프로젝트 저장 경로 확보
        if (CurrentProject.Model.FilePath == null)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "Selah", CurrentProject.Model.Id);
            CurrentProject.Model.FilePath = tempDir;
            Directory.CreateDirectory(tempDir);
        }

        BeginProgress(Loc.Get("Progress_Title_SheetMusic"));
        int inserted = 0;

        try
        {
            long insertPosition = Timeline.PlayheadFrames;

            foreach (var instrument in dlgResult.SelectedInstruments)
            {
                var wavDir  = Path.Combine(CurrentProject.Model.FilePath!, "audio", "sheetmusic");
                var wavName = $"{instrument}_{DateTime.Now:yyyyMMddHHmmss}.wav";
                var wavPath = Path.Combine(wavDir, wavName);

                StatusMessage = Loc.Format("Status_SheetMusic_Synthesizing", instrument);

                var progress = new Progress<SheetMusicProgress>(p =>
                {
                    StatusMessage   = p.Phase;
                    ProgressPercent = p.Percent * 100;
                });

                await SheetMusicService.SynthesizeAsync(
                    dlgResult.MidiPath, instrument, wavPath,
                    CurrentProject.SampleRate, progress);

                // WAV 메타데이터 읽기
                long lengthSamples;
                int sr, ch;
                using (var wr = new WaveFileReader(wavPath))
                {
                    lengthSamples = wr.SampleCount;
                    sr = wr.WaveFormat.SampleRate;
                    ch = wr.WaveFormat.Channels;
                }

                var source = new AudioSource
                {
                    Name          = Path.GetFileNameWithoutExtension(wavName),
                    RelPath       = Path.GetRelativePath(CurrentProject.Model.FilePath!, wavPath),
                    AbsolutePath  = wavPath,
                    SampleRate    = sr,
                    Channels      = ch,
                    LengthSamples = lengthSamples,
                    SourceType    = SourceType.SheetMusic
                };
                CurrentProject.Model.AudioSources.Add(source);

                string trackName = GetInstrumentTrackName(instrument);
                var trackVm = CurrentProject.AddTrack(trackName);
                trackVm.AddClip(new ClipViewModel(new Clip
                {
                    SourceId             = source.Id,
                    TimelineStartSamples = insertPosition,
                    SourceInSamples      = 0,
                    SourceOutSamples     = lengthSamples
                }, CurrentProject.Model));

                inserted++;
            }

            // 루프 완료 후 한 번만 믹서를 재구성합니다.
            // (악기별로 호출하면 N개 악기 × 전체 트랙 수만큼 불필요한 WaveFileReader 재생성이 발생합니다)
            AudioEngine.RebuildMixers();
            CurrentProject.Model.IsDirty = true;
            StatusMessage = Loc.Format("Status_SheetMusicComplete", inserted);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
            StatusMessage = Loc.Format("Status_SheetMusicFailed", ex.Message);
        }
        finally
        {
            EndProgress();
        }
    }

    private static string GetInstrumentTrackName(string key) => key switch
    {
        "Piano"          => Loc.Get("Instrument_Piano"),
        "AcousticGuitar" => Loc.Get("Instrument_AcousticGuitar"),
        "ElectricGuitar" => Loc.Get("Instrument_ElectricGuitar"),
        "BassGuitar"     => Loc.Get("Instrument_BassGuitar"),
        "Drums"          => Loc.Get("Instrument_Drums"),
        "Synthesizer"    => Loc.Get("Instrument_Synthesizer"),
        "Saxophone"      => Loc.Get("Instrument_Saxophone"),
        "Flute"          => Loc.Get("Instrument_Flute"),
        _                => key
    };

    private static string GetStemTrackName(string stemKey) => stemKey switch
    {
        "vocals"    => Loc.Get("Stem_Vocals"),
        "no_vocals" => Loc.Get("Stem_NoVocals"),
        "drums"     => Loc.Get("Stem_Drums"),
        "bass"      => Loc.Get("Stem_Bass"),
        "other"     => Loc.Get("Stem_Other"),
        _           => stemKey
    };

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(HardwareStatusText));
        StatusMessage = Loc.Get("Status_Ready");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Loc.LanguageChanged -= OnLanguageChanged;
        AudioEngine.Dispose();
    }
}

/// <summary>SheetMusicDialog의 OK 결과. MainViewModel.ImportSheetMusicAsync()가 소비합니다.</summary>
public class SheetMusicDialogResult
{
    /// <summary>OMR이 생성한 MIDI 파일 절대 경로.</summary>
    public string MidiPath { get; init; } = string.Empty;

    /// <summary>사용자가 선택한 악기 키 배열 (예: ["Piano", "Flute"]).</summary>
    public string[] SelectedInstruments { get; init; } = [];
}
