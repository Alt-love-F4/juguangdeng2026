var data=new System.Collections.Generic.List<object>();
foreach(var c in UnityEngine.Object.FindObjectsByType<Spotlight.GameFlow.MapSceneController>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)){
 if(!(c.transform is UnityEngine.RectTransform))continue;
 var seen=new System.Collections.Generic.HashSet<UnityEngine.Texture>();long bytes=0;
 foreach(var img in c.GetComponentsInChildren<UnityEngine.UI.Image>(true)){
  var tex=img.sprite==null?null:img.sprite.texture;
  if(tex!=null&&seen.Add(tex)) {long size=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);bytes+=size;data.Add(new{name=tex.name,width=tex.width,height=tex.height,format=tex.format.ToString(),bytes=size});}
 }
 return new{scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,objects=c.GetComponentsInChildren<UnityEngine.Transform>(true).Length,images=c.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length,textureBytes=bytes,textures=data};
}
return "map not found";

