using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// 主菜单常驻氛围：蒸汽、云雾、飞鸟、浮尘、光束、飞散书页与标题扫光。
    /// 全部由一个不受 timeScale 影响的本地时钟程序化驱动；每个元素在自身周期首尾都处于透明状态，
    /// 因而循环无缝，菜单隐藏时随 GameObject 停止，不持有任何 tween 或协程。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuAmbience : MonoBehaviour
    {
        [Header("Background sway")]
        [SerializeField] private RectTransform backgroundMotion;
        // 漂移与视差按背景尺寸的比例计算，任何分辨率下都留在放大余量内（1080p 约 14px / 8px）。
        [SerializeField] private Vector2 drift = new Vector2(.0075f, .0075f);
        [SerializeField, Min(1f)] private float driftPeriod = 26f;
        [SerializeField, Range(0f, 1f)] private float driftRotation = .3f;
        // 鼠标视差：背景朝指针反方向轻移，像隔着橱窗看街景。
        [SerializeField] private Vector2 parallax = new Vector2(.0085f, .0085f);
        [SerializeField, Min(.1f)] private float parallaxSmoothing = 2.5f;

        [Header("Coffee steam")]
        [SerializeField] private Graphic[] steam;
        [SerializeField, Min(.5f)] private float steamCycle = 4.8f;
        [SerializeField, Range(0f, 1f)] private float steamAlpha = .62f;

        [Header("Drifting mist")]
        [SerializeField] private RawImage[] mist;
        [SerializeField] private float[] mistSpeed = { .006f, -.0045f };

        [Header("Warm light")]
        [SerializeField] private Graphic lightRays;
        [SerializeField, Range(0f, 1f)] private float lightAlpha = .28f;

        [Header("Birds")]
        [SerializeField] private Image[] birds;
        [SerializeField] private Sprite[] birdFrames;
        [SerializeField, Min(1f)] private float flockInterval = 19f;
        [SerializeField, Min(1f)] private float flightDuration = 9f;
        [SerializeField, Min(0f)] private float firstFlockDelay = 2.2f;

        [Header("Floating dust")]
        [SerializeField] private Graphic[] motes;
        [SerializeField, Range(0f, 1f)] private float moteAlpha = .7f;

        [Header("Loose page")]
        [SerializeField] private Graphic paperSlip;
        [SerializeField, Min(1f)] private float paperInterval = 16f;
        [SerializeField, Min(1f)] private float paperDuration = 6.5f;
        [SerializeField, Min(0f)] private float firstPaperDelay = 5f;

        [Header("Title gleam")]
        [SerializeField] private Graphic gleam;
        [SerializeField, Min(1f)] private float gleamInterval = 9f;
        [SerializeField, Min(.1f)] private float gleamDuration = 1.3f;

        private static readonly int[] WingCycle = { 0, 1, 2, 1 };
        private float time;
        private float gleamTime = -1f;
        private float nextGleam;
        private Vector4[] moteSeeds;
        private Vector2 backgroundPosition;
        private Vector2 pointer;
        private bool captured;

        /// <summary>开场与转场期间关闭周期扫光，避免与标题淡入抢戏。</summary>
        public bool TitleGleamEnabled { get; set; } = true;

        public void PlayGleam()
        {
            gleamTime = 0f;
            nextGleam = gleamInterval;
        }

        private void Awake()
        {
            captured = true;
            if (backgroundMotion != null) backgroundPosition = backgroundMotion.anchoredPosition;
            // 固定种子：每次进入菜单浮尘分布一致，便于截图回归。
            var random = new System.Random(1018);
            moteSeeds = new Vector4[motes?.Length ?? 0];
            for (int i = 0; i < moteSeeds.Length; i++)
                moteSeeds[i] = new Vector4((float)random.NextDouble(), (float)random.NextDouble(),
                    .012f + (float)random.NextDouble() * .02f, (float)random.NextDouble() * 6.283f);
        }

        // 时钟不在 OnEnable 归零：被设置页遮住时暂停，翻页回来后从原处继续，不会出现跳变。
        private void OnEnable()
        {
            gleamTime = -1f;
            nextGleam = gleamInterval;
            Apply(0f);
        }

        private void OnDisable()
        {
            if (!captured || backgroundMotion == null) return;
            backgroundMotion.anchoredPosition = backgroundPosition;
            backgroundMotion.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            float delta = Mathf.Min(Time.unscaledDeltaTime, .1f);
            time += delta;
            Apply(delta);
        }

        private void Apply(float delta)
        {
            ApplyBackground(delta);
            ApplySteam();
            ApplyMist();
            ApplyLight();
            ApplyBirds();
            ApplyMotes();
            ApplyPaper();
            ApplyGleam(delta);
        }

        private void ApplyBackground(float delta)
        {
            if (backgroundMotion == null) return;
            Mouse mouse = Mouse.current;
            Vector2 target = Vector2.zero;
            if (mouse != null && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 position = mouse.position.ReadValue();
                // 逐轴限幅：指针移出窗口也不会把背景推出放大余量之外。
                target = new Vector2(Mathf.Clamp(position.x / Screen.width * 2f - 1f, -1f, 1f),
                    Mathf.Clamp(position.y / Screen.height * 2f - 1f, -1f, 1f));
            }
            pointer = Vector2.Lerp(pointer, target, 1f - Mathf.Exp(-parallaxSmoothing * delta));
            // 两个互质倍频叠出一条不重复感的椭圆漂移；周期整数倍，长时间循环无跳变。
            float angle = time / driftPeriod * Mathf.PI * 2f;
            Vector2 sway = new Vector2(Mathf.Sin(angle * 2f) * .7f + Mathf.Sin(angle * 3f + .8f) * .3f,
                Mathf.Sin(angle * 3f + 1f) * .7f + Mathf.Sin(angle * 5f) * .3f);
            Vector2 size = backgroundMotion.rect.size;
            backgroundMotion.anchoredPosition = backgroundPosition
                + Vector2.Scale(Vector2.Scale(sway, drift) - Vector2.Scale(pointer, parallax), size);
            backgroundMotion.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(angle * 2f + .4f) * driftRotation);
        }

        private void ApplySteam()
        {
            if (steam == null) return;
            for (int i = 0; i < steam.Length; i++)
                if (steam[i] != null)
                    ApplyWisp(steam[i], Mathf.Repeat(time / steamCycle + i / (float)steam.Length, 1f), i);
        }

        // 每缕蒸汽在生命周期 p∈[0,1) 内上升、向右扩散并淡出；两端透明，循环无缝。
        private void ApplyWisp(Graphic wisp, float p, int index)
        {
            float rise = 1f - (1f - p) * (1f - p);
            float sway = Mathf.Sin((p * 1.3f + index * .31f) * Mathf.PI * 2f) * .08f * p;
            float x = .45f + sway + .14f * p;
            float y = Mathf.Lerp(.2f, .78f, rise);
            float width = Mathf.Lerp(.26f, .56f, p) * .5f;
            float height = Mathf.Lerp(.3f, .6f, p) * .5f;
            RectTransform rect = wisp.rectTransform;
            rect.anchorMin = new Vector2(x - width, y - height);
            rect.anchorMax = new Vector2(x + width, y + height);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-8f, 12f, p) + (index - 1.5f) * 5f);
            float fade = Mathf.SmoothStep(0f, 1f, p / .22f) * (1f - Mathf.SmoothStep(0f, 1f, (p - .4f) / .6f));
            SetAlpha(wisp, steamAlpha * fade);
        }

        private void ApplyMist()
        {
            if (mist == null) return;
            for (int i = 0; i < mist.Length; i++)
            {
                if (mist[i] == null) continue;
                Rect uv = mist[i].uvRect;
                float speed = mistSpeed != null && i < mistSpeed.Length ? mistSpeed[i] : .005f;
                uv.x = Mathf.Repeat(i * .37f + time * speed, 1f);
                mist[i].uvRect = uv;
            }
        }

        private void ApplyLight()
        {
            if (lightRays == null) return;
            float breathe = .72f + .28f * Mathf.Sin(time * Mathf.PI * 2f / 11f);
            SetAlpha(lightRays, lightAlpha * breathe);
            lightRays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * Mathf.PI * 2f / 17f) * 1.2f);
        }

        private void ApplyBirds()
        {
            if (birds == null || birdFrames == null || birdFrames.Length < 3) return;
            float clock = time - firstFlockDelay + flockInterval;
            int pass = Mathf.FloorToInt(clock / flockInterval);
            float t = clock - pass * flockInterval;
            bool flying = time >= firstFlockDelay && t < flightDuration;
            float p = t / flightDuration;
            // 鸟群从右侧屋顶后起飞，斜穿两座建筑之间较亮的雾霭，再升入左上夜空；每轮略换高度。
            float lift = (pass % 3) * .04f;
            for (int i = 0; i < birds.Length; i++)
            {
                Image bird = birds[i];
                if (bird == null) continue;
                if (!flying) { SetAlpha(bird, 0f); continue; }
                float lag = i * .06f;
                float travel = Mathf.Clamp01(p * 1.2f - lag);
                float curve = travel * travel;
                float x = Mathf.Lerp(1.05f, .3f, travel) + (i % 2 == 0 ? .012f : -.012f) * i;
                float y = Mathf.Lerp(.5f + lift, .98f, curve) + Mathf.Sin(travel * 11f + i * 1.7f) * .006f + (i % 2) * .018f;
                float size = (1f - i * .12f) * Mathf.Lerp(1f, .55f, travel);
                RectTransform rect = bird.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(x, y);
                rect.localScale = new Vector3(size, size, 1f);
                rect.localRotation = Quaternion.Euler(0f, 0f, -18f * travel + Mathf.Sin(time * 2f + i) * 4f);
                bird.sprite = birdFrames[WingCycle[(int)((time * 7.5f + i * 1.3f) % WingCycle.Length)]];
                float fade = Mathf.SmoothStep(0f, 1f, travel / .06f) * (1f - Mathf.SmoothStep(0f, 1f, (travel - .8f) / .2f));
                SetAlpha(bird, .9f * fade);
            }
        }

        private void ApplyMotes()
        {
            if (motes == null) return;
            for (int i = 0; i < motes.Length && i < moteSeeds.Length; i++)
            {
                Graphic mote = motes[i];
                if (mote == null) continue;
                Vector4 seed = moteSeeds[i];
                float progress = Mathf.Repeat(seed.y + time * seed.z, 1f);
                float x = .03f + seed.x * .6f + Mathf.Sin(time * .23f + seed.w) * .02f;
                float y = Mathf.Lerp(.05f, .62f, progress);
                mote.rectTransform.anchorMin = mote.rectTransform.anchorMax = new Vector2(x, y);
                float twinkle = .55f + .45f * Mathf.Sin(time * 1.6f + seed.w * 3f);
                SetAlpha(mote, moteAlpha * Mathf.Sin(progress * Mathf.PI) * twinkle);
            }
        }

        private void ApplyPaper()
        {
            if (paperSlip == null) return;
            float clock = time - firstPaperDelay;
            float t = Mathf.Repeat(clock, paperInterval);
            if (clock < 0f || t >= paperDuration) { SetAlpha(paperSlip, 0f); return; }
            float p = t / paperDuration;
            // 从右侧纸堆被风掀起，绕过标题下方划一道弧线，飘出画面右缘。
            Vector2 start = new Vector2(.7f, .5f), control = new Vector2(.82f, .76f), end = new Vector2(1.06f, .62f);
            float u = 1f - p;
            Vector2 position = u * u * start + 2f * u * p * control + p * p * end;
            position.x += Mathf.Sin(p * 10f) * .012f;
            RectTransform rect = paperSlip.rectTransform;
            rect.anchorMin = rect.anchorMax = position;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(p * 7f) * 22f - p * 40f);
            float flutter = Mathf.Cos(p * Mathf.PI * 5f);
            float scale = Mathf.Lerp(1f, .8f, p);
            rect.localScale = new Vector3(scale * (Mathf.Abs(flutter) * .75f + .25f) * Mathf.Sign(flutter + .001f), scale, 1f);
            float fade = Mathf.SmoothStep(0f, 1f, p / .1f) * (1f - Mathf.SmoothStep(0f, 1f, (p - .65f) / .35f));
            SetAlpha(paperSlip, .95f * fade);
        }

        private void ApplyGleam(float delta)
        {
            if (gleam == null) return;
            if (gleamTime < 0f)
            {
                if (TitleGleamEnabled && (nextGleam -= delta) <= 0f) PlayGleam();
                else { SetAlpha(gleam, 0f); return; }
            }
            gleamTime += delta;
            float p = gleamTime / gleamDuration;
            if (p >= 1f) { gleamTime = -1f; SetAlpha(gleam, 0f); return; }
            float x = Mathf.Lerp(-.2f, 1.2f, Mathf.SmoothStep(0f, 1f, p));
            RectTransform rect = gleam.rectTransform;
            rect.anchorMin = new Vector2(x - .09f, -.1f);
            rect.anchorMax = new Vector2(x + .09f, 1.1f);
            SetAlpha(gleam, .6f * Mathf.Sin(p * Mathf.PI));
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            if (Mathf.Approximately(color.a, alpha)) return;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
