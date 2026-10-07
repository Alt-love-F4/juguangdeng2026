using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>Single-page event notice. Progress is display-only until the NCS adapter is connected.</summary>
    public sealed class NoticePanel : UIPanel, INoticePresentation
    {
        [Header("Prefab bindings")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RawImage noticeImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private Slider progressBar;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField, Min(0f)] private float enterSeconds = .2f;
        [SerializeField, Min(0f)] private float exitSeconds = .15f;

        private Sequence transition;
        private Action finished;
        private Action cancelled;
        private bool ready;
        private bool exiting;
        private int lastConfirmFrame;
        public bool IsPlaying { get; private set; }

        protected override void OnInit(IUIData data = null)
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
        }

        protected override void OnOpen(IUIData data = null)
        {
            Stop();
            UIKit.SetPanelModal(this, true);
        }

        protected override void OnShow()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
        }

        public void Play(NoticeData value, Action onFinished = null, Action onCancelled = null)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (canvasGroup == null || confirmButton == null) throw new InvalidOperationException("NoticePanel has incomplete prefab bindings.");
            Stop();
            if (!value.Visible) { onFinished?.Invoke(); return; }
            lastConfirmFrame = Time.frameCount;
            var system = UnityEngine.EventSystems.EventSystem.current;
            if (system != null) system.SetSelectedGameObject(null);
            finished = onFinished;
            cancelled = onCancelled;
            ready = false;
            exiting = false;
            IsPlaying = true;
            if (noticeImage != null)
            {
                noticeImage.texture = value.Image;
                noticeImage.gameObject.SetActive(value.Image != null);
                var fitter = noticeImage.GetComponent<AspectRatioFitter>();
                if (value.Image != null && fitter != null) fitter.aspectRatio = value.Image.width / (float)Mathf.Max(1, value.Image.height);
            }
            if (titleText != null) { titleText.text = value.Title; titleText.gameObject.SetActive(!string.IsNullOrEmpty(value.Title)); }
            if (timeText != null)
            {
                timeText.text = value.Time;
                timeText.gameObject.SetActive(!string.IsNullOrEmpty(value.Time));
            }
            if (bodyText != null)
            {
                bodyText.text = value.Body;
                bodyText.rectTransform.anchorMax = new Vector2(value.Image == null ? .93f : .53f, .66f);
                bodyText.rectTransform.offsetMin = bodyText.rectTransform.offsetMax = Vector2.zero;
            }
            if (progressBar != null)
            {
                progressBar.gameObject.SetActive(value.HasProgress);
                progressBar.minValue = 0f;
                progressBar.maxValue = 1f;
                progressBar.value = value.ProgressValue;
            }
            if (progressLabel != null)
            {
                progressLabel.text = value.ProgressLabel;
                progressLabel.gameObject.SetActive(value.HasProgress && !string.IsNullOrEmpty(value.ProgressLabel));
            }
            if (hintText != null) hintText.text = "空格 / 回车 / 点击确认关闭";
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = canvasGroup.interactable = true;
            if (confirmButton != null) confirmButton.interactable = false;
            transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            transition.Append(DOTween.To(() => canvasGroup.alpha, v => canvasGroup.alpha = v, 1f, enterSeconds));
            transition.OnComplete(() =>
            {
                transition = null;
                ready = true;
                if (confirmButton != null) confirmButton.interactable = true;
            });
        }

        public void Confirm()
        {
            if (!IsPlaying || !ready || exiting || lastConfirmFrame == Time.frameCount) return;
            lastConfirmFrame = Time.frameCount;
            exiting = true;
            if (confirmButton != null) confirmButton.interactable = false;
            transition?.Kill(false);
            transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            transition.Append(DOTween.To(() => canvasGroup.alpha, v => canvasGroup.alpha = v, 0f, exitSeconds));
            transition.OnComplete(() =>
            {
                Action callback = finished;
                Stop();
                callback?.Invoke();
            });
        }

        public void Stop()
        {
            transition?.Kill(false);
            transition = null;
            finished = null;
            cancelled = null;
            ready = exiting = false;
            IsPlaying = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = canvasGroup.interactable = false;
            }
        }

        private void Update()
        {
            if (!IsPlaying || exiting) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Cancel(); return; }
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame)) Confirm();
        }

        public void Cancel()
        {
            if (!IsPlaying || exiting) return;
            Action callback = cancelled;
            Stop();
            CloseSelf();
            callback?.Invoke();
        }

        protected override void OnClose() => Stop();
        protected override void OnWillHide() => Stop();
        private void OnDisable() => Stop();
        protected override void OnBeforeDestroy()
        {
            Stop();
            if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
            base.OnBeforeDestroy();
        }
    }
}
