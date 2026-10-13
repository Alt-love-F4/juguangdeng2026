using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>Display-only details window. No tables, game state, narrative settlement or save writes.</summary>
    public sealed class DetailsPanel : GameFlowPanel, IDetailsPanel
    {
        public enum Page { MyInfo, Relationships, World }

        [Header("Window")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button outsideButton;
        [SerializeField] private Button[] tabButtons;
        [SerializeField] private GameObject[] pages;
        [Header("Shared frame (always visible while switching pages)")]
        [SerializeField] private Image paperSurface;
        [SerializeField] private DetailsPageSurfaceBinding[] paperStyles;
        [SerializeField] private RectTransform selectedTabArtwork;
        [SerializeField] private Image[] idleTabArtwork;
        [SerializeField] private Vector2[] selectedTabPositions;
        [SerializeField] private TMP_Text pageHint;
        [SerializeField] private string[] pageHints;
        [Header("Profile component references")]
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text backgroundText;
        [SerializeField] private TMP_Text introductionText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private GameObject avatarPlaceholder;
        [SerializeField] private TMP_Text avatarPlaceholderLabel;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text footerText;
        [Header("Attribute and page bindings")]
        [SerializeField] private DetailsAttributeBinding[] attributeBindings;
        [SerializeField] private DetailsSectionBinding relationshipsBinding;
        [SerializeField] private DetailsSectionBinding worldBinding;
        [Header("Specialization component references")]
        [SerializeField] private DetailsSpecializationItem[] specializationItems;
        [SerializeField] private RectTransform specializationContent;
        [SerializeField] private RectTransform specializationViewport;
        [SerializeField] private GridLayoutGroup specializationGrid;
        [SerializeField] private ScrollRect specializationScroll;
        [SerializeField] private TMP_Text emptySpecializationsText;
        [SerializeField] private TMP_Text specializationsHintText;
        [SerializeField] private Sprite placeholderSpecializationIcon;
        [Header("Hover description")]
        [SerializeField] private RectTransform window;
        [SerializeField] private RectTransform tooltip;
        [SerializeField] private TMP_Text tooltipTitle;
        [SerializeField] private TMP_Text tooltipBody;
        [SerializeField] private GameObject defaultTooltip;
        [Header("Display transitions (unscaled time)")]
        [SerializeField, Min(0f)] private float pageFadeDuration = .12f;
        [SerializeField, Min(0f)] private float descriptionFadeDuration = .1f;
        private CanvasGroup[] pageGroups;
        private CanvasGroup descriptionGroup;
        private Tween pageTween;
        private Tween descriptionTween;
        private IDetailsPanelDataSource dataSource;
        private DetailsHoverTarget tooltipOwner;
        private Action dismissed;
        private bool closing;
        private int openedFrame;
        private readonly List<DetailsSpecializationItem> badgeViews = new List<DetailsSpecializationItem>();
        private float specializationWidth = -1f;
        [Header("Extended pages")]
        [SerializeField] private Sprite unknownCharacterAvatar;
        private DetailsPagesView detailsPages;
        public int RelationshipPageIndex => detailsPages != default ? detailsPages.RelationshipPageIndex : 0;
        public int RelationshipPageCount => detailsPages != default ? detailsPages.RelationshipPageCount : 1;
        public string SelectedCharacterId => detailsPages != default ? detailsPages.SelectedCharacterId : null;
        public event Action<DetailsPanelData> DataRefreshed;
        public event Action<Page> PageChanged;
        public event Action<string> CharacterSelected;
        public event Action<string> WorldProgressSelected;
        public event Action<int> RelationshipPageChanged;
        public event Action Closed;

        public DetailsPanelData CurrentData { get; private set; }
        public Page CurrentPage { get; private set; }
        public bool TooltipVisible => tooltip != default && tooltip.gameObject.activeSelf;

        protected override void Awake()
        {
            base.Awake();
            SetModal(true);
            HideTooltip();
        }

        protected override void OnInit(IUIData data = null)
        {
            // References are serialized in the prefab, never searched by names at runtime.
            if (playerNameText == default || backgroundText == default || introductionText == default
                || avatarImage == default || avatarPlaceholder == default || avatarPlaceholderLabel == default
                || subtitleText == default || footerText == default || closeButton == default || outsideButton == default
                || window == default || tooltip == default || tooltipTitle == default || tooltipBody == default
                || tabButtons == null || tabButtons.Length != 3 || pages == null || pages.Length != 3
                || attributeBindings == null || attributeBindings.Length != 4
                || relationshipsBinding == null || worldBinding == null
                || specializationItems == null || specializationItems.Length == 0
                || specializationContent == default || specializationViewport == default || specializationGrid == default
                || specializationScroll == default || emptySpecializationsText == default || specializationsHintText == default
                || paperSurface == default || paperStyles == null || paperStyles.Length != 3
                || selectedTabArtwork == default || selectedTabPositions == null || selectedTabPositions.Length != 3
                || idleTabArtwork == null || idleTabArtwork.Length != 3 || pageHint == default || pageHints == null || pageHints.Length != 3)
                throw new InvalidOperationException("DetailsPanel component bindings are incomplete. Run Tools/Game Flow/Bind Details Panel Components.");
            for (int i = 0; i < 3; i++)
                if (tabButtons[i] == default || pages[i] == default || idleTabArtwork[i] == default || paperStyles[i] == null)
                    throw new InvalidOperationException("DetailsPanel tab/page binding is missing at index " + i);
            for (int i = 0; i < attributeBindings.Length; i++)
                if (attributeBindings[i] == null || !attributeBindings[i].IsValid)
                    throw new InvalidOperationException("DetailsPanel attribute binding is incomplete at index " + i);
            if (!relationshipsBinding.IsValid || !worldBinding.IsValid)
                throw new InvalidOperationException("DetailsPanel section binding is incomplete.");
            for (int i = 0; i < specializationItems.Length; i++)
            {
                if (specializationItems[i] == default || !specializationItems[i].IsValid)
                    throw new InvalidOperationException("DetailsPanel specialization binding is incomplete at index " + i);
                specializationItems[i].gameObject.SetActive(false);
            }
            pageGroups = new CanvasGroup[pages.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                pageGroups[i] = pages[i].GetComponent<CanvasGroup>();
                if (pageGroups[i] == default) pageGroups[i] = pages[i].AddComponent<CanvasGroup>();
            }
            descriptionGroup = tooltip.GetComponent<CanvasGroup>();
            if (descriptionGroup == default) descriptionGroup = tooltip.gameObject.AddComponent<CanvasGroup>();
            descriptionGroup.blocksRaycasts = descriptionGroup.interactable = false;
            closeButton.onClick.AddListener(RequestClose);
            outsideButton.onClick.AddListener(RequestClose);
            tabButtons[0].onClick.AddListener(ShowMyInfo);
            tabButtons[1].onClick.AddListener(ShowRelationships);
            tabButtons[2].onClick.AddListener(ShowWorld);
            EnsureDetailsPages();
        }

        protected override void OnOpen(IUIData data = null)
        {
            // UIKit may open an inactive prefab before Unity invokes Awake.
            SetModal(true);
            base.OnOpen(data);
            closing = false;
            openedFrame = Time.frameCount;
            dismissed = null;
            Refresh(data as DetailsPanelData ?? DetailsPanelData.CreatePlaceholder(placeholderSpecializationIcon));
            SelectPage(Page.MyInfo);
        }

        protected override void OnShow()
        {
            base.OnShow();
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != default) canvas.overrideSorting = true;
            RefreshSpecializationLayout();
        }

        /// <summary>Replace the complete display snapshot without changing tabs or accessing gameplay state.</summary>
        public void Refresh(DetailsPanelData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (closing) return;
            HideTooltip();
            string previousCharacter = SelectedCharacterId;
            string previousWorld = SelectedWorldProgressKey;
            int previousIndex = RelationshipPageIndex;
            CurrentData = data;
            ((IPanel)this).Data = data;
            playerNameText.text = data.Profile.Name;
            backgroundText.text = data.Profile.Background;
            introductionText.text = string.IsNullOrEmpty(data.Profile.Background) ? data.Profile.Introduction : data.Profile.Background + "\n" + data.Profile.Introduction;
            avatarImage.sprite = data.Profile.Avatar;
            bool hasAvatar = data.Profile.Avatar != default;
            avatarImage.gameObject.SetActive(hasAvatar);
            avatarPlaceholder.SetActive(!hasAvatar);
            avatarPlaceholderLabel.gameObject.SetActive(!hasAvatar);
            avatarPlaceholderLabel.text = data.IsPlaceholder ? "头像占位" : "未设置头像";
            subtitleText.text = data.IsPlaceholder ? "馆长档案 / 展示原型" : "馆长档案";
            footerText.text = data.IsPlaceholder
                ? "全部内容均为占位样例 · 悬停查看说明 · 点击窗口外或按 Esc / Tab 关闭"
                : "悬停查看说明 · 点击窗口外或按 Esc / Tab 关闭";
            specializationsHintText.text = "-悬停查看相关属性-";
            for (int i = 0; i < attributeBindings.Length; i++) attributeBindings[i].Render(this, data.Attributes[i]);
            relationshipsBinding.Render(data.Relationships);
            worldBinding.Render(data.World);
            RenderSpecializations(data);
            EnsureDetailsPages();
            detailsPages.Refresh(data);
            NotifySelectionChanges(previousCharacter, previousWorld, previousIndex);
            DataRefreshed?.Invoke(data);
        }

        /// <summary>Explicitly injected read boundary; no polling or gameplay writes.</summary>
        public void BindDataSource(IDetailsPanelDataSource source, bool refreshImmediately = true)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (closing) return;
            dataSource = source;
            if (refreshImmediately) RefreshFromSource();
        }
        public void UnbindDataSource() => dataSource = null;
        public bool RefreshFromSource()
        {
            if (closing || dataSource == null) return false;
            DetailsPanelData snapshot = dataSource.ReadDetails();
            if (snapshot == null) return false;
            Refresh(snapshot);
            return true;
        }
        private void NotifySelectionChanges(string character, string world, int index)
        {
            if (index != RelationshipPageIndex) RelationshipPageChanged?.Invoke(RelationshipPageIndex);
            if (character != SelectedCharacterId) CharacterSelected?.Invoke(SelectedCharacterId);
            if (world != SelectedWorldProgressKey) WorldProgressSelected?.Invoke(SelectedWorldProgressKey);
        }

        private void EnsureDetailsPages()
        {
            if (detailsPages != default) return;
            detailsPages = GetComponent<DetailsPagesView>();
            if (detailsPages == default) throw new InvalidOperationException("Missing PSD page bindings. Rebuild Details From PSD.");
            detailsPages.Initialize(this, (RectTransform)pages[1].transform, (RectTransform)pages[2].transform,
                introductionText.font, unknownCharacterAvatar);
        }

        public bool ShowCharacter(string id)
        {
            if (closing || detailsPages == default) return false;
            string character = SelectedCharacterId;
            int index = RelationshipPageIndex;
            if (!detailsPages.ShowCharacter(id)) return false;
            NotifySelectionChanges(character, SelectedWorldProgressKey, index);
            return true;
        }

        public void ReturnToRelationships()
        {
            if (closing || detailsPages == default) return;
            SelectPage(Page.Relationships);
            string character = SelectedCharacterId;
            detailsPages.ReturnToRelationships();
            NotifySelectionChanges(character, SelectedWorldProgressKey, RelationshipPageIndex);
        }

        public void ChangeRelationshipPage(int delta)
        {
            if (closing || detailsPages == default) return;
            SelectPage(Page.Relationships);
            int index = RelationshipPageIndex;
            detailsPages.ChangeRelationshipPage(delta);
            NotifySelectionChanges(SelectedCharacterId, SelectedWorldProgressKey, index);
        }

        /// <summary>Zero-based page index; invalid requests leave the view unchanged.</summary>
        public bool SetRelationshipPage(int index)
        {
            if (closing || detailsPages == default) return false;
            int previous = RelationshipPageIndex;
            if (!detailsPages.SetRelationshipPage(index)) return false;
            SelectPage(Page.Relationships);
            NotifySelectionChanges(SelectedCharacterId, SelectedWorldProgressKey, previous);
            return true;
        }

        /// <summary>Normalized vertical position: 1 is top, 0 is bottom.</summary>
        public bool ScrollTo(DetailsScrollRegion region, float normalizedPosition)
        {
            if (closing || CurrentData == null || float.IsNaN(normalizedPosition) || float.IsInfinity(normalizedPosition)) return false;
            float position = Mathf.Clamp01(normalizedPosition);
            if (region != DetailsScrollRegion.Specializations)
                return detailsPages != default && detailsPages.ScrollTo(region, position);
            LayoutRebuilder.ForceRebuildLayoutImmediate(specializationContent);
            specializationScroll.StopMovement();
            specializationScroll.verticalNormalizedPosition = position;
            return true;
        }

        private void RenderSpecializations(DetailsPanelData data)
        {
            while (badgeViews.Count < data.Specializations.Count)
            {
                DetailsSpecializationItem item = Instantiate(specializationItems[0], specializationContent, false);
                item.name = "Specialization_" + badgeViews.Count;
                badgeViews.Add(item);
            }
            for (int i = 0; i < badgeViews.Count; i++)
            {
                bool visible = i < data.Specializations.Count;
                if (visible) badgeViews[i].Render(this, data.Specializations[i]);
                else badgeViews[i].Clear();
                badgeViews[i].gameObject.SetActive(visible);
            }
            emptySpecializationsText.gameObject.SetActive(data.Specializations.Count == 0);
            RefreshSpecializationLayout();
            // New snapshots start at the top; reuse cannot leave the player stranded on an old scroll offset.
            LayoutRebuilder.ForceRebuildLayoutImmediate(specializationContent);
            specializationScroll.StopMovement();
            specializationScroll.verticalNormalizedPosition = 1f;
        }

        private void RefreshSpecializationLayout()
        {
            if (specializationViewport == default || specializationGrid == default) return;
            float width = specializationViewport.rect.width;
            if (width <= 0f || Mathf.Approximately(specializationWidth, width)) return;
            specializationWidth = width;
            specializationGrid.cellSize = new Vector2(Mathf.Max(1f, (width - specializationGrid.spacing.x * 2f) / 3f), 64f);
        }

        private void LateUpdate() => RefreshSpecializationLayout();

        /// <summary>Optional preview dismissal notification; not a gameplay binding.</summary>
        public void Preview(Action onDismissed) => dismissed = onDismissed;
        public void ShowMyInfo() => SelectPage(Page.MyInfo);
        public void ShowRelationships() => SelectPage(Page.Relationships);
        public void ShowWorld() => SelectPage(Page.World);

        public void SelectPage(Page page)
        {
            if (closing || (int)page < 0 || (int)page > 2) return;
            bool changed = CurrentPage != page;
            StopPageTransition();
            CurrentPage = page;
            paperStyles[(int)page].Render(paperSurface);
            selectedTabArtwork.anchoredPosition = selectedTabPositions[(int)page];
            pageHint.text = pageHints[(int)page];
            for (int i = 0; i < idleTabArtwork.Length; i++) idleTabArtwork[i].enabled = i != (int)page;
            HideTooltip();
            for (int i = 0; pages != null && i < pages.Length; i++)
                if (pages[i] != default) pages[i].SetActive(i == (int)page);
            for (int i = 0; tabButtons != null && i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == default) continue;
                // Shared artwork owns tab states; page groups contain only their own content.
                tabButtons[i].transition = Selectable.Transition.None;
            }
            if (changed && pageGroups != null && pageFadeDuration > 0 && isActiveAndEnabled)
            {
                CanvasGroup group = pageGroups[(int)page];
                group.alpha = 0;
                pageTween = DOTween.To(() => group.alpha, value => group.alpha = value, 1f, pageFadeDuration).SetUpdate(true).SetEase(Ease.OutQuad);
            }
            if (changed) PageChanged?.Invoke(page);
        }

        private void StopPageTransition()
        {
            pageTween?.Kill(); pageTween = null;
            if (pageGroups != null)
                foreach (CanvasGroup group in pageGroups) if (group != default) group.alpha = 1;
        }
        private void StopDisplayTransitions()
        {
            StopPageTransition();
            descriptionTween?.Kill(); descriptionTween = null;
            if (descriptionGroup != default) descriptionGroup.alpha = 1;
        }

        public void RequestClose()
        {
            if (closing) return;
            closing = true;
            StopDisplayTransitions();
            HideTooltip();
            Action callback = dismissed;
            dismissed = null;
            CloseAnimated(callback);
        }

        public void ShowTooltip(DetailsHoverTarget owner, string title, string description, Vector2 screenPosition)
        {
            if (closing || tooltip == default || window == default) return;
            bool wasVisible = TooltipVisible;
            descriptionTween?.Kill(); descriptionTween = null;
            tooltipOwner = owner;
            if (defaultTooltip != default) defaultTooltip.SetActive(false);
            tooltipTitle.text = title;
            tooltipBody.text = description;
            tooltip.gameObject.SetActive(true);
            PositionTooltip(screenPosition);
            if (descriptionGroup != default)
            {
                descriptionGroup.alpha = 1;
                if (!wasVisible && descriptionFadeDuration > 0 && isActiveAndEnabled)
                {
                    descriptionGroup.alpha = 0;
                    descriptionTween = DOTween.To(() => descriptionGroup.alpha, value => descriptionGroup.alpha = value, 1f, descriptionFadeDuration).SetUpdate(true);
                }
            }
        }

        public bool ShowAttributeDescription(DetailsAttributeKind kind)
        {
            if (closing || CurrentData == null || (int)kind < 0 || (int)kind >= CurrentData.Attributes.Count) return false;
            SelectPage(Page.MyInfo);
            DetailsAttributeData attribute = CurrentData.Attributes[(int)kind];
            ShowTooltip(null, attribute.Name, attribute.Description, Vector2.zero);
            return true;
        }
        public bool ShowSpecializationDescription(int index)
        {
            if (closing || CurrentData == null || index < 0 || index >= CurrentData.Specializations.Count) return false;
            SelectPage(Page.MyInfo);
            DetailsSpecializationData item = CurrentData.Specializations[index];
            ShowTooltip(null, item.Name, item.Description, Vector2.zero);
            return true;
        }
        public void HideDescription() => HideTooltip();

        public void HideTooltip(DetailsHoverTarget owner)
        {
            if (tooltipOwner == owner) HideTooltip();
        }

        private void HideTooltip()
        {
            descriptionTween?.Kill(); descriptionTween = null;
            if (descriptionGroup != default) descriptionGroup.alpha = 1;
            tooltipOwner = null;
            if (defaultTooltip != default) defaultTooltip.SetActive(CurrentPage == Page.MyInfo);
            if (tooltip != default) tooltip.gameObject.SetActive(false);
        }

        internal void HidePageTooltip() => HideTooltip();

        public bool SelectWorldProgress(string key)
        {
            if (closing || detailsPages == default) return false;
            string previous = SelectedWorldProgressKey;
            if (!detailsPages.SelectWorldProgress(key)) return false;
            NotifySelectionChanges(SelectedCharacterId, previous, RelationshipPageIndex);
            return true;
        }
        public string SelectedWorldProgressKey => detailsPages != default ? detailsPages.SelectedWorldProgressKey : null;

        private void PositionTooltip(Vector2 screenPosition)
        {
            // My-info uses the PSD's fixed hanging card. Other pages show a floating card near the pointer.
            if (CurrentPage == Page.MyInfo)
            {
                tooltip.anchorMin = tooltip.anchorMax = new Vector2(0, 1);
                tooltip.pivot = new Vector2(.5f, .5f);
                tooltip.anchoredPosition = new Vector2(570, -774);
                return;
            }
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera camera = canvas != default && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(window, screenPosition, camera, out Vector2 local)) return;
            tooltip.anchorMin = tooltip.anchorMax = new Vector2(.5f, .5f);
            tooltip.pivot = Vector2.zero;
            tooltip.anchoredPosition = new Vector2(Mathf.Clamp(local.x + 18, -740, 580), Mathf.Clamp(local.y + 18, -410, 220));
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame
                || (Time.frameCount > openedFrame && keyboard.tabKey.wasPressedThisFrame))) RequestClose();
            if (TooltipVisible && Mouse.current != null) PositionTooltip(Mouse.current.position.ReadValue());
        }

        protected override void OnWillHide()
        {
            StopDisplayTransitions();
            HideTooltip();
            base.OnWillHide();
        }

        protected override void OnClose()
        {
            closing = true;
            dismissed = null;
            dataSource = null;
            CurrentData = null;
            StopDisplayTransitions();
            HideTooltip();
            if (avatarImage != default) avatarImage.sprite = null;
            if (detailsPages != default) detailsPages.Clear();
            for (int i = 0; i < badgeViews.Count; i++)
                if (badgeViews[i] != default) badgeViews[i].Clear();
            Action callback = Closed;
            ClearDisplayEvents();
            base.OnClose();
            callback?.Invoke();
        }

        private void ClearDisplayEvents()
        {
            DataRefreshed = null; PageChanged = null; CharacterSelected = null;
            WorldProgressSelected = null; RelationshipPageChanged = null; Closed = null;
        }
        private void OnDisable()
        {
            StopDisplayTransitions();
            HideTooltip();
        }

#if UNITY_EDITOR
        [ContextMenu("Details/Load Sample Snapshot")]
        private void DebugLoadSample()
        {
            if (Application.isPlaying && CurrentData != null) Refresh(DetailsPanelData.CreatePlaceholder(placeholderSpecializationIcon));
        }
        [ContextMenu("Details/Refresh Bound Source")]
        private void DebugRefreshSource()
        {
            if (Application.isPlaying) RefreshFromSource();
        }
        [ContextMenu("Details/Print Display State")]
        private void DebugPrintState() => Debug.Log($"Details: page={CurrentPage}, character={SelectedCharacterId}, relationshipPage={RelationshipPageIndex + 1}/{RelationshipPageCount}, world={SelectedWorldProgressKey}, tooltip={TooltipVisible}, hasData={CurrentData != null}", this);
#endif

        protected override void OnBeforeDestroy()
        {
            dismissed = null;
            CurrentData = null;
            dataSource = null;
            StopDisplayTransitions();
            ClearDisplayEvents();
            badgeViews.Clear();
            if (closeButton != default) closeButton.onClick.RemoveListener(RequestClose);
            if (outsideButton != default) outsideButton.onClick.RemoveListener(RequestClose);
            if (tabButtons != null && tabButtons.Length == 3)
            {
                tabButtons[0].onClick.RemoveListener(ShowMyInfo);
                tabButtons[1].onClick.RemoveListener(ShowRelationships);
                tabButtons[2].onClick.RemoveListener(ShowWorld);
            }
            base.OnBeforeDestroy();
        }
    }
}
