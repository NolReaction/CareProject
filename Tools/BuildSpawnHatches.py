#!/usr/bin/env python3
"""Build small shared hatch/ground-warning meshes. Does not rewrite the scene."""
from pathlib import Path
import math
import BuildTutorialDistrict as build

ROOT=Path(__file__).resolve().parents[1]
MESHES=ROOT/'Assets/IntercomMVP/Content/Enemies/Spawns/Meshes'
HORIZONTAL_SCALE=1.4 # A 1.54m opening clears the authored robot's helmet and tucked shoulders.

def annulus(mesh,outer,inner,y,material,sides=24):
    for i in range(sides):
        a=math.tau*i/sides;b=math.tau*(i+1)/sides
        mesh.polygon([(outer*math.cos(a),y,outer*math.sin(a)),(outer*math.cos(b),y,outer*math.sin(b)),(inner*math.cos(b),y,inner*math.sin(b)),(inner*math.cos(a),y,inner*math.sin(a))],material,(0,1,0))

def generate():
    MESHES.mkdir(parents=True,exist_ok=True)
    previous=build.MESHES;build.MESHES=MESHES
    try:
        rim=build.Mesh('HatchRim')
        rim.cylinder((0,.013,0),.56,.018,'Roof',24)
        annulus(rim,.66,.55,.036,'Trim')
        for x,z in [(-.44,-.44),(.44,-.44),(-.44,.44),(.44,.44)]:rim.box((x,.06,z),(.085,.055,.085),'Trim',.012)
        for x in [-.17,.17]:rim.box((x,.049,-.59),(.12,.08,.13),'BlueGrey',.014)
        lid=build.Mesh('HatchLid')
        lid.cylinder((0,.065,.5),.525,.055,'BlueGrey',24)
        for x in [-.27,-.135,0,.135,.27]:
            # Short recessed-looking bars form a readable hatch grille.
            length=.62 if abs(x)<.2 else .46
            lid.box((x,.096,.50),(.037,.014,length),'Roof',.004)
        lid.box((0,.125,.75),(.25,.05,.065),'Trim',.012)
        warning=build.Mesh('SpawnWarningRing');annulus(warning,.79,.72,.018,'Warning',24)
        for mesh in [rim,lid,warning]:
            for vertex in mesh.vertices:
                vertex[0]*=HORIZONTAL_SCALE;vertex[2]*=HORIZONTAL_SCALE
                # Non-uniform position scaling uses the inverse transpose for normals.
                vertex[3]/=HORIZONTAL_SCALE;vertex[5]/=HORIZONTAL_SCALE
                length=math.sqrt(sum(v*v for v in vertex[3:6]))
                vertex[3:6]=[v/length for v in vertex[3:6]]
        meshes={m.name:m.export() for m in [rim,lid,warning]}
        print('Built shared spawn meshes:',', '.join(meshes))
        return meshes
    finally:build.MESHES=previous

if __name__=='__main__':generate()
