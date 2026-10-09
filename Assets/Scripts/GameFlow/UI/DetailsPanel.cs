using System;
using System.Collections.Generic;
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
        [Header("Tab palette")]
        [SerializeField] private Color selectedColor = new Color(.52f, .32f, .36f);
        [SerializeField] private Color idleColor = new Color(.19f, .15f, .21f);
        private DetailsHoverTarget tooltipOwner;
        private Action dismissed;
        private bool closing;
        private readonly List<DetailsSpecializationItem> badgeViews = new List<DetailsSpecializationItem>();
        private float specializationWidth = -1f;
        [Header("Extended pages")]
        [SerializeField] private Sprite unknownCharacterAvatar;
        private DetailsPagesView detailsPages;
        public int RelationshipPageIndex => detailsPages != default ? detailsPages.RelationshipPageIndex : 0;

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
                || specializationScroll == default || emptySpecializationsText == default || specializationsHintText == default)
                throw new InvalidOperationException("DetailsPanel component bindings are incomplete. Run Tools/Game Flow/Bind Details Panel Components.");
            for (int i = 0; i < 3; i++)
                if (tabButtons[i] == default || pages[i] == default)
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
                badgeViews.Add(specializationItems[i]);
            }
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
            CurrentData = data;
            ((IPanel)this).Data = data;
            playerNameText.text = data.Profile.Name;
            backgroundText.text = data.Profile.Background;
            introductionText.text = data.Profile.Introduction;
            avatarImage.sprite = data.Profile.Avatar;
            bool hasAvatar = data.Profile.Avatar != default;
            avatarImage.gameObject.SetActive(hasAvatar);
            avatarPlaceholder.SetActive(!hasAvatar);
            avatarPlaceholderLabel.gameObject.SetActive(!hasAvatar);
            avatarPlaceholderLabel.text = data.IsPlaceholder ? "头像占位" : "未设置头像";
            subtitleText.text = data.IsPlaceholder ? "馆长档案 / 展示原型" : "馆长档案";
            footerText.text = data.IsPlaceholder
                ? "全部内容均为占位样例 · 悬停查看说明 · 点击窗口外或按 Esc 关闭"
                : "悬停查看说明 · 点击窗口外或按 Esc 关闭";
            specializationsHintText.text = data.IsPlaceholder ? "徽章墙 · 以下专精均为展示样例" : "徽章墙 · 悬停查看专精说明";
            for (int i = 0; i < attributeBindings.Length; i++) attributeBindings[i].Render(this, data.Attributes[i]);
            relationshipsBinding.Render(data.Relationships);
            worldBinding.Render(data.World);
            RenderSpecializations(data);
            EnsureDetailsPages();
            detailsPages.Refresh(data);
        }

        private void EnsureDetailsPages()
        {
            if (detailsPages != default) return;
            detailsPages = gameObject.AddComponent<DetailsPagesView>();
            detailsPages.Initialize(this, (RectTransform)pages[1].transform, (RectTransform)pages[2].transform,
                playerNameText.font, unknownCharacterAvatar != null ? unknownCharacterAvatar : Resources.Load<DetailsDisplayConfig>("DetailsDisplayConfig")?.unknownAvatar);
        }

        public bool ShowCharacter(string id)
        {
            if (closing || detailsPages == default) return false;
            return detailsPages.ShowCharacter(id);
        }

        public void ReturnToRelationships()
        {
            if (closing || detailsPages == default) return;
            SelectPage(Page.Relationships);
            detailsPages.ReturnToRelationships();
        }

        public void ChangeRelationshipPage(int delta)
        {
            if (closing || detailsPages == default) return;
            SelectPage(Page.Relationships);
            detailsPages.ChangeRelationshipPage(delta);
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
            CurrentPage = page;
            HideTooltip();
            for (int i = 0; pages != null && i < pages.Length; i++)
                if (pages[i] != default) pages[i].SetActive(i == (int)page);
            for (int i = 0; tabButtons != null && i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == default) continue;
                ColorBlock colors = tabButtons[i].colors;
                colors.normalColor = i == (int)page ? selectedColor : idleColor;
                colors.highlightedColor = i == (int)page ? selectedColor * 1.12f : idleColor * 1.35f;
                colors.selectedColor = colors.normalColor;
                tabButtons[i].colors = colors;
            }
        }

        public void RequestClose()
        {
            if (closing) return;
            closing = true;
            HideTooltip();
            Action callback = dismissed;
            dismissed = null;
            CloseAnimated(callback);
        }

        public void ShowTooltip(DetailsHoverTarget owner, string title, string description, Vector2 screenPosition)
        {
            if (closing || tooltip == default || window == default) return;
            tooltipOwner = owner;
            tooltipTitle.text = title;
            tooltipBody.text = description;
            tooltip.gameObject.SetActive(true);
            PositionTooltip(screenPosition);
        }

        public void HideTooltip(DetailsHoverTarget owner)
        {
            if (tooltipOwner == owner) HideTooltip();
        }

        private void HideTooltip()
        {
            tooltipOwner = null;
            if (tooltip != default) tooltip.gameObject.SetActive(false);
        }

        private void PositionTooltip(Vector2 screenPosition)
        {
            Canvas canvas = GetComponent<Canvas>();
            Camera camera = canvas != default && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(window, screenPosition, camera, out Vector2 local)) return;
            // Tooltip pivot is bottom-left; keep the full description inside the window, even near an edge.
            Rect bounds = window.rect;
            Vector2 size = tooltip.rect.size;
            tooltip.anchoredPosition = new Vector2(
                Mathf.Clamp(local.x + 18f, bounds.xMin + 12f, Mathf.Max(bounds.xMin + 12f, bounds.xMax - size.x - 12f)),
                Mathf.Clamp(local.y + 18f, bounds.yMin + 12f, Mathf.Max(bounds.yMin + 12f, bounds.yMax - size.y - 12f)));
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) RequestClose();
            if (TooltipVisible && Mouse.current != null) PositionTooltip(Mouse.current.position.ReadValue());
        }

        protected override void OnWillHide()
        {
            HideTooltip();
            base.OnWillHide();
        }

        protected override void OnClose()
        {
            dismissed = null;
            CurrentData = null;
            HideTooltip();
            if (avatarImage != default) avatarImage.sprite = null;
            if (detailsPages != default) detailsPages.Clear();
            for (int i = 0; i < badgeViews.Count; i++)
                if (badgeViews[i] != default) badgeViews[i].Clear();
            base.OnClose();
        }

        private void OnDisable() => HideTooltip();

        protected override void OnBeforeDestroy()
        {
            dismissed = null;
            CurrentData = null;
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
