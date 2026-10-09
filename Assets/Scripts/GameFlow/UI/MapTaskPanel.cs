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
        public event Action<string> TrackingChanged;
        private const float Width = 340f;

        public void Initialize()
        {
            panel = (RectTransform)transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0, 1);
            panel.pivot = new Vector2(0, 1);
            panel.anchoredPosition = new Vector2(28, -28);
            GetComponent<Image>().color = new Color(.04f, .07f, .07f, .96f);
            summary = Label("TrackedSummary", panel, "", 17);
            Top(summary.rectTransform, 12, -8, Width - 68, 76);
            toggle = Button("TaskToggle", panel, "展开", () => { expanded = !expanded; Refresh(); });
            Top((RectTransform)toggle.transform, Width - 60, -8, 52, 36);
            body = Rect("TaskBody", panel);
            Top(body, 0, -52, Width, 370);
            activeTab = Button("InProgressTab", body, "进行中", () => SelectTab(false));
            Top((RectTransform)activeTab.transform, 8, -8, 158, 34);
            completedTab = Button("CompletedTab", body, "已完成", () => SelectTab(true));
            Top((RectTransform)completedTab.transform, 174, -8, 158, 34);

            RectTransform viewport = Rect("TaskViewport", body);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(8, 8);
            viewport.offsetMax = new Vector2(-8, -52);
            viewport.gameObject.AddComponent<Image>().color = new Color(.06f, .09f, .09f, 1);
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
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            scroll.content = content;
            Refresh();
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
            bool hasSummary = !expanded && tracked != null;
            panel.sizeDelta = new Vector2(Width, expanded ? 422 : hasSummary ? 92 : 52);
            body.gameObject.SetActive(expanded);
            summary.gameObject.SetActive(hasSummary);
            summary.text = tracked == null ? "" : tracked.name + "\n" +
                (tracked.completed ? "已完成" : string.IsNullOrEmpty(tracked.currentTask) ? "暂无当前阶段任务" : tracked.currentTask);
            summary.overflowMode = TextOverflowModes.Ellipsis;
            toggle.GetComponentInChildren<TextMeshProUGUI>().text = expanded ? "收起" : "展开";
            SetTab(activeTab, !showCompleted);
            SetTab(completedTab, showCompleted);
            // Deactivate old rows before deferred destruction so stale buttons cannot receive clicks.
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                GameObject child = content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            float y = 0;
            int count = 0;
            foreach (MapTaskItem item in items)
            {
                if (item.completed != showCompleted) continue;
                count++;
                Button title = Button("Task_" + item.id, content,
                    (expandedId == item.id ? "▾ " : "▸ ") + item.name + (trackedId == item.id ? "  · 追踪中" : ""),
                    () => { expandedId = expandedId == item.id ? null : item.id; Refresh(); });
                Top((RectTransform)title.transform, 0, -y, Width - 16, 40);
                y += 44;
                if (expandedId != item.id) continue;
                y += TextRow("Description", string.IsNullOrEmpty(item.description) ? "暂无事项描述" : item.description, y);
                if (item.previousTasks != null)
                    foreach (string task in item.previousTasks) y += TextRow("PreviousTask", "已进行：" + task, y);
                if (!item.completed)
                    y += TextRow("CurrentTask", "当前任务：" + (string.IsNullOrEmpty(item.currentTask) ? "暂无" : item.currentTask), y);
                else y += TextRow("CompletedStatus", "事项已完成", y);
                Button track = Button("Track_" + item.id, content, trackedId == item.id ? "取消追踪" : "追踪此事件",
                    () => SetTracked(trackedId == item.id ? null : item.id));
                Top((RectTransform)track.transform, 8, -y, Width - 32, 36);
                y += 44;
            }
            if (count == 0) y += TextRow("Empty", showCompleted ? "暂无已完成事项" : "暂无进行中事项", y);
            content.sizeDelta = new Vector2(0, y);
        }

        private float TextRow(string name, string text, float y)
        {
            TextMeshProUGUI label = Label(name, content, text, 16);
            float height = Mathf.Max(28, label.GetPreferredValues(text, Width - 40, 0).y + 12);
            Top(label.rectTransform, 12, -y, Width - 40, height);
            return height + 4;
        }

        private static void SetTab(Button button, bool selected)
        {
            button.GetComponent<Image>().color = selected ? new Color(.85f, .9f, .88f) : new Color(.18f, .22f, .23f);
            button.GetComponentInChildren<TextMeshProUGUI>().color = selected ? Color.black : Color.white;
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
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;
            return label;
        }

        private static Button Button(string name, Transform parent, string text, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Rect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.12f, .18f, .18f, 1);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            TextMeshProUGUI label = Label("Label", rect, text, 17);
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
