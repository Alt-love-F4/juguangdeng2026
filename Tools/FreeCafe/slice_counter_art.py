"""Lossless crops of the supplied counter PSD exports; does not modify source files."""
import json
from pathlib import Path
from PIL import Image, ImageDraw
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/'Assets/Art/Gameplay/咖啡馆/台面道具'
OUT=ROOT/'Assets/Art/Gameplay/咖啡馆/Sliced'
MAIN=Image.open(SRC/'图层 7 副本.png').convert('RGBA')
SCALE=22.044444/4096
manifest={}
def save(art, im, box, origin=(118,-30), state='default', align=None):
    piece=im.crop(box)
    if art in ('source.cream','source.honey'):
        # The spoon/cream crops overlap; retain only this prop's connected silhouette.
        import cv2
        a=np.array(piece); n,labels,stats,_=cv2.connectedComponentsWithStats((a[:,:,3]>5).astype('uint8'))
        keep=1+np.argmax(stats[1:,4]); a[labels!=keep]=0; piece=Image.fromarray(a)
    folder=OUT/'Entities'/art; folder.mkdir(parents=True,exist_ok=True)
    piece.save(folder/(state+'.png'))
    x,y,r,b=box
    if state=='default':
        manifest[art]={'width':(r-x)*SCALE,'height':(b-y)*SCALE,
          'x':(origin[0]+(x+r)/2-2048)*SCALE,'y':(1152-origin[1]-(y+b)/2)*SCALE,'box':box}
    return piece
for art,box in {
'source.beans':(0,300,478,935),'source.cocoa':(480,620,955,920),
'source.matcha':(955,467,1192,890),'source.milk':(1190,465,1560,900),
'machine.grinder':(1830,330,2315,1060),'machine.heater':(3187,240,3654,1016),
'cup.large':(60,1226,499,1630),'cup.small':(518,1450,885,1707),
'source.honey':(2916,1209,3292,1598),'source.cream':(3196,1339,3623,1598),'source.sugar':(3028,1625,3428,1857)
}.items(): save(art,MAIN,box)
# Upper tower is a separate vessel; retain the lower handle/spout at the same cut.
upper=(2566,159,2820,684)
save('container.extractor.filter',MAIN,upper)
jug=MAIN.copy(); ImageDraw.Draw(jug).rectangle((upper[0],upper[1],upper[2]-1,upper[3]-1),fill=(0,0,0,0))
save('container.extractor',jug,(2418,609,3068,1039))
# Nested lower cups have open rims. Pad to the TOP sprite canvas so scale/pivots match.
LOW=Image.open(SRC/'杯子重叠（下）.png').convert('RGBA')
for art,box,pad in [('cup.large',(0,0,439,370),(0,34)),('cup.small',(455,135,822,366),(0,26))]:
    w,h=manifest[art]['box'][2]-manifest[art]['box'][0],manifest[art]['box'][3]-manifest[art]['box'][1]
    im=Image.new('RGBA',(w,h)); im.alpha_composite(LOW.crop(box),pad)
    folder=OUT/'Entities'/art;im.save(folder/'nested.png')
# PSD background layer extends above the document. Crop to the original document viewport.
BG=Image.open(SRC/'组 1.png').convert('RGBA').crop((0,301,4096,2605))
(OUT/'Environment').mkdir(parents=True,exist_ok=True);BG.save(OUT/'Environment/counter.png')
manifest['filterOffset']={k:manifest['container.extractor.filter'][k]-manifest['container.extractor'][k] for k in ('x','y')}
(OUT/'slices.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
# Review sheet, not a runtime asset.
canvas=Image.new('RGB',(1600,1000),'#b5aaa0');d=ImageDraw.Draw(canvas)
for i,(art,e) in enumerate((k,v) for k,v in manifest.items() if 'box' in v):
    im=Image.open(OUT/'Entities'/art/'default.png');im.thumbnail((245,275)); x=i%6*265;y=i//6*330
    canvas.paste(im,(x,y+35),im);d.text((x+3,y+6),art,fill='black')
(ROOT/'Temp/CafeArtReview').mkdir(parents=True,exist_ok=True)
canvas.save(ROOT/'Temp/CafeArtReview/slices.jpg')
print(json.dumps(manifest,indent=1))
