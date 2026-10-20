using System;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>Map task view data. Supply stage history from the task system through SetItems.</summary>
    [Serializable]
    public sealed class MapTaskItem
    {
        public string id;
        public string actionKey;
        public string name;
        public string description;
        public bool completed;
        public bool important;
        public string currentTask;

        public int remainingDays;
        public string location;
        public string positionKey;
        public string status;
        public string navigationLabel;
        public bool preventsRest;
        public bool available = true;
        public bool optional;
        public string blockedReason;
        public int priority = 10;
        public List<string> previousTasks = new List<string>();
    }

    /// <summary>One expanded item and one tracked item per panel; no task progression is inferred by the UI.</summary>
    public sealed class MapTaskPanel : MonoBehaviour
    {
        private readonly List<MapTaskItem> items = new List<MapTaskItem>();
        private RectTransform panel, body, content;
        private Button toggle, activeTab, completedTab;
        private TextMeshProUGUI summary;
        private ScrollRect scroll;
        private bool expanded, showCompleted;
        private string expandedId, trackedId;
        public string TrackedId => trackedId;
        public string ExpandedId => expandedId;
        public Button GetItemButton(string id) => FindVisibleButton("Task_" + id);
        public Button GetTrackingButton(string id) => FindVisibleButton("Track_" + id);
        private Button FindVisibleButton(string name)
        {
            if (content == null || !IsVisible) return null;
            foreach (Transform child in content)
                if (child.name == name && child.gameObject.activeInHierarchy) return child.GetComponent<Button>();
            return null;
        }
        public int ItemCount => items.Count;
        public event Action<string> TrackingChanged;
        public event Action<MapTaskItem> NavigationRequested;
        public event Action ItemsChanged;
        public IReadOnlyList<MapTaskItem> Items => items.AsReadOnly();
        public MapTaskItem CurrentGuidance => GameplayTodoProjection.SelectNext(items, trackedId);
        private readonly List<IGameplayTodoSource> guidanceSources = new List<IGameplayTodoSource>();
        private string itemSignature;
        private bool restGuidanceAvailable;
        private bool cafeTravelAvailable;
        public void SetCafeTravelAvailable(bool value) => cafeTravelAvailable = value;
        public bool CanNavigateToRest => items.Any(i => i.id == GameplayTodoProjection.RestTodoId && !i.completed);
        public void SetRestGuidanceAvailable(bool value) => restGuidanceAvailable = value;
        public void SetGuidanceSources(IEnumerable<IGameplayTodoSource> sources)
        {
            guidanceSources.Clear();
            if (sources != null) guidanceSources.AddRange(sources);
        }
        private const float Width = 550f;
        private MapSidebarView sidebar;
        [SerializeField] private bool showEditorPreview = true;
        [SerializeField] private RectTransform taskRowTemplate;
        [SerializeField] private RectTransform detailTextTemplate;
        [SerializeField] private RectTransform trackButtonTemplate;
        public bool IsVisible => sidebar != null && sidebar.IsVisible;
        private NarrativeActionController actionSource;
        private bool externalItems;

        public static bool ShouldListAction(NarrativeActionDefinition action) => action != null;

        public void RefreshSource() { BindActions(); RefreshActions(); }

        private void BindActions()
        {
            var source = NarrativeRuntime.Instance?.Actions;
            if (source == actionSource) return;
            if (actionSource != null) { actionSource.StateChanged -= RefreshActions; actionSource.MapContentChanged -= OnMapContentChanged; }
            actionSource = source;
            if (source != null) { source.StateChanged += RefreshActions; source.MapContentChanged += OnMapContentChanged; }
            RefreshActions();
        }

        private void OnMapContentChanged(string _) => RefreshActions();
        private void OnEnable() { if (Application.isPlaying) BindActions(); }
        private void OnDestroy()
        {
            if (actionSource == null) return;
            actionSource.StateChanged -= RefreshActions; actionSource.MapContentChanged -= OnMapContentChanged;
        }

        private void RefreshActions()
        {
            if (externalItems) return;
            var runtime = NarrativeRuntime.Instance;
            var values = GameplayTodoProjection.Build(runtime, position => guidanceSources.All(s => s.AllowsTodoPosition(position)));
            foreach (var source in guidanceSources) source.CollectTodos(values);
            GameplayTodoProjection.AppendRestGuidance(values, runtime, restGuidanceAvailable);
            if (cafeTravelAvailable && !values.Any(v => v.id == "guide.map.cafe"))
                values.Add(new MapTaskItem { id = "guide.travel.cafe", name = "前往咖啡馆", location = "咖啡馆",
                    description = "从当前场景返回咖啡馆，继续经营或前往三楼宿舍。",
                    currentTask = "点击「前往咖啡馆」返回咖啡馆；需要休息时再到三楼点击床。",
                    navigationLabel = "前往咖啡馆", status = "可前往", optional = true, priority = 50 });
            values = GameplayTodoProjection.Order(values).ToList();
            string signature = string.Join("\n", values.Select(v => string.Join("|", v.id, v.name, v.description,
                v.currentTask, v.completed, v.important, v.location, v.positionKey, v.status, v.navigationLabel, v.remainingDays, v.priority, v.available, v.optional, v.blockedReason)));
            if (signature == itemSignature) return;
            itemSignature = signature;
            ReplaceItems(values);
        }

        public void Initialize()
        {
            panel = (RectTransform)transform;
            sidebar = GetComponent<MapSidebarView>();
            if (sidebar == null) sidebar = gameObject.AddComponent<MapSidebarView>();
            sidebar.Initialize();
            if (Application.isPlaying) BindActions();
            if (transform.Find("TaskBody/TaskViewport/TaskContent") is RectTransform existingContent)
            {
                content = existingContent;
                body = (RectTransform)transform.Find("TaskBody");
                summary = transform.Find("TrackedSummary")?.GetComponent<TextMeshProUGUI>();
                toggle = transform.Find("TaskToggle")?.GetComponent<UnityEngine.UI.Button>();
                activeTab = body.Find("InProgressTab").GetComponent<UnityEngine.UI.Button>();
                completedTab = body.Find("CompletedTab").GetComponent<UnityEngine.UI.Button>();
                scroll = body.Find("TaskViewport").GetComponent<ScrollRect>();
                // Existing Prefab hierarchies own geometry and artwork; bind behaviour only.
                if (summary != null) summary.gameObject.SetActive(false);
                if (toggle != null) { toggle.onClick.RemoveAllListeners(); toggle.onClick.AddListener(Toggle); }
                if (activeTab != null) { activeTab.onClick.RemoveAllListeners(); activeTab.onClick.AddListener(() => SelectTab(false)); }
                if (completedTab != null) { completedTab.onClick.RemoveAllListeners(); completedTab.onClick.AddListener(() => SelectTab(true)); }
                Spotlight.Audio.UIButtonAudio.Configure(toggle);
                Spotlight.Audio.UIButtonAudio.Configure(activeTab, string.Empty);
                Spotlight.Audio.UIButtonAudio.Configure(completedTab, string.Empty);
                if (content.childCount > 0 && taskRowTemplate == null)
                    taskRowTemplate = content.GetChild(0) as RectTransform;
                if (Application.isPlaying || !showEditorPreview) Refresh();
                return;
            }
            MapVisualTheme.Skin(GetComponent<Image>(), MapVisualTheme.Current?.panel);
            var heading = MapVisualTheme.Art("TaskTitle", panel, MapVisualTheme.Current?.taskTitle, Vector2.zero, new Vector2(430, 106));
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(.5f, 1);
            heading.rectTransform.anchoredPosition = new Vector2(0, -78);
            summary = Label("TrackedSummary", panel, "", 17);
            Top(summary.rectTransform, 12, -8, Width - 68, 76);
            toggle = Button("TaskToggle", panel, "×", Toggle);
            Top((RectTransform)toggle.transform, Width - 48, -16, 32, 32);
            body = Rect("TaskBody", panel);
            body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(16, 20); body.offsetMax = new Vector2(-16, -104);
            activeTab = Button("InProgressTab", body, "进行中", () => SelectTab(false));
            Top((RectTransform)activeTab.transform, 8, -8, 224, 44);
            completedTab = Button("CompletedTab", body, "已完成", () => SelectTab(true));
            Spotlight.Audio.UIButtonAudio.Configure(activeTab, string.Empty);
            Spotlight.Audio.UIButtonAudio.Configure(completedTab, string.Empty);
            Top((RectTransform)completedTab.transform, 254, -8, 224, 44);

            RectTransform viewport = Rect("TaskViewport", body);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(8, 8);
            viewport.offsetMax = new Vector2(-8, -66);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            // Attach the scroll receiver only to the list region, leaving the tabs outside it.
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            content = Rect("TaskContent", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            scroll.content = content;
            ConfigureLayout();
            if (Application.isPlaying || !showEditorPreview) Refresh();
        }

        private void ConfigureLayout()
        {
            MapVisualTheme.Skin(GetComponent<Image>(), MapVisualTheme.Current?.panel);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(24, 92); body.offsetMax = new Vector2(-24, -156);
            scroll.viewport.GetComponent<Image>().color = Color.clear;
            summary.gameObject.SetActive(false);
        }

        public void Toggle()
        {
            SetVisible(!IsVisible);
        }
        public void SetVisible(bool value)
        {
            // The inventory and task buttons can call this before the panel's first explicit initialization.
            if (sidebar == null) Initialize();
            if (Application.isPlaying) BindActions();
            expanded = value;
            sidebar.SetVisible(value);
        }

        public bool ShowImportantTodo()
        {
            RefreshSource();
            var item = items.Find(i => !i.completed && i.important);
            if (item == null) return false;
            showCompleted = false;
            expandedId = item.id;
            SetTracked(item.id);
            SetVisible(true);
            Refresh();
            if (scroll != null)
            {
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 1;
            }
            return true;
        }

        public void SetItems(IEnumerable<MapTaskItem> values)
        {
            externalItems = true;
            ReplaceItems(values);
        }

        private void ReplaceItems(IEnumerable<MapTaskItem> values)
        {
            items.Clear();
            var ids = new HashSet<string>();
            if (values != null)
                foreach (MapTaskItem item in values)
                    if (item != null && !string.IsNullOrEmpty(item.id) && ids.Add(item.id))
                    {
                        var action = NarrativeRuntime.Instance?.Actions?.Get(string.IsNullOrEmpty(item.actionKey) ? item.id : item.actionKey);
                        if (action == null || ShouldListAction(action)) items.Add(item);
                    }
            var sorted = GameplayTodoProjection.Order(items).ToArray();
            items.Clear();
            items.AddRange(sorted);
            if (items.Find(item => item.id == expandedId) == null) expandedId = null;
            if (items.Find(item => item.id == trackedId) == null) SetTracked(null);
            else Refresh();
            ItemsChanged?.Invoke();
        }

        public void Navigate(string id)
        {
            var item = items.Find(i => i.id == id && !i.completed);
            if (item == null || string.IsNullOrEmpty(item.navigationLabel)) return;
            SetTracked(id);
            NavigationRequested?.Invoke(item);
        }

        public void SetTracked(string id)
        {
            string next = items.Exists(item => item.id == id) ? id : null;
            bool changed = trackedId != next;
            trackedId = next;
            Refresh();
            if (changed) { TrackingChanged?.Invoke(trackedId); ItemsChanged?.Invoke(); }
        }

        private void SelectTab(bool completed)
        {
            if (showCompleted == completed) return;
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Tab);
            showCompleted = completed;
            expandedId = null;
            Refresh();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
        }

        private void Refresh()
        {
            if (panel == null || body == null || content == null) return;
            MapTaskItem tracked = items.Find(item => item.id == trackedId);
            body.gameObject.SetActive(true);
            if (summary != null) summary.gameObject.SetActive(false);
            if (summary != null) summary.text = tracked == null ? "" : tracked.name + "\n" +
                (tracked.completed ? "已完成" : string.IsNullOrEmpty(tracked.currentTask) ? "暂无当前阶段任务" : tracked.currentTask);
            if (summary != null) summary.overflowMode = TextOverflowModes.Ellipsis;
            var toggleLabel = toggle != null ? toggle.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (toggleLabel != null) toggleLabel.text = "×";
            SetTab(activeTab, !showCompleted);
            SetTab(completedTab, showCompleted);
            if (taskRowTemplate == null && content.Find("Task_map_example_active_0") is RectTransform authoredRow) taskRowTemplate = authoredRow;
            if (detailTextTemplate == null && content.Find("PreviewDetailText") is RectTransform textTemplate) detailTextTemplate = textTemplate;
            if (trackButtonTemplate == null && content.Find("PreviewTrackButton") is RectTransform buttonTemplate) trackButtonTemplate = buttonTemplate;
            // Keep the authored preview row intact; runtime rows are clones of this exact Prefab structure.
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject;
                if (taskRowTemplate != null && (child == taskRowTemplate.gameObject || child.name == taskRowTemplate.name || child.name == "PreviewDetailText" || child.name == "PreviewTrackButton")) continue;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            float y = 0;
            int count = 0;
            foreach (MapTaskItem item in items)
            {
                if (item.completed != showCompleted) continue;
                count++;
                if (taskRowTemplate == null) { Debug.LogError("MapTaskPanel needs an authored task-row template.", this); break; }
                RectTransform row = Instantiate(taskRowTemplate, content, false);
                row.gameObject.SetActive(true); row.name = "Task_" + item.id;
                var title = row.Find("Label")?.GetComponent<TextMeshProUGUI>();
                var description = row.Find("Description")?.GetComponent<TextMeshProUGUI>();
                var location = row.Find("Location")?.GetComponent<TextMeshProUGUI>();
                if (title != null) title.text = (!item.completed && CurrentGuidance?.id == item.id ? "下一步 · " : item.important ? "重要 · " : "") + item.name + (trackedId == item.id ? "  · 追踪中" : "");
                if (description != null)
                {
                    description.text = string.IsNullOrEmpty(item.description) ? item.currentTask : item.description;
                    description.overflowMode = TextOverflowModes.Ellipsis;
                }
                if (title != null) title.overflowMode = TextOverflowModes.Ellipsis;
                if (location != null)
                {
                    location.text = string.IsNullOrEmpty(item.location) ? "" : "地点：" + item.location;
                    location.overflowMode = TextOverflowModes.Ellipsis;
                }
                ConfigureTimerIcons(row, item.remainingDays);
                var rowButton = row.GetComponent<Button>();
                if (rowButton != null) { rowButton.onClick.RemoveAllListeners(); rowButton.onClick.AddListener(() => { expandedId = expandedId == item.id ? null : item.id; Refresh(); }); }
                Spotlight.Audio.UIButtonAudio.Configure(rowButton);
                float height = row.sizeDelta.y;
                Top(row, 0, -y, Width - 64, height); y += height + 14;
                if (!string.IsNullOrEmpty(item.status)) y += AddDetailText(item.status, y, "TodoStatus");
                if (expandedId != item.id && CurrentGuidance?.id != item.id) continue;
                if (item.previousTasks != null)
                    foreach (string task in item.previousTasks) y += AddDetailText("已进行：" + task, y, "PreviousTask");
                if (!item.completed && !string.IsNullOrEmpty(item.blockedReason))
                    y += AddDetailText("尚需完成：" + item.blockedReason, y, "BlockedReason");
                if (!item.completed)
                    y += AddDetailText("当前待办：" + (string.IsNullOrEmpty(item.currentTask) ? "暂无" : item.currentTask), y, "CurrentTask");
                else y += AddDetailText("事项已完成", y, "CompletedStatus");
                if (trackButtonTemplate != null && !item.completed)
                {
                    if (!string.IsNullOrEmpty(item.navigationLabel))
                    {
                        var go = Instantiate(trackButtonTemplate, content, false); go.gameObject.SetActive(true); go.name = "Navigate_" + item.id;
                        var goButton = go.GetComponent<Button>();
                        var goLabel = go.GetComponentInChildren<TextMeshProUGUI>(true);
                        if (goLabel != null) goLabel.text = item.navigationLabel;
                        goButton.onClick.RemoveAllListeners(); goButton.onClick.AddListener(() => Navigate(item.id));
                        Spotlight.Audio.UIButtonAudio.Configure(goButton);
                        Top(go, 8, -y, Width - 64, go.sizeDelta.y); y += go.sizeDelta.y + 8;
                    }
                    var trackRect = Instantiate(trackButtonTemplate, content, false); trackRect.gameObject.SetActive(true); trackRect.name = "Track_" + item.id;
                    var track = trackRect.GetComponent<Button>();
                    var label = trackRect.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (label != null) label.text = trackedId == item.id ? "取消追踪" : "追踪此待办";
                    track.onClick.RemoveAllListeners(); track.onClick.AddListener(() => SetTracked(trackedId == item.id ? null : item.id));
                    Spotlight.Audio.UIButtonAudio.Configure(track);
                    Top(trackRect, 8, -y, Width - 64, trackRect.sizeDelta.y); y += trackRect.sizeDelta.y + 8;
                }
            }
            if (count == 0) y += AddDetailText(showCompleted ? "暂无已完成事项" : "暂无待办，可前往咖啡馆或查看地图。", y, "Empty");
            bool showPreview = !Application.isPlaying && showEditorPreview;
            if (taskRowTemplate != null) taskRowTemplate.gameObject.SetActive(showPreview);
            var previewDetail = content.Find("PreviewDetailText"); if (previewDetail != null) previewDetail.gameObject.SetActive(showPreview);
            var previewTrack = content.Find("PreviewTrackButton"); if (previewTrack != null) previewTrack.gameObject.SetActive(showPreview);
            if (showPreview)
                for (int i = 0; i < content.childCount; i++)
                {
                    var child = content.GetChild(i) as RectTransform;
                    if (child != null && child.gameObject.activeSelf) y = Mathf.Max(y, -child.anchoredPosition.y + child.rect.height);
                }
            content.sizeDelta = new Vector2(0, y);
        }

        private static void ConfigureTimerIcons(RectTransform row, int days)
        {
            var container = row.Find("TimerIcons") as RectTransform;
            if (container == null) return;
            var iconTemplate = container.Find("timer") as RectTransform;
            if (iconTemplate == null) return;

            int count = Mathf.Max(0, days);
            iconTemplate.gameObject.SetActive(count > 0);
            float width = iconTemplate.rect.width;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                if (child != iconTemplate) Destroy(child.gameObject);
            }
            for (int i = 1; i < count; i++)
            {
                var icon = Instantiate(iconTemplate, container, false);
                icon.name = "timer_" + i;
                icon.gameObject.SetActive(true);
                icon.anchoredPosition = new Vector2(i * width, 0);
            }
        }

        private float AddDetailText(string text, float y, string name)
        {
            if (detailTextTemplate == null) return 0;
            var rect = Instantiate(detailTextTemplate, content, false); rect.gameObject.SetActive(true); rect.name = name;
            var label = rect.GetComponent<TextMeshProUGUI>();
            if (label != null) { label.text = text; label.color = MapVisualTheme.Ink; }
            float height = Mathf.Max(rect.sizeDelta.y, label != null ? label.GetPreferredValues(text, Width - 72, 0).y + 8 : 0);
            Top(rect, 12, -y, Width - 72, height); return height + 4;
        }

        private static void SetTab(Button button, bool selected)
        {
            button.GetComponent<Image>().color = selected ? new Color(.88f, .69f, .42f) : new Color(.96f, .90f, .71f);
            MapVisualTheme.Font(button.GetComponentInChildren<TextMeshProUGUI>(), true);
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Top(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static TextMeshProUGUI Label(string name, Transform parent, string text, float size)
        {
            TextMeshProUGUI label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Resources.Load<TMP_FontAsset>("Fonts/CactusClassicalSerif SDF") ?? TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = size;
            MapVisualTheme.Font(label);
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;
            return label;
        }

        private static Button Button(string name, Transform parent, string text, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Rect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            MapVisualTheme.Skin(image, MapVisualTheme.Current?.taskCard);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            TextMeshProUGUI label = Label("Label", rect, text, 17);
            MapVisualTheme.Font(label, true);
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(6, 2);
            label.rectTransform.offsetMax = new Vector2(-6, -2);
            button.onClick.AddListener(action);
            Spotlight.Audio.UIButtonAudio.Configure(button);
            return button;
        }
    }
}
