using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// Scene-owned picker for isolated configured presentation previews. Requires the scene's EventSystem.
    /// Constructing the helper does not create UI; only Show builds the overlay.
    /// </summary>
    public sealed class PresentationDebugDialogueMenu
    {
        private struct Entry
        {
            public string Id;
            public string Label;
            public string Prefix;
            public GameObject Root;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<string> prefixes = new List<string>();
        private int typeIndex;
        private string searchQuery = string.Empty;
        private GameObject canvasRoot;
        private TMP_FontAsset font;
        private TMP_Text emptyMessage;
        private TMP_Text resultCount;
        private ScrollRect scroll;
        private Action<string> selected;
        private Action closed;

        public bool IsOpen => canvasRoot != null && canvasRoot.activeSelf;

        /// <summary>
        /// Replaces any previous menu without notifying onClosed. Labels may be omitted or shorter
        /// than ids; missing labels fall back to their id. Null/empty ids are ignored.
        /// Selecting an entry closes the menu before calling onSelected with its original id.
        /// Only the close button invokes onClosed; the controller owns stopping the preview.
        /// </summary>
        public void Show(Transform owner, TMP_FontAsset font, IReadOnlyList<string> ids,
            IReadOnlyList<string> labels, Action<string> onSelected, Action onClosed,
            string titleText = null, string hintText = null)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            Close();
            this.font = font != null ? font : TMP_Settings.defaultFontAsset;
            selected = onSelected;
            closed = onClosed;

            canvasRoot = new GameObject("PresentationDebugDialogueMenu", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            // Move the root explicitly, without ever changing the active scene. All children are
            // parented beneath this root and therefore belong to the owner's scene as well.
            SceneManager.MoveGameObjectToScene(canvasRoot, owner.gameObject.scene);
            canvasRoot.transform.SetParent(owner, false);
            Place((RectTransform)canvasRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1100;
            CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;

            Image backdrop = Box(canvasRoot.transform, "Backdrop", Vector2.zero, Vector2.one,
                new Color(0f, 0f, 0f, .84f));
            Image panel = Box(backdrop.transform, "Panel", new Vector2(.15f, .12f),
                new Vector2(.85f, .88f), new Color(.10f, .12f, .16f, 1f));

            TMP_Text title = Text(panel.transform, "Title", string.IsNullOrWhiteSpace(titleText) ? "连续演出 · 选择播放入口" : titleText, 32);
            Place(title.rectTransform, new Vector2(0, 1), Vector2.one,
                new Vector2(30, -72), new Vector2(-150, -16));
            TMP_Text hint = Text(panel.transform, "Hint", string.IsNullOrWhiteSpace(hintText)
                ? "优先选择入口候选 · 按选项/检定连续播放 · 仅影响临时预览状态" : hintText, 24);
            hint.color = new Color(.75f, .80f, .86f, 1f);
            Place(hint.rectTransform, new Vector2(0, 1), Vector2.one,
                new Vector2(30, -115), new Vector2(-30, -72));

            Button close = Button(panel.transform, "CloseButton", "关闭");
            Place((RectTransform)close.transform, Vector2.one, Vector2.one,
                new Vector2(-130, -68), new Vector2(-22, -20));
            close.onClick.AddListener(CloseFromButton);

            TMP_InputField search = BuildSearch(panel.transform);
            RectTransform content = BuildScroll(panel.transform);
            if (ids != null)
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    string id = ids[i]; // Capture the value, never the loop index.
                    if (string.IsNullOrEmpty(id)) continue;
                    string label = labels != null && i < labels.Count ? labels[i] : null;
                    if (string.IsNullOrEmpty(label)) label = id;
                    Button option = Button(content, "DialogueOption_" + i, null);
                    LayoutElement layout = option.gameObject.AddComponent<LayoutElement>();
                    layout.minHeight = 78;
                    layout.preferredHeight = 78;
                    TMP_Text nameText = Text(option.transform, "Label", label, 26);
                    Place(nameText.rectTransform, Vector2.zero, Vector2.one,
                        new Vector2(18, 34), new Vector2(-18, -4));
                    TMP_Text idText = Text(option.transform, "Id", id, 22);
                    idText.color = new Color(.67f, .77f, .85f, 1f);
                    Place(idText.rectTransform, Vector2.zero, Vector2.one,
                        new Vector2(18, 4), new Vector2(-18, -40));
                    option.onClick.AddListener(() => Select(id));
                    entries.Add(new Entry { Id = id, Label = label, Prefix = PrefixOf(id), Root = option.gameObject });
                }
            }

            emptyMessage = Text(panel.transform, "EmptyMessage", "", 26);
            emptyMessage.alignment = TextAlignmentOptions.Center;
            Place(emptyMessage.rectTransform, new Vector2(0, .35f), new Vector2(1, .55f),
                new Vector2(40, 0), new Vector2(-40, 0));
            resultCount = Text(panel.transform, "ResultCount", "", 22);
            resultCount.color = new Color(.67f, .77f, .85f, 1f);
            Place(resultCount.rectTransform, Vector2.zero, new Vector2(1, 0),
                new Vector2(30, 8), new Vector2(-30, 42));
            BuildTypeFilter(panel.transform);
            search.onValueChanged.AddListener(Filter);
            Filter(string.Empty);
        }

        /// <summary>Silently destroys the runtime canvas, including all listeners and UI children.</summary>
        public void Close()
        {
            GameObject previous = canvasRoot;
            canvasRoot = null;
            selected = null;
            closed = null;
            font = null;
            emptyMessage = null;
            resultCount = null;
            scroll = null;
            entries.Clear();
            prefixes.Clear();
            typeIndex = 0;
            searchQuery = string.Empty;
            if (previous == null) return;
            // Destroy is deferred in Play Mode: disable now so replacing a menu cannot leave an
            // old raycast blocker or clickable option alive for the remainder of this frame.
            previous.SetActive(false);
            UnityEngine.Object.Destroy(previous);
        }

        private void Select(string id)
        {
            if (!IsOpen) return;
            Action<string> callback = selected;
            Close();
            callback?.Invoke(id);
        }

        private void CloseFromButton()
        {
            if (!IsOpen) return;
            Action callback = closed;
            Close();
            callback?.Invoke();
        }

        private void Filter(string query)
        {
            if (!IsOpen) return;
            searchQuery = query ?? string.Empty;
            string term = searchQuery.Trim();
            int count = 0, typeCount = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                bool sameType = typeIndex == 0 || string.Equals(entry.Prefix, prefixes[typeIndex - 1], StringComparison.OrdinalIgnoreCase);
                if (sameType) typeCount++;
                bool matches = sameType && (term.Length == 0 ||
                    entry.Id.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    entry.Label.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
                if (entry.Root.activeSelf != matches) entry.Root.SetActive(matches);
                if (matches) count++;
            }
            emptyMessage.text = entries.Count == 0 ? "暂无可播放的配表演出" : "当前类型与搜索下没有匹配的演出";
            emptyMessage.gameObject.SetActive(count == 0);
            resultCount.text = "显示 " + count + " / 当前类型 " + typeCount + " / 全部 " + entries.Count + " 条演出";
            // Filtering is event-driven, not an Update rebuild. Resolve layout before resetting
            // scroll position so narrowing an already-scrolled list does not leave a blank view.
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }

        private TMP_InputField BuildSearch(Transform parent)
        {
            Image background = Box(parent, "SearchInput", Vector2.zero, Vector2.one,
                new Color(.17f, .20f, .25f, 1f));
            Place(background.rectTransform, new Vector2(0, 1), Vector2.one,
                new Vector2(370, -177), new Vector2(-30, -123));
            RectTransform viewport = Rect(background.transform, "TextArea");
            Place(viewport, Vector2.zero, Vector2.one, new Vector2(14, 5), new Vector2(-14, -5));
            viewport.gameObject.AddComponent<RectMask2D>();
            TMP_Text value = Text(viewport, "Text", "", 24);
            value.overflowMode = TextOverflowModes.Overflow;
            TMP_Text placeholder = Text(viewport, "Placeholder", "搜索 id 或名称…", 24);
            placeholder.color = new Color(.65f, .70f, .77f, 1f);
            TMP_InputField input = background.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.textViewport = viewport;
            input.textComponent = value;
            input.placeholder = placeholder;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;
            input.customCaretColor = true;
            input.caretColor = Color.white;
            input.SetTextWithoutNotify(string.Empty);
            return input;
        }

        private static string PrefixOf(string id)
        {
            int colon = id.IndexOf(':');
            return colon > 0 ? id.Substring(0, colon) : string.Empty;
        }

        private static string TypeLabel(string prefix)
        {
            if (string.Equals(prefix, "sequence", StringComparison.OrdinalIgnoreCase)) return "连续演出 / sequence";
            if (prefix.Length == 0) return "无前缀";
            if (string.Equals(prefix, "opening", StringComparison.OrdinalIgnoreCase)) return "开场 / opening";
            if (string.Equals(prefix, "conversation", StringComparison.OrdinalIgnoreCase)) return "对话 / conversation";
            return prefix;
        }

        private void BuildTypeFilter(Transform parent)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Entry entry in entries)
            {
                if (counts.TryGetValue(entry.Prefix, out int count)) counts[entry.Prefix] = count + 1;
                else { counts.Add(entry.Prefix, 1); prefixes.Add(entry.Prefix); }
            }
            prefixes.Sort(StringComparer.OrdinalIgnoreCase);
            Image background = Box(parent, "TypeFilter", Vector2.zero, Vector2.one, new Color(.20f, .25f, .32f, 1f));
            Place(background.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(30, -177), new Vector2(350, -123));
            TMP_Text caption = Text(background.transform, "Caption", "", 23);
            Place(caption.rectTransform, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-34, 0));
            TMP_Text arrow = Text(background.transform, "Arrow", "▼", 20);
            Place(arrow.rectTransform, new Vector2(1, 0), Vector2.one, new Vector2(-30, 0), new Vector2(-6, 0));
            TMP_Dropdown dropdown = background.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = background;
            dropdown.captionText = caption;

            Image template = Box(background.transform, "Template", Vector2.zero, new Vector2(1, 0), new Color(.12f, .15f, .20f, 1));
            template.rectTransform.pivot = new Vector2(.5f, 1);
            template.rectTransform.offsetMin = new Vector2(0, -286);
            template.rectTransform.offsetMax = new Vector2(0, -6);
            ScrollRect typeScroll = template.gameObject.AddComponent<ScrollRect>();
            Image viewport = Box(template.transform, "Viewport", Vector2.zero, Vector2.one, Color.clear);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect(viewport.transform, "Content");
            Place(content, new Vector2(0, 1), Vector2.one, new Vector2(0, -48), Vector2.zero);
            content.pivot = new Vector2(.5f, 1);
            Image item = Box(content, "Item", Vector2.zero, Vector2.one, new Color(.23f, .29f, .37f, 1));
            TMP_Text check = Text(item.transform, "Checkmark", "✓", 22);
            Place(check.rectTransform, Vector2.zero, new Vector2(0, 1), new Vector2(8, 0), new Vector2(34, 0));
            TMP_Text itemText = Text(item.transform, "Label", "", 22);
            Place(itemText.rectTransform, Vector2.zero, Vector2.one, new Vector2(36, 0), new Vector2(-8, 0));
            Toggle toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = item;
            toggle.graphic = check;
            typeScroll.viewport = viewport.rectTransform;
            typeScroll.content = content;
            typeScroll.horizontal = false;
            typeScroll.movementType = ScrollRect.MovementType.Clamped;
            typeScroll.scrollSensitivity = 40;
            dropdown.template = template.rectTransform;
            dropdown.itemText = itemText;
            dropdown.ClearOptions();
            var options = new List<string> { "全部类型 (" + entries.Count + ")" };
            foreach (string prefix in prefixes) options.Add(TypeLabel(prefix) + " (" + counts[prefix] + ")");
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(0);
            dropdown.RefreshShownValue();
            template.gameObject.SetActive(false);
            dropdown.onValueChanged.AddListener(index => { typeIndex = index; Filter(searchQuery); });
        }

        private RectTransform BuildScroll(Transform parent)
        {
            Image area = Box(parent, "DialogueScroll", Vector2.zero, Vector2.one,
                new Color(.06f, .08f, .11f, 1f));
            Place(area.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(30, 48), new Vector2(-30, -193));
            scroll = area.gameObject.AddComponent<ScrollRect>();
            Image viewport = Box(area.transform, "Viewport", Vector2.zero, Vector2.one, Color.clear);
            viewport.rectTransform.offsetMax = new Vector2(-24, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect(viewport.transform, "Content");
            Place(content, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(.5f, 1);
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;

            Image track = Box(area.transform, "Scrollbar", new Vector2(1, 0), Vector2.one,
                new Color(.12f, .15f, .19f, 1f));
            track.rectTransform.offsetMin = new Vector2(-20, 4);
            track.rectTransform.offsetMax = new Vector2(-4, -4);
            Image handle = Box(track.transform, "Handle", Vector2.zero, Vector2.one,
                new Color(.45f, .53f, .63f, 1f));
            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.targetGraphic = handle;
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            return content;
        }

        private Button Button(Transform parent, string name, string caption)
        {
            Image image = Box(parent, name, Vector2.zero, Vector2.one, new Color(.20f, .25f, .32f, 1f));
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(.80f, .90f, 1f, 1f);
            colors.pressedColor = new Color(.50f, .65f, .80f, 1f);
            button.colors = colors;
            if (caption != null)
                Text(button.transform, "Label", caption, 24).alignment = TextAlignmentOptions.Center;
            return button;
        }

        private TMP_Text Text(Transform parent, string name, string value, float size)
        {
            TMP_Text text = Rect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.richText = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static Image Box(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            RectTransform rect = Rect(parent, name);
            Place(rect, min, max, Vector2.zero, Vector2.zero);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static RectTransform Rect(Transform parent, string name)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            Place(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
