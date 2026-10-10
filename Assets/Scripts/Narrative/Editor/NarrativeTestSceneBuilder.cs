using System.IO;
using Spotlight.Narrative;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Spotlight.Narrative.Editor
{
    public static class NarrativeTestSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/NarrativeTest.unity";
        [MenuItem("Spotlight/Narrative/Create Test Scene")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 0, -10);
            Camera camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .045f, .055f); camera.orthographic = true; camera.orthographicSize = 5;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var app = new GameObject("NarrativeTestWorkbench", typeof(NarrativeTestWorkbench), typeof(Spotlight.GameFlow.NarrativeTestPresentationBridge));
            EditorSceneManager.MarkSceneDirty(scene);
            Directory.CreateDirectory("Assets/Scenes");
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Unable to save " + ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[NCS] Independent Narrative test scene created. It reads runtime configuration from Luban and does not modify the game-flow scenes.");
        }

        [MenuItem("Spotlight/Narrative/Open Test Scene")]
        public static void Open() => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }
}
