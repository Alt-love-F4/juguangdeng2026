using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Spotlight.GameFlow
{
    /// <summary>Scrollable action cards. Unavailable buttons remain clickable to explain rejected requirements.</summary>
    public sealed class MapActionDetailsView : MonoBehaviour
    {
        /// <summary>Only the configured cafe position exposes the cafe navigation action.</summary>
        internal const string CafePositionKey = "cafe";
        internal static bool IsCafeEntryPosition(string positionKey)
            => positionKey == CafePositionKey;
        private RectTransform content;
        private TextMeshProUGUI title;
        private MapSceneController mapController;
        private MapActionInventoryView inventory;
        private string position;
        private Func<bool> controls;
        private Action endDay;
        private Func<bool> tryEnterCafe;
        private Action<string> navigationFailed;
        private bool busy;
        public bool IsBusy => busy;
        private string signature;
        private NarrativeActionController subscribedActions;
        private bool contentDirty = true;
        private bool cardsDirty = true;
        private bool lastControlsAvailable;
        private int pageChangedFrame;
        private int executionEpoch;
        private readonly Vector3[] cardCorners = new Vector3[4];
        private Vector2 fittedViewportSize;
        private RectTransform fittedCard;
        private readonly List<RaycastResult> displayHits = new List<RaycastResult>();
        private PointerEventData displayPointer;
        private ScrollRect scroll;
        private Button previousPageButton;
        private Button nextPageButton;
        private TextMeshProUGUI pageLabel;
        private int currentPage;
        private float revealStarted = float.NegativeInfinity;
        private float nextProjectionRefresh;
        private CanvasGroup revealGroup;
        [Header("Popup Layout")]
        [SerializeField] private Vector2 popupSize = new Vector2(820, 540);
        [SerializeField] private Vector2 popupAnchoredPosition = new Vector2(-280, -120);
        [SerializeField] private Vector2 popupAnchor = new Vector2(.5f, .5f);
        [SerializeField] private float cardWidth = 796;
        [SerializeField] private float cardSpacing = 24;
        [SerializeField] private float titleFontSize = 24;
        [SerializeField] private Vector2 emblemPosition = new Vector2(22, -20);
        [SerializeField] private Vector2 emblemSize = new Vector2(82, 82);
        [SerializeField] private Vector2 actionNamePosition = new Vector2(145, -38);
        [SerializeField] private Vector2 actionNameSize = new Vector2(544, 64);
        [SerializeField] private Vector2 descriptionPosition = new Vector2(36, -148);
        [SerializeField] private Vector2 executeButtonPosition = new Vector2(428, -255);
        [SerializeField] private Vector2 executeButtonSize = new Vector2(367, 160);
        [SerializeField] private bool showEditorPreview = true;
        [SerializeField] private string editorPreviewLocation = "圣徒街";
        [SerializeField] private EditorActionPreview[] editorPreviewActions = {
            new EditorActionPreview { name = "拜访旧城区的住民", description = "沿着街道拜访熟悉的住民，了解近期发生的事情。", requirement = "行动点\\n× 1", available = true },
            new EditorActionPreview { name = "搜寻街区物资", description = "在附近寻找可用的日常物资。需要消耗部分行动点与物品。", requirement = "咖啡豆\\n× 2", available = false },
            new EditorActionPreview { name = "帮助街坊完成委托", description = "完成一项阶段性工作，推进当前街区事件。", requirement = "牛奶\\n× 1", available = true }
        };
        [Serializable] private struct EditorActionPreview { public string name; [TextArea] public string description; public string requirement; public bool available; }
        private float Width => popupSize.x;
        private bool IsAuthoredLayout => transform.Find("Viewport/ActionList") != null;
        private float CardWidth => cardWidth;
        private Vector2 initialAnchoredPosition;
        [SerializeField] private RectTransform actionCardTemplate;
        private readonly List<Card> cards = new List<Card>();
        private sealed class Card
        {
            public RectTransform root;
            public NarrativeActionDefinition action;
            public NarrativeActionController owner;
            public string instanceKey;
            public bool displayConfirmed;
            public TextMeshProUGUI heading, description;
            public Bounds visualBounds;
            public bool boundsMeasured;
            public Button button;
            public TextMeshProUGUI type;
            public Image progress;
            public RectTransform repeats;
            public int repeatCount = -1;
            public readonly List<TextMeshProUGUI> requirements = new List<TextMeshProUGUI>();
            public readonly List<RectTransform> requirementSlots = new List<RectTransform>();
            public readonly List<RequirementProjection> projection = new List<RequirementProjection>();
            public RectTransform requirementTemplate;
            public TextMeshProUGUI gains;
            public string layoutSignature;
        }
        private sealed class RequirementProjection
        {
            public NarrativeActionRequirementType? costType;
            public string key, text;
            public long amount;
            public bool satisfied, ready = true;
        }
        public void Initialize(MapActionInventoryView inventoryView, Func<bool> canControl, Action finishDay, Action cafe)
        {
            inventory = inventoryView; controls = canControl; endDay = finishDay;
            tryEnterCafe = cafe == null ? null : () => { cafe(); return true; };
            navigationFailed = null;
            mapController = GetComponentInParent<MapSceneController>();
            var rect = (RectTransform)transform;
            bool authoredLayout = transform.Find("Viewport/ActionList") != null;
            popupSize = Vector2.Max(popupSize, GetRequiredPopupSize());
            if (authoredLayout) rect.sizeDelta = popupSize;
            if (!authoredLayout)
            {
                rect.anchorMin = rect.anchorMax = popupAnchor;
                rect.pivot = popupAnchor;
                rect.anchoredPosition = popupAnchoredPosition;
                rect.sizeDelta = popupSize;
            }
            initialAnchoredPosition = authoredLayout ? rect.anchoredPosition : popupAnchoredPosition;
            if (Application.isPlaying) gameObject.SetActive(false);
            if (transform.Find("Viewport/ActionList") is RectTransform existingContent)
            {
                content = existingContent;
                scroll = content.parent.GetComponent<ScrollRect>();
                ConfigurePagingLayout();
                title = transform.Find("Location").GetComponent<TextMeshProUGUI>();
                if (!Application.isPlaying && showEditorPreview)
                {
                    gameObject.SetActive(true);
                    transform.Find("Viewport")?.gameObject.SetActive(true);
                    title.gameObject.SetActive(true);
                    var closePreview = transform.Find("Close"); if (closePreview != null) closePreview.gameObject.SetActive(true);
                    if (content.childCount == 0) BuildEditorPreview();
                }
                // Authored popups may use the map's right-click/Escape dismissal instead of a close button.
                var existingClose = transform.Find("Close")?.GetComponent<UnityEngine.UI.Button>();
                if (existingClose != null)
                {
                    existingClose.onClick.RemoveAllListeners();
                    existingClose.onClick.AddListener(() => { if (!busy) gameObject.SetActive(false); });
                    Spotlight.Audio.UIButtonAudio.Configure(existingClose, Spotlight.Audio.AudioKeys.Cancel);
                }
                return;
            }
            GetComponent<Image>().color = Color.clear;
            title = Label("Location", rect, "行动详情", titleFontSize);
            Top(title.rectTransform, 24, -8, Width - 90, 42);
            title.gameObject.SetActive(false);
            var close = Button("Close", rect, "×", () => { if (!busy) gameObject.SetActive(false); });
            Top((RectTransform)close.transform, Width - 104, -12, 28, 28);
            close.GetComponent<Image>().color = Color.clear;
            close.gameObject.SetActive(false);
            var viewport = Rect("Viewport", rect);
            Stretch(viewport, 0);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.horizontal = false; scroll.vertical = false;
            scroll.scrollSensitivity = 0; scroll.movementType = ScrollRect.MovementType.Clamped;
            content = Rect("ActionList", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            scroll.content = content;
            ConfigurePagingLayout();
            close.transform.SetAsLastSibling();
        }
        internal void SetNavigationHandler(Func<bool> accepted, Action<string> failed)
        {
            tryEnterCafe = accepted;
            navigationFailed = failed;
        }

        internal void NotifyNavigationFailure(string reason)
        {
            busy = false;
            string message = string.IsNullOrWhiteSpace(reason) ? "无法进入咖啡馆，请稍后重试。" : reason;
            navigationFailed?.Invoke(message);
            Notice(message);
            foreach (var card in cards) UpdateCard(card);
        }

        public void Show(string positionKey, string locationName)
        {
            if (busy) return;
            if (!Application.isPlaying && showEditorPreview) { gameObject.SetActive(true); return; }
            bool changed = position != positionKey;
            position = positionKey; title.text = locationName;
            BindActionNotifications();
            bool opening = !gameObject.activeSelf || changed;
            gameObject.SetActive(true); Rebuild();
            revealGroup = GetComponent<CanvasGroup>();
            if (revealGroup == null) revealGroup = gameObject.AddComponent<CanvasGroup>();
            revealGroup.alpha = 1;
            if (opening)
            {
                revealStarted = Time.unscaledTime;
                revealGroup.alpha = 0;
            }
            if (changed) { currentPage = 0; scroll?.StopMovement(); }
            mapController?.PositionActionDetailsNearLocation(positionKey);
        }
        public void Refresh() { if (!busy && isActiveAndEnabled) Rebuild(); }
        private void LateUpdate()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null) return;
            float t = Mathf.Clamp01((Time.unscaledTime - revealStarted) / .26f);
            float alpha = Mathf.Clamp01(t * 2);
            if (revealGroup != null && revealGroup.alpha != alpha) revealGroup.alpha = alpha;
            FitVisibleCard();
            ConfirmVisibleActionDisplayed();
            if (IsAuthoredLayout) return;
            var rect = (RectTransform)transform;
            float scale = Mathf.Min(1, Mathf.Max(.1f, (parent.rect.width - 64) / popupSize.x));
            float pop = 1 + 2.7f * Mathf.Pow(t - 1, 3) + 1.7f * Mathf.Pow(t - 1, 2);
            Vector3 targetScale = Vector3.one * scale * Mathf.LerpUnclamped(.82f, 1, pop);
            if (!rect.localScale.Equals(targetScale)) rect.localScale = targetScale;
            float offset = Mathf.Min(Mathf.Abs(initialAnchoredPosition.x), Mathf.Max(0, (parent.rect.width - popupSize.x) * .5f - 32));
            var targetPosition = new Vector2(-offset, initialAnchoredPosition.y);
            if (!rect.anchoredPosition.Equals(targetPosition)) rect.anchoredPosition = targetPosition;
        }
        private void OnEnable() => BindActionNotifications();

        private void BindActionNotifications()
        {
            var actions = NarrativeRuntime.Instance?.Actions;
            if (actions == subscribedActions) return;
            UnbindActionNotifications();
            subscribedActions = actions;
            if (actions != null)
            {
                actions.MapContentChanged += OnMapContentChanged;
                actions.StateChanged += OnActionStateChanged;
            }
            contentDirty = cardsDirty = true;
        }

        private void UnbindActionNotifications()
        {
            if (subscribedActions == null) return;
            subscribedActions.MapContentChanged -= OnMapContentChanged;
            subscribedActions.StateChanged -= OnActionStateChanged;
            subscribedActions = null;
        }

        private void OnMapContentChanged(string key) { if (key == position) contentDirty = true; }
        private void OnActionStateChanged() => cardsDirty = true;
        private string ContentSignature(NarrativeActionController actions)
            => string.Join(";", actions.GetDisplayableActions(position).Select(a => actions.GetActionInstanceKey(position, a.actionKey)))
                + "/replace:" + actions.ReplacesLocationActions(position);

        private void Update()
        {
            if (busy || content == null) return;
            BindActionNotifications();
            bool canControl = controls?.Invoke() ?? true;
            if (lastControlsAvailable != canControl) { lastControlsAvailable = canControl; cardsDirty = true; }
            var actions = subscribedActions;
            if (actions == null) return;
            if (contentDirty)
            {
                contentDirty = false;
                if (ContentSignature(actions) != signature) { Rebuild(); return; }
            }
            // Wallet providers need not emit an Action StateChanged notification.
            if (Time.unscaledTime >= nextProjectionRefresh) { nextProjectionRefresh = Time.unscaledTime + .2f; cardsDirty = true; }
            if (!cardsDirty) return;
            cardsDirty = false;
            foreach (var card in cards) UpdateCard(card);
        }

        private void ConfirmVisibleActionDisplayed()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || busy || !(controls?.Invoke() ?? true)
                || Time.frameCount <= pageChangedFrame || (revealGroup != null && revealGroup.alpha < .99f)
                || currentPage >= cards.Count) return;
            Card card = cards[currentPage];
            if (card.action == null || card.displayConfirmed || card.root == null || !card.root.gameObject.activeInHierarchy) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.isActiveAndEnabled) return;
            foreach (var group in GetComponentsInParent<CanvasGroup>())
                if (group.alpha < .99f) return;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            // Reading a card is independent from its decorative bounds and execute button.
            // Both populated text areas must actually be exposed, not merely focused/instantiated.
            if (!IsTextAreaDisplayed(card.heading, card.root, camera)) return;
            if (card.description != null && !string.IsNullOrWhiteSpace(card.description.text)
                && !IsTextAreaDisplayed(card.description, card.root, camera)) return;
            if (subscribedActions == null) return;
            subscribedActions.ConfirmActionDisplayed(position, card.instanceKey);
            // This exact card has been exposed; an already-seen token must not be probed every frame.
            card.displayConfirmed = true;
        }
        private bool IsTextAreaDisplayed(TextMeshProUGUI label, RectTransform card, Camera camera)
        {
            if (label == null || !label.isActiveAndEnabled || string.IsNullOrWhiteSpace(label.text)
                || label.canvasRenderer.cull || label.color.a < .01f || EventSystem.current == null
                || !TryGetVisibleTextPoint(label.rectTransform, camera, out Vector2 point)) return false;
            displayPointer ??= new PointerEventData(EventSystem.current);
            displayPointer.position = point;
            displayHits.Clear();
            EventSystem.current.RaycastAll(displayPointer, displayHits);
            return displayHits.Count > 0 && displayHits[0].gameObject.transform.IsChildOf(card);
        }

        private bool TryGetVisibleTextPoint(RectTransform area, Camera camera, out Vector2 point)
        {
            point = default;
            if (scroll?.viewport == null || area == null) return false;
            RectTransform viewport = scroll.viewport;
            area.GetWorldCorners(cardCorners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector3 corner in cardCorners)
            {
                Vector2 local = viewport.InverseTransformPoint(corner);
                min = Vector2.Min(min, local); max = Vector2.Max(max, local);
            }
            Rect clip = viewport.rect;
            Vector2 exposedMin = Vector2.Max(min, clip.min), exposedMax = Vector2.Min(max, clip.max);
            Vector2 exposed = exposedMax - exposedMin;
            // Ignore a sliver at the mask edge; the point lies inside a meaningful readable area.
            if (exposed.x < Mathf.Min(64f, (max.x - min.x) * .5f)
                || exposed.y < Mathf.Min(24f, (max.y - min.y) * .5f) || exposed.x <= 0 || exposed.y <= 0) return false;
            point = RectTransformUtility.WorldToScreenPoint(camera, viewport.TransformPoint((exposedMin + exposedMax) * .5f));
            return true;
        }

        private void Rebuild()
        {
            if (!Application.isPlaying && showEditorPreview) { BuildEditorPreview(); return; }
            if (actionCardTemplate == null && content.Find("ActionCardTemplate") is RectTransform authoredTemplate) actionCardTemplate = authoredTemplate;
            if (actionCardTemplate != null) actionCardTemplate.gameObject.SetActive(false);
            for (int childIndex = content.childCount - 1; childIndex >= 0; childIndex--)
            {
                var child = content.GetChild(childIndex).gameObject;
                if (child == actionCardTemplate?.gameObject) continue;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            cards.Clear();
            var actions = NarrativeRuntime.Instance?.Actions;
            var values = actions?.GetDisplayableActions(position).ToList() ?? new List<NarrativeActionDefinition>();
            signature = actions != null ? ContentSignature(actions) : string.Empty;
            contentDirty = cardsDirty = false;
            float y = 0;
            foreach (var action in values)
            {
                if (actionCardTemplate == null) { Debug.LogError("MapActionDetailsView needs an authored ActionCard template.", this); break; }
                var root = Instantiate(actionCardTemplate, content, false);
                root.gameObject.name = "Action_" + action.actionKey;
                root.gameObject.SetActive(true);
                var card = BindActionCard(root, action);
                if (action.executionKind == NarrativeActionExecutionKind.Navigate)
                    BindNavigationAction(card);
                float height = Mathf.Max(1, root.rect.height);
                if (!IsAuthoredLayout) Top(root, 12, -y, CardWidth, height);
                y += height + cardSpacing;
                cards.Add(card); UpdateCard(card);
            }
            if (values.Count == 0 && actionCardTemplate != null)
            {
                // Never repurpose the authored template as an empty-state card: later actions
                // must clone the untouched Prefab artwork and serialized child structure.
                var emptyState = Instantiate(actionCardTemplate, content, false);
                emptyState.name = "EmptyActionState";
                emptyState.gameObject.SetActive(true);
                var name = emptyState.Find("Name")?.GetComponent<TextMeshProUGUI>();
                if (name != null) name.text = title != null ? title.text : "此时此地无事可做";
                var description = emptyState.Find("Description")?.GetComponent<TextMeshProUGUI>();
                if (description != null) description.text = "此时此地无事可做";
                var buttonLabel = emptyState.Find("Execute/Label")?.GetComponent<TextMeshProUGUI>();
                if (buttonLabel != null) buttonLabel.text = "暂无行动";
                if (!IsAuthoredLayout) Top(emptyState, 12, -y, CardWidth, Mathf.Max(1, emptyState.rect.height));
                var emptyButton = emptyState.GetComponentInChildren<Button>(true);
                if (emptyButton != null) emptyButton.gameObject.SetActive(false);
                y += Mathf.Max(414, emptyState.rect.height) + cardSpacing;
            }
            currentPage = Mathf.Clamp(currentPage, 0, Mathf.Max(0, cards.Count - 1));
            if (actionCardTemplate != null) actionCardTemplate.gameObject.SetActive(!Application.isPlaying && showEditorPreview);
            if (!IsAuthoredLayout)
            {
                content.sizeDelta = new Vector2(0, y);
                float maximumHeight = Mathf.Min(800, ((RectTransform)transform.parent).rect.height - 180);
                ((RectTransform)transform).sizeDelta = new Vector2(Width, Mathf.Clamp(popupSize.y, 500, Mathf.Max(500, maximumHeight)));
            }
            UpdateVisiblePage();
            Canvas.ForceUpdateCanvases();
        }
        private Vector2 GetRequiredPopupSize()
        {
            // Initialize calls this before content is bound. Resolve the exact authored child
            // here, and include artwork (notably ExecutePreview) outside its nominal root rect.
            if (transform.Find("Viewport/ActionList/ActionCardTemplate") is RectTransform template) actionCardTemplate = template;
            Vector2 size = actionCardTemplate != null
                ? (Vector2)MeasureCardBounds(actionCardTemplate).size
                : new Vector2(796, 414);
            return size + new Vector2(32, 134); // Existing viewport insets plus four pixels of card padding.
        }

        private Bounds MeasureCardBounds(RectTransform card)
        {
            // Templates stay inactive. Unity's default relative-bounds helper can omit
            // inactive descendants, including the overflowing authored execute artwork.
            Vector3 min = Vector3.one * float.PositiveInfinity, max = Vector3.one * float.NegativeInfinity;
            foreach (RectTransform child in card.GetComponentsInChildren<RectTransform>(true))
            {
                // Include inactive authored templates when measuring the template itself, but
                // exclude hidden optional regions from a live card's scroll bounds.
                if (card != actionCardTemplate && child != card && !child.gameObject.activeSelf) continue;
                child.GetWorldCorners(cardCorners);
                foreach (Vector3 corner in cardCorners)
                {
                    Vector3 local = card.InverseTransformPoint(corner);
                    min = Vector3.Min(min, local); max = Vector3.Max(max, local);
                }
            }
            var result = new Bounds(); result.SetMinMax(min, max); return result;
        }

        private void FitVisibleCard()
        {
            if (scroll?.viewport == null || content == null || currentPage >= cards.Count) return;
            Card card = cards[currentPage];
            if (card.root == null) return;
            Vector2 size = scroll.viewport.rect.size;
            if (size.x <= 8 || size.y <= 8 || (fittedCard == card.root && fittedViewportSize == size)) return;
            if (!card.boundsMeasured)
            {
                card.visualBounds = MeasureCardBounds(card.root);
                card.boundsMeasured = true;
            }
            Bounds bounds = card.visualBounds;
            // Fit width and scroll height so long Chinese requirements keep a readable size.
            float scale = Mathf.Min(1f, (size.x - 8) / Mathf.Max(1, bounds.size.x));
            bool sameCard = fittedCard == card.root;
            float scrollPosition = sameCard ? scroll.verticalNormalizedPosition : 1;
            float height = Mathf.Max(size.y, bounds.size.y * scale + 8);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0, height);
            scroll.enabled = true; scroll.horizontal = false; scroll.vertical = height > size.y + 1;
            scroll.scrollSensitivity = 32;
            card.root.anchorMin = card.root.anchorMax = new Vector2(0, 1);
            card.root.localScale = Vector3.one * scale;
            card.root.anchoredPosition = new Vector2(4 - bounds.min.x * scale, -4 - bounds.max.y * scale);
            fittedCard = card.root; fittedViewportSize = size;
            scroll.verticalNormalizedPosition = scrollPosition;
        }

        private void ConfigurePagingLayout()
        {
            if (scroll != null)
            {
                scroll.horizontal = false;
                scroll.vertical = false;
                scroll.inertia = false;
                scroll.enabled = false;
                var viewport = scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform;
                if (viewport != null)
                {
                    viewport.anchorMin = Vector2.zero;
                    viewport.anchorMax = Vector2.one;
                    viewport.offsetMin = new Vector2(12, 68);
                    viewport.offsetMax = new Vector2(-12, -58);
                }
            }

            var navigation = transform.Find("PageNavigation") as RectTransform;
            if (navigation == null)
            {
                navigation = Rect("PageNavigation", transform);
                navigation.anchorMin = new Vector2(.5f, 0);
                navigation.anchorMax = new Vector2(.5f, 0);
                navigation.pivot = new Vector2(.5f, 0);
                navigation.anchoredPosition = new Vector2(0, 12);
                navigation.sizeDelta = new Vector2(260, 44);
            }
            previousPageButton = navigation.Find("Previous")?.GetComponent<Button>();
            nextPageButton = navigation.Find("Next")?.GetComponent<Button>();
            pageLabel = navigation.Find("Page")?.GetComponent<TextMeshProUGUI>();
            if (previousPageButton == null)
            {
                previousPageButton = Button("Previous", navigation, "‹", () => ChangePage(-1));
                Top((RectTransform)previousPageButton.transform, 0, 0, 48, 44);
            }
            if (nextPageButton == null)
            {
                nextPageButton = Button("Next", navigation, "›", () => ChangePage(1));
                Top((RectTransform)nextPageButton.transform, 212, 0, 48, 44);
            }
            if (pageLabel == null)
            {
                pageLabel = Label("Page", navigation, "1 / 1", 20);
                Top(pageLabel.rectTransform, 56, 0, 148, 44);
                pageLabel.alignment = TextAlignmentOptions.Center;
            }
            UpdateVisiblePage();
        }

        private void ChangePage(int direction)
        {
            int pageCount = Mathf.Max(1, cards.Count);
            currentPage = (currentPage + direction + pageCount) % pageCount;
            UpdateVisiblePage();
        }

        private void UpdateVisiblePage()
        {
            int pageCount = Mathf.Max(1, cards.Count);
            pageChangedFrame = Time.frameCount;
            fittedCard = null;
            currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);
            for (int i = 0; i < cards.Count; i++)
                if (cards[i].root != null) cards[i].root.gameObject.SetActive(i == currentPage);
            if (pageLabel != null) pageLabel.text = (currentPage + 1) + " / " + pageCount;
            if (previousPageButton != null) previousPageButton.interactable = cards.Count > 1;
            if (nextPageButton != null) nextPageButton.interactable = cards.Count > 1;
            FitVisibleCard();
        }

        private Card BindActionCard(RectTransform root, NarrativeActionDefinition action)
        {
            var card = new Card { root = root, action = action, owner = NarrativeRuntime.Instance?.Actions,
                instanceKey = NarrativeRuntime.Instance?.Actions?.GetActionInstanceKey(position, action.actionKey) };
            card.type = root.Find("Type")?.GetComponent<TextMeshProUGUI>();
            var description = root.Find("Description")?.GetComponent<TextMeshProUGUI>();
            card.heading = root.Find("Name")?.GetComponent<TextMeshProUGUI>();
            card.description = description;
            var sampleRequirement = root.Find("Requirements/Requirement") ?? root.Find("Requirement");
            var execute = root.Find("Execute")?.GetComponent<Button>()
                ?? root.Find("ExecutePreview")?.GetComponent<Button>()
                ?? root.GetComponentInChildren<Button>(true);
            card.button = execute;
            if (description != null)
            {
                description.textWrappingMode = TextWrappingModes.Normal;
                description.text = Text(action.descText, "暂无行动描述");
            }
            // The authored preview caption has a fixed position; dynamic costs carry their own heading.
            var previewRequirementCaption = root.Find("需要shortcuts");
            if (previewRequirementCaption != null) previewRequirementCaption.gameObject.SetActive(false);
            if (execute != null)
            {
                execute.onClick.RemoveAllListeners();
                execute.onClick.AddListener(() => OnExecute(card));
                Spotlight.Audio.UIButtonAudio.Configure(execute, string.Empty);
            }
            if (card.type != null) card.type.text = action.executionKind == NarrativeActionExecutionKind.Navigate ? "地点导航" : action.type.ToString();
            card.requirementTemplate = sampleRequirement as RectTransform;
            if (sampleRequirement != null) sampleRequirement.gameObject.SetActive(false);
            if (action.executionKind == NarrativeActionExecutionKind.Exchange && description != null)
            {
                card.gains = Instantiate(description, root, false);
                card.gains.name = "ExchangeOutcome";
                card.gains.text = string.Empty;
                card.gains.gameObject.SetActive(false);
            }
            var progress = root.Find("ProgressTrack/Fill")?.GetComponent<Image>();
            card.progress = progress;
            card.repeats = root.Find("RepeatSegments") as RectTransform;
            var heading = root.Find("Name")?.GetComponent<TextMeshProUGUI>();
            if (heading != null) heading.text = Text(action.nameText, "未命名行动");
            return card;
        }
        private void BuildEditorPreview()
        {
            if (content == null || content.childCount > 0) return;
            if (title != null) title.text = editorPreviewLocation;
            // Missing authored cards are intentionally not synthesized here. Create them in Prefab Mode.
        }
        private void OnDisable()
        {
            executionEpoch++;
            UnbindActionNotifications();
            StopAllCoroutines(); busy = false;
            foreach (Transform child in transform.root)
                if (child.name == "ConsumptionEffect") Destroy(child.gameObject);
        }
        private void BindNavigationAction(Card card)
        {
            var requirement = card.root.Find("Requirements/Requirement") ?? card.root.Find("Requirement");
            if (requirement != null) requirement.gameObject.SetActive(false);
            if (card.button == null) return;
            card.button.onClick.RemoveAllListeners();
            card.button.onClick.AddListener(() => OnNavigate(card));
            Spotlight.Audio.UIButtonAudio.Configure(card.button);
            var label = card.button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = Text(card.action.executeTextKey, "进入咖啡馆");
        }

        private void UpdateCard(Card card)
        {
            var actions = NarrativeRuntime.Instance?.Actions;
            if (actions == null) return;
            if (card.action == null) return;
            if (card.action.executionKind == NarrativeActionExecutionKind.Navigate)
            {
                if (card.button != null) card.button.interactable = !busy && (controls?.Invoke() ?? true) && IsCurrentNavigation(card);
                return;
            }
            var action = card.action;
            bool available;
            try { available = actions.IsAvailable(action.actionKey, out _); }
            catch (Exception) { available = false; }
            string type = action.type switch
            {
                NarrativeActionType.Daily => "每天行动", NarrativeActionType.Once => "一次性行动",
                NarrativeActionType.Future => "未来行动", NarrativeActionType.Progress => "进度型行动",
                NarrativeActionType.Timed => "时限行动", _ => "可重复行动"
            };
            if (action.IsDelivery) type = (action.deliveryMode == NarrativeDeliveryMode.Consecutive ? "连续交付" : "累计交付") + " · 每天固定一份";
            if (action.IsTimed) type += " · 剩余 " + actions.GetRemainingDays(action.actionKey) + " 天";
            if (action.type == NarrativeActionType.Future) type += " · " + actions.GetRemainingDays(action.actionKey) + " 天后开放";
            if (action.IsProgress) type += " · 进度 " + actions.GetProgress(action.actionKey) + " / " + action.progressTarget;
            if (action.IsDelivery) type += actions.IsCompleted(action.actionKey) ? " · 已完成" : actions.HasSubmittedToday(action.actionKey) ? " · 今日已交付" : " · 今日未交付";
            if (action.mustExecute) type += " · 今日必做";
            if (card.type != null)
            {
                card.type.text = type;
                card.type.color = MapVisualTheme.Accent;
            }
            if (card.progress != null) card.progress.fillAmount = action.progressTarget > 0
                ? Mathf.Clamp01((float)actions.GetProgress(action.actionKey) / action.progressTarget) : 0;
            UpdateRepeatSegments(card, actions);
            UpdateRequirements(card);
            bool projectionReady = NarrativeRuntime.Instance?.Controller?.ValuesReady == true && card.projection.All(p => p.ready);
            if (!projectionReady && card.type != null) card.type.text += " · 暂不可用";
            string caption = actions.HasPendingExecution(action.actionKey) ? "继续未完成行动" :
                action.IsDelivery && actions.IsCompleted(action.actionKey) ? "已完成" :
                action.IsDelivery && actions.HasSubmittedToday(action.actionKey) ? "今日已交付" : action.endDay && actions.HasBlockingActions() ? "有需要进行的行动" :
                action.type == NarrativeActionType.Future ? "等待开放" : !projectionReady ? "暂不可用" : available
                    ? Text(action.executeTextKey, action.endDay ? "结束一天" : action.executionKind == NarrativeActionExecutionKind.Exchange ? "确认交易" : action.IsDelivery ? "交付" : "执行") : "查看需求";
            if (card.button != null)
            {
                var buttonLabel = card.button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (buttonLabel != null) buttonLabel.text = caption;
                // Preserve the authored button artwork and colors; only update interaction state.
                card.button.interactable = !busy && (controls?.Invoke() ?? true);
            }
        }
        private static void ConfigureExecuteLabel(TextMeshProUGUI label)
        {
            MapVisualTheme.Font(label, true);
            label.fontSize = 38; label.enableAutoSizing = true;
            label.fontSizeMin = 24; label.fontSizeMax = 38;
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform, 0);
            // The sword's text body excludes its pointed tip and right-hand hilt.
            label.rectTransform.offsetMin = new Vector2(24, 24);
            label.rectTransform.offsetMax = new Vector2(-58, -18);
        }

        private void UpdateRepeatSegments(Card card, NarrativeActionController actions)
        {
            // Repeat artwork is optional; never synthesize UI if the authored card omits it.
            if (card.repeats == null) return;
            int count = card.action.IsProgress ? Mathf.Max(0, card.action.progressTarget - actions.GetProgress(card.action.actionKey)) : int.MaxValue;
            if (card.action.IsDelivery)
            {
                try { count = actions.IsAvailable(card.action.actionKey, out _) ? 1 : 0; }
                catch (Exception) { count = 0; }
            }
            foreach (var costs in card.action.parsedRequirements
                .Where(r => r.type == NarrativeActionRequirementType.Item || r.type == NarrativeActionRequirementType.ActionPoint || r.type == NarrativeActionRequirementType.Money)
                .GroupBy(r => (r.type, key: r.type == NarrativeActionRequirementType.Item ? r.key : string.Empty)))
            {
                long cost = costs.Sum(r => (long)Mathf.Max(0, r.value));
                if (cost == 0) continue;
                int stock = costs.Key.type == NarrativeActionRequirementType.ActionPoint ? actions.ActionPoints.Current
                    : costs.Key.type == NarrativeActionRequirementType.Money ? (MapActionInventoryView.TryReadMoney(out int balance) ? balance : 0)
                    : NarrativeRuntime.Instance.MapInventory.GetCount(costs.Key.key);
                count = Mathf.Min(count, (int)(stock / cost));
            }
            if (count == card.repeatCount) return;
            card.repeatCount = count;
            for (int i = card.repeats.childCount - 1; i >= 0; i--)
            {
                var child = card.repeats.GetChild(i);
                child.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
            // Unbounded actions have no finite resource-derived partition count.
            if (count == int.MaxValue) return;
            // The repeat segments are authored. Runtime no longer constructs replacement art.
        }

        private void UpdateRequirements(Card card)
        {
            var runtime = NarrativeRuntime.Instance;
            card.projection.Clear();
            bool costsCommitted = HasCommittedPendingCosts(runtime?.Actions, card.action);
            foreach (var cost in (card.action.parsedRequirements ?? new List<NarrativeActionRequirement>())
                .GroupBy(r => (r.type, key: r.type == NarrativeActionRequirementType.Item ? r.key : string.Empty)))
            {
                long amount = cost.Sum(r => (long)r.value);
                long current = 0;
                bool ready = amount > 0 && amount <= int.MaxValue;
                string name;
                switch (cost.Key.type)
                {
                    case NarrativeActionRequirementType.Item:
                        name = MapActionInventoryView.ItemName(cost.Key.key);
                        ready &= runtime?.Controller?.ValuesReady == true && runtime.MapInventory != null;
                        current = runtime?.MapInventory?.GetCount(cost.Key.key) ?? 0;
                        break;
                    case NarrativeActionRequirementType.ActionPoint:
                        name = "行动点";
                        ready &= runtime?.Controller?.ValuesReady == true && runtime.Actions != null;
                        current = runtime?.Actions?.ActionPoints.Current ?? 0;
                        break;
                    case NarrativeActionRequirementType.Money:
                        name = "金币";
                        ready &= MapActionInventoryView.TryReadMoney(out int balance) && runtime?.Controller?.ValuesReady == true;
                        current = balance;
                        break;
                    default: name = "需求"; ready = false; break;
                }
                bool satisfied = costsCommitted || (ready && current >= amount);
                card.projection.Add(new RequirementProjection { costType = cost.Key.type, key = cost.Key.key,
                    amount = amount, ready = costsCommitted || ready, satisfied = satisfied,
                    text = costsCommitted ? "已支付 · " + name + " × " + amount :
                        (card.action.IsDelivery ? "每次交付 · " : "消耗 · ") + name + "\n"
                        + (ready ? current + " / " + amount + (satisfied ? " · 满足" : " · 不足") : "暂不可用") });
            }
            var diagnostics = runtime?.Controller?.ExplainConditions("actionExecute", card.action.actionKey);
            if (diagnostics != null && diagnostics.Count > 0)
            {
                var groups = diagnostics.GroupBy(d => d.GroupIndex).ToList();
                // Each alternative is a conjunction. Keep every alternative in one eligibility
                // slot so neither the per-slot state nor its heading suggests OR means AND.
                bool ready = runtime.Controller.ValuesReady && diagnostics.All(d => string.IsNullOrEmpty(d.Error)
                    && !string.IsNullOrEmpty(d.Current) && d.Current != "不可用");
                bool satisfied = groups.Any(g => g.All(d => d.Passed && string.IsNullOrEmpty(d.Error)));
                string alternatives = string.Join("\n或\n", groups.Select(g => "（" + string.Join("\n且 ", g.Select(ConditionText)) + "）"));
                card.projection.Add(new RequirementProjection { ready = ready, satisfied = satisfied,
                    text = "资格 · " + (groups.Count > 1 ? "任一组满足" : "全部满足")
                        + " · " + (!ready ? "暂不可用" : satisfied ? "满足" : "未满足") + "\n" + alternatives });
            }
            // The loader rejects more than six display entries. Fail visibly for a malformed
            // injected snapshot as well; never silently omit a seventh payable requirement.
            if (card.projection.Count > 6)
            {
                card.projection.Clear();
                card.projection.Add(new RequirementProjection { ready = false, text = "需求暂不可用" });
            }
            if (card.requirementTemplate == null && card.projection.Count > 0)
            {
                foreach (var entry in card.projection) entry.ready = false;
                return;
            }
            while (card.requirementSlots.Count < card.projection.Count)
            {
                var slot = Instantiate(card.requirementTemplate, card.root, false);
                slot.name = "Requirement_" + card.requirementSlots.Count;
                card.requirementSlots.Add(slot);
                var label = slot.GetComponentInChildren<TextMeshProUGUI>(true);
                card.requirements.Add(label);
                if (label != null)
                {
                    label.enableAutoSizing = false; label.fontSize = 20;
                    label.textWrappingMode = TextWrappingModes.Normal;
                    label.alignment = TextAlignmentOptions.TopLeft;
                    label.overflowMode = TextOverflowModes.Overflow;
                    Stretch(label.rectTransform, 10);
                }
            }
            for (int i = 0; i < card.requirementSlots.Count; i++)
            {
                bool active = i < card.projection.Count;
                card.requirementSlots[i].gameObject.SetActive(active);
                if (!active || card.requirements[i] == null) continue;
                card.requirements[i].text = card.projection[i].text;
                card.requirements[i].color = card.projection[i].satisfied ? MapVisualTheme.Ink : new Color(.65f, .23f, .14f);
            }
            string gains = ExchangeOutcome(card.action);
            if (card.gains != null)
            {
                card.gains.text = gains;
                card.gains.gameObject.SetActive(!string.IsNullOrEmpty(gains));
            }
            string layout = string.Join("|", card.projection.Select(p => p.text)) + gains;
            if (layout == card.layoutSignature) return;
            card.layoutSignature = layout;
            LayoutRequirements(card);
        }

        private string ConditionText(NarrativeConditionDiagnostic diagnostic)
        {
            var config = NarrativeRuntime.Instance?.Configuration;
            var definition = config?.conditions.Find(c => c != null && c.conditionId == diagnostic.ConditionId);
            string key = diagnostic.Target ?? string.Empty;
            foreach (string prefix in new[] { "bar:", "rel:", "event:", "item:", "action:" })
                if (key.StartsWith(prefix, StringComparison.Ordinal)) { key = key.Substring(prefix.Length); break; }
            string type = diagnostic.Type?.Trim().ToUpperInvariant();
            string name;
            switch (type)
            {
                case "ITEM_GTE": name = MapActionInventoryView.ItemName(key); break;
                case "ATTRIBUTE_GTE":
                    name = config != null && config.abilityNameKeys.TryGetValue(key, out string nameKey) ? Text(nameKey, "属性") : "属性";
                    break;
                case "BOOL_EQ": name = Text(config?.conditionKeys.Find(c => c != null && c.key == key)?.nameText, "状态条件"); break;
                case "EVENT_HAPPENED": case "EVENT_NOT_HAPPENED":
                    name = Text(config?.events.Find(e => e != null && e.eventId == key)?.titleText, "前置事件"); break;
                case "ACTION_PROGRESS_GTE": case "ACTION_COMPLETED":
                    name = Text(config?.actions.Find(a => a != null && a.actionKey == key)?.nameText, "前置行动"); break;
                default: name = Text(config?.bars.Find(b => b != null && b.barKey == key)?.nameText, "属性条件"); break;
            }
            if (!string.IsNullOrEmpty(diagnostic.Error) || definition == null) return name + " · 暂不可用";
            string current = diagnostic.Current;
            string required = diagnostic.Required;
            string op = type == "BAR_LTE" ? "≤ " : type == "BAR_BETWEEN" ? "范围 " : "≥ ";
            if (type == "BOOL_EQ") op = "= ";
            if (type == "ACTION_COMPLETED" || type == "EVENT_HAPPENED" || type == "EVENT_NOT_HAPPENED")
            {
                op = ""; required = type == "EVENT_NOT_HAPPENED" ? "未发生" : type == "EVENT_HAPPENED" ? "已发生" : "已完成";
            }
            if (bool.TryParse(current, out bool flag)) current = flag ? "是" : "否";
            if (bool.TryParse(required, out bool expected)) required = expected ? "是" : "否";
            if (string.IsNullOrEmpty(current) || current == "不可用") return name + " · 暂不可用";
            // Numeric thresholds and known booleans are safe display values; arbitrary config
            // identifiers never become a fallback label in the player-facing card.
            if (type != "BOOL_EQ" && type != "ACTION_COMPLETED" && type != "EVENT_HAPPENED" && type != "EVENT_NOT_HAPPENED"
                && !(required ?? string.Empty).All(c => char.IsDigit(c) || char.IsWhiteSpace(c) || ".,+-".Contains(c)))
                return name + " · 暂不可用";
            return name + " " + current + " / " + op + required + (diagnostic.Passed ? " · 满足" : " · 未满足");
        }

        private string ExchangeOutcome(NarrativeActionDefinition action)
        {
            if (action.executionKind != NarrativeActionExecutionKind.Exchange) return string.Empty;
            var controller = NarrativeRuntime.Instance?.Controller;
            if (controller == null) return "获得 · 暂不可用";
            var results = controller.GetActionResults(action.successResultOwnerId);
            if (results.Count == 0) return "获得 · 暂不可用";
            var lines = new List<string>();
            try
            {
                foreach (var group in results.Where(r => controller.EvaluateConditions("result", r.resultId))
                    .GroupBy(r => (r.resultType, r.target)))
                {
                    long amount = 0;
                    foreach (var result in group)
                    {
                        if (!int.TryParse(result.value, out int value) || value <= 0) return "获得 · 暂不可用";
                        amount += value;
                    }
                    if (amount > int.MaxValue) return "获得 · 暂不可用";
                    switch (group.Key.resultType)
                    {
                        case NarrativeResultType.ItemAdd: lines.Add("获得 · " + MapActionInventoryView.ItemName(group.Key.target) + " × " + amount); break;
                        case NarrativeResultType.MoneyAdd: lines.Add("获得 · 金币 + " + amount); break;
                        case NarrativeResultType.ItemRemove: lines.Add("另消耗 · " + MapActionInventoryView.ItemName(group.Key.target) + " × " + amount); break;
                        case NarrativeResultType.MoneyRemove: lines.Add("另消耗 · 金币 − " + amount); break;
                        default: return "获得 · 暂不可用";
                    }
                }
            }
            catch (Exception) { return "获得 · 暂不可用"; }
            return lines.Count > 0 ? string.Join("\n", lines) : "当前无可获得内容";
        }

        private static float PreferredHeight(TextMeshProUGUI label, string text, float width)
        {
            // Inactive/headless authored templates can have no TMP material until first render.
            // Estimate wrapped Chinese lines until the font is ready instead of forcing a mesh.
            if (label.font != null && label.fontSharedMaterial != null)
            {
                try { return label.GetPreferredValues(text, width, 0).y; }
                catch (NullReferenceException) { /* A font asset can also be awaiting glyph-table initialization. */ }
            }
            float fontSize = Mathf.Max(1, label.fontSize);
            int columns = Mathf.Max(1, Mathf.FloorToInt(width / fontSize));
            int lines = (text ?? string.Empty).Split('\n').Sum(line => Mathf.Max(1, Mathf.CeilToInt((float)line.Length / columns)));
            return lines * fontSize * 1.4f;
        }

        private void LayoutRequirements(Card card)
        {
            float width = Mathf.Max(320, card.root.rect.width);
            float availableWidth = width - 72;
            float top = 148;
            if (card.description != null)
            {
                card.description.enableAutoSizing = false;
                card.description.fontSize = 22;
                float height = Mathf.Max(64, PreferredHeight(card.description, card.description.text, availableWidth) + 12);
                Top(card.description.rectTransform, 36, -top, availableWidth, height);
                top += height + 16;
            }
            float slotWidth = (availableWidth - 12) / 2;
            for (int i = 0; i < card.projection.Count; i += 2)
            {
                float height = 84;
                for (int j = i; j < Mathf.Min(i + 2, card.projection.Count); j++)
                    if (card.requirements[j] != null) height = Mathf.Max(height, PreferredHeight(card.requirements[j], card.projection[j].text, slotWidth - 20) + 24);
                for (int j = i; j < Mathf.Min(i + 2, card.projection.Count); j++)
                    Top(card.requirementSlots[j], 36 + (j % 2) * (slotWidth + 12), -top, slotWidth, height);
                top += height + 12;
            }
            if (card.gains != null && card.gains.gameObject.activeSelf)
            {
                card.gains.enableAutoSizing = false; card.gains.fontSize = 22;
                float height = PreferredHeight(card.gains, card.gains.text, availableWidth) + 16;
                Top(card.gains.rectTransform, 36, -top, availableWidth, height);
                top += height + 12;
            }
            if (card.button != null)
            {
                var button = (RectTransform)card.button.transform;
                Top(button, Mathf.Max(36, width - button.rect.width), -top, button.rect.width, button.rect.height);
                top += button.rect.height;
            }
            card.root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(414, top + 16));
            card.boundsMeasured = false;
            if (fittedCard == card.root) fittedViewportSize = Vector2.zero;
        }
        private bool IsCurrentNavigation(Card card)
        {
            var actions = NarrativeRuntime.Instance?.Actions;
            if (card?.action == null || actions == null || card.owner != actions || !cards.Contains(card) || !IsCafeEntryPosition(position)
                || card.action.executionKind != NarrativeActionExecutionKind.Navigate
                || card.action.navigationKey != "cafe.enter" || string.IsNullOrEmpty(card.instanceKey)) return false;
            return actions.GetDisplayableActions(position).Any(action => action.actionKey == card.action.actionKey
                && action.executionKind == NarrativeActionExecutionKind.Navigate
                && action.navigationKey == "cafe.enter"
                && actions.GetActionInstanceKey(position, action.actionKey) == card.instanceKey);
        }

        private void OnNavigate(Card card)
        {
            if (busy || !(controls?.Invoke() ?? true)) return;
            if (!IsCurrentNavigation(card))
            {
                NotifyNavigationFailure("该导航已失效，请重新打开地点详情。");
                return;
            }
            var actions = NarrativeRuntime.Instance?.Actions;
            if (actions == null || !actions.IsAvailable(card.action.actionKey, out _))
            {
                NotifyNavigationFailure("进入条件尚未满足或当前有其他行动进行中，请稍后重试。");
                return;
            }
            if (tryEnterCafe == null)
            {
                NotifyNavigationFailure("咖啡馆入口暂不可用，请稍后重试。");
                return;
            }
            busy = true;
            UpdateCards();
            try
            {
                if (!tryEnterCafe()) NotifyNavigationFailure("无法进入咖啡馆，请稍后重试。");
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Map] Cafe navigation rejected: " + error.Message, this);
                NotifyNavigationFailure("咖啡馆加载失败，请稍后重试。");
            }
        }

        private void UpdateCards()
        {
            foreach (var card in cards) UpdateCard(card);
        }

        private void OnExecute(Card card)
        {
            if (busy || !(controls?.Invoke() ?? true)) return;
            var actions = NarrativeRuntime.Instance?.Actions;
            if (actions == null) return;
            UpdateRequirements(card);
            if (NarrativeRuntime.Instance?.Controller?.ValuesReady != true
                || (!actions.HasPendingExecution(card.action.actionKey) && card.projection.Any(p => !p.ready)))
            {
                Notice("需求数据暂不可用，请稍后重试。");
                return;
            }
            bool available;
            List<NarrativeActionCheck> checks;
            try { available = actions.IsAvailable(card.action.actionKey, out checks); }
            catch (Exception) { Notice("需求数据暂不可用，请稍后重试。"); return; }
            if (!available)
            {
                Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Error);
                Notice(actions.HasPendingExecution(card.action.actionKey) ? "未完成行动暂时无法继续，请稍后重试。" :
                    card.action.IsDelivery && actions.IsCompleted(card.action.actionKey) ? "交付已完成" :
                    card.action.IsDelivery && actions.HasSubmittedToday(card.action.actionKey) ? "今日已交付，每天只能提交一份" :
                    card.action.endDay && actions.HasBlockingActions() ? "有需要进行的行动" :
                    checks.Any(c => c.messageKey == "action.condition.missing") ? "行动前置条件不满足" : "所需物品或行动点不足");
                for (int i = 0; i < card.projection.Count && i < card.requirementSlots.Count; i++)
                    if (!card.projection[i].satisfied) StartCoroutine(Shake(card.requirementSlots[i]));
                if (checks.All(c => c.satisfied)) StartCoroutine(Shake((RectTransform)card.button.transform));
                return;
            }
            StartCoroutine(Execute(card));
        }
        private static bool HasCommittedPendingCosts(NarrativeActionController actions, NarrativeActionDefinition action)
        {
            if (actions == null || !actions.HasPendingExecution(action.actionKey)) return false;
            if (action.IsDelivery) return true;
            var snapshot = new NarrativeActionStateSnapshot();
            actions.CaptureInto(snapshot);
            return snapshot.rootCostCommitted;
        }

        private IEnumerator AnimateCosts(Card card)
        {
            // A pending cursor may already have paid at an earlier event's commit.
            if (HasCommittedPendingCosts(NarrativeRuntime.Instance?.Actions, card.action)) yield break;
            for (int i = 0; i < card.projection.Count && i < card.requirementSlots.Count; i++)
            {
                var requirement = card.projection[i];
                if (inventory == null || !requirement.costType.HasValue) continue;
                var target = card.requirementSlots[i];
                if (requirement.costType == NarrativeActionRequirementType.Item)
                {
                    yield return inventory.Reveal(requirement.key);
                    yield return Fly(inventory.FindSlot(requirement.key), target, MapActionInventoryView.ItemName(requirement.key) + "\n−" + requirement.amount);
                }
                else if (requirement.costType == NarrativeActionRequirementType.Money)
                {
                    yield return inventory.RevealMoney();
                    yield return Fly(inventory.MoneyRect, target, "金币\n−" + requirement.amount);
                }
                else if (requirement.costType == NarrativeActionRequirementType.ActionPoint)
                    yield return Fly(inventory.PointsRect, target, "行动点\n−" + requirement.amount);
            }
        }

        private IEnumerator Execute(Card card)
        {
            busy = true;
            int epoch = ++executionEpoch;
            foreach (var c in cards) if (c.button != null) c.button.interactable = false;
            yield return AnimateCosts(card);
            if (this == null || epoch != executionEpoch || !isActiveAndEnabled) yield break;
            if (!(controls?.Invoke() ?? true)) { busy = false; yield break; }
            var runtime = NarrativeRuntime.Instance;
            var actions = runtime?.Actions;
            if (actions == null || card.owner != actions) { busy = false; yield break; }
            bool exchange = card.action.executionKind == NarrativeActionExecutionKind.Exchange;
            // Capture immediately before Execute, using the same authoritative providers afterwards.
            var itemInventory = runtime.MapInventory;
            var moneyAccount = runtime.Controller?.MoneyAccount;
            var itemsBefore = exchange && itemInventory != null
                ? itemInventory.Items.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal) : null;
            int moneyBefore = 0;
            bool moneyReady = exchange && moneyAccount != null && moneyAccount.TryRead(out moneyBefore);
            int pointsBefore = actions.ActionPoints.Current;
            bool finished = false;
            bool accepted = actions.Execute(card.action.actionKey, success =>
            {
                if (this == null || epoch != executionEpoch || !isActiveAndEnabled) return;
                Spotlight.Audio.GameAudio.Play(success ? Spotlight.Audio.AudioKeys.MapComplete : Spotlight.Audio.AudioKeys.Error);
                finished = true;
                if (success && exchange)
                {
                    var changes = new List<string>();
                    if (moneyReady && moneyAccount.TryRead(out int moneyAfter))
                        AddActualChange(changes, "金币", (long)moneyAfter - moneyBefore);
                    else if (moneyAccount != null) changes.Add("金币变化暂不可用");
                    if (itemsBefore != null)
                        foreach (string key in itemsBefore.Keys.Union(itemInventory.Items.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                        {
                            itemsBefore.TryGetValue(key, out int before);
                            AddActualChange(changes, MapActionInventoryView.ItemName(key), (long)itemInventory.GetCount(key) - before);
                        }
                    else changes.Add("物品变化暂不可用");
                    AddActualChange(changes, "行动点", (long)actions.ActionPoints.Current - pointsBefore);
                    Notice("交易成功 · 实际变化\n" + (changes.Count > 0 ? string.Join("\n", changes) : "物品、金币与行动点数量无变化"), 6);
                }
                if (success && (controls?.Invoke() ?? true) && card.action.endDay) { busy = false; endDay?.Invoke(); }
                if (!success) Notice("行动事件未能执行");
            });
            if (!accepted && !finished) { Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Error); Notice("要求不满足"); finished = true; }
            while (!finished) yield return null;
            if (this == null || epoch != executionEpoch || !isActiveAndEnabled) yield break;
            busy = false; inventory?.Refresh(); Rebuild();
        }
        private static void AddActualChange(List<string> changes, string name, long delta)
        {
            if (delta != 0) changes.Add(name + (delta > 0 ? " 增加 " : " 减少 ") + Math.Abs(delta));
        }

        private IEnumerator Fly(RectTransform source, RectTransform target, string text)
        {
            if (source == null || target == null) yield break;
            var effect = Rect("ConsumptionEffect", transform.root);
            effect.sizeDelta = new Vector2(124, 70);
            var background = effect.gameObject.AddComponent<Image>();
            background.color = new Color(.12f, .32f, .22f, .98f); background.raycastTarget = false;
            var label = Label("Label", effect, text, 20);
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(.82f, 1, .7f);
            Stretch(label.rectTransform, 4);
            Vector3 start = source.position, end = target.position;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / .45f)
            {
                effect.position = Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 65;
                effect.localScale = Vector3.one * Mathf.Lerp(1, .65f, t);
                yield return null;
            }
            Destroy(effect.gameObject);
            var image = target.GetComponent<Image>();
            if (image == null) yield break;
            Color original = image.color; image.color = new Color(.23f, .48f, .3f);
            yield return new WaitForSecondsRealtime(.12f);
            if (image != null) image.color = original;
        }
        private IEnumerator Shake(RectTransform target)
        {
            if (target == null) yield break;
            Vector2 origin = target.anchoredPosition;
            for (float t = 0; t < .35f; t += Time.unscaledDeltaTime)
            {
                if (target == null || !target.gameObject.activeInHierarchy) yield break;
                target.anchoredPosition = origin + Vector2.right * Mathf.Sin(t * 80) * 8 * (1 - t / .35f);
                yield return null;
            }
            if (target != null) target.anchoredPosition = origin;
        }
        private void Notice(string message, float duration = 2)
        {
            var root = Rect("RequirementNotice", transform.root);
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
            root.gameObject.AddComponent<Image>().color = new Color(.16f, .07f, .07f, .98f);
            var label = Label("Message", root, message, 24); Stretch(label.rectTransform, 16);
            label.alignment = TextAlignmentOptions.Center;
            root.sizeDelta = new Vector2(380, Mathf.Max(120, PreferredHeight(label, message, 348) + 32));
            if (Application.isPlaying) Destroy(root.gameObject, duration);
        }
        internal static string Text(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;
            string value = NarrativeRuntime.Instance?.ResolveText(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }
        internal static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform;
        }
        internal static void Top(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
        }
        internal static void Stretch(RectTransform rect, float margin)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin); rect.offsetMax = new Vector2(-margin, -margin);
        }
        internal static TextMeshProUGUI Label(string name, Transform parent, string text, float size)
        {
            var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Resources.Load<TMP_FontAsset>("Fonts/CactusClassicalSerif SDF") ?? TMP_Settings.defaultFontAsset;
            label.text = text; label.fontSize = size; MapVisualTheme.Font(label); label.raycastTarget = false; return label;
        }
        internal static Button Button(string name, Transform parent, string text, UnityEngine.Events.UnityAction click)
        {
            var rect = Rect(name, parent); var image = rect.gameObject.AddComponent<Image>();
            MapVisualTheme.Skin(image, MapVisualTheme.Current?.tracking);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var label = Label("Label", rect, text, 20); Stretch(label.rectTransform, 4); label.alignment = TextAlignmentOptions.Center;
            button.onClick.AddListener(click);
            Spotlight.Audio.UIButtonAudio.Configure(button);
            return button;
        }
    }

}
