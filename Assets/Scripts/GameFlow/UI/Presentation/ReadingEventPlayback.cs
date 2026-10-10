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
            for (int i = 0; i < pages.Count; i++)
            {
                ReadingLine line = new ReadingLine(pages[i], dialogue ? speaker : string.Empty,
                    portrait: dialogue ? portrait : null,
                    kind: dialogue ? ReadingLineKind.Dialogue : ReadingLineKind.Narration);
                readingPages.Add(new ReadingPage(string.Empty, new[] { line }, image, seq: i + 1));
            }
            try
            {
                panel.Play(new ReadingScript(readingPages, title),
                    () => { UIKit.ClosePanel(panel); completed?.Invoke(); },
                    () => { UIKit.ClosePanel(panel); completed?.Invoke(); });
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
