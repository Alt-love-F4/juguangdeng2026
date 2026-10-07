using System;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// 到达新一天的时间切换演出：钟盘推进、拱窗/月色亮起、日期数字落位。
    /// 仍沿用 DayFlowPresentationPanel 的自动完成与非缩放时间生命周期。
    /// </summary>
    public sealed class DateChangePanel : DayFlowPresentationPanel
    {
        [Header("Time transition bindings")]
        [SerializeField] private TMP_Text dayNumberText;
        [SerializeField] private TMP_Text dayCaptionText;
        [SerializeField] private TMP_Text previousDateText;
        [SerializeField] private RectTransform transitionMarker;
        [SerializeField] private RectTransform clockHand;
        [SerializeField] private Graphic moonGlow;
        [SerializeField, Min(0f)] private float dateRevealSeconds = .6f;
        [SerializeField, Min(0f)] private float dialRotationDegrees = 18f;

        private string previousDate = string.Empty;
        private string currentDate = string.Empty;
        private Vector3 handRotation;
        private Vector3 markerScale;
        private Color dayNumberColor;
        private Color dayCaptionColor;
        private Color previousDateColor;
        private Color moonColor;

        protected override bool CompletesAutomatically => true;

        protected override void OnInit(IUIData data = null)
        {
            base.OnInit(data);
            if (clockHand != null) handRotation = clockHand.localEulerAngles;
            if (transitionMarker != null) markerScale = transitionMarker.localScale;
            if (dayNumberText != null) dayNumberColor = dayNumberText.color;
            if (dayCaptionText != null) dayCaptionColor = dayCaptionText.color;
            if (previousDateText != null) previousDateColor = previousDateText.color;
            if (moonGlow != null) moonColor = moonGlow.color;
        }

        public void SetDates(string oldDate, string newDate)
        {
            previousDate = oldDate ?? string.Empty;
            currentDate = newDate ?? string.Empty;
            SetDetail(currentDate);
            if (previousDateText != null) previousDateText.text = previousDate;
            if (dayNumberText != null) dayNumberText.text = ExtractDayNumber(currentDate);
        }

        protected override void BuildEntrance(Sequence sequence)
        {
            base.BuildEntrance(sequence);
            ResetCustomPose();

            if (dayNumberText != null)
            {
                dayNumberText.color = WithAlpha(dayNumberColor, 0f);
                dayNumberText.transform.localScale = Vector3.one * .72f;
                sequence.Join(dayNumberText.transform.DOScale(Vector3.one, dateRevealSeconds).SetEase(Ease.OutBack));
                sequence.Join(DOTween.To(() => dayNumberText.color.a,
                    value => dayNumberText.color = WithAlpha(dayNumberColor, value), 1f, dateRevealSeconds));
            }

            if (dayCaptionText != null)
            {
                dayCaptionText.color = WithAlpha(dayCaptionColor, 0f);
                sequence.Join(DOTween.To(() => dayCaptionText.color.a,
                    value => dayCaptionText.color = WithAlpha(dayCaptionColor, value), 1f, dateRevealSeconds * .8f));
            }

            if (previousDateText != null)
            {
                previousDateText.color = WithAlpha(previousDateColor, 0f);
                sequence.Join(DOTween.To(() => previousDateText.color.a,
                    value => previousDateText.color = WithAlpha(previousDateColor, value), .35f, dateRevealSeconds));
            }

            if (DetailText != null)
            {
                Color detailColor = DetailText.color;
                DetailText.color = WithAlpha(detailColor, 0f);
                sequence.Join(DOTween.To(() => DetailText.alpha,
                    value => DetailText.alpha = value, detailColor.a, dateRevealSeconds));
            }

            if (clockHand != null)
            {
                clockHand.localEulerAngles = handRotation + new Vector3(0f, 0f, -dialRotationDegrees);
                sequence.Join(clockHand.DOLocalRotate(handRotation, dateRevealSeconds * 1.25f)
                    .SetEase(Ease.OutCubic));
            }

            if (transitionMarker != null)
            {
                transitionMarker.localScale = Vector3.zero;
                sequence.Join(transitionMarker.DOScale(markerScale, dateRevealSeconds * .8f).SetEase(Ease.OutBack));
            }

            if (moonGlow != null)
            {
                moonGlow.color = WithAlpha(moonColor, 0f);
                sequence.Join(DOTween.To(() => moonGlow.color.a,
                    value => moonGlow.color = WithAlpha(moonColor, value), moonColor.a, dateRevealSeconds * 1.2f));
            }
        }

        private void ResetCustomPose()
        {
            if (clockHand != null) clockHand.localEulerAngles = handRotation;
            if (transitionMarker != null) transitionMarker.localScale = markerScale;
            if (dayNumberText != null)
            {
                dayNumberText.transform.localScale = Vector3.one;
                dayNumberText.color = dayNumberColor;
            }
            if (dayCaptionText != null) dayCaptionText.color = dayCaptionColor;
            if (previousDateText != null) previousDateText.color = previousDateColor;
            if (moonGlow != null) moonGlow.color = moonColor;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static string ExtractDayNumber(string value)
        {
            if (string.IsNullOrEmpty(value)) return "01";
            int end = value.IndexOf('日');
            if (end < 0) return "01";
            int start = end - 1;
            while (start >= 0 && char.IsDigit(value[start])) start--;
            string digits = value.Substring(start + 1, end - start - 1);
            if (digits.Length == 1) digits = "0" + digits;
            return digits.Length == 0 ? "01" : digits;
        }
    }
}
