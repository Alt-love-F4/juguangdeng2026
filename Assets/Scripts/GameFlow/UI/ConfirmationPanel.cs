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
            foreach (Button button in new[] { acceptButton, cancelButton, backButton, saveAcceptButton, saveCancelButton })
                Spotlight.Audio.UIButtonAudio.Configure(button, string.Empty);
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
            // Use the save-management secondary-menu presentation for every confirmation.
            // The old default presentation is intentionally kept as a fallback in the prefab,
            // but should no longer be shown for the blue confirmation dialogs.
            bool useSaveStyle = savePresentation != null;
            if (defaultPresentation != null) defaultPresentation.SetActive(!useSaveStyle);
            if (savePresentation != null)
            {
                savePresentation.SetActive(useSaveStyle);
                var backdrop = savePresentation.transform.Find("SaveBackdrop");
                if (backdrop != null) backdrop.gameObject.SetActive(false);
            }
            if (saveMessageText != null) saveMessageText.text = current?.Message ?? string.Empty;
            if (saveAcceptButton != null) saveAcceptButton.GetComponentInChildren<TMP_Text>().text = current?.AcceptLabel ?? "确定";
            if (saveCancelButton != null) saveCancelButton.GetComponentInChildren<TMP_Text>().text = current?.CancelLabel ?? "取消";
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

        public bool AutomationAcceptDayFlow()
        {
            if (!NarrativeAutoRunner.IsWalkthroughActive || current == null
                || (current.Title != "今天还有重要的事" && current.Title != "结束营业")) return false;
            ActionOnce(true); return true;
        }

        public void CancelFromFlow() => DismissFromFlow();

        public void DismissFromFlow()
        {
            ConfirmationPanelData result = current;
            current = null;
            if (result == null) return;
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Cancel);
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
            Spotlight.Audio.GameAudio.Play(accepted ? Spotlight.Audio.AudioKeys.Confirm : Spotlight.Audio.AudioKeys.Cancel);
            result.Flow?.CloseModalPanel(this, () =>
            {
                if (accepted) result.Accepted?.Invoke(); else result.Cancelled?.Invoke();
            });
        }
    }
}
