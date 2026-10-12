import json,sys
from pathlib import Path
sys.path.insert(0,'.unity')
import mcp
code=r'''
int first = UnityEditorInternal.ProfilerDriver.firstFrameIndex;
int last = UnityEditorInternal.ProfilerDriver.lastFrameIndex;
var frames = new System.Collections.Generic.List<object>();
var totals = new System.Collections.Generic.Dictionary<string,double[]>();
for(int f=first;f<=last;f++) {
 using(var v=UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(f,0)) {
  if(!v.valid) continue;
  var markers = new System.Collections.Generic.Dictionary<string,double>();
  for(int s=0;s<v.sampleCount;s++) {
   string n=v.GetSampleName(s); double ms=v.GetSampleTimeMs(s);
   if(ms<0.05) continue;
   if(!markers.ContainsKey(n)) markers[n]=0; markers[n]+=ms;
  }
  foreach(var p in markers) {if(!totals.ContainsKey(p.Key)) totals[p.Key]=new double[3]; totals[p.Key][0]+=p.Value; totals[p.Key][1]=System.Math.Max(totals[p.Key][1],p.Value); totals[p.Key][2]++;}
  frames.Add(new {frame=f, ms=v.frameTimeMs, thread=v.threadName, markers=markers});
 }
}
var windows = new System.Collections.Generic.List<object>();
foreach(var w in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>()) {
 if(w.GetType().Name.IndexOf("Profiler")<0) continue;
 var props=new System.Collections.Generic.Dictionary<string,object>();
 foreach(var p in w.GetType().GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)) {
  if(p.Name.ToLower().Contains("frame") && p.GetIndexParameters().Length==0) {try {props[p.Name]=p.GetValue(w,null);}catch{}}
 }
 windows.Add(new {type=w.GetType().FullName, properties=props});
}
return new {first,last,enabled=UnityEngine.Profiling.Profiler.enabled,deep=UnityEditorInternal.ProfilerDriver.deepProfiling,profileEditor=UnityEditorInternal.ProfilerDriver.profileEditor,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,vsync=UnityEngine.QualitySettings.vSyncCount,targetFps=UnityEngine.Application.targetFrameRate,graphics=UnityEngine.SystemInfo.graphicsDeviceType.ToString(),windows,totals,frames};
'''
r=mcp.request('execute_code',{'action':'execute','code':code})
Path('outputs/profiler-analysis/current-capture.json').write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8')
if not r.get('result',{}).get('success'): print(json.dumps(r));sys.exit(1)
d=r['result']['data']['result']; fs=d.pop('frames');ts=d.pop('totals'); print(json.dumps(d,ensure_ascii=False,indent=2));print('FRAMES',len(fs))
ss=sorted(f['ms'] for f in fs)
print('STATS',{'mean':sum(ss)/len(ss),'p50':ss[len(ss)//2],'p95':ss[int(len(ss)*.95)],'p99':ss[int(len(ss)*.99)],'max':ss[-1],'>33ms':sum(x>33.33 for x in ss),'>50ms':sum(x>50 for x in ss),'>100ms':sum(x>100 for x in ss)})
print('TOP TOTAL MARKERS',json.dumps(sorted(ts.items(),key=lambda x:-x[1][0])[:45],indent=2))
for f in sorted(fs,key=lambda f:-f['ms'])[:12]: print('SLOW',f['frame'],f['ms'],json.dumps(sorted(f['markers'].items(),key=lambda x:-x[1])[:18]))
