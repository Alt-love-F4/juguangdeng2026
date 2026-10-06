using DG.Tweening;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>开店演出：店铺背景由近至远展开；确认后才启动经营。</summary>
    public sealed class CafeOpeningPanel : DayFlowPresentationPanel
    {
        [SerializeField, Min(1f)] private float backgroundStartScale = 1.04f;
        [SerializeField, Min(0f)] private float openingSeconds = .65f;
        protected override void BuildEntrance(Sequence sequence)
        {
            base.BuildEntrance(sequence);
            if (Background == null) return;
            Vector3 target = Background.localScale;
            Background.localScale = target * backgroundStartScale;
            sequence.Join(Background.DOScale(target, openingSeconds).SetEase(Ease.OutSine));
        }
    }
}
