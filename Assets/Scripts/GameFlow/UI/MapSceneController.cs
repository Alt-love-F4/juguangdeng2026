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
        // The replacement PSD exports use a 2048x1152 full-canvas layout.
        private const float PsdScale = 2400f / 2048f;

        [Header("PSD map layers")]
        [SerializeField] private Sprite mapBackgroundSprite;
        [SerializeField] private Sprite pinSprite;
        [SerializeField] private Sprite cityTitleSprite;
        [SerializeField] private Sprite compassSprite;
        private const string GenericPinResource = "Art/Map/Locations/location_pin";
        private readonly Dictionary<string, NarrativeMapPositionDefinition> runtimePositions = new Dictionary<string, NarrativeMapPositionDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> locationButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<string, TextMeshProUGUI> locationLabels = new Dictionary<string, TextMeshProUGUI>(StringComparer.Ordinal);
        [Header("Map camera")]
        [SerializeField] private MapFeelConfig feelConfig;
        private float focusSmoothTime => feelConfig.focusSmoothTime;
        private float focusZoom => feelConfig.focusZoom;
        private float backgroundScale => feelConfig.backgroundScale;
        private float zoomSmoothTime => feelConfig.zoomSmoothTime;
        private float minimumZoom => feelConfig.minimumZoom;
        private float maximumZoom => feelConfig.maximumZoom;
        private float scrollZoomSensitivity => feelConfig.scrollZoomSensitivity;
        private float panInertiaDamping => feelConfig.panInertiaDamping;
        private Vector2 panVelocity;
        private Vector2 keyboardPanVelocity;
        [SerializeField] private RectTransform mapRoot;
        [SerializeField] private RectTransform mapContent;
        [SerializeField] private RectTransform mapBackground;
        [SerializeField] private RectTransform entities, decorations, hud, widgets;
        private MapSceneController scenePanelInstance;
        private bool buildingForEditor;
        // Camera bounds use the playable map; the larger backdrop supplies overscan.
        private Vector2 mapCenter;
        private Vector2 focusTarget;
        private Vector2 focusVelocity;
        private bool movingToFocus;
        private Action afterFocus;
        private float zoom = 1f;
        private float targetZoom = 1f;
        private float zoomVelocity;
        private bool focusZooming;
        private bool dragging;
        private bool standaloneLoading;
        private Vector2 dragStart;
        private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
        private PointerEventData mapPointerEvent;
        private Vector2 lastPointerPosition;
        private bool pointerDragging;
        private GameFlowBootstrap flow;
        [SerializeField] private TextMeshProUGUI status;
        [SerializeField] private GameObject focusPanel;
        private RectTransform actionList;
        [SerializeField] private MapActionDetailsView actionDetails;
        [SerializeField] private MapActionInventoryView inventoryView;
        [SerializeField] private Button endDayButton;
        [SerializeField] private TextMeshProUGUI headerLabel;
        private NarrativeConfiguration narrativeConfiguration;
        private NarrativeMapPositionDefinition focusedPosition;
        private RectTransform artworkHudRect;
        private TextMeshProUGUI endDayLabel;
        private Image endDayImage;
        private NarrativeRuntime subscribedNarrativeRuntime;

        protected override void Awake()
        {
            base.Awake();
            if (!(transform is RectTransform))
            {
                var prefab = Resources.Load<GameObject>("Art/UIPrefab/MapPanel");
                foreach (var candidate in GetComponentsInChildren<MapSceneController>(true))
                    if (candidate != this) { scenePanelInstance = candidate; break; }
                if (scenePanelInstance == null && prefab != null) scenePanelInstance = Instantiate(prefab, transform, false).GetComponent<MapSceneController>();
                if (scenePanelInstance == null) Debug.LogError("Generate Resources/Art/UIPrefab/MapPanel.prefab first.", this);
                enabled = false;
                return;
            }
            if (feelConfig == null) feelConfig = Resources.Load<MapFeelConfig>("MapFeelConfig");
            if (feelConfig == null)
            {
                Debug.LogError("MapSceneController requires a MapFeelConfig asset.", this);
                enabled = false;
                return;
            }
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "CoffeeShop")
            {
                GameplayPanelHooks.OpenPanel = OpenPanel;
                GameplayPanelHooks.PanelType = typeof(MapSceneController);
                GameplayPanelHooks.IsScenePanel = true;
            }
            // Runtime must preserve authored prefab geometry. BuildUi is reserved for the
            // explicit editor prefab builder; missing authored references are configuration errors.
            if (mapRoot != null && mapContent != null && hud != null && widgets != null)
            {
                if (actionDetails == null) actionDetails = GetComponentInChildren<MapActionDetailsView>(true);
                if (inventoryView == null) inventoryView = GetComponentInChildren<MapActionInventoryView>(true);
                if (actionDetails == null || inventoryView == null)
                {
                    Debug.LogError("MapPanel prefab is missing its authored action popup or inventory view.", this);
                    enabled = false;
                    return;
                }
                // Nested widget prefabs may leave the legacy popup reference unassigned.
                // Dismissal must target the same popup that handles location selection.
                if (focusPanel == null) focusPanel = actionDetails.gameObject;
                BindExistingUi();
            }
            else
            {
                Debug.LogError("MapPanel prefab is missing authored map root/HUD/widgets. Build the prefab in the Editor, not at runtime.", this);
                enabled = false;
                return;
            }
            artworkHudRect = hud != null ? hud.Find("ArtworkHud") as RectTransform : null;
            if (endDayButton != null)
            {
                endDayLabel = endDayButton.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                endDayImage = endDayButton.GetComponent<Image>();
            }
        }

        private void OnDestroy()
        {
            if (subscribedNarrativeRuntime != null && subscribedNarrativeRuntime.Controller != null)
                subscribedNarrativeRuntime.Controller.EffectRequested -= OnNarrativeEffect;
            if (GameplayPanelHooks.OpenPanel == OpenPanel)
            {
                GameplayPanelHooks.OpenPanel = null;
                GameplayPanelHooks.PanelType = null;
                GameplayPanelHooks.IsScenePanel = false;
            }
        }

        internal bool ControlsAvailable => !standaloneLoading && (flow == null || flow.MapControlsAvailable);

        internal void OpenOverlay(GameFlowBootstrap owner)
        {
            if (scenePanelInstance != null) { scenePanelInstance.OpenOverlay(owner); return; }
            flow = owner;
            SetFlow(owner);
            gameObject.SetActive(true);
            GetComponent<Canvas>().sortingOrder = 30;
            HideFocus();
            if (transform.Find("返回咖啡馆 / M") == null)
            {
                Button close = CreateButton("返回咖啡馆 / M", transform, Vector2.zero, new Vector2(230, 48));
                ((RectTransform)close.transform).SetAnchors(new Vector2(1, 0), new Vector2(1, 0),
                    new Vector2(-155, 110), new Vector2(230, 48));
                close.onClick.AddListener(owner.CloseMap);
            }
        }

        private GameFlowPanel OpenPanel(GameFlowBootstrap owner)
        {
            if (scenePanelInstance != null) return scenePanelInstance.OpenPanel(owner);
            flow = owner;
            SetFlow(owner);
            gameObject.SetActive(true);
            return this;
        }

        private Camera UiCamera => GetComponentInParent<Canvas>()?.worldCamera;

        /// <summary>Called on an inactive RectTransform root by the editor builder; never creates runtime services.</summary>
        public void BuildEditorHierarchy()
        {
            if (!(transform is RectTransform)) throw new InvalidOperationException("MapPanel requires RectTransform.");
            if (mapRoot != null) throw new InvalidOperationException("Hierarchy already built.");
            if (feelConfig == null) feelConfig = Resources.Load<MapFeelConfig>("MapFeelConfig");
            if (feelConfig == null) throw new InvalidOperationException("Missing MapFeelConfig.");
            buildingForEditor = true;
            try { BuildUi(); } finally { buildingForEditor = false; }
        }

        private void BindLocation(Button button, NarrativeMapPositionDefinition position)
        {
            button.onClick.AddListener(() => { if (!dragging) OnPositionClicked(position); });
            var hover = button.GetComponent<MapLocationHover>();
            if (hover == null) hover = button.gameObject.AddComponent<MapLocationHover>();
            hover.Initialize(this);
        }

        private void BindExistingUi()
        {
            inventoryView.Initialize(headerLabel);
            var pointsView = GetComponentInChildren<MapActionPointsView>(true);
            if (pointsView != null) pointsView.Initialize(headerLabel, inventoryView.PointsFill, inventoryView.DateLabel, inventoryView.PointsSegments);
            actionDetails.Initialize(inventoryView, () => ControlsAvailable, EndDay, StartCafe);
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                switch (button.name)
                {
                    case "详情面板": button.onClick.AddListener(OpenDetails); break;
                    case "InventoryToggle": button.onClick.AddListener(ToggleInventory); break;
                    case "TasksToggle": button.onClick.AddListener(ToggleTasks); break;
                    case "结束当天":
                        if (endDayButton == null) endDayButton = button;
                        button.onClick.AddListener(EndDay);
                        break;
                }
            }
            ConfigureZoomFill(zoomSlider);
            if (zoomSlider != null) zoomSlider.onValueChanged.AddListener(value => { targetZoom = value; focusZooming = false; });
            if (endDayButton != null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "CoffeeShop") endDayButton.gameObject.SetActive(false);
            BindConfiguredLocations();
            GetComponentInChildren<MapTaskPanel>(true)?.Initialize();
        }

        private void BuildUi()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            if (canvas.worldCamera == null) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
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
            mapRoot.gameObject.AddComponent<RectMask2D>();
            mapContent = CreateRect("MapContent", mapRoot);
            mapContent.anchorMin = mapContent.anchorMax = new Vector2(.5f, .5f);
            mapContent.pivot = new Vector2(.5f, .5f);
            mapContent.sizeDelta = new Vector2(MapWidth, MapHeight);
            mapContent.anchoredPosition = Vector2.zero;
            Image map = CreateSpriteImage("Background", mapContent, mapBackgroundSprite);
            entities = CreateRect("Entities", mapContent); Stretch(entities);
            decorations = CreateRect("Decorations", mapContent); Stretch(decorations);
            hud = CreateRect("HUD", root); Stretch(hud);
            widgets = CreateRect("Widgets", root); Stretch(widgets);
            map.raycastTarget = false;
            mapBackground = map.rectTransform;
            mapBackground.anchorMin = mapBackground.anchorMax = new Vector2(.5f, .5f);
            mapBackground.pivot = new Vector2(.5f, .5f);
            // Location buildings are rendered as their own interactive button sprites below.
            // Keep the sign/name overlays from the PSD, but omit the full icon layer to avoid
            // drawing a second, non-interactive copy underneath each button.
            // Replacement map exports are full-canvas transparent layers, so all layers share
            // the map center instead of using the offsets from the previous cropped PSD export.
            // Buildings are independent interactive sprites, not a duplicate full icon layer.
            // Location names, building artwork and pins are all built from MapPosition rows.
            BindConfiguredLocations();
            focusPanel = new GameObject("LocationActionPopup", typeof(RectTransform), typeof(Image), typeof(MapActionDetailsView));
            focusPanel.transform.SetParent(root, false);
            actionDetails = focusPanel.GetComponent<MapActionDetailsView>();
            focusPanel.SetActive(false);

                        // Fixed UI layer: these controls are intentionally outside MapContent.
            TextMeshProUGUI header = CreateLabel("Header", root, "行动点 5 / 5    |    王国纪元 670年10月6日", 24, Color.white, TextAlignmentOptions.Center);
            header.rectTransform.SetAnchors(new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-100, -42), new Vector2(540, 48));
            Button detail = CreateButton("详情面板", root, Vector2.zero, new Vector2(170, 44));
            RectTransform detailRect = detail.transform as RectTransform;
            detailRect.SetAnchors(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-210, -18), new Vector2(170, 44));
            detailRect.pivot = new Vector2(1, 1);
            detail.onClick.AddListener(OpenDetails);
            SetButtonText(detail, "详情面板 / Tab", 20);
            Image title = CreateSpriteImage("Title", root, cityTitleSprite);
            title.rectTransform.SetAnchors(new Vector2(0, 0), new Vector2(0, 0), new Vector2(42, 42), new Vector2(366, 118));
            title.rectTransform.pivot = new Vector2(0, 0);
            Image compass = CreateSpriteImage("Compass", root, compassSprite);
            compass.rectTransform.SetAnchors(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-88, -92), new Vector2(166, 184));
            var inventoryObject = new GameObject("InventoryBar", typeof(RectTransform), typeof(Image), typeof(MapActionInventoryView));
            inventoryObject.transform.SetParent(root, false);
            inventoryView = inventoryObject.GetComponent<MapActionInventoryView>();
            inventoryView.Initialize(header);
            headerLabel = header;
            header.transform.SetParent(hud, false);
            detail.transform.SetParent(hud, false);
            title.transform.SetParent(hud, false);
            compass.transform.SetParent(hud, false);
            inventoryObject.transform.SetParent(widgets, false);
            focusPanel.transform.SetParent(widgets, false);
            BuildArtworkHud(hud, header, detail);
            actionDetails.Initialize(inventoryView, () => ControlsAvailable, EndDay, StartCafe);
            BuildTaskPanel(widgets);
            Button endDay = CreateButton("结束当天", root, new Vector2(-110, 46), new Vector2(180, 52));
            endDayButton = endDay;
            endDay.transform.SetParent(hud, false);
            endDay.transform.SetParent(widgets.Find("TaskPanel"), false);
            ((RectTransform)endDay.transform).SetAnchors(new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 48), new Vector2(280, 50));
            MapVisualTheme.Skin(endDay.GetComponent<Image>(), MapVisualTheme.Current?.tracking);
            endDay.onClick.AddListener(EndDay);
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "CoffeeShop")
                endDay.gameObject.SetActive(false);
            status = CreateLabel("Status", root, "滚轮缩放，WASD移动；点击地点聚焦，右键退出聚焦", 20, new Color(1,1,1,.8f), TextAlignmentOptions.Center);
            status.transform.SetParent(hud, false);
            status.rectTransform.SetAnchors(new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 22), new Vector2(960, 32));
            status.gameObject.SetActive(false);
        }

        private void BuildArtworkHud(RectTransform root, TextMeshProUGUI header, Button detail)
        {
            var theme = MapVisualTheme.Current;
            if (theme == null) return;
            header.gameObject.SetActive(false);
            var hud = CreateRect("ArtworkHud", root);
            hud.SetAnchors(new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-20, -113), new Vector2(1320, 200));
            MapVisualTheme.Art("PointsBase", hud, theme.pointsBase, new Vector2(-404, 0), new Vector2(568, 113));
            var fill = MapVisualTheme.Art("PointsFill", hud, theme.pointsFill, new Vector2(-404, 0), new Vector2(568, 113));
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
            inventoryView.PointsFill = fill;
            MapVisualTheme.Art("PointsOutline", hud, theme.pointsOutline, new Vector2(-404, 0), new Vector2(568, 113));
            MapVisualTheme.Art("PointsLabel", hud, theme.pointsLabel, new Vector2(-620, 35), new Vector2(119, 34));
            var date = MapVisualTheme.Art("Date", hud, theme.dateFrame, new Vector2(48, 0), new Vector2(300, 157));
            var day = CreateLabel("DateText", date.transform, "10  06", 46, MapVisualTheme.Ink, TextAlignmentOptions.Center);
            Stretch(day.rectTransform); MapVisualTheme.Font(day, true); inventoryView.DateLabel = day;
            day.gameObject.SetActive(false);
            for (int part = 0; part < 2; part++)
            {
                var label = CreateLabel(part == 0 ? "Month" : "Day", date.transform, part == 0 ? "10" : "06", 86, MapVisualTheme.Ink, TextAlignmentOptions.Center);
                label.fontStyle = FontStyles.Bold;
                label.rectTransform.anchorMin = new Vector2(part * .5f, 0);
                label.rectTransform.anchorMax = new Vector2((part + 1) * .5f, 1);
                label.rectTransform.offsetMin = new Vector2(6, 4); label.rectTransform.offsetMax = new Vector2(-6, -4);
            }
            var segments = new List<Image>();
            if (theme.pointsSegments != null && theme.pointsSegments.Length > 0)
            {
                fill.gameObject.SetActive(false);
                float[] edges = { 0f, 357f, 510f, 663f, 816f, 971f };
                for (int i = 0; i < theme.pointsSegments.Length; i++)
                {
                    float left = theme.pointsSegments.Length == 5 ? edges[i] / 971f : (float)i / theme.pointsSegments.Length;
                    float right = theme.pointsSegments.Length == 5 ? edges[i + 1] / 971f : (float)(i + 1) / theme.pointsSegments.Length;
                    var segment = MapVisualTheme.Art("PointSegment" + i, hud, theme.pointsSegments[i], new Vector2(-404 - 284f + 568f * (left + right) * .5f, 0), new Vector2(568f * (right - left), 113));
                    segment.type = Image.Type.Filled; segment.fillMethod = Image.FillMethod.Horizontal;
                    segments.Add(segment);
                }
            }
            hud.Find("PointsOutline").SetAsLastSibling();
            inventoryView.PointsSegments = segments.ToArray();
            var hudPoints = hud.gameObject.AddComponent<MapActionPointsView>();
            hudPoints.Initialize(header, fill, day, segments.ToArray());
            detail.transform.SetParent(hud, false);
            detailRect(detail, 289); MapVisualTheme.Skin(detail.GetComponent<Image>(), theme.details);
            detail.transform.Find("Label").gameObject.SetActive(false);
            var bag = CreateButton("InventoryToggle", hud, new Vector2(421, 0), new Vector2(112, 107));
            MapVisualTheme.Skin(bag.GetComponent<Image>(), theme.bag); bag.transform.Find("Label").gameObject.SetActive(false);
            bag.onClick.AddListener(ToggleInventory);
            var tasks = CreateButton("TasksToggle", hud, new Vector2(553, 0), new Vector2(112, 107));
            MapVisualTheme.Skin(tasks.GetComponent<Image>(), theme.tasks); tasks.transform.Find("Label").gameObject.SetActive(false);
            tasks.onClick.AddListener(ToggleTasks);
            MapVisualTheme.Art("Shortcuts", hud, theme.shortcuts, new Vector2(421, -80), new Vector2(342, 44));
            var zoomControl = CreateRect("ZoomControl", root);
            zoomControl.SetAnchors(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-307, 73), new Vector2(440, 56));
            MapVisualTheme.Art("Bar", zoomControl, theme.zoomBar, Vector2.zero, new Vector2(440, 56));
            var hitArea = zoomControl.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            var handleArea = CreateRect("HandleArea", zoomControl);
            handleArea.anchorMin = new Vector2(0, 0); handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(73, 0); handleArea.offsetMax = new Vector2(-9, 0);
            var handle = MapVisualTheme.Art("Handle", handleArea, theme.zoomHandle, Vector2.zero, new Vector2(16, -16));
            zoomSlider = zoomControl.gameObject.AddComponent<Slider>();
            zoomSlider.minValue = MinimumZoom; zoomSlider.maxValue = MaximumZoom; zoomSlider.value = zoom;
            zoomSlider.handleRect = handle.rectTransform;
            zoomSlider.targetGraphic = handle;
            zoomSlider.transition = Selectable.Transition.None;
            ConfigureZoomFill(zoomSlider);
            zoomSlider.onValueChanged.AddListener(value => { targetZoom = value; focusZooming = false; });
        }

        // Both prefab binding and procedural construction use the same rail geometry.
        private static void ConfigureZoomFill(Slider slider)
        {
            if (slider == null || slider.handleRect == null) return;
            var control = slider.transform;
            var handleArea = slider.handleRect.parent as RectTransform;
            if (handleArea == null) return;
            var fillArea = control.Find("FillArea") as RectTransform;
            if (fillArea == null) fillArea = CreateRect("FillArea", control);
            fillArea.anchorMin = new Vector2(handleArea.anchorMin.x, .5f);
            fillArea.anchorMax = new Vector2(handleArea.anchorMax.x, .5f);
            fillArea.pivot = new Vector2(.5f, .5f);
            // The light rail occupies y=43..53 of the 734x92 zoomBar artwork.
            fillArea.offsetMin = new Vector2(handleArea.offsetMin.x, -4f);
            fillArea.offsetMax = new Vector2(handleArea.offsetMax.x, 2f);
            fillArea.SetAsLastSibling();
            handleArea.SetAsLastSibling();
            var fill = fillArea.Find("Fill")?.GetComponent<Image>();
            if (fill == null) fill = CreateImage("Fill", fillArea, Color.white);
            Stretch(fill.rectTransform);
            fill.sprite = null;
            fill.type = Image.Type.Simple;
            fill.color = new Color32(104, 91, 62, 255);
            fill.raycastTarget = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill.rectTransform;
        }

        [SerializeField] private Slider zoomSlider;
        private static void detailRect(Button button, float x)
        {
            var rect = (RectTransform)button.transform;
            rect.SetAnchors(new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x, 0), new Vector2(112, 107));
            rect.pivot = new Vector2(.5f, .5f);
        }

        private void OpenDetails()
        {
            if (flow == null) flow = FindFirstObjectByType<GameFlowBootstrap>();
            if (!ControlsAvailable) return;
            flow?.OpenDetails();
        }

        private void ToggleInventory()
        {
            if (!ControlsAvailable || (actionDetails != null && actionDetails.IsBusy)) return;
            var tasks = GetComponentInChildren<MapTaskPanel>(true);
            if (!inventoryView.IsVisible) tasks?.SetVisible(false);
            inventoryView.Toggle();
        }

        private void ToggleTasks()
        {
            if (!ControlsAvailable || (actionDetails != null && actionDetails.IsBusy)) return;
            var tasks = GetComponentInChildren<MapTaskPanel>(true);
            if (tasks == null) return;
            if (!tasks.IsVisible) inventoryView.SetVisible(false);
            tasks.Toggle();
        }

        private void Update()
        {
            if (!isActiveAndEnabled) return;
            Keyboard key = Keyboard.current;
            if (key != null && key.tabKey.wasPressedThisFrame) OpenDetails();
            if (key != null && key.bKey.wasPressedThisFrame) ToggleInventory();
            if (key != null && key.tKey.wasPressedThisFrame) ToggleTasks();
            if (artworkHudRect != null)
            {
                Vector3 hudScale = Vector3.one * Mathf.Min(1, Mathf.Max(0.1f, (hud.rect.width - 64) / 1380));
                if (!artworkHudRect.localScale.Equals(hudScale)) artworkHudRect.localScale = hudScale;
            }
            if (zoomSlider != null)
            {
                float minZoom = MinimumZoom;
                float maxZoom = Mathf.Max(minZoom, maximumZoom);
                if (zoomSlider.minValue != minZoom) zoomSlider.minValue = minZoom;
                if (zoomSlider.maxValue != maxZoom) zoomSlider.maxValue = maxZoom;
                float sliderValue = Mathf.Clamp(targetZoom, minZoom, maxZoom);
                if (zoomSlider.value != sliderValue) zoomSlider.SetValueWithoutNotify(sliderValue);
            }
            if (endDayButton != null)
            {
                bool blocking = NarrativeRuntime.Instance?.Actions?.HasBlockingActions() ?? false;
                string endDayText = blocking ? "有需要进行的行动" : "结束当天";
                if (endDayLabel != null)
                {
                    if (endDayLabel.text != endDayText) endDayLabel.text = endDayText;
                    if (endDayLabel.fontSize != 18f) endDayLabel.fontSize = 18f;
                }
                Color endDayColor = blocking ? new Color(.75f, .67f, .55f) : Color.white;
                if (endDayImage != null && !endDayImage.color.Equals(endDayColor)) endDayImage.color = endDayColor;
            }
            if (!ControlsAvailable)
            {
                pointerDragging = dragging = false;
                panVelocity = Vector2.zero;
                keyboardPanVelocity = Vector2.zero;
                return;
            }
            if (mapRoot != null && mapRoot.rect.width <= 1f) Canvas.ForceUpdateCanvases();
            Vector2 move = Vector2.zero;
            if (key != null) { if (key.wKey.isPressed) move.y += 1; if (key.sKey.isPressed) move.y -= 1; if (key.aKey.isPressed) move.x -= 1; if (key.dKey.isPressed) move.x += 1; }
            bool keyboardMoving = UpdateKeyboardPan(move, Time.unscaledDeltaTime);
            if (Mouse.current != null)
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > .01f && IsMapZoomPointerTarget()) AddScrollZoom(wheel);
                UpdatePointerPan(Mouse.current);
            }
            if (!keyboardMoving) UpdatePanInertia(Time.unscaledDeltaTime);
            UpdateZoom();
            UpdateFocusMovement();
            ApplyMapView();
            if (key != null && key.escapeKey.wasPressedThisFrame && (actionDetails == null || !actionDetails.IsBusy))
            {
                inventoryView.SetVisible(false);
                GetComponentInChildren<MapTaskPanel>(true)?.SetVisible(false);
                HideFocus();
            }
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame && (actionDetails == null || !actionDetails.IsBusy) && IsMapPointerTarget()) HideFocus();
        }

        private bool UpdateKeyboardPan(Vector2 input, float deltaTime)
        {
            if (!feelConfig.keyboardPanEnabled || pointerDragging || feelConfig.keyboardPanSpeed <= 0f)
            {
                keyboardPanVelocity = Vector2.zero;
                return false;
            }

            bool hasInput = input.sqrMagnitude > 0f;
            Vector2 targetVelocity = hasInput ? input.normalized * Mathf.Max(0f, feelConfig.keyboardPanSpeed) : Vector2.zero;
            float response = hasInput ? feelConfig.keyboardPanAcceleration : feelConfig.keyboardPanDeceleration;
            keyboardPanVelocity = response <= 0f ? targetVelocity :
                Vector2.MoveTowards(keyboardPanVelocity, targetVelocity, response * deltaTime);
            if (!hasInput && keyboardPanVelocity.sqrMagnitude == 0f) return false;

            CancelFocusMovement();
            panVelocity = Vector2.zero;
            float scale = feelConfig.keyboardPanCompensateZoom ? Mathf.Max(zoom, .01f) : 1f;
            Vector2 requested = mapCenter + keyboardPanVelocity * deltaTime / scale;
            mapCenter = ClampMapCenter(requested);
            if (mapCenter.x != requested.x) keyboardPanVelocity.x = 0f;
            if (mapCenter.y != requested.y) keyboardPanVelocity.y = 0f;
            return true;
        }

        private void UpdatePointerPan(Mouse mouse)
        {
            Vector2 pointer = mouse.position.ReadValue();
            bool pressed = mouse.leftButton.isPressed;
            if (!pressed)
            {
                pointerDragging = false;
                return;
            }
            if (!RectTransformUtility.RectangleContainsScreenPoint(mapRoot, pointer, UiCamera)) return;
            if (!IsMapPointerTarget()) return;
            if (!pointerDragging)
            {
                pointerDragging = true;
                keyboardPanVelocity = Vector2.zero;
                panVelocity = Vector2.zero;
                dragging = false;
                dragStart = pointer;
                lastPointerPosition = pointer;
                return;
            }
            Vector2 delta = pointer - lastPointerPosition;
            float canvasScale = GetComponent<Canvas>().scaleFactor;
            delta /= Mathf.Max(.01f, canvasScale);
            lastPointerPosition = pointer;
            if ((pointer - dragStart).sqrMagnitude > Mathf.Pow(Mathf.Max(0f, feelConfig.dragThreshold), 2f)) dragging = true;
            if (dragging)
            {
                float deltaTime = Time.unscaledDeltaTime;
                Vector2 velocity = deltaTime > .0001f ? -delta / (Mathf.Max(zoom, .01f) * deltaTime) : Vector2.zero;
                panVelocity = Vector2.Lerp(panVelocity, velocity, 1f - Mathf.Exp(-Mathf.Max(.1f, feelConfig.panVelocityResponse) * deltaTime));
                if (delta.sqrMagnitude > .01f) Pan(delta);
            }
        }

        private bool IsMapPointerTarget()
        {
            if (EventSystem.current == null) return true;
            if (mapPointerEvent == null) mapPointerEvent = new PointerEventData(EventSystem.current);
            mapPointerEvent.Reset();
            mapPointerEvent.position = Mouse.current.position.ReadValue();
            pointerHits.Clear();
            EventSystem.current.RaycastAll(mapPointerEvent, pointerHits);
            if (pointerHits.Count == 0) return true;
            Transform hit = pointerHits[0].gameObject.transform;
            return hit == mapRoot || hit.IsChildOf(mapRoot);
        }

        private bool IsMapZoomPointerTarget()
        {
            if (EventSystem.current == null) return true;
            if (mapPointerEvent == null) mapPointerEvent = new PointerEventData(EventSystem.current);
            mapPointerEvent.Reset();
            mapPointerEvent.position = Mouse.current.position.ReadValue();
            pointerHits.Clear();
            EventSystem.current.RaycastAll(mapPointerEvent, pointerHits);
            if (pointerHits.Count == 0) return true;

            // Decorative HUD graphics should not consume wheel zoom. Actual
            // selectable controls, such as the zoom slider, keep their own input.
            foreach (RaycastResult hit in pointerHits)
            {
                Transform target = hit.gameObject.transform;
                if (target == mapRoot || target.IsChildOf(mapRoot)) return true;
                if (target.GetComponentInParent<Selectable>() != null) return false;
            }
            return true;
        }

        internal void Pan(Vector2 delta)
        {
            // Dragging changes the focus point, rather than applying an arbitrary
            // transform to the map. This keeps panning consistent at every zoom.
            CancelFocusMovement();
            mapCenter -= delta / Mathf.Max(zoom, .01f);
            ApplyMapView();
        }

        private void OnDisable()
        {
            keyboardPanVelocity = Vector2.zero;
            panVelocity = Vector2.zero;
            pointerDragging = dragging = false;
        }

        private void UpdatePanInertia(float deltaTime)
        {
            if (pointerDragging || movingToFocus) return;
            if (panVelocity.sqrMagnitude < 1f) { panVelocity = Vector2.zero; return; }
            float damping = Mathf.Max(.1f, panInertiaDamping);
            float decay = Mathf.Exp(-damping * deltaTime);
            Vector2 requested = mapCenter + panVelocity * ((1f - decay) / damping);
            mapCenter = ClampMapCenter(requested);
            panVelocity *= decay;
            if (mapCenter.x != requested.x) panVelocity.x = 0f;
            if (mapCenter.y != requested.y) panVelocity.y = 0f;
        }

        private float MinimumZoom => Mathf.Max(.01f, minimumZoom,
            mapRoot != null ? Mathf.Max(mapRoot.rect.width / MapWidth, mapRoot.rect.height / MapHeight) : 1);
        private float MaximumZoom => Mathf.Max(MinimumZoom, maximumZoom);

        private void AddScrollZoom(float wheel)
        {
            focusZooming = false;
            // Proportional steps give consistent wheel response at both ends of the range.
            targetZoom = Mathf.Clamp(targetZoom * Mathf.Exp(Mathf.Clamp(wheel * scrollZoomSensitivity, -4f, 4f)),
                MinimumZoom, MaximumZoom);
        }

        private void UpdateZoom()
        {
            targetZoom = Mathf.Clamp(targetZoom, MinimumZoom, MaximumZoom);
            zoom = Mathf.SmoothDamp(zoom, targetZoom, ref zoomVelocity,
                Mathf.Max(.01f, focusZooming ? focusSmoothTime : zoomSmoothTime), Mathf.Infinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(zoom - targetZoom) < .0001f && Mathf.Abs(zoomVelocity) < .001f)
            {
                zoom = targetZoom;
                zoomVelocity = 0f;
                focusZooming = false;
            }
        }

        private void ApplyMapView()
        {
            if (mapContent == null || mapRoot == null) return;
            zoom = Mathf.Clamp(zoom, MinimumZoom, MaximumZoom);
            mapCenter = ClampMapCenter(mapCenter);
            // Keep identical values out of RectTransform setters, including duplicate
            // calls from Pan/focus completion and the end of Update.
            Vector2 backgroundSize = new Vector2(MapWidth, MapHeight);
            if (mapBackground != null && !mapBackground.sizeDelta.Equals(backgroundSize)) mapBackground.sizeDelta = backgroundSize;
            Vector3 contentScale = Vector3.one * zoom;
            Vector2 contentPosition = -mapCenter * zoom;
            if (!mapContent.localScale.Equals(contentScale)) mapContent.localScale = contentScale;
            if (!mapContent.anchoredPosition.Equals(contentPosition)) mapContent.anchoredPosition = contentPosition;
        }
        private Vector2 ClampMapCenter(Vector2 center)
        {
            Vector2 viewport = mapRoot != null ? mapRoot.rect.size : new Vector2(MapWidth, MapHeight);
            float scale = Mathf.Max(zoom, MinimumZoom);
            Vector2 limits = Vector2.Max(Vector2.zero, (new Vector2(MapWidth, MapHeight) - viewport / scale) * .5f);
            return new Vector2(Mathf.Clamp(center.x, -limits.x, limits.x), Mathf.Clamp(center.y, -limits.y, limits.y));
        }

        private void BeginFocusMovement(Vector2 target, Action onComplete = null)
        {
            keyboardPanVelocity = Vector2.zero;
            panVelocity = Vector2.zero;
            focusTarget = target;
            targetZoom = Mathf.Clamp(focusZoom, MinimumZoom, MaximumZoom);
            focusZooming = true;
            movingToFocus = true;
            afterFocus = onComplete;
            // Retain velocity when retargeting so rapid selections do not stop abruptly.
        }

        private void UpdateFocusMovement()
        {
            if (!movingToFocus) return;
                Vector2 viewport = mapRoot.rect.size;
                Vector2 limits = Vector2.Max(Vector2.zero, (new Vector2(MapWidth, MapHeight) - viewport / Mathf.Max(targetZoom, MinimumZoom)) * .5f);
                Vector2 boundedTarget = new Vector2(Mathf.Clamp(focusTarget.x, -limits.x, limits.x), Mathf.Clamp(focusTarget.y, -limits.y, limits.y));
                mapCenter = Vector2.SmoothDamp(mapCenter, boundedTarget, ref focusVelocity,
                Mathf.Max(.01f, focusSmoothTime), Mathf.Infinity, Time.unscaledDeltaTime);
            if ((mapCenter - boundedTarget).sqrMagnitude > .01f || focusVelocity.sqrMagnitude > 1f) return;
            mapCenter = boundedTarget;
            Action completed = afterFocus;
            CancelFocusMovement();
            ApplyMapView();
            completed?.Invoke();
        }

        private void CancelFocusMovement()
        {
            movingToFocus = false;
            focusVelocity = Vector2.zero;
            afterFocus = null;
        }

        private void BindConfiguredLocations()
        {
            if (buildingForEditor) return;
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
            if (narrativeConfiguration == null) return;
            foreach (NarrativeMapPositionDefinition position in narrativeConfiguration.mapPositions)
            {
                if (position == null || string.IsNullOrWhiteSpace(position.positionKey)) continue;
                runtimePositions[position.positionKey] = position;
                bool visible = position.isImportant || position.enabled;
                BindAuthoredLocation(position);
                if (locationButtons.TryGetValue(position.positionKey, out Button authoredButton))
                    authoredButton.gameObject.SetActive(visible);
            }
            SubscribePositionEffects();
        }

        private void BindAuthoredLocation(NarrativeMapPositionDefinition position)
        {
            if (entities == null || position == null) return;
            Button button = entities.Find("Location:" + position.positionKey)?.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogWarning("[Map] No authored location object for " + position.positionKey + "; runtime will not synthesize map artwork.", this);
                return;
            }
            locationButtons[position.positionKey] = button;
            var label = button.transform.Find("LocationName")?.GetComponent<TextMeshProUGUI>();
            if (label != null) { label.text = ResolveText(position.nameText, position.positionKey); locationLabels[position.positionKey] = label; }
            BindLocation(button, position);
        }

        private void EnsureNarrativeRuntime()
        {
            var runtime = NarrativeRuntime.Instance;
            if (runtime == null || !runtime.IsReady)
            {
                GameObject root = new GameObject("NarrativeRuntime.MapFallback");
                runtime = root.AddComponent<NarrativeRuntime>();
            }
            runtime.ConversationPlayback = NarrativeConversationEventPlayback.Play;
            runtime.ReadingPlayback = ReadingEventPlayback.Play;
            runtime.NoticePlayback = NarrativeNoticeEventPlayback.Play;
            runtime.CharacterResolver = NarrativeCharacterResolver.Resolve;
        }

        private void SubscribePositionEffects()
        {
            NarrativeRuntime runtime = NarrativeRuntime.Instance;
            if (runtime == null || runtime == subscribedNarrativeRuntime) return;
            if (subscribedNarrativeRuntime != null) subscribedNarrativeRuntime.Controller.EffectRequested -= OnNarrativeEffect;
            subscribedNarrativeRuntime = runtime;
            runtime.Controller.EffectRequested += OnNarrativeEffect;
        }

        private void OnNarrativeEffect(NarrativeEffectNotification notification)
        {
            if (notification?.Result == null) return;
            NarrativeResultDefinition result = notification.Result;
            string key = string.IsNullOrWhiteSpace(result.target) ? string.Empty : result.target.Trim();
            switch (result.resultType)
            {
                case NarrativeResultType.ShowPosition:
                    if (runtimePositions.TryGetValue(key, out var shown) && !shown.isImportant) SetPositionVisible(shown, true);
                    break;
                case NarrativeResultType.HidePosition:
                    if (runtimePositions.TryGetValue(key, out var hidden) && !hidden.isImportant) SetPositionVisible(hidden, false);
                    break;
                case NarrativeResultType.AddTempPosition:
                    AddTemporaryPosition(result);
                    break;
                case NarrativeResultType.RemoveTempPosition:
                    RemoveTemporaryPosition(key);
                    break;
            }
        }

        private void SetPositionVisible(NarrativeMapPositionDefinition position, bool visible)
        {
            if (position == null || position.isImportant) return;
            position.enabled = visible;
            if (locationButtons.TryGetValue(position.positionKey, out Button existing) && existing != null)
                existing.gameObject.SetActive(visible);
            if (!visible)
            {
                if (focusedPosition == position) HideFocus();
                return;
            }
            if (!locationButtons.ContainsKey(position.positionKey)) BindAuthoredLocation(position);
        }

        private void AddTemporaryPosition(NarrativeResultDefinition result)
        {
            string[] values = (result.value ?? string.Empty).Split(new[] { '|', ';' }, StringSplitOptions.None);
            if (values.Length < 8) { Debug.LogWarning("[Map] ADDTEMP_POSITION expects key|nameText|x|y|isImportant|defaultAction|extendedActions|imgPath", this); return; }
            if (!float.TryParse(values[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(values[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)
                || !bool.TryParse(values[4], out bool important)) { Debug.LogWarning("[Map] Invalid temporary position coordinates/importance: " + result.value, this); return; }
            string key = values[0].Trim();
            if (string.IsNullOrEmpty(key) || runtimePositions.ContainsKey(key)) { Debug.LogWarning("[Map] Temporary position key is empty or already in use: " + key, this); return; }
            if (string.IsNullOrWhiteSpace(values[1])) { Debug.LogWarning("[Map] Temporary position nameText is required: " + key, this); return; }
            var position = new NarrativeMapPositionDefinition { positionKey = key, nameText = values[1], position = new Vector2(x, y), isImportant = important,
                defaultAction = values[5].Trim(), extendedActions = SplitActions(values[6]), imgPath = values[7].Trim(),
                enabled = true, isTemporary = true, dismissOnClick = !important };
            runtimePositions.Add(key, position);
            if (!string.IsNullOrEmpty(position.defaultAction)) NarrativeRuntime.Instance?.Actions?.Mount(key, position.defaultAction);
            foreach (string action in position.extendedActions) if (!string.IsNullOrEmpty(action)) NarrativeRuntime.Instance?.Actions?.Mount(key, action);
            CreateLocation(position, true);
        }

        private static List<string> SplitActions(string value) => (value ?? string.Empty).Split(new[] { ',', '+', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(item => item.Trim()).Where(item => item.Length > 0).ToList();

        private void RemoveTemporaryPosition(string key)
        {
            if (!runtimePositions.TryGetValue(key, out var position) || !position.isTemporary) return;
            if (locationButtons.TryGetValue(key, out Button existing)) Destroy(existing.gameObject);
            locationButtons.Remove(key); locationLabels.Remove(key);
            if (focusedPosition == position) HideFocus();
            runtimePositions.Remove(key);
        }

        private void OnPositionClicked(NarrativeMapPositionDefinition position)
        {
            if (!ControlsAvailable || (actionDetails != null && actionDetails.IsBusy) || position == null) return;
            FocusPosition(position);
            BeginFocusMovement(position.position);
            // Ordinary map locations remain visible while their action details are open.
            // Only explicitly dismissible temporary markers should disappear on click.
            if (position.isTemporary && position.dismissOnClick) RemoveTemporaryPosition(position.positionKey);
        }

        private void FocusPosition(NarrativeMapPositionDefinition position)
        {
            focusedPosition = position;
            if (actionDetails != null) actionDetails.Show(position.positionKey, ResolveText(position.nameText, position.positionKey));
        }

        internal void PositionActionDetailsNearLocation(string positionKey)
        {
            if (actionDetails == null || widgets == null || string.IsNullOrEmpty(positionKey)) return;
            if (!locationButtons.TryGetValue(positionKey, out Button button) || button == null) return;
            RectTransform popup = actionDetails.transform as RectTransform;
            Canvas.ForceUpdateCanvases();
            if (popup.rect.width <= 1 || popup.rect.height <= 1) return;
            Vector3 worldPoint = button.transform is RectTransform locationRect
                ? locationRect.TransformPoint(locationRect.rect.center)
                : button.transform.position;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(UiCamera, worldPoint);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(widgets, screenPoint, UiCamera, out Vector2 localPoint)) return;
            const float gap = 28f;
            Rect bounds = widgets.rect;
            float width = Mathf.Min(popup.rect.width, bounds.width - 32f);
            float height = Mathf.Min(popup.rect.height, bounds.height - 32f);
            popup.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            popup.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            Vector2 right = new Vector2(localPoint.x + width * .5f + gap, localPoint.y);
            Vector2 left = new Vector2(localPoint.x - width * .5f - gap, localPoint.y);
            Vector2 centered = Vector2.zero;
            Vector2[] candidates = { right, left, centered, new Vector2(0, bounds.yMax - height * .5f - 16f), new Vector2(0, bounds.yMin + height * .5f + 16f) };
            Vector2 candidate = centered;
            float bestScore = float.PositiveInfinity;
            RectTransform hudRoot = transform.Find("HUD") as RectTransform;
            RectTransform widgetsRoot = widgets;
            for (int i = 0; i < candidates.Length; i++)
            {
                Vector2 point = candidates[i];
                point.x = Mathf.Clamp(point.x, bounds.xMin + width * .5f + 16f, bounds.xMax - width * .5f - 16f);
                point.y = Mathf.Clamp(point.y, bounds.yMin + height * .5f + 16f, bounds.yMax - height * .5f - 16f);
                Rect candidateRect = new Rect(point.x - width * .5f, point.y - height * .5f, width, height);
                float score = (point - candidates[i]).sqrMagnitude * 100f;
                RectTransform[] roots = { hudRoot, widgetsRoot };
                foreach (RectTransform obstacleRoot in roots)
                {
                    if (obstacleRoot == null) continue;
                    foreach (RectTransform hudElement in obstacleRoot.GetComponentsInChildren<RectTransform>())
                    {
                        if (hudElement == obstacleRoot || hudElement == popup || popup.IsChildOf(hudElement) || !hudElement.gameObject.activeInHierarchy) continue;
                        Vector3[] corners = new Vector3[4];
                        hudElement.GetWorldCorners(corners);
                        Vector2 min = RectTransformUtility.WorldToScreenPoint(UiCamera, corners[0]);
                        Vector2 max = RectTransformUtility.WorldToScreenPoint(UiCamera, corners[2]);
                        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(widgets, min, UiCamera, out Vector2 localMin) ||
                            !RectTransformUtility.ScreenPointToLocalPointInRectangle(widgets, max, UiCamera, out Vector2 localMax)) continue;
                        Rect hudRect = Rect.MinMaxRect(localMin.x, localMin.y, localMax.x, localMax.y);
                        float overlapWidth = Mathf.Min(candidateRect.xMax, hudRect.xMax) - Mathf.Max(candidateRect.xMin, hudRect.xMin);
                        float overlapHeight = Mathf.Min(candidateRect.yMax, hudRect.yMax) - Mathf.Max(candidateRect.yMin, hudRect.yMin);
                        if (overlapWidth > 0 && overlapHeight > 0)
                            score += overlapWidth * overlapHeight * (hudElement.GetComponent<Graphic>() != null ? 5f : 1f);
                    }
                }
                if (score < bestScore) { bestScore = score; candidate = point; }
            }
            popup.anchorMin = popup.anchorMax = new Vector2(.5f, .5f);
            popup.pivot = new Vector2(.5f, .5f);
            popup.anchoredPosition = candidate;
            actionDetails.transform.SetAsLastSibling();
        }

        private void RebuildActionList()
        {
            if (actionDetails != null) { actionDetails.Refresh(); return; }
            if (actionList == null) return;
            for (int i = actionList.childCount - 1; i >= 0; i--) Destroy(actionList.GetChild(i).gameObject);
            if (focusedPosition == null || narrativeConfiguration == null) return;
            var keys = new List<string>();
            if (!string.IsNullOrWhiteSpace(focusedPosition.defaultAction)) keys.Add(focusedPosition.defaultAction);
            if (focusedPosition.extendedActions != null) keys.AddRange(focusedPosition.extendedActions);
            NarrativeActionController actions = NarrativeRuntime.Instance?.Actions;
            int row = 0;
            if (focusedPosition.positionKey == "cafe"
                && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "CoffeeShop")
            {
                Button enter = CreateButton("进入咖啡馆", actionList,
                    Vector2.zero, new Vector2(260, 48));
                enter.onClick.AddListener(StartCafe);
                row++;
            }
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
                bool inCafe = focusedPosition.positionKey == "cafe"
                    && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "CoffeeShop";
                TextMeshProUGUI empty = CreateLabel("Empty", actionList,
                    inCafe ? "当前正处于咖啡馆" : "此时此地无事可做", 18, Color.white, TextAlignmentOptions.Center);
                empty.rectTransform.SetAnchors(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -24), new Vector2(260, 48));
            }
        }

        private void ExecuteAction(NarrativeActionDefinition action)
        {
            if (!ControlsAvailable) return;
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
            GameObject panel = parent.Find("TaskPanel")?.gameObject;
            if (panel == null)
            {
                panel = new GameObject("TaskPanel", typeof(RectTransform), typeof(Image), typeof(MapTaskPanel));
                panel.transform.SetParent(parent, false);
            }
            MapTaskPanel view = panel.GetComponent<MapTaskPanel>();
            view.Initialize();
            // Existing map examples; replace through SetItems when task progression data is available.
            // Runtime task contents are supplied by gameplay data, never hard-coded UI examples.
        }

        private static void SetButtonText(Button button, string text, float size)
        {
            TextMeshProUGUI label = button.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
        }

        private void EndDay()
        {
            if (!ControlsAvailable || (actionDetails != null && actionDetails.IsBusy)) return;
            if (NarrativeRuntime.Instance?.Actions?.HasBlockingActions() ?? false)
            {
                status.text = "有需要进行的行动：请先完成地图上的今日必做行动";
                var notice = MapActionDetailsView.Rect("EndDayBlockedNotice", transform);
                notice.anchorMin = notice.anchorMax = new Vector2(.5f, .5f);
                notice.sizeDelta = new Vector2(440, 120);
                notice.gameObject.AddComponent<Image>().color = new Color(.16f, .07f, .07f, .98f);
                var label = MapActionDetailsView.Label("Message", notice, "有需要进行的行动", 24);
                MapActionDetailsView.Stretch(label.rectTransform, 16);
                label.alignment = TextAlignmentOptions.Center;
                Destroy(notice.gameObject, 2);
                return;
            }
            var actions = NarrativeRuntime.Instance?.Actions;
            if (actions == null || actions.IsExecuting) return;
            var dayFlow = flow?.GetComponent<DayFlowController>();
            if (dayFlow != null) dayFlow.EndDayFromMap();
            else actions.BeginDay(checked(actions.CurrentDay + 1));
        }

        private void StartCafe()
        {
            if (!ControlsAvailable) return;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "CoffeeShop")
            {
                RebuildActionList();
                return;
            }
            CancelFocusMovement();
            DayFlowController dayFlow = flow != null ? flow.GetComponent<DayFlowController>() : null;
            if (dayFlow != null)
            {
                dayFlow.StartCafeFromMap();
                return;
            }
            if (flow != null) flow.TransitionToGameplayScene("CoffeeShop", null);
            else StartCoroutine(LoadCafeStandalone());
        }
        private System.Collections.IEnumerator LoadCafeStandalone()
        {
            if (standaloneLoading) yield break;
            standaloneLoading = true;
            var prefab = Resources.Load<GameObject>("Art/UIPrefab/LoadingPanel");
            if (prefab == null)
            {
                Debug.LogError("Missing LoadingPanel prefab.", this);
                standaloneLoading = false;
                yield break;
            }
            GameObject screen = Instantiate(prefab);
            DontDestroyOnLoad(screen);
            var canvas = screen.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            screen.AddComponent<GraphicRaycaster>();
            CanvasGroup group = screen.GetComponent<CanvasGroup>();
            group.alpha = 1f; group.interactable = group.blocksRaycasts = true;
            LoadingPanel panel = screen.GetComponent<LoadingPanel>();
            panel.SetTip("正在进入咖啡馆……"); panel.SetProgress(0f);
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return null;
            AsyncOperation operation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("CoffeeShop");
            operation.allowSceneActivation = false;
            float elapsed = 0f;
            while (operation.progress < .9f || elapsed < .5f)
            {
                elapsed += Time.unscaledDeltaTime;
                panel.SetProgress(Mathf.Clamp01(operation.progress / .9f));
                yield return null;
            }
            panel.SetProgress(1f);
            // Cleanup belongs to the persistent loading screen: this map is destroyed on activation.
            operation.completed += _ => Destroy(screen);
            operation.allowSceneActivation = true;
        }

        private void ShowFocus(string name, string message)
        {
            if (focusPanel != null) focusPanel.SetActive(true);
            if (status != null) status.text = name + "：" + message;
        }
        private void HideFocus()
        {
            panVelocity = Vector2.zero;
            CancelFocusMovement();
            focusedPosition = null;
            RebuildActionList();
            if (focusPanel != null) focusPanel.SetActive(false);
            if (status != null) status.text = "滚轮缩放，WASD移动；点击地点聚焦，右键退出聚焦";
        }

        private void CreateLocation(NarrativeMapPositionDefinition position, bool temporary)
        {
            if (position == null || entities == null) return;
            if (locationButtons.TryGetValue(position.positionKey, out Button old) && old != null) Destroy(old.gameObject);
            Sprite artwork = LoadMapSprite(position.imgPath);
            Sprite pin = position.isImportant ? null : LoadMapSprite(GenericPinResource);
            string displayName = ResolveText(position.nameText, position.positionKey);
            Button button = CreateButton("Location:" + position.positionKey, entities, position.position, position.isImportant ? new Vector2(210, 130) : new Vector2(88, 112));
            button.onClick.RemoveAllListeners();
            RectTransform rect = button.GetComponent<RectTransform>();
            Image baseImage = button.GetComponent<Image>();
            if (position.isImportant)
            {
                baseImage.sprite = artwork;
                baseImage.color = artwork != null ? Color.white : new Color(1, 1, 1, 0);
                baseImage.preserveAspect = true;
                if (artwork != null) rect.sizeDelta = artwork.rect.size * PsdScale;
            }
            else
            {
                baseImage.sprite = pin;
                baseImage.color = pin != null ? Color.white : Color.clear;
                baseImage.preserveAspect = true;
                if (artwork != null)
                {
                    Image icon = new GameObject("EventIcon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    icon.transform.SetParent(button.transform, false);
                    RectTransform iconRect = icon.rectTransform; iconRect.anchorMin = iconRect.anchorMax = new Vector2(.5f, .66f);
                    iconRect.pivot = new Vector2(.5f, .5f); iconRect.sizeDelta = new Vector2(38, 38);
                    icon.sprite = artwork; icon.preserveAspect = true; icon.raycastTarget = false;
                }
            }
            baseImage.raycastTarget = true;
            var label = new GameObject("LocationName", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(button.transform, false);
            RectTransform labelRect = label.rectTransform; labelRect.anchorMin = labelRect.anchorMax = new Vector2(.5f, 0);
            labelRect.pivot = new Vector2(.5f, 1); labelRect.anchoredPosition = new Vector2(0, position.isImportant ? -8 : 0);
            labelRect.sizeDelta = new Vector2(position.isImportant ? 220 : 160, 34);
            label.text = displayName; label.fontSize = 22; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            MapVisualTheme.Font(label, false);
            button.onClick.AddListener(() => { if (!dragging) OnPositionClicked(position); });
            button.gameObject.AddComponent<MapLocationHover>().Initialize(this);
            locationButtons[position.positionKey] = button;
            locationLabels[position.positionKey] = label;
        }

        private static Sprite LoadMapSprite(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath)) return null;
            Sprite sprite = Resources.Load<Sprite>(resourcePath.Trim());
            if (sprite == null) Debug.LogWarning("[Map] Sprite not found at Resources/" + resourcePath.Trim() + " (without extension).");
            return sprite;
        }

        private void CreateMapLayer(string name, Sprite sprite, Vector2 psdPosition)
        {
            if (sprite == null) return;
            Image image = CreateSpriteImage(name, decorations, sprite);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = (psdPosition - new Vector2(20.48f, 11.52f)) * (MapWidth / 40.96f);
            rect.sizeDelta = sprite.rect.size / sprite.pixelsPerUnit * (MapWidth / 40.96f);
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
        private static TextMeshProUGUI CreateLabel(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment) { GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>(); label.text = text; label.fontSize = size; MapVisualTheme.Font(label, size >= 24); label.alignment = alignment; label.raycastTarget = false; return label; }
        private static Button CreateButton(string text, Transform parent, Vector2 position, Vector2 size) { GameObject go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false); RectTransform rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; Image image = go.GetComponent<Image>(); image.color = new Color(.08f, .11f, .12f, .92f); Button button = go.GetComponent<Button>(); CreateLabel("Label", go.transform, text, 24, Color.white, TextAlignmentOptions.Center); Stretch((RectTransform)go.transform.GetChild(0)); return button; }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
    }

    internal static class RectTransformExtensions
    {
        public static void SetAnchors(this RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size) { rect.anchorMin = min; rect.anchorMax = max; rect.anchoredPosition = position; rect.sizeDelta = size; }
    }
}
