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
    rng = np.random.default_rng(51000 + index)
    t = np.arange(round(FX_RATE * .38)) / FX_RATE
    pitch = (1.0, .94, 1.055)[index]
    crack = noise(rng, len(t), FX_RATE, 700, 10500) * envelope(t, .00018, .0046)
    body_noise = noise(rng, len(t), FX_RATE, 180, 4600, -.20) * envelope(t, .0005, .027)
    # Short falling body resonance, with no sustained laser-like whistle.
    phase = 2 * math.pi * pitch * (78 * t + 132 * .014 * (1 - np.exp(-t / .014)))
    body = np.sin(phase) * envelope(t, .0008, .044)
    ring = np.zeros_like(t)
    for frequency, amplitude, decay in [(720, .11, .024), (1830, .065, .018), (2870, .035, .010)]:
        ring += amplitude * np.sin(2 * math.pi * frequency * pitch * t) * envelope(t, .0003, decay)
    # A quiet action/chamber tick belongs to the shot instead of a giant echo.
    delay = .053 + .003 * index
    tick_t = np.maximum(t - delay, 0)
    tick = noise(rng, len(t), FX_RATE, 1100, 6500) * envelope(tick_t, .0006, .009) * (t > delay)
    tail = noise(rng, len(t), FX_RATE, 240, 2400, -.25) * envelope(t, .003, .062)
    x = .50 * crack + .31 * body_noise + .63 * body + ring + .07 * tick + .055 * tail
    return finish_hit(x, FX_RATE, 10 ** (-2.5 / 20))


def boot(index):
    rng = np.random.default_rng(62000 + index)
    t = np.arange(round(FX_RATE * .23)) / FX_RATE
    pitch = (1.0, .93, 1.06, .98)[index]
    phase = 2 * math.pi * pitch * (84 * t + 90 * .012 * (1 - np.exp(-t / .012)))
    thud = np.sin(phase) * envelope(t, .0015, .024)
    sole = noise(rng, len(t), FX_RATE, 90, 1150, -.15) * envelope(t, .001, .023)
    scuff = noise(rng, len(t), FX_RATE, 650, 3300) * envelope(t, .003, .032)
    clack = np.zeros_like(t)
    for frequency, amplitude in [(830, .055), (1390, .038), (2310, .016)]:
        clack += amplitude * np.sin(2 * math.pi * frequency * pitch * t) * envelope(t, .0004, .013)
    # Small heel/ankle return: short, below the main sole impact.
    late = np.maximum(t - .038 - .002 * index, 0)
    return_tick = noise(rng, len(t), FX_RATE, 850, 2400) * envelope(late, .002, .013) * (late > 0)
    x = .46 * thud + .24 * sole + .070 * scuff + clack + .022 * return_tick
    return finish_hit(x, FX_RATE, 10 ** (-6.0 / 20))


def ambience():
    rate = AMBIENT_RATE
    duration = 8
    count = rate * duration
    t = np.arange(count) / rate
    rng = np.random.default_rng(73000)
    # FFT noise and every modulation/oscillator have integer periods in 8 s.
    # Thus looping has ordinary adjacent samples, with no splice or fade hole.
    common = noise(rng, count, rate, 45, 1900, -.65)
    left = noise(rng, count, rate, 70, 3200, -.55)
    right = noise(rng, count, rate, 70, 3200, -.55)
    wind_gain = .70 + .13 * np.sin(2 * math.pi * t / duration + .4) + .06 * np.sin(4 * math.pi * t / duration - .7)
    fan_mod = 1.0 + .09 * np.sin(6 * math.pi * t / duration)
    hum = (.13 * np.sin(2 * math.pi * 52 * t + .10 * np.sin(2 * math.pi * t / duration))
           + .045 * np.sin(2 * math.pi * 104 * t)
           + .025 * np.sin(2 * math.pi * 208 * t)) * fan_mod
    out = np.column_stack((.065 * wind_gain * (.78 * common + .30 * left) + .085 * hum,
                           .065 * wind_gain * (.78 * common + .30 * right) + .085 * hum))
    out -= np.mean(out, axis=0)
    out *= 10 ** (-24.0 / 20) / np.sqrt(np.mean(out ** 2))
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
    metrics.append(save("Ambience_IndustrialYard.wav", ambience(), AMBIENT_RATE, True))
    report = {"generator": "Tools/GeneratePrototypeAudio.py", "source": "Original deterministic synthesis; no sampled material",
              "verification": "Decoded PCM numeric checks only; Unity mix/listening on device still required",
              "assets": metrics, "totalWavBytes": sum(m["bytes"] for m in metrics)}
    validation = ROOT / "Validation/PrototypeAudioAssets.json"
    validation.parent.mkdir(parents=True, exist_ok=True)
    validation.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
