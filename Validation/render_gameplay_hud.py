"""Raster preview of actual production menu draw commands; does not run Unity."""
from pathlib import Path
import json,base64,textwrap
from PIL import Image,ImageDraw,ImageFont,ImageChops
ROOT=Path(__file__).resolve().parents[1]
INPUT=ROOT/'Validation/Results/V19UI/MissionPreviews'
OUT=ROOT/'Validation/Results/V19UI/HudRaster';OUT.mkdir(parents=True,exist_ok=True)
FONT=Path('/usr/share/fonts/truetype/dejavu')
font_cache={}
def font(size,bold):
 key=(size,bold)
 if key not in font_cache:font_cache[key]=ImageFont.truetype(str(FONT/('DejaVuSans-Bold.ttf' if bold else 'DejaVuSans.ttf')),size)
 return font_cache[key]
def wrap(line,f,width):
 if f.getlength(line)<=width:return [line]
 words=line.split();lines=[];current=''
 for word in words:
  next=(current+' '+word).strip()
  if current and f.getlength(next)>width:lines.append(current);current=word
  else:current=next
 if current:lines.append(current)
 return lines or ['']
def render(path):
 data=json.loads(path.read_text());canvas=Image.new('RGBA',(data['width'],data['height']),(54,67,68,255))
 # Unity SetPixels32 rows start at the bottom; Pillow byte rows start at the
 # top. Authored PNG portraits are loaded normally below, without this flip.
 textures={t['name']:Image.frombytes('RGBA',(t['width'],t['height']),base64.b64decode(t['rgba'])).transpose(Image.Transpose.FLIP_TOP_BOTTOM) for t in data['textures']}
 for name in ['RobotPortrait','PistolPortrait']:
  p=ROOT/'Assets/IntercomMVP/Content/Frontend/Textures'/f'{name}.png'
  if p.exists():textures[name]=Image.open(p).convert('RGBA')
 for c in data['commands']:
  x,y,w,h=c['rect'];x,y=int(round(x)),int(round(y));w,h=max(1,int(round(w))),max(1,int(round(h)))
  rgba=tuple(max(0,min(255,round(v*255))) for v in c['color'])
  if c['kind']=='texture':
   t=textures[c['texture']].resize((w,h),Image.Resampling.BILINEAR)
   tint=Image.new('RGBA',t.size,rgba);t=ImageChops.multiply(t,tint);canvas.alpha_composite(t,(x,y))
  else:
   f=font(max(6,c['fontSize']),c['bold']);lines=[]
   for line in c['text'].split('\n'):lines+=wrap(line,f,w) if c.get('wrap') else [line]
   lineheight=int(f.size*1.2);top=y+(h-lineheight*len(lines))//2
   layer=Image.new('RGBA',(w,h));draw=ImageDraw.Draw(layer)
   for i,line in enumerate(lines):
    left=(w-f.getlength(line))/2 if c.get('align','MiddleCenter')=='MiddleCenter' else 0
    draw.text((left,top-y+i*lineheight),line,font=f,fill=rgba,stroke_width=0)
   canvas.alpha_composite(layer,(x,y))
 out=OUT/(path.stem+'.png');canvas.convert('RGB').save(out);return out
paths=[render(p) for p in sorted(INPUT.glob('*.json'))]
land=[Image.open(p).resize((640,360)) for p in paths if p.stem in {'elimination','low_ammo','reloading','pause'}]
if land:
 sheet=Image.new('RGB',(1280,720));
 for i,im in enumerate(land):sheet.paste(im,((i%2)*640,(i//2)*360))
 sheet.save(OUT/'contact_sheet.jpg',quality=92)
print('\n'.join(str(p) for p in paths))
