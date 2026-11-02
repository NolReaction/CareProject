#!/usr/bin/env python3
"""Original 128 BPM industrial breakbeat score from curated CC0 instrument recordings.

Offline API: generate_music(sample_rate=44100) -> stereo float64 array.
The module does not mutate Unity metadata. CLI writes the OGG and 45 s preview.
Requires Python 3, NumPy, SciPy and ffmpeg; no network or DAW required.
"""
from pathlib import Path
from functools import lru_cache
from fractions import Fraction
import hashlib
import json
import math
import subprocess
import numpy as np
from scipy.signal import butter, sosfiltfilt, resample_poly

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Source/Audio/MusicV3'
RATE = 44100
BPM = 128
BARS = 48
TITLE = 'Iron Pulse'


def peak(x):
    return max(1e-12, float(np.max(np.abs(x))))


def band(x, low=50, high=6000):
    return sosfiltfilt(butter(2, [low, high], fs=RATE, btype='bandpass', output='sos'), x, axis=0)


def shape(x, attack=.0015, release=.025):
    x = np.array(x, dtype=np.float64, copy=True)
    a, r = min(len(x), round(attack*RATE)), min(len(x), round(release*RATE))
    if a: x[:a] *= np.linspace(0, 1, a)
    if r: x[-r:] *= np.cos(np.linspace(0, np.pi/2, r))**2
    return x


def pitch(x, ratio):
    f = Fraction(1/ratio).limit_denominator(1024)
    return resample_poly(x, f.numerator, f.denominator)


def verify_sources():
    data = json.loads((SOURCE/'manifest.json').read_text())
    for item in data['sources']:
        if hashlib.sha256((SOURCE/item['file']).read_bytes()).hexdigest() != item['sha256']:
            raise ValueError('Changed source recording: '+item['file'])
    return data


@lru_cache(maxsize=32)
def recording(name):
    raw = subprocess.check_output(['ffmpeg','-v','fatal','-i',str(SOURCE/name),
        '-ar',str(RATE),'-ac','1','-f','f32le','pipe:1'])
    x = np.frombuffer(raw,dtype='<f4').astype(np.float64)
    # The source drums have a little pre-roll. Retain 0.7 ms before the real attack.
    env = np.convolve(x*x,np.ones(32)/32,mode='same')**.5
    begin = np.flatnonzero(env > peak(env)*.018)[0]
    x = x[max(0,int(begin)-31):]
    return x/peak(x)


@lru_cache(maxsize=256)
def bass_note(midi, beats, alternate=0):
    root, name = min([(28,f'bass_e2_{1+alternate%2}.flac'),(33,'bass_a2_1.flac'),
                      (39,'bass_eb3_1.flac')],key=lambda pair:abs(midi-pair[0]))
    # The direct bass recording is slightly sharp. Apply a fixed small correction,
    # preserving natural string pitch movement and the recorded pick attack.
    x = pitch(recording(name),2**((midi-root-.16)/12))
    duration = beats*60/BPM
    x = x[:round((duration+.075)*RATE)]
    t = np.arange(len(x))/RATE
    x = band(x,34,2300)
    x = .70*x + .30*band(np.tanh(x*3.8),55,1350)
    x *= np.exp(-np.maximum(t-duration,0)/.024)
    x *= np.exp(-t/1.2)
    return shape(x,.0012,.025)/peak(x)


GUITARS = [(40,'e2'),(45,'a2'),(51,'eb3'),(54,'gb3'),(57,'a3'),(60,'c4'),(63,'eb4')]


@lru_cache(maxsize=300)
def guitar_note(midi, beats, articulation='mute', alternate=0):
    root,name = min(GUITARS,key=lambda pair:abs(midi-pair[0]))
    rr = 1+alternate%2 if root==40 else 1
    x = pitch(recording(f'guitar_{name}_{rr}.flac'),2**((midi-root-.10)/12))
    duration=beats*60/BPM
    x=x[:round((duration+.18)*RATE)]
    t=np.arange(len(x))/RATE
    x=band(x,90,5500)
    # Parallel drive and a dark cabinet curve, with pick transient retained.
    dry=x.copy()
    x=band(np.tanh(x*4.4),100,4400)*.72+dry*.28
    if articulation=='mute':
        x*=np.exp(-t/max(.07,duration*.80))
    else:
        x*=np.exp(-t/1.7)
    x*=np.exp(-np.maximum(t-duration,0)/(.026 if articulation=='mute' else .085))
    return shape(x,.0015,.035)/peak(x)


@lru_cache(maxsize=30)
def drum(kind, alternate=0):
    name={'kick':f'kit_kick_{1+alternate%2}.flac','snare':'kit_snare_top.flac',
          'rim':'kit_rimshot.flac','hat':f'kit_hat_{1+alternate%2}.flac',
          'open':'kit_hat_open.flac','crash':'kit_crash.flac','tom':'kit_tom.flac'}[kind]
    x=recording(name).copy()
    if kind=='snare':
        bottom=recording('kit_snare_bottom.flac')
        n=max(len(x),len(bottom));x=np.pad(x,(0,n-len(x)))
        x+=.36*np.pad(bottom,(0,n-len(bottom)))
    duration={'kick':.34,'snare':.32,'rim':.28,'hat':.125,'open':.38,'crash':1.8,'tom':.48}[kind]
    x=x[:round(duration*RATE)]
    high={'kick':4200,'snare':9500,'rim':7000,'hat':11000,'open':11000,'crash':10000,'tom':6000}[kind]
    low={'kick':32,'snare':115,'rim':160,'hat':650,'open':600,'crash':400,'tom':65}[kind]
    x=band(x,low,high)
    t=np.arange(len(x))/RATE
    decay={'kick':.16,'snare':.16,'rim':.11,'hat':.10,'open':.28,'crash':1.05,'tom':.24}[kind]
    x*=np.exp(-np.maximum(t-.025,0)/decay)
    # Recorded close-mic body and impact; no synthesized kick oscillator.
    if kind in ('kick','snare','rim'):x=np.tanh(x/peak(x)*1.8)
    return shape(x,.0009,.02)/peak(x)


def generate_music(sample_rate=44100):
    """Render Iron Pulse; returns a seamless original 48-bar stereo score."""
    verify_sources()
    beat=60/BPM;count=round(RATE*beat*4*BARS)
    buses={key:np.zeros((count,2),np.float64) for key in ('drums','bass','rhythm','lead')}
    rng=np.random.default_rng(734199)
    kick_times=[]
    muted_string=shape(band(recording('guitar_mute.flac'),180,4000)[:round(.12*RATE)],.002,.035)
    muted_string/=peak(muted_string)

    def add(bus,x,when,gain,pan=0.,human=0.):
        start=round((when+rng.uniform(-human,human))*RATE)%count
        gains=np.array([math.sqrt((1-pan)/2),math.sqrt((1+pan)/2)])*gain
        sound=x[:,None]*gains
        end=start+len(x)
        if end<=count:buses[bus][start:end]+=sound
        else:
            first=count-start;buses[bus][start:]+=sound[:first];buses[bus][:len(x)-first]+=sound[first:]

    # A deliberate two-bar answer rather than a constantly ascending arpeggio.
    riffs=[[(0,28,.42),(.75,28,.25),(1.5,31,.36),(2.25,33,.25),(2.75,31,.28),(3.5,38,.30)],
           [(0,40,.60),(1,38,.32),(1.75,35,.25),(2.5,31,.32),(3,28,.28),(3.5,31,.30)]]
    # Eight-bar motif: long/short call, descending answer, then a two-note pickup.
    motif={0:[(.0,64,.70),(1.5,67,.40),(2.5,69,.75)],
           1:[(.5,67,.5),(1.75,64,.80),(3.25,62,.40)],
           2:[(.0,64,.70),(1.5,67,.35),(2.25,71,.85)],
           3:[(.5,69,.48),(1.5,67,.42),(2.5,64,1.0)],
           4:[(.0,67,.70),(1.5,69,.35),(2.5,71,.70)],
           5:[(.5,69,.5),(1.75,67,.7),(3.25,66,.40)],
           6:[(.0,64,.65),(1.5,62,.45),(2.5,59,.7)],
           7:[(.5,62,.50),(2.0,64,.80),(3.5,62,.30)]}

    for bar in range(BARS):
        base=bar*4*beat
        break_section=24<=bar<28
        build=28<=bar<32
        full=8<=bar<24 or 32<=bar<46
        # The harmonic middle moves the same rhythmic signature through C and D.
        shift={16:8,17:8,18:10,19:10,20:0,21:0,22:7,23:7,
               40:8,41:8,42:10,43:10,44:0,45:0,46:7,47:7}.get(bar,0)
        riff=riffs[bar%2]
        if shift in (8,10):
            # Relative-major C / D bridge; keep its thirds diatonic to E minor.
            root = 36 if shift==8 else 38
            intervals = [0,0,4,7,4,2] if bar%2==0 else [0,7,4,2,0,2]
            riff=[(pos,root+intervals[j],length) for j,(pos,_,length) in enumerate(riff)]
            shift=0
        if break_section:riff=[(0,28,.8),(1.75,35,.4),(3,31,.45)]
        for j,(pos,note,length) in enumerate(riff):
            note+=shift
            if note>47:note-=12
            add('bass',bass_note(note,length,j+bar),base+pos*beat,.54 if not break_section else .40,0.,.002)
            # Muted octave guitar doubles only the defining accents, preserving space.
            if not break_section and (j%2==0 or full):
                gnote=note+12
                for side,detune_time in [(-.62,0.),(.62,.008)]:
                    add('rhythm',guitar_note(gnote,length*.8,'mute',j+bar),base+pos*beat+detune_time,
                        .19 if full else .13,side,.0025)
                    if j in (0,3) and full:
                        add('rhythm',guitar_note(gnote+7,length*.8,'mute',j),base+pos*beat+.012+detune_time,.10,side)

        if break_section:
            kicks=[0,2.5];snare_positions=[2.]
        elif bar%2==0:
            kicks=[0,.75,2.,2.75];snare_positions=[1,3]
        else:
            kicks=[0,1.5,2.5,3.5];snare_positions=[1,3]
        if bar%8==7:kicks=[0,1.5,2.5]
        for j,pos in enumerate(kicks):
            add('drums',drum('kick',j+bar),base+pos*beat,.80 if not break_section else .52)
            kick_times.append(base+pos*beat)
        for j,pos in enumerate(snare_positions):
            add('drums',drum('snare'),base+pos*beat+.005,.57 if not break_section else .39,-.04,.0015)
            if full:add('drums',drum('rim'),base+pos*beat+.009,.12,.12)
        if not break_section:
            for pos in ([.75,2.75] if bar%2 else [1.75,2.875]):
                add('drums',drum('snare'),base+pos*beat,.105,-.10,.003)
        for j in range(8):
            pos=j*.5+(0.015 if j%2 else 0)
            if break_section and j%2:continue
            gain=(.14 if j%2==0 else .095)*(.70 if break_section else 1.)
            add('drums',drum('hat',j+bar),base+pos*beat,gain,.36,.002)
            if full and j in (3,7):add('drums',drum('hat',1),base+(pos+.25)*beat,.042,.23,.002)
        if full and bar%2==1:add('drums',drum('open'),base+3.5*beat,.10,.34)
        if not break_section:
            add('rhythm',muted_string,base+(3.25 if bar%2==0 else .5)*beat,.027,-.5,.003)
        if bar in (0,8,16,28,32,40):add('drums',drum('crash'),base,.18,-.4)
        if bar%8==7:
            # Alternating low tom and snare fill, rather than a repeated roll.
            for j,pos in enumerate([3.,3.25,3.5,3.75]):
                add('drums',drum('tom' if j%2==0 else 'snare'),base+pos*beat,.20+j*.045,-.3+j*.18)
        if build:
            for pos in [2.25,2.75,3.25,3.75]:
                add('drums',drum('snare'),base+pos*beat,.075+(bar-28)*.025,0.,.002)

        if full or bar in (4,5,6,7,29,30,31,46,47):
            for j,(pos,note,length) in enumerate(motif[bar%8]):
                # Melody remains in E natural minor over its relative C/D bridge.
                gain=.22 if full else .145
                if 32<=bar<40:gain=.25
                add('lead',guitar_note(note,length,'open',j),base+pos*beat,gain,-.12,.002)
                if bar>=32 and bar<40:
                    add('lead',guitar_note(note-12,length,'open',j+1),base+pos*beat+.012,gain*.28,.23,.001)
        if break_section:
            for pos,note in [(0,52),(2.5,59)]:
                add('lead',guitar_note(note,1.15,'open'),base+pos*beat,.10,-.25)

    # Stereo room and tempo echo on guitars, quiet enough to keep the riff dry.
    lead=buses['lead'];rhythm=buses['rhythm']
    lead=lead+np.roll(lead[:,::-1],round(beat*.75*RATE),axis=0)*.20+np.roll(lead,round(beat*1.5*RATE),axis=0)*.08
    room=np.zeros_like(lead)
    for delay,gain in [(.023,.085),(.041,.065),(.067,.045),(.103,.035),(.157,.025)]:
        room+=np.roll((lead+rhythm*.6)[:,::-1],round(delay*RATE),axis=0)*gain
    # Kick transients open a little space in the bass and guitars, not audible pumping.
    duck=np.ones(count)
    recovery=np.arange(round(.14*RATE))/RATE
    curve=1-.22*np.exp(-recovery/.048)
    for when in kick_times:
        indices=(round(when*RATE)+np.arange(len(curve)))%count
        duck[indices]=np.minimum(duck[indices],curve)
    mix=buses['drums']+(buses['bass']+rhythm+lead+room)*duck[:,None]
    # Gentle parallel bus saturation controls occasional coincident picks/hits.
    mix=.85*mix+.15*np.tanh(mix*1.7)/1.7
    mix-=np.mean(mix,axis=0)
    mix*=.79/peak(mix)
    # Level leaves the drum crest intact; decoded integrated loudness is ~-18 LUFS.
    if sample_rate!=RATE:
        f=Fraction(sample_rate,RATE);mix=resample_poly(mix,f.numerator,f.denominator,axis=0)
    return mix


def encode(path,samples,codec_args):
    path.parent.mkdir(parents=True,exist_ok=True)
    subprocess.run(['ffmpeg','-v','fatal','-y','-f','f32le','-ar',str(RATE),'-ac','2','-i','pipe:0',
        *codec_args,str(path)],input=samples.astype('<f4').tobytes(),check=True)


def main():
    x=generate_music()
    target=ROOT/'Assets/IntercomMVP/Audio/Music_IndustrialGroove.ogg'
    encode(target,x,['-c:a','libvorbis','-q:a','5','-fflags','+bitexact','-flags:a','+bitexact','-map_metadata','-1','-metadata','title='+TITLE,'-metadata','artist=CareProject'])
    # Start on the full groove; a short fade makes this a standalone preview only.
    first=round(8*4*60/BPM*RATE);preview=x[first:first+45*RATE].copy()
    n=round(.65*RATE);preview[-n:]*=np.linspace(1,0,n)[:,None]
    encode(ROOT/'Docs/MusicPreview.mp3',preview,['-c:a','libmp3lame','-q:a','2'])
    print(json.dumps({'title':TITLE,'bpm':BPM,'bars':BARS,'seconds':len(x)/RATE,
        'peak_dbfs':20*math.log10(peak(x)),'rms_dbfs':20*math.log10(np.sqrt(np.mean(x*x))),
        'finite':bool(np.isfinite(x).all()),'clipped_samples':int((abs(x)>=1).sum()),
        'boundary_step':float(np.max(np.abs(x[0]-x[-1]))),
        'ogg_sha256':hashlib.sha256(target.read_bytes()).hexdigest()},indent=2))


if __name__=='__main__':main()
