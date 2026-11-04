"""Original V10 hero chassis impacts. Deterministic mono PCM; no external recordings."""
from pathlib import Path
import json
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
RATE = 44100
OUT = ROOT / "Assets/IntercomMVP/Content/Characters/Robot/Audio"


def make_hit(index):
    rng = np.random.default_rng(7010 + index)
    n = int(RATE * 0.34)
    t = np.arange(n) / RATE
    # Low rubber-backed chassis knock. A few damped partials provide metal character
    # without a piercing high-frequency bell, siren, or synthetic voice.
    fundamental = 126 + index * 9
    attack = 1 - np.exp(-t / .0028)
    phase = 2 * np.pi * (fundamental * t + 23 * .026 * (1 - np.exp(-t / .026)))
    body = .67 * np.sin(phase) * np.exp(-t / .049)
    modes = np.zeros(n)
    for frequency, weight, decay in [(328, .17, .068), (529, .10, .047), (871, .045, .035)]:
        modes += weight * np.sin(2 * np.pi * frequency * (1 + index * .019) * t) * np.exp(-t / decay)
    noise = rng.normal(0, 1, n)
    softened = np.convolve(noise, np.ones(11) / 11, mode="same")
    clack = softened * (.16 * np.exp(-t / .012) + .035 * np.exp(-t / .075))
    # Two quiet follow-up settling clicks, not a long jangling tail.
    for at, amount in [(.046, .042), (.085, .022)]:
        local = np.maximum(0, t - at)
        clack += softened * amount * (t >= at) * (1 - np.exp(-local / .001)) * np.exp(-local / .009)
    signal = (body + modes + clack) * attack
    fade = np.clip((.34 - t) / .028, 0, 1)
    signal *= fade * fade * (3 - 2 * fade)
    signal -= signal.mean()
    signal *= .69 / np.max(np.abs(signal))
    # Short boundary ramp keeps both PCM boundaries silent after DC removal.
    signal *= np.minimum(1, t / .0005) * np.minimum(1, (.34 - t) / .003)
    return signal


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    report = []
    for i in range(2):
        signal = make_hit(i)
        path = OUT / f"ChassisHurt_{i + 1:02d}.wav"
        pcm = np.round(np.clip(signal, -1, 1) * 32767).astype("<i2")
        with wave.open(str(path), "wb") as output:
            output.setnchannels(1); output.setsampwidth(2); output.setframerate(RATE)
            output.writeframes(pcm.tobytes())
        report.append({"file": str(path.relative_to(ROOT)), "seconds": len(signal) / RATE,
                       "peakDbFS": float(20 * np.log10(np.max(np.abs(signal)))),
                       "rmsDbFS": float(20 * np.log10(np.sqrt(np.mean(signal ** 2)))),
                       "clippedSamples": int(np.sum(np.abs(pcm) == 32767)), "channels": 1})
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
