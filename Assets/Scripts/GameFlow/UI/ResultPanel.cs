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
        private ResultPanelData current;
        private RectState titleRectState, messageRectState, endingIdRectState, dividerRectState;
        private Image dividerImage;
        private bool dividerWasActive;
        private bool rectStatesCaptured;
        public string CurrentEndingId => current?.EndingId;
        public string DisplayedTitle => titleText != null ? titleText.text : string.Empty;
        public string DisplayedMessage => messageText != null ? messageText.text : string.Empty;
        public string DisplayedEndingEcho => endingEchoText != null ? endingEchoText.text : string.Empty;
        public string DisplayedEndingArtworkStatus => endingArtworkStatus != null ? endingArtworkStatus.text : string.Empty;
        public Sprite DisplayedEndingArtwork => endingArtwork != null ? endingArtwork.sprite : null;
        public bool IsEndingPresentation => current?.IsEnding == true;
        public void ConfirmResult() => CloseResult();

        protected override void Awake()
        {
            SetModal(true);
            base.Awake();
            CaptureRectStates();
            Spotlight.Audio.UIButtonAudio.Configure(closeButton, string.Empty);
            closeButton?.onClick.AddListener(CloseResult);
        }

        protected override void OnOpen(YokiFrame.IUIData data = null)
        {
            base.OnOpen(data);
            current = data as ResultPanelData;
            ApplyLayout(current?.IsEnding == true);
            if (endingIdText != null)
            {
                endingIdText.text = current?.IsEnding == true ? "ENDING  /  " + current.EndingId : string.Empty;
                endingIdText.gameObject.SetActive(current?.IsEnding == true);
            }
            if (endingDecoration != null) endingDecoration.SetActive(current?.IsEnding == true);
            if (titleText != null) titleText.text = current?.Title ?? string.Empty;
            if (messageText != null) messageText.text = current?.Message ?? string.Empty;
            if (endingArtworkFrame != null) endingArtworkFrame.SetActive(current?.IsEnding == true);
            if (endingArtwork != null)
            {
                endingArtwork.sprite = current?.EndingArtwork;
                endingArtwork.preserveAspect = true;
                endingArtwork.gameObject.SetActive(current?.IsEnding == true && current.EndingArtwork != null);
                endingArtwork.color = current?.EndingArtworkIsPlaceholder == true ? new Color(.72f, .72f, .72f, 1f) : Color.white;
            }
            if (endingArtworkStatus != null)
            {
                bool hasArt = current?.EndingArtwork != null;
                endingArtworkStatus.text = current?.IsEnding == true
                    ? (hasArt && !current.EndingArtworkIsPlaceholder ? string.Empty : current.EndingArtworkStatus)
                    : string.Empty;
                endingArtworkStatus.gameObject.SetActive(current?.IsEnding == true && (!hasArt || current.EndingArtworkIsPlaceholder));
            }
            if (endingEchoText != null) endingEchoText.text = current?.EndingEcho ?? string.Empty;
            if (endingEchoSection != null) endingEchoSection.SetActive(current?.IsEnding == true && !string.IsNullOrWhiteSpace(current.EndingEcho));
            var label = closeButton != null ? closeButton.GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null) label.text = current?.CloseLabel ?? "关闭";
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
            titleRectState = RectState.Capture(titleText != null ? titleText.rectTransform : null);
            messageRectState = RectState.Capture(messageText != null ? messageText.rectTransform : null);
            endingIdRectState = RectState.Capture(endingIdText != null ? endingIdText.rectTransform : null);
            var divider = transform.Find("EndingDivider");
            dividerRectState = RectState.Capture(divider as RectTransform);
            dividerImage = divider != null ? divider.GetComponent<Image>() : null;
            dividerWasActive = divider != null && divider.gameObject.activeSelf;
            rectStatesCaptured = true;
        }

        private void ApplyLayout(bool ending)
        {
            if (!rectStatesCaptured) CaptureRectStates();
            RectTransform title = titleText != null ? titleText.rectTransform : null;
            RectTransform message = messageText != null ? messageText.rectTransform : null;
            RectTransform id = endingIdText != null ? endingIdText.rectTransform : null;
            RectTransform divider = transform.Find("EndingDivider") as RectTransform;
            if (!ending)
            {
                titleRectState.Restore(title); messageRectState.Restore(message);
                endingIdRectState.Restore(id); dividerRectState.Restore(divider);
                if (dividerImage != null) dividerImage.gameObject.SetActive(dividerWasActive);
                return;
            }
            SetAnchors(title, new Vector2(.12f, .82f), new Vector2(.88f, .90f));
            SetAnchors(id, new Vector2(.13f, .77f), new Vector2(.87f, .81f));
            SetAnchors(divider, new Vector2(.35f, .755f), new Vector2(.65f, .759f));
            if (dividerImage != null) dividerImage.gameObject.SetActive(true);
            SetAnchors(message, new Vector2(.12f, .285f), new Vector2(.88f, .43f));
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            if (rect == null) return;
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        protected override void OnClose()
        {
            base.OnClose();
            current = null;
            ApplyLayout(false);
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
