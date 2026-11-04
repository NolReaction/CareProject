#!/usr/bin/env python3
"""Deterministic material impacts, including a damped cartoon metal contact.

Only impact WAVs are written. --metal-only updates the three metal contacts.
Metal uses the verified included CC0 brake-drum recording plus original layers.
Music, footsteps and weapon reports are untouched.
Python 3 + NumPy + SciPy + ffmpeg. Existing Unity GUIDs/import settings are preserved.
Run: python Tools/GenerateImpactAudio.py
"""
from pathlib import Path
import argparse
from functools import lru_cache
import hashlib
import json
import math
import subprocess
import wave

import numpy as np
from scipy.signal import butter, sosfilt

ROOT = Path(__file__).resolve().parents[1]
RATE = 44100
SURFACES = ("metal", "wood", "concrete", "soil")


def filtered_noise(rng, size, low, high):
    x = sosfilt(butter(3, [low, high], fs=RATE, btype="bandpass", output="sos"), rng.normal(size=size))
    return x / max(1e-9, np.std(x))


@lru_cache(maxsize=1)
def metal_source():
    """Decode an included, manifest-verified recording; never download assets."""
    path = ROOT / "Source/Audio/metal.flac"
    manifest = json.loads((path.parent / "manifest.json").read_text())
    entry = next(item for item in manifest["recordings"] if item["file"] == path.name)
    if entry["license"] != "CC0-1.0" or hashlib.sha256(path.read_bytes()).hexdigest() != entry["sha256"]:
        raise ValueError("The included metal recording does not match its source manifest")
    decoded = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-f", "f32le",
                              "-ac", "1", "-ar", str(RATE), "pipe:1"],
                             capture_output=True, check=True).stdout
    x = np.frombuffer(decoded, dtype="<f4").astype(float)
    if len(x) < RATE // 10 or not np.isfinite(x).all():
        raise ValueError("Invalid metal source recording")
    onset = np.flatnonzero(np.abs(x) >= np.max(np.abs(x)) * .018)
    start = max(0, int(onset[0]) - round(.0005 * RATE))
    return x[start:]


def metal_impact(index):
    """Short mechanical clonk with a rounded contact and a tiny latch rebound.

    The recording supplies actual irregular metal texture. Low non-harmonic
    body modes and a small dry click read as cartoon machinery; long plate
    ringing and high sizzling energy are deliberately damped away.
    """
    index %= 3
    rng = np.random.default_rng(151104 + 137 * index)
    t = np.arange(round((.152, .160, .148)[index] * RATE)) / RATE
    shift = (.96, 1.045, 1.0)[index]
    sample = metal_source()
    position = np.arange(len(t)) * (.94, 1.035, .985)[index]
    recorded = np.interp(position, np.arange(len(sample)), sample, left=0, right=0)
    recorded = sosfilt(butter(3, [130, 2100], fs=RATE, btype="bandpass", output="sos"), recorded)
    recorded /= max(1e-12, np.max(np.abs(recorded)))
    recorded *= np.exp(-np.maximum(t - .005, 0) / (.014, .015, .013)[index])
    rise = 1 - np.exp(-t / .00085)
    # A rounded non-musical body, without a high pitched bell/ricochet whistle.
    phase = 2 * math.pi * shift * (345 * t + 38 * .008 * (1 - np.exp(-t / .008)))
    body = np.sin(phase) * np.exp(-t / .014)
    body += .44 * np.sin(2 * math.pi * 574 * shift * t + .18) * np.exp(-t / .009)
    body += .20 * np.sin(2 * math.pi * 913 * shift * t) * np.exp(-t / .006)
    click = filtered_noise(rng, len(t), 700, 3400) * np.exp(-t / .0026)
    x = (1.08 * recorded + .57 * body + .11 * click) * rise
    when = (.022, .026, .019)[index]
    a = np.maximum(0, t - when)
    rebound = np.where(t >= when, (1 - np.exp(-a / .0007)) * np.exp(-a / .0045), 0)
    x += .075 * filtered_noise(rng, len(t), 250, 1500) * rebound
    x -= np.mean(x)
    x[:round(.0006 * RATE)] *= np.linspace(0, 1, round(.0006 * RATE))
    release = round(.016 * RATE)
    x[-release:] *= np.cos(np.linspace(0, math.pi / 2, release)) ** 2
    x[0] = x[-1] = 0
    peak = 10 ** (-10.7 / 20)
    x *= peak / max(1e-12, np.max(np.abs(x)))
    rms_ceiling = 10 ** (-26.6 / 20)
    x *= min(1, rms_ceiling / max(1e-12, np.sqrt(np.mean(x*x))))
    return x

def impact(index, surface="concrete"):
    """A recognisable body AFTER the contact, not four versions of one crack.

    Metal: recorded short clonk; wood: short hollow low-mid knock;
    concrete: unpitched grit with two tiny debris contacts; soil: soft dirt puff.
    Nothing rings indefinitely, whistles, or contains a sharp ultrasonic spike.
    """
    if surface not in SURFACES:
        raise ValueError(surface)
    if surface == "metal":
        return metal_impact(index)
    index %= 3
    rng = np.random.default_rng(51071 + 997 * SURFACES.index(surface) + index)
    length = {"wood": .155, "concrete": .145, "soil": .17}[surface]
    t = np.arange(round(length * RATE)) / RATE
    shift = (.975, 1.035, 1.0)[index]
    rise = 1 - np.exp(-t / .0011)
    if surface == "wood":
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
    rms_ceiling = 10 ** (-24.5 / 20)
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


def main(argv=()):
    # Imported callers (GeneratePrototypeAudio --impacts-only) have their own
    # command-line flags; only the standalone entry point consumes sys.argv.
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--metal-only", action="store_true", help="Update only the three metal-impact WAVs")
    args = parser.parse_args(argv)
    surfaces = ("metal",) if args.metal_only else SURFACES
    results = []
    dest = ROOT / "Validation/Reports/MetalImpactAudio.json"
    history = {row["file"]: row for row in json.loads(dest.read_text()).get("assets", [])} if dest.exists() else {}
    for surface in surfaces:
        for index in range(3):
            path = ROOT / f"Assets/IntercomMVP/Content/Shared/Audio/Impacts/Impact_{surface.title()}_{index+1:02d}.wav"
            meta = path.with_suffix(".wav.meta")
            if not meta.is_file():
                raise ValueError("This generator preserves existing imported assets: " + str(path))
            before = path.read_bytes(); meta_hash = hashlib.sha256(meta.read_bytes()).hexdigest()
            with wave.open(str(path), "rb") as old:
                previous = np.frombuffer(old.readframes(old.getnframes()), dtype="<i2") / 32768
            x = write_wave(path, impact(index, surface))
            if hashlib.sha256(meta.read_bytes()).hexdigest() != meta_hash:
                raise AssertionError("Audio importer changed")
            identity = path.relative_to(ROOT).as_posix()
            results.append({"file": identity, "surface": surface,
                            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                            "previousSha256": history.get(identity, {}).get("previousSha256", hashlib.sha256(before).hexdigest()),
                            "previous": history.get(identity, {}).get("previous", inspect_wave(previous)),
                            "importerSha256": meta_hash, **inspect_wave(x)})
    if not args.metal_only:
        for index in range(2):
            write_wave(ROOT / f"Assets/IntercomMVP/Content/Shared/Audio/Impacts/Impact_{index+1:02d}.wav", impact(index))
    report = {"revision": "cartoon-metal-contact-v15", "metalOnly": args.metal_only,
              "source": "Included verified CC0 VCSL brake-drum contact plus original deterministic mechanical layers. Other material banks retain their original synthesis.",
              "sourceSha256": hashlib.sha256((ROOT / "Source/Audio/metal.flac").read_bytes()).hexdigest(),
              "scope": "PCM analysis and existing GUID/importer preservation; not Unity/device listening approval.", "assets": results}
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({"assets": len(results) + (0 if args.metal_only else 2), "report": str(dest)}))


if __name__ == "__main__":
    main(None)
