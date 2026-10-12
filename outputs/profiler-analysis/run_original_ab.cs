if(!UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Play Mode has not started.");
UnityEngine.GameObject map=null;
foreach(var c in UnityEngine.Object.FindObjectsByType<Spotlight.GameFlow.MapSceneController>(UnityEngine.FindObjectsSortMode.None))if(c.transform is UnityEngine.RectTransform)map=c.gameObject;
if(map==null)throw new System.Exception("Map panel not found");
map.SetActive(false);
bool oldProfiler=UnityEngine.Profiling.Profiler.enabled,oldEditor=UnityEditorInternal.ProfilerDriver.profileEditor;
int oldFps=UnityEngine.Application.targetFrameRate;
UnityEngine.Profiling.Profiler.enabled=true;
UnityEditorInternal.ProfilerDriver.profileEditor=true;
var results=new System.Collections.Generic.List<object>();
string[] names={"hidden_unlimited","visible_unlimited","visible_60fps","hidden_60fps"};
int phase=0,startFrame=UnityEditorInternal.ProfilerDriver.lastFrameIndex+1;
double start=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{
 try {
  if(!UnityEditor.EditorApplication.isPlaying || map==null)throw new System.Exception("Reproduction interrupted.");
  double now=UnityEditor.EditorApplication.timeSinceStartup;
  if(now-start<15)return;
  int last=UnityEditorInternal.ProfilerDriver.lastFrameIndex;
  var frames=new System.Collections.Generic.List<object>();
  for(int f=System.Math.Max(startFrame,UnityEditorInternal.ProfilerDriver.firstFrameIndex);f<=last;f++) {
   using(var v=UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(f,0)) {
    if(!v.valid)continue;
    var marks=new System.Collections.Generic.Dictionary<string,double>();
    for(int s=0;s<v.sampleCount;s++) {
     string n=v.GetSampleName(s); double ms=v.GetSampleTimeMs(s);
     if(n==null||ms<0.1)continue;
     if(!marks.ContainsKey(n))marks[n]=0;marks[n]+=ms;
    }
    frames.Add(new {frame=f,ms=v.frameTimeMs,marks});
   }
  }
  results.Add(new {phase=names[phase],duration=now-start,frames});
  System.IO.File.WriteAllText("outputs/profiler-analysis/map-original-ab.json",Newtonsoft.Json.JsonConvert.SerializeObject(results));
  phase++;
  if(phase==names.Length){
   UnityEditor.EditorApplication.update-=tick;
   UnityEngine.Profiling.Profiler.enabled=oldProfiler;UnityEditorInternal.ProfilerDriver.profileEditor=oldEditor;
   UnityEngine.Application.targetFrameRate=oldFps;map.SetActive(true);
   System.IO.File.WriteAllText("outputs/profiler-analysis/map-original-ab.done","complete");return;
  }
  if(phase==1)map.SetActive(true);
  if(phase==2)UnityEngine.Application.targetFrameRate=60;
  if(phase==3)map.SetActive(false);
  start=UnityEditor.EditorApplication.timeSinceStartup;
  startFrame=UnityEditorInternal.ProfilerDriver.lastFrameIndex+2;
 } catch(System.Exception e) {
  UnityEditor.EditorApplication.update-=tick;
  UnityEngine.Profiling.Profiler.enabled=oldProfiler;UnityEditorInternal.ProfilerDriver.profileEditor=oldEditor;
  UnityEngine.Application.targetFrameRate=oldFps;
  if(map!=null)map.SetActive(true);
  System.IO.File.WriteAllText("outputs/profiler-analysis/map-original-ab.error",e.ToString());
 }
};
UnityEditor.EditorApplication.update+=tick;
return new {scheduled=names,secondsPerPhase=15,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path};

