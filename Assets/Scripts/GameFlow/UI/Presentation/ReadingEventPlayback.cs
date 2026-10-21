using System;
using System.Collections.Generic;
using Spotlight.Narrative;
using UnityEngine;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>Shows complete newspapers in NoticePanel; other reading events retain DialoguePanel.</summary>
    public static class ReadingEventPlayback
    {
        public static bool Play(NarrativeEventRequest request, string title, IReadOnlyList<string> pages,
            string imageResourcePath, bool dialogue, string speaker, string portraitResourcePath, Action completed)
            => Play(request, title, pages, imageResourcePath, dialogue, speaker, portraitResourcePath, completed,
                "narrative-reading", PanelCachePolicy.Reusable);

        public static bool Play(NarrativeEventRequest request, string title, IReadOnlyList<string> pages, string imageResourcePath, bool dialogue, string speaker, string portraitResourcePath, Action completed,
            string panelTag, PanelCachePolicy cachePolicy, bool isPreview = false)
        {
            if (pages == null || pages.Count == 0) return false;
            if (request.Event?.eventId?.StartsWith("newspaper.", StringComparison.Ordinal) == true)
                return NarrativeNoticeEventPlayback.Play(request, new NarrativeNoticePresentationData
                {
                    presentationKey = request.Event.presentationKey,
                    title = title,
                    body = string.Join("\n\n", pages),
                    imageKey = imageResourcePath
                }, completed, panelTag, cachePolicy);

            bool notified = false;
            DialoguePanel panel = null;
            void Detach()
            {
                if (panel == null) return;
                panel.PlaybackInterrupted -= Interrupted;
            }
            void Complete()
            {
                if (notified) return;
                notified = true;
                Detach();
                completed?.Invoke();
            }
            void Cancel()
            {
                if (notified) return;
                if (request.PresentationCancelled == null) { Complete(); return; } // Legacy external callback contract.
                notified = true;
                Detach();
                request.PresentationCancelled();
            }
            void Interrupted()
            {
                // A forced Stop is not a successful read. Formal NCS requests provide an explicit
                // cancellation hook; legacy callers still rely on the panel's Cancel callback.
                if (notified) return;
                if (request.PresentationCancelled == null) { Detach(); return; }
                notified = true;
                Detach();
                request.PresentationCancelled();
            }

            try
            {
                DialogueScript script = CreateDialogueScript(request, title, pages, imageResourcePath,
                    dialogue, speaker, portraitResourcePath);
                if (isPreview) script = script.WithPreviewContext();
                panel = DialoguePlayback.Play(script, Complete, Cancel, panelTag, cachePolicy);
                if (!notified) panel.PlaybackInterrupted += Interrupted;
                return true;
            }
            catch
            {
                Detach();
                throw;
            }
        }

        private static DialogueScript CreateDialogueScript(NarrativeEventRequest request, string title,
            IReadOnlyList<string> pages, string imageResourcePath, bool dialogue, string speaker, string portraitResourcePath)
        {
            Texture background = string.IsNullOrWhiteSpace(imageResourcePath)
                ? null : Resources.Load<Texture>(imageResourcePath);
            var lines = new List<DialogueLine>();
            IReadOnlyList<NarrativeReadingLinePresentationData> readingLines = request.ReadingLines;
            if (readingLines != null && readingLines.Count > 0)
            {
                for (int i = 0; i < readingLines.Count; i++)
                {
                    NarrativeReadingLinePresentationData item = readingLines[i];
                    if (string.Equals(item.mode, "dialogue", StringComparison.OrdinalIgnoreCase))
                    {
                        string speakerName = string.IsNullOrWhiteSpace(item.speakerName) ? "旁白" : item.speakerName;
                        Sprite linePortrait = item.portrait != null ? item.portrait
                            : string.IsNullOrWhiteSpace(item.portraitPath) ? null : Resources.Load<Sprite>(item.portraitPath);
                        string characterKey = string.IsNullOrWhiteSpace(item.speakerKey)
                            ? "reading.speaker." + speakerName : item.speakerKey;
                        var character = new DialogueCharacter(characterKey, speakerName, linePortrait);
                        lines.Add(new DialogueLine(DialogueSpeaker.Right, item.text, speakerNameOverride: speakerName,
                            replaceLeft: true, leftCharacter: null, replaceRight: true, rightCharacter: character,
                            replaceCenter: true, centerCharacter: null, voiceKey: item.voiceKey,
                            passiveHint: i == 0 ? "滚轮向上：查看已读内容" : null,
                            source: ReadingSource(request, item)));
                    }
                    else
                    {
                        lines.Add(new DialogueLine(DialogueSpeaker.Narrator, item.text,
                            replaceLeft: true, leftCharacter: null, replaceRight: true, rightCharacter: null,
                            replaceCenter: true, centerCharacter: null, voiceKey: item.voiceKey,
                            passiveHint: i == 0 ? "滚轮向上：查看已读内容" : null,
                            source: ReadingSource(request, item)));
                    }
                }
            }
            else
            {
                Sprite portrait = dialogue && !string.IsNullOrWhiteSpace(portraitResourcePath)
                    ? Resources.Load<Sprite>(portraitResourcePath) : null;
                for (int i = 0; i < pages.Count; i++)
                {
                    if (dialogue)
                    {
                        string name = string.IsNullOrWhiteSpace(speaker) ? "旁白" : speaker;
                        var character = new DialogueCharacter("reading.speaker." + name, name, portrait);
                        lines.Add(new DialogueLine(DialogueSpeaker.Right, pages[i], speakerNameOverride: name, replaceLeft: true,
                            leftCharacter: null, replaceRight: true, rightCharacter: character,
                            replaceCenter: true, centerCharacter: null,
                            passiveHint: i == 0 ? "滚轮向上：查看已读内容" : null,
                            source: ReadingSource(request, null, i)));
                    }
                    else
                    {
                        lines.Add(new DialogueLine(DialogueSpeaker.Narrator, pages[i],
                            replaceLeft: true, leftCharacter: null, replaceRight: true, rightCharacter: null,
                            replaceCenter: true, centerCharacter: null,
                            passiveHint: i == 0 ? "滚轮向上：查看已读内容" : null,
                            source: ReadingSource(request, null, i)));
                    }
                }
            }

            if (lines.Count == 0) lines.Add(new DialogueLine(DialogueSpeaker.Narrator, string.Empty));
            return new DialogueScript(lines, new DialogueCharacter("", null), new DialogueCharacter("", null),
                title, background);
        }

        private static DialogueSourceLocation ReadingSource(NarrativeEventRequest request, NarrativeReadingLinePresentationData item, int fallbackIndex = 0)
            => new DialogueSourceLocation("Reading", request.Event?.eventId, request.Event?.presentationKey,
                item != null ? (string.IsNullOrWhiteSpace(item.lineId) ? item.textKey : item.lineId) : "reading." + fallbackIndex,
                item != null ? (item.seq > 0 ? item.seq : 1) : fallbackIndex + 1, item != null ? item.pageId : null);
    }
}
