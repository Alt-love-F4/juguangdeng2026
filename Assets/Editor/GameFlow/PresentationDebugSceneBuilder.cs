using System;
using Spotlight.GameFlow;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>One shared scene, created additively without changing or saving the user's current scene.</summary>
    [InitializeOnLoad]
    public static class PresentationDebugSceneBuilder
    {
        public const string ScenePath = PresentationDebugSceneController.ScenePath;
        private const string FontPath = "Assets/Resources/Fonts/SimHei SDF.asset";
        private static readonly Color Surface = new Color(.12f, .10f, .16f, .97f);
        private static readonly Color Warm = new Color(1f, .84f, .69f);
        private static readonly Color Pink = new Color(.96f, .66f, .68f);
        private static readonly Color Muted = new Color(.73f, .69f, .76f);

        static PresentationDebugSceneBuilder() { EditorApplication.delayCall += EnsureSceneExists; }
        private static void EnsureSceneExists()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !System.IO.File.Exists(ScenePath)) CreateMissing();
        }

        [MenuItem("Tools/Game Flow/Create Missing Presentation Debug Scene")]
        public static void CreateMissing()
        {
            if (System.IO.File.Exists(ScenePath)) return;
            CreateScene();
        }

        [MenuItem("Tools/Game Flow/Open Presentation Debug Scene")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            CreateMissing();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/Game Flow/Rebuild Presentation Debug Scene")]
        public static void Rebuild()
        {
            if (!EditorUtility.DisplayDialog("重建共享调试场景", "将覆盖调试场景内的自定义布局，其他场景与演出 Prefab 不变。继续？", "重建", "取消")) return;
            CreateScene();
        }

        // For a deliberate asset rebuild; preserves the existing scene GUID and does not alter Build Settings.
        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) == null) throw new InvalidOperationException("Missing SimHei SDF font.");
            Scene previous = SceneManager.GetActiveScene();
            Scene existing = SceneManager.GetSceneByPath(ScenePath);
            if (existing.IsValid() && existing.isLoaded && existing.isDirty)
                throw new InvalidOperationException("Save or discard changes in the debug scene before rebuilding.");
            bool reopenExisting = existing.IsValid() && existing.isLoaded;
            bool previousWasDebug = previous == existing;
            bool closedExisting = false;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                Build(scene);
                // Unity cannot save over another loaded scene's path. Close only this clean debug scene,
                // after construction succeeds; unrelated scenes and their unsaved changes stay untouched.
                if (reopenExisting) closedExisting = EditorSceneManager.CloseScene(existing, true);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Cannot save debug scene.");
                Debug.Log("[PresentationDebug] Shared scene saved: " + ScenePath);
            }
            finally
            {
                Scene reopened = default;
                if (closedExisting) reopened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                if (previousWasDebug && reopened.IsValid()) SceneManager.SetActiveScene(reopened);
                else if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void Build(Scene scene)
        {
            var camera = new GameObject("DebugCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(camera, scene);
            Camera view = camera.GetComponent<Camera>();
            view.orthographic = true;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(.065f, .055f, .085f);
            camera.transform.position = new Vector3(0f, 0f, -10f);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            GameObject root = CanvasObject("DebugRoomCanvas", 50, scene);
            Image backdrop = Image(root.transform, "Background", Vector2.zero, Vector2.one, new Color(.07f, .06f, .09f));
            backdrop.raycastTarget = false;
            Image stage = Image(root.transform, "Stage", new Vector2(.035f, .08f), new Vector2(.71f, .91f), Surface);
            Text(stage.transform, "Title", "PRESENTATION\nDEBUG ROOM", new Vector2(.065f, .65f), new Vector2(.94f, .93f), 54f, Warm);
            Text(stage.transform, "Description", "共享演出 UI 调试场\n\n不启动 GameFlow、不加载经营、不写存档。\n右侧按钮和键盘都可直接播放真实演出 Prefab。\n\n演出结束后返回这里；随时切换、重播或取消。", new Vector2(.065f, .24f), new Vector2(.94f, .63f), 27f, Color.white);
            Text(stage.transform, "AgentNotice", "AGENT NOTE\n复用本场景，不要为单个演出新建临时 Scene。", new Vector2(.065f, .07f), new Vector2(.94f, .21f), 23f, Pink);

            // Only this narrow sidebar sits above panels. The full-screen debug room must stay BELOW 700/800.
            GameObject helpCanvas = CanvasObject("DebugHelpOverlay", 1000, scene);
            CanvasGroup help = helpCanvas.AddComponent<CanvasGroup>();
            Image sidebar = Image(helpCanvas.transform, "ShortcutSurface", new Vector2(.74f, .025f), new Vector2(.99f, .975f), Surface);
            sidebar.raycastTarget = true;
            Text(sidebar.transform, "Title", "演出测试 / QWERTY", new Vector2(.045f, .94f), new Vector2(.955f, .985f), 25f, Warm);
            TMP_Text status = Text(sidebar.transform, "Status", "状态 / 待机", new Vector2(.045f, .905f), new Vector2(.955f, .94f), 20f, Pink);
            TMP_Text active = Text(sidebar.transform, "Active", "当前 / 无", new Vector2(.045f, .872f), new Vector2(.955f, .905f), 17f, Muted);

            PresentationDebugSceneController controller = root.AddComponent<PresentationDebugSceneController>();
            string[] labels = { "Q   新一天 / 日期切换", "W   NCS 来信（Shift：草稿）", "E   NCS 对话（Shift：长文本测试）", "R   经营前阶段", "T   开店演出", "Y   经营结束", "U   城区地图", "I   夜晚前阶段", "O   住房夜晚", "P   一天结束", "F   NCS 第一天阿黛尔引入", "G   特效试验台（1~0）", "V   阅读（图文 / 旁白 / 对话）", "B   单页事件通知", "N   视频过场 / 长按跳过", "C   三人选项对话" };
            for (int i = 0; i < labels.Length; i++)
            {
                float top = .855f - i * .0263f;
                Button button = Button(sidebar.transform, "Preview_" + i, labels[i], new Vector2(.045f, top - .025f), new Vector2(.955f, top));
                AddPreviewListener(button, controller, i);
            }
            string[] effectLabels = { "1 黑影入场", "2 受惊", "3 轻震屏", "4 重震屏", "5 白闪", "6 黑场切换", "7 换位", "8 镜头推近", "9 镜头复位", "0 淡出" };
            for (int i = 0; i < effectLabels.Length; i++)
            {
                float top = .426f - i / 2 * .027f;
                float left = i % 2 == 0 ? .045f : .51f;
                Button effectButton = Button(sidebar.transform, "Effect_" + i, effectLabels[i],
                    new Vector2(left, top - .023f), new Vector2(left + .445f, top));
                AddEffectListener(effectButton, controller, i);
            }
            Button stop = Button(sidebar.transform, "Stop", "Esc 关闭", new Vector2(.045f, .258f), new Vector2(.47f, .297f));
            Button replay = Button(sidebar.transform, "Replay", "Backspace 重播", new Vector2(.495f, .258f), new Vector2(.955f, .297f));
            UnityEventTools.AddVoidPersistentListener(stop.onClick, controller.StopCurrent);
            UnityEventTools.AddVoidPersistentListener(replay.onClick, controller.ReplayLast);
            Text(sidebar.transform, "Controls", "H 显示说明 · F12 恢复 · F9 暂停\n空格/回车：补全 → 继续\n阅读 ← 返回 · 通知确认关闭\n视频：长按 Enter 跳过", new Vector2(.045f, .14f), new Vector2(.955f, .248f), 18f, Muted);
            TMP_Text hint = Text(sidebar.transform, "Hint", PresentationDebugSceneController.DefaultHint,
                new Vector2(.045f, .02f), new Vector2(.955f, .13f), 17f, Pink);
            var data = new SerializedObject(controller);
            data.FindProperty("statusText").objectReferenceValue = status;
            data.FindProperty("activeText").objectReferenceValue = active;
            data.FindProperty("hintText").objectReferenceValue = hint;
            data.FindProperty("helpGroup").objectReferenceValue = help;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Patch only this scene's shortcut list, preserving camera, scene GUID and other user objects.</summary>
        [MenuItem("Tools/Game Flow/Update Dialogue Options Debug Entry")]
        [MenuItem("Tools/Game Flow/Update Reading Notice CutScene Debug Entries")]
        public static void UpdateContentEntries()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            CreateMissing();
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("Save the debug scene before updating shortcuts.");
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                PresentationDebugSceneController controller = null;
                Transform sidebar = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    var found = root.GetComponentInChildren<PresentationDebugSceneController>();
                    if (found != null) controller = found;
                    if (root.name == "DebugHelpOverlay") sidebar = root.transform.Find("ShortcutSurface");
                }
                if (controller == null || sidebar == null) throw new InvalidOperationException("Shared debug scene bindings not found.");
                string[] labels = { "V   阅读（图文 / 旁白 / 对话）", "B   单页事件通知", "N   视频过场 / 长按跳过", "C   三人选项对话" };
                for (int i = 0; i < 16; i++)
                {
                    Transform entry = sidebar.Find("Preview_" + i);
                    float top = .855f - i * .0263f;
                    if (entry == null && i >= 12)
                    {
                        Button button = Button(sidebar, "Preview_" + i, labels[i - 12], new Vector2(.045f, top - .025f), new Vector2(.955f, top));
                        AddPreviewListener(button, controller, i);
                    }
                    else if (entry != null) Rect((RectTransform)entry, new Vector2(.045f, top - .025f), new Vector2(.955f, top));
                }
                for (int i = 0; i < 10; i++)
                {
                    Transform effect = sidebar.Find("Effect_" + i);
                    if (effect == null) continue;
                    float top = .426f - i / 2 * .027f;
                    float left = i % 2 == 0 ? .045f : .51f;
                    Rect((RectTransform)effect, new Vector2(left, top - .023f), new Vector2(left + .445f, top));
                }
                var controls = sidebar.Find("Controls")?.GetComponent<TMP_Text>();
                if (controls != null) controls.text = "H 显示说明 · F12 恢复 · F9 暂停\n空格/回车：补全 → 继续\n阅读 ← 返回 · 通知确认关闭\n视频：长按 Enter 跳过";
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save shortcut update.");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static void AddPreviewListener(Button button, PresentationDebugSceneController controller, int index)
        {
            switch (index)
            {
                case 0: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayDateChange); break;
                case 1: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayLetter); break;
                case 2: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayDialogue); break;
                case 3: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayPreBusiness); break;
                case 4: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayCafeOpening); break;
                case 5: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayBusinessEnd); break;
                case 6: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayCityMap); break;
                case 7: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayPreNight); break;
                case 8: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayHomeNight); break;
                case 9: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayDayEnd); break;
                case 10: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayIntroduction); break;
                case 11: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayEffects); break;
                case 12: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayReading); break;
                case 13: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayNotice); break;
                case 14: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayCutScene); break;
                case 15: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayDialogueOptions); break;
            }
        }

        private static void AddEffectListener(Button button, PresentationDebugSceneController controller, int index)
        {
            switch (index)
            {
                case 0: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PreviewEnter); break;
                case 1: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PreviewStartle); break;
                case 2: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PreviewShakeLight); break;
                case 3: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PreviewShakeHeavy); break;
                case 4: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PreviewFlash); break;
                case 5: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PreviewBlack); break;
                case 6: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PreviewSwap); break;
                case 7: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PreviewZoom); break;
                case 8: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PreviewResetCamera); break;
                case 9: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PreviewExit); break;
            }
        }

        private static GameObject CanvasObject(string name, int order, Scene scene)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(root, scene);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            return root;
        }
        private static GameObject Child(Transform parent, string name, params Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            foreach (Type type in components) go.AddComponent(type);
            return go;
        }
        private static void Rect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static Image Image(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            Image image = Child(parent, name, typeof(Image)).GetComponent<Image>();
            Rect(image.rectTransform, min, max); image.color = color; image.raycastTarget = false;
            return image;
        }
        private static TMP_Text Text(Transform parent, string name, string value, Vector2 min, Vector2 max, float size, Color color)
        {
            TMP_Text text = Child(parent, name, typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            Rect(text.rectTransform, min, max);
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            text.fontSize = size; text.text = value; text.color = color; text.raycastTarget = false;
            text.richText = false;
            text.enableAutoSizing = true; text.fontSizeMin = size * .75f; text.fontSizeMax = size;
            return text;
        }
        private static Button Button(Transform parent, string name, string value, Vector2 min, Vector2 max)
        {
            Image image = Image(parent, name, min, max, new Color(.24f, .18f, .25f));
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            Navigation navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            TMP_Text label = Text(image.transform, "Label", value, new Vector2(.04f, .08f), new Vector2(.96f, .92f), 21f, Warm);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return button;
        }
    }
}
