# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

# Selah — 프로젝트 코딩 규칙

## Build & Run

```bash
dotnet build Selah.sln              # Debug build
dotnet build -c Release Selah.sln   # Release build
dotnet run --project src/Selah.App  # Run the app
```

No test projects exist.

## Architecture

**Selah** is a Windows desktop audio workstation (WPF, .NET 8) for worship music preparation. It follows strict MVVM with two projects:

- **`Selah.Core`** — platform-agnostic library: audio engine, data models, services
- **`Selah.App`** — WPF frontend: views, viewmodels, converters, localization resources

### Core Layer (`src/Selah.Core/`)

**Audio engine** (`Audio/`):
- `AudioEngine` — top-level playback manager; tries WaveOut, falls back to WASAPI
- `MasterMixerProvider` — mixes all tracks + metronome; feeds through `SoftLimiter`. Call `RebuildMixers()` whenever the track list or clip list changes. `Seek()` must reset `_lastPlayheadFrames = long.MinValue / 2` (already done) — if you add a new reset path, do not skip this or `PlayheadAdvanced` will be suppressed until playback reaches the previous position
- `TrackMixerProvider` — per-track gain/pan/solo/mute
- `ClipSampleProvider` — reads audio from a `Clip` region with resampling to project SR; outputs silence outside clip bounds
- `MetronomeProvider` — synthesizes click track from `TempoMap` (downbeat 1400 Hz / beat 880 Hz, ~20 ms sine); no external files required
- `WaveformCache` — async peak data per `AudioSource`; `GetOrRequest()` launches a background task, `FramesPerPeak = 256`
- `SoftLimiter` — master output soft clip (threshold 0.88 f, hard clip 0.999 f)
- `AudioRenderer` — exports the timeline to a WAV file

**Models** (`Models/`):
- `Project` — root data model; saved as gzip-compressed JSON (`.slh`)
- `Track` → `Clip` → `AudioSource` — core timeline hierarchy
- `Clip` fields: `TimelineStartSamples`, `SourceInSamples`, `SourceOutSamples` (exclusive); `LengthSamples` and `TimelineEndSamples` are computed
- `AudioSource`: `RelPath` is serialized (relative to project folder); `AbsolutePath` is `[JsonIgnore]`, resolved at load time
- `TempoMap` / `TempoEvent` — tempo/beat information
- `ScoreProfile` — result of sheet music OMR: staff count, clef types, chord density, pitch range, MIDI path, suggested instruments

**Services** (`Services/`):
- `ProjectService` — load/save `.slh` (JSON.gz). Project folder layout: `audio/`, `peaks/`, `recordings/`
- `FFmpegService` — shells out to `ffmpeg`/`ffprobe` for format conversion
- `FluidSynthService` — detects FluidSynth exe and SoundFont (`.sf2`/`.sf3`) files. SoundFont search order: `%AppData%\Selah\soundfonts\` → app bundle → `C:\Program Files\FluidSynth\` → `C:\soundfonts\`
- `SheetMusicService` — OMR + MIDI synthesis pipeline: `RecognizeAsync()` → `sheet_music_runner.py` → `SynthesizeAsync()` → `midi_synthesizer.py`
- `StemSeparatorService` — dispatches stem separation to one of three Python backends
- `NoiseReductionService` — shells out to `noise_reducer.py`
- `HardwareDetectionService` — detects CPU/GPU capabilities for ML backend recommendation
- `ModelManagerService` — downloads ML models from HuggingFace

### App Layer (`src/Selah.App/`)

- `MainViewModel` — top-level orchestrator; owns all major commands. `_playbackGeneration` (volatile int) is the key guard against stale `PlayheadAdvanced` events — increment it before every `AudioEngine.Stop()` call so old `Dispatcher.BeginInvoke` lambdas are discarded
- `TimelineViewModel` — playhead position, zoom, snap. `UpdatePlayhead()` bypasses the `PlayheadFrames` setter to fire both `PlayheadFrames` and `PlayheadSeconds` notifications together. `PlayheadTimeDisplay` in `MainViewModel` auto-syncs via a `Timeline.PropertyChanged` subscription (no manual `OnPropertyChanged` needed for seek)
- `ProjectViewModel` / `TrackViewModel` / `ClipViewModel` — UI wrappers around core models
- `ClipViewModel.IsSelected` — drives multi-selection. Use `MainViewModel.GetSelectedClips()` (returns `IReadOnlyList<(TrackViewModel Track, ClipViewModel Clip)>` sorted by start position) when implementing any operation that should apply to all selected clips. Single-clip operations (`SplitClipAt`) still fall back to the SelectedClip if nothing is selected
- `TrackViewModel.SplitSpecificClip(clip, frame)` — splits by reference rather than by position search; use this when iterating `GetSelectedClips()` to split each independently
- `SheetMusicViewModel` — sheet music recognition dialog
- `ModelManagerViewModel` — model download/management dialog
- **Command pattern**: `RelayCommand` (sync) and `AsyncRelayCommand` (async, sets `_isExecuting` flag). Both raise `CanExecuteChanged` via `CommandManager.InvalidateRequerySuggested()`
- Localization via XAML resource dictionaries: `Resources/Ko.xaml`, `Resources/En.xaml`, `Resources/Zh.xaml`
- Themes: `Resources/Light.xaml`, `Resources/Dark.xaml`

### Canvas Subscriptions (`Controls/TimelineCanvas.cs`)

The canvas subscribes to:
- `TimelineViewModel.PropertyChanged` → redraws on `PlayheadFrames`, `PixelsPerSecond`, `ScrollOffsetX`
- `ClipViewModel.PropertyChanged` → redraws on `TimelineStartSamples`, `SourceOutSamples`, `FadeInSamples`, `FadeOutSamples`, `IsSelected`, `Muted`
- Track and clip `CollectionChanged` — maintains subscription lists

If a new property change should trigger a canvas repaint, add it to `OnClipPropertyChanged` or `OnTimelinePropertyChanged`.

### Python Scripts (`scripts/`)

All scripts communicate with C# over stdout. C# sets `PYTHONIOENCODING=utf-8:replace`.

**stdout protocol:**

| Line prefix | Meaning |
|---|---|
| `PROGRESS:<0-100>` | Progress percentage |
| `LOG:<message>` | Status or error message |
| `PROFILE:<json>` | `ScoreProfile` JSON (sheet_music_runner only) |
| `STEM:<key>=<path>` | Stem file ready at path (audio_separator_runner only) |

**Error sentinels** (appear as `LOG:` lines): `OEMER_MISSING`, `OMR_FAILED`, `FLUIDSYNTH_MISSING`, `FLUIDSYNTH_DLL_MISSING`, `AUDIO_SEPARATOR_MISSING`, `NOISEREDUCE_MISSING`

| Script | Role |
|---|---|
| `sheet_music_runner.py` | Image preprocessing (auto-downscale >3500px, ~300 DPI target, dark-bg correction, RGBA→RGB) → oemer OMR → MusicXML → MIDI + ScoreProfile |
| `midi_synthesizer.py` | MIDI patch replacement → FluidSynth synthesis (Python API first, exe fallback) → WAV |
| `demucs_runner.py` | Meta Demucs CLI stem separation |
| `onnx_runner.py` | ONNX Runtime htdemucs inference + FFmpeg audio I/O |
| `audio_separator_runner.py` | UVR MDX-Net vocal/instrumental separation via `audio-separator` |
| `noise_reducer.py` | Spectral-gate noise reduction via `noisereduce` |

## Key Conventions

### 시간 표시 형식

프로젝트 내 모든 시간 표시는 **`mm:ss.fff`** 형식을 사용합니다.

```csharp
var ts = TimeSpan.FromSeconds(seconds);
return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
```

> **주의:** `{seconds:000f}` 같은 표현식은 `051f`처럼 잘못된 출력을 생성합니다. `f`는 C# 숫자 포맷에서 리터럴 문자입니다.

### Playback race condition pattern

`PlayheadAdvanced` fires on the audio thread; its `Dispatcher.BeginInvoke` lambda can run after the user has already stopped playback. The guard is:

```csharp
// in OnPlayheadAdvanced:
int generation = _playbackGeneration;
Dispatcher.BeginInvoke(() => {
    if (_playbackGeneration != generation) return;
    // safe to update UI
});

// before every AudioEngine.Stop():
_playbackGeneration++;
AudioEngine.Stop();
```

Never check only `AudioEngine.IsPlaying` inside the lambda — it is `true` again if `OnReturnToStart` immediately called `Play()`.

### Other conventions

- Editing is **non-destructive**: `Clip` objects reference regions of `AudioSource` files; source files are never modified.
- All audio is resampled to the project's fixed sample rate at playback time.
- Nullable reference types are enabled (`<Nullable>enable</Nullable>`); keep code null-safe.
- The app is Windows-only (`net8.0-windows`); do not introduce cross-platform assumptions.
- After any structural change to tracks or clips, call `AudioEngine.RebuildMixers()` once (not per-track).
- When adding a new language string, add it to all three files: `Ko.xaml`, `En.xaml`, `Zh.xaml`.

## External Dependencies

| Dependency | Purpose |
|---|---|
| NAudio 2.2.1 | Audio I/O (WaveOut, WASAPI, resampling) |
| FFmpeg / FFprobe | Audio format conversion (must be on PATH or bundled) |
| Python 3.10+ | Stem separation, noise reduction, sheet music scripts |
| FluidSynth + SoundFont (.sf2/.sf3) | MIDI synthesis for sheet music recognition |
| oemer | Optical music recognition — `pip install oemer` |
| HuggingFace models | Downloaded by `ModelManagerService` on demand |
