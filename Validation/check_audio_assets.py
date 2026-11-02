#!/usr/bin/env python3
"""Decode/measure audio and write an offline mix preview. Requires NumPy and ffmpeg.
This does not listen to audio or run Unity. Optional --baseline-ref compares a local Git commit.
"""
from pathlib import Path
import argparse, functools, hashlib, io, json, math, re, subprocess, wave
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
AUDIO=ROOT/'Assets/IntercomMVP/Audio'
RATE=44100

def db(value): return 20*math.log10(max(1e-12,float(value)))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def level(x): return {'peakDbFS':db(np.max(np.abs(x))), 'rmsDbFS':db(np.sqrt(np.mean(x*x))), 'clippedSamples':int(np.sum(np.abs(x)>=1)), 'finite':bool(np.isfinite(x).all())}
@functools.lru_cache(maxsize=80)
def decode(path):
    channels=int(subprocess.check_output(['ffprobe','-v','error','-select_streams','a:0',
        '-show_entries','stream=channels','-of','default=noprint_wrappers=1:nokey=1',str(path)]))
    assert channels in (1,2),path
    raw=subprocess.check_output(['ffmpeg','-v','error','-i',str(path),'-ar',str(RATE),'-f','f32le','pipe:1'])
    x=np.frombuffer(raw,dtype='<f4').reshape(-1,channels).astype(float)
    # Do not let ffmpeg's automatic mono->stereo pan law quietly subtract 3 dB.
    # Duplicate mono at unity gain for a conservative per-channel sum.
    return np.repeat(x,2,axis=1) if channels==1 else x
def measure(path):
    x=decode(path);result={'file':path.name,'sha256':sha(path),'seconds':len(x)/RATE,**level(x)}
    assert result['finite'] and result['clippedSamples']==0,path
    result['boundaryStep']=float(np.max(np.abs(x[-1]-x[0])))
    result['dcOffset']=float(np.max(np.abs(np.mean(x,axis=0))))
    result['internalStep99Percentile']=float(np.quantile(np.abs(np.diff(x,axis=0)),.99))
    if path.name.startswith(('Pistol','BootStep','Impact')):
        threshold=np.max(np.abs(x))*.02
        result['onsetMs']=float(np.flatnonzero(np.max(np.abs(x),axis=1)>threshold)[0]/RATE*1000)
        result['attack80msRmsDbFS']=db(np.sqrt(np.mean(x[:round(.08*RATE)]**2)))
        assert result['onsetMs']<12 and np.all(x[0]==0) and np.all(x[-1]==0),path
        mono=x.mean(axis=1);spectrum=np.abs(np.fft.rfft(mono*np.hanning(len(mono))))**2
        f=np.fft.rfftfreq(len(mono),1/RATE)
        result['spectralCentroidHz']=float(np.sum(f*spectrum)/np.sum(spectrum))
        result['lowBodyToCrackDb']=10*math.log10(max(1e-12,np.sum(spectrum[(f>=60)&(f<600)])/max(1e-12,np.sum(spectrum[(f>=1600)&(f<9000)]))))
    return result

def get_profile():
    text=(AUDIO/'PrototypeAudio.asset').read_text()
    fields=['masterVolume','shotsVolume','stepsVolume','impactsVolume','musicVolume','ambienceVolume']
    gains={k:float(re.search(r'^  '+k+r': ([0-9.]+)',text,re.M).group(1)) for k in fields}
    mapping={re.search(r'^guid: ([a-f0-9]+)',p.read_text(),re.M).group(1):p.with_suffix('') for p in AUDIO.glob('*.meta') if p.name.endswith(('.wav.meta','.ogg.meta'))}
    banks={}
    for match in re.finditer(r'^  (\w+):\n((?:  - .+\n)+)',text,re.M):
        banks[match.group(1)]=[mapping[g] for g in re.findall(r'guid: ([a-f0-9]+)',match.group(2))]
    for required in ['pistolShots','footsteps','concreteSteps','metalSteps','gravelSteps','woodSteps','soilSteps','impactHits','metalImpacts','woodImpacts','concreteImpacts','soilImpacts']:
        assert banks.get(required),required
    return gains,banks

def add(mix,clip,at,gain):
    start=round(at*RATE);count=min(len(clip),len(mix)-start)
    if count>0:mix[start:start+count]+=clip[:count]*gain

def stress(gains,banks,steps_per_second,volleys_per_second,impacts_per_second,seconds=24):
    n=round(seconds*RATE);music=decode(AUDIO/'Music_IndustrialGroove.ogg');ambient=decode(AUDIO/'Ambience_IndustrialYard.wav')
    # Select the busy hook, and leave all loop gains fully open. This is a
    # conservative centred, no-ducking mix, not a simulation of Unity's DSP.
    m=gains['masterVolume'];mix=music[(np.arange(n)+30*RATE)%len(music)]*(m*gains['musicVolume'])
    mix+=ambient[np.arange(n)%len(ambient)]*(m*gains['ambienceVolume'])
    for i,at in enumerate(np.arange(0,seconds,1/volleys_per_second)):
        add(mix,decode(banks['pistolShots'][i%3]),at,m*gains['shotsVolume'])
    surfaces=['footsteps','concreteSteps','metalSteps','gravelSteps','woodSteps','soilSteps'];material_gain=[1,1,.88,.84,1,.82]
    for i,at in enumerate(np.arange(.015,seconds,1/steps_per_second)):
        k=i%6;bank=banks[surfaces[k]];add(mix,decode(bank[(i//6)%len(bank)]),at,m*gains['stepsVolume']*material_gain[k])
    surfaces=['metalImpacts','woodImpacts','concreteImpacts','soilImpacts']
    for i,at in enumerate(np.arange(.028,seconds,1/impacts_per_second)):
        bank=banks[surfaces[i%4]];add(mix,decode(bank[(i//4)%len(bank)]),at,m*gains['impactsVolume'])
    return mix

def export_mp3(path,x):
    assert np.isfinite(x).all() and np.max(np.abs(x))<1
    subprocess.run(['ffmpeg','-v','error','-f','f32le','-ar',str(RATE),'-ac','2','-i','pipe:0','-c:a','libmp3lame','-q:a','2','-map_metadata','-1','-y',str(path)],input=x.astype('<f4').tobytes(),check=True)

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--baseline-ref');args=parser.parse_args()
    gains,banks=get_profile();assert gains['stepsVolume']<=.2
    clips=[measure(p) for p in sorted(AUDIO.glob('*')) if p.suffix in ('.wav','.ogg')]
    manifest_counts={}
    for relative,key in [('Source/Audio/manifest.json','recordings'),('Source/Audio/MusicV3/manifest.json','sources')]:
        p=ROOT/relative;data=json.loads(p.read_text());manifest_counts[relative]=len(data[key])
        for item in data[key]:assert sha(p.parent/item['file'])==item['sha256'],item['file']
    mixes=[]
    for name,steps,volleys,impacts in [('current_gameplay',6.3,1/.3,1/.3),('double_impact_stress',6.3,1/.3,2/.3),('higher_cadence_stress',8,5,10)]:
        x=stress(gains,banks,steps,volleys,impacts)
        row={'case':name,'seconds':len(x)/RATE,'stepsPerSecond':steps,'volleysPerSecond':volleys,'impactsPerSecond':impacts,**level(x)}
        assert row['clippedSamples']==0 and row['peakDbFS']<-1,row
        mixes.append(row)
    preview=np.zeros((44*RATE,2));m=gains['masterVolume']
    for i,at in enumerate([.1,1.0,1.9,2.2,2.5,2.8,3.1]):add(preview,decode(banks['pistolShots'][i%3]),at,m*gains['shotsVolume'])
    materials=['footsteps','concreteSteps','metalSteps','gravelSteps','woodSteps','soilSteps'];vol=[1,1,.88,.84,1,.82]
    for k,key in enumerate(materials):
        for i in range(6):add(preview,decode(banks[key][i%len(banks[key])]),4+k*2+i*.28,m*gains['stepsVolume']*vol[k])
    for k,key in enumerate(['concreteImpacts','metalImpacts','woodImpacts','soilImpacts']):
        for i in range(3):add(preview,decode(banks[key][i%3]),16+k*2+i*.55,m*gains['impactsVolume'])
    preview[24*RATE:]=stress(gains,banks,6.3,1/.3,1/.3,20)
    preview[-RATE:]*=np.linspace(1,0,RATE)[:,None]
    export_mp3(ROOT/'Docs/SoundPreview.mp3',preview)
    decoded_preview=decode(ROOT/'Docs/SoundPreview.mp3');assert np.max(np.abs(decoded_preview))<1
    baseline=[]
    if args.baseline_ref:
        for p in sorted(AUDIO.glob('*.wav')):
            relative=p.relative_to(ROOT).as_posix()
            raw=subprocess.run(['git','show',args.baseline_ref+':'+relative],cwd=ROOT,capture_output=True)
            if raw.returncode:continue
            with wave.open(io.BytesIO(raw.stdout)) as w:
                old=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').astype(float)/32768
            new=decode(p);row={'file':p.name,'changed':hashlib.sha256(raw.stdout).hexdigest()!=sha(p),'oldPeakDbFS':db(np.max(np.abs(old))),'newPeakDbFS':db(np.max(np.abs(new)))}
            if p.name.startswith('BootStep'):
                row['profileAdjustedRmsChangeDb']=db(np.sqrt(np.mean(new*new))*gains['stepsVolume'])-db(np.sqrt(np.mean(old*old))*.5)
            if p.name.startswith('Pistol'):
                row['profileAdjustedAttack80msChangeDb']=db(np.sqrt(np.mean(new[:3528]**2))*gains['shotsVolume'])-db(np.sqrt(np.mean(old[:3528]**2))*.66)
            previous_meta=subprocess.check_output(['git','show',args.baseline_ref+':'+relative+'.meta'],cwd=ROOT).decode()
            row['guidPreserved']=re.search(r'guid: (\w+)',previous_meta).group(1)==re.search(r'guid: (\w+)',p.with_suffix(p.suffix+'.meta').read_text()).group(1)
            assert row['guidPreserved'];baseline.append(row)
    music=next(v for v in clips if v['file'].endswith('.ogg'))
    assert music['boundaryStep']<max(.001,music['internalStep99Percentile'])
    report={'revision':'gameplay-audio-v3','profile':gains,'sourceManifests':manifest_counts,'clips':clips,'mixes':mixes,'comparisonToGitRef':args.baseline_ref,'comparison':baseline,'preview':{'file':'Docs/SoundPreview.mp3','seconds':44,'timeline':'0-4 pistol singles/burst;4-16 asphalt/concrete/metal/gravel/wood/soil,2s each;16-24 concrete/metal/wood/soil impacts,2s each;24-44 gameplay at actual profile gains','decoded':level(decoded_preview)},'scope':'Independent decoding, source hashes, GUIDs, numerical mix and timings. This does not listen to audio or run Unity, device output, DSP import or gameplay.'}
    (ROOT/'Validation/AudioGameplayV3Review.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps({'assets':len(clips),'sources':sum(manifest_counts.values()),'mixes':mixes,'worstClipPeakDbFS':max(v['peakDbFS'] for v in clips),'baselineClips':len(baseline)},indent=2))

if __name__=='__main__':main()
