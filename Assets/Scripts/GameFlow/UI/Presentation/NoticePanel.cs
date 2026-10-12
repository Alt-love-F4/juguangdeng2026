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
        [SerializeField] private RectTransform newspaperLayout;
        [SerializeField] private RectTransform articleSurface;
        [SerializeField] private TMP_Text confirmLabel;

        private int textPage;
        private int textPageCount;
        private Vector2 layoutSize;

        private DG.Tweening.Sequence transition;
        private Action finished;
        private Action cancelled;
        private bool ready;
        private bool exiting;
        private int lastConfirmFrame;
        public bool IsPlaying { get; private set; }

        protected override void OnInit(IUIData data = null)
        {
            SetShowAnimation(null);
            SetHideAnimation(null);
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
            // UIKit's generic show animation may leave a reused panel at zero scale.
            // NoticePanel owns its fade through CanvasGroup, so always restore its UI transform.
            RectTransform rect = transform as RectTransform;
            if (rect != null)
            {
                rect.localScale = Vector3.one;
                rect.localPosition = Vector3.zero;
                rect.localRotation = Quaternion.identity;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
            }
            FitLayout();
        }

        private void FitLayout()
        {
            if (newspaperLayout == null || !(transform is RectTransform root)) return;
            layoutSize = root.rect.size;
            float scale = Mathf.Min(layoutSize.x / 1920f, layoutSize.y / 1080f);
            newspaperLayout.localScale = Vector3.one * Mathf.Max(.01f, scale);
        }

        public void Play(NoticeData value, Action onFinished = null, Action onCancelled = null)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (canvasGroup == null || confirmButton == null) throw new InvalidOperationException("NoticePanel has incomplete prefab bindings.");
            Stop();
            if (!value.Visible) { CloseSelf(); onFinished?.Invoke(); return; }
            OnShow();
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
                // Letterbox within the article photograph, never fit against the entire screen.
                noticeImage.uvRect = new Rect(0f, 0f, 1f, 1f);
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
                // Newspaper artwork supplies a default illustration. Copy stays in its own column.
                bodyText.text = value.Body;
                bodyText.overflowMode = TextOverflowModes.Page;
                bodyText.pageToDisplay = 1;
                bodyText.maxVisibleCharacters = int.MaxValue;
                bodyText.gameObject.SetActive(!string.IsNullOrWhiteSpace(value.Body));
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
            if (articleSurface != null) articleSurface.gameObject.SetActive(!string.IsNullOrWhiteSpace(value.Body));
            Canvas.ForceUpdateCanvases();
            FitLayout();
            if (bodyText != null && bodyText.gameObject.activeInHierarchy) bodyText.ForceMeshUpdate();
            textPage = 1;
            textPageCount = bodyText != null && bodyText.gameObject.activeInHierarchy ? Mathf.Max(1, bodyText.textInfo.pageCount) : 1;
            UpdatePageHint();
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
            if (textPage < textPageCount)
            {
                bodyText.pageToDisplay = ++textPage;
                UpdatePageHint();
                return;
            }
            exiting = true;
            if (confirmButton != null) confirmButton.interactable = false;
            transition?.Kill(false);
            transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            transition.Append(DOTween.To(() => canvasGroup.alpha, v => canvasGroup.alpha = v, 0f, exitSeconds));
            transition.OnComplete(() =>
            {
                Action callback = finished;
                Stop();
                CloseSelf();
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

        private void UpdatePageHint()
        {
            if (hintText != null) hintText.text = (textPageCount > 1 ? $"{textPage} / {textPageCount}  ·  " : string.Empty) + "空格 / 回车：继续   Esc：关闭";
            if (confirmLabel != null) confirmLabel.text = textPage < textPageCount ? "继续阅读" : "开始一天";
        }

        private void Update()
        {
            if (newspaperLayout != null && transform is RectTransform root && root.rect.size != layoutSize) FitLayout();
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
