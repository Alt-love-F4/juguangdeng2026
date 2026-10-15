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
        {
            if (pages == null || pages.Count == 0) return false;
            ReadingPanel panel = UIKit.OpenPanel<ReadingPanel>(UILevel.Pop, tag: "narrative-reading",
                cachePolicy: PanelCachePolicy.Reusable);
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
            try
            {
                bool notified = false;
                void Complete() { if (notified) return; notified = true; completed?.Invoke(); }
                panel.Play(new ReadingScript(readingPages, title),
                    () => { UIKit.ClosePanel(panel); Complete(); },
                    () => { UIKit.ClosePanel(panel); Complete(); });
                panel.PlaybackStopped += Complete;
                return true;
            }
            catch
            {
                UIKit.ClosePanel(panel);
                throw;
            }
        }
    }
}
