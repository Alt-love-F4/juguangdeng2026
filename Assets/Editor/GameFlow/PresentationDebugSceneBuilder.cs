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
        private const string FontPath = "Assets/Resources/Fonts/乐米曲奇方块体 SDF.asset";
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

            // The legacy overlay root stays at 1000 for compatibility; ApplyLayout puts all
            // shortcut graphics in a nested order-60 canvas below actual presentations.
            GameObject helpCanvas = CanvasObject("DebugHelpOverlay", 1000, scene);
            CanvasGroup help = helpCanvas.AddComponent<CanvasGroup>();
            Image sidebar = Image(helpCanvas.transform, "ShortcutSurface", new Vector2(.74f, .025f), new Vector2(.99f, .975f), Surface);
            sidebar.raycastTarget = true;
            Text(sidebar.transform, "Title", "演出测试 / QWERTY", new Vector2(.045f, .94f), new Vector2(.955f, .985f), 25f, Warm);
            TMP_Text status = Text(sidebar.transform, "Status", "状态 / 待机", new Vector2(.045f, .905f), new Vector2(.955f, .94f), 20f, Pink);
            TMP_Text active = Text(sidebar.transform, "Active", "当前 / 无", new Vector2(.045f, .872f), new Vector2(.955f, .905f), 17f, Muted);

            PresentationDebugSceneController controller = root.AddComponent<PresentationDebugSceneController>();
            string[] labels = { "Q   新一天 / 日期切换", "W   NCS 来信（Shift：草稿）", "E   连续演出 / 选择入口", "R   经营前阶段", "T   开店演出", "Y   经营结束", "U   城区地图", "I   夜晚前阶段", "O   住房夜晚", "P   一天结束", "F   NCS 第一天阿黛尔引入", "G   配表对话特效（1~0）", "V   阅读（图文 / 旁白 / 对话）", "B   单页事件通知", "N   视频过场 / 长按跳过", "C   连续演出 / 选项分支", "M   详情面板（全占位）", "L   长加载测试", "K   短加载测试", "游戏结束 / 返回主菜单", "Z   开场序章（独立预览）" };
            for (int i = 0; i < labels.Length; i++)
            {
                float top = .855f - i * .021f;
                Button button = Button(sidebar.transform, "Preview_" + i, labels[i], new Vector2(.045f, top - .021f), new Vector2(.955f, top));
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
            ApplyLayout(stage.transform, sidebar.transform);
        }

        /// <summary>Compatibility entry point. Upgrades the loaded clean scene without saving it.</summary>
        [MenuItem("Tools/Game Flow/Update Details Debug Entry")]
        [MenuItem("Tools/Game Flow/Update Dialogue Options Debug Entry")]
        [MenuItem("Tools/Game Flow/Update Reading Notice CutScene Debug Entries")]
        [MenuItem("Tools/Game Flow/Update Loading Debug Entries")]
        public static void UpdateContentEntries() => UpgradeLayout();

        /// <summary>
        /// Upgrades only the existing Stage and ShortcutSurface UI subtrees. Requires the debug
        /// scene to be loaded and clean; never opens, closes, activates or saves a scene.
        /// Existing buttons, UnityEvents, controller bindings and unknown user children survive.
        /// </summary>
        [MenuItem("Tools/Game Flow/Upgrade Presentation Debug Layout")]
        public static void UpgradeLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open the presentation debug scene before upgrading its layout.");
            if (scene.isDirty)
                throw new InvalidOperationException("Debug scene has unsaved changes. Save or discard them explicitly before UpgradeLayout; nothing was changed.");
            Transform stage = null, sidebar = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "DebugRoomCanvas")
                {
                    if (stage != null) throw new InvalidOperationException("Ambiguous DebugRoomCanvas roots.");
                    stage = root.transform.Find("Stage");
                }
                if (root.name == "DebugHelpOverlay")
                {
                    if (sidebar != null) throw new InvalidOperationException("Ambiguous DebugHelpOverlay roots.");
                    sidebar = root.transform.Find("ShortcutSurface");
                }
            }
            if (stage == null || sidebar == null || !(stage is RectTransform) || !(sidebar is RectTransform))
                throw new InvalidOperationException("Expected DebugRoomCanvas/Stage and DebugHelpOverlay/ShortcutSurface UI were not found.");
            // Resolve every required existing object before the first mutation. Never replace a
            // controller reference or a user's persistent listener with a freshly generated one.
            Require<TMP_Text>(stage, "Title");
            Require<TMP_Text>(stage, "Description");
            Require<TMP_Text>(stage, "AgentNotice");
            foreach (string name in new[] { "Title", "Status", "Active", "Controls", "Hint" }) Require<TMP_Text>(sidebar, name);
            Require<Button>(sidebar, "Stop"); Require<Button>(sidebar, "Replay");
            for (int i = 0; i < 21; i++) Require<Button>(sidebar, "Preview_" + i);
            for (int i = 0; i < 10; i++) Require<Button>(sidebar, "Effect_" + i);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Upgrade Presentation Debug Layout");
            try
            {
                Undo.RegisterFullObjectHierarchyUndo(stage.gameObject, "Upgrade debug stage");
                Undo.RegisterFullObjectHierarchyUndo(sidebar.gameObject, "Upgrade debug shortcuts");
                ApplyLayout(stage, sidebar);
                Undo.CollapseUndoOperations(undoGroup);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[PresentationDebug] Layout upgraded in memory only. Review it, then save the scene explicitly (or Undo). Scene GUID and controller bindings were preserved.");
        }

        private static T Require<T>(Transform root, string name) where T : Component
        {
            T result = null;
            foreach (T item in root.GetComponentsInChildren<T>(true))
            {
                if (item.name != name) continue;
                if (result != null) throw new InvalidOperationException("Ambiguous UI object: " + name);
                result = item;
            }
            if (result == null) throw new InvalidOperationException("Missing existing UI object: " + name);
            return result;
        }

        private static Transform Container(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;
            GameObject child = Child(parent, name);
            Undo.RegisterCreatedObjectUndo(child, "Create debug layout container");
            return child.transform;
        }

        private static void Place(Transform item, Transform parent, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
        {
            if (item.parent != parent) Undo.SetTransformParent(item, parent, "Group debug UI");
            var rect = (RectTransform)item;
            Rect(rect, min, max);
            rect.offsetMin = low; rect.offsetMax = high;
        }

        private static void FixedRow(Transform item, Transform parent, float height)
        {
            Place(item, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            LayoutElement layout = item.GetComponent<LayoutElement>();
            if (layout == null) layout = Undo.AddComponent<LayoutElement>(item.gameObject);
            layout.minHeight = layout.preferredHeight = height;
            layout.flexibleHeight = 0;
            item.SetAsLastSibling();
        }

        private static void Heading(Transform content, string name, string caption)
        {
            Transform existing = content.Find(name);
            TMP_Text text;
            if (existing != null) text = existing.GetComponent<TMP_Text>();
            else
            {
                text = Text(content, name, caption, Vector2.zero, Vector2.one, 24, Pink);
                Undo.RegisterCreatedObjectUndo(text.gameObject, "Create debug group heading");
            }
            text.text = caption; text.fontSize = 24; text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.BottomLeft;
            FixedRow(text.transform, content, 42);
        }

        private static void ApplyLayout(Transform stage, Transform sidebar)
        {
            Rect((RectTransform)stage, new Vector2(.025f, .08f), new Vector2(.665f, .92f));
            Require<TMP_Text>(stage, "Title").text = "PRESENTATION\n演出调试室";
            Require<TMP_Text>(stage, "Description").text = "所有配表演出与 UI 样例的共享入口\n\n右侧按类型分组，可滚动选择。\nE 打开连续演出二级菜单：选择事件入口，跟随选项与检定连续播放。\n\n仅预览，不启动经营；分支结果只影响临时状态，不写入正式存档。";
            Require<TMP_Text>(stage, "AgentNotice").text = "播放时按 H 收起说明，F12 恢复。\n复用本场景，不为单个演出创建临时 Scene。";
            Rect((RectTransform)sidebar, new Vector2(.69f, .025f), new Vector2(.985f, .975f));
            // Keep the legacy 1000 root untouched. Its only graphics live in this nested canvas,
            // whose absolute order stays below the real 700/800 panels instead of covering them.
            Canvas surfaceCanvas = sidebar.GetComponent<Canvas>();
            if (surfaceCanvas == null) surfaceCanvas = Undo.AddComponent<Canvas>(sidebar.gameObject);
            surfaceCanvas.overrideSorting = true;
            surfaceCanvas.sortingOrder = 60;
            if (sidebar.GetComponent<GraphicRaycaster>() == null) Undo.AddComponent<GraphicRaycaster>(sidebar.gameObject);
            TMP_Text title = Require<TMP_Text>(sidebar, "Title");
            title.text = "演出入口  /  滚动选择"; title.fontSize = 28; title.enableAutoSizing = false;
            Place(title.transform, sidebar, new Vector2(0, 1), Vector2.one, new Vector2(20, -58), new Vector2(-20, -12));

            Transform list = Container(sidebar, "EntryScroll");
            Image listImage = list.GetComponent<Image>();
            if (listImage == null) listImage = Undo.AddComponent<Image>(list.gameObject);
            listImage.color = new Color(.08f, .07f, .11f, .95f);
            Place(list, sidebar, Vector2.zero, Vector2.one, new Vector2(16, 300), new Vector2(-16, -70));
            Transform viewport = Container(list, "Viewport");
            Rect((RectTransform)viewport, Vector2.zero, Vector2.one);
            ((RectTransform)viewport).offsetMax = new Vector2(-22, 0);
            if (viewport.GetComponent<RectMask2D>() == null) Undo.AddComponent<RectMask2D>(viewport.gameObject);
            Transform content = Container(viewport, "Content");
            Rect((RectTransform)content, new Vector2(0, 1), Vector2.one);
            ((RectTransform)content).pivot = new Vector2(.5f, 1);
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = Undo.AddComponent<VerticalLayoutGroup>(content.gameObject);
            layout.padding = new RectOffset(10, 10, 4, 14); layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = Undo.AddComponent<ContentSizeFitter>(content.gameObject);
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = list.GetComponent<ScrollRect>();
            if (scroll == null) scroll = Undo.AddComponent<ScrollRect>(list.gameObject);
            scroll.viewport = (RectTransform)viewport; scroll.content = (RectTransform)content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 48;
            Transform track = Container(list, "Scrollbar");
            Place(track, list, new Vector2(1, 0), Vector2.one, new Vector2(-18, 6), new Vector2(-4, -6));
            Image trackImage = track.GetComponent<Image>();
            if (trackImage == null) trackImage = Undo.AddComponent<Image>(track.gameObject);
            trackImage.color = new Color(.18f, .15f, .21f);
            Transform handle = Container(track, "Handle");
            Rect((RectTransform)handle, Vector2.zero, Vector2.one);
            Image handleImage = handle.GetComponent<Image>();
            if (handleImage == null) handleImage = Undo.AddComponent<Image>(handle.gameObject);
            handleImage.color = Muted;
            Scrollbar bar = track.GetComponent<Scrollbar>();
            if (bar == null) bar = Undo.AddComponent<Scrollbar>(track.gameObject);
            bar.targetGraphic = handleImage; bar.handleRect = (RectTransform)handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;

            string[] labels = { "Q   新一天 / 日期切换", "W   来信（Shift：草稿）", "E   连续演出 / 选择入口", "R   经营前阶段", "T   开店演出", "Y   经营结束", "U   城区地图", "I   夜晚前阶段", "O   住房夜晚", "P   一天结束", "F   第一天引入", "G   配表演出 / 特效预览", "V   阅读（图文 / 旁白）", "B   单页事件通知", "N   视频过场 / 长按跳过", "C   连续演出 / 选项分支", "M   详情面板（占位）", "L   长加载测试", "K   短加载测试", "游戏结束 / 返回主菜单", "Z   开场序章（独立预览）" };
            Heading(content, "ConfiguredHeading", "01  配表与叙事");
            foreach (int i in new[] { 0, 1, 2, 15, 10, 20, 12, 13, 14 }) LayoutEntry(sidebar, content, i, labels[i]);
            Heading(content, "FlowHeading", "02  日程与场景");
            foreach (int i in new[] { 3, 4, 5, 6, 7, 8, 9, 19 }) LayoutEntry(sidebar, content, i, labels[i]);
            Heading(content, "ToolsHeading", "03  面板与加载");
            foreach (int i in new[] { 16, 17, 18 }) LayoutEntry(sidebar, content, i, labels[i]);
            Heading(content, "EffectsHeading", "04  特效试验台");
            LayoutEntry(sidebar, content, 11, labels[11]);
            for (int i = 0; i < 10; i++)
            {
                Button effect = Require<Button>(sidebar, "Effect_" + i);
                FixedRow(effect.transform, content, 48);
                TMP_Text label = effect.GetComponentInChildren<TMP_Text>(true);
                if (label != null) { label.fontSize = 23; label.enableAutoSizing = false; }
            }

            Transform footer = Container(sidebar, "Footer");
            Place(footer, sidebar, Vector2.zero, new Vector2(1, 0), new Vector2(20, 12), new Vector2(-20, 288));
            FooterText(sidebar, footer, "Status", 242, 276, 24, Pink);
            FooterText(sidebar, footer, "Active", 207, 241, 22, Muted);
            Button stop = Require<Button>(sidebar, "Stop"), replay = Require<Button>(sidebar, "Replay");
            Place(stop.transform, footer, Vector2.zero, new Vector2(.48f, 0), new Vector2(0, 148), new Vector2(0, 198));
            Place(replay.transform, footer, new Vector2(.51f, 0), new Vector2(1, 0), new Vector2(0, 148), new Vector2(0, 198));
            FooterText(sidebar, footer, "Controls", 84, 140, 21, Warm).text = "H 隐藏说明 · F12 恢复 · F9 暂停\n空格/回车继续 · Esc 关闭";
            TMP_Text hint = FooterText(sidebar, footer, "Hint", 0, 78, 20, Muted);
            hint.textWrappingMode = TextWrappingModes.Normal;
            hint.overflowMode = TextOverflowModes.Ellipsis;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)content);
            scroll.verticalNormalizedPosition = 1;
        }

        private static TMP_Text FooterText(Transform sidebar, Transform footer, string name, float low, float high, float size, Color color)
        {
            TMP_Text text = Require<TMP_Text>(sidebar, name);
            Place(text.transform, footer, Vector2.zero, new Vector2(1, 0), new Vector2(0, low), new Vector2(0, high));
            text.fontSize = size; text.enableAutoSizing = false; text.color = color;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static void LayoutEntry(Transform sidebar, Transform content, int index, string caption)
        {
            Button button = Require<Button>(sidebar, "Preview_" + index);
            FixedRow(button.transform, content, 56);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) { label.text = caption; label.fontSize = 23; label.enableAutoSizing = false; }
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
                case 16: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayDetails); break;
                case 17: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayLongLoading); break;
                case 18: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayShortLoading); break;
                case 19: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayGameOver); break;
                case 20: UnityEventTools.AddVoidPersistentListener(button.onClick, controller.PlayOpeningPrologue); break;
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
