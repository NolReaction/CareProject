#!/usr/bin/env python3
"""Check the shipped V21 scene/profile links and protection inventory; no Unity execution."""
from pathlib import Path
import sys,re,json,hashlib
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools'))
import AuthorNpcScene as author

def main():
 checks=0
 def check(ok,label):
  nonlocal checks
  checks+=1
  if not ok:raise AssertionError(label)
 def scalar(text,key):
  m=re.search(r'^  '+re.escape(key)+r': ([^\n]+)$',text,re.M)
  check(m is not None,'field '+key)
  return float(m[1])
 def local(text,key):
  m=re.search(r'^  '+re.escape(key)+r': \{fileID: (-?\d+)\}$',text,re.M)
  check(m is not None,'reference '+key)
  return int(m[1])
 scene=author.Scene((ROOT/'Assets/IntercomMVP/Scenes/TutorialYard/IntercomMVP.unity').read_text())
 missions=scene.find_go('MVP_NpcSandbox');hero=scene.find_go('Robot')
 check(missions is not None and hero is not None,'authored mission and hero')
 progression=scene.component(missions,'IntercomProgression');director=scene.component(missions,'IntercomMissionDirector');frontend=scene.component(missions,'IntercomFrontEnd');magazine=scene.component(hero,'IntercomWeaponMagazine')
 check(all([progression,director,frontend,magazine]),'complete profile/gameplay owners')
 data=scene.blocks[progression]
 check(local(data,'mission')==director,'profile settles this mission')
 check(local(data,'magazine')==magazine,'profile upgrades actual hero magazine')
 check(local(scene.blocks[frontend],'progression')==progression,'workshop uses profile owner')
 check(local(data,'visualLeftMount')==scene.transform(scene.find_go('IntercomMuzzle_L')),'left extension follows barrel marker')
 check(local(data,'visualRightMount')==scene.transform(scene.find_go('IntercomMuzzle_R')),'right extension follows barrel marker')
 check('efc4ccf9a1224f078dadf974b01d842e' in data,'profile uses shipped pistol definition')
 profile=(ROOT/'Assets/IntercomMVP/Content/Enemies/Udarnik/Settings/UdarnikCapsule.asset').read_text()
 hp=scalar(profile,'maximumHealth');speed=scalar(profile,'moveSpeed')
 values={key:scalar(profile,key) for key in ['runnerHealthMultiplier','runnerSpeedMultiplier','heavyHealthMultiplier','heavySpeedMultiplier','heavyDamageMultiplier']}
 check(hp*values['runnerHealthMultiplier']<hp<hp*values['heavyHealthMultiplier'],'variant health roles differ')
 check(speed*values['heavySpeedMultiplier']<speed<speed*values['runnerSpeedMultiplier'],'variant speed roles differ')
 check(scalar(profile,'attackCommitSeconds')>=.14,'authored dodge commitment')
 nav=scene.blocks[scene.component(missions,'IntercomNpcNavigation')]
 check(scalar(nav,'agentHeight')>=1.35*1.12,'navigation considers heavy height')
 check(scalar(nav,'agentClearanceRadius')>=.32*1.12,'navigation considers heavy radius')
 missions_data={}
 for name,total in [('Kill50',50),('Rescue',50)]:
  text=(ROOT/'Assets/IntercomMVP/Content/Missions'/(name+'.asset')).read_text()
  check(scalar(text,'useRhythmicWaves')==1,name+' rhythm enabled')
  check(scalar(text,'waveSize')==10,name+' base wave quota')
  check(scalar(text,'continuousWaves')==1,name+' continuous overlap enabled')
  check(.9<=scalar(text,'spawnInterval')<=1.2,name+' bounded high pressure cadence')
  check(scalar(text,'waveRestSeconds')==0,name+' no automatic wave pauses')
  check(1<=scalar(text,'groupSize')<=3,name+' small groups')
  check(scalar(text,'groupRestSeconds')==0,name+' no automatic group pauses')
  check(7<=scalar(text,'maximumAlive')<=8,name+' bounded larger crowd')
  check(scalar(text,'minimumVisibleSpawnDistance')>=5.5,name+' safe visible emergence distance')
  check(scalar(text,'visibleSpawnWarningSeconds')>=.35,name+' telegraph remains readable')
  check(scalar(text,'killTarget' if name=='Kill50' else 'rescueEnemySupply')==total,name+' finite supply')
  missions_data[name]={'supply':total,'spawnInterval':scalar(text,'spawnInterval'),'waveRest':scalar(text,'waveRestSeconds')}
 weapon=(ROOT/'Assets/IntercomMVP/Content/Weapons/Pistols/Settings/DualPistols.asset').read_text()
 check(scalar(weapon,'magazineSize')==24,'shared authored base capacity stays24')
 protection=json.loads((ROOT/'Validation/Reports/ProtectedHero.json').read_text())
 for item in protection['files']:
  p=ROOT/item['path'];check(p.is_file(),'protected source exists '+item['path'])
  check(hashlib.sha256(p.read_bytes()).hexdigest()==item['sha256'],'protected source unchanged '+item['path'])
 result={'status':'PASS','checks':checks,'progressionComponent':progression,'missionProfiles':missions_data,'protectedHeroFiles':len(protection['files']),'scope':'Serialized owner links, authored tuning and byte preservation. Not Unity import, rendering, Play Mode or Profiler.'}
 (ROOT/'Validation/Reports/FoundationScene.json').write_text(json.dumps(result,indent=2)+'\n')
 print(json.dumps(result))
if __name__=='__main__':main()
