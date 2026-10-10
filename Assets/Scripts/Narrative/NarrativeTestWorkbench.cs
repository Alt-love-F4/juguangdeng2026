using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.Narrative
{
    public sealed class NarrativeTestWorkbench : MonoBehaviour
    {
        private NarrativeRuntime runtime;
        private readonly List<string> barKeys = new List<string>();
        private readonly List<string> eventIds = new List<string>();
        private Selector barDropdown, eventDropdown;
        private TMP_InputField amountInput;
        private TMP_Text status, eventDetails;
        private Transform rows;
        private bool initialized;

        private void Start()
        {
            runtime = FindFirstObjectByType<NarrativeRuntime>();
            if (runtime == null)
            {
                var go = new GameObject("NarrativeRuntime");
                runtime = go.AddComponent<NarrativeRuntime>();
            }
            if (!runtime.IsReady) runtime.Initialize(LubanNarrativeLoader.Load());
            Build();
            runtime.BarChanged += OnBarChanged;
            runtime.EventQueued += OnEventQueued;
            runtime.EventFinished += OnEventFinished;
            initialized = true;
            RefreshBars();
        }

        private void Build()
        {
            var canvasGo = new GameObject("NCS Test Workbench", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            Image basePanel = Panel(canvasGo.transform, "Base", new Vector2(.02f, .02f), new Vector2(.98f, .98f), new Color(.055f, .065f, .075f, .96f));
            Text(basePanel.transform, "Heading", "NCS 配置测试台", new Vector2(.03f, .87f), new Vector2(.97f, .98f), 32, TextAlignmentOptions.MidlineLeft);
            Text(basePanel.transform, "Subheading", "事件、演出、触发与结果均来自当前 Luban 配置", new Vector2(.03f, .82f), new Vector2(.97f, .88f), 18, TextAlignmentOptions.MidlineLeft);
            barDropdown = SelectorControl(basePanel.transform, "Bar", new Vector2(.04f, .74f), new Vector2(.39f, .80f));
            amountInput = NumberInput(basePanel.transform, "Amount", "数值", new Vector2(.41f, .74f), new Vector2(.54f, .80f));
            Button(basePanel.transform, "Add", "+ 增加", new Vector2(.56f, .74f), new Vector2(.68f, .80f), () => ChangeBar(true));
            Button(basePanel.transform, "Set", "设为", new Vector2(.70f, .74f), new Vector2(.82f, .80f), () => ChangeBar(false));
            Button(basePanel.transform, "Begin", "开始回合", new Vector2(.04f, .66f), new Vector2(.19f, .72f), () => { runtime.BeginTurn(); SetStatus("回合已开始；条值变化会在提交时判定触发"); });
            Button(basePanel.transform, "End", "提交回合", new Vector2(.21f, .66f), new Vector2(.36f, .72f), () => runtime.EndTurn());
            Button(basePanel.transform, "Trigger", "调值并触发", new Vector2(.38f, .66f), new Vector2(.55f, .72f), ChangeBarAndCommit);
            Button(basePanel.transform, "Demo", "演示阈值事件", new Vector2(.57f, .66f), new Vector2(.75f, .72f), PlayTriggerDemo);
            Button(basePanel.transform, "Reset", "重置会话", new Vector2(.77f, .66f), new Vector2(.95f, .72f), () => { runtime.ResetSession(); RebuildData(); SetStatus("会话已重置"); });
            eventDropdown = SelectorControl(basePanel.transform, "Event", new Vector2(.04f, .56f), new Vector2(.68f, .63f));
            Button(basePanel.transform, "Play", "播放配置事件", new Vector2(.70f, .56f), new Vector2(.90f, .63f), PlayEvent);
            status = Text(basePanel.transform, "Status", "", new Vector2(.04f, .50f), new Vector2(.94f, .54f), 15, TextAlignmentOptions.MidlineLeft);
            eventDetails = Text(basePanel.transform, "EventDetails", "", new Vector2(.04f, .43f), new Vector2(.94f, .49f), 18, TextAlignmentOptions.MidlineLeft);
            ScrollRect scroll = Scroll(basePanel.transform, out rows, new Vector2(.04f, .06f), new Vector2(.94f, .41f));
            scroll.vertical = true;
            RebuildData();
        }

        private void RebuildData()
        {
            if (runtime?.Controller == null) return;
            barKeys.Clear(); barKeys.AddRange(runtime.Controller.State.Bars.Keys.OrderBy(x => x == "npc_a.trust_b" ? 0 : 1).ThenBy(x => x, StringComparer.Ordinal));
            eventIds.Clear();
            var config = runtime.Configuration;
            eventIds.AddRange(config.events.OrderBy(x => x.eventId.StartsWith("EV_TEST_", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(x => x.eventId, StringComparer.Ordinal).Select(x => x.eventId));
            Fill(barDropdown, barKeys); Fill(eventDropdown, eventIds);
            RefreshEventDetails(config);
            RefreshBars();
        }

        private void ChangeBar(bool add)
        {
            if (!int.TryParse(amountInput.text, out int amount)) { SetStatus("请输入整数数值"); return; }
            string key = barDropdown.Selected;
            bool changed = add ? runtime.AddBar(key, amount) : runtime.SetBar(key, amount);
            SetStatus(changed ? $"{key} 已更新" : $"无法修改 {key}；检查范围、回合和演出状态");
            RefreshBars();
        }

        private void ChangeBarAndCommit()
        {
            if (!int.TryParse(amountInput.text, out int amount)) { SetStatus("请输入整数数值"); return; }
            string key = barDropdown.Selected;
            runtime.BeginTurn();
            if (!runtime.AddBar(key, amount)) { SetStatus($"无法修改 {key}；检查范围或当前状态"); return; }
            runtime.EndTurn();
            SetStatus($"已提交 {key} +{amount}，正在按触发器判定事件");
            RefreshBars();
        }

        private void PlayTriggerDemo()
        {
            runtime.ResetSession();
            runtime.BeginTurn();
            runtime.SetBar("npc_a.trust_b", 3);
            runtime.AddBar("npc_a.trust_b", 1);
            runtime.EndTurn();
            SetStatus("样例触发：npc_a.trust_b 穿过 4，等待 EV_STANDOFF 演出完成");
            RefreshBars();
        }

        private void PlayEvent()
        {
            string id = eventDropdown.Selected;
            if (runtime.IsPresenting || runtime.Controller.IsBusy)
            {
                SetStatus("请先完成或关闭当前演出，再播放下一次");
                return;
            }
            // Manual preview uses a fresh, isolated configuration; production rules remain intact.
            var preview = LubanNarrativeLoader.Load();
            preview.conditions.RemoveAll(condition => condition.ownerType == "event" && condition.ownerId == id);
            preview.events.Find(item => item.eventId == id).onceOnly = false;
            runtime.ResetSession(preview);
            SetStatus(runtime.QueueEvent(id) ? $"已排队：{id}" : $"无法播放：{id}，请检查事件配置");
        }

        private void RefreshEventDetails(NarrativeConfiguration config = null)
        {
            if (eventDetails == null) return;
            config ??= runtime?.Configuration ?? LubanNarrativeLoader.Load();
            NarrativeEventDefinition definition = config.events.FirstOrDefault(item => item.eventId == eventDropdown?.Selected);
            if (definition == null) { eventDetails.text = ""; return; }
            NarrativeSetupParamsDefinition setup = config.setups.FirstOrDefault(item => item.setupKey == definition.setupKey);
            string template = setup?.tplName ?? definition.setupType ?? definition.presentationClass ?? "(未指定)";
            string presentationClass = definition.presentationClass;
            NarrativePresentationIndexDefinition index = config.presentations.index.FirstOrDefault(x => x.presentationKey == definition.presentationKey);
            if (index != null) presentationClass = index.presentationClass;
            eventDetails.text = $"{definition.eventId}  [{presentationClass ?? template}]  key={definition.presentationKey ?? "-"}\\n{definition.titleText}";
        }

        private void RefreshBars()
        {
            if (rows == null || runtime?.Controller == null) return;
            for (int i = rows.childCount - 1; i >= 0; i--) Destroy(rows.GetChild(i).gameObject);
            foreach (string key in barKeys)
            {
                runtime.Controller.State.TryGetBar(key, out int value);
                NarrativeBarDefinition definition = runtime.Controller.State.GetDefinition(key);
                Row(rows, key, $"{value}   [{definition.min}..{definition.max}]  {definition.advanceMode}");
            }
        }

        private void OnBarChanged(string key, int oldValue, int newValue) { if (initialized) RefreshBars(); }
        private void OnEventQueued(NarrativeEventRequest request) => SetStatus($"事件入队：{request.Event.eventId} / {request.Event.logTag}");
        private void OnEventFinished(NarrativeEventRequest request) => SetStatus($"事件完成：{request.Event.eventId}");
        private void SetStatus(string message) { if (status != null) status.text = message; }
        private static void Fill(Selector selector, List<string> values) => selector?.SetOptions(values);

        private static Image Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            RectTransform r = go.GetComponent<RectTransform>(); r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>(); image.color = color; return image;
        }
        private static TMP_Text Text(Transform parent, string name, string value, Vector2 min, Vector2 max, float size, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            RectTransform r = go.GetComponent<RectTransform>(); r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero;
            var text = go.GetComponent<TMP_Text>(); text.font = Resources.Load<TMP_FontAsset>("Fonts/乐米曲奇方块体 SDF"); text.text = value; text.fontSize = size; text.color = Color.white; text.alignment = alignment; text.raycastTarget = false; return text;
        }
        private static Button Button(Transform parent, string name, string label, Vector2 min, Vector2 max, Action action)
        {
            Image image = Panel(parent, name, min, max, new Color(.13f, .19f, .22f, 1)); Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors; colors.highlightedColor = new Color(.2f, .38f, .4f); colors.pressedColor = new Color(.08f, .32f, .34f); button.colors = colors;
            Text(image.transform, "Label", label, Vector2.zero, Vector2.one, 18); button.onClick.AddListener(() => action?.Invoke()); return button;
        }
        private static TMP_InputField NumberInput(Transform parent, string name, string placeholder, Vector2 min, Vector2 max)
        {
            Image image = Panel(parent, name, min, max, new Color(.12f, .13f, .14f, 1));
            RectTransform textArea = Panel(image.transform, "Text Area", new Vector2(.04f, .04f), new Vector2(.96f, .96f), Color.clear).rectTransform;
            textArea.gameObject.AddComponent<RectMask2D>();
            TMP_Text text = Text(textArea, "Value", "", Vector2.zero, Vector2.one, 18, TextAlignmentOptions.MidlineLeft);
            TMP_Text placeholderText = Text(textArea, "Placeholder", placeholder, Vector2.zero, Vector2.one, 17, TextAlignmentOptions.MidlineLeft);
            placeholderText.color = new Color(1, 1, 1, .45f);
            TMP_InputField input = image.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = image; input.textViewport = textArea; input.textComponent = text;
            input.placeholder = placeholderText;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterValidation = TMP_InputField.CharacterValidation.Integer;
            input.interactable = true;
            input.readOnly = false;
            input.SetTextWithoutNotify("1");
            return input;
        }
        private Selector SelectorControl(Transform parent, string name, Vector2 min, Vector2 max)
        {
            Image image = Panel(parent, name, min, max, new Color(.12f, .13f, .14f, 1));
            TMP_Text caption = Text(image.transform, "Label", "", new Vector2(.04f, .04f), new Vector2(.88f, .96f), 17, TextAlignmentOptions.MidlineLeft);
            Text(image.transform, "Arrow", "▼", new Vector2(.9f, .04f), new Vector2(.98f, .96f), 14);
            Selector selector = image.gameObject.AddComponent<Selector>();
            selector.Initialize(image, caption, CreateSelectorOptions, OnSelectorChanged);
            return selector;
        }

        private void OnSelectorChanged(Selector selector)
        {
            if (selector == eventDropdown) SetStatus("已选择事件：" + selector.Selected);
        }

        private List<Button> CreateSelectorOptions(Transform parent, IReadOnlyList<string> values)
        {
            var result = new List<Button>(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                int index = i;
                Image option = Panel(parent, "Option_" + index, Vector2.zero, Vector2.one, new Color(.09f, .1f, .11f, .99f));
                option.gameObject.AddComponent<LayoutElement>().preferredHeight = 38;
                Button button = option.gameObject.AddComponent<Button>(); button.targetGraphic = option;
                ColorBlock colors = button.colors; colors.highlightedColor = new Color(.2f, .38f, .4f); colors.pressedColor = new Color(.08f, .32f, .34f); button.colors = colors;
                Text(option.transform, "Label", values[i], new Vector2(.04f, 0), new Vector2(.96f, 1), 16, TextAlignmentOptions.MidlineLeft);
                result.Add(button);
            }
            return result;
        }
        private sealed class Selector : MonoBehaviour
        {
            private Image background;
            private TMP_Text caption;
            private Func<Transform, IReadOnlyList<string>, List<Button>> buildOptions;
            private Action<Selector> changed;
            private readonly List<Button> optionButtons = new List<Button>();
            private readonly List<string> values = new List<string>();
            private GameObject popup;
            private int selectedIndex;
            public string Selected => selectedIndex >= 0 && selectedIndex < values.Count ? values[selectedIndex] : string.Empty;

            public void Initialize(Image image, TMP_Text label, Func<Transform, IReadOnlyList<string>, List<Button>> optionBuilder, Action<Selector> onChanged)
            {
                background = image; caption = label; buildOptions = optionBuilder; changed = onChanged;
                Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(TogglePopup);
                ColorBlock colors = button.colors; colors.highlightedColor = new Color(.2f, .3f, .32f); button.colors = colors;
            }

            public void SetOptions(IReadOnlyList<string> options)
            {
                values.Clear(); if (options != null) values.AddRange(options);
                foreach (Button item in optionButtons) if (item != null) Destroy(item.gameObject);
                optionButtons.Clear();
                if (popup != null) { Destroy(popup); popup = null; }
                if (values.Count == 0) { selectedIndex = -1; caption.text = string.Empty; return; }
                selectedIndex = Mathf.Clamp(selectedIndex, 0, values.Count - 1);
                caption.text = values[selectedIndex];
            }

            private void TogglePopup()
            {
                if (popup != null) { popup.SetActive(!popup.activeSelf); return; }
                popup = new GameObject("Options", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
                popup.transform.SetParent(transform.parent, false);
                RectTransform rect = popup.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(0, 1);
                rect.sizeDelta = new Vector2(background.rectTransform.rect.width, Mathf.Min(420, values.Count * 42 + 8));
                RectTransform own = background.rectTransform; Vector3[] corners = new Vector3[4]; own.GetWorldCorners(corners);
                rect.position = corners[1] + Vector3.down * 6f;
                popup.GetComponent<Image>().color = new Color(.045f, .055f, .06f, .99f);
                ScrollRect scroll = popup.GetComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
                GameObject viewport = new GameObject("Viewport", typeof(RectTransform)); viewport.transform.SetParent(popup.transform, false);
                RectTransform vp = viewport.GetComponent<RectTransform>(); vp.anchorMin = Vector2.zero; vp.anchorMax = Vector2.one; vp.offsetMin = new Vector2(4, 4); vp.offsetMax = new Vector2(-4, -4); scroll.viewport = vp;
                GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)); content.transform.SetParent(viewport.transform, false);
                RectTransform cr = content.GetComponent<RectTransform>(); cr.anchorMin = new Vector2(0, 1); cr.anchorMax = Vector2.one; cr.pivot = new Vector2(.5f, 1); cr.sizeDelta = Vector2.zero;
                VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>(); layout.spacing = 2; layout.childControlHeight = true; layout.childForceExpandHeight = false;
                content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; scroll.content = cr;
                optionButtons.AddRange(buildOptions(content.transform, values));
                for (int i = 0; i < optionButtons.Count; i++)
                {
                    int index = i; optionButtons[i].onClick.AddListener(() => Select(index));
                }
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 1;
                ClampPopupToCanvas(rect);
            }

            private void ClampPopupToCanvas(RectTransform popupRect)
            {
                RectTransform canvasRect = popupRect.GetComponentInParent<Canvas>()?.transform as RectTransform;
                if (canvasRect == null) return;
                Vector3[] popupCorners = new Vector3[4]; popupRect.GetWorldCorners(popupCorners);
                Vector3[] canvasCorners = new Vector3[4]; canvasRect.GetWorldCorners(canvasCorners);
                Vector3 shift = Vector3.zero;
                if (popupCorners[0].x < canvasCorners[0].x) shift.x = canvasCorners[0].x - popupCorners[0].x + 4f;
                if (popupCorners[2].x > canvasCorners[2].x) shift.x = canvasCorners[2].x - popupCorners[2].x - 4f;
                if (popupCorners[0].y < canvasCorners[0].y) shift.y = canvasCorners[0].y - popupCorners[0].y + 4f;
                if (popupCorners[1].y > canvasCorners[1].y) shift.y = canvasCorners[1].y - popupCorners[1].y - 4f;
                popupRect.position += shift;
            }

            private void Select(int index)
            {
                selectedIndex = Mathf.Clamp(index, 0, values.Count - 1); caption.text = values[selectedIndex];
                if (popup != null) { Destroy(popup); popup = null; }
                optionButtons.Clear(); changed?.Invoke(this);
            }

            private void OnDisable() { if (popup != null) popup.SetActive(false); }
            private void OnDestroy() { if (popup != null) Destroy(popup); }
        }

        private static ScrollRect Scroll(Transform parent, out Transform content, Vector2 min, Vector2 max)
        {
            Image viewport = Panel(parent, "BarViewport", min, max, new Color(.035f, .04f, .045f, .95f)); viewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            var body = new GameObject("BarRows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)); body.transform.SetParent(viewport.transform, false);
            RectTransform rect = body.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = body.GetComponent<VerticalLayoutGroup>(); layout.spacing = 4; layout.padding = new RectOffset(8, 8, 8, 8); layout.childControlHeight = true; layout.childForceExpandHeight = false;
            body.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; scroll.viewport = viewport.rectTransform; scroll.content = rect; content = body.transform; return scroll;
        }
        private static void Row(Transform parent, string key, string value)
        {
            Image row = Panel(parent, key, Vector2.zero, Vector2.one, new Color(.09f, .105f, .115f, 1)); row.gameObject.AddComponent<LayoutElement>().preferredHeight = 42;
            Text(row.transform, "Key", key, new Vector2(.02f, 0), new Vector2(.48f, 1), 15, TextAlignmentOptions.MidlineLeft);
            Text(row.transform, "Value", value, new Vector2(.5f, 0), new Vector2(.98f, 1), 15, TextAlignmentOptions.MidlineRight);
        }
        private void OnDestroy()
        {
            if (runtime == null) return;
            runtime.BarChanged -= OnBarChanged; runtime.EventQueued -= OnEventQueued; runtime.EventFinished -= OnEventFinished;
        }
    }
}
