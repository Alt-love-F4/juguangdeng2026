using DG.Tweening;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>返家过渡：夜景缓慢展开，确认后打开夜晚活动页面。</summary>
    public sealed class PreNightPanel : DayFlowPresentationPanel
    {
        [SerializeField, Min(0f)] private float nightRevealSeconds = .7f;
        protected override void BuildEntrance(Sequence sequence)
        {
            base.BuildEntrance(sequence);
            if (Background == null) return;
            Vector3 target = Background.localScale;
            Background.localScale = target * 1.025f;
            sequence.Join(Background.DOScale(target, nightRevealSeconds).SetEase(Ease.OutSine));
        }
    }
}
