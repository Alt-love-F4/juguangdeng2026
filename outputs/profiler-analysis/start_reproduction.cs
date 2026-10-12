if (UnityEditor.EditorApplication.isPlaying || UnityEditor.EditorApplication.isCompiling) throw new System.Exception("Editor must be idle before reproduction.");
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(scene.isDirty) throw new System.Exception("Current scene has unsaved changes.");
var state=new {scene=scene.path, profiler=UnityEngine.Profiling.Profiler.enabled,profileEditor=UnityEditorInternal.ProfilerDriver.profileEditor,targetFps=UnityEngine.Application.targetFrameRate};
System.IO.File.WriteAllText("outputs/profiler-analysis/reproduction-original-state.json",Newtonsoft.Json.JsonConvert.SerializeObject(state));
UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/PresentationDebugScene.unity");
UnityEditor.EditorApplication.isPlaying=true;
return state;
