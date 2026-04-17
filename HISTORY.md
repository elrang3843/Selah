# Selah — Release History

---

## v1.0.0 (2026-04-17)

First stable release.

### New — Clip Editing

- **Multi-clip selection** — Ctrl+click to toggle individual clips; Shift+click for range selection across a track
- **Clipboard operations** — Copy (Ctrl+C), Cut (Ctrl+X), Paste (Ctrl+V)
- **Merge clips** (Ctrl+M) — merges all selected clips on the same track into one
- **Split selected clips** (S) — splits all selected clips simultaneously at the playhead
- **Move after previous clip** (Ctrl+J) — shifts the selected group immediately after the preceding clip on the same track
- **Move to playhead position** (Ctrl+G) — moves the selected group so its first clip starts at the playhead
- **Move to track start** (Ctrl+H) — moves the selected group so its first clip starts at position 0

### New — Transport

- Auto-scroll: timeline scrolls to keep the playhead in view during playback

### Bug Fixes

- Playhead time counter stays at 00:00.000 after Stop+ReturnToStart (root cause: `_lastPlayheadFrames` not reset on `Seek()`)
- `PlayheadTimeDisplay` not updating when seeking by clicking the timeline ruler
- IsPlaying button state incorrect after Stop+ReturnToStart (race condition between `Dispatcher.BeginInvoke` and audio restart)
- Stale `PlayheadAdvanced` events from a previous playback session overwriting the reset playhead position (fixed via playback generation counter)
- Ctrl+J (Move After Previous) requiring a mouse click before rendering (canvas not subscribed to `ClipViewModel.PropertyChanged`)

---

## v0.9.0 (2026-04-16 ~ 2026-04-17)

### New — Sheet Music Recognition

- Import scanned or photographed sheet music as per-instrument audio tracks
- OMR pipeline: oemer (optical music recognition) → MusicXML → MIDI → WAV via FluidSynth synthesis
- SF3 SoundFont format support (in addition to SF2)
- Variable tempo rendering from tempo maps in the score
- Auto-correction for dark-background (inverted) score images
- Transparent (RGBA/LA/P) image handling — composited over white before recognition
- Robust FluidSynth DLL auto-detection across multiple install paths (Chocolatey, manual, PATH)
- Friendly error messages with actionable guidance when recognition fails
- Supported instruments panel in the sheet music dialog
- Memory and UI delay improvements during large score processing

---

## v0.8.0 (2026-04-14 ~ 2026-04-16)

### New — Stem Separation & Noise Reduction

- **Stem separation** with three backends:
  - audio-separator (MDX-Net, primary — best vocal isolation)
  - ONNX Runtime (lightweight, no GPU required)
  - Demucs (GPU-accelerated, highest quality)
- **Noise reduction** via noisereduce spectral gating (free, local, no GPU)
- Model Manager window — download and manage ML models with live progress
- Real-time track creation as stems are separated
- Progress popup window for import, export, separation, and noise reduction operations
- Startup installation guide when Python or FFmpeg is not found

---

## v0.7.0 (2026-04-11 ~ 2026-04-16)

### New — Localization, Themes & Help

- **Korean / English / Chinese** localization with runtime language switching
- **Dark / Light** theme with full menu and canvas support (defaults to dark)
- In-app HTML help viewer in all three languages
- Timeline sub-tick marks at 0.1 s and 0.5 s intervals
- Track selection checkboxes in the track header panel

---

## v0.1.0 (2026-04-08 ~ 2026-04-11)

### Initial Implementation

- Core data model: Project → Track → Clip → AudioSource, saved as gzip-compressed JSON (`.slh`)
- WPF timeline canvas with waveform rendering and scroll/zoom
- Playback engine: WaveOut (primary) + WASAPI shared (fallback), master mixer with per-track gain/pan/solo/mute
- Soft limiter, metronome with tempo map
- Audio import/export via FFmpeg; hardware detection service
- Auto-stop when all clips finish playing
- Non-destructive editing — clips reference regions of source files; source files are never modified

### Audio Stability Fixes (applied during this phase)

- Deadlock on repeated play/pause
- Low-pitched scratch noise from WASAPI sample rate mismatch
- Distortion from incorrect SoftLimiter knee region behaviour
- Audio noise caused by seek-on-read in sample provider
- Playhead time display showing literal `f` character instead of milliseconds
