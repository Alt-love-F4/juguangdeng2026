import json
from pathlib import Path
p=Path('outputs/profiler-analysis/map-active-ab.json')
if p.exists():
 for d in json.loads(p.read_text()):
  fs=d['frames'][5:];ss=sorted(f['ms'] for f in fs);ds=sorted(d['deltas'][5:])
  print(d['phase'],'profiler frames',len(fs),'game frames',len(ds),'ms',{'avg':round(sum(ss)/len(ss),2),'p99':round(ss[int(len(ss)*.99)],2),'max':round(ss[-1],2),'over100':sum(x>100 for x in ss)},'game intervals',{'avg':round(sum(ds)/len(ds),2),'p99':round(ds[int(len(ds)*.99)],2),'max':round(ds[-1],2),'over100':sum(x>100 for x in ds)} if ds else {})
  print('slow',[(round(f['ms'],2),sorted(f['marks'].items(),key=lambda x:-x[1])[:6])for f in sorted(fs,key=lambda f:-f['ms'])[:2]])
print('done',Path('outputs/profiler-analysis/map-active-ab.done').exists(),'error',Path('outputs/profiler-analysis/map-active-ab.error').read_text() if Path('outputs/profiler-analysis/map-active-ab.error').exists()else '')
