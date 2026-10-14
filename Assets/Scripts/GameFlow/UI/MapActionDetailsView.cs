using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>Scrollable action cards. Unavailable buttons remain clickable to explain rejected requirements.</summary>
    public sealed class MapActionDetailsView : MonoBehaviour
    {
        /// <summary>Only these locations expose the cafe navigation card instead of an NCS action card.</summary>
        internal const string CafePositionKey = "cafe";
        private const string CafeLobbyPositionKey = "大堂区";
        private const string CafeEntryNodeName = "EnterCafe";
        private const string CafeEntryLabel = "进入咖啡馆";
        private const string CafeEntryDescription = "推门进入咖啡馆，开始今天的经营。";
        /// <summary>The cafe (and its authored lobby alias) navigates to the cafe scene instead of running an NCS action.</summary>
        internal static bool IsCafeEntryPosition(string positionKey)
            => positionKey == CafePositionKey || positionKey == CafeLobbyPositionKey;
        private RectTransform content;
        private TextMeshProUGUI title;
        private MapSceneController mapController;
        private MapActionInventoryView inventory;
        private string position;
        private Func<bool> controls;
        private Action endDay, enterCafe;
        private bool busy;
        public bool IsBusy => busy;
        private float nextRefresh;
        private string signature;
        private ScrollRect scroll;
        private Button previousPageButton;
        private Button nextPageButton;
        private TextMeshProUGUI pageLabel;
        private int currentPage;
        private float revealStarted = float.NegativeInfinity;
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
            public Button button;
            public TextMeshProUGUI type;
            public Image progress;
            public RectTransform repeats;
            public int repeatCount = -1;
            public readonly List<TextMeshProUGUI> requirements = new List<TextMeshProUGUI>();
        }
        public void Initialize(MapActionInventoryView inventoryView, Func<bool> canControl, Action finishDay, Action cafe)
        {
            inventory = inventoryView; controls = canControl; endDay = finishDay; enterCafe = cafe;
            mapController = GetComponentInParent<MapSceneController>();
            var rect = (RectTransform)transform;
            bool authoredLayout = transform.Find("Viewport/ActionList") != null;
            popupSize.y = Mathf.Max(popupSize.y, GetRequiredPopupHeight());
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
        public void Show(string positionKey, string locationName)
        {
            if (busy) return;
            if (!Application.isPlaying && showEditorPreview) { gameObject.SetActive(true); return; }
            bool changed = position != positionKey;
            position = positionKey; title.text = locationName;
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
        private void Update()
        {
            if (busy || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .2f;
            var actions = NarrativeRuntime.Instance?.Actions;
            if (actions == null) return;
            string next = string.Join("|", actions.GetMountedActions(position).Select(a => a.actionKey));
            if (next != signature) Rebuild(); else foreach (var card in cards) UpdateCard(card);
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
            var values = actions?.GetMountedActions(position).ToList() ?? new List<NarrativeActionDefinition>();
            signature = string.Join("|", values.Select(a => a.actionKey));
            float y = 0;
            foreach (var action in values)
            {
                if (actionCardTemplate == null) { Debug.LogError("MapActionDetailsView needs an authored ActionCard template.", this); break; }
                var root = Instantiate(actionCardTemplate, content, false);
                root.gameObject.name = "Action_" + action.actionKey;
                root.gameObject.SetActive(true);
                var card = BindActionCard(root, action);
                float height = Mathf.Max(1, root.rect.height);
                if (!IsAuthoredLayout) Top(root, 12, -y, CardWidth, height);
                y += height + cardSpacing;
                cards.Add(card); UpdateCard(card);
            }
            if (IsCafeEntryPosition(position) && actionCardTemplate != null
                && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "CoffeeShop")
            {
                // Entering the cafe is scene navigation, not an NCS action: it never checks or
                // spends action points, items or action results. Reuse the authored card artwork.
                var entry = Instantiate(actionCardTemplate, content, false);
                entry.gameObject.name = CafeEntryNodeName;
                entry.gameObject.SetActive(true);
                var heading = entry.Find("Name")?.GetComponent<TextMeshProUGUI>();
                if (heading != null) heading.text = title != null ? title.text : CafeEntryLabel;
                var description = entry.Find("Description")?.GetComponent<TextMeshProUGUI>();
                if (description != null) description.text = CafeEntryDescription;
                // Navigation costs nothing, so the authored requirement slot must stay hidden.
                var requirement = entry.Find("Requirements/Requirement") ?? entry.Find("Requirement");
                if (requirement != null) requirement.gameObject.SetActive(false);
                var execute = entry.Find("Execute")?.GetComponent<Button>()
                    ?? entry.Find("ExecutePreview")?.GetComponent<Button>()
                    ?? entry.GetComponentInChildren<Button>(true);
                if (execute == null)
                {
                    Debug.LogError("MapActionDetailsView action card template needs an authored execute button.", this);
                    if (Application.isPlaying) Destroy(entry.gameObject); else DestroyImmediate(entry.gameObject);
                }
                else
                {
                    execute.gameObject.SetActive(true);
                    execute.onClick.RemoveAllListeners();
                    execute.onClick.AddListener(OnEnterCafe);
                    var label = execute.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (label != null) label.text = CafeEntryLabel;
                    var card = new Card { root = entry, button = execute };
                    if (!IsAuthoredLayout) Top(entry, 12, -y, CardWidth, Mathf.Max(1, entry.rect.height));
                    y += Mathf.Max(414, entry.rect.height) + cardSpacing;
                    cards.Add(card);
                }
            }
            else if (values.Count == 0 && actionCardTemplate != null)
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
        private float GetRequiredPopupHeight()
        {
            if (actionCardTemplate == null && content != null && content.Find("ActionCardTemplate") is RectTransform template)
                actionCardTemplate = template;
            float cardHeight = actionCardTemplate != null ? actionCardTemplate.rect.height : 414f;
            return cardHeight + 126f;
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
            currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);
            for (int i = 0; i < cards.Count; i++)
                if (cards[i].root != null) cards[i].root.gameObject.SetActive(i == currentPage);
            if (pageLabel != null) pageLabel.text = (currentPage + 1) + " / " + pageCount;
            if (previousPageButton != null) previousPageButton.interactable = cards.Count > 1;
            if (nextPageButton != null) nextPageButton.interactable = cards.Count > 1;
        }

        private Card BindActionCard(RectTransform root, NarrativeActionDefinition action)
        {
            var card = new Card { root = root, action = action };
            card.type = root.Find("Type")?.GetComponent<TextMeshProUGUI>();
            var description = root.Find("Description")?.GetComponent<TextMeshProUGUI>();
            var sampleRequirement = root.Find("Requirements/Requirement") ?? root.Find("Requirement");
            var execute = root.Find("Execute")?.GetComponent<Button>()
                ?? root.Find("ExecutePreview")?.GetComponent<Button>()
                ?? root.GetComponentInChildren<Button>(true);
            card.button = execute;
            if (description != null) description.text = Text(action.descText, string.IsNullOrEmpty(action.descText) ? "暂无行动描述" : action.descText);
            if (execute != null)
            {
                execute.onClick.RemoveAllListeners();
                execute.onClick.AddListener(() => OnExecute(card));
            }
            var requirements = action.parsedRequirements;
            if (card.type != null) card.type.text = action.type.ToString();
            if (sampleRequirement != null)
            {
                // Use the authored requirement slot. Duplicate or synthesized slot UI is not
                // allowed: art must author the required number of slots explicitly.
                if (requirements.Count > 0)
                {
                    sampleRequirement.gameObject.SetActive(true);
                    var label = sampleRequirement.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (label != null) card.requirements.Add(label);
                }
                else sampleRequirement.gameObject.SetActive(false);
            }
            var progress = root.Find("ProgressTrack/Fill")?.GetComponent<Image>();
            card.progress = progress;
            card.repeats = root.Find("RepeatSegments") as RectTransform;
            var heading = root.Find("Name")?.GetComponent<TextMeshProUGUI>();
            if (heading != null) heading.text = Text(action.nameText, action.actionKey);
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
            StopAllCoroutines(); busy = false;
            foreach (Transform child in transform.root)
                if (child.name == "ConsumptionEffect") Destroy(child.gameObject);
        }
        private void UpdateCard(Card card)
        {
            var actions = NarrativeRuntime.Instance?.Actions;
            if (actions == null) return;
            // The cafe navigation card carries no NCS action and needs no per-frame refresh.
            if (card.action == null) return;
            var action = card.action;
            bool available = actions.IsAvailable(action.actionKey, out var checks);
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
            if (card.requirements.Count > 0)
            {
                card.requirements[0].text = (action.IsDelivery ? "每次交付\n" : string.Empty) + string.Join("\n", action.parsedRequirements.Select(RequirementName));
                bool allSatisfied = checks.Count >= action.parsedRequirements.Count && checks.All(check => check.satisfied);
                card.requirements[0].color = allSatisfied ? MapVisualTheme.Ink : new Color(.65f, .23f, .14f);
            }
            string caption = action.IsDelivery && actions.IsCompleted(action.actionKey) ? "已完成" :
                action.IsDelivery && actions.HasSubmittedToday(action.actionKey) ? "今日已交付" : action.endDay && actions.HasBlockingActions() ? "有需要进行的行动" :
                action.type == NarrativeActionType.Future ? "等待开放" : available ? (action.endDay ? "结束一天" : "执行") : "查看需求";
            if (card.button != null)
            {
                var buttonLabel = card.button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (buttonLabel != null) buttonLabel.text = caption;
                // Preserve the authored button artwork and colors; only update interaction state.
                card.button.interactable = !busy;
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
            int count = card.action.IsDelivery ? (actions.IsAvailable(card.action.actionKey, out _) ? 1 : 0) : card.action.IsProgress ? Mathf.Max(0, card.action.progressTarget - actions.GetProgress(card.action.actionKey)) : int.MaxValue;
            foreach (var costs in card.action.parsedRequirements
                .Where(r => r.type == NarrativeActionRequirementType.Item || r.type == NarrativeActionRequirementType.ActionPoint)
                .GroupBy(r => (r.type, r.key)))
            {
                long cost = costs.Sum(r => (long)Mathf.Max(0, r.value));
                if (cost == 0) continue;
                int stock = costs.Key.type == NarrativeActionRequirementType.ActionPoint ? actions.ActionPoints.Current : NarrativeRuntime.Instance.MapInventory.GetCount(costs.Key.key);
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

        private string RequirementName(NarrativeActionRequirement r) => r.type switch
        {
            NarrativeActionRequirementType.Item => Text("item." + r.key + ".name", r.key) + "\n× " + r.value,
            NarrativeActionRequirementType.Bar => Text("bar." + r.key + ".name", r.key) + "\n" + r.op + " " + r.value,
            NarrativeActionRequirementType.Condition => "拥有条件：" + Text(r.key, r.key),
            NarrativeActionRequirementType.NotCondition => "不能拥有：" + Text(r.key, r.key),
            _ => "行动点\n× " + r.value
        };
        private void OnEnterCafe()
        {
            if (busy || !(controls?.Invoke() ?? true)) return;
            // Reuse the map's existing cafe scene entry; no NCS action is executed here.
            enterCafe?.Invoke();
        }

        private void OnExecute(Card card)
        {
            if (busy || !(controls?.Invoke() ?? true)) return;
            var actions = NarrativeRuntime.Instance?.Actions;
            if (actions == null) return;
            if (!actions.IsAvailable(card.action.actionKey, out var checks))
            {
                Notice(card.action.IsDelivery && actions.IsCompleted(card.action.actionKey) ? "交付已完成" :
                    card.action.IsDelivery && actions.HasSubmittedToday(card.action.actionKey) ? "今日已交付，每天只能提交一份" :
                    card.action.endDay && actions.HasBlockingActions() ? "有需要进行的行动" :
                    checks.Any(c => c.messageKey == "action.condition.missing") ? "行动前置条件不满足" : "所需物品或行动点不足");
                if (card.requirements.Count > 0 && checks.Any(check => !check.satisfied))
                    StartCoroutine(Shake((RectTransform)card.requirements[0].transform.parent));
                if (checks.All(c => c.satisfied)) StartCoroutine(Shake((RectTransform)card.button.transform));
                return;
            }
            StartCoroutine(Execute(card));
        }
        private IEnumerator Execute(Card card)
        {
            busy = true;
            foreach (var c in cards) c.button.interactable = false;
            // Show the costs first; the controller rechecks and consumes them atomically afterwards.
            for (int i = 0; i < card.action.parsedRequirements.Count; i++)
            {
                var requirement = card.action.parsedRequirements[i];
                if (requirement.type == NarrativeActionRequirementType.Item)
                {
                    yield return inventory.Reveal(requirement.key);
                    if (card.requirements.Count > 0)
                        yield return Fly(inventory.FindSlot(requirement.key), (RectTransform)card.requirements[0].transform.parent, "×" + requirement.value);
                }
                else if (requirement.type == NarrativeActionRequirementType.ActionPoint)
                    if (card.requirements.Count > 0)
                        yield return Fly(inventory.PointsRect, (RectTransform)card.requirements[0].transform.parent, "−" + requirement.value + " 行动点");
            }
            if (!(controls?.Invoke() ?? true)) { busy = false; yield break; }
            bool finished = false;
            bool accepted = NarrativeRuntime.Instance.Actions.Execute(card.action.actionKey, success =>
            {
                finished = true;
                if (success && (controls?.Invoke() ?? true) && card.action.endDay) { busy = false; endDay?.Invoke(); }
                if (!success) Notice("行动事件未能执行");
            });
            if (!accepted) { Notice("要求不满足"); finished = true; }
            while (!finished) yield return null;
            busy = false; inventory.Refresh(); Rebuild();
        }
        private IEnumerator Fly(RectTransform source, RectTransform target, string text)
        {
            if (source == null || target == null) yield break;
            var effect = Rect("ConsumptionEffect", transform.root);
            effect.sizeDelta = new Vector2(124, 70);
            var background = effect.gameObject.AddComponent<Image>();
            background.color = new Color(.12f, .32f, .22f, .98f); background.raycastTarget = false;
            var itemLabel = source.GetComponentInChildren<TextMeshProUGUI>();
            var label = Label("Label", effect, source == inventory.PointsRect ? text : (itemLabel?.text.Split('\n')[0] ?? "物品") + "\n" + text, 20);
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
        private void Notice(string message)
        {
            var root = Rect("RequirementNotice", transform.root);
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
            root.sizeDelta = new Vector2(380, 120);
            root.gameObject.AddComponent<Image>().color = new Color(.16f, .07f, .07f, .98f);
            var label = Label("Message", root, message, 24); Stretch(label.rectTransform, 16);
            label.alignment = TextAlignmentOptions.Center;
            Destroy(root.gameObject, 2);
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
            button.onClick.AddListener(click); return button;
        }
    }

}
