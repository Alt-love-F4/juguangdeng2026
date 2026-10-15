using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// Common playback lifecycle only. Content and animation belong to each XXPanel prefab.
    /// UIKit owns prefab loading, instances and resource leases.
    /// </summary>
    public abstract class DayFlowPresentationPanel : UIPanel, IDayFlowPresentation
    {
        [Header("Prefab bindings")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform content;
        [SerializeField] private RawImage background;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private Button continueButton;
        [Header("Playback")]
        [SerializeField, Min(0f)] private float enterSeconds = .35f;
        [SerializeField, Min(0f)] private float exitSeconds = .18f;
        [SerializeField, Min(0f)] private float automaticHoldSeconds = 1.2f;

        private IDisposable pauseOwner;
        private Sequence animation;
        private Action finished;
        private bool exiting;
        private bool capturedPose;
        private Vector2 contentPosition;
        private Vector3 contentScale;
        private Vector3 backgroundScale;
        private Vector2 detailPosition;
        private Color detailColor;
        private string defaultDetail;

        public bool IsPlaying { get; private set; }
        protected RectTransform Content => content;
        protected RectTransform Background => background == null ? null : background.rectTransform;
        protected TMP_Text DetailText => detailText;
        protected float EnterSeconds => enterSeconds;
        protected virtual bool CompletesAutomatically => false;

        protected override void OnInit(IUIData data = null)
        {
            Spotlight.Audio.UIButtonAudio.Install(gameObject);
            CapturePose();
            defaultDetail = detailText == null ? string.Empty : detailText.text;
            if (continueButton != null) continueButton.onClick.AddListener(Continue);
        }

        protected override void OnOpen(IUIData data = null)
        {
            Stop();
            SetDetail(defaultDetail);
            UIKit.SetPanelModal(this, true);
        }

        protected override void OnShow()
        {
            // OnOpen runs before UIKit reparents the prefab. Apply sorting only after Show.
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
        }

        public void Play(Action onFinished)
        {
            if (canvasGroup == null || content == null || continueButton == null)
                throw new InvalidOperationException(GetType().Name + " has incomplete prefab bindings.");
            Stop();
            pauseOwner = PresentationPauseRegistry.Acquire(GetType().Name);
            string cue = this is DateChangePanel ? Spotlight.Audio.AudioKeys.Date :
                this is BusinessEndPanel ? Spotlight.Audio.AudioKeys.BusinessEnd : this is DayEndPanel ? Spotlight.Audio.AudioKeys.DayEnd : null;
            Spotlight.Audio.GameAudio.Play(cue, this);
            try
            {
            CapturePose();
            RestorePose();
            finished = onFinished;
            IsPlaying = true;
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            continueButton.gameObject.SetActive(!CompletesAutomatically);
            continueButton.interactable = false;
            animation = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            BuildEntrance(animation);
            if (CompletesAutomatically)
            {
                animation.AppendInterval(automaticHoldSeconds);
                animation.OnComplete(Continue);
            }
            else
            {
                animation.OnComplete(() =>
                {
                    if (IsPlaying && !exiting && continueButton != null)
                        continueButton.interactable = true;
                });
            }
            }
            catch { Stop(); throw; }
        }

        /// <summary>Called by the button after entrance, or by an automatic panel after its animation.</summary>
        public void Continue()
        {
            if (!IsPlaying || exiting) return;
            // Ignore early/double submissions while the entrance is still running.
            if (!CompletesAutomatically && !continueButton.interactable) return;
            exiting = true;
            continueButton.interactable = false;
            animation?.Kill(false);
            animation = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            animation.Append(DOTween.To(() => canvasGroup.alpha, value => canvasGroup.alpha = value, 0f, exitSeconds));
            animation.OnComplete(() =>
            {
                animation = null;
                IsPlaying = false;
                exiting = false;
                Action callback = finished;
                finished = null;
                pauseOwner?.Dispose();
                pauseOwner = null;
                callback?.Invoke();
            });
        }

        public void Stop()
        {
            Spotlight.Audio.GameAudio.StopOwner(this);
            pauseOwner?.Dispose();
            pauseOwner = null;
            finished = null;
            IsPlaying = exiting = false;
            animation?.Kill(false);
            animation = null;
            if (continueButton != null) continueButton.interactable = false;
            RestorePose();
        }

        protected virtual void BuildEntrance(Sequence sequence)
        {
            content.anchoredPosition = contentPosition + Vector2.down * 28f;
            sequence.Append(DOTween.To(() => canvasGroup.alpha, value => canvasGroup.alpha = value, 1f, enterSeconds));
            sequence.Join(DOTween.To(() => content.anchoredPosition, value => content.anchoredPosition = value, contentPosition, enterSeconds).SetEase(Ease.OutCubic));
        }

        protected void SetDetail(string text)
        {
            if (detailText != null) detailText.text = text;
        }

        private void CapturePose()
        {
            if (capturedPose) return;
            capturedPose = true;
            if (content != null)
            {
                contentPosition = content.anchoredPosition;
                contentScale = content.localScale;
            }
            if (background != null) backgroundScale = background.transform.localScale;
            if (detailText != null)
            {
                detailPosition = detailText.rectTransform.anchoredPosition;
                detailColor = detailText.color;
            }
        }

        private void RestorePose()
        {
            if (!capturedPose) return;
            if (content != null)
            {
                content.anchoredPosition = contentPosition;
                content.localScale = contentScale;
            }
            if (background != null) background.transform.localScale = backgroundScale;
            if (detailText != null)
            {
                detailText.rectTransform.anchoredPosition = detailPosition;
                detailText.color = detailColor;
            }
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        protected override void OnClose() => Stop();
        protected override void OnWillHide() => Stop();
        protected override void OnBeforeDestroy()
        {
            Stop();
            if (continueButton != null) continueButton.onClick.RemoveListener(Continue);
            base.OnBeforeDestroy();
        }
        private void OnDisable() => Stop();
    }
}
