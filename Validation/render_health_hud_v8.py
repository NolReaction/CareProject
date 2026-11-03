"""Render captured C# HUD draw commands offline; this is not a Unity screenshot.

First run HudLayoutTests. No GUI geometry is duplicated here: rectangles, colours,
texture pixels and strings come from IntercomWorkshopHud.Draw through test fakes.
Pillow/DejaVu font rasterization differs from Unity's actual font rendering.
"""
from pathlib import Path
import base64
import json
from PIL import Image, ImageDraw, ImageFont
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
INPUT = ROOT / 'Validation/Results/V8UI/HudPreviews'
FONT = Path('/usr/share/fonts/truetype/dejavu')

def render(source, destination):
    spec = json.loads(source.read_text())
    width, height = spec['width'], spec['height']
    image = Image.new('RGBA', (width, height), (61, 75, 74, 255))
    draw = ImageDraw.Draw(image)
    # Neutral test ground keeps the preview honest and readable.
    for y in range(height // 4, height, max(1, height // 4)):
        draw.line((0, y, width, y), fill=(68, 82, 80), width=1)
    for x in range(width // 5, width, max(1, width // 5)):
        draw.line((x, 0, x, height), fill=(68, 82, 80), width=1)
    textures = {}
    for texture in spec['textures']:
        raw = base64.b64decode(texture['rgba'])
        textures[texture['name']] = Image.frombytes('RGBA', (texture['width'], texture['height']), raw).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    for command in spec['commands']:
        x, y, w, h = command['rect']
        color = command['color']
        if command['kind'] == 'texture':
            tile = textures[command['texture']].resize((max(1, round(w)), max(1, round(h))), Image.Resampling.BILINEAR)
            rgba = np.asarray(tile).astype(float)
            rgba *= np.array(color)
            tile = Image.fromarray(np.clip(rgba, 0, 255).astype('uint8'), 'RGBA')
            image.alpha_composite(tile, (round(x), round(y)))
        else:
            font = ImageFont.truetype(str(FONT / ('DejaVuSans-Bold.ttf' if command['bold'] else 'DejaVuSans.ttf')), command['fontSize'])
            layer = Image.new('RGBA', image.size)
            d = ImageDraw.Draw(layer)
            d.text((x + w / 2, y + h / 2), command['text'], font=font, fill=tuple(round(c * 255) for c in color), anchor='mm')
            image.alpha_composite(layer)
    draw = ImageDraw.Draw(image)
    font = ImageFont.truetype(str(FONT / 'DejaVuSans.ttf'), max(10, round(height / 60)))
    draw.text((width/2, height-36), 'OFFLINE HUD LAYOUT / V8', font=font, fill=(180,190,178), anchor='mm')
    draw.text((width/2, height-17), 'Actual drawing commands; Unity rendering not tested', font=font, fill=(148,161,151), anchor='mm')
    destination.parent.mkdir(parents=True, exist_ok=True)
    image.convert('RGB').save(destination)
    print(destination)

if __name__ == '__main__':
    for name, suffix in [('landscape', ''), ('notched', '_Notched'), ('compact', '_Compact'), ('defeat', '_Defeat')]:
        render(INPUT / (name+'.json'), ROOT / ('Docs/PlayerHealthV8'+suffix+'.png'))
