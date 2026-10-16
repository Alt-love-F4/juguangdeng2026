using System;
using System.Collections.Generic;
using Spotlight.Narrative;
using UnityEngine;
using YokiFrame;

namespace Spotlight.GameFlow
{
    public static class NarrativeConversationEventPlayback
    {
        public static DialogueScript CreateScript(NarrativeConversationPresentationData data, string passiveHint = null)
        {
            if (data == null || data.lines.Count == 0 && data.options.Count == 0)
                throw new ArgumentException("Conversation has no lines or options.", nameof(data));
            var lines = new List<DialogueLine>(data.lines.Count);

            foreach (NarrativeConversationPresentationLine item in data.lines)
            {
                var source = new DialogueSourceLocation("Conversation", data.eventId, data.presentationKey,
                    item.lineId, item.seq, item.pageId);
                bool isLeft = string.Equals(item.side, "left", StringComparison.OrdinalIgnoreCase);
                bool isCenter = string.Equals(item.side, "center", StringComparison.OrdinalIgnoreCase);
                bool isRight = !string.IsNullOrWhiteSpace(item.speakerKey) && !isLeft && !isCenter && !string.Equals(item.side, "none", StringComparison.OrdinalIgnoreCase);
                CharacterProfile profile = string.IsNullOrWhiteSpace(item.speakerKey) ? null : NarrativeCharacterResolver.Find(item.speakerKey);
                string speakerName = !string.IsNullOrWhiteSpace(item.speakerName) ? item.speakerName : profile?.displayName;
                DialogueCharacter speakingCharacter = string.IsNullOrWhiteSpace(item.speakerKey) ? null :
                    new DialogueCharacter(item.speakerKey, speakerName, ResolvePortrait(item, profile),
                        portraitSpecified: !string.IsNullOrWhiteSpace(item.emote));
                DialogueCharacter left = StageCharacter(item.stageLeft);
                DialogueCharacter right = StageCharacter(item.stageRight);
                DialogueCharacter center = StageCharacter(item.stageCenter);
                if (isLeft) left = MergeSpeaking(left, speakingCharacter, item.stageLeft != null || item.replaceStageLeft, "left", item.speakerKey);
                if (isRight) right = MergeSpeaking(right, speakingCharacter, item.stageRight != null || item.replaceStageRight, "right", item.speakerKey);
                if (isCenter) center = MergeSpeaking(center, speakingCharacter, item.stageCenter != null || item.replaceStageCenter, "center", item.speakerKey);
                bool replaceLeft = item.replaceStageLeft || isLeft;
                bool replaceRight = item.replaceStageRight || isRight;
                bool replaceCenter = item.replaceStageCenter || isCenter;
                if (string.IsNullOrWhiteSpace(item.speakerKey))
                    lines.Add(new DialogueLine(DialogueSpeaker.Narrator, item.text, passiveHint: lines.Count == 0 ? passiveHint : null,
                        replaceLeft: item.replaceStageLeft, leftCharacter: left, replaceRight: item.replaceStageRight, rightCharacter: right,
                        replaceCenter: item.replaceStageCenter, centerCharacter: center, accentFxKey: item.accentFxKey, accentTargetKey: item.accentTargetKey,
                        sfxKey: item.sfxKey, voiceKey: item.voiceKey, source: source));
                else if (string.Equals(item.side, "none", StringComparison.OrdinalIgnoreCase))
                    lines.Add(new DialogueLine(DialogueSpeaker.Offstage, item.text, speakerNameOverride: speakerName,
                        replaceLeft: item.replaceStageLeft, leftCharacter: left, replaceRight: item.replaceStageRight, rightCharacter: right,
                        replaceCenter: item.replaceStageCenter, centerCharacter: center, accentFxKey: item.accentFxKey, accentTargetKey: item.accentTargetKey,
                        passiveHint: lines.Count == 0 ? passiveHint : null, sfxKey: item.sfxKey, voiceKey: item.voiceKey, source: source));
                else
                    lines.Add(new DialogueLine(isLeft ? DialogueSpeaker.Left : isCenter ? DialogueSpeaker.Center : DialogueSpeaker.Right, item.text,
                        speakerNameOverride: speakerName, replaceLeft: replaceLeft, leftCharacter: left, replaceRight: replaceRight, rightCharacter: right,
                        replaceCenter: replaceCenter, centerCharacter: center, accentFxKey: item.accentFxKey, accentTargetKey: item.accentTargetKey,
                        passiveHint: lines.Count == 0 ? passiveHint : null, sfxKey: item.sfxKey, voiceKey: item.voiceKey, source: source));
            }
            if (lines.Count == 0)
                lines.Add(new DialogueLine(DialogueSpeaker.Narrator, string.Empty, passiveHint: passiveHint));
            var options = new List<DialogueOption>();
            foreach (var option in data.options)
                options.Add(new DialogueOption(option.id, option.text, enabled: option.enabled, requirementHint: option.requirement));
            // Only the first line introduces its speaker. Later characters must not appear early.
            return new DialogueScript(lines, new DialogueCharacter("", null), new DialogueCharacter("", null), data.title,
                options: options, continuityOwner: data.continuityOwner, continues: data.continues, musicKey: data.bgmOverride,
                backgroundVisual: NarrativeStageVisuals.Resolve(data.backgroundPath));
        }

        private static DialogueCharacter StageCharacter(NarrativeCharacterPresentationData data)
            => data == null ? null : new DialogueCharacter(data.key, data.name, data.portrait ?? (string.IsNullOrWhiteSpace(data.portraitPath) ? null : Resources.Load<Sprite>(data.portraitPath)));

        private static DialogueCharacter MergeSpeaking(DialogueCharacter staged, DialogueCharacter speaking, bool explicitStage, string slot, string speakerKey)
        {
            if (!explicitStage) return speaking;
            if (staged == null)
                throw new InvalidOperationException("Stage " + slot + " clears the speaking character " + speakerKey);
            if (!string.Equals(staged.CharacterKey, speakerKey, StringComparison.Ordinal))
                throw new InvalidOperationException("Stage " + slot + " conflicts with speaking character " + speakerKey + ": " + staged.CharacterKey);
            return DialogueCharacter.Resolve(staged, speaking);
        }

        private static readonly HashSet<string> warnedPortraits = new HashSet<string>(StringComparer.Ordinal);
        private static Sprite ResolvePortrait(NarrativeConversationPresentationLine item, CharacterProfile profile)
        {
            Sprite portrait = profile != null ? profile.PortraitFor(item.emote)
                : item.portrait != null ? item.portrait : string.IsNullOrWhiteSpace(item.portraitPath) ? null : Resources.Load<Sprite>(item.portraitPath);
            bool requested = !string.IsNullOrWhiteSpace(item.emote) && !string.Equals(item.emote, "neutral", StringComparison.OrdinalIgnoreCase);
            bool missing = requested && (profile == null ||
                !(string.Equals(item.emote, "happy", StringComparison.OrdinalIgnoreCase) && profile.portraitHappy != null)
                && !(string.Equals(item.emote, "angry", StringComparison.OrdinalIgnoreCase) && profile.portraitAngry != null));
            if (missing && warnedPortraits.Add(item.speakerKey + "/" + item.emote))
                Debug.LogWarning("[NCS][PortraitFallback] character=" + item.speakerKey + " emote=" + item.emote
                    + " line=" + item.lineId + "; using configured base portrait (expression resource missing).");
            return portrait;
        }

        private static void FadeStage(DialoguePanel panel, object owner, float seconds, Action<bool> done)
        {
            bool notified = false;
            void Complete(bool success)
            {
                if (notified) return;
                notified = true;
                if (panel != null) panel.PlaybackStopped -= Stopped;
                done?.Invoke(success);
            }
            void Stopped() => Complete(false);
            System.Collections.IEnumerator Wait(PresentationEffectHandle handle)
            {
                try
                {
                    while (!notified && !handle.IsDone) yield return null;
                    if (!notified) Complete(handle.IsCompleted);
                }
                finally { Complete(false); }
            }

            try
            {
                if (panel == null || !panel.isActiveAndEnabled) { Complete(false); return; }
                // Stop/Close/disable may kill the panel's coroutine before it can observe cancellation.
                panel.PlaybackStopped += Stopped;
                var handle = DialoguePlayback.FadeStage(owner, seconds);
                panel.StartCoroutine(Wait(handle));
            }
            catch { Complete(false); throw; }
        }

        public static bool Play(NarrativeEventRequest request, NarrativeConversationPresentationData data, Action completed)
            => Play(request, data, completed, "character-dialogue", PanelCachePolicy.Reusable);

        public static bool PlayPreview(NarrativeEventRequest request, NarrativeConversationPresentationData data, Action completed)
            => Play(request, data, completed, "character-dialogue", PanelCachePolicy.Reusable, true);

        public static bool Play(NarrativeEventRequest request, NarrativeConversationPresentationData data, Action completed,
            string panelTag, PanelCachePolicy cachePolicy, bool isPreview = false)
        {
            if (data == null || data.lines.Count == 0 && data.options.Count == 0) return false;
            bool notified = false;
            DialoguePanel panel = null;
            void Detach() { if (panel != null) panel.PlaybackInterrupted -= Cancel; }
            void Complete() { if (notified) return; notified = true; Detach(); completed?.Invoke(); }
            void Cancel() { if (notified) return; notified = true; Detach(); data.cancelled?.Invoke(); }
            var script = CreateScript(data, request.PassiveHint);
            if (isPreview) script = script.WithPreviewContext();
            panel = DialoguePlayback.Play(script, Complete, Cancel, panelTag, cachePolicy);
            if (!notified) panel.PlaybackInterrupted += Cancel;
            data.releaseStage = () => { Detach(); DialoguePlayback.ReleaseStage(data.continuityOwner); };
            data.fadeStage = (seconds, done) => FadeStage(panel, data.continuityOwner, seconds, done);
            // Formal first-session guidance owns progression; ESC belongs to the pause menu.
            // Preview playback has no running guide and retains its normal cancellation behavior.
            var dayFlow = UnityEngine.Object.FindFirstObjectByType<DayFlowController>();
            panel.AllowCancel = isPreview || request.Event?.blocksSave != true && (dayFlow == null || !dayFlow.IsFirstSessionGuideActive);
            panel.OptionSelectionRequested = data.selectChoice;
            return true;
        }
    }
}
