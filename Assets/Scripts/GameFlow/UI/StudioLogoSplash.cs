using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// 有机游戏工坊（Organic Game Workshop）开场标志动画，在 Unity 启动画面之后、主菜单之前播放一次。
    /// 素材由 Tools/StudioSplash/generate_studio_splash.py 从团队图标切分生成；时间轴常量需与脚本保持一致。
    /// 整段画面是时间 t 的纯函数，跳过只是把遮罩提前拉满，不会留下半途状态。
    /// </summary>
    public sealed class StudioLogoSplash : MonoBehaviour
    {
        private const string ResourceRoot = "StudioSplash/";
        private const float LogoHalf = 320f;
        private const float SkipGuard = .25f;
        private const float SkipFade = .28f;
        private const float RevealSeconds = .45f;

        // --- timeline (seconds), mirrored in generate_studio_splash.py ---
        private const float SeedStart = .05f, Root = .35f;
        private const float Stem0 = .50f, Stem1 = 1.20f;
        private const float Fly0 = 1.30f, Impact = 1.85f;
        private static readonly float[] ButtonTimes = { 2.00f, 2.08f, 2.16f, 2.24f, 2.32f };
        private const float Word0 = 2.42f, LetterStep = .06f;
        private const float Sub0 = 2.90f, SubStep = .025f;
        private const float Shine = 3.35f;
        private const float Cover0 = 4.55f, CoverEnd = 5.05f;

        private static readonly Color Brand = new Color32(11, 50, 11, 255);
        private static readonly Color Lime = new Color32(164, 255, 74, 255);
        private static readonly Color Mint = new Color32(70, 255, 186, 255);
        private static readonly Color HotWhite = new Color32(236, 255, 240, 255);
        private static readonly Color Backdrop = new Color32(4, 9, 6, 255);
        private static readonly Color BackGlow = new Color32(34, 120, 52, 255);

        private static StudioLogoSplash active;
        private static bool played;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            active = null;
            played = false;
        }

        [Serializable] private sealed class LayoutFile { public SpriteEntry[] sprites; }
        [Serializable] private sealed class SpriteEntry { public string name; public float[] atlas; public float[] logo; public float[] anchor; }

        private sealed class Layer
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Home;
            public float Time;
        }

        private struct Particle
        {
            public Image Image;
            public float Start, Life, Drag, Gravity, Spin, Size;
            public Vector2 Origin, Velocity;
            public Color Color;
        }

        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly Dictionary<string, SpriteEntry> entries = new Dictionary<string, SpriteEntry>(StringComparer.Ordinal);
        private readonly List<Layer> vine = new List<Layer>();
        private readonly List<Layer> leaves = new List<Layer>();
        private readonly List<Layer> emblem = new List<Layer>();
        private readonly List<Layer> title = new List<Layer>();
        private readonly List<Layer> subtitle = new List<Layer>();
        private readonly List<Layer> ghosts = new List<Layer>();
        private readonly List<Image> leafFlashes = new List<Image>();
        private readonly List<Image> twinkles = new List<Image>();
        private readonly List<Vector2> twinklePoints = new List<Vector2>();
        private readonly List<Particle> particles = new List<Particle>();
        private readonly List<Image> spores = new List<Image>();
        private readonly List<Vector4> sporeSeeds = new List<Vector4>();

        private Texture2D atlas;
        private AudioClip clip;
        private AudioSource source;
        private CanvasGroup group;
        private RectTransform canvasRect, stage, logoRoot;
        private Image shine, backdrop, backGlow, halo, disc, ringA, ringB, flash, cover, seed, glowVine, glowEmblem, glowLeft, glowRight;
        private Layer ctrlLeft, ctrlRight, stem;
        private Vector2 vineFocus;
        private float time, skipAt = -1f, revealAt = -1f;
        private bool started;
        private Action revealed;

        /// <summary>True until the timeline (or a skip) has faded to the opaque end cover.</summary>
        public bool IsPlaying => revealAt < 0f && time < FinishTime;
        private float FinishTime => skipAt >= 0f ? Mathf.Min(CoverEnd, skipAt + SkipFade) : CoverEnd;

        /// <summary>Starts the splash once per run. Returns null when it already played or its assets are missing.</summary>
        public static StudioLogoSplash Play()
        {
            if (played || active != null) return null;
            played = true;
            var root = new GameObject("StudioLogoSplash", typeof(RectTransform));
            DontDestroyOnLoad(root);
            var splash = root.AddComponent<StudioLogoSplash>();
            if (!splash.Build())
            {
                Destroy(root);
                return null;
            }
            active = splash;
            return splash;
        }

        /// <summary>Removes a running splash immediately, e.g. when a test or tool starts a session directly.</summary>
        public static void DismissActive()
        {
            if (active != null) Destroy(active.gameObject);
            active = null;
        }

        /// <summary>Fades the opaque end cover away to show whatever was opened beneath it, then self-destructs.</summary>
        public void Reveal(Action completed = null)
        {
            if (revealAt >= 0f) return;
            revealed = completed;
            revealAt = Time.unscaledTime;
            if (cover != null) cover.color = Color.black;
            if (group != null) group.blocksRaycasts = false;
        }

        private void OnDestroy()
        {
            if (active == this) active = null;
            foreach (Sprite sprite in sprites.Values) if (sprite != null) Destroy(sprite);
            if (atlas != null) Resources.UnloadAsset(atlas);
            if (clip != null) Resources.UnloadAsset(clip);
        }

        private bool Build()
        {
            atlas = Resources.Load<Texture2D>(ResourceRoot + "studio_splash_atlas");
            TextAsset layoutAsset = Resources.Load<TextAsset>(ResourceRoot + "studio_splash_layout");
            if (atlas == null || layoutAsset == null)
            {
                Debug.LogWarning("[StudioLogoSplash] 缺少开场标志素材，跳过工作室开场动画。", this);
                return false;
            }
            LayoutFile layout = JsonUtility.FromJson<LayoutFile>(layoutAsset.text);
            Resources.UnloadAsset(layoutAsset);
            foreach (SpriteEntry entry in layout.sprites)
            {
                entries[entry.name] = entry;
                var rect = new Rect(entry.atlas[0], entry.atlas[1], entry.atlas[2], entry.atlas[3]);
                Sprite sprite = Sprite.Create(atlas, rect, new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                sprite.name = entry.name;
                sprites[entry.name] = sprite;
            }

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>();
            canvasRect = (RectTransform)transform;

            backdrop = FullScreen("Backdrop", Backdrop);
            backdrop.raycastTarget = true; // swallow clicks so the scene below never reacts to a skip
            backGlow = Free("BackGlow", "soft", transform, new Vector2(2200f, 1500f));
            for (int i = 0; i < 28; i++)
            {
                spores.Add(Free("Spore", "soft", transform, Vector2.one * 16f));
                sporeSeeds.Add(new Vector4(Rand(i, 1) * 2f - 1f, Rand(i, 2), .6f + Rand(i, 3) * 1.4f, Rand(i, 4)));
            }

            stage = Group("Stage", transform);
            logoRoot = Group("Logo", stage);
            logoRoot.sizeDelta = Vector2.one * LogoHalf * 2f;

            halo = Free("Halo", "soft", logoRoot, Vector2.one * 1150f);
            disc = Free("Disc", "disc", logoRoot, Vector2.one * LogoHalf * 2f);
            ringA = Free("RingA", "ring", logoRoot, Vector2.one * LogoHalf * 2f);
            ringB = Free("RingB", "ring", logoRoot, Vector2.one * LogoHalf * 2f);

            for (int i = 0; i < 6; i++) ghosts.Add(Place(i < 3 ? "ctrl_left" : "ctrl_right", logoRoot));
            ctrlLeft = Place("ctrl_left", logoRoot);
            ctrlRight = Place("ctrl_right", logoRoot);
            glowLeft = Place("glow_ctrl_left", ctrlLeft.Rect, ctrlLeft).Image;
            glowRight = Place("glow_ctrl_right", ctrlRight.Rect, ctrlRight).Image;

            stem = Place("vine_stem", logoRoot);
            stem.Image.type = Image.Type.Filled;
            stem.Image.fillMethod = Image.FillMethod.Vertical;
            stem.Image.fillOrigin = (int)Image.OriginVertical.Bottom;
            vine.Add(stem);
            float[] stemRect = entries["vine_stem"].logo;
            foreach (string name in new[] { "vine_root", "leaf_right", "leaf_left", "leaf_top" })
            {
                Layer leaf = Place(name, logoRoot);
                float y = entries[name].anchor[1];
                leaf.Time = name == "vine_root" ? Root
                    : Mathf.Lerp(Stem0, Stem1, Mathf.Clamp01((stemRect[1] + stemRect[3] - y) / stemRect[3]));
                vine.Add(leaf);
                leaves.Add(leaf);
                Image burst = Free("LeafFlash", "soft", logoRoot, Vector2.one * 90f);
                burst.rectTransform.anchoredPosition = leaf.Home;
                leafFlashes.Add(burst);
                for (int k = 0; k < 4; k++)
                {
                    float angle = (name == "leaf_left" ? 140f : name == "leaf_right" ? 40f : 90f) + (Rand(k, leaves.Count) - .5f) * 110f;
                    AddParticle("spark", leaf.Time, leaf.Home, Dir(angle) * (160f + 220f * Rand(k, 7 + leaves.Count)),
                        .45f + .25f * Rand(k, 9), 4f, 0f, 0f, 18f + 14f * Rand(k, 11), k % 2 == 0 ? Lime : Mint);
                }
            }
            glowVine = Place("glow_vine", logoRoot).Image;
            float[] topLeaf = entries["leaf_top"].logo, rootLeaf = entries["vine_root"].logo;
            vineFocus = ToUi(stemRect[0] + stemRect[2] * .5f, (topLeaf[1] + rootLeaf[1] + rootLeaf[3]) * .5f);

            foreach (string name in new[] { "dpad", "btn_top", "btn_right", "btn_bottom", "btn_left" })
                emblem.Add(Place(name, logoRoot));
            for (int i = 0; i < emblem.Count; i++) emblem[i].Time = ButtonTimes[i];
            for (int i = 0; entries.ContainsKey("organic_" + i); i++)
            {
                Layer letter = Place("organic_" + i, logoRoot);
                letter.Time = Word0 + i * LetterStep;
                title.Add(letter);
            }
            for (int i = 0; entries.ContainsKey("workshop_" + i); i++)
            {
                Layer letter = Place("workshop_" + i, logoRoot);
                letter.Time = Sub0 + i * SubStep;
                subtitle.Add(letter);
            }
            glowEmblem = Place("glow_emblem", logoRoot).Image;

            seed = Free("Seed", "spark", logoRoot, Vector2.one * 70f);

            var maskImage = Free("ShineMask", "disc", logoRoot, Vector2.one * LogoHalf * 2f);
            maskImage.color = Color.white; // the stencil is written by alpha clip, so the graphic must stay opaque
            maskImage.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            shine = Free("Shine", "shine", maskImage.transform, new Vector2(150f, 1000f));
            shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -24f);

            foreach (string name in new[] { "leaf_top", "btn_top", "organic_0", "leaf_left", "btn_right", "organic_6" })
            {
                SpriteEntry entry = entries[name];
                twinklePoints.Add(ToUi(entry.logo[0] + entry.logo[2] * (name.StartsWith("organic") ? .25f : .75f),
                    entry.logo[1] + entry.logo[3] * .25f));
                twinkles.Add(Free("Twinkle", "spark", logoRoot, Vector2.one * 64f));
            }

            for (int i = 0; i < 34; i++)
            {
                bool leaf = i % 3 == 0;
                float angle = i * 360f / 34f + (Rand(i, 21) - .5f) * 20f;
                AddParticle(leaf ? "leaf_particle" : "spark", Impact, Vector2.zero, Dir(angle) * (420f + 600f * Rand(i, 22)),
                    .75f + .5f * Rand(i, 23), 3.2f, leaf ? 260f : 60f, (Rand(i, 24) - .5f) * 720f,
                    (leaf ? 30f : 22f) + 20f * Rand(i, 25), i % 4 == 0 ? HotWhite : i % 2 == 0 ? Lime : Mint);
            }

            flash = FullScreen("Flash", Color.clear);
            cover = FullScreen("Cover", Color.clear);

            clip = Resources.Load<AudioClip>(ResourceRoot + "studio_splash_sfx");
            if (clip != null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.clip = clip;
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
            }
            Apply(0f);
            return true;
        }

        private void Update()
        {
            if (revealAt >= 0f)
            {
                float p = Mathf.Clamp01((Time.unscaledTime - revealAt) / RevealSeconds);
                group.alpha = 1f - EaseInOut(p);
                if (p >= 1f)
                {
                    Action callback = revealed;
                    revealed = null;
                    Destroy(gameObject);
                    callback?.Invoke();
                }
                return;
            }
            if (!started)
            {
                // Begin on the first rendered frame so the build hitch does not eat the opening beat.
                started = true;
                if (source != null) { source.volume = Spotlight.Audio.GameAudio.CategoryGain(Spotlight.Audio.AudioCategory.Sfx); source.Play(); }
                return;
            }
            time += Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
            // Stay locked to the soundtrack: a long frame must not drift picture and impact apart.
            if (source != null && source.isPlaying && skipAt < 0f)
            {
                float audioTime = source.timeSamples / (float)clip.frequency;
                if (Mathf.Abs(audioTime - time) > .05f) time = audioTime;
            }
            if (skipAt < 0f && time > SkipGuard && SkipPressed()) skipAt = time;
            if (skipAt >= 0f && source != null)
                source.volume = Spotlight.Audio.GameAudio.CategoryGain(Spotlight.Audio.AudioCategory.Sfx)
                    * (1f - Mathf.Clamp01((time - skipAt) / SkipFade));
            Apply(Mathf.Min(time, FinishTime));
        }

        private static bool SkipPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
            Gamepad pad = Gamepad.current;
            return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
        }

        private void Apply(float t)
        {
            float width = canvasRect.rect.width > 1f ? canvasRect.rect.width : 1920f;
            float fit = Mathf.Min(1.15f, width * .82f / (LogoHalf * 2f));
            float since = t - Impact;
            bool hit = since >= 0f;

            // Camera starts close on the sprouting vine and pulls back as the halves fly in;
            // then the impact punch, a slow cinematic push-in, and a decaying shake.
            float pull = EaseInOut(P(t, Fly0 - .1f, Impact - Fly0 + .1f));
            float zoom = fit * Mathf.Lerp(1.75f, 1f, pull);
            float punch = hit ? .11f * Mathf.Exp(-since * 7f) * Mathf.Cos(since * 22f) : 0f;
            logoRoot.localScale = Vector3.one * zoom * (1f + punch + .03f * EaseInOut(P(t, 2.3f, 2.75f)));
            logoRoot.anchoredPosition = -vineFocus * zoom * (1f - pull);
            float shake = hit ? 26f * Mathf.Exp(-since * 6.5f) : 0f;
            stage.anchoredPosition = new Vector2(Mathf.PerlinNoise(t * 38f, .3f) - .5f, Mathf.PerlinNoise(.7f, t * 38f) - .5f) * 2f * shake;
            stage.localRotation = Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(t * 20f, 5f) - .5f) * shake * .08f);

            // Living backdrop: green aura and drifting spores that are blown outward by the impact.
            float glow = .32f * EaseOut(P(t, .2f, 1.4f)) + (hit ? .7f * Mathf.Exp(-since * 3.2f) : 0f) + .05f * Mathf.Sin(t * 2.2f);
            backGlow.color = WithAlpha(BackGlow, glow);
            float burst = hit ? 1f - Mathf.Exp(-since * 5f) : 0f;
            for (int i = 0; i < spores.Count; i++)
            {
                Vector4 s = sporeSeeds[i];
                float y = Mathf.Repeat(s.y * 1300f + t * 28f * s.z, 1300f) - 650f;
                var p = new Vector2(s.x * width * .55f, y);
                p += p.normalized * 160f * burst;
                spores[i].rectTransform.anchoredPosition = p + new Vector2(Mathf.Sin(t * s.z + s.w * 6f) * 18f, 0f);
                spores[i].rectTransform.localScale = Vector3.one * (.6f + s.z * .7f);
                spores[i].color = WithAlpha(i % 3 == 0 ? Lime : Mint, (.12f + .22f * (.5f + .5f * Mathf.Sin(t * 3f * s.z + s.w * 9f))) * P(t, .1f, .8f));
            }

            Color vineTint = Color.Lerp(Mint, Lime, .5f + .5f * Mathf.Sin(t * 5.5f));
            float settle = EaseOut(P(t, Impact, .32f));

            // Seed falls onto the root, lands with a ring.
            float fall = P(t, SeedStart, Root - SeedStart);
            seed.rectTransform.anchoredPosition = leaves[0].Home + Vector2.up * 360f * (1f - fall * fall);
            seed.rectTransform.localScale = new Vector3(.55f, .55f + .9f * fall * (1f - P(t, Root, .05f)), 1f);
            seed.color = WithAlpha(HotWhite, P(t, SeedStart, .12f) * (1f - P(t, Root, .12f)));

            // Vine growth.
            stem.Image.fillAmount = P(t, Stem0, Stem1 - Stem0);
            for (int i = 0; i < leaves.Count; i++)
            {
                Layer leaf = leaves[i];
                float p = P(t, leaf.Time, .34f);
                float fold = i == 0 ? -40f : i == 1 ? 65f : i == 2 ? -65f : 55f;
                leaf.Rect.localScale = Vector3.one * EaseOutBack(p, 2.4f);
                leaf.Rect.localRotation = Quaternion.Euler(0f, 0f, fold * (1f - EaseOut(p)));
                leafFlashes[i].color = WithAlpha(HotWhite, t >= leaf.Time ? .9f * (1f - P(t, leaf.Time, .3f)) : 0f);
            }
            foreach (Layer part in vine) part.Image.color = hit ? Color.Lerp(HotWhite, Brand, settle) : vineTint;
            glowVine.color = WithAlpha(vineTint, hit ? 1f - settle : P(t, Stem1 - .15f, .3f) * (.85f + .1f * Mathf.Sin(t * 9f)));

            // Controller halves converge with motion-blur ghosts.
            FlyHalf(ctrlLeft, t, -1f);
            FlyHalf(ctrlRight, t, 1f);
            for (int i = 0; i < ghosts.Count; i++)
            {
                int lag = i % 3 + 1;
                FlyHalf(ghosts[i], t - lag * .022f, i < 3 ? -1f : 1f);
                ghosts[i].Image.color = WithAlpha(Lime, t >= Impact ? 0f : (.36f - lag * .1f) * (t >= Fly0 ? 1f : 0f));
            }
            Color halves = hit ? Color.Lerp(HotWhite, Brand, settle) : HotWhite;
            ctrlLeft.Image.color = ctrlRight.Image.color = WithAlpha(halves, t >= Fly0 ? 1f : 0f);
            glowLeft.color = glowRight.color = WithAlpha(Lime, hit ? .9f * (1f - settle) : .8f * P(t, Fly0, .2f));

            // Impact: flash, badge iris, double shockwave, emblem bloom.
            flash.color = WithAlpha(Color.white, hit ? 1f - EaseOut(P(t, Impact, .42f)) : 0f);
            disc.rectTransform.localScale = Vector3.one * (hit ? EaseOutBack(P(t, Impact, .5f), 1.6f) : 0f);
            disc.color = Color.white;
            Ring(ringA, t, Impact, .62f, 2.7f, Lime);
            Ring(ringB, t, Impact + .06f, .5f, 1.9f, HotWhite);
            halo.color = WithAlpha(Color.Lerp(Lime, Mint, .4f), hit ? .2f + .65f * Mathf.Exp(-since * 2.6f) + .05f * Mathf.Sin(t * 2.4f) : 0f);
            glowEmblem.color = WithAlpha(Lime, hit ? .85f * (1f - EaseOut(P(t, Impact, .55f))) : 0f);

            // D-pad and buttons pop clockwise, flashing lime before settling into brand green.
            foreach (Layer part in emblem)
            {
                float p = P(t, part.Time, .3f);
                part.Rect.localScale = Vector3.one * EaseOutBack(p, 2.6f);
                part.Rect.localRotation = Quaternion.Euler(0f, 0f, 90f * (1f - EaseOut(p)));
                part.Image.color = Color.Lerp(Lime, Brand, P(t, part.Time + .08f, .35f));
            }

            // ORGANIC drops in letter by letter; GAME WORKSHOP slides in behind it.
            foreach (Layer letter in title)
            {
                float p = P(t, letter.Time, .4f);
                letter.Rect.anchoredPosition = letter.Home + Vector2.up * 110f * (1f - EaseOutBack(p, 1.8f));
                float squash = EaseOutBack(p, 2.2f);
                letter.Rect.localScale = new Vector3(Mathf.Lerp(1.5f, 1f, squash), Mathf.Lerp(.4f, 1f, squash), 1f);
                letter.Image.color = WithAlpha(Color.Lerp(Lime, Brand, P(t, letter.Time + .12f, .35f)), Mathf.Clamp01(p * 3f));
            }
            foreach (Layer letter in subtitle)
            {
                float p = P(t, letter.Time, .32f);
                letter.Rect.anchoredPosition = letter.Home + Vector2.right * 46f * (1f - EaseOut(p));
                letter.Image.color = WithAlpha(Brand, p);
            }

            // Light sweep across the badge, then sparkles on the highlights.
            float sweep = P(t, Shine, .7f);
            shine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-520f, 520f, EaseInOut(sweep)), 0f);
            shine.color = WithAlpha(Color.white, .6f * Mathf.Sin(Mathf.PI * sweep));
            for (int i = 0; i < twinkles.Count; i++)
            {
                float p = P(t, Shine + .1f + i * .085f, .5f);
                float bloom = Mathf.Sin(Mathf.PI * p);
                twinkles[i].rectTransform.anchoredPosition = twinklePoints[i];
                twinkles[i].rectTransform.localScale = Vector3.one * bloom;
                twinkles[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, p * 120f);
                twinkles[i].color = WithAlpha(i % 2 == 0 ? HotWhite : Lime, bloom);
            }

            foreach (Particle particle in particles) Simulate(particle, t);

            float end = EaseIn(P(t, Cover0, CoverEnd - Cover0));
            if (skipAt >= 0f) end = Mathf.Max(end, P(t, skipAt, SkipFade));
            cover.color = WithAlpha(Color.black, end);
        }

        private void FlyHalf(Layer half, float t, float side)
        {
            float p = EaseIn3(P(t, Fly0, Impact - Fly0));
            half.Rect.anchoredPosition = half.Home + Vector2.right * side * 1250f * (1f - p);
            half.Rect.localRotation = Quaternion.Euler(0f, 0f, -side * 32f * (1f - p));
            half.Rect.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, p);
        }

        private static void Ring(Image ring, float t, float start, float duration, float size, Color color)
        {
            float p = P(t, start, duration);
            ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(.3f, size, EaseOut(p));
            ring.color = WithAlpha(color, t >= start ? .95f * (1f - p) : 0f);
        }

        private void AddParticle(string sprite, float start, Vector2 origin, Vector2 velocity, float life, float drag,
            float gravity, float spin, float size, Color color)
        {
            Image image = Free("Particle", sprite, logoRoot, Vector2.one * size);
            particles.Add(new Particle
            {
                Image = image, Start = start, Origin = origin, Velocity = velocity, Life = life,
                Drag = drag, Gravity = gravity, Spin = spin, Size = size, Color = color
            });
        }

        private static void Simulate(Particle particle, float t)
        {
            float age = t - particle.Start;
            if (age < 0f || age > particle.Life)
            {
                particle.Image.color = Color.clear;
                return;
            }
            float travel = (1f - Mathf.Exp(-particle.Drag * age)) / particle.Drag;
            RectTransform rect = particle.Image.rectTransform;
            rect.anchoredPosition = particle.Origin + particle.Velocity * travel + Vector2.down * .5f * particle.Gravity * age * age;
            rect.localRotation = Quaternion.Euler(0f, 0f, particle.Spin * age + Mathf.Atan2(particle.Velocity.y, particle.Velocity.x) * Mathf.Rad2Deg);
            float life = age / particle.Life;
            rect.localScale = Vector3.one * (1f - .4f * life);
            particle.Image.color = WithAlpha(particle.Color, 1f - life * life);
        }

        // ------------------------------------------------------------------ construction helpers
        private Layer Place(string name, RectTransform parent, Layer parentLayer = null)
        {
            SpriteEntry entry = entries[name];
            Image image = NewImage(name, parent, sprites[name]);
            RectTransform rect = image.rectTransform;
            float x = entry.logo[0], y = entry.logo[1], w = entry.logo[2], h = entry.logo[3];
            float ax = entry.anchor.Length == 2 ? entry.anchor[0] : x + w * .5f;
            float ay = entry.anchor.Length == 2 ? entry.anchor[1] : y + h * .5f;
            rect.sizeDelta = new Vector2(w, h);
            rect.pivot = new Vector2((ax - x) / w, 1f - (ay - y) / h);
            Vector2 home;
            if (parentLayer == null)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                home = ToUi(ax, ay);
            }
            else
            {
                // Children hang off the parent's pivot so they follow its fly-in/rotation exactly.
                rect.anchorMin = rect.anchorMax = parentLayer.Rect.pivot;
                home = ToUi(ax, ay) - parentLayer.Home;
            }
            rect.anchoredPosition = home;
            return new Layer { Rect = rect, Image = image, Home = home };
        }

        private Image Free(string name, string sprite, Transform parent, Vector2 size)
        {
            Image image = NewImage(name, parent, sprites[sprite]);
            image.rectTransform.sizeDelta = size;
            image.color = Color.clear;
            return image;
        }

        private Image FullScreen(string name, Color color)
        {
            Image image = NewImage(name, transform, null);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.color = color;
            return image;
        }

        private static RectTransform Group(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static Vector2 ToUi(float x, float y) => new Vector2(x - LogoHalf, LogoHalf - y);
        private static Vector2 Dir(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
        private static float Rand(int i, int salt) => Mathf.Repeat(Mathf.Sin(i * 12.9898f + salt * 78.233f) * 43758.5453f, 1f);
        private static Color WithAlpha(Color color, float alpha) { color.a = Mathf.Clamp01(alpha); return color; }

        // ------------------------------------------------------------------ easing
        private static float P(float t, float start, float duration) => Mathf.Clamp01((t - start) / duration);
        private static float EaseOut(float p) => 1f - (1f - p) * (1f - p) * (1f - p);
        private static float EaseIn(float p) => p * p;
        private static float EaseIn3(float p) => p * p * p;
        private static float EaseInOut(float p) => p * p * (3f - 2f * p);
        private static float EaseOutBack(float p, float s)
        {
            if (p <= 0f) return 0f;
            float q = p - 1f;
            return 1f + (s + 1f) * q * q * q + s * q * q;
        }
    }
}
