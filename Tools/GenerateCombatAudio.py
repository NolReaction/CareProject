#!/usr/bin/env python3
"""Generate the current recorded/hybrid pistol and robot-hit sounds.

numpy/scipy/ffmpeg. Verified CC0 recordings in Source/Audio supply the actual
gun pressure/crack and short damped percussion contact. Synthesized layers are
original. Processing is offline; GUIDs/import settings/voice limits retained.
"""
from pathlib import Path
import json, wave, subprocess, hashlib
import numpy as np
from scipy.signal import butter, sosfilt, resample_poly
import GeneratePrototypeAudio as source

ROOT=Path(__file__).resolve().parents[1]
RATE=44100
HI=RATE*4
PISTOL=ROOT/'Assets/IntercomMVP/Content/Weapons/Pistols/Audio'
ROBOT=ROOT/'Assets/IntercomMVP/Content/Enemies/Shared/Audio'
SOURCE_FILES=['1911_near_1.flac','1911_near_2.flac','PPQ_shot_1.flac','PPQ_shot_2.flac','wood_crack.flac','wood_body.flac']


def verify_sources():
    entries={entry['file']:entry for entry in json.loads((ROOT/'Source/Audio/manifest.json').read_text())['recordings']}
    for name in SOURCE_FILES:
        entry=entries[name]
        assert entry['license']=='CC0-1.0',name
        assert hashlib.sha256((ROOT/'Source/Audio'/name).read_bytes()).hexdigest()==entry['sha256'],name


def band(x,lo=None,hi=None,rate=HI):
    if lo and hi:sos=butter(2,[lo,hi],'bandpass',fs=rate,output='sos')
    elif lo:sos=butter(2,lo,'highpass',fs=rate,output='sos')
    else:sos=butter(2,hi,'lowpass',fs=rate,output='sos')
    return sosfilt(sos,x)


def finish(x,peak_db):
    x=resample_poly(band(x,hi=11500),1,4)
    x=band(x,lo=40,rate=RATE)
    x-=np.mean(x)
    x[:8]*=np.linspace(0,1,8)
    x[-int(.009*RATE):]*=np.linspace(1,0,int(.009*RATE))
    return x*10**(peak_db/20)/max(np.max(np.abs(x)),1e-12)


def pistol(variant):
    rng=np.random.default_rng(120420+variant)
    t=np.arange(round(.235*HI))/HI;n=len(t)
    # Recorded close-mic gun report is the dominant source. The existing CC0
    # source manifest is verified before rendering; raw files stay untouched.
    take=source.sample(f'1911_near_{1+(variant-1)%2}.flac')
    take=source.onset_trim(source.rate_shift(take,[.94,.97,.915][variant-1]),.010,.00025)
    take=source.fit(source.band(take,65,8500),.235)
    rt=np.arange(len(take))/RATE
    normalized=lambda x:x/max(np.max(np.abs(x)),1e-12)
    pressure_recording=normalized(source.band(take,90,920))*np.exp(-np.maximum(rt-.014,0)/.049)
    main_recording=normalized(source.band(take,260,6900))*np.exp(-np.maximum(rt-.011,0)/.045)
    crack_take=source.fit(source.onset_trim(source.sample(f'PPQ_shot_{1+(variant-1)%2}.flac'),.012,.00025),.235)
    crack_recording=(.76*normalized(source.band(take,1600,8500))+.24*normalized(source.band(crack_take,1400,7400)))*np.exp(-np.maximum(rt-.006,0)/.017)
    recording=resample_poly(.70*main_recording+.48*pressure_recording+.33*crack_recording,4,1)[:n]
    if len(recording)<n:recording=np.pad(recording,(0,n-len(recording)))
    # Original supplemental pressure bed replaces the old thin oscillator/noise
    # report; it supplies body below the recorded transient instead of a beep.
    crack=band(rng.normal(size=n),lo=1400,hi=9000)*np.exp(-t/.0048)
    bite=band(rng.normal(size=n),lo=650,hi=5200)*np.exp(-t/.015)
    body=band(rng.normal(size=n),lo=95,hi=1700)*np.exp(-t/.037)
    phase=2*np.pi*(88*t+52*.006*(1-np.exp(-t/.006)))
    pressure=(np.sin(phase)+.27*np.sin(phase*1.87+.3))*np.exp(-t/.038)*(1-np.exp(-t/.0003))
    raw=.95*crack+1.16*bite+1.85*body+.60*pressure
    # Parallel saturated body retains the unsaturated snap at the beginning.
    core=.88*recording+.12*raw
    core=.16*core+.84*np.tanh(core*4.1)/1.25
    room=np.zeros(n)
    for delay,gain in [(.008+.0007*variant,.16),(.020+.0005*variant,.10),(.034,.045)]:
        d=int(delay*HI);room[d:]+=band(core,hi=3100)[:-d]*gain
    slide=np.zeros(n)
    for delay,gain,decay in [(.039,.16,.0045),(.087,.10,.0055)]:
        d=int((delay+.0006*variant)*HI);a=t[:n-d]
        slide[d:]+=band(rng.normal(size=n-d),lo=900,hi=6200)*gain*np.exp(-a/decay)
    return finish(core+room+slide,-1.7)


def robot_hit(variant):
    rng=np.random.default_rng(124840+variant)
    duration=[.185,.21,.195][variant-1]
    t=np.arange(round(duration*HI))/HI;n=len(t)
    # Three distinct body responses: compact shell pop, cable compression,
    # and a hollow housing crush. No sustained metal/bell oscillator.
    body=band(rng.normal(size=n),lo=80,hi=[1050,690,1350][variant-1])*np.exp(-t/[.029,.037,.025][variant-1])
    snap=band(rng.normal(size=n),lo=[850,530,1200][variant-1],hi=[5100,3100,6400][variant-1])*np.exp(-t/.007)
    pressure=np.sin(2*np.pi*[135,104,172][variant-1]*t)*np.exp(-t/.021)*(1-np.exp(-t/.00045))
    crunch=np.zeros(n)
    delays=[[.008,.018,.034,.057],[.013,.030,.046,.075],[.005,.025,.041,.068]][variant-1]
    for i,delay in enumerate(delays):
        d=int(delay*HI);a=t[:n-d]
        burst=band(rng.normal(size=n-d),lo=380+220*i,hi=4200-240*i)
        crunch[d:]+=burst*(.42/(1+i*.58))*np.exp(-a/(.006+i*.001))
    if variant==2:
        # A brief unpitched ratchet/friction release, not a cartoon laser chirp.
        friction=band(rng.normal(size=n),lo=180,hi=1700)
        envelope=np.exp(-np.maximum(t-.034,0)/.026)*(t>.034)
        crunch+=friction*envelope*(.12+.08*np.sin(t*2*np.pi*95)**2)
    # A short recorded percussion contact adds physical friction/detail. Its
    # original provenance is wood percussion, not claimed as a real robot hit.
    contact=source.fit(source.onset_trim(source.sample('wood_crack.flac' if variant!=2 else 'wood_body.flac'),.010,.00025),duration)
    contact=source.band(contact,200 if variant==2 else 650,3500)
    contact=contact/max(np.max(np.abs(contact)),1e-12)
    contact*=np.exp(-np.arange(len(contact))/RATE/.019)
    contact=resample_poly(contact,4,1)[:n]
    if len(contact)<n:contact=np.pad(contact,(0,n-len(contact)))
    composite=1.5*body+.7*snap+.4*pressure+crunch+.28*contact
    return finish(.42*composite+.58*np.tanh(composite*1.8),-3.4)


def read(path):
    with wave.open(str(path),'rb') as source:
        assert source.getnchannels()==1 and source.getframerate()==RATE and source.getsampwidth()==2
        return np.frombuffer(source.readframes(source.getnframes()),dtype='<i2').astype(float)/32768


def write(path,x):
    path.parent.mkdir(parents=True,exist_ok=True)
    with wave.open(str(path),'wb') as out:
        out.setnchannels(1);out.setsampwidth(2);out.setframerate(RATE)
        out.writeframes(np.round(np.clip(x,-1,1)*32767).astype('<i2').tobytes())


def metric(path,x):
    power=np.abs(np.fft.rfft(x))**2
    frequencies=np.fft.rfftfreq(len(x),1/RATE)
    energy=lambda lo,hi:float(np.sum(power[(frequencies>=lo)&(frequencies<hi)])/np.sum(power))
    return dict(path=path.relative_to(ROOT).as_posix(),seconds=len(x)/RATE,
        peakDbFS=float(20*np.log10(max(np.max(np.abs(x)),1e-12))),
        rmsDbFS=float(20*np.log10(max(np.sqrt(np.mean(x*x)),1e-12))),
        first80msRmsDbFS=float(20*np.log10(max(np.sqrt(np.mean(x[:int(.08*RATE)]**2)),1e-12))),
        bodyFraction80to1800Hz=energy(80,1800),upperFraction6000to16000Hz=energy(6000,16000),
        firstSample=float(x[0]),lastSample=float(x[-1]),clippedSamples=int(np.sum(np.abs(x)>=1)))


def mix(shots,hits,interval,shot_gain=.8*.64,hit_gain=.8*.32*.75):
    burst=np.zeros(RATE*4)
    for i in range(24):
        at=int(i*interval*RATE);x=shots[i%len(shots)]*shot_gain;burst[at:at+len(x)]+=x
        if hits:
            at+=int(.125*RATE);h=hits[(i*2)%len(hits)]*hit_gain;burst[at:at+len(h)]+=h
    return burst


def main():
    verify_sources()
    shots=[pistol(i) for i in range(1,4)];hits=[robot_hit(i) for i in range(1,4)]
    metrics=[]
    for folder,name,bank in [(PISTOL,'Pistol',shots),(ROBOT,'ChassisHit',hits)]:
        for i,x in enumerate(bank,1):
            path=folder/f'{name}_0{i}.wav';write(path,x);metrics.append(metric(path,read(path)))
    burst=mix(shots,hits,.12)
    assert all(np.isfinite(x).all() and np.max(np.abs(x))<1 for x in shots+hits)
    assert np.max(np.abs(burst))<1
    output=ROOT/'Validation/Results/Audio';output.mkdir(parents=True,exist_ok=True)
    write(output/'CombatPreview.wav',burst)
    report=dict(status='PASS',interval=.12,clips=metrics,
        mixedBurstPeakDbFS=float(20*np.log10(np.max(np.abs(burst)))),
        sources={name:hashlib.sha256((ROOT/'Source/Audio'/name).read_bytes()).hexdigest() for name in SOURCE_FILES},
        scope='Verified source recordings plus deterministic original layers; offline processing, not Unity playback')
    (output/'CombatAudioReview.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report,indent=2))


if __name__=='__main__':main()
