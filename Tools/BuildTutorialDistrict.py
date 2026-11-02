#!/usr/bin/env python3
"""Build the authored tutorial courtyard meshes and layout; no external art needed.

python Tools/BuildTutorialDistrict.py --apply-scene
The explicit flag replaces ONLY children of MVP_TestEnvironment in the supplied
scene. Robot, camera, lights and unrelated root objects are preserved. Commit or
back up manual environment edits before regenerating. Requires Python 3 + NumPy.
Render the same meshes with Blender using --render-json (see Tools/RenderTutorialDistrict.py).
"""
from pathlib import Path
import argparse, collections, hashlib, json, math, re, struct, uuid, zlib
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'Assets/IntercomMVP/Art/PrototypeEnvironment'
MESHES = ART / 'Modules'
TEXTURES = ART / 'Surfaces'
LAYOUT = ROOT / 'Assets/IntercomMVP/World/DistrictLayout.json'
SCENE = ROOT / 'Assets/IntercomMVP/Scenes/IntercomMVP.unity'
SALT = 'CareProject/tutorial-courtyard/v2/'

def stable(path): return uuid.uuid5(uuid.NAMESPACE_URL, SALT + str(path)).hex

def meta(path, kind='NativeFormatImporter', main=2100000):
    p=Path(str(path)+'.meta')
    if p.exists(): return re.search(r'^guid: (\w+)',p.read_text(),re.M)[1]
    guid=stable(path.relative_to(ROOT).as_posix())
    if path.is_dir(): content=f'fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    else: content=f'fileFormatVersion: 2\nguid: {guid}\n{kind}:\n  externalObjects: {{}}\n  mainObjectFileID: {main}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    p.write_text('\n'.join(line.rstrip() for line in content.splitlines())+'\n'); return guid

def v3(v): return '{x: %.8g, y: %.8g, z: %.8g}' % tuple(v)
def v4(v): return '{x: %.8g, y: %.8g, z: %.8g, w: %.8g}' % tuple(v)
def yawq(a): return (0,math.sin(math.radians(a)/2),0,math.cos(math.radians(a)/2))
def rotate(v,yaw):
    c=math.cos(math.radians(yaw));s=math.sin(math.radians(yaw));x,y,z=v
    return (c*x+s*z,y,-s*x+c*z)

class Mesh:
    def __init__(self,name):self.name=name;self.vertices=[];self.faces=[];self.materials=[]
    def polygon(self,pts,mat,normal=None):
        if mat not in self.materials:self.materials.append(mat)
        pts=[np.array(p,dtype=float) for p in pts]
        n=np.cross(pts[1]-pts[0],pts[2]-pts[0]);n/=max(1e-9,np.linalg.norm(n))
        if normal is not None and np.dot(n,normal)<0:pts.reverse();n=-n
        axis=int(np.argmax(np.abs(n)));uvaxes=[k for k in range(3) if k!=axis]
        first=len(self.vertices)
        for p in pts:self.vertices.append([*p,*n,p[uvaxes[0]],p[uvaxes[1]]])
        for j in range(1,len(pts)-1):self.faces.append(([first,first+j,first+j+1],self.materials.index(mat)))
    def box(self,pos,size,mat,bevel=.025,yaw=0):
        h=np.array(size)/2;b=min(bevel,float(min(h))*.45)
        transform=lambda p:np.array(rotate(p,yaw))+np.array(pos)
        # Six face panels, then twelve chamfer strips and eight corner triangles.
        for axis in range(3):
            others=[k for k in range(3) if k!=axis]
            for sign in [-1,1]:
                pts=[]
                for a,c in [(-1,-1),(1,-1),(1,1),(-1,1)]:
                    q=np.zeros(3);q[axis]=sign*h[axis];q[others[0]]=a*(h[others[0]]-b);q[others[1]]=c*(h[others[1]]-b);pts.append(transform(q))
                normal=np.zeros(3);normal[axis]=sign
                self.polygon(pts,mat,rotate(normal,yaw))
        if b<=0:return
        for edge in range(3):
            a,c=[k for k in range(3) if k!=edge]
            for sa in [-1,1]:
                for sc in [-1,1]:
                    pts=[]
                    for se,outer in [(-1,a),(1,a),(1,c),(-1,c)]:
                        q=np.zeros(3);q[edge]=se*(h[edge]-b);q[a]=sa*(h[a] if outer==a else h[a]-b);q[c]=sc*(h[c] if outer==c else h[c]-b);pts.append(transform(q))
                    normal=np.zeros(3);normal[a]=sa;normal[c]=sc
                    self.polygon(pts,mat,rotate(normal,yaw))
        for sx in [-1,1]:
            for sy in [-1,1]:
                for sz in [-1,1]:
                    signs=np.array([sx,sy,sz]);pts=[]
                    for axis in range(3):
                        q=signs*(h-b);q[axis]=signs[axis]*h[axis];pts.append(transform(q))
                    self.polygon(pts,mat,rotate(signs,yaw))
    def cylinder(self,pos,radius,height,mat,sides=12,axis='y'):
        def trans(x,y,z):
            q=(x,y,z) if axis=='y' else ((y,x,z) if axis=='x' else (x,z,y))
            return np.array(q)+np.array(pos)
        for i in range(sides):
            a=math.tau*i/sides;b=math.tau*(i+1)/sides
            pts=[trans(radius*math.cos(t),y,radius*math.sin(t)) for t,y in [(a,-height/2),(b,-height/2),(b,height/2),(a,height/2)]]
            normal=np.mean(pts,axis=0)-np.array(pos)
            self.polygon(pts,mat,normal)
        for sign in [-1,1]:
            pts=[trans(radius*math.cos(math.tau*i/sides),sign*height/2,radius*math.sin(math.tau*i/sides)) for i in range(sides)]
            normal=np.zeros(3);normal['xyz'.index(axis)]=sign
            self.polygon(pts,mat,normal)
    def pipe_elbow(self,pos,radius,tube,mat):
        # Quarter arc in XY; ten segments / eight sides, shared module geometry.
        for i in range(10):
            for j in range(8):
                pts=[]
                for a,t in [(i,j),(i+1,j),(i+1,j+1),(i,j+1)]:
                    u=a*math.pi/20;v=t*math.tau/8
                    pts.append(np.array(pos)+[(radius+tube*math.cos(v))*math.cos(u),(radius+tube*math.cos(v))*math.sin(u),tube*math.sin(v)])
                u=(i+.5)*math.pi/20;v=(j+.5)*math.tau/8
                self.polygon(pts,mat,[math.cos(u)*math.cos(v),math.sin(u)*math.cos(v),math.sin(v)])
    def bounds(self,verts=None):
        a=np.array(verts if verts is not None else self.vertices)[:,:3]
        lo=a.min(axis=0);hi=a.max(axis=0);return ((lo+hi)/2,(hi-lo)/2)
    def export(self):
        assert len(self.vertices)<65536
        path=MESHES/(self.name+'.mesh');center,extent=self.bounds()
        binary=b''.join(struct.pack('<8f',*v) for v in self.vertices)
        grouped=[];sub=[]
        for mi in range(len(self.materials)):
            indices=[i for tri,m in self.faces if m==mi for i in tri]
            c,e=self.bounds([self.vertices[i] for i in indices]);sub.append(f'''  - serializedVersion: 2\n    firstByte: {len(grouped)*2}\n    indexCount: {len(indices)}\n    topology: 0\n    baseVertex: 0\n    firstVertex: {min(indices)}\n    vertexCount: {max(indices)-min(indices)+1}\n    localAABB:\n      m_Center: {v3(c)}\n      m_Extent: {v3(e)}\n''');grouped+=indices
        channels=''
        for i in range(14):
            dim,offset={0:(3,0),1:(3,12),4:(2,24)}.get(i,(0,0))
            channels+=f'    - stream: 0\n      offset: {offset}\n      format: 0\n      dimension: {dim}\n'
        empty=''
        for name in ['Vertices','UV','Normals','Tangents','Weights','NormalSigns','TangentSigns','FloatColors','BoneIndices','Triangles']:
            empty+=f'    m_{name}:\n      m_NumItems: 0\n'
            if name in ['Vertices','UV','Normals','Tangents','FloatColors']:empty+='      m_Range: 0\n      m_Start: 0\n'
            empty+='      m_Data: \n      m_BitSize: 0\n'
        text=f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!43 &4300000
Mesh:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {self.name}
  serializedVersion: 11
  m_SubMeshes:
{''.join(sub)}  m_Shapes:
    vertices: []
    shapes: []
    channels: []
    fullWeights: []
  m_BindPose: []
  m_BoneNameHashes:
  m_RootBoneNameHash: 0
  m_BonesAABB: []
  m_VariableBoneCountWeights:
    m_Data:
  m_MeshCompression: 0
  m_IsReadable: 1
  m_KeepVertices: 1
  m_KeepIndices: 1
  m_IndexFormat: 0
  m_IndexBuffer: {struct.pack('<'+'H'*len(grouped),*grouped).hex()}
  m_VertexData:
    serializedVersion: 3
    m_VertexCount: {len(self.vertices)}
    m_Channels:
{channels}    m_DataSize: {len(binary)}
    _typelessdata: {binary.hex()}
  m_CompressedMesh:
{empty}    m_UVInfo: 0
  m_LocalAABB:
    m_Center: {v3(center)}
    m_Extent: {v3(extent)}
  m_MeshUsageFlags: 0
  m_CookingOptions: 30
  m_BakedConvexCollisionMesh:
  m_BakedTriangleCollisionMesh:
  m_MeshMetrics[0]: 1
  m_MeshMetrics[1]: 1
  m_MeshOptimizationFlags: 1
  m_StreamData:
    serializedVersion: 2
    offset: 0
    size: 0
    path:
'''
        path.write_text('\n'.join(line.rstrip() for line in text.splitlines())+'\n');meta(path,main=4300000)
        return {'name':self.name,'vertices':self.vertices,'faces':self.faces,'materials':self.materials,'triangles':len(self.faces)}

MATERIALS={
 'Asphalt':((.29,.31,.31),0,.12,'AsphaltGrain'), 'Concrete':((.65,.64,.57),0,.12,'ConcreteGrain'),
 'Backdrop':((.25,.29,.27),0,.1,'AsphaltGrain'),'BlueGrey':((.27,.44,.43),.3,.28,'MetalWeathering'),
 'Roof':((.22,.31,.34),.45,.32,'MetalWeathering'),'Orange':((.59,.27,.14),.35,.22,'MetalWeathering'),
 'Trim':((.57,.59,.54),.4,.3,'MetalWeathering'),'Door':((.14,.20,.21),.4,.24,'MetalWeathering'),
 'Paint':((.87,.66,.24),.05,.2,None),'Crate':((.47,.35,.20),0,.12,'WoodGrain'),
 'Moss':((.29,.39,.19),0,.04,None),'Gravel':((.40,.39,.34),0,.1,'ConcreteGrain'),
 'TargetCoral':((.76,.33,.18),.25,.25,None),'TargetMint':((.34,.60,.53),.25,.25,None),
 'Lamp':((.93,.75,.40),.1,.2,None),'Rubber':((.10,.13,.13),0,.12,None),
}

def png(path,array):
    a=np.uint8(np.clip(array,0,1)*255);h,w,c=a.shape
    raw=b''.join(b'\x00'+a[y].tobytes() for y in range(h))
    chunk=lambda name,data:struct.pack('>I',len(data))+name+data+struct.pack('>I',zlib.crc32(name+data)&0xffffffff)
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(raw,9))+chunk(b'IEND',b''))

def make_materials():
    TEXTURES.mkdir(exist_ok=True);meta(TEXTURES)
    rng=np.random.default_rng(86132);n=256;y,x=np.mgrid[0:n,0:n];u=x/n;v=y/n
    low=np.zeros((n,n))
    for k in range(28):
        a,b=rng.integers(1,13,2);b*=rng.choice([-1,1]);low+=np.sin(math.tau*(a*u+b*v)+rng.uniform(0,math.tau))/math.sqrt(a*a+b*b)
    low=low/np.max(np.abs(low));fine=rng.normal(0,1,(n,n))
    fields={'AsphaltGrain':.80+.035*low+.035*fine,'ConcreteGrain':.88+.035*low+.014*fine,
            'MetalWeathering':.86+.045*low+.018*fine-.035*(np.cos(math.tau*16*u)>.92),
            'WoodGrain':.84+.055*np.sin(math.tau*(12*u+.1*low))+.025*fine}
    for name,f in fields.items():
        path=TEXTURES/(name+'.png');png(path,np.repeat(np.clip(f,.58,.99)[...,None],3,axis=2))
        # Existing texture importer is the project's stable compatible reference.
        t=(ROOT/'Assets/IntercomMVP/Art/Robot/Textures/tripo_part_0_base_color.jpg.meta').read_text()
        t=re.sub(r'^guid: \w+',f'guid: {stable(path.relative_to(ROOT).as_posix())}',t,flags=re.M)
        t=re.sub(r'(maxTextureSize:) \d+',r'\1 512',t)
        t=re.sub(r'(textureCompression:) \d+',r'\1 1',t)
        t=re.sub(r'(wrap[UVW]:) \d+',r'\1 0',t)
        Path(str(path)+'.meta').write_text('\n'.join(line.rstrip() for line in t.splitlines())+'\n')
    template=(ART/'Asphalt.mat').read_text()
    for prop in ['_BaseMap','_MainTex']:
        template=re.sub(r'(- '+prop+r':\n        m_Texture:) \{[^}]+\}',r'\1 {fileID: 0}',template)
        template=re.sub(r'(- '+prop+r':\n        m_Texture: [^\n]+\n        m_Scale:) \{[^}]+\}',r'\1 {x: 1, y: 1}',template)
    for name,(color,metal,smooth,texture) in MATERIALS.items():
        path=ART/(name+'.mat');t=template
        t=re.sub(r'(  m_Name:) Asphalt',r'\1 '+name,t)
        t=t.replace('m_EnableInstancingVariants: 0','m_EnableInstancingVariants: 1')
        for key,value in [('_Metallic',metal),('_Smoothness',smooth)]:t=re.sub(r'(- '+key+r':) [\d.]+',r'\g<1> '+str(value),t)
        colorstr='{r: %s, g: %s, b: %s, a: 1}'%color
        t=re.sub(r'(- _(BaseColor|Color):) \{[^}]+\}',lambda m:m[1]+' '+colorstr,t)
        if texture:
            guid=meta(TEXTURES/(texture+'.png'))
            for prop in ['_BaseMap','_MainTex']:
                t=t.replace(f'- {prop}:\n        m_Texture: {{fileID: 0}}',f'- {prop}:\n        m_Texture: {{fileID: 2800000, guid: {guid}, type: 3}}')
        if name in ['Asphalt','Backdrop','Concrete','Gravel']:
            tile={'Asphalt':18,'Backdrop':18,'Concrete':2,'Gravel':5}[name]
            for prop in ['_BaseMap','_MainTex']:
                t=re.sub(r'(- '+prop+r':\n        m_Texture: [^\n]+\n        m_Scale:) \{[^}]+\}',lambda m:m[1]+f' {{x: {tile}, y: {tile}}}',t)
        if name=='Lamp':
            t=t.replace('m_ValidKeywords: []','m_ValidKeywords:\n  - _EMISSION')
            t=t.replace('- _EmissionColor: {r: 0, g: 0, b: 0, a: 1}','- _EmissionColor: {r: 0.5, g: 0.29, b: 0.08, a: 1}')
        path.write_text('\n'.join(line.rstrip() for line in t.splitlines())+'\n');meta(path)

MODULES={}
def mesh(name):m=Mesh(name);MODULES[name]=m;return m

# Authoring metadata, serialized onto colliders. Runtime never guesses from names or colours.
SURFACE_KINDS=['Asphalt','Concrete','Metal','Gravel','Wood','Soil']
MODULE_SURFACES={
    'Warehouse':'Metal','Workshop':'Metal','CargoCrate':'Wood','SteelDrum':'Metal',
    'CableSpool':'Wood','Transformer':'Metal','CargoContainer':'Metal',
    'JerseyBarrier':'Concrete','WallPanel':'Concrete','GatePanel':'Metal',
    'Planter':'Concrete','Bollard':'Metal','TrainingTarget':'Metal','FencePanel':'Metal',
}
MATERIAL_SURFACES={'Concrete':'Concrete','Asphalt':'Asphalt','Gravel':'Gravel',
                   'Roof':'Metal','Crate':'Wood','Moss':'Soil'}

def configure_surface_responses(layout):
    for group in layout['groups']:
        for block in group['blocks']:
            if not block['collider']:continue
            if not block.get('footstepSurface'):
                block['footstepSurface']=(MODULE_SURFACES[block['mesh']] if block.get('mesh')
                                          else MATERIAL_SURFACES[block['material']])
    for target,kind,material in zip(layout['targets'],['Metal','Wood','Concrete'],['TargetCoral','Crate','Concrete']):
        target['footstepSurface']=kind;target['material']=material
    layout['surfaceVersion']=2
    return layout

def surface_fields(kind):
    return f'  kind: {SURFACE_KINDS.index(kind)}\n  volume: 1\n  pitch: 1\n  impactVolume: 1\n  impactPitch: 1\n'

def build_modules():
    m=mesh('CargoCrate')
    m.box((0,.55,0),(1.05,1.1,1.05),'Crate',.045)
    for z in [-.54,.54]:
        for x in [-.40,.40]:m.box((x,.55,z),(.12,1.08,.07),'Trim',.012)
        for y in [.13,.96]:m.box((0,y,z),(1.08,.10,.08),'Crate',.01)
    for x in [-.54,.54]:
        for y in [.13,.96]:m.box((x,y,0),(.07,.10,1.08),'Crate',.01)
    m=mesh('SteelDrum');m.cylinder((0,.43,0),.30,.84,'BlueGrey',16)
    for y in [.08,.76]:m.cylinder((0,y,0),.316,.045,'Trim',16)
    m.cylinder((0,.864,0),.27,.025,'Roof',16);m.cylinder((.13,.887,0),.043,.025,'Trim',8)
    m=mesh('JerseyBarrier')
    # Tapered physical cover with thick feet and visible painted safety patches.
    m.box((0,.17,0),(2.1,.34,.60),'Concrete',.06)
    m.box((0,.53,0),(2.0,.44,.38),'Concrete',.05)
    m.box((0,.82,0),(2.0,.16,.28),'Concrete',.035)
    for x in [-.75,.75]:
        for z in [-.205,.205]:m.box((x,.55,z),(.22,.32,.025),'Paint',.005,yaw=0)
    for x in [-.8,.8]:m.box((x,.92,0),(.10,.12,.06),'Trim',.015)
    m=mesh('CargoContainer');m.box((0,1.18,0),(4.6,2.35,2.25),'Orange',.055)
    for x in np.linspace(-2.12,2.12,18):
        for z in [-1.142,1.142]:m.box((x,1.16,z),(.055,2.13,.045),'Orange',.008)
        m.box((x,2.37,0),(.055,.045,2.17),'Trim',.006)
    for y in [.10,2.27]:m.box((0,y,-1.15),(4.5,.12,.06),'Trim',.01)
    for z in [-.97,0,.97]:m.box((2.33,1.17,z),(.06,2.2,.055),'Trim',.01)
    m=mesh('FencePanel')
    for x in [-1.5,1.5]:m.box((x,1.05,0),(.10,2.1,.10),'Roof',.015)
    for y in [.30,1.92]:m.box((0,y,0),(3,.07,.07),'Trim',.008)
    for x in np.linspace(-1.4,1.4,15):m.box((x,1.08,0),(.035,1.62,.035),'Trim',0)
    m=mesh('WallPanel');m.box((0,.42,0),(3,.84,.32),'Concrete',.045)
    m.box((0,.88,0),(3.07,.12,.40),'Trim',.025)
    m=mesh('GatePanel')
    for x in [-1.45,1.45]:m.box((x,1.06,0),(.12,2.12,.12),'Roof',.015)
    for y in [.18,1.96]:m.box((0,y,0),(3,.12,.12),'Roof',.015)
    for x in np.linspace(-1.3,1.3,14):m.box((x,1.08,0),(.045,1.7,.06),'Trim',0)
    m=mesh('Transformer');m.box((0,.12,0),(1.75,.24,1.6),'Concrete',.045)
    m.box((0,.88,0),(1.45,1.35,1.23),'BlueGrey',.07)
    for x in np.linspace(-.61,.61,9):
        for z in [-.66,.66]:m.box((x,.95,z),(.045,1.0,.18),'Trim',.01)
    for x in [-.4,0,.4]:
        m.cylinder((x,1.73,0),.09,.5,'Door',10)
        for y in [1.55,1.68,1.81]:m.cylinder((x,y,0),.14,.065,'Roof',10)
    m.box((0,1.04,-.738),(.30,.36,.03),'Paint',.01)
    m=mesh('UtilityPipes')
    for z in [-.28,.28]:
        m.cylinder((0,1.65,z),.14,3.0,'Trim',12,'x')
        m.cylinder((-1.45,.83,z),.14,1.65,'Orange',12)
        m.pipe_elbow((-1.14,1.34,z),.31,.14,'Trim')
        for x in [-.8,.8]:m.cylinder((x,1.65,z),.19,.10,'Door',12,'x')
    for x in [-1.1,1.1]:m.box((x,.8,0),(.10,1.6,.8),'Roof',.01)
    m=mesh('CableSpool');m.cylinder((0,.39,0),.22,.60,'Rubber',16)
    for y in [.10,.70]:m.cylinder((0,y,0),.49,.11,'Crate',16)
    m.cylinder((0,.77,0),.07,.03,'Door',8)
    m=mesh('Bollard');m.cylinder((0,.43,0),.095,.86,'Paint',10)
    for y in [.25,.55]:m.cylinder((0,y,0),.098,.13,'Door',10)
    m.cylinder((0,.045,0),.16,.09,'Roof',10)
    m=mesh('Planter');m.box((0,.21,0),(1.6,.42,.75),'Concrete',.045)
    m.box((0,.43,0),(1.4,.05,.55),'Door',.01)
    for x,z,h in [(-.52,-.1,.33),(-.15,.08,.46),(.25,-.06,.35),(.51,.1,.4)]:
        m.box((x,.44+h/2,z),(.34,h,.31),'Moss',.13,yaw=x*65)
    m=mesh('TrainingStand');m.cylinder((0,.10,0),.43,.2,'Roof',16)
    m.box((0,.71,0),(.14,1.18,.12),'Trim',.02)
    m=mesh('TrainingTarget');m.cylinder((0,1.18,0),.47,.16,'TargetCoral',20,'z')
    for r,y in [(.31,-.095),(.12,-.11)]:m.cylinder((0,1.18,y),r,.025,'Paint',20,'z')
    # Buildings are modular shells: no enterable interiors; apron remains playable.
    for name,w,d,h,wall in [('Warehouse',10.5,5.1,4.1,'BlueGrey'),('Workshop',6.7,5.2,3.15,'BlueGrey')]:
        m=mesh(name);m.box((0,h/2,0),(w,h,d),wall,.075)
        m.box((0,.16,0),(w+.15,.32,d+.15),'Concrete',.04)
        m.box((0,h+.08,0),(w+.35,.18,d+.35),'Roof',.035)
        for x in np.arange(-w/2+.25,w/2,.58):m.box((x,h/2,-d/2-.025),(.055,h-.25,.05),'Trim',.008)
        # Deep dark doors read as recessed work bays; frame and awning provide depth.
        bays=[(-2.5,2.7),(1.6,2.7)] if name=='Warehouse' else [(0,3.5)]
        for x,bw in bays:
            m.box((x,1.26,-d/2-.064),(bw,2.38,.08),'Door',.025)
            for bx in [x-bw/2-.10,x+bw/2+.10]:m.box((bx,1.3,-d/2-.13),(.13,2.6,.15),'Trim',.015)
            for ly in [.45,.72,1.0,1.28,1.56,1.84,2.1]:m.box((x,ly,-d/2-.11),(bw-.06,.035,.03),'Roof',.005)
            m.box((x,2.75,-d/2-.18),(bw+.4,.13,.22),'Trim',.015)
            m.box((x,2.96,-d/2-.76),(bw+.75,.11,1.40),'Roof',.02)
            for bx in [x-bw/2,x+bw/2]:m.box((bx,2.3,-d/2-.52),(.065,.9,.065),'Trim',.012,yaw=0)
        for x in [-w*.41,w*.41]:
            m.box((x,h-.5,-d/2-.07),(.42,.34,.08),'Door',.015)
            m.box((x,h-.5,-d/2-.125),(.25,.17,.035),'Lamp',.018)
        for x in [-w/2+.15,w/2-.15]:m.cylinder((x,h/2,-d/2-.15),.065,h,'Orange',10)
        for x in np.arange(-w/2+.5,w/2,.65):m.box((x,h+.19,0),(.045,.06,d+.18),'Trim',.008)
        # Roof utility boxes and a turbine.
        m.box((w*.25,h+.46,.6),(1.2,.6,1.1),'Trim',.06)
        for z in np.linspace(.22,.97,5):m.box((w*.25,h+.48,z),(1.04,.035,.04),'Door',.006)
        m.cylinder((-w*.27,h+.40,.45),.32,.45,'Trim',16)
        m.cylinder((-w*.27,h+.66,.45),.42,.10,'Roof',16)


def build_layout():
    groups=[]
    def group(key,name,pos,role='Prop',footprint=(1,1),hide=False):
        g={'key':key,'name':name,'position':list(pos),'role':role,'footprint':list(footprint),'hide':hide,'blocks':[]};groups.append(g);return g
    def module(g,name,pos,meshname,yaw=0,collider=False,size=None,surface=None):
        m=MODULES[meshname];c,e=m.bounds()
        spec={'name':name,'position':list(pos),'size':[1,1,1],'mesh':meshname,'materials':m.materials,'material':m.materials[0],'yaw':yaw,'shape':'Mesh','collider':collider,'shadow':True}
        if collider:spec['collisionCenter']=list(c);spec['collisionSize']=list(size or e*2)
        if surface:spec['footstepSurface']=surface
        g['blocks'].append(spec);return spec
    def box(g,name,pos,size,material,collider=False,shadow=False,surface=None,yaw=0):
        spec={'name':name,'position':list(pos),'size':list(size),'material':material,'yaw':yaw,'shape':'Cube','collider':collider,'shadow':shadow}
        if surface:spec['footstepSurface']=surface
        g['blocks'].append(spec);return spec
    # Ground details combined into just three meshes / renderers.
    m=mesh('YardMarkings')
    for z in np.arange(-12,12,2.8):
        m.box((-.1,.009,z),(.10,.008,1.15),'Paint',0)
    for x in np.arange(-12,13,2.8):m.box((x,.010,-2.8),(1.15,.008,.10),'Paint',0)
    for x,z,w,d in [(-9,6.7,11.3,2.2),(-11,-5,7.4,6.1),(10,-7.8,9,7),(-3,2,5.7,3.4)]:
        height=.060 if z!=2 else .016
        for xx in [x-w/2,x+w/2]:m.box((xx,height,z),(.10,.008,d),'Paint',0)
        for zz in [z-d/2,z+d/2]:m.box((x,height,zz),(w,.008,.1),'Paint',0)
    # Landing stripes at entrance and visibly separated practice lanes.
    for x in np.arange(-9.5,-4.5,.48):m.box((x,.016,-12.9),(.19,.012,1.1),'Paint',0,yaw=-30)
    for x in [8.1,10.6,13.1]:
        m.box((x,.060,-7.5),(.08,.008,4.4),'Paint',0)
        m.box((x+.8,.060,-9.7),(1.6,.008,.08),'Paint',0)
    # Light slab seams and inset drains add scale without filling the combat space.
    for x,z,w,d in [(-9,6.7,11.7,3.0),(-12,-5,8.4,7.0),(10.9,-8.2,9.8,7.2)]:
        for xx in np.arange(x-w/2+1.8,x+w/2,1.8):m.box((xx,.056,z),(.018,.004,d),'Trim',0)
        for zz in np.arange(z-d/2+1.8,z+d/2,1.8):m.box((x,.056,zz),(w,.004,.018),'Trim',0)
    for x,z in [(-7,-11.2),(-9,4.9),(4.5,-3.8),(14.5,3.9)]:
        m.box((x,.012,z),(.7,.018,.42),'Door',0)
        for dx in np.arange(-.3,.35,.10):m.box((x+dx,.026,z),(.035,.008,.38),'Trim',0)
    g=group('surface','01 - Pavement and lane markings',(0,0,0),'Route',(36,32))
    # Shared ground collider remains outside environment. Thin aprons have contact tags.
    for name,pos,size in [('Warehouse concrete apron',(-9,.025,6.7),(11.7,.05,3.0)),('Workshop concrete apron',(-12,.025,-5.0),(8.4,.05,7.0)),('Training concrete pad',(10.9,.025,-8.2),(9.8,.05,7.2)),('Substation slab',(11,.04,9.2),(8.5,.08,8.0))]:box(g,name,pos,size,'Concrete',True,False,'Concrete')
    box(g,'Gravel service strip',(-16.3,.022,3.2),(2,.044,5.4),'Gravel',True,False,'Gravel')
    box(g,'Metal service plate',(-6.0,.03,-9.9),(2,.06,1.2),'Roof',True,False,'Metal')
    module(g,'Faded yard markings',(0,0,0),'YardMarkings')
    g=group('depot','02 - Warehouse - replace shell',(-9,0,10.8),'Building',(10.5,5.1),True)
    shell=module(g,'Warehouse shell',(0,0,0),'Warehouse',collider=True);shell['collisionCenter']=[0,2.05,0];shell['collisionSize']=[10.5,4.1,5.1]
    module(g,'Loading cargo A',(-4,.05,-3.35),'CargoCrate',8,True)
    module(g,'Loading cargo B',(4,.05,-3.3),'CargoCrate',-8,True)
    module(g,'Loading drum',(3,.05,-3.25),'SteelDrum',collider=True)
    g=group('workshop','03 - Workshop - replace shell',(-12,0,-4.8),'Building',(6.7,5.2),True)
    shell=module(g,'Workshop shell',(0,0,0),'Workshop',collider=True);shell['collisionCenter']=[0,1.575,0];shell['collisionSize']=[6.7,3.15,5.2]
    module(g,'Workshop spool',(2.6,.05,-3.55),'CableSpool',collider=True)
    module(g,'Workshop drum',(3.55,.05,-2.9),'SteelDrum',collider=True)
    module(g,'Workshop crates',(-2.6,.05,-3.55),'CargoCrate',5,True)
    module(g,'Service pipes',(-3.65,.05,0),'UtilityPipes',90)
    g=group('substation','04 - Electrical substation',(11,0,9.2),'Building',(8.5,8.0),True)
    for x in [-1.8,1.8]:module(g,'Transformer '+str(x),(x,.08,0),'Transformer',collider=True)
    for x in [-2.9,.1,3.1]:
        module(g,'Rear fence '+str(x),(x,.08,3.8),'FencePanel')
        module(g,'Front fence '+str(x),(x,.08,-3.8),'FencePanel')
    for z in [-2.3,.7]:
        for x in [-4.3,4.3]:module(g,'Side fence '+str(x)+str(z),(x,.08,z),'FencePanel',90)
    # A single simple collider blocks substation internals, separated from fence renderers.
    box(g,'Substation collision',(0,1,0),(8.6,2,7.7),'Concrete',True,False,'Metal')['visual']=False
    module(g,'Substation pipes',(0,.08,2.4),'UtilityPipes')
    g=group('container','05 - Container cover',(-3,0,2),'Cover',(4.6,2.3),True)
    module(g,'Rust cargo container',(0,0,0),'CargoContainer',collider=True)
    module(g,'Container barrier',(-.5,0,-1.7),'JerseyBarrier',collider=True)
    module(g,'Container drum',(2.8,0,-.8),'SteelDrum',collider=True)
    for key,name,pos,yaw in [('cover_e','06 - East cover cluster',(6,0,-.4),0),('cover_s','07 - Entry cover cluster',(-3,0,-6.3),0),('cover_n','08 - North cover cluster',(1,0,7.5),0)]:
        g=group(key,name,pos,'Cover',(3.6,2.4))
        module(g,'Low barrier',(0,0,-.65),'JerseyBarrier',yaw,True)
        module(g,'Supply crate',(-.35,0,.35),'CargoCrate',yaw+10,True)
        module(g,'Supply drum',(1.3,0,.28),'SteelDrum',collider=True)
    g=group('training','09 - Training range',(10.9,0,-8.2),'Zone',(9.8,7.2))
    for x in [-3.4,0,3.4]:module(g,'Range backstop '+str(x),(x,.05,-3.4),'JerseyBarrier',collider=True)
    module(g,'Range side cover',(-4.7,.05,-1.4),'JerseyBarrier',90,True)
    module(g,'Range supplies',(4.2,.05,-2.5),'CargoCrate',collider=True)
    g=group('perimeter','10 - Perimeter and gates',(0,0,0),'Boundary',(36,32))
    def wall_run(label,start,end,fixed,along_x=True,fence=False):
        count=math.ceil((end-start)/3);length=(end-start)/count
        for i in range(count):
            mid=start+(i+.5)*length;pos=(mid,0,fixed) if along_x else (fixed,0,mid);yaw=0 if along_x else 90
            b=module(g,label+' '+str(i),pos,'WallPanel',yaw,True);b['size']=[length/3,1,1]
            if fence and (along_x or mid > 3):
                fp=(pos[0],.92,pos[2]);b=module(g,label+' fence '+str(i),fp,'FencePanel',yaw);b['size']=[length/3,1,1]
    wall_run('West wall',-15.55,15.55,-17.65,False,True)
    wall_run('East wall',-15.55,15.55,17.65,False,True)
    wall_run('Front west wall',-17.65,-8.6,-15.55)
    wall_run('Front east wall',-4.5,17.65,-15.55)
    wall_run('Rear west wall',-17.65,-2.5,15.55,True,True)
    wall_run('Rear east wall',3.5,17.65,15.55,True,True)
    b=module(g,'Entry closed gate',(-6.55,0,-15.55),'GatePanel',collider=True);b['size']=[4.1/3,1,1]
    b=module(g,'Exit closed gate',(.5,0,15.55),'GatePanel',collider=True);b['size']=[2,1,1]
    # Invisible boundary stops leave entry visually open without allowing a fall off the scene.
    for name,pos,size in [('West stop',(-18.1,1,0),(.3,2,33)),('East stop',(18.1,1,0),(.3,2,33)),('South stop',(0,1,-16.1),(36.5,2,.3)),('North stop',(0,1,16.1),(36.5,2,.3))]:box(g,name,pos,size,'Concrete',True)['visual']=False
    g=group('details','11 - Street furniture',(0,0,0),'Prop',(36,32))
    for x,z in [(-16,8),(-16,-10),(16,-10),(16,4),(-5,13),(6,13)]:module(g,'Planter '+str(x)+str(z),(x,0,z),'Planter',collider=True)
    for x,z in [(-8.6,-12.5),(-5.3,-12.5),(5.6,-4.3),(15.3,-4.3),(-3.4,6),(4.3,6)]:module(g,'Safety bollard '+str(x)+str(z),(x,0,z),'Bollard',collider=True)
    for key,pos,role in [('spawn',(-7,0,-12),'PlayerSpawn'),('exit',(.5,0,13.5),'Exit'),('wave_w',(-14.5,0,1),'EnemySpawn'),('wave_n',(3,0,11.5),'EnemySpawn'),('loot',(2.5,0,-3.0),'Loot')]:group(key,key.title()+' marker',pos,role)
    targets=[{'key':f'target_{i+1}','name':f'Training Target {i+1}','position':[x,.05,-8.7],'material':'TargetCoral','mesh':'TrainingTarget'} for i,x in enumerate([8.9,11.4,13.9])]
    return configure_surface_responses({'version':2,'width':36,'depth':32,'title':'Industrial courtyard - first tutorial location','groups':groups,'targets':targets})

class SceneWriter:
    def __init__(self,old):
        chunks=re.split(r'(?=^--- !u!)',old,flags=re.M);self.header=chunks[0]
        self.objects={int(re.match(r'--- !u!\d+ &(-?\d+)',c)[1]):c for c in chunks[1:]};self.next=8200000000;self.children={};self.components={};self.go={};self.transforms={};self.renderers={};self.target_ids=[]
        # Reuse scene templates for engine-version-specific renderer and collider fields.
        self.renderer_template=next(c for c in chunks if c.startswith('--- !u!23 '))
        self.collider_template=next(c for c in chunks if c.startswith('--- !u!65 '))
    def ident(self):self.next+=1;return self.next
    def obj(self,name,parent,pos=(0,0,0),scale=(1,1,1),yaw=0):
        go,tr=self.ident(),self.ident();self.go[go]=name;self.components[go]=[tr];self.transforms[tr]=(go,parent,pos,scale,yaw)
        self.children.setdefault(parent,[]).append(tr);self.children[tr]=[];return go,tr
    def common(self,go):return f'  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n'
    def component(self,go,klass,fields):
        cid=self.ident();self.components[go].append(cid);guid=meta(ROOT/f'Assets/IntercomMVP/Runtime/{klass}.cs')
        self.objects[cid]=f'--- !u!114 &{cid}\nMonoBehaviour:\n'+self.common(go)+f'  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: Assembly-CSharp::IntercomMvp.{klass}\n'+fields
        return cid
    def collider(self,go,center=(0,0,0),size=(1,1,1)):
        cid=self.ident();self.components[go].append(cid);s=self.collider_template
        s=re.sub(r'^--- !u!65 &\d+',f'--- !u!65 &{cid}',s)
        s=re.sub(r'm_GameObject: \{fileID: \d+\}',f'm_GameObject: {{fileID: {go}}}',s)
        s=re.sub(r'm_Size: \{[^}]+\}','m_Size: '+v3(size),s);s=re.sub(r'm_Center: \{[^}]+\}','m_Center: '+v3(center),s)
        self.objects[cid]=s;return cid
    def renderer(self,go,meshname,materials,shadow=True):
        mf,mr=self.ident(),self.ident();self.components[go]+=[mf,mr]
        meshref='{fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}' if meshname is None else '{fileID: 4300000, guid: '+meta(MESHES/(meshname+'.mesh'),main=4300000)+', type: 2}'
        self.objects[mf]=f'--- !u!33 &{mf}\nMeshFilter:\n'+self.common(go)+f'  m_Mesh: {meshref}\n'
        s=self.renderer_template;s=re.sub(r'^--- !u!23 &\d+',f'--- !u!23 &{mr}',s);s=re.sub(r'm_GameObject: \{fileID: \d+\}',f'm_GameObject: {{fileID: {go}}}',s)
        s=re.sub(r'm_CastShadows: \d+','m_CastShadows: '+str(int(shadow)),s)
        mats=''.join('  - {fileID: 2100000, guid: '+meta(ART/(m+'.mat'))+', type: 2}\n' for m in materials)
        s=re.sub(r'  m_Materials:\n(?:  - .*\n)+','  m_Materials:\n'+mats,s)
        self.objects[mr]=s;self.renderers[go]=mr;return mr
    def remove_old_map(self):
        root=1550949731;trs={root};changed=True
        while changed:
            changed=False
            for oid,c in self.objects.items():
                if not c.startswith('--- !u!4 '):continue
                m=re.search(r'm_Father: \{fileID: (\d+)\}',c)
                if m and int(m[1]) in trs and oid not in trs:trs.add(oid);changed=True
        gos={int(re.search(r'm_GameObject: \{fileID: (\d+)\}',self.objects[t])[1]) for t in trs if t!=root}
        removed=[]
        for oid,c in list(self.objects.items()):
            m=re.search(r'm_GameObject: \{fileID: (\d+)\}',c)
            if oid in gos or oid in trs-{root} or (m and int(m[1]) in gos):removed.append(oid);del self.objects[oid]
        # Retained environment root and marker are updated, not replaced.
        self.objects[6100000756]=re.sub(r'layoutVersion: \d+','layoutVersion: 2',self.objects[6100000756])
        self.objects[6100000756]=re.sub(r'surfaceVersion: \d+','surfaceVersion: 2',self.objects[6100000756])
        self.children[root]=[];return removed
    def finish(self):
        for go,name in self.go.items():
            cs=''.join(f'  - component: {{fileID: {c}}}\n' for c in self.components[go])
            self.objects[go]=f'''--- !u!1 &{go}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n  m_Component:\n{cs}  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n'''
        for tr,(go,parent,pos,scale,yaw) in self.transforms.items():
            ch='\n'+''.join(f'  - {{fileID: {c}}}\n' for c in self.children[tr]) if self.children[tr] else ' []\n'
            self.objects[tr]=f'--- !u!4 &{tr}\nTransform:\n'+self.common(go)+f'  serializedVersion: 2\n  m_LocalRotation: {v4(yawq(yaw))}\n  m_LocalPosition: {v3(pos)}\n  m_LocalScale: {v3(scale)}\n  m_ConstrainProportionsScale: 0\n  m_Children:'+ch+f'  m_Father: {{fileID: {parent}}}\n  m_LocalEulerAnglesHint: {v3((0,yaw,0))}\n'
        ch=''.join(f'  - {{fileID: {c}}}\n' for c in self.children[1550949731])
        self.objects[1550949731]=re.sub(r'  m_Children:\n(?:  - .*\n)+','  m_Children:\n'+ch,self.objects[1550949731])
        return self.header+''.join(('\n'.join(line.rstrip() for line in c.splitlines())+'\n') if oid >= 8200000000 else c for oid,c in self.objects.items())

def write_scene(layout):
    old=SCENE.read_text();w=SceneWriter(old);removed=w.remove_old_map()
    ground_renderer=next(oid for oid,c in w.objects.items() if c.startswith('--- !u!23 ') and 'm_GameObject: {fileID: 1981137698}' in c)
    asphalt=meta(ART/'Asphalt.mat')
    w.objects[ground_renderer]=re.sub(r'(  m_Materials:\n)(?:  - .*\n)+',r'\1  - {fileID: 2100000, guid: '+asphalt+', type: 2}\n',w.objects[ground_renderer])
    roles=['Zone','Building','Route','Cover','Prop','Boundary','PlayerSpawn','EnemySpawn','Loot','Exit','Target']
    for g in layout['groups']:
        go,tr=w.obj(g['name'],1550949731,g['position'])
        w.component(go,'IntercomWorldAnchor',f"  anchorId: {g['key']}\n  role: {roles.index(g['role'])}\n  footprint: {{x: {g['footprint'][0]}, y: {g['footprint'][1]}}}\n  designerNote: Replace visual module children; retain gameplay colliders and anchor.\n")
        w.component(go,'IntercomOccluder',f"  hideWhenBlocking: {int(g['hide'])}\n")
        for b in g['blocks']:
            obj,tf=w.obj(b['name'],tr,b['position'],b['size'],b['yaw'])
            if b.get('visual',True):w.renderer(obj,b.get('mesh'),b.get('materials',[b['material']]),b['shadow'])
            if b['collider']:
                # Colliders are separate siblings so later visual replacements preserve physical boundaries.
                col,ct=w.obj('Collision - '+b['name'],tr,b['position'],b['size'],b['yaw'])
                w.collider(col,b.get('collisionCenter',(0,0,0)),b.get('collisionSize',(1,1,1)))
                if b.get('footstepSurface'):w.component(col,'IntercomFootstepSurface',surface_fields(b['footstepSurface']))
    targets,tt=w.obj('12 - Training targets',1550949731)
    for spec in layout['targets']:
        go,tr=w.obj(spec['name'],tt,spec['position']);w.collider(go,(0,.85,0),(.95,1.7,.45))
        w.component(go,'IntercomFootstepSurface',surface_fields(spec.get('footstepSurface','Metal')))
        v,vt=w.obj('Visual - replace target',tr)
        stand,st=w.obj('Target stand',vt);w.renderer(stand,'TrainingStand',MODULES['TrainingStand'].materials)
        face,ft=w.obj('Target face',vt);mr=w.renderer(face,'TrainingTarget',[spec['material'],'Paint'])
        cid=w.component(go,'IntercomTargetDummy',f'  visual: {{fileID: {vt}}}\n  bodyRenderer: {{fileID: {mr}}}\n  maxHealth: 5\n  resetSeconds: 2.5\n')
        w.target_ids.append({'key':spec['key'],'name':spec['name'],'go':go,'transform':tr,'component':cid,'position':spec['position']})
    result=w.finish();SCENE.write_text(result)
    # Every unrelated serialized block must remain byte-identical.
    after=SceneWriter(result).objects;before=SceneWriter(old).objects
    changed=[oid for oid,c in before.items() if oid not in removed and after.get(oid)!=c]
    assert set(changed)<={1550949731,6100000756,ground_renderer},changed
    return {'environmentObjects':len(w.go),'renderers':len(w.renderers),'removedSerializedBlocks':len(removed),'preservedNonEnvironmentBlocks':len(before)-len(removed)-3,'groundRendererMaterialOnly':ground_renderer,'targets':w.target_ids}

def clearance(layout):
    # Rasterize conservative AABB obstacles expanded by the player's radius + margin.
    step=.20;radius=.42;xs=np.arange(-17.4,17.401,step);zs=np.arange(-15.4,15.401,step);blocked=np.zeros((len(zs),len(xs)),dtype=bool)
    obstacles=[]
    for g in layout['groups']:
        for b in g['blocks']:
            if not b['collider']:continue
            center=np.array(b.get('collisionCenter',[0,0,0]))*np.array(b['size']);size=np.array(b.get('collisionSize',[1,1,1]))*np.array(b['size'])
            if b['shape']=='Cube':center=[0,0,0];size=b['size']
            p=np.array(g['position'])+np.array(b['position'])+np.array(rotate(center,b['yaw']))
            if p[1]+size[1]/2<.28:continue
            c=abs(math.cos(math.radians(b['yaw'])));s=abs(math.sin(math.radians(b['yaw'])));hx=(c*size[0]+s*size[2])/2+radius;hz=(s*size[0]+c*size[2])/2+radius
            blocked|=(np.abs(xs[None,:]-p[0])<hx)&(np.abs(zs[:,None]-p[2])<hz);obstacles.append([float(p[0]),float(p[2]),float(hx),float(hz)])
    point=lambda x,z:(int(np.argmin(abs(zs-z))),int(np.argmin(abs(xs-x))))
    start=point(-7,-12);assert not blocked[start],'Spawn blocked';seen={start};q=collections.deque([start])
    while q:
        y,x=q.popleft()
        for ny,nx in [(y-1,x),(y+1,x),(y,x-1),(y,x+1)]:
            if 0<=ny<len(zs) and 0<=nx<len(xs) and not blocked[ny,nx] and (ny,nx) not in seen:seen.add((ny,nx));q.append((ny,nx))
    routes={'center':(0,0),'warehouse_apron':(-9,6.1),'workshop_apron':(-12,-8.1),'training_entry':(10,-5.5),'rear_exit':(.5,13.3),'west_flank':(-15,1),'east_flank':(15,1),'north_flank':(3.0,10),'south_crossing':(0,-11)}
    results={k:point(*v) in seen for k,v in routes.items()};assert all(results.values()),results
    return {'gridMeters':step,'inflatedPlayerRadius':radius,'checkedWaypoints':results,'reachableFreeCells':len(seen),'freeCells':int((~blocked).sum()),'obstacleAabbs':obstacles,'scope':'Conservative 2D AABB connectivity, not Unity CharacterController or PhysX.'}

def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--apply-scene',action='store_true');ap.add_argument('--render-json',type=Path);args=ap.parse_args()
    for folder in [MESHES,TEXTURES]:folder.mkdir(parents=True,exist_ok=True);meta(folder)
    make_materials();build_modules();layout=build_layout();exported={name:m.export() for name,m in MODULES.items()};LAYOUT.write_text(json.dumps(layout,ensure_ascii=False,indent=2)+'\n')
    report={'layoutVersion':2,'meshAssets':len(exported),'uniqueMeshTriangles':sum(x['triangles'] for x in exported.values()),'nativeMeshFormat':'Unity Graphics official Mesh v11 / VertexData v3 / Position+Normal+UV0 Float32 stride32 / UInt16 indices','pathClearance':clearance(layout)}
    if args.apply_scene:report['sceneIntegration']=write_scene(layout)
    if args.render_json:args.render_json.write_text(json.dumps({'layout':layout,'modules':exported,'materials':MATERIALS}))
    out=ROOT/'Validation/TutorialEnvironment.json';out.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({k:v for k,v in report.items() if k!='pathClearance'},ensure_ascii=False,indent=2))

if __name__=='__main__':main()
