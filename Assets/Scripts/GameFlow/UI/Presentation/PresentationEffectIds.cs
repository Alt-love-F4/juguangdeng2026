using System;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Stable effect IDs from the planning/NCS vocabulary, not an NCS runtime dependency.</summary>
    public static class PresentationEffectIds
    {
        public const string EnterShadowWalk = "fx_enter_shadow_walk";
        public const string AccentShakeStartle = "fx_accent_shake_startle";
        public const string ShakeLight = "fx_shake_light";
        public const string ShakeHeavy = "fx_shake_heavy";
        public const string FlashWhite = "fx_flash_white";
        public const string TransitionBlack = "fx_transition_black";
        public const string PortraitFadeOut = "fx_portrait_fade_out";
        public const string PortraitFadeIn = "fx_portrait_fade_in";
        public const string PortraitNod = "fx_portrait_nod";
        public const string BackgroundPan = "fx_background_pan";
    }

    public enum PresentationEffectStatus { Playing, Completed, Cancelled }

    /// <summary>A cancelled/replaced cue never reports successful completion. Safe for future blocking atoms.</summary>
    public sealed class PresentationEffectHandle
    {
        public PresentationEffectStatus Status { get; private set; } = PresentationEffectStatus.Playing;
        public bool IsDone => Status != PresentationEffectStatus.Playing;
        public bool IsCompleted => Status == PresentationEffectStatus.Completed;
        internal void Finish(bool completed) => Status = completed ? PresentationEffectStatus.Completed : PresentationEffectStatus.Cancelled;
    }

    /// <summary>Only an input boundary; no triggers, event settlement, save system or NCS tables are read here.</summary>
    public interface IPresentationEffectPlayer
    {
        bool IsEffectBlocking { get; }
        PresentationEffectHandle PlayEffect(string fx, string targetKey, bool blocking, Action completed = null);
        PresentationEffectHandle PlayEffect(string fx, string targetKey = null, Action completed = null);
        PresentationEffectHandle PlayCameraCue(float zoom = 1f, float shake = 0f, float duration = .3f,
            Vector2? focus = null, Action completed = null);
        PresentationEffectHandle PlayFade(string type, float duration = .3f, Action atBlack = null, Action completed = null);
        void StopEffects();
    }
}
