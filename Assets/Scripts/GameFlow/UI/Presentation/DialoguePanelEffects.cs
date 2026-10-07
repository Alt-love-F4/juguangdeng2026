using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    public sealed partial class DialoguePanel
    {
        [Header("Shared effect library (no NCS runtime required)")]
        [SerializeField] private PresentationEffectLibrary effectLibrary;
        private PresentationEffects effects;
        private Coroutine portraitChange;
        private bool entranceComplete;
        private readonly List<PresentationEffectHandle> blockingHandles = new List<PresentationEffectHandle>();
        private int blockingEffects
        {
            get
            {
                for (int i = blockingHandles.Count - 1; i >= 0; i--)
                    if (blockingHandles[i].IsDone) blockingHandles.RemoveAt(i);
                return blockingHandles.Count;
            }
        }
        public PresentationEffects Effects => effects;
        public bool IsEffectBlocking => blockingEffects > 0 || portraitChange != null;
        public bool IsReady => ready && !IsEffectBlocking;
        public RectTransform EffectStage { get; private set; }
        public Image EffectOverlay { get; private set; }
        public Image LeftPortrait => leftPortrait;
        public Image RightPortrait => rightPortrait;
        public DialogueCharacter LeftCharacter => left;
        public DialogueCharacter RightCharacter => right;

        private void InitializeEffects()
        {
            if (effectLibrary == null) throw new InvalidOperationException("DialoguePanel needs the shared PresentationEffectLibrary asset.");
            // Keep the modal raycast blocker stationary; only visible content belongs to the camera/shake stage.
            var go = new GameObject("EffectStage", typeof(RectTransform));
            EffectStage = go.GetComponent<RectTransform>();
            EffectStage.SetParent(transform, false);
            EffectStage.anchorMin = Vector2.zero; EffectStage.anchorMax = Vector2.one;
            EffectStage.offsetMin = EffectStage.offsetMax = Vector2.zero;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == EffectStage || child.name == "InputBlocker") continue;
                child.SetParent(EffectStage, false);
            }
            // Reverse enumeration above reverses sibling order. Re-establish rendering order explicitly.
            background.transform.SetAsFirstSibling();
            if (leftPortraitFrame != null) leftPortraitFrame.transform.SetSiblingIndex(1);
            if (rightPortraitFrame != null) rightPortraitFrame.transform.SetSiblingIndex(2);
            dialogueBox.SetAsLastSibling();
            var overlayObject = new GameObject("EffectOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayObject.transform.SetParent(transform, false);
            EffectOverlay = overlayObject.GetComponent<Image>();
            EffectOverlay.rectTransform.anchorMin = Vector2.zero;
            EffectOverlay.rectTransform.anchorMax = Vector2.one;
            EffectOverlay.rectTransform.offsetMin = EffectOverlay.rectTransform.offsetMax = Vector2.zero;
            EffectOverlay.raycastTarget = false;
            effects = new PresentationEffects(effectLibrary, EffectStage, EffectOverlay, leftPortrait,
                leftPortraitFrame, rightPortrait, rightPortraitFrame, inactiveTint, focusSeconds);
        }
        private void EnterVisiblePortraits()
        {
            if (leftPortrait.enabled) effects.Play(PresentationEffectIds.EnterShadowWalk, 0);
            if (rightPortrait.enabled) effects.Play(PresentationEffectIds.EnterShadowWalk, 1);
        }
        private void PlayLineAccent(DialogueLine line)
        {
            if (!string.IsNullOrWhiteSpace(line.AccentFxKey)) PlayEffect(line.AccentFxKey, line.AccentTargetKey);
            else if (line.Gesture == DialogueGesture.Shake) PlayEffect(PresentationEffectIds.AccentShakeStartle);
            else if (line.Gesture == DialogueGesture.Nod) effects.Nod(line.Speaker == DialogueSpeaker.Left ? 0 : 1);
        }

        /// <summary>Shared effect-ID input. A future NCS adapter resolves its target ID and calls this method.</summary>
        public PresentationEffectHandle PlayEffect(string fx, string targetKey = null, Action completed = null)
            => PlayEffect(fx, targetKey, false, completed);
        public PresentationEffectHandle PlayEffect(string fx, string targetKey, bool blocking, Action completed = null)
        {
            if (!IsPlaying) throw new InvalidOperationException("Start a presentation before playing an effect.");
            bool portrait = fx == PresentationEffectIds.EnterShadowWalk || fx == PresentationEffectIds.AccentShakeStartle
                || fx == PresentationEffectIds.PortraitFadeIn || fx == PresentationEffectIds.PortraitFadeOut;
            int slot = portrait ? ResolveEffectSlot(targetKey) : -1;
            PresentationEffectHandle handle = effects.Play(fx, slot, completed);
            if (blocking && !handle.IsDone) blockingHandles.Add(handle);
            return handle;
        }
        private int ResolveEffectSlot(string targetKey)
        {
            if (string.IsNullOrWhiteSpace(targetKey) || targetKey == "speaker")
            {
                DialogueSpeaker who = script[LineIndex].Speaker;
                if (who == DialogueSpeaker.Narrator) throw new ArgumentException("Narrator has no portrait: supply a target character or side.");
                targetKey = who == DialogueSpeaker.Left ? "left" : "right";
            }
            int slot = targetKey == "left" || (left != null && !string.IsNullOrEmpty(left.CharacterKey) && left.CharacterKey == targetKey) ? 0
                : targetKey == "right" || (right != null && !string.IsNullOrEmpty(right.CharacterKey) && right.CharacterKey == targetKey) ? 1 : -1;
            if (slot < 0) throw new ArgumentException("Effect target is not on stage: " + targetKey);
            if (!(slot == 0 ? leftPortrait.enabled : rightPortrait.enabled))
                throw new ArgumentException("Effect target has no visible portrait: " + targetKey);
            return slot;
        }
        public PresentationEffectHandle PlayCameraCue(float zoom = 1f, float shake = 0f, float duration = .3f,
            Vector2? focus = null, Action completed = null)
        {
            if (!IsPlaying) throw new InvalidOperationException("Start a presentation before moving the camera stage.");
            return effects.Camera(zoom, shake, duration, focus ?? Vector2.zero, completed);
        }
        public PresentationEffectHandle PlayFade(string type, float duration = .3f, Action atBlack = null, Action completed = null)
        {
            if (!IsPlaying) throw new InvalidOperationException("Start a presentation before fading.");
            PresentationEffectHandle handle = effects.Fade(type, duration, atBlack, completed);
            blockingHandles.Add(handle);
            return handle;
        }
        public void SetBackgroundTint(Color tint) => background.color = tint;
        public void StopEffects()
        {
            if (effects != null) effects.Reset();
            blockingHandles.Clear();
        }
        private void CancelEffectWork()
        {
            entranceComplete = false;
            if (background != null) background.color = Color.white;
            if (portraitChange != null) { StopCoroutine(portraitChange); portraitChange = null; }
            StopEffects();
        }
        private void PrepareNextLine(int index)
        {
            DialogueLine line = script[index];
            if (!line.ReplaceLeft && !line.ReplaceRight) { ShowLine(index); return; }
            ready = false;
            continueButton.interactable = false;
            portraitChange = StartCoroutine(ChangeCharacters(index));
        }
        private IEnumerator ChangeCharacters(int index)
        {
            yield return null; // Ensure StartCoroutine has returned before the routine may clear its owner field.
            DialogueLine line = script[index];
            PresentationEffectHandle a = line.ReplaceLeft && leftPortrait.enabled ? effects.Play(PresentationEffectIds.PortraitFadeOut, 0) : null;
            PresentationEffectHandle b = line.ReplaceRight && rightPortrait.enabled ? effects.Play(PresentationEffectIds.PortraitFadeOut, 1) : null;
            while ((a != null && !a.IsDone) || (b != null && !b.IsDone)) yield return null;
            if ((a != null && !a.IsCompleted) || (b != null && !b.IsCompleted)) { FinishPortraitChange(); yield break; }
            ShowLine(index);
            a = line.ReplaceLeft && leftPortrait.enabled ? effects.Play(PresentationEffectIds.EnterShadowWalk, 0) : null;
            b = line.ReplaceRight && rightPortrait.enabled ? effects.Play(PresentationEffectIds.EnterShadowWalk, 1) : null;
            while ((a != null && !a.IsDone) || (b != null && !b.IsDone)) yield return null;
            FinishPortraitChange();
            if (IsPlaying && script != null) PlayLineAccent(script[LineIndex]);
        }
        /// <summary>Default left/right switch: fade out, swap resources while hidden, mirror, then fade in.</summary>
        public void SwapPortraits(Action completed = null)
        {
            if (!IsReady || exiting) return;
            ready = false;
            continueButton.interactable = false;
            portraitChange = StartCoroutine(SwapPortraitRoutine(completed));
        }
        private IEnumerator SwapPortraitRoutine(Action completed)
        {
            yield return null;
            PresentationEffectHandle a = effects.Play(PresentationEffectIds.PortraitFadeOut, 0);
            PresentationEffectHandle b = effects.Play(PresentationEffectIds.PortraitFadeOut, 1);
            while (!a.IsDone || !b.IsDone) yield return null;
            if (!a.IsCompleted || !b.IsCompleted) { FinishPortraitChange(); yield break; }
            DialogueCharacter original = left; left = right; right = original;
            ApplyPortrait(leftPortrait, leftPortraitFrame, left);
            ApplyPortrait(rightPortrait, rightPortraitFrame, right);
            RefreshSpeakerName();
            a = effects.Play(PresentationEffectIds.PortraitFadeIn, 0);
            b = effects.Play(PresentationEffectIds.PortraitFadeIn, 1);
            while (!a.IsDone || !b.IsDone) yield return null;
            bool success = a.IsCompleted && b.IsCompleted;
            FinishPortraitChange();
            if (success) completed?.Invoke();
        }
        private void FinishPortraitChange()
        {
            portraitChange = null;
            if (!IsPlaying || script == null) return;
            ready = true;
            continueButton.interactable = true;
            effects.SetFocus(script[LineIndex].Speaker);
        }
        private void RefreshSpeakerName()
        {
            DialogueLine line = script[LineIndex];
            string name = line.Speaker == DialogueSpeaker.Left ? left?.DisplayName
                : line.Speaker == DialogueSpeaker.Right ? right?.DisplayName : "旁白";
            speakerText.text = string.IsNullOrEmpty(line.SpeakerNameOverride) ? name ?? "旁白" : line.SpeakerNameOverride;
        }
    }
}
