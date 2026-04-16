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

No test projects exist yet.

## Architecture

**Selah** is a Windows desktop audio workstation (WPF, .NET 8) for worship music preparation. It follows strict MVVM with two projects:

- **`Selah.Core`** — platform-agnostic library: audio engine, data models, services
- **`Selah.App`** — WPF frontend: views, viewmodels, converters, localization resources

### Core Layer (`src/Selah.Core/`)

**Audio engine** (`Audio/`):
- `AudioEngine` — top-level playback manager; tries WaveOut, falls back to WASAPI
- `MasterMixerProvider` — mixes all tracks + metronome; feeds through `SoftLimiter`
- `TrackMixerProvider` — per-track gain/pan/solo/mute
- `ClipSampleProvider` — reads audio from a `Clip` region with resampling to project SR
- `AudioRenderer` — exports the timeline to a WAV file

**Models** (`Models/`):
- `Project` — root data model; saved as gzip-compressed JSON (`.slh`)
- `Track` → `Clip` → `AudioSource` — the core timeline hierarchy
- `TempoMap` / `TempoEvent` — tempo/beat information

**Services** (`Services/`):
- `ProjectService` — load/save `.slh` (JSON.gz)
- `FFmpegService` — shells out to `ffmpeg`/`ffprobe` for format conversion
- `StemSeparatorService` — dispatches stem separation to one of three Python backends (onnx_runner.py, demucs_runner.py, audio_separator_runner.py)
- `NoiseReductionService` — shells out to `noise_reducer.py`
- `ModelManagerService` — downloads ML models from HuggingFace

### App Layer (`src/Selah.App/`)

- `MainViewModel` — top-level orchestrator; owns all major commands
- `TimelineViewModel` — playhead position, zoom, snap
- `ProjectViewModel` / `TrackViewModel` / `ClipViewModel` — UI wrappers around core models
- Localization via XAML resource dictionaries: `Resources/Ko.xaml`, `Resources/En.xaml`
- Themes: `Resources/Light.xaml`, `Resources/Dark.xaml`

### Python Scripts (`scripts/`)

Stem separation and noise reduction run as **external Python subprocesses**. Scripts expect Python 3.10+ and write results to files that the C# service then reads.

## Key Conventions

### 시간 표시 형식

프로젝트 내 모든 시간 표시는 **`mm:ss.fff`** 형식을 사용합니다.

- `mm` — 분 (2자리, 0 패딩)
- `ss` — 초 (2자리, 0 패딩)
- `fff` — 밀리초 (3자리, 0 패딩)

**구현 패턴 (C#):**

```csharp
var ts = TimeSpan.FromSeconds(seconds);
return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
```

> **주의:** C# 커스텀 숫자 포맷 문자열에서 `f`는 밀리초 지정자가 아니라 리터럴 문자입니다.
> `TimeSpan`의 컴포넌트를 직접 포맷하거나 `TimeSpan.ToString(@"mm\:ss\.fff")`를 사용하세요.
> `{seconds:000f}` 같은 표현식은 `051f`처럼 잘못된 출력을 생성합니다.

### Other conventions

- Editing is **non-destructive**: `Clip` objects reference regions of `AudioSource` files; source files are never modified.
- All audio is resampled to the project's fixed sample rate at playback time.
- Nullable reference types are enabled (`<Nullable>enable</Nullable>`); keep code null-safe.
- The app is Windows-only (`net8.0-windows`); do not introduce cross-platform assumptions.

## External Dependencies

| Dependency | Purpose |
|---|---|
| NAudio 2.2.1 | Audio I/O (WaveOut, WASAPI, resampling) |
| FFmpeg / FFprobe | Audio format conversion (must be on PATH or bundled) |
| Python 3.10+ | Stem separation & noise reduction scripts |
| HuggingFace models | Downloaded by `ModelManagerService` on demand |
