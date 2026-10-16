using System;
using System.Collections.Generic;
using System.Text;
using Spotlight.Narrative;
using UnityEngine;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>Owner-scoped bridge to the existing opening-letter prefab, with no narrative settlement.</summary>
    public static class LetterEventPlayback
    {
        private const string PanelTag = "narrative-letter";
        private static FirstDayLetterPanel active;

        public static bool Play(NarrativeEventRequest request, NarrativeLetterPresentationData data, Action completed)
            => PlayInternal(data, completed, false);

        public static bool PlayPreview(NarrativeEventRequest request, NarrativeLetterPresentationData data, Action completed)
            => PlayInternal(data, completed, true);

        private static bool PlayInternal(NarrativeLetterPresentationData data, Action completed, bool preview)
        {
            if (data == null || data.continuityOwner == null || !data.revealOnly && data.lines.Count == 0) return false;
            var snapshot = new LetterReadingSnapshot(data, preview);
            Action onCancelled = data.cancelled;
            FirstDayLetterPanel existing = UIKit.GetPanel<FirstDayLetterPanel>();
            if (existing != null && (existing.IsPlaying || existing.IsGenericLetter) && !existing.OwnsLetter(data.continuityOwner))
                return false; // Never steal the opening letter or another owner's live presentation.
            FirstDayLetterPanel panel = existing != null && existing.OwnsLetter(data.continuityOwner) ? existing
                : UIKit.OpenPanel<FirstDayLetterPanel>(UILevel.Pop, tag: PanelTag, cachePolicy: PanelCachePolicy.Reusable);
            active = panel;
            object owner = data.continuityOwner;
            bool notified = false, cancelled = false;
            void Complete()
            {
                if (notified || cancelled) return;
                notified = true;
                if (!snapshot.RevealOnly && panel != null)
                {
                    if (active == panel) active = null;
                    UIKit.ClosePanel(panel);
                }
                completed?.Invoke();
            }
            void Cancel()
            {
                // Reveal acknowledges a step, not the retained owner's lifetime.
                if (cancelled || notified && !snapshot.RevealOnly) return;
                cancelled = true;
                if (active == panel) active = null;
                if (panel != null && panel.State == PanelState.Open && !panel.IsPlaying && !panel.IsGenericLetter)
                    UIKit.ClosePanel(panel);
                onCancelled?.Invoke();
            }
            data.releaseStage = () => ReleaseStage(owner);
            data.dismissStage = () =>
            {
                FirstDayLetterPanel current = active;
                if (current == null || !current.OwnsLetter(owner) || !current.DismissLetterReveal(owner)) return;
                active = null;
                UIKit.ClosePanel(current);
            };
            data.setSuspended = paused => SetSuspended(owner, paused);
            try
            {
                panel.PlayLetter(snapshot, Complete, Cancel);
                return true;
            }
            catch
            {
                ReleaseStage(owner);
                if (panel != null && !panel.IsPlaying && !panel.IsGenericLetter) UIKit.ClosePanel(panel);
                throw;
            }
        }

        public static void ReleaseStage(object owner)
        {
            FirstDayLetterPanel panel = active;
            if (panel == null || !panel.OwnsLetter(owner)) return;
            active = null;
            panel.Stop(); // Interruption is cancellation, never completion.
            if (panel != null && !panel.IsPlaying && !panel.IsGenericLetter) UIKit.ClosePanel(panel);
        }

        public static void SetSuspended(object owner, bool suspended)
        {
            if (active != null && active.OwnsLetter(owner)) active.SetSuspended(suspended);
        }
    }

    /// <summary>A copied UI snapshot, never a query into the running narrative.</summary>
    public sealed class LetterReadingSnapshot
    {
        public sealed class Line
        {
            public string Text { get; }
            public string Speaker { get; }
            public string TextKey { get; }
            public string SpeakerKey { get; }
            public string Mode { get; }
            public string VoiceKey { get; }
            public DialogueSourceLocation Source { get; }
            internal Line(NarrativeLetterPresentationLine line, NarrativeLetterPresentationData data)
            {
                Text = line.text ?? string.Empty;
                Speaker = line.speakerName ?? string.Empty;
                TextKey = line.textKey ?? string.Empty;
                SpeakerKey = line.speakerKey ?? string.Empty;
                Mode = line.mode ?? string.Empty;
                VoiceKey = line.voiceKey ?? string.Empty;
                Source = new DialogueSourceLocation("Reading", line.eventId ?? data.eventId,
                    line.presentationKey ?? data.presentationKey, line.lineId, line.seq, line.pageId ?? data.pageId);
            }
        }

        private readonly List<Line> lines = new List<Line>();
        public IReadOnlyList<Line> Lines { get; }
        public string Title { get; }
        public string ImagePath { get; }
        public string EnvelopePath { get; }
        public string ReadingKey { get; }
        public string TitleKey { get; }
        public string ReadingPageId { get; }
        public DialogueSourceLocation TitleSource { get; }
        public object Owner { get; }
        public bool RevealOnly { get; }
        public bool AllowCancel { get; }
        public bool IsPreview { get; }
        public string Body { get; }

        public LetterReadingSnapshot(NarrativeLetterPresentationData data, bool preview = false)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            Owner = data.continuityOwner;
            Title = data.title ?? string.Empty;
            ImagePath = data.imagePath;
            EnvelopePath = data.envelopePath;
            ReadingKey = data.readingKey;
            TitleKey = data.titleKey;
            ReadingPageId = data.readingPageId;
            TitleSource = new DialogueSourceLocation("Reading", data.eventId, data.readingKey, data.titleKey, 0, data.readingPageId);
            RevealOnly = data.revealOnly;
            AllowCancel = preview || data.allowCancel;
            IsPreview = preview;
            var body = new StringBuilder();
            foreach (NarrativeLetterPresentationLine line in data.lines)
            {
                if (line == null) continue;
                var copy = new Line(line, data);
                if (lines.Count > 0) body.Append('\n');
                body.Append(copy.Text);
                lines.Add(copy);
            }
            Body = body.ToString();
            Lines = lines.AsReadOnly();
        }

        /// <summary>Keep each numeric token intact while allowing wrapping at its original spaces.</summary>
        public static string LayoutText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var result = new StringBuilder(text.Length + 64);
            for (int i = 0; i < text.Length;)
            {
                char c = text[i];
                if (c >= '0' && c <= '9')
                {
                    result.Append("<nobr>");
                    do { result.Append(text[i++]); }
                    while (i < text.Length && text[i] >= '0' && text[i] <= '9');
                    result.Append("</nobr>");
                }
                else
                {
                    // Snapshot text is plain content, not TMP markup or opening character-creation links.
                    result.Append(c == '<' ? "<noparse><</noparse>" : c == '>' ? "<noparse>></noparse>" : c.ToString());
                    i++;
                }
            }
            return result.ToString();
        }
    }
}
