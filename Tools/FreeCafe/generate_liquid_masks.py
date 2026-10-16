"""Build container alpha + signed-distance masks from scene-referenced sprite contours.
R encodes signed distance / image height in [-distanceRange, distanceRange].
Alpha is the outer silhouette; transparent RGB is deliberately preserved for SDF sampling.
"""
import json
from pathlib import Path
import cv2
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Assets/Art/Gameplay/咖啡馆/LiquidMasks'

def contour(commands, width, height):
    points=[]
    for c in commands:
        if c[0] in ('M','L'):
            p=np.array(c[1:3],dtype=float);points.append(p)
        else:
            start=p.copy();a=np.array(c[1:3]);b=np.array(c[3:5]);end=np.array(c[5:7])
            for t in np.linspace(0,1,33)[1:]:
                points.append((1-t)**3*start+3*(1-t)**2*t*a+3*(1-t)*t*t*b+t**3*end)
            p=end
    return [(float(p[0]*width),float(p[1]*height)) for p in points]

def main():
    manifest=json.loads((ROOT/'Tools/FreeCafe/liquid-mask-shapes.json').read_text(encoding='utf-8'))
    OUTPUT.mkdir(parents=True,exist_ok=True)
    preview=Image.new('RGB',(4*320,2*390),(225,222,211));draw=ImageDraw.Draw(preview)
    for index,s in enumerate(manifest['shapes']):
        source=Image.open(ROOT/s['source']).convert('RGBA');w,h=source.size
        hi=Image.new('L',(w*3,h*3));ImageDraw.Draw(hi).polygon(contour(s['path'],w*3,h*3),fill=255)
        alpha=np.asarray(hi.resize((w,h),Image.Resampling.LANCZOS))
        binary=(alpha>=128).astype(np.uint8)
        # Prevent clamp sampling from treating the image edge as unbounded interior.
        binary[[0,-1],:]=0;binary[:,[0,-1]]=0
        distance=cv2.distanceTransform(binary,cv2.DIST_L2,cv2.DIST_MASK_PRECISE)-cv2.distanceTransform(1-binary,cv2.DIST_L2,cv2.DIST_MASK_PRECISE)
        red=np.round(np.clip(.5+distance/(h*2*manifest['distanceRange']),0,1)*255).astype(np.uint8)
        rgba=np.stack([red,red,red,alpha],axis=-1)
        Image.fromarray(rgba).save(OUTPUT/(s['specId']+'.png'))
        # Preview the same signed-distance inset as the shader, on the actual source.
        inside=distance/h>=manifest['wallThickness'];edge=(binary>0)&~inside
        overlay=np.zeros((h,w,4),dtype=np.uint8);overlay[inside]=(35,158,205,155);overlay[edge]=(58,33,21,220)
        composite=Image.alpha_composite(source,Image.fromarray(overlay));composite.thumbnail((290,320))
        x=(index%4)*320+15;y=(index//4)*390+37
        preview.paste(composite,(x,y),composite);draw.text((x,y-25),s['specId'],fill=(35,30,25))
    out=ROOT/'output/liquid-masks';out.mkdir(parents=True,exist_ok=True)
    preview.save(out/'container-mask-preview.png')
    print('Generated',len(manifest['shapes']),'masks from the scene/pickup sprite references:',OUTPUT)

if __name__=='__main__':main()
