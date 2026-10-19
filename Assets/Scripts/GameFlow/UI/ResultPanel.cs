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
        private ResultPanelData current;
        public string CurrentEndingId => current?.EndingId;
        public string DisplayedTitle => titleText != null ? titleText.text : string.Empty;
        public string DisplayedMessage => messageText != null ? messageText.text : string.Empty;
        public bool IsEndingPresentation => current?.IsEnding == true;
        public void ConfirmResult() => CloseResult();

        protected override void Awake()
        {
            SetModal(true);
            base.Awake();
            Spotlight.Audio.UIButtonAudio.Configure(closeButton, string.Empty);
            closeButton?.onClick.AddListener(CloseResult);
        }

        protected override void OnOpen(YokiFrame.IUIData data = null)
        {
            base.OnOpen(data);
            current = data as ResultPanelData;
            if (endingIdText != null)
            {
                endingIdText.text = current?.IsEnding == true ? "ENDING  /  " + current.EndingId : string.Empty;
                endingIdText.gameObject.SetActive(current?.IsEnding == true);
            }
            if (endingDecoration != null) endingDecoration.SetActive(current?.IsEnding == true);
            if (titleText != null) titleText.text = current?.Title ?? string.Empty;
            if (messageText != null) messageText.text = current?.Message ?? string.Empty;
            var label = closeButton != null ? closeButton.GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null) label.text = current?.CloseLabel ?? "关闭";
        }

        protected override void OnClose()
        {
            base.OnClose();
            current = null;
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
