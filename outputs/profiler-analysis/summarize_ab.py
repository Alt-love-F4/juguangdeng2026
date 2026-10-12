from pathlib import Path
import json
p=Path('outputs/profiler-analysis/map-ab.json')
if p.exists():
 for d in json.loads(p.read_text()):
  fs=d['frames'][5:];ss=sorted(f['ms'] for f in fs)
  print(d['phase'],len(fs),{'avg':round(sum(ss)/len(ss),2),'p50':ss[len(ss)//2],'p99':ss[int(len(ss)*.99)],'max':ss[-1],'>50':sum(x>50 for x in ss),'>100':sum(x>100 for x in ss)})
  for k in ['PlayerLoop','Application.Message','EditorLoop','GameView.Paint','SceneView.Paint','WaitForTargetFPS','Gfx.WaitForPresentOnGfxThread']:
   vals=[f['marks'].get(k,0) for f in fs];print(k,round(sum(vals)/len(fs),3),round(max(vals),3))
  print('slow',[(f['frame'],round(f['ms'],2),sorted(f['marks'].items(),key=lambda x:-x[1])[:5])for f in sorted(fs,key=lambda f:-f['ms'])[:3]])
else: print('waiting for first phase')
print('done',Path('outputs/profiler-analysis/map-ab.done').exists())
