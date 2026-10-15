using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Session-only, revealed text. Never serialized into a save or populated from future configuration.</summary>
    public sealed class DialogueHistoryService
    {
        public sealed class Entry
        {
            public string Speaker { get; internal set; }
            public string Text { get; internal set; }
            internal long Playback;
            internal int Line;
        }
        public static DialogueHistoryService Formal { get; } = new DialogueHistoryService();
        public static void ClearFormal() => Formal.Clear();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => ClearFormal();
        private readonly List<Entry> entries = new List<Entry>();
        private long nextPlayback;
        public int Capacity { get; set; } = 500;
        public IReadOnlyList<Entry> Entries => entries;
        public long BeginPlayback() => ++nextPlayback;
        public void Clear() { entries.Clear(); ++nextPlayback; }
        public void Reveal(long playback, int line, string speaker, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                Entry previous = entries[i];
                if (previous.Playback == playback && previous.Line == line)
                { previous.Text = text; return; }
            }
            entries.Add(new Entry { Playback = playback, Line = line, Speaker = speaker, Text = text });
            Trim();
        }
        public void Choice(string text)
        {
            entries.Add(new Entry { Playback = -1, Line = -1, Speaker = "玩家选择", Text = "「" + text + "」" });
            Trim();
        }
        private void Trim()
        {
            while (entries.Count > Math.Max(1, Capacity)) entries.RemoveAt(0);
        }
    }
}
