#!/usr/bin/env python3
"""Rebuild CareProject audio from the curated CC0 recordings in Source/Audio.

Python 3, NumPy, SciPy and ffmpeg are required. No network access or API key.
The score is an original industrial guitar/bass arrangement; source authors/licences/hashes are
listed in Source/Audio/manifest.json. Existing Unity GUIDs are preserved.
"""
from functools import lru_cache
from pathlib import Path
import argparse
import hashlib
import json
import math
import re
import subprocess
import uuid
import wave
from AssetPaths import asset_path

import numpy as np
from scipy.signal import butter, sosfiltfilt, resample_poly
from fractions import Fraction

ROOT = Path(__file__).resolve().parents[1]
AUDIO = ROOT / "Assets/IntercomMVP/Audio"
SOURCE = ROOT / "Source/Audio"
FX_RATE = 44100
AMBIENT_RATE = 24000


def guid(relative_path):
    meta = ROOT / (relative_path + ".meta")
    if meta.exists():
        existing = re.search(r"^guid: ([a-f0-9]{32})$", meta.read_text(), re.MULTILINE)
        if existing:
            return existing.group(1)
    return uuid.uuid5(uuid.NAMESPACE_URL, "CareProject/audio/v1/" + relative_path).hex


def db(value):
    return 10 ** (value / 20)


def peak(x):
    return max(1e-12, float(np.max(np.abs(x))))


def band(x, low=45, high=9000, rate=FX_RATE):
    return sosfiltfilt(butter(2, [low, high], fs=rate, btype="bandpass", output="sos"), x, axis=0)


def edges(x, attack=.0015, release=.035, rate=FX_RATE):
    x = np.array(x, dtype=np.float64, copy=True)
    a = min(len(x), max(2, round(attack * rate)))
    r = min(len(x), max(2, round(release * rate)))
    shape = (slice(None),) + (None,) * (x.ndim - 1)
    x[:a] *= np.linspace(0, 1, a)[shape]
    x[-r:] *= (np.cos(np.linspace(0, math.pi / 2, r)) ** 2)[shape]
    x[0] = x[-1] = 0
    return x


def rate_shift(x, ratio):
    # Band-limited resampling also prevents aliases when pitching samples up.
    f = Fraction(1 / ratio).limit_denominator(2048)
    return resample_poly(x, f.numerator, f.denominator)


@lru_cache(maxsize=40)
def sample(name):
    data = subprocess.check_output(["ffmpeg", "-v", "fatal", "-i", str(SOURCE / name),
                                    "-ar", str(FX_RATE), "-ac", "1", "-f", "f32le", "pipe:1"])
    x = np.frombuffer(data, dtype="<f4").astype(np.float64)
    if len(x) < 100:
        raise ValueError(f"Empty/truncated source: {name}")
    return x / peak(x)


def onset_trim(x, fraction=.025, preroll=.0015):
    # 1 ms envelope catches the real start, not just the largest later toe thump.
    env = np.convolve(x * x, np.ones(44) / 44, mode="same") ** .5
    begin = int(np.flatnonzero(env > peak(env) * fraction)[0])
    return x[max(0, begin - round(preroll * FX_RATE)):]


def finish_hit(x, target_db):
    x = np.asarray(x) - np.mean(x)
    x = edges(x)
    return x * db(target_db) / peak(x)


def fit(x, seconds):
    count = round(seconds * FX_RATE)
    return np.pad(x[:count], (0, max(0, count - len(x))))


def pistol(index):
    """Near-field 1911 pressure/body plus PPQ crack, all from real recordings."""
    ratio = (.94, .965, .925)[index]
    main = onset_trim(rate_shift(sample(f"1911_near_{1+index%2}.flac"), ratio), .012, .0005)
    main = fit(band(main, 52, 10000), .37)
    # Preserve the fast pressure transient, then a broader 45-150ms chest/body.
    # Parallel body comes from the same recording; no pitched-down sci-fi sweep.
    body = band(main, 65, 920)
    body /= peak(body)
    t = np.arange(len(main)) / FX_RATE
    body *= np.exp(-np.maximum(t-.025, 0)/.095)
    crack = onset_trim(sample(f"PPQ_shot_{1+index%2}.flac"), .018, .0005)
    crack = fit(band(crack, 1800, 10400), .37)
    crack /= peak(crack)
    crack *= np.exp(-t/.027)
    main /= peak(main)
    x = .82*main + .34*body + .28*crack
    x *= np.exp(-np.maximum(t-.055, 0)/.145)
    # Parallel saturation retains the original crest while adding low-mid energy.
    x = .25*x + .75*np.tanh(2.8*x)/2.8
    return finish_hit(edges(x, .00035, .045), -2.4)


BASE_STEPS = ("Fantozzi-StoneL1.flac", "Fantozzi-StoneR1.flac",
              "Fantozzi-StoneL2.flac", "Fantozzi-StoneR2.flac")
GRAVEL_STEPS = ("Fantozzi-SandL1.flac", "Fantozzi-SandR2.flac", "Fantozzi-SandL3.flac")


def boot(index, surface="asphalt"):
    """Recorded sole and grit with identifiable, short material responses."""
    granular = surface in ("gravel", "soil")
    file = GRAVEL_STEPS[index % 3] if granular else BASE_STEPS[index % 4]
    ratio = (1.05, 1.00, 1.08)[index % 3] if surface == "concrete" else (.88, .91, .87, .93)[index % 4]
    x = rate_shift(onset_trim(sample(file), fraction=.018), ratio)
    length = .185 if surface == "concrete" else .245 if granular else .225
    x = fit(x, length)
    x = band(x, 130 if surface == "concrete" else 90,
             6800 if surface == "concrete" else 5700 if surface == "gravel" else 1900 if surface == "soil" else 3600)
    t = np.arange(len(x)) / FX_RATE
    x *= np.exp(-np.maximum(t-(.025 if surface == "concrete" else .065), 0)
                /(.038 if surface == "concrete" else .07 if surface == "soil" else .09))
    x /= peak(x)
    if surface == "metal":
        response = fit(rate_shift(onset_trim(sample("metal.flac")), .58+.025*index), length)
        response = band(response, 170, 5100)*np.exp(-t/.063)
        x = .67*x + .48*response/peak(response)
    elif surface == "wood":
        response = fit(rate_shift(onset_trim(sample("wood_body.flac")), .43+.025*index), length)
        response = band(response, 130, 1900)*np.exp(-t/.044)
        x = .65*x + .48*response/peak(response)
    elif surface == "soil":
        x = band(x, 80, 1650)
    x = np.tanh(x/peak(x)*1.65)
    return finish_hit(x, -10.0 if surface != "soil" else -11.0)


def impact(index, surface="concrete"):
    # V5: share the standalone original material renderer; never recreate the
    # old short, similar foley transients when rebuilding the full audio set.
    from GenerateImpactAudio import impact as material_impact
    return material_impact(index, surface)


def ambience():
    """Subtle, periodic outdoor air, with no loud looping machinery or tonal whistle."""
    rate = AMBIENT_RATE
    count = rate * 20
    rng = np.random.default_rng(98377)
    t = np.arange(count) / rate
    f = np.fft.rfftfreq(count, 1 / rate)
    colour = (1 - np.exp(-(f / 110) ** 4)) * np.exp(-(f / 1900) ** 2) / np.maximum(f, 110) ** .7
    sound = np.fft.irfft(np.fft.rfft(rng.normal(size=(count, 2)), axis=0) * colour[:, None], n=count, axis=0)
    swell = .85 + .10 * np.sin(2 * math.pi * t / 20) + .05 * np.sin(4 * math.pi * t / 20 + .8)
    sound *= swell[:, None]
    sound -= np.mean(sound, axis=0)
    sound *= db(-35) / math.sqrt(np.mean(sound * sound))
    return sound


def importer(path, stereo, ambient=False, music_clip=False):
    relative = path.relative_to(ROOT).as_posix()
    return f"""fileFormatVersion: 2
guid: {guid(relative)}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: {2 if music_clip else 1 if ambient else 0}
    sampleRateSetting: 0
    sampleRateOverride: {AMBIENT_RATE if ambient else FX_RATE}
    compressionFormat: {1 if ambient or music_clip else 0}
    quality: {0.8 if ambient or music_clip else 1}
    conversionMode: 0
    preloadAudioData: {0 if music_clip else 1}
  platformSettingOverrides: {{}}
  forceToMono: {0 if stereo else 1}
  normalize: 0
  loadInBackground: {1 if music_clip else 0}
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def metrics(path, data, rate):
    return {"file":path.relative_to(ROOT).as_posix(), "guid":guid(path.relative_to(ROOT).as_posix()),
            "seconds":len(data) / rate, "sampleRate":rate, "channels":1 if data.ndim == 1 else data.shape[1],
            "peakDbFS":round(20 * math.log10(peak(data)), 3), "rmsDbFS":round(20 * math.log10(max(1e-12, math.sqrt(np.mean(data * data)))), 3),
            "clippedSamples":int(np.sum(np.abs(data) >= 1)), "dcOffset":float(np.max(np.abs(np.mean(data, axis=0)))),
            "boundaryStep":float(np.max(np.abs(data[-1] - data[0]))), "internalStep99Percentile":float(np.quantile(np.abs(np.diff(data, axis=0)), .99)),
            "bytes":path.stat().st_size, "sha256":hashlib.sha256(path.read_bytes()).hexdigest()}


def save(name, data, rate, ambient=False):
    path = asset_path(ROOT, "Assets/IntercomMVP/Audio/" + name)
    path.parent.mkdir(parents=True, exist_ok=True)
    assert np.all(np.isfinite(data)) and peak(data) < 1
    pcm = np.rint(data * 32767).astype("<i2")
    with wave.open(str(path), "wb") as wav:
        wav.setnchannels(1 if pcm.ndim == 1 else pcm.shape[1])
        wav.setsampwidth(2)
        wav.setframerate(rate)
        wav.writeframes(pcm.tobytes())
    path.with_suffix(".wav.meta").write_text(importer(path, pcm.ndim == 2, ambient))
    result = metrics(path, pcm.astype(np.float64) / 32768, rate)
    assert result["clippedSamples"] == 0
    if not ambient:
        assert np.all(pcm[0] == 0) and np.all(pcm[-1] == 0)
    return result


def save_music(data):
    from GenerateMainMusic import TITLE, BARS, BPM
    path = AUDIO / "Music_IndustrialGroove.ogg"
    subprocess.run(["ffmpeg", "-v", "error", "-f", "f32le", "-ar", str(FX_RATE), "-ac", "2", "-i", "pipe:0",
                    "-c:a", "libvorbis", "-q:a", "5", "-fflags", "+bitexact", "-flags:a", "+bitexact", "-map_metadata", "-1",
                    "-metadata", "title=" + TITLE, "-metadata", "artist=CareProject", "-y", str(path)],
                   input=data.astype("<f4").tobytes(), check=True)
    path.with_suffix(".ogg.meta").write_text(importer(path, True, music_clip=True))
    raw = subprocess.check_output(["ffmpeg", "-v", "error", "-i", str(path), "-f", "f32le", "-ac", "2", "pipe:1"])
    decoded = np.frombuffer(raw, dtype="<f4").reshape(-1, 2)
    result = metrics(path, decoded, FX_RATE)
    assert len(decoded) == len(data) and result["clippedSamples"] == 0
    assert result["boundaryStep"] < max(.001, result["internalStep99Percentile"])
    result.update(title=TITLE, bars=BARS, bpm=BPM, source="Original v3 arrangement; see Validation/MusicV3Review.json")
    return result


def verify_sources():
    manifest = json.loads((SOURCE / "manifest.json").read_text())
    for item in manifest["recordings"]:
        actual = hashlib.sha256((SOURCE / item["file"]).read_bytes()).hexdigest()
        if actual != item["sha256"]:
            raise ValueError("Source checksum mismatch: " + item["file"])
    return len(manifest["recordings"])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sfx-only", action="store_true", help="Keep the existing music render")
    parser.add_argument("--impacts-only", action="store_true", help="Rebuild only the V5 material hits; preserve music, footsteps and pistols")
    args = parser.parse_args()
    if args.impacts_only:
        from GenerateImpactAudio import main as build_impacts
        build_impacts()
        return
    count = verify_sources()
    AUDIO.mkdir(parents=True, exist_ok=True)
    results = [save(f"Pistol_{i+1:02d}.wav", pistol(i), FX_RATE) for i in range(3)]
    results += [save(f"BootStep_{i+1:02d}.wav", boot(i), FX_RATE) for i in range(4)]
    for surface in ("concrete", "metal", "gravel", "wood", "soil"):
        results += [save(f"BootStep_{surface.title()}_{i+1:02d}.wav", boot(i, surface), FX_RATE) for i in range(3)]
    results += [save(f"Impact_{i+1:02d}.wav", impact(i), FX_RATE) for i in range(2)]
    for surface in ("metal", "wood", "concrete", "soil"):
        results += [save(f"Impact_{surface.title()}_{i+1:02d}.wav", impact(i, surface), FX_RATE) for i in range(3)]
    results.append(save("Ambience_IndustrialYard.wav", ambience(), AMBIENT_RATE, True))
    if not args.sfx_only:
        from GenerateMainMusic import generate_music
        results.append(save_music(generate_music(sample_rate=FX_RATE)))
    report = {"revision":"material-foley-close-pistol-v3", "sourceRecordings":count, "assets":results,
              "scope":"Decoded PCM, timing, clipping and seams. Not Unity engine listening, actual device mix or subjective approval.",
              "sourceManifest":"Source/Audio/manifest.json", "totalAudioBytes":sum(x["bytes"] for x in results)}
    output = ROOT / "Validation/Results/PrototypeAudioAssets.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({"revision":report["revision"], "assets":len(results), "bytes":report["totalAudioBytes"], "clippedSamples":sum(x["clippedSamples"] for x in results)}))


if __name__ == "__main__":
    main()
