#!/usr/bin/env python3
"""Validate the short metal-impact bank, its source, and actual mix routing.

Requires Python3, NumPy, SciPy and ffmpeg. Does not execute Unity audio playback.
"""
from pathlib import Path
import hashlib
import json
import re
import sys
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'Tools'))
import GenerateImpactAudio as generator


def main():
    checks = 0
    def check(value, label):
        nonlocal checks
        checks += 1
        if not value:
            raise AssertionError(label)

    generator.metal_source()  # Decoding includes the checked-in source/license hash validation.
    bank = ROOT / 'Assets/IntercomMVP/Content/Shared/Audio/Impacts'
    profile = (ROOT / 'Assets/IntercomMVP/Content/Shared/Audio/Settings/GameAudio.asset').read_text()
    block = re.search(r'^  metalImpacts:\n((?:  - .*\n)+)', profile, re.M)
    check(block is not None, 'The actual audio profile has a metal impact bank')
    routed = re.findall(r'guid: ([0-9a-f]{32})', block[1])
    guids = []
    hashes = []
    rows = []
    for index in range(3):
        path = bank / ('Impact_Metal_%02d.wav' % (index + 1))
        meta = Path(str(path) + '.meta')
        guid = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(), re.M)
        check(guid is not None, 'Importer GUID exists: ' + path.name)
        guids.append(guid[1])
        with wave.open(str(path), 'rb') as stream:
            check((stream.getnchannels(), stream.getsampwidth(), stream.getframerate()) == (1, 2, 44100), 'Mono16/44.1kHz: ' + path.name)
            pcm = np.frombuffer(stream.readframes(stream.getnframes()), '<i2')
        x = pcm.astype(float) / 32768
        metrics = generator.inspect_wave(x)
        check(np.isfinite(x).all(), 'Finite PCM')
        check(.10 <= metrics['seconds'] <= .20, 'Short contact envelope')
        check(-14 <= metrics['peakDbFS'] <= -8, 'Bounded contact peak')
        check(-31 <= metrics['rmsDbFS'] <= -25, 'No long loud resonance')
        check(metrics['energy90ms'] < 35, 'Most energy is an initial contact, not ringing')
        check(metrics['clippedSamples'] == 0, 'No clipped samples')
        check(abs(metrics['dcOffset']) < .0001, 'Near-zero DC offset')
        check(x[0] == 0 and x[-1] == 0, 'No boundary clicks')
        tail = float(np.sum(x[round(.06 * 44100):] ** 2) / np.sum(x*x))
        check(tail < .005, 'Damped 60ms tail')
        power = np.abs(np.fft.rfft(x)) ** 2
        frequency = np.fft.rfftfreq(len(x), 1 / 44100)
        high = float(np.sum(power[frequency > 3000]) / np.sum(power))
        check(high < .05, 'Limited sharp upper-band energy')
        check(500 < metrics['energyCentroidHz'] < 1300, 'Recognisable low-mid metallic contact')
        rendered = np.rint(generator.impact(index, 'metal') * 32767).astype('<i2')
        check(np.array_equal(rendered, pcm), 'Current WAV reproducible from included source')
        hashes.append(hashlib.sha256(path.read_bytes()).hexdigest())
        rows.append({'file': path.relative_to(ROOT).as_posix(), **metrics, 'energyAfter60ms': tail, 'energyAbove3000Hz': high})
    check(len(set(guids)) == 3, 'Three distinct preserved import identities')
    check(routed == guids, 'The actual gameplay profile routes the three current clips in order')
    check(len(set(hashes)) == 3, 'Variants are distinct recordings, not duplicated files')
    print(json.dumps({'status': 'PASS', 'checks': checks, 'clips': rows,
                      'scope': 'Included source, PCM envelope, reproducibility and serialized gameplay routing; not Unity/device listening approval.'}, indent=2))


if __name__ == '__main__':
    main()
