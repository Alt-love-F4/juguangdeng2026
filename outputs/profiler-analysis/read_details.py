import json,sys
from pathlib import Path
sys.path.insert(0,'.unity');import mcp
code=r'''
var output=new System.Collections.Generic.List<object>();
int first=UnityEditorInternal.ProfilerDriver.firstFrameIndex,last=UnityEditorInternal.ProfilerDriver.lastFrameIndex;
for(int f=first;f<=System.Math.Min(last,285148);f++) {
 using(var v=UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(f,0)) {
  if(!v.valid||v.frameTimeMs<70||f==284071) continue;
  bool detailed=false;for(int i=0;i<v.sampleCount;i++)if(v.GetSampleName(i)=="Application.Message")detailed=true;
  if(!detailed)continue;
  var samples=new System.Collections.Generic.List<object>();
  for(int s=0;s<v.sampleCount;s++) if(v.GetSampleTimeMs(s)>=0.1) samples.Add(new {index=s,name=v.GetSampleName(s),ms=v.GetSampleTimeMs(s),children=v.GetSampleChildrenCount(s)});
  output.Add(new {frame=f,ms=v.frameTimeMs,samples});
 }
}
return new {first,last,frames=output};
'''
r=mcp.request('execute_code',{'action':'execute','code':code});Path('outputs/profiler-analysis/slow-frame-details.json').write_text(json.dumps(r,indent=2),encoding='utf-8')
if not r.get('result',{}).get('success'):print(json.dumps(r));sys.exit(1)
d=r['result']['data']['result'];print('range',d['first'],d['last'],'slow frames',len(d['frames']))
for f in d['frames'][:3]:print(json.dumps(f,indent=2))
