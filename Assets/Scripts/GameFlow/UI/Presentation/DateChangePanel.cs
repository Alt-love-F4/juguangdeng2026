using System;
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
        [Header("Rotating date dial")]
        [SerializeField] private RectTransform dateDial;
        [SerializeField] private TMP_Text[] timeLabels;
        [SerializeField] private RectTransform selectionHalo;
        [SerializeField] private Graphic sun;
        [SerializeField] private Graphic moon;
        [SerializeField, Min(0f)] private float dateRevealSeconds = .85f;
        [SerializeField, Min(0f)] private float dialRotationDegrees = 30f;

        private string previousDate = string.Empty;
        private string currentDate = string.Empty;
        private Vector3 handRotation;
        private Vector3 markerScale;
        private Color dayNumberColor;
        private Color dayCaptionColor;
        private Color previousDateColor;
        private Color moonColor;
        private Color sunColor;
        private Color windowMoonColor;
        private Vector3 haloScale;
        private Vector2 sunPosition;
        private float dialAngle;
        private float targetDialAngle;

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
            if (sun != null) { sunColor = sun.color; sunPosition = sun.rectTransform.anchoredPosition; }
            if (moon != null) windowMoonColor = moon.color;
            if (selectionHalo != null) haloScale = selectionHalo.localScale;
        }

        public void SetDates(string oldDate, string newDate)
        {
            previousDate = oldDate ?? string.Empty;
            currentDate = newDate ?? string.Empty;
            SetDetail(currentDate);
            if (previousDateText != null) previousDateText.text = previousDate;
            if (dayNumberText != null) dayNumberText.text = ExtractDayNumber(currentDate);
            int day = ExtractDay(currentDate);
            targetDialAngle = (day - 1) * dialRotationDegrees;
            dialAngle = targetDialAngle;
            UpdateDial();
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

            if (dateDial != null)
            {
                dialAngle = targetDialAngle - dialRotationDegrees;
                UpdateDial();
                sequence.Join(DOTween.To(() => dialAngle, value =>
                {
                    dialAngle = value;
                    UpdateDial();
                }, targetDialAngle, dateRevealSeconds * 1.45f).SetEase(Ease.OutCubic));
            }

            if (selectionHalo != null)
            {
                selectionHalo.localScale = Vector3.zero;
                sequence.Join(selectionHalo.DOScale(haloScale, dateRevealSeconds * 1.2f).SetEase(Ease.OutBack));
            }

            if (sun != null && moon != null)
            {
                sun.color = WithAlpha(sunColor, 0f);
                moon.color = windowMoonColor;
                sequence.Join(DOTween.To(() => sun.color.a, a => sun.color = WithAlpha(sunColor, a),
                    sunColor.a, dateRevealSeconds * 1.5f));
                sequence.Join(DOTween.To(() => moon.color.a, a => moon.color = WithAlpha(windowMoonColor, a),
                    0f, dateRevealSeconds * 1.3f));
                sun.rectTransform.anchoredPosition = sunPosition + Vector2.down * 90f;
                sequence.Join(DOTween.To(() => sun.rectTransform.anchoredPosition,
                    position => sun.rectTransform.anchoredPosition = position,
                    sunPosition, dateRevealSeconds * 1.5f).SetEase(Ease.OutCubic));
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
            dialAngle = targetDialAngle;
            UpdateDial();
            if (selectionHalo != null) selectionHalo.localScale = haloScale;
            if (sun != null) { sun.color = sunColor; sun.rectTransform.anchoredPosition = sunPosition; }
            if (moon != null) moon.color = windowMoonColor;
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

        private void UpdateDial()
        {
            if (dateDial != null) dateDial.localRotation = Quaternion.Euler(0f, 0f, dialAngle);
            if (timeLabels == null) return;
            for (int i = 0; i < timeLabels.Length; i++)
            {
                TMP_Text label = timeLabels[i];
                if (label == null) continue;
                label.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -dialAngle);
                float distance = Mathf.Abs(Mathf.DeltaAngle(dialAngle - i * dialRotationDegrees, 0f));
                float focus = Mathf.Clamp01(1f - distance / dialRotationDegrees);
                label.color = Color.Lerp(new Color(.83f, .64f, .67f, .8f),
                    new Color(1f, .92f, .77f, 1f), focus);
                label.rectTransform.localScale = Vector3.one * Mathf.Lerp(.9f, 1.35f, focus);
            }
        }

        private static int ExtractDay(string value)
        {
            int end = value == null ? -1 : value.IndexOf('日');
            if (end < 0) return 1;
            int start = end - 1;
            while (start >= 0 && char.IsDigit(value[start])) start--;
            return int.TryParse(value.Substring(start + 1, end - start - 1), out int day)
                ? Mathf.Clamp(day, 1, 31) : 1;
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
