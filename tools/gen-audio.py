#!/usr/bin/env python3
"""Synthesises the game's placeholder audio into UnityProject/Assets/Main/Audio.

Every clip is generated from this file, so the asset's provenance is the script
(ATTRIBUTION.md: project-owned, CC0). There is no downloaded sample anywhere in
the repository.

Deterministic on purpose: the same Python version and this file produce the same
bytes, so `git diff` on the .wav files means somebody changed the sound, not that
a noise source drifted. Noise comes from a seeded LCG, never from `random`.

Usage:
    python tools/gen-audio.py            # write the clips
    python tools/gen-audio.py --check    # report what would change, write nothing

Format: mono, 22 050 Hz, 16-bit PCM. The rate is deliberately below 44.1 kHz —
these are short, dull placeholder sounds, and the Web build pays for every
sample. Unity re-encodes on build (WebGL imports AAC, per the 6.3 manual).
"""

from __future__ import annotations

import argparse
import math
import os
import struct
import sys

SAMPLE_RATE = 22050
OUT_DIR = os.path.join("UnityProject", "Assets", "Main", "Audio")


# --------------------------------------------------------------------------
# tiny DSP kit
# --------------------------------------------------------------------------

class Noise:
    """A deterministic white-noise source (LCG), so a rebuild is byte-identical."""

    def __init__(self, seed: int) -> None:
        self._state = seed & 0xFFFFFFFF

    def next(self) -> float:
        self._state = (1664525 * self._state + 1013904223) & 0xFFFFFFFF
        return (self._state / 0xFFFFFFFF) * 2.0 - 1.0


def silence(seconds: float) -> list[float]:
    return [0.0] * int(round(seconds * SAMPLE_RATE))


def decay(t: float, rate: float) -> float:
    """Exponential decay; `rate` is how many e-folds per second."""
    return math.exp(-rate * t)


def ramp(t: float, attack: float, length: float, release: float) -> float:
    """Linear attack / sustain / release envelope, no clicks at either end."""
    if t < attack:
        return t / attack
    remaining = length - t
    if remaining < release:
        return max(0.0, remaining / release)
    return 1.0


def mix(buf: list[float], index: int, value: float) -> None:
    if 0 <= index < len(buf):
        buf[index] += value


def tone(buf: list[float], start: float, length: float, freq: float,
         gain: float, decay_rate: float = 0.0, sweep_to: float | None = None,
         harmonic: float = 0.0) -> None:
    """Adds one decaying (optionally gliding) sine partial."""
    first = int(round(start * SAMPLE_RATE))
    count = int(round(length * SAMPLE_RATE))
    phase = 0.0
    for i in range(count):
        t = i / SAMPLE_RATE
        f = freq if sweep_to is None else freq + (sweep_to - freq) * (i / max(1, count - 1))
        phase += 2.0 * math.pi * f / SAMPLE_RATE
        envelope = decay(t, decay_rate) if decay_rate > 0.0 else 1.0
        value = math.sin(phase)
        if harmonic > 0.0:
            value += harmonic * math.sin(phase * 2.0)
        mix(buf, first + i, value * gain * envelope)


def noise_burst(buf: list[float], start: float, length: float, gain: float,
                decay_rate: float, seed: int, lowpass: float) -> None:
    """Filtered noise: a one-pole low-pass tames a burst into a thud rather than a hiss."""
    first = int(round(start * SAMPLE_RATE))
    count = int(round(length * SAMPLE_RATE))
    source = Noise(seed)
    state = 0.0
    for i in range(count):
        t = i / SAMPLE_RATE
        state += lowpass * (source.next() - state)
        mix(buf, first + i, state * gain * decay(t, decay_rate))


def pluck(buf: list[float], start: float, freq: float, gain: float, length: float = 0.9) -> None:
    """A soft plucked string: fundamental plus a quiet octave and a fast attack."""
    tone(buf, start, length, freq, gain, decay_rate=5.0)
    tone(buf, start, length * 0.6, freq * 2.0, gain * 0.25, decay_rate=7.0)


def drum(buf: list[float], start: float, gain: float, seed: int) -> None:
    """A frame drum: a low body plus a short skin slap."""
    tone(buf, start, 0.22, 96.0, gain, decay_rate=16.0, sweep_to=54.0)
    noise_burst(buf, start, 0.05, gain * 0.35, 45.0, seed, 0.35)


def crossfade_loop(buf: list[float], fade_seconds: float = 0.05) -> list[float]:
    """Makes the end meet the beginning: the tail fades into the head, then is cut.

    Without this, every loop point in the browser is a step discontinuity — an
    audible click once per six seconds, which is worse than the placeholder tone.
    """
    fade = min(int(round(fade_seconds * SAMPLE_RATE)), len(buf) // 4)
    if fade <= 0:
        return buf
    head = buf[:fade]
    tail_start = len(buf) - fade
    for i in range(fade):
        alpha = i / fade
        buf[i] = head[i] * alpha + buf[tail_start + i] * (1.0 - alpha)
    return buf[:tail_start]


def normalise(buf: list[float], peak: float) -> list[float]:
    largest = max((abs(v) for v in buf), default=0.0)
    if largest <= 1e-9:
        return buf
    scale = peak / largest
    return [v * scale for v in buf]


# --------------------------------------------------------------------------
# the clips
# --------------------------------------------------------------------------

def sfx_hit() -> list[float]:
    buf = silence(0.10)
    noise_burst(buf, 0.0, 0.09, 0.9, 42.0, 11, 0.45)
    tone(buf, 0.0, 0.09, 220.0, 0.6, decay_rate=30.0, sweep_to=150.0)
    return normalise(buf, 0.75)


def sfx_hurt() -> list[float]:
    buf = silence(0.30)
    tone(buf, 0.0, 0.28, 110.0, 0.8, decay_rate=7.0, sweep_to=68.0, harmonic=0.3)
    noise_burst(buf, 0.0, 0.12, 0.35, 20.0, 23, 0.15)
    return normalise(buf, 0.8)


def sfx_kill() -> list[float]:
    buf = silence(0.22)
    tone(buf, 0.0, 0.20, 340.0, 0.7, decay_rate=10.0, sweep_to=110.0, harmonic=0.35)
    noise_burst(buf, 0.0, 0.08, 0.3, 40.0, 37, 0.4)
    return normalise(buf, 0.7)


def sfx_pickup() -> list[float]:
    buf = silence(0.18)
    tone(buf, 0.0, 0.07, 880.0, 0.55, decay_rate=16.0)
    tone(buf, 0.06, 0.11, 1320.0, 0.5, decay_rate=14.0)
    return normalise(buf, 0.6)


def sfx_telegraph() -> list[float]:
    buf = silence(0.38)
    tone(buf, 0.0, 0.34, 440.0, 0.5, sweep_to=660.0)
    # A tremolo names the cue "warning" without a second clip.
    for i in range(int(0.34 * SAMPLE_RATE)):
        buf[i] *= 0.55 + 0.45 * math.cos(2.0 * math.pi * 9.0 * (i / SAMPLE_RATE))
    return normalise(buf, 0.55)


def sfx_boss_phase() -> list[float]:
    buf = silence(0.85)
    tone(buf, 0.0, 0.85, 65.0, 0.8, decay_rate=2.6)
    tone(buf, 0.0, 0.85, 98.0, 0.5, decay_rate=3.1)
    tone(buf, 0.0, 0.4, 131.0, 0.25, decay_rate=6.0)
    noise_burst(buf, 0.0, 0.25, 0.2, 12.0, 53, 0.08)
    return normalise(buf, 0.75)


def sfx_rent() -> list[float]:
    buf = silence(0.20)
    tone(buf, 0.0, 0.09, 1180.0, 0.5, decay_rate=22.0, harmonic=0.4)
    tone(buf, 0.05, 0.14, 1560.0, 0.45, decay_rate=18.0, harmonic=0.3)
    return normalise(buf, 0.6)


def sfx_place() -> list[float]:
    buf = silence(0.14)
    noise_burst(buf, 0.0, 0.10, 0.7, 45.0, 67, 0.25)
    tone(buf, 0.0, 0.10, 180.0, 0.45, decay_rate=34.0, sweep_to=120.0)
    return normalise(buf, 0.65)


def sfx_rotate() -> list[float]:
    buf = silence(0.12)
    first = 0
    count = int(0.10 * SAMPLE_RATE)
    source = Noise(71)
    state = 0.0
    for i in range(count):
        t = i / SAMPLE_RATE
        cutoff = 0.55 - 0.35 * (i / max(1, count - 1))
        state += cutoff * (source.next() - state)
        mix(buf, first + i, state * 0.6 * decay(t, 30.0))
    return normalise(buf, 0.5)


def sfx_merge() -> list[float]:
    buf = silence(0.55)
    for index, freq in enumerate((523.25, 659.25, 783.99)):
        tone(buf, index * 0.055, 0.45, freq, 0.4, decay_rate=5.5)
    return normalise(buf, 0.65)


def sfx_refuse() -> list[float]:
    buf = silence(0.22)
    count = int(0.18 * SAMPLE_RATE)
    phase = 0.0
    for i in range(count):
        t = i / SAMPLE_RATE
        phase += 2.0 * math.pi * 140.0 / SAMPLE_RATE
        square = 1.0 if math.sin(phase) >= 0.0 else -1.0
        tremolo = 0.5 + 0.5 * math.cos(2.0 * math.pi * 28.0 * t)
        mix(buf, i, square * 0.22 * tremolo * decay(t, 9.0))
    return normalise(buf, 0.5)


def music_camp() -> list[float]:
    """A slow pentatonic phrase over a low drone: the camp's quiet sala."""
    length = 6.0
    buf = silence(length)
    tone(buf, 0.0, length, 130.81, 0.16)          # C3 drone
    tone(buf, 0.0, length, 196.00, 0.07)          # G3 fifth, quieter
    for start, freq in ((0.00, 261.63), (1.50, 329.63), (3.00, 293.66), (4.50, 392.00)):
        pluck(buf, start, freq, 0.22)
    return crossfade_loop(normalise(buf, 0.55))


def music_battle() -> list[float]:
    """A drum pulse and a minor drone: enough to tell the arena from the camp."""
    length = 6.0
    buf = silence(length)
    tone(buf, 0.0, length, 98.00, 0.14)           # G2 drone
    tone(buf, 0.0, length, 146.83, 0.06)          # D3
    seed = 97
    for beat in range(12):
        drum(buf, beat * 0.5, 0.30 if beat % 4 == 0 else 0.18, seed + beat)
    return crossfade_loop(normalise(buf, 0.6))


CLIPS = {
    "sfx-hit": sfx_hit,
    "sfx-hurt": sfx_hurt,
    "sfx-kill": sfx_kill,
    "sfx-pickup": sfx_pickup,
    "sfx-telegraph": sfx_telegraph,
    "sfx-boss-phase": sfx_boss_phase,
    "sfx-rent": sfx_rent,
    "sfx-place": sfx_place,
    "sfx-rotate": sfx_rotate,
    "sfx-merge": sfx_merge,
    "sfx-refuse": sfx_refuse,
    "music-camp": music_camp,
    "music-battle": music_battle,
}


def to_wav_bytes(samples: list[float]) -> bytes:
    frames = bytearray()
    for value in samples:
        clipped = max(-1.0, min(1.0, value))
        frames += struct.pack("<h", int(round(clipped * 32767.0)))

    data_size = len(frames)
    header = b"RIFF" + struct.pack("<I", 36 + data_size) + b"WAVE"
    header += b"fmt " + struct.pack("<IHHIIHH", 16, 1, 1, SAMPLE_RATE,
                                    SAMPLE_RATE * 2, 2, 16)
    header += b"data" + struct.pack("<I", data_size)
    return header + bytes(frames)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true",
                        help="report differences without writing")
    args = parser.parse_args()

    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out_dir = os.path.join(root, OUT_DIR)
    if not args.check:
        os.makedirs(out_dir, exist_ok=True)

    changed = 0
    total = 0
    for name, build in CLIPS.items():
        data = to_wav_bytes(build())
        total += len(data)
        path = os.path.join(out_dir, name + ".wav")
        existing = None
        if os.path.exists(path):
            with open(path, "rb") as handle:
                existing = handle.read()
        if existing == data:
            print(f"  unchanged  {name}.wav  {len(data):>8,} bytes")
            continue
        changed += 1
        print(f"  {'would write' if args.check else 'wrote    '}  {name}.wav  {len(data):>8,} bytes")
        if not args.check:
            with open(path, "wb") as handle:
                handle.write(data)

    print(f"{len(CLIPS)} clips, {total:,} bytes of PCM "
          f"({total / 1024.0:.0f} KiB), {changed} changed")
    return 1 if args.check and changed else 0


if __name__ == "__main__":
    sys.exit(main())
