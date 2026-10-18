using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// 主菜单编排动效：开场与「翻页」转场。只驱动现有 PSD 图层与转场纸页的透明度、位移与填充，
    /// 常驻氛围交给 <see cref="MainMenuAmbience"/>。所有序列在 OnDisable 统一回收并恢复终态，重复进入菜单不会叠加。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuMotion : MonoBehaviour
    {
        private enum Mode { None, Intro, Cover, Uncover }

        [Header("Layers")]
        [SerializeField] private CanvasGroup background;
        [SerializeField] private RectTransform backgroundMotion;
        [SerializeField] private Graphic title;
        [SerializeField] private Graphic subtitle;
        [SerializeField] private Image divider;
        [SerializeField] private CanvasGroup[] buttons;
        [SerializeField] private MainMenuAmbience ambience;

        [Header("Intro")]
        [SerializeField, Min(0f)] private float backgroundFade = 1.2f;
        [SerializeField, Range(1f, 1.2f)] private float backgroundStartScale = 1.12f;
        // 静止时保留的放大量，给常驻摇晃与鼠标视差留出不露边的余量。
        [SerializeField, Range(1f, 1.1f)] private float backgroundScale = 1.05f;
        [SerializeField, Min(0f)] private float titleDelay = .5f;
        [SerializeField, Min(0f)] private float subtitleDelay = 1f;
        [SerializeField, Min(0f)] private float buttonsDelay = 1.25f;
        [SerializeField, Min(0f)] private float buttonStagger = .12f;
        [SerializeField] private float titleRise = 10f;
        [SerializeField] private float buttonSlide = 28f;
        [SerializeField, Min(0f)] private float skipGuard = .2f;

        [Header("Page transition")]
        [SerializeField] private RectTransform page;
        [SerializeField] private Graphic pageSurface;
        [SerializeField, Min(.1f)] private float coverDuration = .5f;
        [SerializeField, Min(.1f)] private float uncoverDuration = .55f;
        [SerializeField] private float pageTilt = 5f;
        // 目标面板若没有接管（例如 Flow 尚未绑定），在这段时间后自动翻回。
        [SerializeField, Min(.1f)] private float coverTimeout = 2.5f;

        private Sequence playing;
        private Mode mode;
        private float playTime;
        private bool introPending;
        private bool uncoverPending;
        private bool captured;
        private bool covered;
        private bool blurred;
        private float coveredAt;
        private Action onCovered;
        private Vector2 titlePosition;
        private Vector2 subtitlePosition;
        private Vector2[] buttonPositions;

        public bool IntroPlaying => mode == Mode.Intro;
        /// <summary>翻页进行中或纸页仍盖住菜单；期间忽略重复点击。</summary>
        public bool Transitioning => covered || mode == Mode.Cover || mode == Mode.Uncover;

        /// <summary>由面板 OnOpen 调用：仅在菜单从隐藏进入可见时播放开场，栈内返回不重播。</summary>
        public void RequestIntro()
        {
            if (!isActiveAndEnabled) introPending = true;
        }

        public void CompleteIntro()
        {
            if (mode == Mode.Intro) Finish();
        }

        /// <summary>按钮滑出并翻入一张纸页盖住菜单；完全盖住后再打开目标面板。</summary>
        public void CoverThen(Action covering)
        {
            if (Transitioning || !isActiveAndEnabled) return;
            Capture();
            Stop();
            ShowFinal();
            onCovered = covering;
            if (ambience != null) ambience.TitleGleamEnabled = false;
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.PageTurn);
            Begin(Mode.Cover);
            for (int i = 0; i < buttonPositions.Length; i++)
            {
                CanvasGroup group = buttons[i];
                if (group == null) continue;
                group.blocksRaycasts = false;
                var rect = (RectTransform)group.transform;
                float at = i * .04f;
                playing.Insert(at, DOTween.To(() => group.alpha, v => group.alpha = v, 0f, .28f).SetEase(Ease.InSine));
                playing.Insert(at, DOTween.To(() => rect.anchoredPosition, p => rect.anchoredPosition = p,
                    buttonPositions[i] + Vector2.left * buttonSlide * 1.4f, .3f).SetEase(Ease.InCubic));
            }
            InsertPage(1f, 0f, coverDuration, Ease.InOutCubic, .08f);
        }

        /// <summary>菜单失去/恢复焦点时由面板通知：目标面板接管后停止超时；重新获得焦点时把纸页翻回。</summary>
        public void SetBlurred(bool value)
        {
            if (blurred == value) return;
            blurred = value;
            if (!blurred && covered && isActiveAndEnabled) Uncover();
        }

        private void Awake() => Capture();

        private void Capture()
        {
            if (captured) return;
            captured = true;
            if (title != null) titlePosition = title.rectTransform.anchoredPosition;
            if (subtitle != null) subtitlePosition = subtitle.rectTransform.anchoredPosition;
            buttonPositions = new Vector2[buttons?.Length ?? 0];
            for (int i = 0; i < buttonPositions.Length; i++)
                if (buttons[i] != null) buttonPositions[i] = ((RectTransform)buttons[i].transform).anchoredPosition;
        }

        private void OnEnable()
        {
            Capture();
            blurred = covered = false;
            if (ambience != null) ambience.TitleGleamEnabled = !introPending && !uncoverPending;
            // 被设置/存档页隐藏后再次显示：从盖住的纸页开始翻回；全新进入菜单则优先播放开场。
            if (introPending) PlayIntro();
            else if (uncoverPending) Uncover();
            else ShowFinal();
            introPending = uncoverPending = false;
        }

        private void OnDisable()
        {
            // 盖住时被隐藏 = 已切到目标面板，记下返回时需要翻页。
            bool wasCovering = covered || mode == Mode.Cover;
            Stop();
            covered = blurred = false;
            uncoverPending = wasCovering && !introPending;
            onCovered = null;
            ShowFinal();
        }

        private void Update()
        {
            if (covered && !blurred && Time.unscaledTime - coveredAt > coverTimeout)
            {
                Uncover();
                return;
            }
            if (playing == null) return;
            // 打开菜单的那次按键不算跳过；之后任意点击或按键直接跳到终态。
            if (mode == Mode.Intro && playTime >= skipGuard && SkipPressed())
            {
                Finish();
                return;
            }
            // 手动推进并限制单帧步长：加载卡顿的首帧不会吞掉大半段动画。
            playTime += Mathf.Min(Time.unscaledDeltaTime, .05f);
            if (playTime >= playing.Duration()) { Finish(); return; }
            playing.Goto(playTime);
            if (mode == Mode.Cover) return;
            // 滑入基本到位后再接收射线，悬停位移不会与入场位移争抢。
            foreach (CanvasGroup group in buttons)
                if (group != null) group.blocksRaycasts = group.alpha > .95f;
        }

        private static bool SkipPressed()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            return (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (keyboard != null && keyboard.anyKey.wasPressedThisFrame);
        }

        private void Begin(Mode next)
        {
            mode = next;
            playTime = 0f;
            playing = DOTween.Sequence().SetUpdate(true).SetAutoKill(false).Pause()
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void Stop()
        {
            playing?.Kill(false);
            playing = null;
            mode = Mode.None;
        }

        private void Finish()
        {
            Mode finished = mode;
            Stop();
            if (finished == Mode.Cover)
            {
                SetPage(0f);
                covered = true;
                coveredAt = Time.unscaledTime;
                Action callback = onCovered;
                onCovered = null;
                callback?.Invoke();
                return;
            }
            ShowFinal();
            if (ambience == null) return;
            ambience.TitleGleamEnabled = true;
            // 入场/返回的收尾：标题掠过一道纸面反光。
            ambience.PlayGleam();
        }

        private void Uncover()
        {
            covered = false;
            Stop();
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.PageTurn);
            ShowFinal();
            HideButtons();
            SetPage(0f);
            Begin(Mode.Uncover);
            InsertPage(0f, 1f, uncoverDuration, Ease.InOutCubic, 0f);
            // 纸页从左向右揭开，左侧按钮在揭开过半后依次滑回。
            SlideButtonsIn(uncoverDuration * .55f, .07f);
        }

        // 纸页位置 t：0 = 完全盖住，1 = 移出右侧屏幕；边缘随移动略微倾斜，像被手翻动的纸。
        private void InsertPage(float from, float to, float duration, Ease ease, float at)
        {
            if (page == null) return;
            SetPage(from);
            float value = from;
            playing.Insert(at, DOTween.To(() => value, v => { value = v; SetPage(v); }, to, duration).SetEase(ease));
        }

        private void SetPage(float t)
        {
            if (page == null) return;
            bool visible = t < .999f;
            if (page.gameObject.activeSelf != visible) page.gameObject.SetActive(visible);
            if (!visible) return;
            float width = page.rect.width;
            page.anchoredPosition = new Vector2(t * width * 1.08f, 0f);
            page.localRotation = Quaternion.Euler(0f, 0f, -pageTilt * Mathf.Sin(t * Mathf.PI));
            if (pageSurface != null) pageSurface.raycastTarget = true;
        }


        private void PlayIntro()
        {
            Stop();
            if (background != null) background.alpha = 0f;
            if (backgroundMotion != null) backgroundMotion.localScale = Vector3.one * backgroundStartScale;
            Prepare(title, titlePosition, titleRise);
            // 副标题与标题同图裁切，仅原位淡入，避免切口处的笔画错位。
            Prepare(subtitle, subtitlePosition, 0f);
            if (divider != null) divider.fillAmount = 0f;
            HideButtons();

            Begin(Mode.Intro);
            if (background != null)
                playing.Insert(0f, DOTween.To(() => background.alpha, v => background.alpha = v, 1f, backgroundFade).SetEase(Ease.OutSine));
            if (backgroundMotion != null)
                playing.Insert(0f, DOTween.To(() => backgroundMotion.localScale, v => backgroundMotion.localScale = v,
                    Vector3.one * backgroundScale, backgroundFade + 1f).SetEase(Ease.OutCubic));
            Reveal(title, titlePosition, titleDelay, .75f);
            if (divider != null)
                playing.Insert(subtitleDelay - .1f, DOTween.To(() => divider.fillAmount, v => divider.fillAmount = v, 1f, .7f).SetEase(Ease.OutCubic));
            Reveal(subtitle, subtitlePosition, subtitleDelay, .6f);
            SlideButtonsIn(buttonsDelay, buttonStagger);
        }

        private void HideButtons()
        {
            for (int i = 0; i < buttonPositions.Length; i++)
            {
                if (buttons[i] == null) continue;
                buttons[i].alpha = 0f;
                buttons[i].blocksRaycasts = false;
                ((RectTransform)buttons[i].transform).anchoredPosition = buttonPositions[i] + Vector2.left * buttonSlide;
            }
        }

        private void SlideButtonsIn(float delay, float stagger)
        {
            for (int i = 0; i < buttonPositions.Length; i++)
            {
                CanvasGroup group = buttons[i];
                if (group == null) continue;
                var rect = (RectTransform)group.transform;
                float at = delay + i * stagger;
                playing.Insert(at, DOTween.To(() => group.alpha, v => group.alpha = v, 1f, .45f).SetEase(Ease.OutSine));
                playing.Insert(at, DOTween.To(() => rect.anchoredPosition, p => rect.anchoredPosition = p, buttonPositions[i], .5f).SetEase(Ease.OutCubic));
            }
        }

        private static void Prepare(Graphic graphic, Vector2 position, float rise)
        {
            if (graphic == null) return;
            SetAlpha(graphic, 0f);
            graphic.rectTransform.anchoredPosition = position + Vector2.down * rise;
        }

        private void Reveal(Graphic graphic, Vector2 position, float at, float time)
        {
            if (graphic == null) return;
            RectTransform rect = graphic.rectTransform;
            playing.Insert(at, DOTween.To(() => graphic.color.a, v => SetAlpha(graphic, v), 1f, time).SetEase(Ease.OutSine));
            playing.Insert(at, DOTween.To(() => rect.anchoredPosition, p => rect.anchoredPosition = p, position, time + .1f).SetEase(Ease.OutCubic));
        }

        private void ShowFinal()
        {
            if (background != null) background.alpha = 1f;
            if (backgroundMotion != null) backgroundMotion.localScale = Vector3.one * backgroundScale;
            if (title != null) { SetAlpha(title, 1f); title.rectTransform.anchoredPosition = titlePosition; }
            if (subtitle != null) { SetAlpha(subtitle, 1f); subtitle.rectTransform.anchoredPosition = subtitlePosition; }
            if (divider != null) divider.fillAmount = 1f;
            SetPage(1f);
            for (int i = 0; i < buttonPositions.Length; i++)
            {
                if (buttons[i] == null) continue;
                buttons[i].alpha = 1f;
                buttons[i].blocksRaycasts = true;
                ((RectTransform)buttons[i].transform).anchoredPosition = buttonPositions[i];
            }
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
