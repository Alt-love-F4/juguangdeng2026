using System;
using System.Collections.Generic;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.SceneManagement;
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
                if (string.IsNullOrWhiteSpace(item.speakerKey))
                {
                    lines.Add(new DialogueLine(DialogueSpeaker.Narrator, item.text, passiveHint: lines.Count == 0 ? passiveHint : null,
                        accentFxKey: item.accentFxKey, accentTargetKey: item.accentTargetKey,
                        sfxKey: item.sfxKey, voiceKey: item.voiceKey, source: source));
                    continue;
                }
                CharacterProfile profile = NarrativeCharacterResolver.Find(item.speakerKey);
                string speakerName = !string.IsNullOrWhiteSpace(item.speakerName) ? item.speakerName : profile?.displayName;
                if (string.Equals(item.side, "none", StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add(new DialogueLine(DialogueSpeaker.Offstage, item.text, speakerNameOverride: speakerName,
                        accentFxKey: item.accentFxKey, accentTargetKey: item.accentTargetKey,
                        passiveHint: lines.Count == 0 ? passiveHint : null, sfxKey: item.sfxKey, voiceKey: item.voiceKey, source: source));
                    continue;
                }
                Sprite portrait = ResolvePortrait(item, profile);
                var character = new DialogueCharacter(item.speakerKey, speakerName, portrait,
                    portraitSpecified: !string.IsNullOrWhiteSpace(item.emote));
                bool isLeft = string.Equals(item.side, "left", StringComparison.OrdinalIgnoreCase);
                bool isCenter = string.Equals(item.side, "center", StringComparison.OrdinalIgnoreCase);
                lines.Add(new DialogueLine(isLeft ? DialogueSpeaker.Left : isCenter ? DialogueSpeaker.Center : DialogueSpeaker.Right, item.text,
                    replaceLeft: isLeft, leftCharacter: isLeft ? character : null,
                    replaceRight: !isLeft && !isCenter, rightCharacter: !isLeft && !isCenter ? character : null,
                    replaceCenter: isCenter, centerCharacter: isCenter ? character : null,
                    accentFxKey: item.accentFxKey, accentTargetKey: item.accentTargetKey,
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
                backgroundVisual: NarrativeStageVisuals.Resolve(data.backgroundPath,
                    data.lines.Count > 0 && !string.IsNullOrWhiteSpace(data.lines[0].pageId)
                        ? data.lines[0].pageId : data.presentationKey, data.continuityOwner));
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
            void Fail()
            {
                if (notified) return;
                notified = true;
                Detach();
                data.cancelled?.Invoke();
                NarrativeRuntime.Instance?.Presenter?.InterruptForHost();
            }
            var script = CreateScript(data, request.PassiveHint);
            if (isPreview) script = script.WithPreviewContext();
            void BeginPlayback()
            {
                if (notified) return;
                panel = DialoguePlayback.Play(script, Complete, Cancel, panelTag, cachePolicy);
                if (!notified) panel.PlaybackInterrupted += Cancel;
                // Formal first-session guidance owns progression; ESC belongs to the pause menu.
                // Preview playback has no running guide and retains its normal cancellation behavior.
                var dayFlow = UnityEngine.Object.FindFirstObjectByType<DayFlowController>();
                panel.AllowCancel = isPreview || request.Event?.blocksSave != true && (dayFlow == null || !dayFlow.IsFirstSessionGuideActive);
                panel.OptionSelectionRequested = data.selectChoice;
                if (data.fastForwardText) panel.FastForwardToChoiceOrComplete();
            }
            data.releaseStage = () => { notified = true; Detach(); DialoguePlayback.ReleaseStage(data.continuityOwner); };
            var flow = !isPreview && script.BackgroundVisual?.useSceneBackground == true
                && SceneManager.GetActiveScene().name != "CoffeeShop"
                ? UnityEngine.Object.FindFirstObjectByType<GameFlowBootstrap>() : null;
            if (flow != null && flow.SessionState == GameSessionState.Playing)
                flow.TransitionToGameplayScene("CoffeeShop", BeginPlayback, Fail);
            else BeginPlayback();
            return true;
        }
    }
}
