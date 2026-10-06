using DG.Tweening;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>一天结束：文字缓慢显现，确认后进入下一天。</summary>
    public sealed class DayEndPanel : DayFlowPresentationPanel
    {
        [SerializeField, Min(0f)] private float textRevealSeconds = .55f;
        protected override void BuildEntrance(Sequence sequence)
        {
            base.BuildEntrance(sequence);
            if (DetailText == null) return;
            Color color = DetailText.color;
            DetailText.color = new Color(color.r, color.g, color.b, 0f);
            sequence.Append(DOTween.To(() => DetailText.alpha, value => DetailText.alpha = value, color.a, textRevealSeconds));
        }
    }
}
