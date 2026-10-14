using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace Spotlight.GameFlow
{
    public sealed class LoadingPanel : GameFlowPanel
    {
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private RectTransform logo;
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private Image coffeeFill;
        [SerializeField] private TMP_Text percentageText;
        [SerializeField, Min(0.1f)] private float pulseDuration = 1.2f;
        private Tween pulse;
        private Sequence tipTransition;
        private bool splash;
        private Vector3 logoBaseScale;
        private bool tipInitiallyActive;
        private bool progressInitiallyActive;
        private bool percentageInitiallyActive;

        protected override void Awake()
        {
            base.Awake();
            if (logo != null) logoBaseScale = logo.localScale;
            tipInitiallyActive = tipText != null && tipText.gameObject.activeSelf;
            progressInitiallyActive = progressFill != null && progressFill.parent.gameObject.activeSelf;
            percentageInitiallyActive = percentageText != null && percentageText.gameObject.activeSelf;
        }

        public void SetSplash(bool value)
        {
            splash = value;
            if (tipText != null) tipText.gameObject.SetActive(!value && tipInitiallyActive);
            if (progressFill != null) progressFill.parent.gameObject.SetActive(!value && progressInitiallyActive);
            if (percentageText != null) percentageText.gameObject.SetActive(!value && percentageInitiallyActive);
        }

        public void SetProgress(float progress)
        {
            progress = float.IsNaN(progress) ? 0f : Mathf.Clamp01(progress);
            if (progressFill != null)
            {
                Vector2 anchorMax = progressFill.anchorMax;
                anchorMax.x = progress;
                progressFill.anchorMax = anchorMax;
            }
            if (coffeeFill != null) coffeeFill.fillAmount = progress;
            if (percentageText != null) percentageText.SetText("{0}%", Mathf.FloorToInt(progress * 100f));
        }

        /// <summary>Reset the long-loading visuals after opening the panel through UIKit.</summary>
        public void BeginLoading()
        {
            SetSplash(false);
            SetProgress(0f);
        }

        /// <summary>Show completion and close using the existing animated-close lifecycle.</summary>
        public void EndLoading(Action finished = null)
        {
            SetProgress(1f);
            CloseAnimated(finished);
        }

        public void SetTip(string tip)
        {
            if (tipText == null) return;
            tipTransition?.Kill();
            tipTransition = DOTween.Sequence().SetUpdate(true)
                .Append(DOTween.To(() => tipText.alpha, value => tipText.alpha = value, 0f, .1f))
                .AppendCallback(() => tipText.text = tip ?? string.Empty)
                .Append(DOTween.To(() => tipText.alpha, value => tipText.alpha = value, 1f, .1f));
        }

        protected override void OnOpen(YokiFrame.IUIData data = null)
        {
            base.OnOpen(data);
            SetProgress(0f);
        }

        protected override void OnShow()
        {
            base.OnShow();
            if (logo == null) return;
            pulse?.Kill(false);
            logo.localScale = logoBaseScale;
            pulse = logo.DOScale(logoBaseScale * 1.035f, pulseDuration).SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo).SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        protected override void OnWillHide()
        {
            base.OnWillHide();
        }

        protected override void OnClose()
        {
            StopPulse();
            base.OnClose();
        }

        protected override void OnBeforeDestroy()
        {
            StopPulse();
            base.OnBeforeDestroy();
        }

        private void StopPulse()
        {
            pulse?.Kill(false);
            tipTransition?.Kill();
            pulse = null;
            if (logo != null) logo.localScale = logoBaseScale;
        }
    }
}
