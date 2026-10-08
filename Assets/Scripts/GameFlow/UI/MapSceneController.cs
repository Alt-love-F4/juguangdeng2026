using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    /// <summary>Scene-owned city map shell. Art and positions are intentionally data-driven later.</summary>
    [DefaultExecutionOrder(-500)]
    public sealed class MapSceneController : GameFlowPanel
    {
        private const float MapWidth = 2400f;
        private const float MapHeight = 1350f;
        private const float PsdScale = 2.5f;

        [Header("PSD map layers")]
        [SerializeField] private Sprite mapBackgroundSprite;
        [SerializeField] private Sprite locationIconsSprite;
        [SerializeField] private Sprite locationSignsSprite;
        [SerializeField] private Sprite locationNamesSprite;
        [SerializeField] private Sprite pinSprite;
        [SerializeField] private Sprite cityTitleSprite;
        [SerializeField] private Sprite compassSprite;
        private RectTransform mapRoot;
        private RectTransform mapContent;
        // The player moves the viewport focus in map coordinates. The map graphic is
        // positioned from this focus so its edges never leave the viewport.
        private Vector2 mapCenter;
        private float zoom = .8f;
        private bool dragging;
        private Vector2 dragStart;
        private Vector2 dragOrigin;
        private GameFlowBootstrap flow;
        private TextMeshProUGUI status;
        private GameObject focusPanel;
        private RectTransform actionList;
        private NarrativeConfiguration narrativeConfiguration;
        private NarrativeMapPositionDefinition focusedPosition;

        protected override void Awake()
        {
            base.Awake();
            GameplayPanelHooks.OpenPanel = OpenPanel;
            GameplayPanelHooks.PanelType = typeof(MapSceneController);
            GameplayPanelHooks.IsScenePanel = true;
            BuildUi();
        }

        private void OnDestroy()
        {
            if (GameplayPanelHooks.OpenPanel == OpenPanel)
            {
                GameplayPanelHooks.OpenPanel = null;
                GameplayPanelHooks.PanelType = null;
                GameplayPanelHooks.IsScenePanel = false;
            }
        }

        private GameFlowPanel OpenPanel(GameFlowBootstrap owner)
        {
            flow = owner;
            SetFlow(owner);
            gameObject.SetActive(true);
            return this;
        }

        private void BuildUi()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            RectTransform root = transform as RectTransform;
            if (root == null) return;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;

            Image background = CreateImage("MapBackdrop", root, new Color(0.12f, 0.18f, 0.16f, 1f));
            Stretch(background.rectTransform);
            mapRoot = CreateRect("MapViewport", root);
            Stretch(mapRoot);
            Image inputSurface = mapRoot.gameObject.AddComponent<Image>();
            inputSurface.color = new Color(0f, 0f, 0f, 0f);
            inputSurface.raycastTarget = true;
            mapRoot.gameObject.AddComponent<MapDragReceiver>().Initialize(this);
            mapRoot.gameObject.AddComponent<RectMask2D>();
            mapContent = CreateRect("MapContent", mapRoot);
            mapContent.anchorMin = mapContent.anchorMax = new Vector2(.5f, .5f);
            mapContent.pivot = new Vector2(.5f, .5f);
            mapContent.sizeDelta = new Vector2(MapWidth, MapHeight);
            mapContent.anchoredPosition = Vector2.zero;
            Image map = CreateSpriteImage("MapBackground", mapContent, mapBackgroundSprite);
            map.raycastTarget = false;
            Stretch(map.rectTransform);
            CreateMapLayer("LocationSigns", locationSignsSprite, new Vector2(-1.45f, 9.23f));
            CreateMapLayer("LocationIcons", locationIconsSprite, new Vector2(-1.32f, 11.33f));
            CreateMapLayer("LocationNames", locationNamesSprite, new Vector2(-1.48f, 9.21f));
            CreateMapLayer("LocationPins", pinSprite, new Vector2(-2.19f, 4.65f));
            mapContent.gameObject.AddComponent<MapDragReceiver>().Initialize(this);
            BuildConfiguredLocations();
            focusPanel = CreateActionPanel(root);
            actionList = focusPanel.transform.Find("ActionList") as RectTransform;
            focusPanel.SetActive(false);

                        // Fixed UI layer: these controls are intentionally outside MapContent.
            TextMeshProUGUI header = CreateLabel("Header", root, "行动点 5 / 5    |    王国纪元 670年10月6日", 24, Color.white, TextAlignmentOptions.Center);
            header.rectTransform.SetAnchors(new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-100, -42), new Vector2(540, 48));
            Button detail = CreateButton("详情面板", root, new Vector2(250, -42), new Vector2(170, 44));
            RectTransform detailRect = detail.transform as RectTransform;
            detailRect.anchorMin = detailRect.anchorMax = new Vector2(.5f, 1f);
            detailRect.pivot = new Vector2(.5f, 1f);
            detailRect.anchoredPosition = new Vector2(350, -18);
            detail.onClick.AddListener(() => ShowFocus("详情面板", "我的信息 / 人物关系 / 世界情况（基础占位）。"));
            Image title = CreateSpriteImage("Title", root, cityTitleSprite);
            title.rectTransform.SetAnchors(new Vector2(0, 0), new Vector2(0, 0), new Vector2(42, 42), new Vector2(366, 118));
            title.rectTransform.pivot = new Vector2(0, 0);
            Image compass = CreateSpriteImage("Compass", root, compassSprite);
            compass.rectTransform.SetAnchors(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-88, -92), new Vector2(166, 184));
            BuildInventory(root);
            BuildTaskPanel(root);
            Button endDay = CreateButton("结束当天", root, new Vector2(-110, 46), new Vector2(180, 52));
            endDay.onClick.AddListener(() => flow?.GetComponent<DayFlowController>()?.EndDayFromMap());
            status = CreateLabel("Status", root, "滚轮缩放，WASD移动；点击地点聚焦，右键退出聚焦", 20, new Color(1,1,1,.8f), TextAlignmentOptions.Center);
            status.rectTransform.SetAnchors(new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-400, 18), new Vector2(800, 32));
        }

        private void Update()
        {
            if (!isActiveAndEnabled) return;
            if (mapRoot != null && mapRoot.rect.width <= 1f) Canvas.ForceUpdateCanvases();
            Keyboard key = Keyboard.current;
            Vector2 move = Vector2.zero;
            if (key != null) { if (key.wKey.isPressed) move.y += 1; if (key.sKey.isPressed) move.y -= 1; if (key.aKey.isPressed) move.x -= 1; if (key.dKey.isPressed) move.x += 1; }
            if (move.sqrMagnitude > 0) mapCenter += move.normalized * 520f * Time.unscaledDeltaTime / Mathf.Max(zoom, .01f);
            if (Mouse.current != null) { float wheel = Mouse.current.scroll.ReadValue().y; if (Mathf.Abs(wheel) > .01f) zoom = Mathf.Clamp(zoom + wheel * .0015f, .8f, 1.35f); }
            ApplyMapView();
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) HideFocus();
        }

        internal void Pan(Vector2 delta)
        {
            // Dragging changes the focus point, rather than applying an arbitrary
            // transform to the map. This keeps panning consistent at every zoom.
            mapCenter -= delta / Mathf.Max(zoom, .01f);
            ApplyMapView();
        }

        private void ApplyMapView()
        {
            if (mapContent == null || mapRoot == null) return;
            Vector2 viewport = mapRoot.rect.size;
            Vector2 scaledMap = new Vector2(MapWidth, MapHeight) * zoom;
            Vector2 maxOffset = Vector2.Max(Vector2.zero, (scaledMap - viewport) * .5f);
            Vector2 focusOffset = new Vector2(
                Mathf.Clamp(mapCenter.x * zoom, -maxOffset.x, maxOffset.x),
                Mathf.Clamp(mapCenter.y * zoom, -maxOffset.y, maxOffset.y));
            mapContent.localScale = Vector3.one * zoom;
            mapContent.anchoredPosition = -focusOffset;
        }
        private void BuildConfiguredLocations()
        {
            try
            {
                narrativeConfiguration = LubanNarrativeLoader.Load();
                EnsureNarrativeRuntime();
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Map] Could not load map position configuration: " + error.Message, this);
                return;
            }
            foreach (NarrativeMapPositionDefinition position in narrativeConfiguration.mapPositions)
            {
                if (position == null || !position.enabled) continue;
                string label = ResolveText(position.nameText, position.positionKey);
                Vector2 mapPosition = PositionForArtwork(position.positionKey, position.position);
                CreateLocation(mapContent, label, mapPosition, position.isImportant, () => OnPositionClicked(position));
            }
        }

        private void EnsureNarrativeRuntime()
        {
            if (NarrativeRuntime.Instance != null && NarrativeRuntime.Instance.IsReady) return;
            GameObject root = new GameObject("NarrativeRuntime.MapFallback");
            root.AddComponent<NarrativeRuntime>();
        }

        private Vector2 PositionForArtwork(string key, Vector2 configured)
        {
            // Temporary visual alignment for the imported PSD. Runtime data remains
            // untouched and can replace these anchors when its coordinate schema is finalized.
            return key switch
            {
                "cafe" => new Vector2(-470f, -170f),
                "market" => new Vector2(-35f, 145f),
                "library" => new Vector2(300f, -145f),
                "temporary" => new Vector2(125f, 260f),
                _ => configured
            };
        }

        private void OnPositionClicked(NarrativeMapPositionDefinition position)
        {
            mapCenter = PositionForArtwork(position.positionKey, position.position);
            ApplyMapView();
            if (position.positionKey == "cafe")
            {
                StartCafe();
                return;
            }
            FocusPosition(position);
        }

        private void FocusPosition(NarrativeMapPositionDefinition position)
        {
            focusedPosition = position;
            ShowFocus(ResolveText(position.nameText, position.positionKey), "请选择行动");
            RebuildActionList();
        }

        private void RebuildActionList()
        {
            if (actionList == null) return;
            for (int i = actionList.childCount - 1; i >= 0; i--) Destroy(actionList.GetChild(i).gameObject);
            if (focusedPosition == null || narrativeConfiguration == null) return;
            var keys = new List<string>();
            if (!string.IsNullOrWhiteSpace(focusedPosition.defaultAction)) keys.Add(focusedPosition.defaultAction);
            if (focusedPosition.extendedActions != null) keys.AddRange(focusedPosition.extendedActions);
            NarrativeActionController actions = NarrativeRuntime.Instance?.Actions;
            int row = 0;
            foreach (string key in keys.Distinct())
            {
                NarrativeActionDefinition action = actions?.Get(key);
                if (action == null) continue;
                Button button = CreateButton(ResolveText(action.nameText, key), actionList, new Vector2(0, -row * 58f), new Vector2(260, 48));
                button.onClick.AddListener(() => ExecuteAction(action));
                row++;
            }
            if (row == 0)
            {
                TextMeshProUGUI empty = CreateLabel("Empty", actionList, "此时此地无事可做", 18, Color.white, TextAlignmentOptions.Center);
                empty.rectTransform.SetAnchors(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -24), new Vector2(260, 48));
            }
        }

        private void ExecuteAction(NarrativeActionDefinition action)
        {
            NarrativeActionController actions = NarrativeRuntime.Instance?.Actions;
            if (actions == null) { status.text = "行动系统尚未初始化"; return; }
            if (!actions.Execute(action.actionKey, success =>
            {
                status.text = success ? "行动完成：" + ResolveText(action.nameText, action.actionKey) : "行动事件未能执行";
                RebuildActionList();
            }))
            {
                actions.IsAvailable(action.actionKey, out List<NarrativeActionCheck> checks);
                status.text = "行动需求未满足：" + string.Join("；", checks.Where(check => !check.satisfied).Select(check => check.requirement));
            }
        }

        private string ResolveText(string key, string fallback)
        {
            string text = NarrativeRuntime.Instance?.ResolveText(key);
            return string.IsNullOrEmpty(text) || text == key ? fallback : text;
        }

        private void BuildInventory(Transform parent)
        {
            GameObject panel = new GameObject("InventoryBar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            panel.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)panel.transform;
            rect.SetAnchors(new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 74), new Vector2(560, 88));
            panel.GetComponent<Image>().color = new Color(.04f, .07f, .07f, .92f);
            HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8; layout.padding = new RectOffset(12, 12, 10, 10); layout.childForceExpandWidth = false; layout.childForceExpandHeight = true; layout.childAlignment = TextAnchor.MiddleCenter;
            CreateInventorySlot(panel.transform, "咖啡豆", "12", new Color(.52f, .32f, .16f));
            CreateInventorySlot(panel.transform, "牛奶", "4", new Color(.78f, .86f, .9f));
            CreateInventorySlot(panel.transform, "糖", "8", new Color(.9f, .8f, .45f));
            CreateInventorySlot(panel.transform, "杯子", "3", new Color(.7f, .7f, .72f));
            CreateInventorySlot(panel.transform, "资金", "120", new Color(.4f, .72f, .4f));
        }

        private void CreateInventorySlot(Transform parent, string itemName, string count, Color iconColor)
        {
            GameObject slot = new GameObject(itemName, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(Button));
            slot.transform.SetParent(parent, false);
            slot.GetComponent<Image>().color = new Color(.12f, .16f, .16f, 1f);
            LayoutElement element = slot.GetComponent<LayoutElement>(); element.preferredWidth = 92; element.minWidth = 92;
            Button button = slot.GetComponent<Button>(); button.onClick.AddListener(() => ShowFocus(itemName, itemName + "：占位物品详情。"));
            GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)); icon.transform.SetParent(slot.transform, false); Image image = icon.GetComponent<Image>(); image.color = iconColor; image.raycastTarget = false; icon.GetComponent<RectTransform>().SetAnchors(new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(0,8), new Vector2(38,38));
            TextMeshProUGUI label = CreateLabel("Count", slot.transform, count, 16, Color.white, TextAlignmentOptions.BottomRight); label.rectTransform.SetAnchors(new Vector2(1,0), new Vector2(1,0), new Vector2(-5,4), new Vector2(42,22));
            TextMeshProUGUI name = CreateLabel("Name", slot.transform, itemName, 11, Color.white, TextAlignmentOptions.BottomLeft); name.rectTransform.SetAnchors(new Vector2(0,0), new Vector2(0,0), new Vector2(5,4), new Vector2(62,18));
        }

        private void BuildTaskPanel(Transform parent)
        {
            GameObject panel = new GameObject("TaskPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)panel.transform;
            rect.SetAnchors(new Vector2(0,1), new Vector2(0,1), new Vector2(28,-170), new Vector2(270,150));
            rect.pivot = new Vector2(0, 1);
            panel.GetComponent<Image>().color = new Color(.04f, .07f, .07f, .9f);
            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>(); layout.spacing = 5; layout.padding = new RectOffset(12,12,10,10); layout.childForceExpandHeight = false;
            CreateLabel("TaskTitle", panel.transform, "事项", 22, Color.white, TextAlignmentOptions.Left).rectTransform.sizeDelta = new Vector2(240, 30);
            CreateTaskRow(panel.transform, "进入绿道");
            CreateTaskRow(panel.transform, "完成今日营业");
            CreateTaskRow(panel.transform, "探索城区的新地点");
        }

        private void CreateTaskRow(Transform parent, string text)
        {
            GameObject row = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(LayoutElement)); row.transform.SetParent(parent, false); row.GetComponent<Image>().color = new Color(.12f,.16f,.16f,1f); row.GetComponent<LayoutElement>().preferredHeight = 28;
            CreateLabel("Task", row.transform, "□  " + text, 15, Color.white, TextAlignmentOptions.Left).rectTransform.SetAnchors(Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private void StartCafe() { flow?.GetComponent<DayFlowController>()?.StartCafeFromMap(); }
        private void ShowFocus(string name, string message)
        {
            if (focusPanel != null) focusPanel.SetActive(true);
            if (status != null) status.text = name + "：" + message;
        }
        private void HideFocus()
        {
            focusedPosition = null;
            RebuildActionList();
            if (focusPanel != null) focusPanel.SetActive(false);
            if (status != null) status.text = "滚轮缩放，WASD移动；点击地点聚焦，右键退出聚焦";
        }

        private void CreateLocation(RectTransform parent, string name, Vector2 position, bool important, Action action)
        {
            Button button = CreateButton(name, parent, position, important ? new Vector2(210, 130) : new Vector2(150, 100));
            Image image = button.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            TextMeshProUGUI label = button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null) label.enabled = false;
            button.onClick.AddListener(() => action?.Invoke());
            button.gameObject.AddComponent<MapLocationHover>();
        }

        private void CreateMapLayer(string name, Sprite sprite, Vector2 psdPosition)
        {
            if (sprite == null) return;
            Image image = CreateSpriteImage(name, mapContent, sprite);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(psdPosition.x * 50f * PsdScale, (psdPosition.y - 10.8f) * 50f * PsdScale);
            rect.sizeDelta = sprite.rect.size * PsdScale;
            image.raycastTarget = false;
        }

        private static GameObject CreateActionPanel(RectTransform root)
        {
            GameObject panel = new GameObject("LocationActionPopup", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root, false);
            RectTransform rect = (RectTransform)panel.transform;
            rect.SetAnchors(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-190, 0), new Vector2(300, 280));
            panel.GetComponent<Image>().color = new Color(.06f, .08f, .09f, .94f);
            RectTransform list = CreateRect("ActionList", panel.transform);
            list.SetAnchors(new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -145), new Vector2(270, 230));
            return panel;
        }

        private static RectTransform CreateRect(string name, Transform parent) { GameObject go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform; }
        private static Image CreateImage(string name, Transform parent, Color color) { GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); Image image = go.GetComponent<Image>(); image.color = color; return image; }
        private static Image CreateSpriteImage(string name, Transform parent, Sprite sprite) { GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); Image image = go.GetComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; image.color = Color.white; return image; }
        private static TextMeshProUGUI CreateLabel(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment) { GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>(); label.text = text; label.fontSize = size; label.color = color; label.alignment = alignment; label.raycastTarget = false; return label; }
        private static Button CreateButton(string text, Transform parent, Vector2 position, Vector2 size) { GameObject go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false); RectTransform rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; Image image = go.GetComponent<Image>(); image.color = new Color(.08f, .11f, .12f, .92f); Button button = go.GetComponent<Button>(); CreateLabel("Label", go.transform, text, 24, Color.white, TextAlignmentOptions.Center); Stretch((RectTransform)go.transform.GetChild(0)); return button; }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
    }

    internal sealed class MapLocationHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform rect;
        private Image image;
        private Vector3 baseScale;
        private bool hovered;

        private void Awake()
        {
            rect = transform as RectTransform;
            image = GetComponent<Image>();
            baseScale = rect.localScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            rect.localScale = baseScale * 1.08f;
            if (image != null) image.color = new Color(1f, .88f, .55f, .18f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            rect.localScale = baseScale;
            if (image != null) image.color = new Color(1f, 1f, 1f, 0f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            rect.localScale = baseScale * .96f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            rect.localScale = hovered ? baseScale * 1.08f : baseScale;
        }
    }

    internal sealed class MapDragReceiver : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private MapSceneController owner;
        public void Initialize(MapSceneController value) => owner = value;
        public void OnPointerDown(PointerEventData eventData) { }
        public void OnDrag(PointerEventData eventData) { owner?.Pan(eventData.delta); }
        public void OnPointerUp(PointerEventData eventData) { }
    }

    internal static class RectTransformExtensions
    {
        public static void SetAnchors(this RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size) { rect.anchorMin = min; rect.anchorMax = max; rect.anchoredPosition = position; rect.sizeDelta = size; }
    }
}
