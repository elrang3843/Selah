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

    private ProjectViewModel? _currentProject;
    private string _statusMessage;
    private bool _isBusy;
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

    public MainViewModel()
    {
        _statusMessage = Loc.Get("Status_Ready");

        FFmpegService.Detect();
        _hardwareInfo = HardwareDetectionService.Detect();
        StemSeparator = new StemSeparatorService(ModelManagerService);
        NoiseReducer  = new NoiseReductionService();

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
        set => SetField(ref _isBusy, value);
    }

    public bool HasProject => CurrentProject != null;
    public bool IsFFmpegAvailable => FFmpegService.IsAvailable;

    public string HardwareStatusText =>
        _hardwareInfo?.BackendDisplayName ?? Loc.Get("Status_Detecting");

    public string SampleRateText =>
        CurrentProject != null ? $"{CurrentProject.SampleRate / 1000.0:G4} kHz" : "";

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
    public ICommand SeparateClipCommand { get; }
    public ICommand ReduceNoiseCommand  { get; }

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

        IsBusy = true;
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
                    StatusMessage = Loc.Format("Status_Converting", p.ToString("P0")));

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
        IsBusy = false;
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
        if (SelectedClip != null && SelectedTrack != null)
        {
            SelectedTrack.RemoveClip(SelectedClip);
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

        IsBusy = true;
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
                    StatusMessage = Loc.Format("Status_RenderProgress", p.ToString("P0"));
                    OnPropertyChanged(nameof(StatusMessage));
                }));
            StatusMessage = Loc.Format("Status_ExportComplete", Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            StatusMessage = Loc.Format("Status_ExportFailed", ex.Message);
            ErrorOccurred?.Invoke(ex.Message);
        }
        finally { IsBusy = false; }
    }

    private void OnSplitAtPlayhead()
    {
        if (CurrentProject == null || SelectedTrack == null || !SelectedTrack.IsEnabled) return;
        SelectedTrack.SplitClipAt(Timeline.PlayheadFrames);
        AudioEngine.RebuildMixers();
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

        // 설치된 모델 탐색 — htdemucs_ft(파인튜닝 버전) 우선, 없으면 htdemucs
        var installed = ModelManagerService.GetCatalog().Where(m => m.IsInstalled).ToList();
        var model = installed.FirstOrDefault(m => m.Id == "htdemucs_ft")
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

        IsBusy = true;
        StatusMessage = Loc.Get("Status_Separating");

        string origName = src.Name;
        int stemIndex = 1;
        var addedStems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var progress = new Progress<SeparationProgress>(p =>
            {
                StatusMessage = $"{p.Phase} {p.Percent:P0}";

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
                    result.IsTorchCodecMissing  || result.IsTorchCodecBroken)
                {
                    // 도구/패키지 미설치 → 설치 안내 페이지 열기
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
            IsBusy = false;
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

        IsBusy = true;
        StatusMessage = Loc.Get("Status_ReducingNoise");
        string origName = src.Name;

        try
        {
            var progress = new Progress<NoiseReductionProgress>(p =>
                StatusMessage = $"{Loc.Get("Status_ReducingNoise")} {p.Percent:P0}");

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
            IsBusy = false;
        }
    }

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
