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
        private ResultPanelData current;

        protected override void Awake()
        {
            SetModal(true);
            base.Awake();
            closeButton?.onClick.AddListener(CloseResult);
        }

        protected override void OnOpen(YokiFrame.IUIData data = null)
        {
            base.OnOpen(data);
            current = data as ResultPanelData;
            if (titleText != null) titleText.text = current?.Title ?? string.Empty;
            if (messageText != null) messageText.text = current?.Message ?? string.Empty;
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
            result.Flow?.CloseModalPanel(this, () => result.Closed?.Invoke());
        }
    }
}
