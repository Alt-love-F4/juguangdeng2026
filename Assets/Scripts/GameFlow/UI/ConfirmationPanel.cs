using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    public sealed class ConfirmationPanel : GameFlowPanel
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private TMP_Text acceptLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text cancelLabel;
        [SerializeField] private Button backButton;
        private ConfirmationPanelData current;

        protected override void Awake()
        {
            SetModal(true);
            base.Awake();
            acceptButton?.onClick.AddListener(() => ActionOnce(true));
            cancelButton?.onClick.AddListener(() => ActionOnce(false));
            backButton?.onClick.AddListener(DismissFromFlow);
        }

        protected override void OnOpen(YokiFrame.IUIData data = null)
        {
            base.OnOpen(data);
            current = data as ConfirmationPanelData;
            if (titleText != null) titleText.text = current?.Title ?? string.Empty;
            if (messageText != null) messageText.text = current?.Message ?? string.Empty;
            if (acceptLabel != null) acceptLabel.text = current?.AcceptLabel ?? "确定";
            if (cancelLabel != null) cancelLabel.text = current?.CancelLabel ?? "取消";
        }

        protected override void OnClose()
        {
            base.OnClose();
            current = null;
        }

        public void CancelFromFlow() => DismissFromFlow();

        public void DismissFromFlow()
        {
            current = null;
            Flow?.CloseModalPanel(this);
        }

        private void ActionOnce(bool accepted)
        {
            ConfirmationPanelData result = current;
            current = null;
            if (result == null) return;
            result.Flow?.CloseModalPanel(this, () =>
            {
                if (accepted) result.Accepted?.Invoke(); else result.Cancelled?.Invoke();
            });
        }
    }
}
