using DG.Tweening;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>日期演出：日期文字进入后自动结束，不需要点击。</summary>
    public sealed class DateChangePanel : DayFlowPresentationPanel
    {
        [SerializeField, Min(0f)] private float dateRevealSeconds = .45f;
        protected override bool CompletesAutomatically => true;

        public void SetDates(string previousDate, string currentDate)
            => SetDetail(previousDate + "  →  " + currentDate);

        protected override void BuildEntrance(Sequence sequence)
        {
            base.BuildEntrance(sequence);
            if (DetailText == null) return;
            Color color = DetailText.color;
            DetailText.color = new Color(color.r, color.g, color.b, 0f);
            sequence.Append(DOTween.To(() => DetailText.alpha, value => DetailText.alpha = value, color.a, dateRevealSeconds));
        }
    }
}
