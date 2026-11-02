#!/usr/bin/env python3
"""V6 close pistol reports and mechanical reload cues.

Uses the existing, verified CC0 gun/metal recordings in Source/Audio, plus
deterministic filtered-noise mechanical detail. Only the Pistols/Audio folder
is written. The migration copies in the old Audio folder are never touched.
Python3, NumPy, SciPy and ffmpeg; no network access.
"""
from pathlib import Path
import hashlib
import json
import math
import wave

import numpy as np
from scipy.signal import butter, sosfilt
import GeneratePrototypeAudio as source

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'Assets/IntercomMVP/Game/Weapons/Pistols/Audio'
RATE = source.FX_RATE


def noise(rng, count, lo, hi):
    x = sosfilt(butter(2, [lo, hi], btype='bandpass', fs=RATE, output='sos'), rng.normal(size=count))
    return x / max(1e-9, np.std(x))


def normalized(x):
    return x / source.peak(x)


def render_pistol(index):
    """Pressure, dry crack and a short mechanical return; no pitched sweep."""
    index %= 3
    t = np.arange(round(.23 * RATE)) / RATE
    raw = source.onset_trim(source.rate_shift(source.sample(f'1911_near_{1+index%2}.flac'),
                                             (.93, .965, .915)[index]), .012, .0004)
    raw = source.fit(source.band(raw, 55, 7600), .23)
    body = normalized(source.band(raw, 70, 780))
    # Give the report a low-mid body but leave space after its first 75ms.
    body *= np.exp(-np.maximum(t-.012, 0)/.030)
    crack = source.fit(source.onset_trim(source.sample(f'PPQ_shot_{1+index%2}.flac'), .018, .0003), .23)
    crack = (.7*normalized(source.band(raw, 1800, 7600))
             + .3*normalized(source.band(crack, 1500, 6300))) * np.exp(-np.maximum(t-.005,0)/.018)
    main = normalized(source.band(raw, 340, 7300)) * np.exp(-np.maximum(t-.008, 0)/.035)
    metal = source.fit(source.onset_trim(source.sample('metal.flac')), .23)
    metal = normalized(source.band(metal, 700, 3800)) * np.exp(-t/.010)
    delayed = np.zeros_like(t); offset = round((.046 + index*.003) * RATE)
    delayed[offset:] = metal[:-offset]
    x = .70*main + .24*body + .40*crack + .075*delayed
    # Soft parallel compression adds weight without a clipped square-wave edge.
    x = .45*x + .55*np.tanh(2.2*x)/2.2
    return source.finish_hit(source.edges(x, .00025, .025), -3.2)


def render_reload(finish=False):
    rng = np.random.default_rng(60451 + int(finish))
    t = np.arange(round((.19 if finish else .27)*RATE))/RATE
    x = np.zeros_like(t)
    # Two compact clunks suggest spring-loaded magazines and the final rack.
    contacts = [(0, .55), (.087, .9)] if finish else [(0, .65), (.118, .45)]
    for at, weight in contacts:
        a = np.maximum(0, t-at)
        env = np.where(t >= at, (1-np.exp(-a/.0008))*np.exp(-a/.012), 0)
        x += weight*(.35*noise(rng,len(t),550,3300)
                     + np.sin(2*math.pi*(335 if finish else 265)*a)*np.exp(-a/.022))*env
    if finish:
        a=np.maximum(0,t-.025)
        x += .14*noise(rng,len(t),900,2600)*np.where(t>=.025,(1-np.exp(-a/.003))*np.exp(-a/.025),0)
    return source.finish_hit(source.edges(x,.0005,.025), -9.0)


def metrics(x):
    energy=x*x; cumulative=np.cumsum(energy)
    f=np.fft.rfftfreq(len(x),1/RATE); power=abs(np.fft.rfft(x))**2
    return {'seconds':len(x)/RATE,
            'peakDbFS':round(20*np.log10(source.peak(x)),3),
            'rmsDbFS':round(20*np.log10(max(1e-12,np.sqrt(np.mean(energy)))),3),
            'energy95ms':round(1000*np.searchsorted(cumulative,cumulative[-1]*.95)/RATE,2),
            'energyAfter100msFraction':round(float(np.sum(energy[round(.1*RATE):])/np.sum(energy)),6),
            'centroidHz':round(float(np.sum(f*power)/np.sum(power)),1),
            'clippedSamples':int(np.sum(abs(x)>=1))}


def write(name,x):
    path=DEST/name; before=path.read_bytes() if path.exists() else None
    previous=None
    if before:
        with wave.open(str(path),'rb') as w:
            old=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2')/32768
        previous=metrics(old)
    pcm=np.rint(x*32767).astype('<i2')
    with wave.open(str(path),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(pcm.tobytes())
    meta=path.with_suffix('.wav.meta')
    if not meta.exists():meta.write_text(source.importer(path,False))
    return {'file':str(path.relative_to(ROOT)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
            'previousSha256':hashlib.sha256(before).hexdigest() if before else None,
            'previous':previous,**metrics(pcm.astype(float)/32768)}


def main():
    source.verify_sources()
    DEST.mkdir(parents=True,exist_ok=True)
    assets=[write(f'Pistol_{i+1:02d}.wav',render_pistol(i)) for i in range(3)]
    assets += [write('Reload_Start.wav',render_reload()),write('Reload_Finish.wav',render_reload(True))]
    report={'revision':'weapon-audio-v6','source':'Existing verified CC0 gun and metal recordings; original deterministic mechanical reload cues.',
            'scope':'PCM verification; not a subjective listening or Unity playback claim.','assets':assets}
    path=ROOT/'Validation/WeaponAudioV6Review.json'
    if path.exists():
        history={a['file']:a for a in json.loads(path.read_text())['assets']}
        for a in assets:
            if a['file'] in history:
                a['previousSha256']=history[a['file']]['previousSha256'];a['previous']=history[a['file']]['previous']
    path.write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report,indent=2))


if __name__=='__main__':main()
