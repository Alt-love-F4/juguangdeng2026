if(UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Still leaving Play Mode.");
UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Map.unity");
UnityEditor.EditorApplication.isPlaying=true;
return "starting original Map scene";
