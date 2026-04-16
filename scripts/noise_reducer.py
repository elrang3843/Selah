#!/usr/bin/env python3
"""
Selah noise_reducer.py — spectral-gating noise reduction via the noisereduce library.
No model file required; works entirely from numpy/scipy signal processing.

Install dependency:
    pip install noisereduce

Usage:
    python noise_reducer.py --input <wav> --output <wav> \
        [--strength 0.75] [--stationary]

Output protocol (stdout):
    PROGRESS:<0-100>   progress percentage
    LOG:<message>      status / error messages
"""

import sys
import os
import argparse
import wave

try:
    import numpy as np
except ImportError:
    print("LOG:NOISEREDUCE_MISSING", flush=True)
    sys.exit(1)

try:
    import noisereduce as nr
except ImportError:
    print("LOG:NOISEREDUCE_MISSING", flush=True)
    sys.exit(1)


def read_wav_float(path: str):
    """
    Read a WAV file and return (float32 ndarray, sample_rate, channels).
    ndarray shape: [samples] for mono, [samples, channels] for multi-channel.
    """
    with wave.open(path, "rb") as f:
        nch = f.getnchannels()
        sr  = f.getframerate()
        sw  = f.getsampwidth()
        n   = f.getnframes()
        raw = f.readframes(n)

    dtype_map = {1: np.int8, 2: np.int16, 4: np.int32}
    dtype = dtype_map.get(sw)
    if dtype is None:
        raise ValueError(f"Unsupported sample width: {sw} bytes")

    samples = np.frombuffer(raw, dtype=dtype).astype(np.float32)
    samples /= float(2 ** (sw * 8 - 1))   # normalise to [-1, 1]

    if nch > 1:
        samples = samples.reshape(-1, nch)
    return samples, sr, nch


def write_wav_int16(path: str, audio: np.ndarray, sr: int, nch: int) -> None:
    """Write float32 audio ([-1, 1]) as a 16-bit PCM WAV file."""
    peak = float(np.max(np.abs(audio)))
    if peak > 0.9999:
        audio = audio * (0.9999 / peak)

    pcm = (np.clip(audio, -1.0, 1.0) * 32767.0).astype(np.int16)
    if nch > 1:
        pcm = pcm.reshape(-1)   # interleave channels back to flat

    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with wave.open(path, "wb") as f:
        f.setnchannels(nch)
        f.setsampwidth(2)
        f.setframerate(sr)
        f.writeframes(pcm.tobytes())


def main():
    parser = argparse.ArgumentParser(description="Selah spectral-gating noise reducer")
    parser.add_argument("--input",      required=True,  help="Input WAV path")
    parser.add_argument("--output",     required=True,  help="Output WAV path")
    parser.add_argument("--strength",   type=float, default=0.75,
                        help="Noise reduction strength 0.0–1.0 (default 0.75)")
    parser.add_argument("--stationary", action="store_true",
                        help="Assume stationary noise (faster for constant hum/hiss)")
    args = parser.parse_args()

    print(f"LOG:Loading {os.path.basename(args.input)}", flush=True)
    print("PROGRESS:5", flush=True)

    try:
        audio, sr, nch = read_wav_float(args.input)
    except Exception as exc:
        print(f"LOG:READ_FAILED: {exc}", flush=True)
        sys.exit(1)

    duration = audio.shape[0] / sr
    print(f"LOG:Processing {duration:.1f}s audio ({nch}ch, {sr} Hz)", flush=True)
    print("PROGRESS:10", flush=True)

    if audio.ndim == 1:
        # Mono
        print("PROGRESS:20", flush=True)
        reduced = nr.reduce_noise(
            y=audio,
            sr=sr,
            prop_decrease=args.strength,
            stationary=args.stationary,
        )
        print("PROGRESS:85", flush=True)
    else:
        # Multi-channel: process each channel independently
        channels_out = []
        for i in range(nch):
            pct = 20 + int(65 * i / nch)
            print(f"PROGRESS:{pct}", flush=True)
            ch_reduced = nr.reduce_noise(
                y=audio[:, i],
                sr=sr,
                prop_decrease=args.strength,
                stationary=args.stationary,
            )
            channels_out.append(ch_reduced)
        reduced = np.stack(channels_out, axis=1)
        print("PROGRESS:85", flush=True)

    print("LOG:Writing output", flush=True)
    write_wav_int16(args.output, reduced, sr, nch)

    print("PROGRESS:100", flush=True)
    print("LOG:DONE", flush=True)


if __name__ == "__main__":
    main()
