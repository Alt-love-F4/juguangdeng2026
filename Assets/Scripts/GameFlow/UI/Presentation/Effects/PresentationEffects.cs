using System;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// UI-local effect runner. Channels compose instead of fighting over transforms:
    /// two portrait motion channels + two accent channels + screen shake + camera + overlay.
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
            public bool right;
        }
        private readonly PresentationEffectLibrary library;
        private readonly RectTransform stage;
        private readonly Image overlay;
        private readonly Portrait[] portraits = new Portrait[2];
        private readonly Channel[] channels = new Channel[7];
        private readonly Vector2 stagePosition;
        private readonly Vector3 stageScale;
        private readonly Color inactiveTint;
        private Vector2 screenOffset, cameraOffset;
        private float cameraZoom = 1f;
        private int shakePriority;
        private float focusSeconds;
        private bool disposed;
        public int ScreenShakePriority => channels[4].Active ? shakePriority : 0;
        public float OverlayAlpha => overlay.color.a;
        public float CameraZoom => cameraZoom;
        public bool IsPortraitTransitioning => channels[0].Active || channels[1].Active;
        public bool IsOverlayPlaying => channels[6].Active;

        public PresentationEffects(PresentationEffectLibrary library, RectTransform stage, Image overlay,
            Image left, Image leftFrame, Image right, Image rightFrame, Color inactiveTint, float focusSeconds)
        {
            if (library == null || stage == null || overlay == null || left == null || right == null)
                throw new ArgumentException("Presentation effects require a library, stage, overlay and two portrait slots.");
            this.library = library;
            this.stage = stage;
            this.overlay = overlay;
            this.inactiveTint = inactiveTint;
            this.focusSeconds = Mathf.Max(.001f, focusSeconds);
            stagePosition = stage.anchoredPosition;
            stageScale = stage.localScale;
            for (int i = 0; i < channels.Length; i++) channels[i] = new Channel();
            portraits[0] = Capture(left, leftFrame, false);
            portraits[1] = Capture(right, rightFrame, true);
            Reset();
        }
        private static Portrait Capture(Image image, Image frame, bool right)
        {
            RectTransform rect = frame != null ? frame.rectTransform : image.rectTransform;
            return new Portrait { image = image, frame = frame, rect = rect, position = rect.anchoredPosition,
                scale = rect.localScale, imageScale = image.transform.localScale,
                frameColor = frame != null ? frame.color : Color.clear, right = right };
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
                    if (slot < 0 || slot > 1) throw new ArgumentException("Portrait effect requires one occupied slot.");
                    return PortraitMotion(fx, slot, definition, completed);
                case PresentationEffectIds.AccentShakeStartle:
                    if (slot < 0 || slot > 1) throw new ArgumentException("Accent effect requires one occupied slot.");
                    Portrait p = portraits[slot];
                    return Start(2 + slot, definition.duration,
                        t => p.accent = new Vector2(Mathf.Sin(t * Mathf.PI * 16f) * definition.strength * (1f-t) * (1f-t), 0f),
                        () => p.accent = Vector2.zero,
                        () => { p.accent = Vector2.zero; completed?.Invoke(); });
                case PresentationEffectIds.ShakeLight:
                case PresentationEffectIds.ShakeHeavy:
                    if (channels[4].Active && shakePriority > definition.priority) return Rejected();
                    shakePriority = definition.priority;
                    return Start(4, definition.duration,
                        t => screenOffset = new Vector2(Mathf.Sin(t * Mathf.PI * 24f), Mathf.Sin(t * Mathf.PI * 31f) * .45f)
                            * definition.strength * (1f-t) * (1f-t),
                        () => screenOffset = Vector2.zero,
                        () => { screenOffset = Vector2.zero; completed?.Invoke(); });
                case PresentationEffectIds.FlashWhite:
                    return Start(6, definition.duration,
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
                p.offset = enter ? new Vector2((p.right ? 1f : -1f) * d.strength * (1f-ease), 16f * (1f-ease)) : Vector2.zero;
            }, () => { p.alpha = 1f; p.shadow = 0f; p.size = 1f; p.offset = Vector2.zero; },
            () => { p.alpha = exit ? 0f : 1f; p.shadow = 0f; p.size = 1f; p.offset = Vector2.zero; complete?.Invoke(); });
        }

        public void SetFocus(DialogueSpeaker speaker, bool immediate = false)
        {
            for (int i = 0; i < 2; i++)
            {
                Portrait p = portraits[i];
                p.focusTarget = speaker == DialogueSpeaker.Narrator || (i == 0 && speaker == DialogueSpeaker.Left)
                    || (i == 1 && speaker == DialogueSpeaker.Right) ? 1f : 0f;
                if (immediate) p.focus = p.focusTarget;
            }
        }
        public PresentationEffectHandle Nod(int slot)
        {
            if (slot < 0 || slot > 1) return Rejected();
            Portrait p = portraits[slot];
            return Start(slot + 2, .4f, t => p.accent = new Vector2(0f, -12f * Mathf.Sin(t * Mathf.PI * 4f)),
                () => p.accent = Vector2.zero, () => p.accent = Vector2.zero);
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
            CancelChannel(5);
            if (shake > 0f && !channels[4].Active)
            {
                shakePriority = 0;
                Start(4, duration, t => screenOffset = new Vector2(Mathf.Sin(t * Mathf.PI * 24f), Mathf.Sin(t * Mathf.PI * 31f) * .45f)
                    * shake * (1f-t) * (1f-t), () => screenOffset = Vector2.zero, () => screenOffset = Vector2.zero);
            }
            return Start(5, duration, t =>
            {
                cameraZoom = Mathf.Lerp(fromZoom, zoom, Smooth(t));
                cameraOffset = Vector2.Lerp(fromFocus, focus, Smooth(t));
            }, () => { cameraZoom = 1f; cameraOffset = Vector2.zero; }, completed);
        }
        public void ResetCamera() { CancelChannel(5); cameraZoom = 1f; cameraOffset = Vector2.zero; ApplyPose(); }
        public PresentationEffectHandle Fade(string type, float duration, Action atBlack = null, Action completed = null)
        {
            EnsureAlive(); ValidateFinite(duration, nameof(duration), 0f);
            if (type != "in" && type != "out" && type != "black" && type != "transition")
                throw new ArgumentException("Fade type must be in, out, black or transition.", nameof(type));
            bool fired = false;
            float from = type == "in" ? 1f : overlay.color.a;
            return Start(6, duration, t =>
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
            if (disposed) return;
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
            for (int i = 0; i < 2; i++)
                portraits[i].focus = Mathf.MoveTowards(portraits[i].focus, portraits[i].focusTarget, seconds / focusSeconds);
            ApplyPose();
        }
        private void ApplyPose()
        {
            if (stage == null) return;
            stage.anchoredPosition = stagePosition + cameraOffset + screenOffset;
            stage.localScale = stageScale * cameraZoom;
            for (int i = 0; i < 2; i++)
            {
                Portrait p = portraits[i];
                if (p.image == null || p.rect == null) continue;
                p.rect.anchoredPosition = p.position + p.offset + p.accent;
                p.rect.localScale = p.scale * p.size * Mathf.Lerp(1f, 1.025f, p.focus);
                Vector3 mirror = p.imageScale;
                mirror.x = Mathf.Abs(mirror.x) * (p.right ? -1f : 1f);
                p.image.transform.localScale = p.frame == null
                    ? mirror * p.size * Mathf.Lerp(1f, 1.025f, p.focus) : mirror;
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
            cameraZoom = 1f; cameraOffset = screenOffset = Vector2.zero; shakePriority = 0;
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
