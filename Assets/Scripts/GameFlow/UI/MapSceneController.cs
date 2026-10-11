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
        [SerializeField] private Sprite locationIconsSprite;
        [SerializeField] private Sprite locationSignsSprite;
        [SerializeField] private Sprite locationNamesSprite;
        [SerializeField] private Sprite pinSprite;
        [SerializeField] private Sprite cityTitleSprite;
        [SerializeField] private Sprite compassSprite;
        [Serializable]
        private sealed class LocationArtwork
        {
            public string positionKey;
            public Sprite sprite;
            public Vector2 position;
        }
        [SerializeField] private List<LocationArtwork> locationArtwork = new List<LocationArtwork>();
        [SerializeField] private Sprite cafeArtwork;
        [SerializeField] private Sprite marketArtwork;
        [SerializeField] private Sprite libraryArtwork;
        [SerializeField] private Sprite temporaryArtwork;
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
        private RectTransform mapRoot;
        private RectTransform mapContent;
        private RectTransform mapBackground;
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
        private TextMeshProUGUI status;
        private GameObject focusPanel;
        private RectTransform actionList;
        private MapActionDetailsView actionDetails;
        private MapActionInventoryView inventoryView;
        private Button endDayButton;
        private NarrativeConfiguration narrativeConfiguration;
        private NarrativeMapPositionDefinition focusedPosition;

        protected override void Awake()
        {
            base.Awake();
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

        internal bool ControlsAvailable => !standaloneLoading && (flow == null || flow.MapControlsAvailable);

        internal void OpenOverlay(GameFlowBootstrap owner)
        {
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
            mapRoot.gameObject.AddComponent<RectMask2D>();
            mapContent = CreateRect("MapContent", mapRoot);
            mapContent.anchorMin = mapContent.anchorMax = new Vector2(.5f, .5f);
            mapContent.pivot = new Vector2(.5f, .5f);
            mapContent.sizeDelta = new Vector2(MapWidth, MapHeight);
            mapContent.anchoredPosition = Vector2.zero;
            Image map = CreateSpriteImage("MapBackground", mapContent, mapBackgroundSprite);
            map.raycastTarget = false;
            mapBackground = map.rectTransform;
            mapBackground.anchorMin = mapBackground.anchorMax = new Vector2(.5f, .5f);
            mapBackground.pivot = new Vector2(.5f, .5f);
            // Location buildings are rendered as their own interactive button sprites below.
            // Keep the sign/name overlays from the PSD, but omit the full icon layer to avoid
            // drawing a second, non-interactive copy underneath each button.
            // Replacement map exports are full-canvas transparent layers, so all layers share
            // the map center instead of using the offsets from the previous cropped PSD export.
            CreateMapLayer("LocationIcons", locationIconsSprite, new Vector2(19.15f, 12.62f));
            CreateMapLayer("LocationSigns", locationSignsSprite, new Vector2(19.03f, 10.89f));
            CreateMapLayer("LocationNames", locationNamesSprite, new Vector2(19.01f, 10.87f));
            CreateMapLayer("LocationPins", pinSprite, new Vector2(35.01f, 8.73f));
            BuildConfiguredLocations();
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
            BuildArtworkHud(root, header, detail);
            actionDetails.Initialize(inventoryView, () => ControlsAvailable, EndDay, StartCafe);
            BuildTaskPanel(root);
            Button endDay = CreateButton("结束当天", root, new Vector2(-110, 46), new Vector2(180, 52));
            endDayButton = endDay;
            endDay.onClick.AddListener(EndDay);
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "CoffeeShop")
                endDay.gameObject.SetActive(false);
            status = CreateLabel("Status", root, "滚轮缩放，WASD移动；点击地点聚焦，右键退出聚焦", 20, new Color(1,1,1,.8f), TextAlignmentOptions.Center);
            status.rectTransform.SetAnchors(new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-400, 18), new Vector2(800, 32));
        }

        private void BuildArtworkHud(RectTransform root, TextMeshProUGUI header, Button detail)
        {
            var theme = MapVisualTheme.Current;
            if (theme == null) return;
            header.gameObject.SetActive(false);
            var hud = CreateRect("ArtworkHud", root);
            hud.SetAnchors(new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -62), new Vector2(1020, 120));
            MapVisualTheme.Art("PointsBase", hud, theme.pointsBase, new Vector2(-290, 0), new Vector2(455, 90));
            var fill = MapVisualTheme.Art("PointsFill", hud, theme.pointsFill, new Vector2(-290, 0), new Vector2(455, 90));
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
            inventoryView.PointsFill = fill;
            MapVisualTheme.Art("PointsOutline", hud, theme.pointsOutline, new Vector2(-290, 0), new Vector2(455, 90));
            MapVisualTheme.Art("PointsLabel", hud, theme.pointsLabel, new Vector2(-465, 24), new Vector2(95, 27));
            var date = MapVisualTheme.Art("Date", hud, theme.dateFrame, new Vector2(5, 0), new Vector2(258, 108));
            var day = CreateLabel("DateText", date.transform, "10  06", 46, MapVisualTheme.Ink, TextAlignmentOptions.Center);
            Stretch(day.rectTransform); MapVisualTheme.Font(day, true); inventoryView.DateLabel = day;
            detail.transform.SetParent(hud, false);
            detailRect(detail, 200); MapVisualTheme.Skin(detail.GetComponent<Image>(), theme.details);
            detail.transform.Find("Label").gameObject.SetActive(false);
            var bag = CreateButton("InventoryToggle", hud, new Vector2(300, 0), new Vector2(82, 76));
            MapVisualTheme.Skin(bag.GetComponent<Image>(), theme.bag); bag.transform.Find("Label").gameObject.SetActive(false);
            bag.onClick.AddListener(inventoryView.Toggle);
            var tasks = CreateButton("TasksToggle", hud, new Vector2(400, 0), new Vector2(82, 76));
            MapVisualTheme.Skin(tasks.GetComponent<Image>(), theme.tasks); tasks.transform.Find("Label").gameObject.SetActive(false);
            tasks.onClick.AddListener(() => GetComponentInChildren<MapTaskPanel>(true)?.Toggle());
            MapVisualTheme.Art("Shortcuts", hud, theme.shortcuts, new Vector2(300, -48), new Vector2(252, 33));
            var zoomControl = CreateRect("ZoomControl", root);
            zoomControl.SetAnchors(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-205, 40), new Vector2(344, 44));
            MapVisualTheme.Art("Bar", zoomControl, theme.zoomBar, Vector2.zero, new Vector2(344, 44));
            var handle = MapVisualTheme.Art("Handle", zoomControl, theme.zoomHandle, Vector2.zero, new Vector2(12, 32));
            zoomSlider = zoomControl.gameObject.AddComponent<Slider>();
            zoomSlider.minValue = MinimumZoom; zoomSlider.maxValue = MaximumZoom; zoomSlider.value = zoom;
            zoomSlider.handleRect = handle.rectTransform;
            zoomSlider.onValueChanged.AddListener(value => { targetZoom = value; focusZooming = false; });
        }

        private Slider zoomSlider;
        private static void detailRect(Button button, float x)
        {
            var rect = (RectTransform)button.transform;
            rect.SetAnchors(new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x, 0), new Vector2(82, 76));
            rect.pivot = new Vector2(.5f, .5f);
        }

        private void OpenDetails()
        {
            if (flow == null) flow = FindFirstObjectByType<GameFlowBootstrap>();
            if (!ControlsAvailable) return;
            flow?.OpenDetails();
        }

        private void Update()
        {
            if (!isActiveAndEnabled) return;
            Keyboard key = Keyboard.current;
            if (key != null && key.tabKey.wasPressedThisFrame) OpenDetails();
            if (ControlsAvailable && key != null && key.bKey.wasPressedThisFrame) inventoryView.Toggle();
            if (ControlsAvailable && key != null && key.tKey.wasPressedThisFrame) GetComponentInChildren<MapTaskPanel>(true)?.Toggle();
            if (zoomSlider != null) zoomSlider.SetValueWithoutNotify(targetZoom);
            if (endDayButton != null)
            {
                bool blocking = NarrativeRuntime.Instance?.Actions?.HasBlockingActions() ?? false;
                SetButtonText(endDayButton, blocking ? "有需要进行的行动" : "结束当天", 18);
                endDayButton.GetComponent<Image>().color = blocking ? new Color(.3f, .2f, .18f) : new Color(.08f, .18f, .14f);
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
                if (Mathf.Abs(wheel) > .01f && IsMapPointerTarget()) AddScrollZoom(wheel);
                UpdatePointerPan(Mouse.current);
            }
            if (!keyboardMoving) UpdatePanInertia(Time.unscaledDeltaTime);
            UpdateZoom();
            UpdateFocusMovement();
            ApplyMapView();
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame && !actionDetails.IsBusy) HideFocus();
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
            if (!RectTransformUtility.RectangleContainsScreenPoint(mapRoot, pointer, null)) return;
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

        private float MinimumZoom => Mathf.Max(.01f, minimumZoom);
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
            Vector2 viewport = mapRoot.rect.size;
            zoom = Mathf.Clamp(zoom, MinimumZoom, MaximumZoom);
            mapCenter = ClampMapCenter(mapCenter);
            // Only enlarge the backdrop, keeping buildings and PSD overlays aligned.
            // Reserve half a viewport beyond every camera limit, including at low zoom.
            if (mapBackground != null)
                mapBackground.sizeDelta = Vector2.Max(new Vector2(MapWidth, MapHeight) * Mathf.Max(1f, backgroundScale),
                    new Vector2(MapWidth, MapHeight) + viewport / MinimumZoom * 1.05f);
            Vector2 focusOffset = mapCenter * zoom;
            mapContent.localScale = Vector3.one * zoom;
            mapContent.anchoredPosition = -focusOffset;
        }
        private static Vector2 ClampMapCenter(Vector2 center) => new Vector2(
            Mathf.Clamp(center.x, -MapWidth * .5f, MapWidth * .5f),
            Mathf.Clamp(center.y, -MapHeight * .5f, MapHeight * .5f));

        private void BeginFocusMovement(Vector2 target, Action onComplete = null)
        {
            keyboardPanVelocity = Vector2.zero;
            panVelocity = Vector2.zero;
            focusTarget = ClampMapCenter(target);
            targetZoom = Mathf.Clamp(focusZoom, MinimumZoom, MaximumZoom);
            focusZooming = true;
            movingToFocus = true;
            afterFocus = onComplete;
            // Retain velocity when retargeting so rapid selections do not stop abruptly.
        }

        private void UpdateFocusMovement()
        {
            if (!movingToFocus) return;
            mapCenter = Vector2.SmoothDamp(mapCenter, focusTarget, ref focusVelocity,
                Mathf.Max(.01f, focusSmoothTime), Mathf.Infinity, Time.unscaledDeltaTime);
            if ((mapCenter - focusTarget).sqrMagnitude > .01f || focusVelocity.sqrMagnitude > 1f) return;
            mapCenter = focusTarget;
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
            foreach (var artwork in locationArtwork)
            {
                if (!narrativeConfiguration.mapPositions.Any(p => p.positionKey == artwork.positionKey))
                    narrativeConfiguration.mapPositions.Add(new NarrativeMapPositionDefinition { positionKey = artwork.positionKey, nameText = artwork.positionKey, position = artwork.position, enabled = true });
            }
            foreach (NarrativeMapPositionDefinition position in narrativeConfiguration.mapPositions)
            {
                if (position == null || !position.enabled) continue;
                string label = ResolveText(position.nameText, position.positionKey);
                Vector2 mapPosition = PositionForArtwork(position.positionKey, position.position);
                CreateLocation(mapContent, label, mapPosition, position.isImportant, FindArtwork(position.positionKey), () => OnPositionClicked(position));
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
            var artwork = locationArtwork.FirstOrDefault(item => item != null && item.positionKey == key);
            return artwork != null ? artwork.position : configured;
        }

        private void OnPositionClicked(NarrativeMapPositionDefinition position)
        {
            if (!ControlsAvailable || actionDetails.IsBusy) return;
            BeginFocusMovement(PositionForArtwork(position.positionKey, position.position));
            FocusPosition(position);
        }

        private void FocusPosition(NarrativeMapPositionDefinition position)
        {
            focusedPosition = position;
            actionDetails.Show(position.positionKey, ResolveText(position.nameText, position.positionKey));
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
            GameObject panel = new GameObject("TaskPanel", typeof(RectTransform), typeof(Image), typeof(MapTaskPanel));
            panel.transform.SetParent(parent, false);
            MapTaskPanel view = panel.GetComponent<MapTaskPanel>();
            view.Initialize();
            // Existing map examples; replace through SetItems when task progression data is available.
            var examples = new List<MapTaskItem>();
            string[] active = { "进入绿道", "完成今日营业", "探索城区的新地点", "寻找可靠的保护", "协助别人的工作", "报告街道状况" };
            string[] completed = { "修好炉灶温备用", "船队援助", "让新版本机重获自由", "救出莹莹" };
            for (int i = 0; i < active.Length; i++)
                examples.Add(new MapTaskItem { id = "map_example_active_" + i, name = active[i], description = "示例事项，尚未接入剧情任务数据。", currentTask = active[i] });
            for (int i = 0; i < completed.Length; i++)
                examples.Add(new MapTaskItem { id = "map_example_completed_" + i, name = completed[i], description = "示例已完成事项，尚未接入剧情任务数据。", completed = true, previousTasks = new List<string> { completed[i] } });
            view.SetItems(examples);
        }

        private static void SetButtonText(Button button, string text, float size)
        {
            TextMeshProUGUI label = button.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
        }

        private void EndDay()
        {
            if (!ControlsAvailable || actionDetails.IsBusy) return;
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

        private Sprite FindArtwork(string key)
        {
            Sprite configured = locationArtwork.FirstOrDefault(item => item != null && item.positionKey == key)?.sprite;
            if (configured != null) return configured;
            return key switch
            {
                "cafe" => cafeArtwork,
                "market" => marketArtwork,
                "library" => libraryArtwork,
                "temporary" => temporaryArtwork,
                _ => pinSprite
            };
        }

        private void CreateLocation(RectTransform parent, string name, Vector2 position, bool important, Sprite artwork, Action action)
        {
            // The replacement full-canvas icon layer already draws the location artwork.
            // Keep an invisible button at each configured location for interaction only.
            Vector2 size = important ? new Vector2(210, 130) : new Vector2(150, 100);
            Button button = CreateButton(name, parent, position, size);
            Image image = button.GetComponent<Image>();
            image.sprite = null;
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            TextMeshProUGUI label = button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null) label.enabled = false;
            button.onClick.AddListener(() => { if (!dragging) action?.Invoke(); });
            button.gameObject.AddComponent<MapLocationHover>().Initialize(this);
        }

        private void CreateMapLayer(string name, Sprite sprite, Vector2 psdPosition)
        {
            if (sprite == null) return;
            Image image = CreateSpriteImage(name, mapContent, sprite);
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

    internal sealed class MapLocationHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform rect;
        private MapSceneController owner;
        public void Initialize(MapSceneController value) => owner = value;
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
            if (image != null) image.color = new Color(1, 1, 1, 0);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            rect.localScale = baseScale;
            if (image != null) image.color = new Color(1, 1, 1, 0);
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

    internal static class RectTransformExtensions
    {
        public static void SetAnchors(this RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size) { rect.anchorMin = min; rect.anchorMax = max; rect.anchoredPosition = position; rect.sizeDelta = size; }
    }
}
