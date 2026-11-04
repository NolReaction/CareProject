"""Check V7 courtyard connectivity, low central cover and folding bumper geometry.

This validates authored geometry, not Unity gameplay, lighting or device performance.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import sys
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools'))
import BuildTutorialDistrict as build
from UnitySceneIds import validate_scene_ids


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline-layout',type=Path,help='Optional previous delivered DistrictLayout.json for a measured comparison.')
    args=parser.parse_args()
    layout=json.loads(build.LAYOUT.read_text())
    paths=build.clearance(layout)
    assert paths['reachableFreeCells']==paths['freeCells'],'Unreachable walkable pocket'
    assert all(paths['checkedWaypoints'].values())
    tall=[]
    for group in layout['groups']:
        for block in group['blocks']:
            if not block['collider']:continue
            scale=np.array(block['size'])
            center=np.array(block.get('collisionCenter',[0,0,0]))*scale
            size=np.array(block.get('collisionSize',[1,1,1]))*scale
            position=np.array(group['position'])+np.array(block['position'])+np.array(build.rotate(center,block['yaw']))
            angle=math.radians(block['yaw'])
            hx=(abs(math.cos(angle))*size[0]+abs(math.sin(angle))*size[2])/2
            hz=(abs(math.sin(angle))*size[0]+abs(math.cos(angle))*size[2])/2
            if position[1]+size[1]/2>1.3 and abs(position[0])<8+hx and -9-hz<position[2]<8+hz:
                tall.append(block['name'])
    assert not tall,tall
    interactions=[]
    for group in layout['groups']:
        spec=group.get('interaction')
        if not spec:continue
        block=next(b for b in group['blocks'] if 'Collision - '+b['name']==spec['collision'])
        top=block['position'][1]+(block['collisionCenter'][1]+block['collisionSize'][1]/2)*block['size'][1]
        assert 1.1<=top<=1.2,(group['key'],top)
        if spec['kind']=='Crate':
            spent=next(b for b in group['blocks'] if b['name']==spec['spentVisual'])
            assert not spent['active'] and not spent['collider']
        interactions.append({'key':group['key'],'kind':spec['kind'],'visibleTopMeters':top})
    assert len(interactions)==3
    build.build_modules()
    vertices=np.asarray(build.MODULES['SafetyPunchPost'].vertices)[:,:3]
    minimums=[]
    for degrees in np.linspace(0,78,79):
        angle=math.radians(degrees)
        for azimuth in np.linspace(0,math.tau,73):
            axis=np.array([math.cos(azimuth),0,math.sin(azimuth)])
            rotated=vertices*math.cos(angle)+np.cross(axis,vertices)*math.sin(angle)+(vertices@axis)[:,None]*axis*(1-math.cos(angle))
            minimums.append(float(rotated[:,1].min()+.46*math.sin(angle)/math.sin(math.radians(78))))
    assert min(minimums)>=-1e-6
    assert max(minimums)<.06
    ids=validate_scene_ids(build.SCENE.read_text())
    report={
        'revision':'open-courtyard-v7',
        'routes':paths['checkedWaypoints'],
        'allFreeCellsReachable':True,'gridMeters':paths['gridMeters'],
        'playerRadiusPlusMarginMeters':paths['inflatedPlayerRadius'],
        'reachableFreeCells':paths['reachableFreeCells'],
        'conservativeWalkableAreaM2':round(paths['reachableFreeCells']*.2*.2,2),
        'aboveGroundObstacleCount':len(paths['obstacleAabbs']),
        'centralZone':{'x':[-8,8],'z':[-9,8],'occludersTallerThan1_3m':tall},
        'reactiveProps':interactions,
        'foldedBumper':{'azimuthSamples':73,'angleSamples':79,'foldLiftMeters':.46,
                        'minimumVertexHeightRangeMeters':[min(minimums),max(minimums)],'floorPenetration':False},
        'sceneIdsAndReferences':ids,
        'sceneSha256':hashlib.sha256(build.SCENE.read_bytes()).hexdigest(),
        'scope':'Authored meshes, conservative 2D routes and scene references. No Unity Editor or device playtest.'}
    if args.baseline_layout:
        previous=build.clearance(json.loads(args.baseline_layout.read_text()))
        report['comparison']={'baseline':'Delivered V6.1 DistrictLayout.json',
                              'baselineSha256':hashlib.sha256(args.baseline_layout.read_bytes()).hexdigest(),
                              'beforeReachableFreeCells':previous['reachableFreeCells'],
                              'afterReachableFreeCells':paths['reachableFreeCells'],
                              'reachableSpaceIncreasePercent':round(100*(paths['reachableFreeCells']/previous['reachableFreeCells']-1),3),
                              'beforeObstacles':len(previous['obstacleAabbs']),
                              'afterObstacles':len(paths['obstacleAabbs'])}
    output=ROOT/'Validation/CourtyardReview.json'
    output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(report,ensure_ascii=False,indent=2))


if __name__=='__main__':main()
