using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Per-session snapshot for the ending panel. Dialogue remains owned by its existing history service.</summary>
    public static class NarrativeEndingEcho
    {
        public sealed class ArtworkSnapshot
        {
            public Sprite Sprite { get; }
            public bool IsPlaceholder { get; }
            public string StatusLabel { get; }
            public ArtworkSnapshot(Sprite sprite, bool isPlaceholder, string statusLabel)
            { Sprite = sprite; IsPlaceholder = isPlaceholder; StatusLabel = statusLabel ?? string.Empty; }
        }

        public static ArtworkSnapshot ResolveArtwork(string endingId)
        {
            NarrativeEndingArtworkCatalog catalog = NarrativeEndingArtworkCatalog.Load();
            NarrativeEndingArtworkCatalog.Entry entry = catalog != null ? catalog.Find(endingId) : null;
            if (entry == null)
                return new ArtworkSnapshot(null, false, "结局插画素材未登记 · 需求待确认");
            if (entry.artwork == null)
                return new ArtworkSnapshot(null, false, entry.status == NarrativeEndingArtworkCatalog.ArtStatus.NeedsReview
                    ? NarrativeEndingArtworkCatalog.StatusLabel(NarrativeEndingArtworkCatalog.ArtStatus.MissingNeedsReview)
                    : NarrativeEndingArtworkCatalog.StatusLabel(entry.status));
            return new ArtworkSnapshot(entry.artwork, entry.artworkIsPlaceholder,
                entry.artworkIsPlaceholder ? "美术需求占位卡 · 非最终插画" : "");
        }

        private static readonly List<string> lines = new List<string>();
        public static string Text { get; private set; } = string.Empty;
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            lines.Clear();
            Text = string.Empty;
            Changed = null;
        }

        public static void Capture(IReadOnlyList<DialogueHistoryService.Entry> history, int maxLines = 2)
        {
            lines.Clear();
            int capacity = Mathf.Clamp(maxLines, 1, 8);
            if (history != null)
            {
                for (int i = history.Count - 1; i >= 0 && lines.Count < capacity; i--)
                {
                    DialogueHistoryService.Entry entry = history[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Text)
                        || string.Equals(entry.Speaker, "玩家选择", StringComparison.Ordinal)) continue;
                    string line = entry.Text.Trim();
                    if (lines.Count > 0 && string.Equals(lines[lines.Count - 1], line, StringComparison.Ordinal)) continue;
                    lines.Add((string.IsNullOrWhiteSpace(entry.Speaker) ? string.Empty : entry.Speaker.Trim() + "：") + "“" + line + "”");
                }
            }
            lines.Reverse();
            var text = new StringBuilder();
            foreach (string line in lines)
            {
                if (text.Length > 0) text.Append("\n");
                text.Append(line);
            }
            Text = text.ToString();
            Changed?.Invoke();
        }

        public static void Clear() => Capture(null);
    }
}
