using System;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// UI-local effect runner. Channels compose instead of fighting over transforms:
    /// independent portrait motion/accent channels (two or three slots) + screen shake + camera + overlay.
    /// Tick is unscaled; cancellation restores owned poses and never calls stale completions.
    /// </summary>
    public sealed class PresentationEffects : IDisposable
    {
        private sealed class Channel
        {
            public PresentationEffectHandle handle;
            public float elapsed, duration;
            public Action<float> apply;
            public Action cancelled, completed;
            public bool Active => handle != null && !handle.IsDone;
        }
        private sealed class Portrait
        {
            public Image image, frame;
            public RectTransform rect;
            public Vector2 position, offset, accent;
            public Vector3 scale, imageScale;
            public Color frameColor;
            public float alpha = 1f, size = 1f, focus = 1f, focusTarget = 1f, shadow;
            public float enterDirection;
        }
        private readonly PresentationEffectLibrary library;
        private readonly RectTransform stage, dialogueBox;
        private readonly Vector2 dialoguePosition;
        private Vector2 dialogueOffset;
        private readonly Image overlay;
        private readonly Portrait[] portraits;
        private readonly Channel[] channels;
        private int AccentStart => portraits.Length;
        private int ShakeChannel => portraits.Length * 2;
        private int CameraChannel => ShakeChannel + 1;
        private int OverlayChannel => ShakeChannel + 2;
        private int BackgroundMotionChannel => ShakeChannel + 3;
        private int BackgroundFadeChannel => ShakeChannel + 4;
        private int DialogueShakeChannel => ShakeChannel + 5;
        private readonly RawImage background, previousBackground;
        private readonly Vector2 backgroundPosition;
        private readonly Vector3 backgroundScale;
        private Color backgroundTint = Color.white;
        private Vector2 backgroundOffset;
        private float backgroundZoom = 1f;
        public bool IsBackgroundTransitioning => channels[BackgroundFadeChannel].Active;
        private readonly Vector2 stagePosition;
        private readonly Vector3 stageScale;
        private readonly Color inactiveTint;
        private Vector2 screenOffset, cameraOffset;
        private float cameraZoom = 1f;
        private int shakePriority;
        private float focusSeconds;
        private bool disposed;
        public bool IsPaused { get; private set; }
        public void SetPaused(bool paused) => IsPaused = paused;
        public int ScreenShakePriority => channels[ShakeChannel].Active ? shakePriority : 0;
        public float OverlayAlpha => overlay.color.a;
        public float CameraZoom => cameraZoom;
        public bool IsPortraitTransitioning
        {
            get
            {
                for (int i = 0; i < portraits.Length; i++) if (channels[i].Active) return true;
                return false;
            }
        }
        public bool IsOverlayPlaying => channels[OverlayChannel].Active;

        public PresentationEffects(PresentationEffectLibrary library, RectTransform stage, Image overlay,
            Image left, Image leftFrame, Image right, Image rightFrame, Color inactiveTint, float focusSeconds,
            Image center = null, Image centerFrame = null, RawImage background = null, RectTransform dialogueBox = null)
        {
            if (library == null || stage == null || overlay == null || left == null || right == null)
                throw new ArgumentException("Presentation effects require a library, stage, overlay and two portrait slots.");
            this.library = library;
            this.stage = stage;
            this.dialogueBox = dialogueBox;
            if (dialogueBox != null) dialoguePosition = dialogueBox.anchoredPosition;
            this.overlay = overlay;
            this.inactiveTint = inactiveTint;
            this.background = background;
            if (background != null)
            {
                backgroundPosition = background.rectTransform.anchoredPosition;
                backgroundScale = background.rectTransform.localScale;
                var old = new GameObject("BackgroundCrossfade", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                old.transform.SetParent(background.transform, false);
                previousBackground = old.GetComponent<RawImage>();
                previousBackground.rectTransform.anchorMin = Vector2.zero; previousBackground.rectTransform.anchorMax = Vector2.one;
                previousBackground.rectTransform.offsetMin = previousBackground.rectTransform.offsetMax = Vector2.zero;
                previousBackground.raycastTarget = false; previousBackground.enabled = false;
            }
            this.focusSeconds = Mathf.Max(.001f, focusSeconds);
            stagePosition = stage.anchoredPosition;
            stageScale = stage.localScale;
            portraits = new Portrait[center != null ? 3 : 2];
            channels = new Channel[portraits.Length * 2 + 6];
            for (int i = 0; i < channels.Length; i++) channels[i] = new Channel();
            portraits[0] = Capture(left, leftFrame, -1f);
            portraits[1] = Capture(right, rightFrame, 1f);
            if (center != null) portraits[2] = Capture(center, centerFrame, 0f);
            Reset();
        }
        private static Portrait Capture(Image image, Image frame, float enterDirection)
        {
            RectTransform rect = frame != null ? frame.rectTransform : image.rectTransform;
            return new Portrait { image = image, frame = frame, rect = rect, position = rect.anchoredPosition,
                scale = UnmirroredScale(rect.localScale), imageScale = UnmirroredScale(image.transform.localScale),
                frameColor = frame != null ? frame.color : Color.clear, enterDirection = enterDirection };
        }

        private static Vector3 UnmirroredScale(Vector3 scale)
        { scale.x = Mathf.Abs(scale.x); return scale; }

        /// <summary>Refresh only the rest positions after the panel changes two/three-person layout.</summary>
        internal void RefreshPortraitPositions()
        {
            foreach (Portrait portrait in portraits) portrait.position = portrait.rect.anchoredPosition;
            ApplyPose();
        }

        private PresentationEffectHandle Start(int index, float duration, Action<float> apply, Action cancel = null, Action complete = null)
        {
            CancelChannel(index);
            var handle = new PresentationEffectHandle();
            Channel channel = channels[index];
            channel.handle = handle; channel.elapsed = 0f; channel.duration = Mathf.Max(.001f, duration);
            channel.apply = apply; channel.cancelled = cancel; channel.completed = complete;
            apply(0f);
            return handle;
        }
        private void CancelChannel(int index)
        {
            Channel channel = channels[index];
            if (!channel.Active) return;
            channel.handle.Finish(false);
            Action cancel = channel.cancelled;
            channel.apply = null; channel.cancelled = channel.completed = null;
            cancel?.Invoke();
        }
        private static PresentationEffectHandle Rejected()
        { var result = new PresentationEffectHandle(); result.Finish(false); return result; }
        private void EnsureAlive()
        { if (disposed) throw new ObjectDisposedException(nameof(PresentationEffects)); }

        public PresentationEffectHandle Play(string fx, int slot = -1, Action completed = null)
        {
            EnsureAlive();
            PresentationEffectDefinition definition = library.Find(fx); // Validate before cancelling a running cue.
            switch (fx)
            {
                case PresentationEffectIds.EnterShadowWalk:
                case PresentationEffectIds.PortraitFadeIn:
                case PresentationEffectIds.PortraitFadeOut:
                    if (slot < 0 || slot >= portraits.Length) throw new ArgumentException("Portrait effect requires one occupied slot.");
                    return PortraitMotion(fx, slot, definition, completed);
                case PresentationEffectIds.PortraitNod:
                    if (slot < 0 || slot >= portraits.Length) throw new ArgumentException("Nod requires one occupied slot.");
                    Portrait nod = portraits[slot];
                    return Start(AccentStart + slot, definition.duration,
                        t => nod.accent = new Vector2(0f, -definition.strength * Mathf.Sin(t * Mathf.PI)),
                        () => nod.accent = Vector2.zero, () => { nod.accent = Vector2.zero; completed?.Invoke(); });
                case PresentationEffectIds.BackgroundPan:
                    if (background == null) throw new InvalidOperationException("Background pan requires a background layer.");
                    float width = background.rectTransform.rect.width;
                    float amount = definition.strength;
                    return Start(BackgroundMotionChannel, definition.duration, t =>
                    {
                        backgroundOffset = new Vector2(width * amount * Mathf.Sin(t * Mathf.PI * 2f), 0f);
                        float coverage = Mathf.Max(definition.zoom, 1f + 2f * amount);
                        float edge = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f));
                        backgroundZoom = Mathf.Lerp(1f, coverage, edge);
                    }, ResetBackgroundMotion, () => { ResetBackgroundMotion(); completed?.Invoke(); });
                case PresentationEffectIds.BackgroundPanLeftToRight:
                    if (background == null) throw new InvalidOperationException("Background pan requires a background layer.");
                    float travel = background.rectTransform.rect.width * definition.strength;
                    float panZoom = Mathf.Max(definition.zoom, 1f + 2f * definition.strength);
                    return Start(BackgroundMotionChannel, definition.duration, t =>
                    {
                        // Cover both edges before moving, then travel once from left to right.
                        backgroundZoom = panZoom;
                        backgroundOffset = new Vector2(Mathf.Lerp(-travel, travel, Smooth(t)), 0f);
                    }, ResetBackgroundMotion, () => { ResetBackgroundMotion(); completed?.Invoke(); });
                case PresentationEffectIds.AccentShakeStartle:
                    if (slot < 0 || slot >= portraits.Length) throw new ArgumentException("Accent effect requires one occupied slot.");
                    Portrait p = portraits[slot];
                    return Start(AccentStart + slot, definition.duration,
                        t => p.accent = new Vector2(Mathf.Sin(t * Mathf.PI * 16f) * definition.strength * (1f-t) * (1f-t), 0f),
                        () => p.accent = Vector2.zero,
                        () => { p.accent = Vector2.zero; completed?.Invoke(); });
                case PresentationEffectIds.DialogueShake:
                    if (dialogueBox == null) throw new InvalidOperationException("Dialogue shake requires a dialogue box.");
                    return Start(DialogueShakeChannel, definition.duration,
                        t => dialogueOffset = new Vector2(Mathf.Sin(t * Mathf.PI * 24f), Mathf.Sin(t * Mathf.PI * 31f) * .45f)
                            * definition.strength * (1f-t) * (1f-t),
                        () => dialogueOffset = Vector2.zero,
                        () => { dialogueOffset = Vector2.zero; dialogueBox.anchoredPosition = dialoguePosition; completed?.Invoke(); });
                case PresentationEffectIds.ShakeLight:
                case PresentationEffectIds.ShakeHeavy:
                    if (channels[ShakeChannel].Active && shakePriority > definition.priority) return Rejected();
                    shakePriority = definition.priority;
                    return Start(ShakeChannel, definition.duration,
                        t => screenOffset = new Vector2(Mathf.Sin(t * Mathf.PI * 24f), Mathf.Sin(t * Mathf.PI * 31f) * .45f)
                            * definition.strength * (1f-t) * (1f-t),
                        () => screenOffset = Vector2.zero,
                        () => { screenOffset = Vector2.zero; completed?.Invoke(); });
                case PresentationEffectIds.FlashWhite:
                    return Start(OverlayChannel, definition.duration,
                        t => overlay.color = new Color(1f, 1f, 1f, definition.strength * Mathf.Sin(t * Mathf.PI)),
                        () => overlay.color = Color.clear,
                        () => { overlay.color = Color.clear; completed?.Invoke(); });
                case PresentationEffectIds.TransitionBlack:
                    return Fade("transition", definition.duration, null, completed);
                default: throw new ArgumentException("Unsupported effect ID: " + fx);
            }
        }
        private PresentationEffectHandle PortraitMotion(string fx, int slot, PresentationEffectDefinition d, Action complete)
        {
            Portrait p = portraits[slot];
            float startAlpha = p.alpha;
            bool enter = fx == PresentationEffectIds.EnterShadowWalk;
            bool exit = fx == PresentationEffectIds.PortraitFadeOut;
            return Start(slot, d.duration, t =>
            {
                float ease = Smooth(t);
                p.alpha = enter ? Mathf.Clamp01(t * 4f) : Mathf.Lerp(startAlpha, exit ? 0f : 1f, ease);
                p.shadow = enter ? 1f - Mathf.InverseLerp(.65f, 1f, t) : 0f;
                p.size = enter ? Mathf.Lerp(.84f, 1f, ease) : 1f;
                p.offset = enter ? new Vector2(p.enterDirection * d.strength * (1f-ease), 16f * (1f-ease)) : Vector2.zero;
            }, () => { p.alpha = 1f; p.shadow = 0f; p.size = 1f; p.offset = Vector2.zero; },
            () => { p.alpha = exit ? 0f : 1f; p.shadow = 0f; p.size = 1f; p.offset = Vector2.zero; complete?.Invoke(); });
        }

        public void SetFocus(DialogueSpeaker speaker, bool immediate = false)
        {
            for (int i = 0; i < portraits.Length; i++)
            {
                Portrait p = portraits[i];
                p.focusTarget = speaker == DialogueSpeaker.Narrator || (i == 0 && speaker == DialogueSpeaker.Left)
                    || (i == 1 && speaker == DialogueSpeaker.Right)
                    || (i == 2 && speaker == DialogueSpeaker.Center) ? 1f : 0f;
                if (immediate) p.focus = p.focusTarget;
            }
        }
        public PresentationEffectHandle Nod(int slot)
        {
            if (slot < 0 || slot >= portraits.Length) return Rejected();
            return Play(PresentationEffectIds.PortraitNod, slot);
        }
        private void ResetBackgroundMotion() { backgroundOffset = Vector2.zero; backgroundZoom = 1f; }
        public PresentationEffectHandle SetBackground(Texture texture, Color tint, float fadeSeconds = 0f)
        {
            EnsureAlive(); ValidateFinite(fadeSeconds, nameof(fadeSeconds), 0f);
            if (background == null) throw new InvalidOperationException("Presentation has no background layer.");
            CancelChannel(BackgroundMotionChannel); ResetBackgroundMotion();
            CancelChannel(BackgroundFadeChannel);
            bool same = background.texture == texture && backgroundTint == tint;
            Texture old = background.texture; Color oldTint = backgroundTint;
            bool wasVisible = background.enabled;
            background.texture = texture; background.color = tint; backgroundTint = tint; background.enabled = true;
            if (same || fadeSeconds <= 0f || !wasVisible)
            {
                previousBackground.enabled = false; ApplyPose();
                var done = new PresentationEffectHandle(); done.Finish(true); return done;
            }
            previousBackground.texture = old; previousBackground.color = oldTint; previousBackground.enabled = true;
            return Start(BackgroundFadeChannel, fadeSeconds, t =>
            { var color = oldTint; color.a *= 1f - Smooth(t); previousBackground.color = color; },
                () => previousBackground.enabled = false, () => previousBackground.enabled = false);
        }
        public PresentationEffectHandle Camera(float zoom, float shake, float duration, Vector2 focus, Action completed = null)
        {
            EnsureAlive();
            ValidateFinite(zoom, nameof(zoom), .01f); ValidateFinite(shake, nameof(shake), 0f);
            ValidateFinite(duration, nameof(duration), 0f);
            if (!IsFinite(focus.x) || !IsFinite(focus.y)) throw new ArgumentOutOfRangeException(nameof(focus));
            // A_CAMERA uses UI-stage space: never Camera.main, never scales the modal input blocker.
            float fromZoom = cameraZoom;
            Vector2 fromFocus = cameraOffset;
            CancelChannel(CameraChannel);
            if (shake > 0f && !channels[ShakeChannel].Active)
            {
                shakePriority = 0;
                Start(ShakeChannel, duration, t => screenOffset = new Vector2(Mathf.Sin(t * Mathf.PI * 24f), Mathf.Sin(t * Mathf.PI * 31f) * .45f)
                    * shake * (1f-t) * (1f-t), () => screenOffset = Vector2.zero, () => screenOffset = Vector2.zero);
            }
            return Start(CameraChannel, duration, t =>
            {
                cameraZoom = Mathf.Lerp(fromZoom, zoom, Smooth(t));
                cameraOffset = Vector2.Lerp(fromFocus, focus, Smooth(t));
            }, () => { cameraZoom = 1f; cameraOffset = Vector2.zero; }, completed);
        }
        public void ResetCamera() { CancelChannel(CameraChannel); cameraZoom = 1f; cameraOffset = Vector2.zero; ApplyPose(); }
        public PresentationEffectHandle Fade(string type, float duration, Action atBlack = null, Action completed = null)
        {
            EnsureAlive(); ValidateFinite(duration, nameof(duration), 0f);
            if (type != "in" && type != "out" && type != "black" && type != "transition")
                throw new ArgumentException("Fade type must be in, out, black or transition.", nameof(type));
            bool fired = false;
            float from = type == "in" ? 1f : overlay.color.a;
            return Start(OverlayChannel, duration, t =>
            {
                float a = type == "in" ? 1f - Smooth(t)
                    : type == "transition" ? (t < .5f ? Smooth(t * 2f) : 1f - Smooth((t-.5f) * 2f))
                    : Mathf.Lerp(from, 1f, Smooth(t));
                overlay.color = new Color(0f, 0f, 0f, a);
                if (!fired && (type == "transition" ? t >= .5f : type != "in" && t >= 1f))
                { fired = true; overlay.color = Color.black; atBlack?.Invoke(); }
            }, () => overlay.color = Color.clear, completed);
        }
        public void Tick(float seconds)
        {
            if (disposed || IsPaused) return;
            if (!IsFinite(seconds) || seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            for (int i = 0; i < channels.Length; i++)
            {
                Channel channel = channels[i];
                if (!channel.Active) continue;
                PresentationEffectHandle current = channel.handle;
                channel.elapsed += Mathf.Max(0f, seconds);
                float t = Mathf.Clamp01(channel.elapsed / channel.duration);
                channel.apply(t);
                // atBlack callbacks may replace a channel or stop the whole presentation.
                if (t >= 1f && channel.handle == current && !current.IsDone)
                {
                    current.Finish(true);
                    Action complete = channel.completed;
                    channel.apply = null; channel.cancelled = channel.completed = null;
                    ApplyPose();
                    complete?.Invoke();
                }
            }
            for (int i = 0; i < portraits.Length; i++)
                portraits[i].focus = Mathf.MoveTowards(portraits[i].focus, portraits[i].focusTarget, seconds / focusSeconds);
            ApplyPose();
        }
        private void ApplyPose()
        {
            if (stage == null) return;
            stage.anchoredPosition = stagePosition + cameraOffset + screenOffset;
            stage.localScale = stageScale * cameraZoom;
            if (dialogueBox != null && channels[DialogueShakeChannel].Active)
                dialogueBox.anchoredPosition = dialoguePosition + dialogueOffset;
            if (background != null)
            {
                background.rectTransform.anchoredPosition = backgroundPosition + backgroundOffset;
                background.rectTransform.localScale = backgroundScale * backgroundZoom;
            }
            for (int i = 0; i < portraits.Length; i++)
            {
                Portrait p = portraits[i];
                if (p.image == null || p.rect == null) continue;
                p.rect.anchoredPosition = p.position + p.offset + p.accent;
                p.rect.localScale = p.scale * p.size * Mathf.Lerp(1f, 1.025f, p.focus);
                // Every slot keeps the source sprite orientation, including after swaps/replacements.
                p.image.transform.localScale = p.frame == null
                    ? p.imageScale * p.size * Mathf.Lerp(1f, 1.025f, p.focus) : p.imageScale;
                Color tint = Color.Lerp(inactiveTint, Color.white, p.focus);
                tint = Color.Lerp(tint, new Color(.025f, .02f, .03f, tint.a), p.shadow);
                tint.a *= p.alpha;
                p.image.color = tint;
                if (p.frame != null)
                { Color frame = p.frameColor; frame.a *= p.alpha; p.frame.color = frame; }
            }
        }
        public void Reset()
        {
            for (int i = 0; i < channels.Length; i++) CancelChannel(i);
            cameraZoom = 1f; cameraOffset = screenOffset = dialogueOffset = Vector2.zero; shakePriority = 0;
            ResetBackgroundMotion();
            if (dialogueBox != null) dialogueBox.anchoredPosition = dialoguePosition;
            if (previousBackground != null) { previousBackground.enabled = false; previousBackground.texture = null; }
            if (overlay != null) overlay.color = Color.clear;
            foreach (Portrait p in portraits)
            {
                p.alpha = p.size = p.focus = p.focusTarget = 1f;
                p.shadow = 0f; p.offset = p.accent = Vector2.zero;
            }
            ApplyPose();
        }
        public void Dispose() { if (disposed) return; Reset(); disposed = true; }
        private static float Smooth(float t) => t * t * (3f - 2f * t);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void ValidateFinite(float value, string name, float min)
        { if (!IsFinite(value) || value < min) throw new ArgumentOutOfRangeException(name); }
    }
}
