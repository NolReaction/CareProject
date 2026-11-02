#!/usr/bin/env python3
"""Rebuild CareProject audio from the curated CC0 recordings in Source/Audio.

Python 3, NumPy, SciPy and ffmpeg are required. No network access or API key.
The score is an original 32-bar arrangement; source authors/licences/hashes are
listed in Source/Audio/manifest.json. Existing Unity GUIDs are preserved.
"""
from functools import lru_cache
from pathlib import Path
import hashlib
import json
import math
import subprocess
import uuid
import wave

import numpy as np
from scipy.signal import butter, sosfiltfilt, resample_poly
from fractions import Fraction

ROOT = Path(__file__).resolve().parents[1]
AUDIO = ROOT / "Assets/IntercomMVP/Audio"
SOURCE = ROOT / "Source/Audio"
FX_RATE = 44100
AMBIENT_RATE = 24000
MUSIC_BPM = 92
MUSIC_BARS = 32


def guid(relative_path):
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


def pistol(index):
    """Recorded PPQ muzzle report with restrained early reflection and mechanical texture."""
    source = sample(f"PPQ_shot_{1 + index % 2}.flac")
    x = rate_shift(source, (1.0, .985, 1.018)[index])
    x = onset_trim(band(x, 95, 9200))[:round(.31 * FX_RATE)]
    t = np.arange(len(x)) / FX_RATE
    # Keep the real pressure transient. Shape only the outdoor decay so repeated
    # volleys remain distinct, with no oscillator pitch dive or 'laser' overtone.
    x *= np.exp(-np.maximum(t - .045, 0) / .090)
    # Moderate static soft limiting gives the recorded body enough weight on
    # a phone speaker without adding an artificial bass oscillator.
    x = np.tanh(x / peak(x) * 3.0)
    return finish_hit(x, -3.2)


BASE_STEPS = ("Fantozzi-StoneL1.flac", "Fantozzi-StoneR1.flac",
              "Fantozzi-StoneL2.flac", "Fantozzi-StoneR2.flac")
GRAVEL_STEPS = ("Fantozzi-SandL1.flac", "Fantozzi-SandR2.flac", "Fantozzi-SandL3.flac")


def boot(index, surface="asphalt"):
    """Real shoe contact and sole friction; floor material changes its texture."""
    file = GRAVEL_STEPS[index % 3] if surface == "gravel" else BASE_STEPS[index % 4]
    x = onset_trim(sample(file), fraction=.018)
    x = rate_shift(x, (.88, .91, .87, .93)[index % 4])
    length = round((.28 if surface == "gravel" else .235) * FX_RATE)
    x = np.pad(x[:length], (0, max(0, length - len(x))))
    x = band(x, 85, 4700 if surface == "gravel" else 3400)
    t = np.arange(len(x)) / FX_RATE
    # Suppress late shoe drag; the event is one landing, not a whole stride.
    x *= np.exp(-np.maximum(t - .10, 0) / .095)
    x /= peak(x)
    if surface == "metal":
        plate = rate_shift(onset_trim(sample("metal.flac")), .68 + .035 * index)
        plate = band(plate, 145, 4200)[:length]
        plate = np.pad(plate, (0, length - len(plate)))
        plate *= np.exp(-t / .050)
        plate /= peak(plate)
        x = .78 * x + .30 * plate
    # Round the very high crest factor of close-miked foley so its texture
    # survives quiet playback instead of becoming a single tiny click.
    x = np.tanh(x / peak(x) * (3.5 if surface == "gravel" else 2.0))
    return finish_hit(x, -9.8 if surface == "gravel" else -9.0)


def impact(index):
    plate = onset_trim(sample("metal.flac"))
    plate = rate_shift(plate, (1.43, 1.72)[index])[:round(.17 * FX_RATE)]
    plate = band(plate, 280, 6800)
    t = np.arange(len(plate)) / FX_RATE
    plate *= np.exp(-t / .032)
    # Real metal source, quickly damped so hits do not create a pitched bell bed.
    return finish_hit(plate, -11.5)


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


@lru_cache(maxsize=180)
def piano(midi, length, bass=False):
    # The source producer uses C3 for concert C4. The root notes below were
    # verified against the actual recording frequencies, not inferred by name.
    name, root = ("piano_bass.flac", 48) if midi < 53 else ("piano_low.flac", 60) if midi < 67 else ("piano_mid.flac", 72)
    x = rate_shift(sample(name), 2 ** ((midi - root) / 12))
    x = band(x, 35 if bass else 70, 2400 if bass else 7200)
    # A sampled acoustic decay replaces the previous sine/FM 'toy tine'.
    x = x[:round((length + .38) * FX_RATE)]
    t = np.arange(len(x)) / FX_RATE
    x *= np.exp(-np.maximum(t - length, 0) / (.060 if bass else .13))
    if bass:
        x *= np.exp(-t / .65)
    return edges(x, .002, .08) / peak(x)


@lru_cache(maxsize=64)
def mallet(midi, length):
    # Warm wooden marimba used as a sparse answering voice, never a constant arpeggio.
    x = rate_shift(sample("marimba.flac"), 2 ** ((midi - 72 - .04) / 12))
    x = band(x, 130, 4000)[:round((length + .25) * FX_RATE)]
    t = np.arange(len(x)) / FX_RATE
    x *= np.exp(-np.maximum(t - length, 0) / .10)
    return edges(x, .002, .06) / peak(x)


@lru_cache(maxsize=8)
def drum(kind):
    x = onset_trim(sample(kind + ".flac"), fraction=.012)
    high = {"kick": 1800, "rim": 4100, "snare": 6500, "shaker_up": 7800, "shaker_down": 7800}[kind]
    x = band(x, 38 if kind == "kick" else 170, high)
    length = {"kick": .22, "rim": .13, "snare": .22, "shaker_up": .105, "shaker_down": .145}[kind]
    x = x[:round(length * FX_RATE)]
    t = np.arange(len(x)) / FX_RATE
    x *= np.exp(-np.maximum(t - .022, 0) / (.075 if kind == "kick" else .09))
    return edges(x, .001, .025) / peak(x)


def music():
    """Morning Shift: original 32-bar piano / wooden mallet / live percussion score."""
    beat = 60 / MUSIC_BPM
    count = round(FX_RATE * beat * 4 * MUSIC_BARS)
    mix = np.zeros((count, 2), dtype=np.float64)
    rng = np.random.default_rng(2026110207)

    def add(x, when, gain, pan=0., human=.0):
        start = round((when + rng.uniform(-human, human)) * FX_RATE)
        indices = (np.arange(len(x)) + start) % count
        gains = np.array([math.sqrt((1 - pan) / 2), math.sqrt((1 + pan) / 2)])
        mix[indices] += x[:, None] * gains * gain

    # Two related 16-bar periods: harmonic movement and arrangement change avoid
    # a four-bar ringtone. Open voicings leave the shot/footstep midrange free.
    chords = {
        "Dm": ([53, 57, 60, 64], 38), "Bb": ([53, 57, 60, 62], 34),
        "F": ([53, 57, 60, 67], 41), "C": ([52, 55, 60, 62], 36),
        "Gm": ([53, 57, 58, 62], 43), "Asus": ([55, 57, 62, 64], 33),
        "A": ([55, 57, 61, 64], 33),
    }
    order = ["Dm", "Dm", "Bb", "Bb", "F", "F", "C", "C", "Gm", "Gm", "Dm", "Dm", "Bb", "Bb", "Asus", "A",
             "Dm", "Dm", "F", "F", "Bb", "Bb", "C", "C", "Gm", "Gm", "Bb", "Bb", "Dm", "Dm", "Asus", "A"]
    melody = {
        1: [(1.5, 69, .6), (2.5, 74, .65), (3.5, 76, .35)],
        3: [(0.5, 77, .75), (2, 76, .5), (3.25, 74, .6)],
        5: [(1, 72, .7), (2.5, 69, .9)],
        7: [(1.5, 67, .65), (2.5, 64, .4), (3.25, 67, .5)],
        9: [(1.5, 70, .75), (3, 69, .8)],
        11: [(0.5, 69, .4), (1.5, 72, .5), (2.75, 74, .9)],
        13: [(1, 77, .8), (2.75, 74, .65)],
        15: [(1, 73, .7), (2.5, 71, .6), (3.5, 69, .35)],
        19: [(1, 72, .7), (2.5, 76, 1)],
        23: [(1.5, 67, .7), (3, 64, .8)],
        25: [(0.5, 74, .6), (1.5, 77, .5), (3, 76, .6)],
        27: [(1, 74, .7), (2.5, 72, .8)],
        29: [(1, 69, .65), (2.25, 72, .6), (3.25, 74, .8)],
        31: [(1, 73, .5), (2, 71, .5), (3, 69, .5)],
    }
    for bar, key in enumerate(order):
        chord, root = chords[key]
        start = 4 * bar * beat
        breakdown = 16 <= bar < 20
        # Broken/open voicings and naturally overlapping sampled tails.
        chord_gain = .15 if breakdown else .19
        for j, note in enumerate(chord):
            add(piano(note, 1.5 * beat), start + j * .019, chord_gain * rng.uniform(.85, 1.03), -.28 + j * .15, .006)
        if bar % 2 == 0 and not breakdown:
            for j, note in enumerate(chord[1:]):
                add(piano(note + (12 if bar >= 24 else 0), .55 * beat), start + 2.65 * beat + j * .016, .075, .10, .005)
        add(piano(root, .65 * beat, True), start, .25 if not breakdown else .15, -.02, .003)
        if not breakdown:
            add(piano(root + 7, .38 * beat, True), start + 2.5 * beat, .14, -.02, .006)
            if bar % 4 == 3:
                next_root = chords[order[(bar + 1) % len(order)]][1]
                add(piano(next_root + 12, .20 * beat, True), start + 3.5 * beat, .075, -.02, .004)
        for at, note, length in melody.get(bar, []):
            add(piano(note, length * beat), start + at * beat, .15 * rng.uniform(.85, 1.04), .22, .009)
        if bar in (4, 8, 12, 20, 24, 28):
            add(mallet(chord[-1] + 12, .5 * beat), start + 3.25 * beat, .035, -.35, .004)
        # No piercing hi-hat or synthesized blips: quiet recorded skin and shaker.
        if not breakdown:
            add(drum("kick"), start, .28)
            add(drum("kick"), start + (2.5 if bar % 2 == 0 else 2.75) * beat, .17)
            for at in (1.025, 3.02):
                add(drum("snare"), start + at * beat, .066 if bar < 8 else .085, .10, .006)
                add(drum("rim"), start + at * beat, .026, .10)
            for j in range(8):
                if bar % 8 == 7 and j > 5:
                    continue
                add(drum("shaker_down" if j % 2 == 0 else "shaker_up"), start + (j / 2 + (.035 if j % 2 else 0)) * beat,
                    .018 if j % 2 else .011, -.30, .008)
            if bar % 8 == 7:
                add(drum("rim"), start + 3.5 * beat, .032, -.05)
        elif bar % 2 == 0:
            add(drum("shaker_down"), start + 3 * beat, .010, -.3)

    # Modest room diffusion, with circular tails rather than a fade-to-silence seam.
    dry = mix.copy()
    for delay, gain in ((.041, .10), (.089, .075), (.163, .048), (.241, .032), (.337, .020)):
        mix += np.roll(dry[:, ::-1], round(delay * FX_RATE), axis=0) * gain
    frequency = np.fft.rfftfreq(count, 1 / FX_RATE)
    curve = (1 - np.exp(-(frequency / 32) ** 4)) * np.exp(-(frequency / 11200) ** 4)
    mix = np.fft.irfft(np.fft.rfft(mix, axis=0) * curve[:, None], n=count, axis=0)
    mix = np.tanh(mix * .85) / .85
    # Leave natural transient headroom; no loudness-war limiter pumping.
    mix *= min(db(-19.5) / math.sqrt(np.mean(mix * mix)), db(-3.5) / peak(mix))
    return mix


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
    path = AUDIO / name
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
    path = AUDIO / "Music_IndustrialGroove.ogg"
    subprocess.run(["ffmpeg", "-v", "error", "-f", "f32le", "-ar", str(FX_RATE), "-ac", "2", "-i", "pipe:0",
                    "-c:a", "libvorbis", "-q:a", "5", "-fflags", "+bitexact", "-flags:a", "+bitexact", "-map_metadata", "-1", "-y", str(path)],
                   input=data.astype("<f4").tobytes(), check=True)
    path.with_suffix(".ogg.meta").write_text(importer(path, True, music_clip=True))
    raw = subprocess.check_output(["ffmpeg", "-v", "error", "-i", str(path), "-f", "f32le", "-ac", "2", "pipe:1"])
    decoded = np.frombuffer(raw, dtype="<f4").reshape(-1, 2)
    result = metrics(path, decoded, FX_RATE)
    assert len(decoded) == len(data) and result["clippedSamples"] == 0
    assert result["boundaryStep"] < max(.001, result["internalStep99Percentile"])
    result.update(title="Morning Shift", bars=MUSIC_BARS, bpm=MUSIC_BPM, source="Original arrangement rendered with credited CC0 real instruments")
    return result


def verify_sources():
    manifest = json.loads((SOURCE / "manifest.json").read_text())
    for item in manifest["recordings"]:
        actual = hashlib.sha256((SOURCE / item["file"]).read_bytes()).hexdigest()
        if actual != item["sha256"]:
            raise ValueError("Source checksum mismatch: " + item["file"])
    return len(manifest["recordings"])


def main():
    count = verify_sources()
    AUDIO.mkdir(parents=True, exist_ok=True)
    results = [save(f"Pistol_{i+1:02d}.wav", pistol(i), FX_RATE) for i in range(3)]
    results += [save(f"BootStep_{i+1:02d}.wav", boot(i), FX_RATE) for i in range(4)]
    for surface in ("metal", "gravel"):
        results += [save(f"BootStep_{surface.title()}_{i+1:02d}.wav", boot(i, surface), FX_RATE) for i in range(3)]
    results += [save(f"Impact_{i+1:02d}.wav", impact(i), FX_RATE) for i in range(2)]
    results.append(save("Ambience_IndustrialYard.wav", ambience(), AMBIENT_RATE, True))
    results.append(save_music(music()))
    report = {"revision":"recorded-foley-morning-shift-v4", "sourceRecordings":count, "assets":results,
              "scope":"Decoded PCM, timing, clipping and seams. Not Unity engine listening, actual device mix or subjective approval.",
              "sourceManifest":"Source/Audio/manifest.json", "totalAudioBytes":sum(x["bytes"] for x in results)}
    output = ROOT / "Validation/Results/PrototypeAudioAssets.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({"revision":report["revision"], "assets":len(results), "bytes":report["totalAudioBytes"], "musicSeconds":results[-1]["seconds"], "clippedSamples":sum(x["clippedSamples"] for x in results)}))


if __name__ == "__main__":
    main()
