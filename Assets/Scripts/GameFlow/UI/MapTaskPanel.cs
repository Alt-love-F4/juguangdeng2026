using System;
using System.Collections.Generic;
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
        public string name;
        public string description;
        public bool completed;
            public string currentTask;

        public int remainingDays;
        public string location;
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
        private bool showCompleted;
        private string expandedId, trackedId;
        public string TrackedId => trackedId;
        public event Action<string> TrackingChanged;
        private const float Width = 550f;
        private MapSidebarView sidebar;
        public bool IsVisible => sidebar != null && sidebar.IsVisible;

        public void Initialize()
        {
            panel = (RectTransform)transform;
            sidebar = GetComponent<MapSidebarView>();
            if (sidebar == null) sidebar = gameObject.AddComponent<MapSidebarView>();
            sidebar.Initialize();
            if (transform.Find("TaskBody/TaskViewport/TaskContent") is RectTransform existingContent)
            {
                content = existingContent;
                body = (RectTransform)transform.Find("TaskBody");
                summary = transform.Find("TrackedSummary").GetComponent<TextMeshProUGUI>();
                toggle = transform.Find("TaskToggle").GetComponent<UnityEngine.UI.Button>();
                activeTab = body.Find("InProgressTab").GetComponent<UnityEngine.UI.Button>();
                completedTab = body.Find("CompletedTab").GetComponent<UnityEngine.UI.Button>();
                scroll = body.Find("TaskViewport").GetComponent<ScrollRect>();
                ConfigureLayout();
                toggle.onClick.RemoveAllListeners(); toggle.onClick.AddListener(Toggle);
                activeTab.onClick.RemoveAllListeners(); activeTab.onClick.AddListener(() => SelectTab(false));
                completedTab.onClick.RemoveAllListeners(); completedTab.onClick.AddListener(() => SelectTab(true));
                Refresh();
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
            ConfigureLayout(); Refresh();
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
            // Initialize and content changes already build the rows; visibility only slides the sidebar.
            sidebar.SetVisible(value);
        }

        public void SetItems(IEnumerable<MapTaskItem> values)
        {
            items.Clear();
            var ids = new HashSet<string>();
            if (values != null)
                foreach (MapTaskItem item in values)
                    if (item != null && !string.IsNullOrEmpty(item.id) && ids.Add(item.id)) items.Add(item);
            if (items.Find(item => item.id == expandedId) == null) expandedId = null;
            if (items.Find(item => item.id == trackedId) == null) SetTracked(null);
            else Refresh();
        }

        public void SetTracked(string id)
        {
            string next = items.Exists(item => item.id == id) ? id : null;
            bool changed = trackedId != next;
            trackedId = next;
            Refresh();
            if (changed) TrackingChanged?.Invoke(trackedId);
        }

        private void SelectTab(bool completed)
        {
            showCompleted = completed;
            expandedId = null;
            Refresh();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
        }

        private void Refresh()
        {
            if (panel == null) return;
            MapTaskItem tracked = items.Find(item => item.id == trackedId);
            body.gameObject.SetActive(true);
            summary.gameObject.SetActive(false);
            summary.text = tracked == null ? "" : tracked.name + "\n" +
                (tracked.completed ? "已完成" : string.IsNullOrEmpty(tracked.currentTask) ? "暂无当前阶段任务" : tracked.currentTask);
            summary.overflowMode = TextOverflowModes.Ellipsis;
            toggle.GetComponentInChildren<TextMeshProUGUI>().text = "×";
            SetTab(activeTab, !showCompleted);
            SetTab(completedTab, showCompleted);
            // Deactivate old rows before deferred destruction so stale buttons cannot receive clicks.
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                GameObject child = content.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            float y = 0;
            int count = 0;
            foreach (MapTaskItem item in items)
            {
                if (item.completed != showCompleted) continue;
                count++;
                Button title = Button("Task_" + item.id, content,
                    item.name + (trackedId == item.id ? "  · 追踪中" : ""),
                    () => { expandedId = expandedId == item.id ? null : item.id; Refresh(); });
                float rowWidth = Width - 64;
                var heading = title.GetComponentInChildren<TextMeshProUGUI>();
                heading.fontSize = 28; heading.alignment = TextAlignmentOptions.Left;
                Top(heading.rectTransform, 22, -14, rowWidth - 44, 40);
                var description = Label("Description", title.transform, string.IsNullOrEmpty(item.description) ? "暂无事项描述" : item.description, 21);
                float descriptionHeight = Mathf.Max(54, description.GetPreferredValues(description.text, rowWidth - 44, 0).y + 6);
                Top(description.rectTransform, 22, -62, rowWidth - 44, descriptionHeight);
                var location = Label("Location", title.transform, string.IsNullOrEmpty(item.location) ? "" : "地点：" + item.location, 20);
                location.color = MapVisualTheme.Accent;
                location.alignment = TextAlignmentOptions.Right;
                Top(location.rectTransform, 22, -70 - descriptionHeight, rowWidth - 44, 26);
                if (item.remainingDays > 0)
                {
                    var timer = MapVisualTheme.Art("Deadline", title.transform, MapVisualTheme.Current?.timer, Vector2.zero, new Vector2(22, 22));
                    Top(timer.rectTransform, rowWidth - 62, -23, 22, 22);
                    var remaining = Label("RemainingDays", title.transform, item.remainingDays.ToString(), 18);
                    Top(remaining.rectTransform, rowWidth - 38, -22, 22, 24);
                }
                float height = 108 + descriptionHeight;
                Top((RectTransform)title.transform, 0, -y, rowWidth, height);
                y += height + 14;
                if (expandedId != item.id) continue;
                if (item.previousTasks != null)
                    foreach (string task in item.previousTasks) y += TextRow("PreviousTask", "已进行：" + task, y);
                if (!item.completed)
                    y += TextRow("CurrentTask", "当前任务：" + (string.IsNullOrEmpty(item.currentTask) ? "暂无" : item.currentTask), y);
                else y += TextRow("CompletedStatus", "事项已完成", y);
                Button track = Button("Track_" + item.id, content, trackedId == item.id ? "取消追踪" : "追踪此事件",
                    () => SetTracked(trackedId == item.id ? null : item.id));
                Top((RectTransform)track.transform, 8, -y, Width - 64, 36);
                y += 44;
            }
            if (count == 0) y += TextRow("Empty", showCompleted ? "暂无已完成事项" : "暂无进行中事项", y);
            content.sizeDelta = new Vector2(0, y);
        }

        private float TextRow(string name, string text, float y)
        {
            TextMeshProUGUI label = Label(name, content, text, 16);
            float height = Mathf.Max(28, label.GetPreferredValues(text, Width - 72, 0).y + 12);
            Top(label.rectTransform, 12, -y, Width - 72, height);
            return height + 4;
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
            return button;
        }
    }
}
