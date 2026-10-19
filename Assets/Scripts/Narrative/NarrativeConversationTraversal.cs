using System;
using System.Collections.Generic;

namespace Spotlight.Narrative
{
    /// <summary>Root-local bounded cursor. A hidden page never takes its explicit edge.</summary>
    internal sealed class NarrativeConversationTraversal
    {
        private readonly List<NarrativeConversationPageDefinition> pages;
        private readonly int limit;
        private readonly Dictionary<string, int> indices = new Dictionary<string, int>(StringComparer.Ordinal);
        private int cursor, visited, currentIndex;
        public NarrativeConversationPageDefinition Current { get; private set; }
        public NarrativeConversationTraversal(List<NarrativeConversationPageDefinition> sortedPages, int start = 0)
        {
            pages = sortedPages ?? throw new ArgumentNullException(nameof(sortedPages));
            if (start < 0 || start > pages.Count) throw new ArgumentOutOfRangeException(nameof(start));
            cursor = start;
            // Bound revisits, but never reject a valid large linear presentation just because
            // its page count exceeds the ordinary revisit-budget cap. Use long before multiplying.
            limit = (int)Math.Max(pages.Count, Math.Min(16384L, Math.Max(128L, pages.Count * 8L)));
            for (int i = 0; i < pages.Count; i++)
            {
                var page = pages[i];
                if (page == null || string.IsNullOrWhiteSpace(page.pageId) || indices.ContainsKey(page.pageId))
                    throw new InvalidOperationException("Missing or duplicate conversation page ID: " + page?.pageId);
                indices.Add(page.pageId, i);
            }
        }
        public bool MoveNext(Func<NarrativeConversationPageDefinition, bool> visible)
        {
            while (cursor < pages.Count)
            {
                if (++visited > limit) throw new InvalidOperationException("Conversation graph traversal limit exceeded: " + Current?.presentationKey);
                currentIndex = cursor++;
                var page = pages[currentIndex];
                if (!visible(page)) continue;
                if (page.stepType == NarrativeConversationStepType.End) cursor = pages.Count;
                else if (!string.IsNullOrWhiteSpace(page.nextPageId))
                {
                    if (!indices.TryGetValue(page.nextPageId, out cursor) || pages[cursor].presentationKey != page.presentationKey) throw new InvalidOperationException("Conversation edge leaves presentation or is missing: " + page.pageId + " -> " + page.nextPageId);
                }
                Current = page;
                return true;
            }
            return false;
        }
        // A failed PassiveCheck has no played content, so its edge is not committed.
        public void ResumeLinearAfterCurrent() { cursor = currentIndex + 1; }
    }
}
