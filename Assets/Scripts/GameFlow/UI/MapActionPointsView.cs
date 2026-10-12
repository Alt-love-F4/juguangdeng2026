using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>Fixed HUD state, independent of inventory visibility.</summary>
    public sealed class MapActionPointsView : MonoBehaviour
    {
        private TextMeshProUGUI fallback;
        private Image legacyFill;
        private TextMeshProUGUI date;
        private Image[] segments;
        private float nextRefresh;

        public void Initialize(TextMeshProUGUI label, Image fill, TextMeshProUGUI dateLabel, Image[] pointImages)
        {
            fallback = label; legacyFill = fill; date = dateLabel; segments = pointImages;
            Refresh();
        }
        private void OnEnable() => nextRefresh = 0f;
        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .1f;
            Refresh();
        }
        public void Refresh()
        {
            var actions = NarrativeRuntime.Instance?.Actions;
            if (actions == null) return;
            var points = actions.ActionPoints;
            float ratio = points.Max > 0 ? Mathf.Clamp01((float)points.Current / points.Max) : 0f;
            if (fallback != null) fallback.text = $"行动点 {points.Current} / {points.Max}    |    第 {actions.CurrentDay} 天";
            if (legacyFill != null) legacyFill.fillAmount = ratio;
            if (date != null)
            {
                var calendar = new System.DateTime(670, 10, 6).AddDays(Mathf.Max(0, actions.CurrentDay - 1));
                date.text = calendar.ToString("MM  dd");
                var month = date.transform.parent.Find("Month")?.GetComponent<TextMeshProUGUI>();
                var day = date.transform.parent.Find("Day")?.GetComponent<TextMeshProUGUI>();
                if (month != null) month.text = calendar.ToString("MM");
                if (day != null) day.text = calendar.ToString("dd");
            }
            if (segments == null) return;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null) continue;
                segments[i].fillMethod = Image.FillMethod.Horizontal;
                segments[i].fillOrigin = (int)Image.OriginHorizontal.Right;
                segments[i].fillAmount = Mathf.Clamp01(ratio * segments.Length - (segments.Length - 1 - i));
            }
        }
    }

}
