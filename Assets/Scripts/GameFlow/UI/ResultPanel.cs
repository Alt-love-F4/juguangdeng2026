using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    public sealed class ResultPanel : GameFlowPanel
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text endingIdText;
        [SerializeField] private GameObject endingDecoration;
        [SerializeField] private Image endingArtwork;
        [SerializeField] private GameObject endingArtworkFrame;
        [SerializeField] private TMP_Text endingArtworkStatus;
        [SerializeField] private TMP_Text endingEchoText;
        [SerializeField] private GameObject endingEchoSection;
        [SerializeField] private DialoguePanel endingDialoguePanel;
        public DialoguePanel EndingDialogue => endingDialoguePanel;
        private RectState closeRectState;
        private Image backgroundImage, surfaceImage;
        private Color backgroundColor, messageColor;
        private Sprite backgroundSprite;
        private Image.Type backgroundType;
        private bool surfaceWasActive;
        public bool IsEndingDialogueVisible => endingDialoguePanel != null && endingDialoguePanel.gameObject.activeSelf;
        private ResultPanelData current;
        private RectState titleRectState, messageRectState, endingIdRectState, dividerRectState;
        private Image dividerImage;
        private bool dividerWasActive;
        private RectState panelRectState, buttonRectState;
        private Color originalTitleColor;
private bool rectStatesCaptured;
        public string CurrentEndingId => current?.EndingId;
        public string DisplayedTitle => titleText != null ? titleText.text : string.Empty;
        public string DisplayedMessage => current?.IsEnding == true && endingDialoguePanel != null ? endingDialoguePanel.SettlementText : messageText != null ? messageText.text : string.Empty;
        public string DisplayedCloseLabel
        {
            get
            {
                TMP_Text label = closeButton != null ? closeButton.GetComponentInChildren<TMP_Text>(true) : null;
                return label != null ? label.text : string.Empty;
            }
        }
        public string DisplayedEndingEcho => endingEchoText != null ? endingEchoText.text : string.Empty;
        public string DisplayedEndingArtworkStatus => endingArtworkStatus != null ? endingArtworkStatus.text : string.Empty;
        public Sprite DisplayedEndingArtwork => endingArtwork != null ? endingArtwork.sprite : null;
        public bool IsEndingPresentation => current?.IsEnding == true;
        public bool IsSaveSuccessPresentation => current?.IsSaveSuccess == true;
        public void ConfirmResult()
        {
            if (current?.IsEnding == true && endingDialoguePanel != null && endingDialoguePanel.IsEndingSettlement)
                endingDialoguePanel.AdvanceEndingSettlement();
            else CloseResult();
        }

        protected override void Awake()
        {
            SetModal(true);
            base.Awake();
            CaptureRectStates();
            Spotlight.Audio.UIButtonAudio.Configure(closeButton, string.Empty);
            closeButton?.onClick.AddListener(ConfirmResult);
            if (endingDialoguePanel != null) endingDialoguePanel.SettlementChanged += RefreshContinueLabel;
        }

        protected override void OnOpen(YokiFrame.IUIData data = null)
        {
            base.OnOpen(data);
            current = data as ResultPanelData;
            ApplyLayout(current?.IsEnding == true, current?.IsSaveSuccess == true);
            if (endingIdText != null)
            {
                endingIdText.text = current?.IsEnding == true ? "ENDING  /  " + (string.IsNullOrWhiteSpace(current.EndingDisplayCode) ? current.EndingId : current.EndingDisplayCode) : string.Empty;
                endingIdText.gameObject.SetActive(current?.IsEnding == true);
            }
            if (endingDecoration != null) endingDecoration.SetActive(current?.IsEnding == true);
            if (titleText != null) titleText.text = current?.Title ?? string.Empty;
            if (messageText != null) { messageText.text = current?.Message ?? string.Empty; messageText.gameObject.SetActive(current?.IsEnding != true); }
            if (endingDialoguePanel != null)
            {
                bool ending=current?.IsEnding==true;
                endingDialoguePanel.gameObject.SetActive(ending);
                if(ending)endingDialoguePanel.ShowEndingSettlement(current.Message, CloseResult);
            }
            if (endingArtworkFrame != null) endingArtworkFrame.SetActive(current?.IsEnding == true);
            if (endingArtwork != null)
            {
                endingArtwork.sprite = current?.EndingArtwork;
                endingArtwork.preserveAspect = true;
                endingArtwork.gameObject.SetActive(current?.IsEnding == true && current.EndingArtwork != null);
                endingArtwork.color = Color.white;
            }
            if (endingArtworkStatus != null)
            {
                bool hasArt = current?.EndingArtwork != null;
                endingArtworkStatus.text = current?.IsEnding == true
                    ? (hasArt && !current.EndingArtworkIsPlaceholder ? string.Empty : current.EndingArtworkStatus)
                    : string.Empty;
                endingArtworkStatus.gameObject.SetActive(current?.IsEnding == true && !hasArt);
            }
            if (endingEchoText != null) endingEchoText.text = string.Empty;
            if (endingEchoSection != null) endingEchoSection.SetActive(false);
            RefreshContinueLabel();
            Spotlight.Audio.GameAudio.StopOwner(this);
            if (current?.IsEnding == true) StartEndingMusic();
        }

        private void StartEndingMusic()
        {
            if (current?.IsEnding == true)
                Spotlight.Audio.GameAudio.OverrideMusic(this, Spotlight.Audio.NarrativeMusic.Ending(current.EndingId));
        }

        protected override void OnShow()
        {
            base.OnShow();
            StartEndingMusic();
        }

        protected override void OnWillHide()
        {
            Spotlight.Audio.GameAudio.StopOwner(this);
            base.OnWillHide();
        }

        protected override void OnBeforeDestroy()
        {
            Spotlight.Audio.GameAudio.StopOwner(this);
            base.OnBeforeDestroy();
        }

        private void OnDisable() => Spotlight.Audio.GameAudio.StopOwner(this);

        private void RefreshContinueLabel()
        {
            var label = closeButton != null ? closeButton.GetComponentInChildren<TMP_Text>(true) : null;
            if (label == null) return;
            label.text = current?.IsEnding == true && endingDialoguePanel != null && endingDialoguePanel.IsEndingSettlement
                ? endingDialoguePanel.IsSettlementTyping ? "点击显示全文"
                    : !endingDialoguePanel.IsSettlementLastPage ? "继续阅读" : current.CloseLabel
                : current?.CloseLabel ?? "关闭";
        }

        private struct RectState
        {
            public Vector2 anchorMin, anchorMax, offsetMin, offsetMax;
            public static RectState Capture(RectTransform rect) => rect == null ? default : new RectState
            { anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, offsetMin = rect.offsetMin, offsetMax = rect.offsetMax };
            public void Restore(RectTransform rect)
            {
                if (rect == null) return;
                rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            }
        }

        private void CaptureRectStates()
        {
            if (rectStatesCaptured) return;
            panelRectState = RectState.Capture(transform.Find("Panel") as RectTransform);
            buttonRectState = RectState.Capture(closeButton != null ? closeButton.transform as RectTransform : null);
            titleRectState = RectState.Capture(titleText != null ? titleText.rectTransform : null);
            messageRectState = RectState.Capture(messageText != null ? messageText.rectTransform : null);
            if(messageText != null) messageColor = messageText.color;
            endingIdRectState = RectState.Capture(endingIdText != null ? endingIdText.rectTransform : null);
            var divider = transform.Find("EndingDivider");
            dividerRectState = RectState.Capture(divider as RectTransform);
            dividerImage = divider != null ? divider.GetComponent<Image>() : null;
            dividerWasActive = divider != null && divider.gameObject.activeSelf;
            closeRectState = RectState.Capture(closeButton != null ? closeButton.transform as RectTransform : null);
            var background = titleText != null ? titleText.transform.parent.Find("Background") : null;
            backgroundImage = background != null ? background.GetComponent<Image>() : null;
            if (backgroundImage != null) { backgroundColor = backgroundImage.color; backgroundSprite = backgroundImage.sprite; backgroundType = backgroundImage.type; }
            var surface = titleText != null ? titleText.transform.parent.Find("Panel") : null;
            surfaceImage = surface != null ? surface.GetComponent<Image>() : null;
            if (surfaceImage != null) surfaceWasActive = surfaceImage.gameObject.activeSelf;
            originalTitleColor = titleText != null ? titleText.color : Color.white;
            rectStatesCaptured = true;
        }

        private void ApplyLayout(bool ending, bool saveSuccess)
        {
            if (!rectStatesCaptured) CaptureRectStates();
            RectTransform panel = transform.Find("Panel") as RectTransform;
            RectTransform title = titleText != null ? titleText.rectTransform : null;
            RectTransform message = messageText != null ? messageText.rectTransform : null;
            RectTransform id = endingIdText != null ? endingIdText.rectTransform : null;
            RectTransform divider = transform.Find("EndingDivider") as RectTransform;
            RectTransform button = closeButton != null ? closeButton.transform as RectTransform : null;

            panelRectState.Restore(panel);
            buttonRectState.Restore(button);
            titleRectState.Restore(title);
            messageRectState.Restore(message);
            endingIdRectState.Restore(id);
            dividerRectState.Restore(divider);
            if (dividerImage != null) dividerImage.gameObject.SetActive(dividerWasActive);
            if (titleText != null) titleText.color = originalTitleColor;

            if (saveSuccess)
            {
                // 保存成功：居中卡片式提示，同时把结局态的暗色背景/隐藏衬底还原回来
                SetAnchors(panel, new Vector2(.28f, .30f), new Vector2(.72f, .70f));
                SetAnchors(title, new Vector2(.32f, .60f), new Vector2(.68f, .69f));
                SetAnchors(message, new Vector2(.32f, .47f), new Vector2(.68f, .56f));
                SetAnchors(button, new Vector2(.36f, .34f), new Vector2(.64f, .43f));
                SetAnchors(divider, new Vector2(.40f, .575f), new Vector2(.60f, .579f));
                if (dividerImage != null) dividerImage.gameObject.SetActive(true);
                if (titleText != null) titleText.color = new Color(1f, .82f, .42f, 1f);
                if (backgroundImage != null) { backgroundImage.color = backgroundColor; backgroundImage.sprite = backgroundSprite; backgroundImage.type = backgroundType; }
                if (surfaceImage != null) surfaceImage.gameObject.SetActive(surfaceWasActive);
                if (messageText != null) { messageText.alignment = TextAlignmentOptions.Center; messageText.color = messageColor; }
                return;
            }

            if (!ending)
            {
                closeRectState.Restore(closeButton != null ? closeButton.transform as RectTransform : null);
                if (backgroundImage != null) { backgroundImage.color = backgroundColor; backgroundImage.sprite = backgroundSprite; backgroundImage.type = backgroundType; }
                if (surfaceImage != null) surfaceImage.gameObject.SetActive(surfaceWasActive);
                if (messageText != null) { messageText.alignment = TextAlignmentOptions.Center; messageText.color = messageColor; }
                return;
            }

            SetAnchors(title, new Vector2(.12f, .89f), new Vector2(.88f, .96f));
            SetAnchors(id, new Vector2(.13f, .84f), new Vector2(.87f, .88f));
            SetAnchors(divider, new Vector2(.35f, .835f), new Vector2(.65f, .837f));
            if (dividerImage != null) dividerImage.gameObject.SetActive(true);
            SetAnchors(message, new Vector2(.12f, .12f), new Vector2(.88f, .288f));
            SetAnchors(closeButton != null ? closeButton.transform as RectTransform : null, new Vector2(.82f,.89f),new Vector2(.97f,.95f));
            if (backgroundImage != null) { backgroundImage.sprite = null; backgroundImage.type = Image.Type.Simple; backgroundImage.color = new Color(.035f,.04f,.055f,1f); }
            if (surfaceImage != null) surfaceImage.gameObject.SetActive(false);
            if (messageText != null) { messageText.alignment = TextAlignmentOptions.TopLeft; messageText.color = new Color(.93f,.93f,.94f); }
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            if (rect == null) return;
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        protected override void OnClose()
        {
            Spotlight.Audio.GameAudio.StopOwner(this);
            base.OnClose();
            current = null;
            if(endingDialoguePanel!=null)endingDialoguePanel.gameObject.SetActive(false);
            ApplyLayout(false, false);
        }

        public void CloseFromFlow() => CloseResult();

        private void CloseResult()
        {
            ResultPanelData result = current;
            current = null;
            if (result == null) return;
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Cancel);
            if (result.Flow != null) result.Flow.CloseModalPanel(this, () => result.Closed?.Invoke());
            else CloseAnimated(() => result.Closed?.Invoke());
        }
    }
}
