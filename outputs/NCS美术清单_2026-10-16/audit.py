from pathlib import Path
import json,re,hashlib,collections,yaml
from PIL import Image,ImageDraw,ImageFont
out=Path(__file__).parent; root=out.parents[1]
def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def unity(p):
 s=p.read_text(encoding='utf-8-sig'); s=re.sub(r'^%.*\n','',s,flags=re.M); s=re.sub(r'^--- !u!.*\n','',s,flags=re.M); return yaml.safe_load(s)['MonoBehaviour']
def dump(name,data): (out/name).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
guids={}
for p in (root/'Assets').rglob('*.meta'):
 m=re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig',errors='replace'),re.M)
 if m: guids[m[1]]=p.with_suffix('').relative_to(root).as_posix()
def resolve(v): return guids.get(v.get('guid',''),'') if isinstance(v,dict) else ''
assets=[]
for p in (root/'Assets').rglob('*'):
 if p.suffix.lower() in ['.png','.jpg','.jpeg','.psd','.webp','.tga','.mp4','.mov','.gif']:
  rel=p.relative_to(root).as_posix()
  if any(x in rel for x in ['/Plugins/','TextMesh Pro','/YokiFrame/']): continue
  item={'path':rel,'size':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
  try:
   with Image.open(p) as im: item.update(width=im.width,height=im.height)
  except: pass
  assets.append(item)
dump('asset_inventory.json',assets)
stage=unity(root/'Assets/Resources/Dialogue/NarrativeStageVisuals.asset')
for e in stage['entries']: e['file']=resolve(e['texture'])
dump('stage_inventory.json',stage['entries'])
catalog=unity(root/'Assets/Resources/Characters/CharacterCatalog.asset')
dump('catalog_raw.json',catalog)
for e in catalog.get('characters',catalog.get('entries',[])):
 for k,v in list(e.items()):
  if isinstance(v,dict): e[k]=resolve(v)
dump('catalog_inventory.json',catalog)
files={p.stem:load(p) for p in (root/'Assets/Settings/Resources/Luban').glob('*.json')}
refs=[]
for name,rows in files.items():
 if not ('ncs' in name or 'opening' in name): continue
 for row in rows:
  for key,val in row.items():
   if val and key in ['bgOverride','imageKey','visualsResource','portraitPath','portraitIndoorPath','iconPath','avtarPath','imgPath','seatedVisualPath']:
    matches=[a['path'] for a in assets if '/Resources/' in a['path'] and a['path'].split('/Resources/',1)[1].rsplit('.',1)[0]==val]
    matches += [p.relative_to(root).as_posix() for p in (root/'Assets/Resources').glob(str(val)+'.asset')]
    st=next((x for x in stage['entries'] if x['key']==val),None)
    refs.append({'table':name,'id':next(iter(row.values())),'field':key,'value':val,'files':matches,'stage':st})
dump('config_references.json',refs)
summary=[]
for name in ['tbncsobject','tbncsmapposition','tbncsreadingpage','tbncsnotice','tbncscafevisit','tbncssetupparams']:
 summary.append(name)
 for row in files[name]: summary.append(json.dumps(row,ensure_ascii=False))
(out/'config_selected.txt').write_text('\n'.join(summary),encoding='utf-8')
from zipfile import ZipFile
import xml.etree.ElementTree as ET
rootdoc=Path(r'D:\桌面\文案'); embedded=[]
for p in rootdoc.glob('*.xlsx'):
 with ZipFile(p) as z:
  if 'xl/cellimages.xml' not in z.namelist(): continue
  rels={x.attrib['Id']:x.attrib['Target'] for x in ET.fromstring(z.read('xl/_rels/cellimages.xml.rels'))}
  ns={'xdr':'http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing','a':'http://schemas.openxmlformats.org/drawingml/2006/main','r':'http://schemas.openxmlformats.org/officeDocument/2006/relationships'}
  for pic in ET.fromstring(z.read('xl/cellimages.xml')).iter('{'+ns['xdr']+'}pic'):
   nv=pic.find('.//xdr:cNvPr',ns); blip=pic.find('.//a:blip',ns)
   if nv is None or blip is None: continue
   target=rels[blip.attrib['{'+ns['r']+'}embed']]; zp='xl/'+target.lstrip('/') if not target.startswith('/') else target[1:]
   data=z.read(zp); sha=hashlib.sha256(data).hexdigest()
   embedded.append({'file':p.name,'id':nv.attrib.get('name'),'zip_path':zp,'sha256':sha,'exact_matches':[a['path'] for a in assets if a['sha256']==sha]})
dump('embedded_images.json',embedded)
selected=[a for a in assets if a['path'].startswith('Assets/Resources/Characters/') or (a['path'].startswith('Assets/Art/') and any(t in a['path'] for t in ['Dialogue','Opening','Letter','Prologue','Background','DayFlow/phase','DayFlow/home','DayFlow/city']))]
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',15)
for batch in range(0,len(selected),24):
 items=selected[batch:batch+24]; sheet=Image.new('RGB',(1200,360*((len(items)+3)//4)),(220,220,220)); draw=ImageDraw.Draw(sheet)
 for i,a in enumerate(items):
  x=(i%4)*300; y=(i//4)*360
  try:
   with Image.open(root/a['path']) as im:
    im.thumbnail((290,285)); im=im.convert('RGBA'); sheet.paste(im,(x+(300-im.width)//2,y),im)
  except: pass
  label=a['path'].replace('Assets/Resources/','').replace('Assets/Art/',''); lines=[label[j:j+29] for j in range(0,len(label),29)]
  draw.text((x+5,y+287),'\n'.join(lines[:4]),fill=(0,0,0),font=font)
 sheet.save(out/f'现有资源预览_{batch//24+1}.jpg')
print('assets',len(assets),'stage',len(stage['entries']),'refs',len(refs),'embedded',len(embedded),'contact',len(selected))
