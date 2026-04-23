# Third-Party Notices

This project, **Selah**, uses, invokes, or depends on the third-party software listed below.
Each component remains subject to its own license. This file does not replace any original license text.

---

## 1. NAudio

- **License:** MIT License
- **Copyright:** Mark Heath and contributors
- **Source:** https://github.com/naudio/NAudio
- **Use:** Audio I/O — WaveOut, WASAPI playback, resampling, WAV encoding

---

## 2. FFmpeg / FFprobe

- **License:** GNU Lesser General Public License v2.1+ (LGPL v2.1+) for standard builds; GPL-enabled builds available separately
- **Source:** https://ffmpeg.org/
- **Use:** Audio/video format conversion and probing (MP3, MP4, FLAC, etc.)

Users and redistributors must confirm which FFmpeg build is in use and comply with the applicable license for that build.

---

## 3. Python Runtime

- **License:** Python Software Foundation License (PSF License)
- **Source:** https://www.python.org/
- **Use:** Runtime environment for stem separation, noise reduction, and sheet music recognition scripts

---

## 4. Demucs

- **License:** MIT License
- **Copyright:** Meta AI Research (Alexandre Défossez and contributors)
- **Source:** https://github.com/facebookresearch/demucs
- **Use:** Music source separation (via `demucs_runner.py`)

Model weights (htdemucs / htdemucs_ft) are also licensed under MIT.
ONNX exports by MrCitron (HuggingFace: MrCitron/demucs-v4-onnx) carry no separate export-format license; the original MIT weight license is considered to apply.

---

## 5. ONNX Runtime

- **License:** MIT License
- **Copyright:** Microsoft Corporation
- **Source:** https://github.com/microsoft/onnxruntime
- **Use:** ONNX model inference for htdemucs stem separation (via `onnx_runner.py`)

---

## 6. audio-separator

- **License:** MIT License
- **Source:** https://github.com/nomadkaraoke/python-audio-separator
- **Use:** MDX-Net / UVR vocal and instrumental separation (via `audio_separator_runner.py`)

---

## 7. oemer (Optical Music Recognition)

- **License:** Apache License 2.0
- **Source:** https://github.com/BreezeWhite/oemer
- **Use:** Sheet music recognition — scanned score image → MusicXML/MIDI (via `sheet_music_runner.py`)

---

## 8. FluidSynth

- **License:** GNU Lesser General Public License v2.1 (LGPL v2.1)
- **Source:** https://www.fluidsynth.org/
- **Use:** MIDI synthesis → WAV (via `midi_synthesizer.py`)

---

## 9. noisereduce

- **License:** MIT License
- **Source:** https://github.com/timsainb/noisereduce
- **Use:** Spectral-gate noise reduction (via `noise_reducer.py`)

---

## 10. NumPy

- **License:** BSD 3-Clause License
- **Copyright:** NumPy Developers
- **Source:** https://numpy.org/
- **Use:** Array operations in ONNX and audio processing scripts

---

## 11. SciPy

- **License:** BSD 3-Clause License
- **Copyright:** SciPy Developers
- **Source:** https://scipy.org/
- **Use:** Signal processing in ONNX stem separation script

---

## 12. torchaudio / PyTorch

- **License:** BSD 3-Clause License
- **Copyright:** Meta Platforms, Inc. and affiliates
- **Source:** https://pytorch.org/
- **Use:** Optional dependency for Demucs-based separation workflows

---

## 13. music21

- **License:** BSD 3-Clause License
- **Copyright:** Michael Scott Asato Cuthbert and contributors
- **Source:** https://web.mit.edu/music21/
- **Use:** MusicXML parsing and MIDI generation in sheet music recognition pipeline

---

## 14. SoundFont Files (user-supplied)

SoundFont (`.sf2` / `.sf3`) files are not bundled with Selah. Users must obtain and install them separately and comply with the applicable license for each file.

Recommended fonts and their licenses:
- **GeneralUser GS** — free for use; see distribution terms at the author's site
- **MuseScore_General.sf3** — MIT License

---

## 15. User Responsibility

Users and redistributors are responsible for reviewing and complying with all applicable license terms, including but not limited to those for:

- FFmpeg builds (LGPL vs. GPL)
- Python packages installed in the local environment
- Demucs and related model weights
- FluidSynth and SoundFont files
- Input audio/video materials and their copyright status

Technical capability provided by Selah does not imply legal permission to process, redistribute, or commercialize any input or output media.

---

## 16. No Exhaustive Guarantee

This file is not guaranteed to be a complete or exhaustive list of all runtime or transitive dependencies.
Environment-specific packages may vary. Consult relevant package managers, lock files, and upstream project pages for a full picture.
