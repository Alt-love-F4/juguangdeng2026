using System;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    /// <summary>Read-only index of generated data. Source ids never depend on translated text.</summary>
    public sealed class NarrativeDebugCatalog
    {
        public sealed class Entry
        {
            public string Kind, Key, PageId, Title, SearchText;
            public readonly List<string> EventIds = new List<string>();
            public string Identity => Kind + ":" + Key + ":" + PageId;
            public override string ToString() => Kind + " | " + Title + " | " + Key + (string.IsNullOrEmpty(PageId) ? "" : " / " + PageId);
        }
        public NarrativeConfiguration Configuration { get; }
        public readonly List<Entry> Entries = new List<Entry>();
        private readonly Func<string, string> resolve;
        public NarrativeDebugCatalog(NarrativeConfiguration configuration, Func<string, string> resolver)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            resolve = resolver ?? throw new ArgumentNullException(nameof(resolver));
            foreach (var e in configuration.events)
            {
                var entry = Add("Event", e.eventId, null, resolve(e.titleText), string.Join(" ", e.contentKeys.Select(resolve)));
                entry.EventIds.Add(e.eventId);
                entry.SearchText += " " + e.presentationKey + " " + e.setupKey;
            }
            foreach (var p in configuration.openingPresentations)
                Add("Opening", p.presentationId, null, resolve(p.titleKey), string.Join("\n", configuration.openingLines.Where(l => l.presentationId == p.presentationId).Select(l => l.lineId + " " + resolve(l.textKey))));
            foreach (var p in configuration.presentations.conversationPages.OrderBy(p => p.presentationKey).ThenBy(p => p.seq))
                Add("Conversation", p.presentationKey, p.pageId, "段 " + p.seq, string.Join("\n", configuration.presentations.conversationLines.Where(l => l.pageId == p.pageId).Select(l => l.lineId + " " + resolve(l.textKey))));
            foreach (var p in configuration.presentations.index)
                if (!Entries.Any(e => e.Key == p.presentationKey && e.Kind != "Event"))
                    Add(p.presentationClass, p.presentationKey, null, p.logTag, "");
            foreach (var entry in Entries.Where(e => e.Kind != "Event"))
                foreach (var e in configuration.events)
                    if (e.presentationKey == entry.Key || e.setupKey == entry.Key || e.eventProperties.Any(p => p.value == entry.Key))
                    { entry.EventIds.Add(e.eventId); entry.SearchText += " " + e.eventId + " " + resolve(e.titleText); }
        }
        private Entry Add(string kind, string key, string page, string title, string text)
        {
            var entry = new Entry { Kind = kind, Key = key, PageId = page, Title = title, SearchText = kind + " " + key + " " + page + " " + title + " " + text };
            Entries.Add(entry);
            return entry;
        }
        public List<Entry> Search(string query) => Entries.Where(e => string.IsNullOrWhiteSpace(query) || e.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        public DialogueScript CreateScript(Entry entry)
        {
            if (entry.Kind == "Opening")
            {
                if (!NarrativePresentationDataAdapter.TryCreateOpeningDialogue(Configuration, resolve, entry.Key, out var script, out var error)) throw new InvalidOperationException(error);
                return script;
            }
            if (entry.Kind != "Conversation") throw new InvalidOperationException("请选择关联的 Opening / Conversation 演出段落；此入口不能直接重建为对话。");
            var page = Configuration.presentations.conversationPages.First(p => p.pageId == entry.PageId);
            var data = new NarrativeConversationPresentationData { title = entry.Key, presentationKey = entry.Key, backgroundPath = InheritedBackground(page), bgmOverride = page.bgmOverride };
            foreach (var line in Configuration.presentations.conversationLines.Where(l => l.pageId == page.pageId).OrderBy(l => l.seq))
                data.lines.Add(new NarrativeConversationPresentationLine { lineId = line.lineId, pageId = line.pageId, seq = line.seq, speakerKey = line.speakerKey, speakerName = SpeakerName(line), side = line.side,
                    text = resolve(line.textKey), emote = line.emote, accentFxKey = line.accentFxKey, accentTargetKey = line.accentTargetKey, sfxKey = line.sfxKey, voiceKey = line.voiceKey });
            foreach (var choice in Configuration.choices.Where(c => c.groupKey == page.optionGroupKey).OrderBy(c => c.seq))
                data.options.Add(new NarrativeConversationPresentationOption { id = choice.choiceId, text = resolve(choice.textKey), enabled = false, requirement = "仅展示选项；条件、检定及后续分支请使用分支测试" });
            if (data.lines.Count == 0) throw new InvalidOperationException("演出段落无台词：" + page.pageId);
            return NarrativeConversationEventPlayback.CreateScript(data);
        }
        private string SpeakerName(NarrativeConversationLineDefinition line)
        {
            // Match the formal Presenter: a local alias (including pre-reveal ???) wins,
            // while speakerKey remains the real actor ID used for portraits and continuity.
            if (!string.IsNullOrWhiteSpace(line.speakerNameKey)) return resolve(line.speakerNameKey);
            if (string.IsNullOrWhiteSpace(line.speakerKey)) return null;
            var speaker = Configuration.objects.FirstOrDefault(o => o.objId == line.speakerKey);
            return NarrativeCharacterResolver.Resolve(line.speakerKey, resolve(speaker?.nameText), speaker?.portraitPath)?.name
                ?? resolve(speaker?.nameText);
        }

        private string InheritedBackground(NarrativeConversationPageDefinition page)
        {
            if (!string.IsNullOrWhiteSpace(page.bgOverride)) return page.bgOverride;
            // A standalone page preview has no runtime traversal/previous batch. Reconstruct
            // only the statically authored, unconditional bed within this presentation.
            string previous = Configuration.presentations.conversationPages
                .Where(p => p.presentationKey == page.presentationKey && p.seq < page.seq
                    && string.IsNullOrWhiteSpace(p.pageCondKeys) && string.IsNullOrWhiteSpace(p.pageBarConds)
                    && !string.IsNullOrWhiteSpace(p.bgOverride))
                .OrderByDescending(p => p.seq).Select(p => p.bgOverride).FirstOrDefault();
            if (previous != null) return previous;
            var backgrounds = Configuration.events.Where(e => e.presentationKey == page.presentationKey).Select(e =>
            {
                var setup = Configuration.setups.FirstOrDefault(s => s.setupKey == e.setupKey);
                var slots = PresentationSlots.Parse(setup);
                PresentationSlots.ApplyEventProperties(slots, e);
                return slots.TryGetValue("bg", out string background) ? background : string.Empty;
            }).Distinct(StringComparer.Ordinal).ToArray();
            // Shared presentations may have different event-specific stages. Do not choose
            // an arbitrary event or invent a background when the source is ambiguous.
            return backgrounds.Length == 1 ? backgrounds[0] : null;
        }

        public string SourceAt(Entry entry, int index)
        {
            if (entry == null) return "无选择";
            if (entry.Kind == "Opening")
            {
                var line = Configuration.openingLines.Where(l => l.presentationId == entry.Key).OrderBy(l => l.seq).ElementAtOrDefault(index);
                return line == null ? "源行不可用" : "sourceLineId=" + line.lineId + " seq=" + line.seq;
            }
            var item = Configuration.presentations.conversationLines.Where(l => l.pageId == entry.PageId).OrderBy(l => l.seq).ElementAtOrDefault(index);
            return item == null ? "源行不可用" : "sourceLineId=" + item.lineId + " seq=" + item.seq + " sourcePage=" + item.pageId;
        }
    }
}
