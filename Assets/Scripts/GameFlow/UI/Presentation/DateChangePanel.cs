using System.Text.RegularExpressions;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// 「小历书」换日演出：夜色烛光 → 熄烛轻烟 → 晨光斜照入墙 → 撕下昨日历页 → 新页盖章。
    /// 只显示年/月/日，不显示钟点。仍沿用 DayFlowPresentationPanel 的自动完成与非缩放时间生命周期，
    /// 所有动效都插入同一个 Sequence，Stop/关闭时一并取消。
    /// </summary>
    public sealed class DateChangePanel : DayFlowPresentationPanel
    {
        [Header("Calendar")]
        [SerializeField] private RectTransform calendar;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private RectTransform newPage;
        [SerializeField] private TMP_Text newMonthText;
        [SerializeField] private TMP_Text newMonthLatinText;
        [SerializeField] private TMP_Text newDayText;
        [SerializeField] private RectTransform oldPage;
        [SerializeField] private CanvasGroup oldPageGroup;
        [SerializeField] private TMP_Text oldMonthText;
        [SerializeField] private TMP_Text oldMonthLatinText;
        [SerializeField] private TMP_Text oldDayText;
        [SerializeField] private Graphic stamp;
        [Header("Night to morning")]
        [SerializeField] private Graphic nightTint;
        [SerializeField] private Graphic windowLight;
        [SerializeField] private RectTransform flame;
        [SerializeField] private Graphic candleGlow;
        [SerializeField] private Graphic smoke;
        [SerializeField] private RectTransform[] motes;
        [SerializeField] private TMP_Text titleCaption;
        [SerializeField] private Graphic captionShade;
        [Header("Palette")]
        [SerializeField] private Color nightColor = new Color(.04f, .035f, .13f, .93f);
        [SerializeField] private Color dawnColor = new Color(1f, .6f, .32f, .3f);
        [SerializeField, Range(0f, 1f)] private float windowLightAlpha = .55f;
        [Header("Timing")]
        [SerializeField, Min(.1f)] private float tempo = 1f;

        private static readonly Regex DatePattern = new Regex(@"^(?<era>.*?)(?<y>\d+)\s*年\s*(?<m>\d+)\s*月\s*(?<d>\d+)\s*日");
        private static readonly string[] MonthNames = { "一", "二", "三", "四", "五", "六", "七", "八", "九", "十", "十一", "十二" };
        private static readonly string[] MonthNamesFr = { "JANVIER", "FÉVRIER", "MARS", "AVRIL", "MAI", "JUIN",
            "JUILLET", "AOÛT", "SEPTEMBRE", "OCTOBRE", "NOVEMBRE", "DÉCEMBRE" };

        private bool captured;
        private Vector2 oldPagePosition;
        private Vector2 windowLightPosition;
        private Vector2 smokePosition;
        private Vector3 stampScale;
        private Quaternion stampRotation;
        private Vector2 calendarPosition;
        private Vector2[] motePositions;
        private Color stampColor;
        private Color glowColor;
        private Color smokeColor;
        private Color titleColor;
        private Color shadeColor;
        private string oldHeader = string.Empty;
        private string newHeader = string.Empty;

        protected override bool CompletesAutomatically => true;
        public bool UseCoffeeCupExit { get; set; }
        protected override bool UsesCoffeeCupExit => UseCoffeeCupExit;

        protected override void OnInit(IUIData data = null)
        {
            base.OnInit(data);
            Capture();
        }

        public void SetDates(string oldDate, string newDate)
        {
            Capture();
            Almanac previous = Almanac.Parse(oldDate);
            Almanac current = Almanac.Parse(newDate);
            oldHeader = previous.Header;
            newHeader = current.Header;
            Apply(previous, oldMonthText, oldMonthLatinText, oldDayText);
            Apply(current, newMonthText, newMonthLatinText, newDayText);
            if (headerText != null) headerText.text = newHeader;
        }

        protected override void BuildEntrance(Sequence sequence)
        {
            base.BuildEntrance(sequence);
            Capture();
            ResetNight();
            float s = tempo;

            // 0 ~ 1.0s：夜里烛火摇曳。
            if (flame != null || candleGlow != null)
                sequence.Insert(0f, DOTween.To(() => 0f, Flicker, 1f, 1.05f * s).SetEase(Ease.Linear));

            // 1.0s：熄烛，一缕轻烟。
            if (flame != null)
                sequence.Insert(1f * s, flame.DOScale(new Vector3(.25f, 0f, 1f), .2f * s).SetEase(Ease.InBack));
            if (candleGlow != null)
                sequence.Insert(1f * s, Fade(candleGlow, glowColor, 0f, .4f * s));
            if (smoke != null)
            {
                sequence.Insert(1.05f * s, Fade(smoke, smokeColor, smokeColor.a, .25f * s));
                sequence.Insert(1.3f * s, Fade(smoke, smokeColor, 0f, .9f * s));
                sequence.Insert(1.05f * s, DOTween.To(() => smoke.rectTransform.anchoredPosition,
                    p => smoke.rectTransform.anchoredPosition = p, smokePosition + new Vector2(14f, 110f), 1.15f * s)
                    .SetEase(Ease.OutSine));
                sequence.Insert(1.05f * s, smoke.rectTransform.DOScale(new Vector3(1.35f, 1.2f, 1f), 1.15f * s));
            }

            // 1.05 ~ 2.6s：夜色转晨曦，晨光从窗外斜照到墙上。
            if (nightTint != null)
            {
                sequence.Insert(1.05f * s, DOTween.To(() => 0f,
                    t => nightTint.color = Color.Lerp(nightColor, dawnColor, t), 1f, .8f * s).SetEase(Ease.InOutSine));
                sequence.Insert(1.85f * s, Fade(nightTint, dawnColor, 0f, .75f * s).SetEase(Ease.OutSine));
            }
            if (windowLight != null)
            {
                Color light = windowLight.color;
                sequence.Insert(1.15f * s, Fade(windowLight, light, windowLightAlpha, 1.1f * s));
                sequence.Insert(1.15f * s, DOTween.To(() => windowLight.rectTransform.anchoredPosition,
                    p => windowLight.rectTransform.anchoredPosition = p, windowLightPosition, 1.5f * s)
                    .SetEase(Ease.OutCubic));
            }
            if (motes != null && motes.Length > 0)
                sequence.Insert(1.3f * s, DOTween.To(() => 0f, MoteDrift, 1f, 2.2f * s).SetEase(Ease.Linear));

            // 1.3 ~ 2.35s：撕下昨日那页，纸片飘落。
            if (oldPage != null)
            {
                sequence.Insert(1.3f * s, DOTween.To(() => 0f, LiftPage, 1f, .25f * s).SetEase(Ease.OutSine));
                sequence.Insert(1.55f * s, DOTween.To(() => 0f, FallPage, 1f, .85f * s).SetEase(Ease.Linear));
            }

            // 年份/纪元变化时，铭牌翻面换字。
            if (headerText != null && oldHeader != newHeader && !string.IsNullOrEmpty(oldHeader))
            {
                RectTransform header = headerText.rectTransform;
                headerText.text = oldHeader;
                sequence.Insert(1.55f * s, header.DOScaleY(0f, .12f * s).SetEase(Ease.InSine));
                sequence.InsertCallback(1.67f * s, () => headerText.text = newHeader);
                sequence.Insert(1.67f * s, header.DOScaleY(1f, .18f * s).SetEase(Ease.OutBack));
            }

            // 2.3s：新页落章，日历轻震。
            if (stamp != null)
            {
                RectTransform stampRect = stamp.rectTransform;
                sequence.Insert(2.3f * s, Fade(stamp, stampColor, stampColor.a, .1f * s));
                sequence.Insert(2.3f * s, stampRect.DOScale(stampScale, .16f * s).SetEase(Ease.InQuad));
                sequence.Insert(2.3f * s, stampRect.DOLocalRotateQuaternion(stampRotation, .16f * s).SetEase(Ease.InQuad));
            }
            if (calendar != null)
                sequence.Insert(2.44f * s, DOTween.To(() => 0f, t => calendar.anchoredPosition = calendarPosition
                    + Vector2.down * (7f * Mathf.Sin(t * Mathf.PI * 3f) * (1f - t)), 1f, .3f * s).SetEase(Ease.Linear));
            if (newDayText != null)
                sequence.Insert(2.44f * s, newDayText.rectTransform.DOPunchScale(Vector3.one * .08f, .35f * s, 6, .5f));

            // 2.45 ~ 3.1s：标题。
            if (captionShade != null)
                sequence.Insert(2.35f * s, Fade(captionShade, shadeColor, shadeColor.a, .5f * s));
            if (titleCaption != null)
            {
                sequence.Insert(2.45f * s, DOTween.To(() => titleCaption.alpha, a => titleCaption.alpha = a,
                    titleColor.a, .45f * s));
                sequence.Insert(2.45f * s, DOTween.To(() => titleCaption.characterSpacing,
                    v => titleCaption.characterSpacing = v, 18f, .65f * s).SetEase(Ease.OutCubic));
            }
            if (DetailText != null)
            {
                Color detail = DetailText.color;
                DetailText.alpha = 0f;
                sequence.Insert(2.65f * s, DOTween.To(() => DetailText.alpha, a => DetailText.alpha = a, detail.a, .45f * s));
            }
        }

        private void Capture()
        {
            if (captured) return;
            captured = true;
            if (oldPage != null) oldPagePosition = oldPage.anchoredPosition;
            if (windowLight != null) windowLightPosition = windowLight.rectTransform.anchoredPosition;
            if (smoke != null) { smokePosition = smoke.rectTransform.anchoredPosition; smokeColor = smoke.color; }
            if (stamp != null)
            {
                stampScale = stamp.rectTransform.localScale;
                stampRotation = stamp.rectTransform.localRotation;
                stampColor = stamp.color;
            }
            if (calendar != null) calendarPosition = calendar.anchoredPosition;
            if (candleGlow != null) glowColor = candleGlow.color;
            if (titleCaption != null) titleColor = titleCaption.color;
            if (captionShade != null) shadeColor = captionShade.color;
            if (motes == null) return;
            motePositions = new Vector2[motes.Length];
            for (int i = 0; i < motes.Length; i++)
                if (motes[i] != null) motePositions[i] = motes[i].anchoredPosition;
        }

        /// <summary>Opening pose: night, candle lit, yesterday's page still on the pad.</summary>
        private void ResetNight()
        {
            if (calendar != null) calendar.anchoredPosition = calendarPosition;
            if (headerText != null) { headerText.text = newHeader; headerText.rectTransform.localScale = Vector3.one; }
            if (oldPage != null)
            {
                oldPage.anchoredPosition = oldPagePosition;
                oldPage.localRotation = Quaternion.identity;
                oldPage.localScale = Vector3.one;
            }
            if (oldPageGroup != null) oldPageGroup.alpha = 1f;
            if (newDayText != null) newDayText.rectTransform.localScale = Vector3.one;
            if (stamp != null)
            {
                stamp.color = WithAlpha(stampColor, 0f);
                stamp.rectTransform.localScale = stampScale * 1.9f;
                stamp.rectTransform.localRotation = stampRotation * Quaternion.Euler(0f, 0f, 24f);
            }
            if (nightTint != null) nightTint.color = nightColor;
            if (windowLight != null)
            {
                windowLight.color = WithAlpha(windowLight.color, 0f);
                windowLight.rectTransform.anchoredPosition = windowLightPosition + new Vector2(-280f, 170f);
            }
            if (flame != null) flame.localScale = Vector3.one;
            if (candleGlow != null) candleGlow.color = glowColor;
            if (smoke != null)
            {
                smoke.color = WithAlpha(smokeColor, 0f);
                smoke.rectTransform.anchoredPosition = smokePosition;
                smoke.rectTransform.localScale = Vector3.one;
            }
            MoteDrift(0f);
            if (captionShade != null) captionShade.color = WithAlpha(shadeColor, 0f);
            if (titleCaption != null)
            {
                titleCaption.alpha = 0f;
                titleCaption.characterSpacing = 46f;
            }
        }

        private void Flicker(float t)
        {
            float time = t * 7f;
            float wave = Mathf.Sin(time * 5.3f) * .5f + Mathf.Sin(time * 8.9f + 1.3f) * .3f + Mathf.Sin(time * 15.1f) * .2f;
            if (flame != null)
            {
                flame.localScale = new Vector3(1f - wave * .05f, 1f + wave * .09f, 1f);
                flame.localRotation = Quaternion.Euler(0f, 0f, wave * 3f);
            }
            if (candleGlow != null) candleGlow.color = WithAlpha(glowColor, glowColor.a * (.88f + wave * .12f));
        }

        private void LiftPage(float t)
        {
            oldPage.anchoredPosition = oldPagePosition + new Vector2(6f * t, 4f * t);
            oldPage.localRotation = Quaternion.Euler(0f, 0f, -5f * t);
            oldPage.localScale = new Vector3(1f, 1f - .03f * t, 1f);
        }

        /// <summary>Torn leaf drifts down, swaying like paper rather than dropping like a stone.</summary>
        private void FallPage(float t)
        {
            float sway = Mathf.Sin(t * Mathf.PI * 2.1f);
            oldPage.anchoredPosition = oldPagePosition + new Vector2(6f - 170f * t + 70f * sway * t, 4f - 860f * Mathf.Pow(t, 1.7f) + 40f * t);
            oldPage.localRotation = Quaternion.Euler(0f, 0f, -5f + 26f * Mathf.Sin(t * Mathf.PI * 1.4f) + 14f * t);
            oldPage.localScale = new Vector3(1f - .1f * t, (1f - .03f) * (1f - .1f * t) * (.82f + .18f * Mathf.Abs(Mathf.Cos(t * Mathf.PI * 1.6f))), 1f);
            if (oldPageGroup != null) oldPageGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.45f, 1f, t));
        }

        private void MoteDrift(float t)
        {
            if (motes == null || motePositions == null) return;
            for (int i = 0; i < motes.Length; i++)
            {
                RectTransform mote = motes[i];
                if (mote == null) continue;
                float phase = i * .61f;
                mote.anchoredPosition = motePositions[i] + new Vector2(Mathf.Sin(t * 5f + phase) * 14f + t * 30f, t * (40f + i % 3 * 22f));
                float life = Mathf.Clamp01(t * 3f - i * .08f) * Mathf.Clamp01((1f - t) * 3f);
                if (mote.TryGetComponent(out Graphic graphic))
                    graphic.color = WithAlpha(graphic.color, life * (.55f + .45f * Mathf.Sin(t * 9f + phase * 3f)));
            }
        }

        private static void Apply(Almanac date, TMP_Text month, TMP_Text latin, TMP_Text day)
        {
            if (month != null) month.text = date.Month;
            if (latin != null) latin.text = date.MonthLatin;
            if (day != null) day.text = date.Day;
        }

        private static Tweener Fade(Graphic graphic, Color baseColor, float alpha, float duration)
            => DOTween.To(() => graphic.color.a, a => graphic.color = WithAlpha(baseColor, a), alpha, duration);

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private readonly struct Almanac
        {
            public readonly string Header;
            public readonly string Month;
            public readonly string MonthLatin;
            public readonly string Day;

            private Almanac(string header, string month, string latin, string day)
            {
                Header = header;
                Month = month;
                MonthLatin = latin;
                Day = day;
            }

            /// <summary>"王国纪元 670年10月6日" → 铭牌「王国纪元 670年」，页面「十月 / OCTOBRE / 6」。</summary>
            public static Almanac Parse(string value)
            {
                value = value ?? string.Empty;
                Match match = DatePattern.Match(value.Trim());
                if (!match.Success) return new Almanac(value, string.Empty, string.Empty, "·");
                string era = match.Groups["era"].Value.Trim();
                string header = (era.Length > 0 ? era + " " : string.Empty) + match.Groups["y"].Value + "年";
                int month = Mathf.Clamp(int.Parse(match.Groups["m"].Value), 1, 12);
                int day = int.Parse(match.Groups["d"].Value);
                return new Almanac(header, MonthNames[month - 1] + "月", MonthNamesFr[month - 1], day.ToString());
            }
        }
    }
}
