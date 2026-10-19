using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.Narrative.Editor
{
    /// <summary>
    /// Explicit, editor-only creation of the read-only story map. Never changes build settings,
    /// saves unrelated scenes, or starts the game's narrative/runtime hosts.
    /// </summary>
    public static class NarrativeRelationshipSceneBuilder
    {
        public const string ScenePath = NarrativeRelationshipSceneController.ScenePath;

        [MenuItem("Tools/Narrative/Create Missing Story Relationship Scene")]
        public static void CreateMissing()
        {
            RequireEditMode();
            if (File.Exists(ScenePath)) return;
            CreateScene();
        }

        [MenuItem("Tools/Narrative/Open Story Relationship Scene")]
        public static void Open()
        {
            RequireEditMode();
            CreateMissing();
            // Additive opening intentionally leaves every other scene and its unsaved work alone.
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            Selection.activeObject = FindController(scene);
        }

        [MenuItem("Tools/Narrative/Rebuild Story Relationship Scene")]
        public static void Rebuild()
        {
            RequireEditMode();
            Scene existing = SceneManager.GetSceneByPath(ScenePath);
            if (existing.IsValid() && existing.isLoaded && existing.isDirty)
            {
                EditorUtility.DisplayDialog("关系图场景存在未保存修改",
                    "为避免丢失修改，本次不重建。请先手动保存关系图场景，或明确撤销其中修改后再重建。其他场景不会被保存或关闭。", "知道了");
                return;
            }
            if (!EditorUtility.DisplayDialog("重建故事关系图场景",
                    "将覆盖 NarrativeRelationshipScene 的自定义场景布局。不会改动剧情源表、存档、其他场景或 Build Settings。继续？",
                    "重建", "取消")) return;
            CreateScene();
        }

        /// <summary>
        /// Deliberate rebuild entry point for tooling. Dirty target scenes are rejected, not discarded.
        /// Construction completes in an additive scratch scene before a loaded clean target is closed.
        /// A previously loaded target stays loaded after replacement; an unloaded target stays unloaded.
        /// </summary>
        public static void CreateScene()
        {
            RequireEditMode();
            Scene previous = SceneManager.GetActiveScene();
            Scene existing = SceneManager.GetSceneByPath(ScenePath);
            RequireCleanTarget(existing);
            bool wasLoaded = existing.IsValid() && existing.isLoaded;
            bool wasActive = wasLoaded && previous == existing;
            bool closedExisting = false;
            bool saved = false;
            Scene created = default;
            try
            {
                created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(created);
                Build(created);
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/Scenes");
                if (wasLoaded)
                {
                    closedExisting = EditorSceneManager.CloseScene(existing, true);
                    if (!closedExisting)
                        throw new InvalidOperationException("Cannot close the clean relationship scene; nothing was overwritten.");
                }
                if (!EditorSceneManager.SaveScene(created, ScenePath))
                    throw new IOException("Unable to save relationship scene: " + ScenePath);
                saved = true;
                Debug.Log("[NarrativeRelationship] Read-only relationship scene saved: " + ScenePath);
            }
            finally
            {
                if (saved && wasLoaded)
                {
                    // Keep the replacement rather than reopening the same path while it is loaded.
                    if (wasActive) SceneManager.SetActiveScene(created);
                    else RestoreActive(previous);
                }
                else
                {
                    RestoreActive(previous);
                    if (closedExisting)
                    {
                        // A failed save must not leave the old target silently unloaded.
                        // created is an unsaved additive scene on ordinary SaveScene failure.
                        Scene reopened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                        if (wasActive) SceneManager.SetActiveScene(reopened);
                        else RestoreActive(previous);
                    }
                    if (created.IsValid() && created.isLoaded)
                        EditorSceneManager.CloseScene(created, true);
                }
            }
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before creating or opening the relationship scene.");
        }

        private static void RequireCleanTarget(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                throw new InvalidOperationException("Save or explicitly discard changes in the relationship scene before rebuilding.");
        }

        private static void RestoreActive(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
        }

        private static GameObject FindController(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponent<NarrativeRelationshipSceneController>() != null) return root;
            return null;
        }

        private static void Build(Scene scene)
        {
            var cameraObject = new GameObject("StoryRelationshipCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.transform.position = new Vector3(0, 0, -10);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .065f, .085f);
            camera.cullingMask = 0;
            var controller = new GameObject("StoryRelationshipGraph", typeof(NarrativeRelationshipSceneController));
            SceneManager.MoveGameObjectToScene(controller, scene);
        }
    }

    /// <summary>The same graph view as the scene, available without entering Play Mode.</summary>
    public sealed class NarrativeRelationshipWindow : EditorWindow
    {
        [NonSerialized] private NarrativeRelationshipGraphView _view;

        [MenuItem("Tools/Narrative/Story Relationship Window")]
        public static void Open()
        {
            var window = GetWindow<NarrativeRelationshipWindow>("故事关系图");
            window.minSize = new Vector2(800, 480);
            window.Show();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
        }

        private void OnGUI()
        {
            if (_view == null) _view = new NarrativeRelationshipGraphView();
            _view.Draw(new Rect(0, 0, position.width, position.height));
            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)
                Repaint();
        }

        private void OnDisable()
        {
            _view?.Dispose();
            _view = null;
        }
    }
}

namespace Spotlight.Narrative.Editor
{
    [CustomEditor(typeof(NarrativeRelationshipSceneController))]
    public sealed class NarrativeRelationshipSceneInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("这是只读剧情关系图。进入 Play 后在 Game 视图浏览；也可以直接打开编辑器窗口，无需运行任何场景。不会启动 NCS、结算剧情或写入存档。", MessageType.Info);
            if (GUILayout.Button("打开故事关系图窗口", GUILayout.Height(30))) NarrativeRelationshipWindow.Open();
            EditorGUILayout.LabelField("场景", NarrativeRelationshipSceneController.ScenePath);
        }
    }
}
