#!/usr/bin/env python3
"""Generate original CareProject prototype audio. Requires Python 3, numpy, ffmpeg.

No recordings, samples, external sounds, models, or network access are used.
Run from any directory: python Tools/GeneratePrototypeAudio.py
This regenerates only this script's named WAV/OGG files and their stable Unity .meta.
"""
from pathlib import Path
import hashlib
import json
import math
import subprocess
import uuid
import wave

import numpy as np


ROOT = Path(__file__).resolve().parents[1]
AUDIO = ROOT / "Assets/IntercomMVP/Audio"
FX_RATE = 44100
AMBIENT_RATE = 24000


def guid(relative_path):
    return uuid.uuid5(uuid.NAMESPACE_URL, "CareProject/audio/v1/" + relative_path).hex


def noise(rng, count, rate, low, high, tilt=0.0):
    """Smooth band-limited, periodic noise with unit RMS; no filter startup edge."""
    frequencies = np.fft.rfftfreq(count, 1.0 / rate)
    f = np.maximum(frequencies, 1.0)
    shape = (1.0 - np.exp(-(f / low) ** 4)) * np.exp(-(f / high) ** 4)
    shape *= (f / low) ** tilt
    spectrum = np.fft.rfft(rng.standard_normal(count)) * shape
    spectrum[0] = 0
    result = np.fft.irfft(spectrum, n=count)
    return result / max(1e-12, np.sqrt(np.mean(result * result)))


def envelope(t, attack, decay):
    return (1.0 - np.exp(-t / attack)) * np.exp(-t / decay)


def finish_hit(x, rate, peak):
    # The endpoints are genuinely silent, including after DC removal.
    x = x - np.mean(x)
    x *= np.minimum(np.arange(len(x)) / max(1, int(rate * .0005)), 1.0)
    fade = min(int(rate * .025), len(x))
    x[-fade:] *= np.sin(np.linspace(math.pi / 2.0, 0, fade)) ** 2
    x[0] = x[-1] = 0.0
    x = np.tanh(x * .65)  # Gentle body saturation; never hard clipping.
    return x * peak / max(np.max(np.abs(x)), 1e-12)


def pistol(index):
    """Weighted dry pop plus short action-clack; no pitch sweep / laser chirp."""
    rng = np.random.default_rng(151000 + index)
    t = np.arange(round(FX_RATE * .24)) / FX_RATE
    pitch = (1.0, .982, 1.023)[index]
    crack = noise(rng, len(t), FX_RATE, 680, 5100, -.24) * envelope(t, .00035, .0034)
    pressure = noise(rng, len(t), FX_RATE, 110, 2100, -.48) * envelope(t, .0007, .015)
    # A low, heavily damped cluster adds weight without an audible falling note.
    body = sum(g * np.sin(2 * math.pi * pitch * f * t + p) * envelope(t, .0009, d)
               for g, f, p, d in ((.32, 126, .3, .021), (.24, 189, -.3, .017),
                                  (.17, 293, .1, .012), (.08, 417, .6, .009)))
    opening = np.maximum(t - .026 - .001 * index, 0)
    closure = np.maximum(t - .055 - .002 * index, 0)
    action = noise(rng, len(t), FX_RATE, 430, 2700, -.2)
    tick = action * (.095 * envelope(opening, .0008, .0055) * (opening > 0)
                     + .068 * envelope(closure, .0007, .007) * (closure > 0))
    dry = .43 * crack + .60 * pressure + body + tick
    # Very short, quiet early reflections, never a long room hiss or ringing tail.
    x = dry.copy()
    for seconds, gain in ((.023, .09), (.047, .035)):
        offset = round(seconds * FX_RATE)
        x[offset:] += dry[:-offset] * gain
    return finish_hit(x, FX_RATE, 10 ** (-5.5 / 20))


def boot(index, surface="asphalt"):
    """Heel-to-sole contact: compact rubber on hard ground, metal, or loose grit."""
    rng = np.random.default_rng(162000 + index + {"asphalt": 0, "metal": 200, "gravel": 400}[surface])
    t = np.arange(round(FX_RATE * (.23 if surface == "gravel" else .20))) / FX_RATE
    pitch = (1.0, .965, 1.035, .99)[index]
    thud = (.38 * np.sin(2 * math.pi * 106 * pitch * t)
            + .15 * np.sin(2 * math.pi * 183 * pitch * t)) * envelope(t, .0012, .020)
    sole = noise(rng, len(t), FX_RATE, 100, 1400, -.45) * envelope(t, .0015, .016)
    toe_time = np.maximum(t - .019, 0)
    toe = noise(rng, len(t), FX_RATE, 220, 1700, -.35) * envelope(toe_time, .002, .016) * (toe_time > 0)
    x = thud + .28 * sole + .105 * toe
    if surface == "metal":
        # Unequal panel modes give a short hollow deck response, not a pitched bell.
        for frequency, level, decay in ((361, .105, .032), (713, .050, .023), (1139, .025, .015)):
            x += level * np.sin(2 * math.pi * frequency * pitch * t) * envelope(t, .0008, decay)
        x += .12 * noise(rng, len(t), FX_RATE, 650, 3200, -.35) * envelope(t, .0005, .005)
    elif surface == "gravel":
        grit = noise(rng, len(t), FX_RATE, 550, 3500, -.35)
        granular = np.zeros_like(t)
        for delay, strength in zip(rng.uniform(.006, .080, 7), rng.uniform(.026, .060, 7)):
            u = np.maximum(t - delay, 0)
            granular += strength * envelope(u, .0012, .010) * (u > 0)
        x = .83 * x + grit * (granular + .09 * envelope(t, .003, .035))
    else:
        x += .04 * noise(rng, len(t), FX_RATE, 650, 2400, -.25) * envelope(t, .002, .018)
    return finish_hit(x, FX_RATE, 10 ** (-8.0 / 20))


def impact(index):
    """Bright but soft pock/plink feedback, separate from the gun report."""
    rng = np.random.default_rng(82000 + index)
    t = np.arange(round(FX_RATE * .16)) / FX_RATE
    pitch = (1.0, 1.10)[index]
    pop_phase = 2 * math.pi * pitch * (210 * t + 200 * .008 * (1 - np.exp(-t / .008)))
    pop = np.sin(pop_phase) * envelope(t, .0007, .018)
    tap = noise(rng, len(t), FX_RATE, 400, 3600, -.15) * envelope(t, .0004, .008)
    ring = (.12 * np.sin(2 * math.pi * 1160 * pitch * t)
            + .045 * np.sin(2 * math.pi * 1810 * pitch * t)) * envelope(t, .001, .025)
    return finish_hit(.48 * pop + .18 * tap + ring, FX_RATE, 10 ** (-8 / 20))


def ambience():
    rate = AMBIENT_RATE
    duration = 8
    count = rate * duration
    t = np.arange(count) / rate
    rng = np.random.default_rng(73000)
    # All FFT noise and modulation have integer periods, giving a seamless loop.
    common = noise(rng, count, rate, 90, 1400, -.65)
    left = noise(rng, count, rate, 130, 1800, -.55)
    right = noise(rng, count, rate, 130, 1800, -.55)
    wind_gain = .76 + .10 * np.sin(2 * math.pi * t / duration + .4) + .04 * np.sin(4 * math.pi * t / duration - .7)
    fan_mod = 1.0 + .07 * np.sin(6 * math.pi * t / duration)
    hum = (.12 * np.sin(2 * math.pi * 70 * t + .08 * np.sin(2 * math.pi * t / duration))
           + .04 * np.sin(2 * math.pi * 140 * t)
           + .014 * np.sin(2 * math.pi * 280 * t)) * fan_mod
    out = np.column_stack((.060 * wind_gain * (.82 * common + .26 * left) + .12 * hum,
                           .060 * wind_gain * (.82 * common + .26 * right) + .12 * hum))
    out -= np.mean(out, axis=0)
    # Leave the soundscape to steps and music; this is only a distant room bed.
    out *= 10 ** (-39.0 / 20) / np.sqrt(np.mean(out ** 2))
    assert np.max(np.abs(out)) < .8
    return out


MUSIC_BPM = 96
MUSIC_BARS = 16


def note_frequency(midi):
    return 440.0 * 2.0 ** ((midi - 69) / 12.0)


def key_note(midi, length, velocity=1.0):
    """Soft struck tine with a rounded fundamental and fast-decaying overtones."""
    t = np.arange(round((length + .55) * FX_RATE)) / FX_RATE
    f = note_frequency(midi)
    fundamental = np.sin(2 * math.pi * f * t + .44 * np.exp(-t / .13) * np.sin(2 * math.pi * f * 2 * t))
    modes = (.20 * np.sin(2 * math.pi * f * 2.002 * t) * np.exp(-t / .25)
             + .065 * np.sin(2 * math.pi * f * 3.997 * t) * np.exp(-t / .08))
    release = np.exp(-np.maximum(t - length, 0) / .13)
    env = (1 - np.exp(-t / .006)) * np.exp(-t / 1.05) * release
    return .22 * velocity * (fundamental + modes) * env


def bass_note(midi, length):
    t = np.arange(round((length + .16) * FX_RATE)) / FX_RATE
    f = note_frequency(midi)
    x = (np.sin(2 * math.pi * f * t)
         + .27 * np.sin(2 * math.pi * 2 * f * t) * np.exp(-t / .18)
         + .08 * np.sin(2 * math.pi * 3 * f * t) * np.exp(-t / .07))
    env = (1 - np.exp(-t / .0045)) * np.exp(-t / .50) * np.exp(-np.maximum(t - length, 0) / .045)
    return .21 * x * env


def percussion(kind, rng):
    t = np.arange(round(FX_RATE * .22)) / FX_RATE
    if kind == "kick":
        phase = 2 * math.pi * (54 * t + 47 * .012 * (1 - np.exp(-t / .012)))
        x = (.13 * np.sin(phase) * envelope(t, .0015, .032)
             + .018 * noise(rng, len(t), FX_RATE, 100, 700, -.3) * envelope(t, .001, .008))
    elif kind == "rim":
        x = (.026 * noise(rng, len(t), FX_RATE, 600, 3200, -.3) * envelope(t, .001, .011)
             + .024 * np.sin(2 * math.pi * 472 * t) * envelope(t, .0008, .014)
             + .012 * np.sin(2 * math.pi * 731 * t) * envelope(t, .0008, .010))
    else:
        x = .010 * noise(rng, len(t), FX_RATE, 2600, 6500, -.35) * envelope(t, .002, .014)
    return x


def music():
    """Original 16-bar warm instrumental loop. No samples, pulse/square waves, or bit reduction."""
    beat = 60.0 / MUSIC_BPM
    count = round(FX_RATE * beat * 4 * MUSIC_BARS)
    mix = np.zeros((count, 2), dtype=np.float64)
    rng = np.random.default_rng(20261102)

    def add(sound, when, gain=1.0, pan=0.0):
        # Circular placement preserves ringing tails through the musical loop seam.
        indices = (np.arange(len(sound)) + round(when * FX_RATE)) % count
        gains = np.array([math.sqrt((1 - pan) / 2), math.sqrt((1 + pan) / 2)])
        mix[indices] += sound[:, None] * gains * gain

    chords = [(57, 60, 64, 71), (60, 64, 67, 71), (53, 57, 60, 64), (57, 60, 64, 67),
              (53, 57, 60, 64), (57, 60, 64, 69), (56, 59, 62, 64), (56, 59, 62, 68)]
    roots = [45, 45, 41, 41, 38, 38, 40, 40]
    # Sparse answering phrases leave space for footsteps and gunfire.
    phrases = {
        1: [(2.50, 76, .30), (3.25, 74, .28)],
        3: [(1.75, 72, .42), (3.00, 69, .56)],
        5: [(2.25, 69, .25), (2.90, 72, .30), (3.50, 74, .27)],
        7: [(1.50, 71, .45), (2.75, 68, .42)],
        9: [(1.75, 76, .32), (2.50, 74, .30), (3.25, 72, .32)],
        11: [(2.00, 69, .55), (3.25, 67, .30)],
        13: [(1.75, 69, .40), (2.75, 72, .35)],
        15: [(1.50, 71, .35), (2.50, 68, .45)],
    }
    for bar in range(MUSIC_BARS):
        start = 4 * bar * beat
        chord, root = chords[bar % 8], roots[bar % 8]
        # Loose strumming and alternate off-beat voicings avoid a rigid metronomic bed.
        for j, midi in enumerate(chord):
            add(key_note(midi, .92, .91 + rng.uniform(-.08, .08)), start + .008 * j,
                gain=.68, pan=-.27 + .15 * j)
        if bar % 4 != 3:
            for j, midi in enumerate(chord[1:]):
                add(key_note(midi, .37, .75), start + (2.50 if bar % 2 else 2.75) * beat + .009 * j,
                    gain=.42, pan=.23 - .12 * j)
        add(bass_note(root, .48), start, gain=.78)
        add(bass_note(root + 7, .30), start + 2.55 * beat, gain=.50)
        if bar % 2 == 1:
            target = roots[(bar + 1) % 8]
            add(bass_note(target + 2, .19), start + 3.70 * beat, gain=.32)
        add(percussion("kick", rng), start, gain=.61)
        if bar % 4 != 3:
            add(percussion("kick", rng), start + 2.50 * beat, gain=.33)
        for at in (1.04, 3.03):
            add(percussion("rim", rng), start + at * beat, gain=.65, pan=.13)
        for j, at in enumerate((.56, 1.55, 2.56, 3.54)):
            if bar % 4 == 3 and j > 1:
                continue
            add(percussion("brush", rng), start + at * beat, gain=.52 + .1 * (j % 2), pan=-.22)
        for at, midi, length in phrases.get(bar, []):
            add(key_note(midi, length, .70), start + at * beat, gain=.36, pan=.27)

    # Gentle stereo early reflections; circular delays retain the seamless loop.
    dry = mix.copy()
    for delay, gain in ((.037, .08), (.083, .05), (.149, .025)):
        mix += np.roll(dry[:, ::-1], round(delay * FX_RATE), axis=0) * gain
    # Periodic filtering removes inaudible DC/sub-bass and rounds the highest tine/brush edge.
    f = np.fft.rfftfreq(count, 1 / FX_RATE)
    curve = (1 - np.exp(-(f / 32) ** 4)) * np.exp(-(f / 7000) ** 4)
    mix = np.fft.irfft(np.fft.rfft(mix, axis=0) * curve[:, None], n=count, axis=0)
    mix = np.tanh(mix * 1.12)
    mix *= 10 ** (-5.5 / 20) / np.max(np.abs(mix))
    return mix


def importer(path, stereo, ambient, music_clip=False):
    # Short PCM hits stay cheap and exact; ambience is compressed, music streams.
    # See UnityCsReference AudioCompressionFormat and AudioClipLoadType enums.
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
    sampleRateOverride: {AMBIENT_RATE if ambient and not music_clip else FX_RATE}
    compressionFormat: {1 if ambient or music_clip else 0}
    quality: {0.7 if ambient or music_clip else 1}
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


def save(name, data, rate, ambient=False):
    path = AUDIO / name
    data = np.asarray(data)
    channels = 1 if data.ndim == 1 else data.shape[1]
    pcm = np.rint(data * 32767).astype("<i2")
    with wave.open(str(path), "wb") as wav:
        wav.setnchannels(channels)
        wav.setsampwidth(2)
        wav.setframerate(rate)
        wav.writeframes(pcm.tobytes())
    path.with_suffix(".wav.meta").write_text(importer(path, channels == 2, ambient), encoding="utf-8")
    decoded = pcm.astype(np.float64) / 32768
    changes = np.diff(decoded, axis=0)
    seam = np.max(np.abs(decoded[-1] - decoded[0]))
    metrics = {
        "file": path.relative_to(ROOT).as_posix(), "guid": guid(path.relative_to(ROOT).as_posix()),
        "channels": channels, "sampleRate": rate, "seconds": len(pcm) / rate,
        "peakDbFS": round(20 * math.log10(np.max(np.abs(decoded))), 3),
        "rmsDbFS": round(20 * math.log10(np.sqrt(np.mean(decoded ** 2))), 3),
        "clippedSamples": int(np.sum(np.abs(pcm.astype(np.int32)) >= 32767)),
        "dcOffset": float(np.max(np.abs(np.mean(decoded, axis=0)))),
        "boundaryStep": float(seam),
        "internalStep99Percentile": float(np.quantile(np.abs(changes), .99)),
        "bytes": path.stat().st_size, "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
    }
    assert metrics["clippedSamples"] == 0
    if ambient:
        assert seam < metrics["internalStep99Percentile"]
    else:
        assert np.all(pcm[0] == 0) and np.all(pcm[-1] == 0)
    return metrics


def save_music(data):
    path = AUDIO / "Music_IndustrialGroove.ogg"
    subprocess.run(["ffmpeg", "-v", "error", "-f", "f32le", "-ar", str(FX_RATE), "-ac", "2", "-i", "pipe:0",
                    "-c:a", "libvorbis", "-q:a", "4", "-fflags", "+bitexact", "-flags:a", "+bitexact",
                    "-map_metadata", "-1", "-y", str(path)],
                   input=data.astype("<f4").tobytes(), check=True)
    path.with_suffix(".ogg.meta").write_text(importer(path, True, False, True), encoding="utf-8")
    decoded_bytes = subprocess.check_output(["ffmpeg", "-v", "error", "-i", str(path), "-f", "f32le", "-ac", "2", "pipe:1"])
    decoded = np.frombuffer(decoded_bytes, dtype="<f4").reshape(-1, 2)
    assert len(decoded) == len(data), "Vorbis loop duration changed"
    seam = float(np.max(np.abs(decoded[-1] - decoded[0])))
    step = float(np.quantile(np.abs(np.diff(decoded, axis=0)), .99))
    clipped = int(np.sum(np.abs(decoded) >= 1))
    assert clipped == 0 and seam < step
    return {
        "file": path.relative_to(ROOT).as_posix(), "guid": guid(path.relative_to(ROOT).as_posix()),
        "channels": 2, "sampleRate": FX_RATE, "seconds": len(decoded) / FX_RATE,
        "peakDbFS": round(20 * math.log10(np.max(np.abs(decoded))), 3),
        "rmsDbFS": round(20 * math.log10(np.sqrt(np.mean(decoded ** 2))), 3),
        "clippedSamples": clipped, "dcOffset": float(np.max(np.abs(np.mean(decoded, axis=0)))),
        "boundaryStep": seam, "internalStep99Percentile": step,
        "bytes": path.stat().st_size, "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        "codec": "Vorbis quality 4 source, Unity streaming import", "bars": MUSIC_BARS, "bpm": MUSIC_BPM,
    }


def main():
    AUDIO.mkdir(parents=True, exist_ok=True)
    metrics = [save(f"Pistol_{i + 1:02d}.wav", pistol(i), FX_RATE) for i in range(3)]
    metrics += [save(f"BootStep_{i + 1:02d}.wav", boot(i), FX_RATE) for i in range(4)]
    for surface in ("metal", "gravel"):
        metrics += [save(f"BootStep_{surface.title()}_{i + 1:02d}.wav", boot(i, surface), FX_RATE) for i in range(3)]
    metrics += [save(f"Impact_{i + 1:02d}.wav", impact(i), FX_RATE) for i in range(2)]
    metrics.append(save("Ambience_IndustrialYard.wav", ambience(), AMBIENT_RATE, True))
    metrics.append(save_music(music()))
    report = {"generator": "Tools/GeneratePrototypeAudio.py", "source": "Original deterministic synthesis and composition; no sampled material", "revision": "grounded-groove-v3",
              "verification": "Decoded PCM numeric checks only; Unity mix/listening on device still required",
              "assets": metrics, "totalWavBytes": sum(m["bytes"] for m in metrics if m["file"].endswith(".wav")),
              "totalAudioBytes": sum(m["bytes"] for m in metrics)}
    validation = ROOT / "Validation/Results/PrototypeAudioAssets.json"
    validation.parent.mkdir(parents=True, exist_ok=True)
    validation.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
