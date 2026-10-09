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
        [SerializeField] private GameObject defaultPresentation;
        [SerializeField] private GameObject savePresentation;
        [SerializeField] private TMP_Text saveMessageText;
        [SerializeField] private Button saveAcceptButton;
        [SerializeField] private Button saveCancelButton;
        private ConfirmationPanelData current;

        protected override void Awake()
        {
            SetModal(true);
            base.Awake();
            acceptButton?.onClick.AddListener(() => ActionOnce(true));
            cancelButton?.onClick.AddListener(() => ActionOnce(false));
            backButton?.onClick.AddListener(DismissFromFlow);
            saveAcceptButton?.onClick.AddListener(() => ActionOnce(true));
            saveCancelButton?.onClick.AddListener(() => ActionOnce(false));
        }

        protected override void OnOpen(YokiFrame.IUIData data = null)
        {
            base.OnOpen(data);
            current = data as ConfirmationPanelData;
            // Keep the replacement dialog artwork, overlaying the actual slot list.
            bool saveStyle = current?.Title == "读取存档" || current?.Title == "覆盖存档" || current?.Title == "删除存档";
            if (defaultPresentation != null) defaultPresentation.SetActive(!saveStyle);
            if (savePresentation != null)
            {
                savePresentation.SetActive(saveStyle);
                var backdrop = savePresentation.transform.Find("SaveBackdrop");
                if (backdrop != null) backdrop.gameObject.SetActive(false);
            }
            if (saveMessageText != null) saveMessageText.text = current?.Message ?? string.Empty;
            if (saveAcceptButton != null) saveAcceptButton.GetComponentInChildren<TMP_Text>().text = current?.AcceptLabel ?? "确定";
            if (saveCancelButton != null) saveCancelButton.GetComponentInChildren<TMP_Text>().text = "取消";
            if (titleText != null) titleText.text = current?.Title ?? string.Empty;
            if (messageText != null) messageText.text = current?.Message ?? string.Empty;
            if (acceptLabel != null) acceptLabel.text = current?.AcceptLabel ?? "确定";
            if (cancelLabel != null) cancelLabel.text = current?.CancelLabel ?? "取消";
            if (backButton != null)
            {
                backButton.gameObject.SetActive(current?.Title == "退出主菜单" || current?.Title == "退出游戏" && current?.CancelLabel == "不保存并退出");
                var label = backButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "取消";
            }
        }

        public void SetMessage(string message)
        {
            if (messageText != null) messageText.text = message;
            if (saveMessageText != null) saveMessageText.text = message;
        }

        protected override void OnShow()
        {
            if (savePresentation != null && savePresentation.activeSelf) SetDefaultSelectable(saveCancelButton);
            else SetDefaultSelectable(backButton != null && backButton.gameObject.activeSelf ? backButton : cancelButton);
            base.OnShow();
        }

        protected override void OnClose()
        {
            base.OnClose();
            current = null;
        }

        public void CancelFromFlow() => DismissFromFlow();

        public void DismissFromFlow()
        {
            ConfirmationPanelData result = current;
            current = null;
            Flow?.CloseModalPanel(this, () =>
            {
                if (result?.CancelLabel == "取消" || result?.Title == "确认分辨率") result.Cancelled?.Invoke();
            });
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
