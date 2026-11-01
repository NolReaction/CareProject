#!/usr/bin/env python3
"""Generate original CareProject prototype audio. Requires Python 3 + numpy.

No recordings, samples, external sounds, models, or network access are used.
Run from any directory: python Tools/GeneratePrototypeAudio.py
This regenerates only this script's named WAV files and their stable Unity .meta.
"""
from pathlib import Path
import hashlib
import json
import math
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
    """A compact comic pop with a falling electronic chirp and a small action tick."""
    rng = np.random.default_rng(51000 + index)
    t = np.arange(round(FX_RATE * .30)) / FX_RATE
    pitch = (1.0, .965, 1.045)[index]
    crack = noise(rng, len(t), FX_RATE, 600, 6200, -.20) * envelope(t, .00045, .0045)
    body_phase = 2 * math.pi * pitch * (102 * t + 136 * .013 * (1 - np.exp(-t / .013)))
    body = (np.sin(body_phase) + .17 * np.sin(2 * body_phase)) * envelope(t, .0010, .031)
    # The short falling tone reads as a toy/mechanical blaster without a long whistle.
    chirp_phase = 2 * math.pi * pitch * (245 * t + 730 * .009 * (1 - np.exp(-t / .009)))
    chirp = np.sin(chirp_phase) * envelope(t, .0008, .025)
    pressure = noise(rng, len(t), FX_RATE, 160, 2600, -.35) * envelope(t, .001, .021)
    delay = .045 + .002 * index
    late = np.maximum(t - delay, 0)
    tick = noise(rng, len(t), FX_RATE, 900, 3400) * envelope(late, .001, .006) * (late > 0)
    x = .28 * crack + .58 * body + .22 * chirp + .20 * pressure + .045 * tick
    return finish_hit(x, FX_RATE, 10 ** (-3.5 / 20))


def boot(index):
    """A short rubber-sole thock, with a quiet spring/servo return."""
    rng = np.random.default_rng(62000 + index)
    t = np.arange(round(FX_RATE * .17)) / FX_RATE
    pitch = (1.0, .94, 1.055, .98)[index]
    phase = 2 * math.pi * pitch * (95 * t + 92 * .010 * (1 - np.exp(-t / .010)))
    thud = np.sin(phase) * envelope(t, .0015, .020)
    knock = np.sin(2 * math.pi * 265 * pitch * t) * envelope(t, .001, .012)
    sole = noise(rng, len(t), FX_RATE, 110, 1500, -.35) * envelope(t, .001, .016)
    scuff = noise(rng, len(t), FX_RATE, 650, 2200) * envelope(t, .003, .018)
    late = np.maximum(t - .027, 0)
    servo_phase = 2 * math.pi * pitch * (420 * late + 190 * .012 * (1 - np.exp(-late / .012)))
    servo = np.sin(servo_phase) * envelope(late, .003, .016) * (late > 0)
    x = .52 * thud + .13 * knock + .24 * sole + .04 * scuff + .028 * servo
    return finish_hit(x, FX_RATE, 10 ** (-7.0 / 20))


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
    out *= 10 ** (-26.0 / 20) / np.sqrt(np.mean(out ** 2))
    assert np.max(np.abs(out)) < .8
    return out


def importer(path, stereo, ambient):
    # Short PCM hits stay cheap and exact; the one ambient voice uses Vorbis.
    # See UnityCsReference AudioCompressionFormat and AudioClipLoadType enums.
    relative = path.relative_to(ROOT).as_posix()
    return f"""fileFormatVersion: 2
guid: {guid(relative)}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: {1 if ambient else 0}
    sampleRateSetting: 0
    sampleRateOverride: {AMBIENT_RATE if ambient else FX_RATE}
    compressionFormat: {1 if ambient else 0}
    quality: {0.7 if ambient else 1}
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: {0 if stereo else 1}
  normalize: 0
  loadInBackground: 0
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


def main():
    AUDIO.mkdir(parents=True, exist_ok=True)
    metrics = [save(f"Pistol_{i + 1:02d}.wav", pistol(i), FX_RATE) for i in range(3)]
    metrics += [save(f"BootStep_{i + 1:02d}.wav", boot(i), FX_RATE) for i in range(4)]
    metrics += [save(f"Impact_{i + 1:02d}.wav", impact(i), FX_RATE) for i in range(2)]
    metrics.append(save("Ambience_IndustrialYard.wav", ambience(), AMBIENT_RATE, True))
    report = {"generator": "Tools/GeneratePrototypeAudio.py", "source": "Original deterministic synthesis; no sampled material", "revision": "cartoon-district-v2",
              "verification": "Decoded PCM numeric checks only; Unity mix/listening on device still required",
              "assets": metrics, "totalWavBytes": sum(m["bytes"] for m in metrics)}
    validation = ROOT / "Validation/PrototypeAudioAssets.json"
    validation.parent.mkdir(parents=True, exist_ok=True)
    validation.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
