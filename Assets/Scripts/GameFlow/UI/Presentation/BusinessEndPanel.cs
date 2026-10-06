using DG.Tweening;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>营业结束/结算入口：结算卡片展开，确认后进入城区。</summary>
    public sealed class BusinessEndPanel : DayFlowPresentationPanel
    {
        [SerializeField, Range(.5f, 1f)] private float cardStartScale = .94f;
        protected override void BuildEntrance(Sequence sequence)
        {
            base.BuildEntrance(sequence);
            Vector3 target = Content.localScale;
            Content.localScale = target * cardStartScale;
            sequence.Join(Content.DOScale(target, EnterSeconds).SetEase(Ease.OutBack));
        }
    }
}
