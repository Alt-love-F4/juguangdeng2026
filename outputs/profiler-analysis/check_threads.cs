int last=UnityEditorInternal.ProfilerDriver.lastFrameIndex;
var frames=new System.Collections.Generic.List<object>();
for(int f=System.Math.Max(UnityEditorInternal.ProfilerDriver.firstFrameIndex,last-500);f<=last;f++) {
 using(var main=UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(f,0)) {
  if(!main.valid||main.frameTimeMs<60)continue;
  var threads=new System.Collections.Generic.List<object>();
  for(int t=0;t<40;t++) using(var v=UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(f,t)){
   if(!v.valid)break;
   var samples=new System.Collections.Generic.List<object>();
   for(int s=0;s<v.sampleCount;s++)if(v.GetSampleTimeMs(s)>3) samples.Add(new {name=v.GetSampleName(s),ms=v.GetSampleTimeMs(s)});
   if(samples.Count>0)threads.Add(new {name=v.threadName,samples});
  }
  frames.Add(new {frame=f,ms=main.frameTimeMs,threads});
 }
 if(frames.Count==5)break;
}
return frames;
