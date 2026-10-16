using System;
using System.Collections;
using System.Collections.Generic;

namespace Spotlight.Narrative
{
    public sealed partial class NarrativePresenter
    {
        public NarrativeConversationReadingPlayback ConversationReadingPlayback { get; set; }
        private Action<bool> suspendActiveReading;

        private IEnumerator ReadingStep(NarrativeEventDefinition definition, NarrativeConversationPageDefinition step,
            object stageOwner, List<Action> stageReleases)
        {
            if (ConversationReadingPlayback == null)
                throw new InvalidOperationException("Conversation Reading playback is not connected: " + step.pageId);
            var readingPages = configuration.presentations.readingPages.FindAll(x => x != null && x.presentationKey == step.readingKey);
            if (readingPages.Count != 1)
                throw new InvalidOperationException("Conversation Reading requires exactly one reading page: " + step.readingKey);
            var page = readingPages[0];
            if (!PageVisible(page.pageId, page.pageCondKeys, page.pageBarConds))
                throw new InvalidOperationException("Conversation Reading page is unavailable: " + page.pageId);
            var data = new NarrativeConversationReadingPresentationData
            {
                continuityOwner = stageOwner, eventId = definition.eventId, presentationKey = definition.presentationKey,
                pageId = step.pageId, readingKey = step.readingKey, readingPageId = page.pageId,
                titleKey = page.titleKey, title = string.IsNullOrWhiteSpace(page.titleKey) ? string.Empty : resolve(page.titleKey),
                imagePath = page.imageKey, allowCancel = !definition.blocksSave
            };
            var lines = configuration.presentations.readingLines.FindAll(x => x != null && x.pageId == page.pageId);
            lines.Sort((a, b) => a.seq.CompareTo(b.seq));
            if (lines.Count == 0) throw new InvalidOperationException("Conversation Reading has no lines: " + page.pageId);
            foreach (var line in lines)
            {
                var speaker = configuration.objects.Find(x => x != null && x.objId == line.speakerKey);
                string portraitPath = string.IsNullOrWhiteSpace(speaker?.portraitPath) ? speaker?.iconPath : speaker.portraitPath;
                var character = characterResolver?.Invoke(line.speakerKey, resolve(speaker?.nameText), portraitPath);
                data.lines.Add(new NarrativeConversationReadingPresentationLine
                {
                    lineId = line.lineId, seq = line.seq, textKey = line.textKey, text = resolve(line.textKey),
                    mode = line.mode, speakerKey = line.speakerKey, speakerName = character?.name ?? resolve(speaker?.nameText),
                    portraitPath = character?.portraitPath ?? portraitPath, portrait = character?.portrait, voiceKey = line.voiceKey
                });
            }
            bool finished = false, cancelled = false, stopped = false, closed = false;
            long version = sessionVersion;
            bool IsCurrent() => !closed && !disposed && sessionVersion == version && completion != null;
            data.cancelled = () =>
            {
                if (!IsCurrent() || finished || stageReleaseInProgress) return;
                cancelled = true;
                stageCancellationRequested = true;
            };
            data.playbackStopped = () => { if (IsCurrent() && !finished && !cancelled) stopped = true; };
            try
            {
                bool handled;
                try
                {
                    handled = ConversationReadingPlayback(request, data, () =>
                    {
                        if (IsCurrent() && !cancelled) finished = true;
                    });
                }
                finally
                {
                    if (data.releaseStage != null) stageReleases.Add(data.releaseStage);
                    if (IsCurrent()) suspendActiveReading = data.setSuspended;
                }
                if (!IsCurrent()) yield break;
                if (!handled) throw new InvalidOperationException("Conversation Reading playback rejected step: " + step.pageId);
                ui.Hide();
                while (!finished && !cancelled && IsCurrent())
                {
                    if (stopped)
                    {
                        // Stop is emitted before the panel's finish/cancel callback in the same call stack.
                        // A stop with neither terminal callback means owner loss, never successful reading.
                        yield return null;
                        if (IsCurrent() && !finished && !cancelled) cancelled = true;
                        break;
                    }
                    yield return null;
                }
                if (!IsCurrent()) yield break;
                if (cancelled) { AbortPresentation("playback_cancelled"); yield break; }
                resumeFrame = UnityEngine.Time.frameCount;
                ui.ResetSubmit();
                yield return null;
            }
            finally
            {
                closed = true;
                if (ReferenceEquals(suspendActiveReading, data.setSuspended)) suspendActiveReading = null;
                data.releaseStage?.Invoke();
            }
        }
    }
}
