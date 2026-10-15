using System;
using System.Collections.Generic;
using Spotlight.Narrative;
using UnityEngine;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>Bridges NCS reading events to the reusable ReadingPanel prefab.</summary>
    public static class ReadingEventPlayback
    {
        public static bool Play(NarrativeEventRequest request, string title, IReadOnlyList<string> pages,
            string imageResourcePath, bool dialogue, string speaker, string portraitResourcePath, Action completed)
            => Play(request, title, pages, imageResourcePath, dialogue, speaker, portraitResourcePath, completed,
                "narrative-reading", PanelCachePolicy.Reusable);

        public static bool Play(NarrativeEventRequest request, string title, IReadOnlyList<string> pages,
            string imageResourcePath, bool dialogue, string speaker, string portraitResourcePath, Action completed,
            string panelTag, PanelCachePolicy cachePolicy)
        {
            if (pages == null || pages.Count == 0) return false;
            ReadingPanel existing = UIKit.GetPanel<ReadingPanel>();
            if (existing != null && existing.State != PanelState.Close && existing.State != PanelState.Cached
                && existing.State != PanelState.Preloaded
                && !string.Equals(existing.Tag, panelTag, StringComparison.Ordinal))
                throw new InvalidOperationException("A reading panel is already open in another scope.");
            ReadingPanel panel = UIKit.OpenPanel<ReadingPanel>(UILevel.Pop, tag: panelTag,
                cachePolicy: cachePolicy);
            Texture image = string.IsNullOrWhiteSpace(imageResourcePath) ? null : Resources.Load<Texture>(imageResourcePath);
            Sprite portrait = string.IsNullOrWhiteSpace(portraitResourcePath) ? null : Resources.Load<Sprite>(portraitResourcePath);
            var readingPages = new List<ReadingPage>(pages.Count);
            if (request.ReadingLines != null && request.ReadingLines.Count > 0)
            {
                var lines = new List<ReadingLine>();
                foreach (NarrativeReadingLinePresentationData item in request.ReadingLines)
                    lines.Add(new ReadingLine(item.text, item.speakerName, item.textKey, item.speakerKey, item.voiceKey,
                        item.portrait != null ? item.portrait : string.IsNullOrWhiteSpace(item.portraitPath) ? null : Resources.Load<Sprite>(item.portraitPath),
                        item.mode == "dialogue" ? ReadingLineKind.Dialogue : ReadingLineKind.Narration));
                readingPages.Add(new ReadingPage(string.Empty, lines, image));
            }
            else for (int i = 0; i < pages.Count; i++)
            {
                ReadingLine line = new ReadingLine(pages[i], dialogue ? speaker : string.Empty,
                    portrait: dialogue ? portrait : null,
                    kind: dialogue ? ReadingLineKind.Dialogue : ReadingLineKind.Narration);
                readingPages.Add(new ReadingPage(string.Empty, new[] { line }, image, seq: i + 1));
            }
            bool notified = false;
            // Stop precedes the panel's finish/cancel callback; it must not advance the next event itself.
            void Stopped() => panel.PlaybackStopped -= Stopped;
            void Complete()
            {
                if (notified) return;
                notified = true;
                panel.PlaybackStopped -= Stopped;
                UIKit.ClosePanel(panel);
                completed?.Invoke();
            }
            try
            {
                panel.Play(new ReadingScript(readingPages, title), Complete, Complete);
                if (!notified) panel.PlaybackStopped += Stopped;
                return true;
            }
            catch
            {
                panel.PlaybackStopped -= Stopped;
                UIKit.ClosePanel(panel);
                throw;
            }
        }
    }
}
