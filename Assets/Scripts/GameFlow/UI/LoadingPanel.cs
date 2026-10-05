using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Spotlight.GameFlow
{
    public sealed class LoadingPanel : GameFlowPanel
    {
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private RectTransform logo;
        [SerializeField] private RectTransform progressFill;
        [SerializeField, Min(0.1f)] private float pulseDuration = 1.2f;
        private Tween pulse;

        public void SetProgress(float progress)
        {
            if (progressFill == null) return;
            Vector2 anchorMax = progressFill.anchorMax;
            anchorMax.x = Mathf.Clamp01(progress);
            progressFill.anchorMax = anchorMax;
        }

        public void SetTip(string tip)
        {
            if (tipText != null) tipText.text = tip ?? string.Empty;
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
            logo.localScale = Vector3.one;
            pulse = logo.DOScale(1.035f, pulseDuration).SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo).SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        protected override void OnWillHide()
        {
            StopPulse();
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
            pulse = null;
            if (logo != null) logo.localScale = Vector3.one;
        }
    }
}
