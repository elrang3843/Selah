# Selah — Setup & Dependency Installation Guide

This guide covers every external tool and Python package required to unlock all Selah features.
Core playback works without any of these; each section below enables an additional feature group.

---

## 1. Runtime Prerequisites

### .NET 8 Desktop Runtime (required)

Selah is a WPF application targeting .NET 8.

- Download: <https://dotnet.microsoft.com/download/dotnet/8.0>
- Choose **".NET Desktop Runtime 8.x"** for Windows x64.

### Python 3.10 or later (required for AI features)

Required for stem separation, noise reduction, and sheet music recognition.

- Download: <https://www.python.org/downloads/>
- During installation, check **"Add Python to PATH"**.
- Verify: open a terminal and run `python --version`.

---

## 2. FFmpeg (Audio Import / Export)

FFmpeg is used to import audio files in formats other than WAV and to export mixed audio.

### Installation

1. Download a Windows build from <https://ffmpeg.org/download.html>
   (e.g. the "gyan.dev full" or "BtbN" release).
2. Extract the archive and locate the `bin\` folder containing `ffmpeg.exe` and `ffprobe.exe`.
3. Add that `bin\` folder to your system `PATH`
   (*System Properties → Environment Variables → Path → New*).
4. Verify: `ffmpeg -version`

---

## 3. Stem Separation

Selah supports two stem separation backends. Install one or both.

### Option A — audio-separator (recommended)

```
pip install "audio-separator[cpu]"
```

GPU acceleration (optional, requires a compatible NVIDIA GPU and CUDA):

```
pip install "audio-separator[gpu]"
```

Models are downloaded automatically on first use (~100–400 MB each).

### Option B — ONNX Runtime (lightweight)

```
pip install onnxruntime numpy scipy
```

Models are downloaded through the Selah model manager before first use.

---

## 4. Noise Reduction

```
pip install noisereduce soundfile numpy
```

No additional system tools are required.

---

## 5. Sheet Music Recognition (OMR)

The sheet music import feature (File → Import Sheet Music) converts a scanned or
photographed score image into per-instrument audio tracks using three steps:
image preprocessing → OMR recognition → MIDI synthesis.

### 5.1 OMR & Analysis Packages

```
pip install oemer music21 mido Pillow scipy
```

| Package  | Purpose                                  |
|----------|------------------------------------------|
| oemer    | Optical music recognition (OMR) engine   |
| music21  | MusicXML / MIDI analysis and export      |
| mido     | MIDI file manipulation & GM patch remapping |
| Pillow   | Image preprocessing (grayscale, binarize)|
| scipy    | Noise reduction filter for score images  |

### 5.2 FluidSynth — MIDI → WAV Synthesis

FluidSynth converts the recognized MIDI into audio using a SoundFont instrument bank.
Two components are needed: the **native library** and the **Python wrapper**.

#### Step 1 — Install the FluidSynth native library

Download and run the Windows installer from <https://www.fluidsynth.org/>

> The installer places `libfluidsynth-3.dll` (and the `fluidsynth.exe` executable)
> in `C:\Program Files\FluidSynth\bin\`.
> Selah finds the DLL automatically — no manual PATH configuration is required.

Alternatively, install via Chocolatey:

```
choco install fluidsynth
```

#### Step 2 — Install the Python wrapper

```
pip install fluidsynth
```

This installs the ctypes bindings that allow Selah's Python script to call the
native library directly, without shelling out to `fluidsynth.exe`.

> **Why both?** `pip install fluidsynth` installs only the Python wrapper.
> The wrapper still needs `libfluidsynth-3.dll` from the native installer to
> function. The installer provides the DLL; pip provides the Python interface.

#### Verify

```python
python -c "import fluidsynth; print('fluidsynth OK')"
```

If this prints `fluidsynth OK`, synthesis is ready (assuming a SoundFont is also installed — see §5.3).

---

### 5.3 SoundFont (.sf2)

A SoundFont is an instrument sample bank that FluidSynth uses to render MIDI notes
into realistic audio. Selah does not bundle one due to file size.

#### Free SoundFont comparison

Both SF2 and SF3 formats are supported. SF3 stores samples compressed with OGG Vorbis,
offering the same quality at a much smaller file size (requires FluidSynth 1.1.7+).

| SoundFont | Format | Size | Quality | License | Notes |
|-----------|--------|------|---------|---------|-------|
| **GeneralUser GS** | SF2 | ~29 MB | ★★★★☆ | Free (commercial OK) | Best size-to-quality ratio; **recommended** |
| **MuseScore_General.sf3** | SF3 | ~50 MB | ★★★★★ | MIT | Best quality + compressed |
| **MuseScore_General.sf2** | SF2 | ~206 MB | ★★★★★ | MIT | Best quality (uncompressed) |
| **FluidR3_GM.sf2** | SF2 | ~141 MB | ★★★☆☆ | MIT | Widely distributed; weak on brass/reeds |

---

**GeneralUser GS** — recommended for most users

Smallest download with well-balanced quality. Strong performance on piano, strings,
and woodwinds (including saxophone).

- Download: `schristiancollins.com/generaluser.php`
- Extract `GeneralUser GS.sf2` from the archive.

**MuseScore_General.sf3** — best quality + compact (recommended high-quality option)

Same samples as MuseScore_General.sf2, compressed with OGG Vorbis as an SF3 file.
206 MB reduced to ~50 MB with identical audio quality.

- MuseScore installation path: `C:\Program Files\MuseScore 4\sound\MuseScore_General.sf3`
- Also available for separate download from the MuseScore website.

**MuseScore_General.sf2** — best quality (uncompressed, SF3 alternative)

Use this if you have an older FluidSynth build without SF3 support. Available from
the same location as the SF3 version.

**FluidR3_GM.sf2** — fallback option (lower quality)

Included with many Linux distributions and music applications, making it easy to
obtain. However, brass and reed instrument samples are noticeably weak
(saxophone quality in particular is poor). Use as a last resort.

> **For worship music:** GeneralUser GS is recommended. Piano, strings, acoustic
> guitar, and woodwind patches are well-suited for hymn and worship score playback.

#### Where to place the .sf2 / .sf3 file

Place the `.sf2` or `.sf3` file in **any one** of these locations (Selah searches them in order):

| Priority | Path |
|----------|------|
| 1 (highest) | `%AppData%\Selah\soundfonts\` |
| 2 | `<app folder>\soundfonts\` |
| 3 | `C:\Program Files\FluidSynth\` |
| 4 | `C:\soundfonts\` |

The recommended location is `%AppData%\Selah\soundfonts\` (created automatically
by Selah on first run). You can open it by typing that path into Explorer's address bar.

---

## 6. Summary Table

| Feature                     | Required packages / tools |
|-----------------------------|---------------------------|
| Basic playback & editing    | .NET 8 Desktop Runtime |
| Audio import / export       | FFmpeg + FFprobe on PATH |
| Stem separation (audio-sep) | Python 3.10+ · `audio-separator[cpu]` |
| Stem separation (ONNX)      | Python 3.10+ · `onnxruntime numpy scipy` |
| Noise reduction             | Python 3.10+ · `noisereduce soundfile numpy` |
| Sheet music recognition     | Python 3.10+ · `oemer music21 mido Pillow scipy` |
| Sheet music synthesis       | FluidSynth installer · `pip install fluidsynth` · SoundFont (.sf2) |

---

## 7. Quick-Start Install (all features)

### Option A — Setup script (recommended)

Run **`setup_env.bat`** from the repository root. It lets you choose a feature set and installs the required Python packages automatically.

```
setup_env.bat
```

### Option B — pip directly

```bat
:: Install all Python packages in one go
pip install oemer music21 mido Pillow scipy fluidsynth noisereduce soundfile numpy "audio-separator[cpu]"
```

### Option C — requirements files

| File | Scope |
|------|-------|
| `requirements.txt` | Everything |
| `requirements-stem.txt` | Stem separation only |
| `requirements-sheet-music.txt` | Sheet music recognition only |

```bat
pip install -r requirements.txt
```

Then:
1. Install FluidSynth from <https://www.fluidsynth.org/> (native DLL for synthesis).
2. Download a SoundFont and place it in `%AppData%\Selah\soundfonts\`.
3. Install FFmpeg and add its `bin\` folder to PATH.

---

## 8. Troubleshooting

### "FluidSynth not found"

- Make sure you ran `pip install fluidsynth`.
- Make sure the FluidSynth **native installer** was run (provides `libfluidsynth-3.dll`).
- If the DLL is in a non-standard location, add that folder to your system `PATH`.

### "SoundFont (.sf2) not found"

- Place a `.sf2` file in `%AppData%\Selah\soundfonts\` and restart Selah.

### "oemer not found" / OMR fails

- Run `pip install oemer` and ensure Python 3.10+ is on PATH.
- oemer requires a network connection on first use to download its own model weights.

### Python not detected

- Reinstall Python and check **"Add Python to PATH"** during setup.
- Avoid using the Windows Store version of Python (it may not be detected in
  background processes); use the installer from python.org instead.
