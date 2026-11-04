#!/usr/bin/env python3
"""Original, deterministic V5 material impacts; no downloaded samples required.

Only impact WAVs are written. Music, footsteps and weapon reports are untouched.
Python 3 + NumPy + SciPy. Existing Unity GUIDs/import settings are preserved.
Run: python Tools/GenerateImpactAudio.py
"""
from pathlib import Path
import hashlib
import json
import math
import wave

import numpy as np
from scipy.signal import butter, sosfilt

ROOT = Path(__file__).resolve().parents[1]
RATE = 44100
SURFACES = ("metal", "wood", "concrete", "soil")


def filtered_noise(rng, size, low, high):
    x = sosfilt(butter(3, [low, high], fs=RATE, btype="bandpass", output="sos"), rng.normal(size=size))
    return x / max(1e-9, np.std(x))


def impact(index, surface="concrete"):
    """A recognisable body AFTER the contact, not four versions of one crack.

    Metal: non-harmonic, damped plate modes; wood: short hollow low-mid knock;
    concrete: unpitched grit with two tiny debris contacts; soil: soft dirt puff.
    Nothing rings indefinitely, whistles, or contains a sharp ultrasonic spike.
    """
    if surface not in SURFACES:
        raise ValueError(surface)
    index %= 3
    rng = np.random.default_rng(51071 + 997 * SURFACES.index(surface) + index)
    length = {"metal": .245, "wood": .155, "concrete": .145, "soil": .17}[surface]
    t = np.arange(round(length * RATE)) / RATE
    shift = (.975, 1.035, 1.0)[index]
    rise = 1 - np.exp(-t / .0011)
    if surface == "metal":
        # Plate modes do not form a musical pitch. The sustained midrange is
        # deliberately different from the pistol's broadband opening transient.
        x = np.zeros_like(t)
        for frequency, weight, decay in [(620, .65, .043), (1037, 1, .060),
                                          (1711, .56, .038), (2683, .22, .022)]:
            x += weight * np.sin(2 * math.pi * frequency * shift * t + rng.uniform(-.15, .15)) * np.exp(-t / decay)
        x *= 1 - np.exp(-t / .0028)
        x += .16 * filtered_noise(rng, len(t), 1300, 5400) * np.exp(-t / .005) * rise
    elif surface == "wood":
        # A box-body thock, not a metallic pitched-down clang.
        x = (1.0 * np.sin(2 * math.pi * 245 * shift * t) * np.exp(-t / .021)
             + .52 * np.sin(2 * math.pi * 436 * shift * t + .12) * np.exp(-t / .014)
             + .23 * np.sin(2 * math.pi * 815 * shift * t) * np.exp(-t / .009)) * rise
        x += .26 * filtered_noise(rng, len(t), 500, 2400) * np.exp(-t / .007) * rise
        x += .10 * filtered_noise(rng, len(t), 250, 1300) * np.exp(-t / .026) * rise
    elif surface == "concrete":
        # A dull contact under an unpitched sandy break. Later grains make its
        # identity survive the nearby gun report without raising the mix gain.
        x = .68 * filtered_noise(rng, len(t), 1200, 5400) * np.exp(-t / .013) * rise
        x += .25 * filtered_noise(rng, len(t), 330, 1200) * np.exp(-t / .010) * rise
        for when, level in [(.026, .23), (.045, .13)]:
            a = np.maximum(0, t - when)
            envelope = np.where(t >= when, (1 - np.exp(-a / .0015)) * np.exp(-a / .009), 0)
            x += level * filtered_noise(rng, len(t), 1700, 4800) * envelope
    else:
        x = filtered_noise(rng, len(t), 100, 1250) * np.exp(-t / .026) * (1 - np.exp(-t / .003))
        x += .17 * filtered_noise(rng, len(t), 650, 2200) * np.exp(-t / .017) * rise

    # Gentle onset/release and no DC, preserving the characteristic envelope.
    x -= np.mean(x)
    x[:44] *= np.linspace(0, 1, 44)
    release = round(.018 * RATE)
    x[-release:] *= np.cos(np.linspace(0, math.pi / 2, release)) ** 2
    x[0] = x[-1] = 0
    peak_db = -8.0 if surface != "soil" else -10.0
    x *= 10 ** (peak_db / 20) / max(1e-12, np.max(np.abs(x)))
    # A resonant material must not get louder merely because its tail is longer.
    rms_ceiling = 10 ** ((-23.5 if surface == "metal" else -24.5) / 20)
    x *= min(1, rms_ceiling / max(1e-12, np.sqrt(np.mean(x*x))))
    return x


def inspect_wave(x):
    spectrum = np.abs(np.fft.rfft(x)) ** 2
    frequency = np.fft.rfftfreq(len(x), 1 / RATE)
    energy = np.cumsum(x*x)
    return {"seconds": round(len(x) / RATE, 4),
            "peakDbFS": round(20 * np.log10(max(1e-12, np.max(np.abs(x)))), 3),
            "rmsDbFS": round(20 * np.log10(max(1e-12, np.sqrt(np.mean(x*x)))), 3),
            "energyCentroidHz": round(float(np.sum(frequency * spectrum) / sum(spectrum)), 1),
            "energy90ms": round(1000 * np.searchsorted(energy, energy[-1] * .90) / RATE, 2),
            "energyAfter20ms": round(float(sum(x[round(.02*RATE):]**2) / energy[-1]), 4),
            "clippedSamples": int(np.sum(abs(x) >= 1)),
            "dcOffset": round(float(np.mean(x)), 7)}


def write_wave(path, x):
    pcm = np.rint(x * 32767).astype("<i2")
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(pcm.tobytes())
    return pcm.astype(float) / 32768


def main():
    results = []
    # Re-rendering does not silently move the comparison baseline from V4 to V5.
    dest = ROOT / "Validation/ImpactAudioV5Review.json"
    history = {r["file"]: r for r in json.loads(dest.read_text()).get("assets", [])} if dest.exists() else {}
    for surface in SURFACES:
        for index in range(3):
            path = ROOT / f"Assets/IntercomMVP/Content/Shared/Audio/Impacts/Impact_{surface.title()}_{index+1:02d}.wav"
            assert path.with_suffix(".wav.meta").exists(), "This generator preserves existing imported assets."
            before = path.read_bytes()
            with wave.open(str(path), "rb") as old:
                previous = np.frombuffer(old.readframes(old.getnframes()), dtype="<i2") / 32768
            x = write_wave(path, impact(index, surface))
            results.append({"file": path.relative_to(ROOT).as_posix(), "surface": surface,
                            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                            "previousSha256": history.get(path.relative_to(ROOT).as_posix(), {}).get("previousSha256", hashlib.sha256(before).hexdigest()),
                            "previous": history.get(path.relative_to(ROOT).as_posix(), {}).get("previous", inspect_wave(previous)), **inspect_wave(x)})
    # Keep the generic fallback consistent with the authored concrete bank.
    for index in range(2):
        write_wave(ROOT / f"Assets/IntercomMVP/Content/Shared/Audio/Impacts/Impact_{index+1:02d}.wav", impact(index))
    report = {"revision": "V5-original-material-impacts", "source": "Original deterministic synthesis in Tools/GenerateImpactAudio.py; no external samples.",
              "mix": {"previousImpactGain": .70, "impactGain": .42, "gainChangeDb": round(20*math.log10(.42/.70), 2)},
              "scope": "PCM analysis, not Unity/device listening approval.", "assets": results}
    dest = ROOT / "Validation/ImpactAudioV5Review.json"
    dest.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({"assets": len(results) + 2, "report": str(dest)}))


if __name__ == "__main__":
    main()
