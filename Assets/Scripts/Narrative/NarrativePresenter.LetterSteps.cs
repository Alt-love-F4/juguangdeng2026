using System;
using System.Collections;
using System.Collections.Generic;

namespace Spotlight.Narrative
{
    public sealed partial class NarrativePresenter
    {
        private IEnumerator LetterStep(NarrativeEventDefinition definition, NarrativeConversationPageDefinition step,
            object stageOwner, List<Action> stageReleases)
        {
            if (LetterPlayback == null) throw new InvalidOperationException("Letter playback is not connected: " + step.pageId);
            var readingPages = configuration.presentations.readingPages.FindAll(x => x != null && x.presentationKey == step.readingKey);
            readingPages.Sort((a, b) => a.seq.CompareTo(b.seq));
            if (readingPages.Count != 1) throw new InvalidOperationException("Letter step requires exactly one reading page: " + step.readingKey);
            var page = readingPages[0];
            if (!string.IsNullOrWhiteSpace(page.pageBarConds))
                throw new InvalidOperationException("Unsupported letter page bar conditions: " + page.pageId);
            if (!string.IsNullOrWhiteSpace(page.pageCondKeys))
                foreach (string key in page.pageCondKeys.Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    if (PageCondition == null || !PageCondition(key.Trim()))
                        throw new InvalidOperationException("Letter reading page is unavailable: " + page.pageId);
            var data = new NarrativeLetterPresentationData
            {
                continuityOwner = stageOwner, eventId = definition.eventId, presentationKey = definition.presentationKey,
                pageId = step.pageId, readingKey = step.readingKey, title = resolve(page.titleKey), imagePath = page.imageKey,
                titleKey = page.titleKey, readingPageId = page.pageId,
                revealOnly = step.stepType == NarrativeConversationStepType.LetterReveal, allowCancel = !definition.blocksSave
            };
            if (definition.eventProperties != null)
                foreach (var property in definition.eventProperties)
                    if (property != null && string.Equals(property.property, "envelope", StringComparison.OrdinalIgnoreCase)) data.envelopePath = property.value;
            var lines = configuration.presentations.readingLines.FindAll(x => x != null && x.pageId == page.pageId);
            lines.Sort((a, b) => a.seq.CompareTo(b.seq));
            foreach (var line in lines)
            {
                var speaker = configuration.objects.Find(x => x != null && x.objId == line.speakerKey);
                var character = characterResolver?.Invoke(line.speakerKey, resolve(speaker?.nameText), speaker?.portraitPath);
                data.lines.Add(new NarrativeLetterPresentationLine { eventId = definition.eventId, presentationKey = step.readingKey,
                    pageId = page.pageId, lineId = line.lineId, seq = line.seq, textKey = line.textKey, text = resolve(line.textKey),
                    mode = line.mode, speakerKey = line.speakerKey, speakerName = character?.name ?? resolve(speaker?.nameText), voiceKey = line.voiceKey });
            }
            bool finished = false, cancelled = false, closed = false;
            long version = sessionVersion;
            data.cancelled = () =>
            {
                if (stageReleaseInProgress || sessionVersion != version || completion == null || closed && !data.revealOnly) return;
                cancelled = true;
                stageCancellationRequested = true;
            };
            try
            {
                bool handled;
                try { handled = LetterPlayback(request, data, () => { if (!closed && !cancelled && sessionVersion == version) finished = true; }); }
                finally
                {
                    if (data.releaseStage != null) stageReleases.Add(data.releaseStage);
                    if (data.setSuspended != null) suspendActiveLetter = data.setSuspended;
                }
                if (!handled) throw new InvalidOperationException("Letter playback rejected step: " + step.pageId);
                ui.Hide();
                while (!finished && !cancelled) yield return null;
                if (cancelled) AbortPresentation("playback_cancelled");
                else if (!data.revealOnly)
                {
                    // Resume dialogue on the next frame, never on the key that closed the letter.
                    resumeFrame = UnityEngine.Time.frameCount;
                    ui.ResetSubmit();
                    yield return null;
                }
            }
            finally { closed = true; }
        }
    }
}
