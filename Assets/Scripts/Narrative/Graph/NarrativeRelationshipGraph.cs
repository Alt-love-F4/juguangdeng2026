using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Spotlight.Narrative
{
    public enum StoryLinkKind { Prerequisite, Exclusion, Continuation, CheckSuccess, CheckFailure, Ending, Unresolved }
    public enum StoryNodeKind { Event, Opening, Ending, Fact }
    public sealed class StoryGraphNode
    {
        public string Id, Key, Title, Group, Lane, Entry, Snippet, Blocker, SearchText, DateSource;
        public int Day, Priority;
        public bool Sample, Disabled;
        public StoryNodeKind Kind;
        public bool Blocked => !string.IsNullOrWhiteSpace(Blocker);
    }
    public sealed class StoryGraphEdge
    {
        public string From, To, Label, Evidence;
        public int ConditionGroup;
        public StoryLinkKind Kind;
        public bool Conditional;
    }
    public sealed class StoryContentBlock
    {
        // Legacy combined display remains unchanged for existing graph consumers.
        public string Id, Caption, Text;
        // Reading prose (including choice labels), separate from source IDs/conditions/technical evidence.
        public string BodyText = "", Diagnostics = "";
        // KindLabel may include local condition/choice markers; Summary is a short prose heading.
        public string KindLabel = "", Summary = "";
        // Only the source's explicit nextPageId; empty never means inferred seq + 1.
        public string NextPageId = "";
    }

    /// <summary>Read-only config index. No runtime, save, condition evaluation or result execution.
    /// Calendar ordering is a layout hint, never fabricated causal edges.</summary>
    public sealed class NarrativeRelationshipGraph
    {
        private readonly NarrativeConfiguration config;
        private readonly Dictionary<string, string> texts;
        private readonly Dictionary<string, StoryGraphNode> nodes = new Dictionary<string, StoryGraphNode>(StringComparer.Ordinal);
        private readonly List<StoryGraphEdge> edges = new List<StoryGraphEdge>();
        private readonly HashSet<string> edgeKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<NarrativeConversationPageDefinition>> pages;
        private readonly Dictionary<string, List<NarrativeConversationLineDefinition>> lines;
        private readonly Dictionary<string, List<NarrativeReadingPageDefinition>> readings;
        private readonly Dictionary<string, List<NarrativeReadingLineDefinition>> readingLines;
        private readonly Dictionary<string, List<NarrativeChoiceDefinition>> choices;
        private readonly Dictionary<string, List<NarrativeConditionDefinition>> conditions;
        private readonly Dictionary<string, List<NarrativeResultDefinition>> results;
        private readonly Dictionary<string, List<Producer>> producers = new Dictionary<string, List<Producer>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Producer>> owners = new Dictionary<string, List<Producer>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<StoryContentBlock>> content = new Dictionary<string, List<StoryContentBlock>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<StoryGraphEdge>> adjacency = new Dictionary<string, List<StoryGraphEdge>>(StringComparer.Ordinal);
        private sealed class Producer { public string Node, Context; }
        public IReadOnlyList<StoryGraphNode> Nodes { get; private set; }
        public IReadOnlyList<StoryGraphEdge> Edges => edges;
        public int StoryCount => Nodes.Count(n => n.Kind == StoryNodeKind.Event && IsStory(n.Key));
        public int BlockedCount => Nodes.Count(n => n.Blocked && IsStory(n.Key));
        public static bool IsStory(string id) => id != null && new[] { "oct07.", "neutral.", "rose.", "church.", "revolution." }.Any(p => id.StartsWith(p, StringComparison.Ordinal));
        public static string EventId(string key) => "event:" + key;
        private static string OwnerKey(string type, string id) => type + ":" + id;
        private static List<T> At<T>(Dictionary<string, List<T>> map, string key) => key != null && map.TryGetValue(key, out var found) ? found : new List<T>();
        private static Dictionary<string, List<T>> Index<T>(IEnumerable<T> values, Func<T, string> key) where T : class
            => values.Where(x => x != null && key(x) != null).GroupBy(key).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        private static string Strip(string value, string prefix) => (value ?? "").StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value.Substring(prefix.Length) : value ?? "";
        public string Text(string key) => key != null && texts.TryGetValue(key, out string value) ? value : key ?? "";
        public StoryGraphNode Find(string id) => id != null && nodes.TryGetValue(id, out var n) ? n : null;
        public IReadOnlyList<StoryGraphEdge> Connections(string id) => adjacency.TryGetValue(id ?? "", out var list) ? list : Array.Empty<StoryGraphEdge>();
        /// <summary>Find the first exact, case-sensitive page ID. Returns a zero-based index or -1.
        /// Does not trim/parse IDs, interpret numeric page numbers, or traverse conditional branches.</summary>
        public static int FindPageIndex(IReadOnlyList<StoryContentBlock> blocks, string exactPageId)
        {
            if (blocks == null || string.IsNullOrWhiteSpace(exactPageId)) return -1;
            for (int i = 0; i < blocks.Count; i++)
                if (blocks[i] != null && string.Equals(blocks[i].Id, exactPageId, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>Returns zero-based matching indices in source order, once per block.
        /// Matches exact IDs, or ordinal-ignore-case substrings of body, caption and legacy Text (including choices).
        /// Null/blank queries return all non-null blocks; surrounding query whitespace is ignored. No runtime evaluation.</summary>
        public static IReadOnlyList<int> FindMatchingPages(IReadOnlyList<StoryContentBlock> blocks, string query)
        {
            if (blocks == null) return Array.Empty<int>();
            query = query == null ? "" : query.Trim();
            var matches = new List<int>();
            for (int i = 0; i < blocks.Count; i++) {
                var block = blocks[i];
                if (block != null && (query.Length == 0 || string.Equals(block.Id, query, StringComparison.Ordinal) ||
                    ContainsQuery(block.BodyText, query) || ContainsQuery(block.Caption, query) || ContainsQuery(block.Text, query)))
                    matches.Add(i);
            }
            return matches.AsReadOnly();
        }
        private static bool ContainsQuery(string value, string query)
            => value != null && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        public static NarrativeRelationshipGraph Build(NarrativeConfiguration configuration) => new NarrativeRelationshipGraph(configuration);
        private NarrativeRelationshipGraph(NarrativeConfiguration configuration)
        {
            config = configuration ?? throw new ArgumentNullException(nameof(configuration));
            texts = config.texts.Where(t => t != null && t.key != null).GroupBy(t => t.key).ToDictionary(g => g.Key, g => g.First().content ?? "", StringComparer.Ordinal);
            pages = Index(config.presentations.conversationPages, p => p.presentationKey);
            lines = Index(config.presentations.conversationLines, l => l.pageId);
            readings = Index(config.presentations.readingPages, p => p.presentationKey);
            readingLines = Index(config.presentations.readingLines, l => l.pageId);
            choices = Index(config.choices, c => c.groupKey);
            conditions = Index(config.conditions, c => OwnerKey(c.ownerType, c.ownerId));
            results = Index(config.results, r => r.eventId);
            BuildNodes(); BindCheckOwners(); BuildRelationships();
            for (int i = 0; i < 8; i++) foreach (var edge in edges.Where(e => e.Kind == StoryLinkKind.Continuation || e.Kind == StoryLinkKind.Ending))
            {
                var a = Find(edge.From); var b = Find(edge.To);
                if (a != null && b != null && a.Day > 0 && b.Day == 0) { b.Day = a.Day; b.DateSource = "随上游排布（非日期入口）"; }
            }
            Nodes = nodes.Values.OrderBy(n => n.Day == 0 ? int.MaxValue : n.Day).ThenBy(n => n.Group, StringComparer.Ordinal).ThenBy(n => n.Key, StringComparer.Ordinal).ToArray();
            foreach (var n in Nodes) adjacency[n.Id] = new List<StoryGraphEdge>();
            foreach (var e in edges) { adjacency[e.From].Add(e); if (e.To != e.From) adjacency[e.To].Add(e); }
        }
        private string Property(NarrativeEventDefinition e, string key)
        {
            string found = e.eventProperties?.FirstOrDefault(p => p != null && p.property == key)?.value;
            if (found != null) return found;
            foreach (string part in (e.eventProperty ?? "").Split(';')) { int split = part.IndexOf('='); if (split > 0 && part.Substring(0, split).Trim() == key) return part.Substring(split + 1).Trim(); }
            return null;
        }
        private static string GroupFor(string id)
        {
            if (id.StartsWith("neutral.") || id == "oct07.s1") return "neutral";
            if (id.StartsWith("rose.")) return "rose";
            if (id.StartsWith("church.")) return "church";
            if (id.StartsWith("revolution.") || id == "oct07.s2") return "revolution";
            if (id.StartsWith("daily.")) return "daily";
            if (id.StartsWith("newspaper.")) return "newspaper";
            if (id.StartsWith("news.")) return "news";
            if (id.StartsWith("arrival.")) return "opening";
            return "sample";
        }
        private static int DateInName(string key)
        {
            var m = Regex.Match(key ?? "", @"(?:^|\.)(10|11)(\d{2})(?:\.|$)");
            if (!m.Success) return 0;
            try { return (new DateTime(2000, int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)) - new DateTime(2000, 10, 6)).Days + 1; } catch (ArgumentOutOfRangeException) { return 0; }
        }
        public static string DateLabel(int day) => day <= 0 ? "续接 / 未指定" : day > 366 ? "Day " + day : new DateTime(2000, 10, 6).AddDays(day - 1).ToString("MM/dd", CultureInfo.InvariantCulture) + " · D" + day;
        public static string GroupLabel(string group)
        {
            switch (group) {
                case "neutral": return "中立 / 经营"; case "rose": return "新玫瑰"; case "church": return "王庭教会"; case "revolution": return "革命联合";
                case "daily": return "地图日常"; case "opening": return "开场"; case "tutorial": return "教程"; case "ambient": return "咖啡馆片段";
                case "newspaper": return "报纸"; case "news": return "公开消息"; case "ending": return "结局"; case "fact": return "表外 / 未找到生产者"; default: return "演示 / 测试";
            }
        }
        private void Owner(string owner, string node, string context)
        {
            if (string.IsNullOrWhiteSpace(owner)) return;
            if (!owners.TryGetValue(owner, out var list)) owners[owner] = list = new List<Producer>();
            if (!list.Any(x => x.Node == node && x.Context == context)) list.Add(new Producer { Node = node, Context = context });
        }
        private void BuildNodes()
        {
            foreach (var e in config.events.Where(e => e != null))
            {
                string id = EventId(e.eventId), group = GroupFor(e.eventId);
                var dates = config.eventDates.Where(d => d.eventId == e.eventId).OrderBy(d => d.day).ToArray();
                int day = dates.FirstOrDefault()?.day ?? 0; string dateSource = day > 0 ? "EventDate 源表" : "";
                if (day == 0) {
                    var calendar = At(conditions, OwnerKey("event", string.IsNullOrWhiteSpace(e.conditionOwnerId) ? e.eventId : e.conditionOwnerId)).FirstOrDefault(c => Strip(c.target, "bar:") == "world.calendar" && (c.condType == "BAR_BETWEEN" || c.condType == "BAR_GTE"));
                    if (calendar != null && int.TryParse(Regex.Match(calendar.value ?? "", @"\d+").Value, out int value)) { day = value; dateSource = "日历条件（非日期入口）"; }
                }
                if (day == 0) { day = DateInName(e.eventId); if (day > 0) dateSource = "仅按 ID 排布"; }
                if (day == 0 && group == "opening") { day = 1; dateSource = "开场分组"; }
                string title = Text(e.titleText), lane = GroupLabel(group);
                if (string.IsNullOrWhiteSpace(title) || title.EndsWith(".title", StringComparison.Ordinal)) title = e.eventId;
                if (group == "daily") lane = Strip(Property(e, "dailyPool") ?? "地图日常", "daily.action.");
                if (lane == "market") lane = "大堂区"; if (lane == "library") lane = "灰墙学院";
                nodes[id] = new StoryGraphNode { Id = id, Key = e.eventId, Kind = StoryNodeKind.Event, Group = group, Lane = lane,
                    Entry = e.entry.ToString(), Day = day, DateSource = dateSource, Title = title, Blocker = Property(e, "blockedBy"), Sample = group == "sample" };
                Owner(e.eventId, id, "根事件结果");
                foreach (var p in At(pages, e.presentationKey)) foreach (var c in At(choices, p.optionGroupKey)) Owner(string.IsNullOrWhiteSpace(c.resultOwnerId) ? c.choiceId : c.resultOwnerId, id, "选项 " + Text(c.textKey));
                var setup = config.setups.FirstOrDefault(s => s.setupKey == e.setupKey);
                if (setup != null && PresentationSlots.Parse(setup).TryGetValue("opts", out string key))
                    foreach (var c in At(choices, key)) Owner(string.IsNullOrWhiteSpace(c.resultOwnerId) ? c.choiceId : c.resultOwnerId, id, "选项 " + Text(c.textKey));
            }
            foreach (var o in config.openingPresentations.Where(o => o != null))
            {
                string group = o.presentationId.StartsWith("cafe.ambient.") ? "ambient" : o.presentationId.StartsWith("cafe_tutorial.") ? "tutorial" : "opening";
                var body = config.openingLines.Where(l => l.presentationId == o.presentationId).OrderBy(l => l.seq).Select(l => Text(l.textKey)).ToArray();
                nodes["opening:" + o.presentationId] = new StoryGraphNode { Id = "opening:" + o.presentationId, Key = o.presentationId, Kind = StoryNodeKind.Opening,
                    Group = group, Lane = GroupLabel(group), Title = Text(o.titleKey), Day = group == "ambient" ? 0 : 1, Entry = "代码 / 教程入口", DateSource = "分组排布", Snippet = body.FirstOrDefault() ?? "", SearchText = string.Join("\n", body) };
            }
            foreach (var e in config.endings.Where(e => e != null)) nodes["ending:" + e.endingId] = new StoryGraphNode { Id = "ending:" + e.endingId, Key = e.endingId, Kind = StoryNodeKind.Ending,
                Group = "ending", Lane = "结局", Title = Text(e.titleText), Entry = "安全边界检查", Snippet = Text(e.messageText), Priority = e.priority, Sample = !e.enabled, Disabled = !e.enabled };
            foreach (var e in config.events.Where(e => e != null))
            {
                var node = nodes[EventId(e.eventId)]; var keys = new HashSet<string>(); var pageIds = new List<string>();
                foreach (var p in At(pages, e.presentationKey).OrderBy(p => p.seq)) {
                    pageIds.Add(p.pageId);
                    foreach (var l in At(lines, p.pageId).OrderBy(l => l.seq)) { keys.Add(l.textKey); if (string.IsNullOrEmpty(node.Snippet)) node.Snippet = Text(l.textKey); }
                    foreach (var c in At(choices, p.optionGroupKey)) keys.Add(c.textKey);
                    foreach (var rp in At(readings, p.readingKey)) {
                        pageIds.Add(rp.pageId);
                        foreach (var l in At(readingLines, rp.pageId)) keys.Add(l.textKey);
                    }
                }
                foreach (var p in At(readings, e.presentationKey)) {
                    pageIds.Add(p.pageId);
                    foreach (var l in At(readingLines, p.pageId)) { keys.Add(l.textKey); if (string.IsNullOrEmpty(node.Snippet)) node.Snippet = Text(l.textKey); }
                }
                foreach (string key in e.contentKeys ?? new List<string>()) keys.Add(key);
                foreach (var n in config.presentations.notices.Where(n => n.presentationKey == e.presentationKey)) { keys.Add(n.titleKey); keys.Add(n.textKey); }
                node.SearchText = node.Key + "\n" + node.Title + "\n" + node.Snippet + "\n" + string.Join("\n", keys.Where(k => k != null).Select(Text)) + "\n" + string.Join("\n", pageIds.Distinct(StringComparer.Ordinal));
            }
        }
        private void BindCheckOwners()
        {
            foreach(var e in config.events) {
                var checkIds = new HashSet<string>();
                var setup=config.setups.FirstOrDefault(s=>s.setupKey==e.setupKey);
                if(!string.IsNullOrWhiteSpace(setup?.checkId))checkIds.Add(setup.checkId);
                foreach(var p in At(pages,e.presentationKey)) {
                    if(!string.IsNullOrWhiteSpace(p.checkId))checkIds.Add(p.checkId);
                    foreach(var c in At(choices,p.optionGroupKey))if(!string.IsNullOrWhiteSpace(c.checkId))checkIds.Add(c.checkId);
                }
                foreach(var check in config.abilityChecks.Where(c=>checkIds.Contains(c.checkId))) {
                    if(!string.IsNullOrWhiteSpace(check.successResultOwnerId)&&check.successResultOwnerId!=check.successEventId)Owner(check.successResultOwnerId,EventId(e.eventId),"检定成功 "+check.checkId);
                    if(!string.IsNullOrWhiteSpace(check.failureResultOwnerId)&&check.failureResultOwnerId!=check.failureEventId)Owner(check.failureResultOwnerId,EventId(e.eventId),"检定失败 "+check.checkId);
                }
            }
        }
        private string Guards(string result)
        {
            var cs = At(conditions, OwnerKey("result", result));
            return cs.Count == 0 ? "" : string.Join(" OR ", cs.GroupBy(c => c.groupIndex).Select(g => "(" + string.Join(" AND ", g.Select(ConditionText)) + ")"));
        }
        private void Edge(string from, string to, StoryLinkKind kind, string label, string evidence, int group = 0, bool conditional = false)
        {
            if (!nodes.ContainsKey(from) || !nodes.ContainsKey(to) || from == to) return;
            if (!edgeKeys.Add(from + "\n" + to + "\n" + kind + "\n" + label + "\n" + group + "\n" + evidence)) return;
            edges.Add(new StoryGraphEdge { From = from, To = to, Kind = kind, Label = label, Evidence = evidence, ConditionGroup = group, Conditional = conditional });
        }
        private void FactEdges(string fact, string to, bool positive, string label, string evidence, int group, StoryLinkKind kind)
        {
            if (producers.TryGetValue(fact, out var ps) && ps.Count > 0) {
                foreach (var p in ps) Edge(p.Node, to, positive ? kind : StoryLinkKind.Exclusion, label + (p.Context == "根事件结果" ? "" : " · 分支"), evidence + "\n生产条件: " + p.Context, group, p.Context != "根事件结果");
                return;
            }
            var c = config.conditionKeys.FirstOrDefault(x => x.key == fact); string id = "fact:" + fact;
            if (!nodes.ContainsKey(id)) nodes[id] = new StoryGraphNode { Id = id, Key = fact, Kind = StoryNodeKind.Fact, Group = "fact", Lane = "表外条件", Title = fact,
                Entry = c != null && c.initialValue ? "初始配置为真" : "未找到表内置真来源", Snippet = "可能由代码宿主提供；不推测运行状态。", SearchText = fact };
            Edge(id, to, positive ? StoryLinkKind.Unresolved : StoryLinkKind.Exclusion, label, evidence + "\n未找到表内生产者（不等于确认缺实现）。", group);
        }
        private void ConditionsFor(string type, string owner, string to, StoryLinkKind kind = StoryLinkKind.Prerequisite)
        {
            foreach (var c in At(conditions, OwnerKey(type, owner))) {
                string target = Strip(Strip(c.target, "event:"), "cond:");
                string proof = "Condition " + c.conditionId + "\nowner=" + type + ":" + owner + "\n组 " + c.groupIndex + "（组内 AND，组间 OR）\n" + ConditionText(c);
                if (c.condType == "EVENT_HAPPENED" || c.condType == "EVENT_NOT_HAPPENED") Edge(EventId(target), to, c.condType == "EVENT_NOT_HAPPENED" ? StoryLinkKind.Exclusion : kind, c.condType + " " + target, proof, c.groupIndex);
                else if (c.condType == "BOOL_EQ" && bool.TryParse(c.value, out bool expected) && !new[] { "choice:", "bar:", "not:", "check:", "gameplay:", "anyChoice:" }.Any(p => target.StartsWith(p))) {
                    if (target == "story." + owner + ".completed" && !expected) continue;
                    FactEdges(target, to, expected, target + "=" + expected.ToString().ToLowerInvariant(), proof, c.groupIndex, kind);
                }
            }
        }
        private void BuildRelationships()
        {
            foreach (var r in config.results.Where(r => r != null)) foreach (var p in At(owners, r.eventId)) {
                string guard = Guards(r.resultId), context = p.Context + (guard.Length == 0 ? "" : "；" + guard);
                if ((r.resultType == NarrativeResultType.SetFlag || r.resultType == NarrativeResultType.SetSwitch) && bool.TryParse(r.value, out bool on) && on) {
                    string fact = Strip(r.target, "cond:"); if (!producers.TryGetValue(fact, out var list)) producers[fact] = list = new List<Producer>();
                    if (!list.Any(x => x.Node == p.Node && x.Context == context)) list.Add(new Producer { Node = p.Node, Context = context });
                }
                if (r.resultType == NarrativeResultType.QueueEvent) Edge(p.Node, EventId(Strip(r.target, "event:")), StoryLinkKind.Continuation, "续接" + (context == "根事件结果" ? "" : " · 分支"), "Result " + r.resultId + "\n" + context, conditional: context != "根事件结果");
            }
            foreach (var e in config.events.Where(e => e != null)) {
                string id = EventId(e.eventId); ConditionsFor("event", string.IsNullOrWhiteSpace(e.conditionOwnerId) ? e.eventId : e.conditionOwnerId, id);
                foreach (var a in config.actions.Where(a => a.resultEvents != null && a.resultEvents.Contains(e.eventId))) ConditionsFor("action", a.actionKey, id);
                var checks = new HashSet<string>(); var setup = config.setups.FirstOrDefault(s => s.setupKey == e.setupKey);
                if (!string.IsNullOrWhiteSpace(setup?.checkId)) checks.Add(setup.checkId);
                foreach (var p in At(pages, e.presentationKey)) { if (!string.IsNullOrWhiteSpace(p.checkId)) checks.Add(p.checkId); foreach (var c in At(choices, p.optionGroupKey)) if (!string.IsNullOrWhiteSpace(c.checkId)) checks.Add(c.checkId); }
                foreach (var c in config.abilityChecks.Where(c => checks.Contains(c.checkId))) {
                    if (!string.IsNullOrWhiteSpace(c.successEventId)) Edge(id, EventId(c.successEventId), StoryLinkKind.CheckSuccess, "检定成功", "AbilityCheck " + c.checkId, conditional: true);
                    if (!string.IsNullOrWhiteSpace(c.failureEventId)) Edge(id, EventId(c.failureEventId), StoryLinkKind.CheckFailure, "检定失败", "AbilityCheck " + c.checkId, conditional: true);
                }
            }
            foreach (var end in config.endings.Where(e => e != null)) ConditionsFor("ending", end.endingId, "ending:" + end.endingId, StoryLinkKind.Ending);
            foreach (var s in config.cafeTutorialSteps.Where(s => s != null && !string.IsNullOrWhiteSpace(s.nextStepId))) {
                var next = config.cafeTutorialSteps.FirstOrDefault(n => n.stepId == s.nextStepId && n.tutorialId == s.tutorialId);
                if (next != null) Edge("opening:" + s.presentationId, "opening:" + next.presentationId, StoryLinkKind.Continuation, "教程步骤", "CafeTutorialStep " + s.stepId + " -> " + next.stepId, conditional: true);
            }
        }
        public static string ConditionText(NarrativeConditionDefinition c) => c.condType + "(" + c.target + ", " + c.value + ")";
        private void AppendConditions(StringBuilder b, string type, string key)
        {
            b.AppendLine().AppendLine("【" + type + " 前置：组内 AND，组间 OR】");
            var cs = At(conditions, OwnerKey(type, key));
            if (cs.Count == 0) b.AppendLine("无表内条件（仍受运行时入口、忙碌和宿主门禁限制）。");
            foreach (var g in cs.GroupBy(c => c.groupIndex).OrderBy(g => g.Key)) {
                b.AppendLine("组 " + g.Key + ":"); foreach (var c in g) b.AppendLine("  AND " + ConditionText(c) + "  [" + c.conditionId + "]");
            }
        }
        public string Describe(string id)
        {
            var n = Find(id); if (n == null) return "没有选择节点。";
            var b = new StringBuilder(); b.AppendLine(n.Key).AppendLine().AppendLine("【状态】");
            b.AppendLine(n.Blocked ? "明确施工阻塞：" + n.Blocker : "配置浏览；不是已自然触发 / 实机验收声明。");
            b.AppendLine("类型：" + n.Kind + "　分组：" + GroupLabel(n.Group)); b.AppendLine("排布日期：" + DateLabel(n.Day) + "　" + n.DateSource);
            if (n.Kind == StoryNodeKind.Event) {
                var e = config.events.First(x => EventId(x.eventId) == id);
                b.AppendLine().AppendLine("【入口】").AppendLine(e.entry + "；onceOnly=" + e.onceOnly + "；blocksSave=" + e.blocksSave);
                foreach (var d in config.eventDates.Where(d => d.eventId == e.eventId)) b.AppendLine("EventDate " + DateLabel(d.day) + " / " + d.timeSlot + " / " + d.repeatRule + " / priority=" + d.priority);
                foreach (var a in config.actions.Where(a => a.resultEvents != null && a.resultEvents.Contains(e.eventId))) {
                    string places = string.Join("、", config.mapPositions.Where(p => p.defaultAction == a.actionKey || p.extendedActions != null && p.extendedActions.Contains(a.actionKey)).Select(p => Text(p.nameText)));
                    b.AppendLine("行动：" + Text(a.nameText) + " [" + a.actionKey + "] @ " + places + "\n消耗=" + a.requirements + "; 必做=" + a.mustExecute);
                }
                AppendConditions(b, "event", string.IsNullOrWhiteSpace(e.conditionOwnerId) ? e.eventId : e.conditionOwnerId);
                b.AppendLine().AppendLine("【结果：根尾提交，不代表每条路径都执行】");
                foreach (var pair in owners.Where(p => p.Value.Any(x => x.Node == id))) foreach (var r in At(results, pair.Key).OrderBy(r => r.seq)) {
                    b.AppendLine(r.resultId + "\n  " + r.resultType + " " + r.target + " = " + r.value);
                    string guard = Guards(r.resultId); if (guard.Length > 0) b.AppendLine("  条件：" + guard);
                    foreach (var source in pair.Value.Where(x => x.Node == id && x.Context != "根事件结果")) b.AppendLine("  " + source.Context);
                }
                b.AppendLine().AppendLine("【玩法 / 检定 / 资源】");
                foreach (var p in At(pages, e.presentationKey).Where(p => p.stepType != NarrativeConversationStepType.Dialogue || !string.IsNullOrWhiteSpace(p.bgOverride)))
                    b.AppendLine(p.pageId + " | " + p.stepType + (string.IsNullOrWhiteSpace(p.gameplayKey) ? "" : " | " + p.gameplayKey) + (string.IsNullOrWhiteSpace(p.checkId) ? "" : " | check=" + p.checkId) + (string.IsNullOrWhiteSpace(p.bgOverride) ? "" : " | bg=" + p.bgOverride));
            }
            else if (n.Kind == StoryNodeKind.Ending) {
                var e = config.endings.First(x => x.endingId == n.Key); b.AppendLine("enabled=" + e.enabled + "; priority=" + e.priority); AppendConditions(b, "ending", e.endingId); b.AppendLine().AppendLine(Text(e.messageText));
            }
            else if (n.Kind == StoryNodeKind.Fact) {
                var c = config.conditionKeys.FirstOrDefault(x => x.key == n.Key);
                b.AppendLine("没有扫描到表内置真 Result；可能由代码宿主、初始状态或未接入内容生产。不是运行状态诊断。");
                if (c != null) b.AppendLine("初始=" + c.initialValue + "; onlySetTrue=" + c.onlySetTrue);
            }
            else b.AppendLine("OpeningPresentation 独立演出快照，不等同 Event 结果事务。此界面不播放或结算。");
            return b.ToString();
        }
        public IReadOnlyList<StoryContentBlock> Content(string id)
        {
            if (content.TryGetValue(id, out var cached)) return cached;
            var n = Find(id); var blocks = new List<StoryContentBlock>(); if (n == null) return blocks;
            if (n.Kind == StoryNodeKind.Opening) {
                var b = new StringBuilder(); var body = new StringBuilder(); var diagnostics = new StringBuilder();
                foreach (var l in config.openingLines.Where(l => l.presentationId == n.Key).OrderBy(l => l.seq)) {
                    b.AppendLine("[" + l.lineId + "] " + l.speaker + "\n" + Text(l.textKey) + "\n");
                    body.AppendLine(Text(l.textKey)).AppendLine();
                    diagnostics.AppendLine("OpeningLine " + l.lineId + "; seq=" + l.seq + "; speaker=" + l.speaker + "; gesture=" + l.gesture);
                }
                blocks.Add(CreateBlock(n.Key, "开场演出快照", b.ToString(), body.ToString(), diagnostics.ToString(), "开场演出"));
            }
            if (n.Kind == StoryNodeKind.Event) {
                var e = config.events.First(x => x.eventId == n.Key);
                foreach (var p in At(pages, e.presentationKey).OrderBy(p => p.seq)) {
                    var b = new StringBuilder(); b.AppendLine("Page " + p.pageId).AppendLine("步骤：" + p.stepType + "　seq=" + p.seq);
                    if (!string.IsNullOrWhiteSpace(p.pageCondKeys)) b.AppendLine("显示条件（AND）：" + p.pageCondKeys);
                    if (!string.IsNullOrWhiteSpace(p.nextPageId)) b.AppendLine("显式下一页：" + p.nextPageId);
                    if (!string.IsNullOrWhiteSpace(p.gameplayKey)) b.AppendLine("等待真实玩法：" + p.gameplayKey);
                    if (!string.IsNullOrWhiteSpace(p.checkId)) {
                        b.AppendLine("检定：" + p.checkId); foreach (var c in config.abilityCheckConditions.Where(c => c.checkId == p.checkId)) b.AppendLine("  组" + c.groupIndex + " " + c.attributeId + ">=" + c.threshold);
                    }
                    if (!string.IsNullOrWhiteSpace(p.bgOverride)) b.AppendLine("背景：" + p.bgOverride);
                    var body = new StringBuilder(); var diagnostics = new StringBuilder(b.ToString());
                    if (!string.IsNullOrWhiteSpace(p.pageBarConds)) diagnostics.AppendLine("属性条件（原配置）：" + p.pageBarConds);
                    if (!string.IsNullOrWhiteSpace(p.bgmOverride)) diagnostics.AppendLine("音乐：" + p.bgmOverride);
                    if (!string.IsNullOrWhiteSpace(p.optionGroupKey)) diagnostics.AppendLine("选项组：" + p.optionGroupKey);
                    if (!string.IsNullOrWhiteSpace(p.readingKey)) diagnostics.AppendLine("阅读配置：" + p.readingKey);
                    b.AppendLine();
                    foreach (var l in At(lines, p.pageId).OrderBy(l => l.seq)) {
                        var actor = config.objects.FirstOrDefault(o => o.objId == l.speakerKey);
                        string who = !string.IsNullOrWhiteSpace(l.speakerNameKey) ? Text(l.speakerNameKey) : actor == null ? "旁白" : Text(actor.nameText);
                        b.AppendLine(who + (string.IsNullOrWhiteSpace(l.emote) ? "" : " [" + l.emote + "]") + "：" + Text(l.textKey)).AppendLine("  ↳ " + l.lineId);
                        body.AppendLine(who + "：" + Text(l.textKey));
                        diagnostics.AppendLine("Line " + l.lineId + "; seq=" + l.seq + "; speaker=" + l.speakerKey + "; emote=" + l.emote);
                    }
                    foreach (var c in At(choices, p.optionGroupKey).OrderBy(c => c.seq)) {
                        b.AppendLine().AppendLine("选项 " + c.seq + "：" + Text(c.textKey)).AppendLine("  " + c.choiceId + (string.IsNullOrWhiteSpace(c.checkId) ? "" : "; check=" + c.checkId));
                        foreach (var condition in At(conditions, OwnerKey("choice", c.choiceId))) b.AppendLine("  可选条件：" + ConditionText(condition));
                        body.AppendLine("选项 " + c.seq + "：" + Text(c.textKey));
                        diagnostics.AppendLine("选项 " + c.seq + ": " + c.choiceId + "; check=" + c.checkId + "; resultOwnerId=" + c.resultOwnerId);
                        AppendConditions(diagnostics, "choice", c.choiceId);
                    }
                    foreach (var rp in At(readings, p.readingKey).OrderBy(rp => rp.seq)) {
                        AppendReading(b, rp); AppendReadingFields(body, diagnostics, rp);
                    }
                    if (p.stepType == NarrativeConversationStepType.End) {
                        b.AppendLine("本分支终点：完成当前根；后续线性页不再播放。");
                        diagnostics.AppendLine("本分支终点：完成当前根；后续线性页不再播放。仅展示配置，不执行完成回调。");
                    }
                    blocks.Add(CreateBlock(p.pageId, p.seq + ". " + p.pageId, b.ToString(), body.ToString(),
                        diagnostics.ToString(), WithPageMarkers(PageKindLabel(p.stepType), p.pageCondKeys, p.pageBarConds,
                            At(choices, p.optionGroupKey).Count > 0), p.nextPageId));
                }
                foreach (var p in At(readings, e.presentationKey).OrderBy(p => p.seq)) {
                    var b = new StringBuilder(); var body = new StringBuilder(); var diagnostics = new StringBuilder();
                    AppendReading(b, p); AppendReadingFields(body, diagnostics, p);
                    blocks.Add(CreateBlock(p.pageId, "阅读 " + p.seq + " · " + Text(p.titleKey), b.ToString(), body.ToString(), diagnostics.ToString(), WithPageMarkers("阅读", p.pageCondKeys, p.pageBarConds)));
                }
                foreach (var notice in config.presentations.notices.Where(p => p.presentationKey == e.presentationKey)) {
                    string text = Text(notice.titleKey) + "\n" + Text(notice.timeKey) + "\n\n" + Text(notice.textKey);
                    blocks.Add(CreateBlock(notice.presentationKey, "通知", text, text,
                        "Notice " + notice.presentationKey + "\nimageKey=" + notice.imageKey + "\nbarConds=" + notice.barConds + "\nnoticeBarConds=" + notice.noticeBarConds, "通知"));
                }
                if (blocks.Count == 0) {
                    string body = string.Join("\n\n", (e.contentKeys ?? new List<string>()).Select(Text));
                    string diagnostics = string.IsNullOrWhiteSpace(e.setupKey) ? "" : "SetupParams: " + e.setupKey;
                    blocks.Add(CreateBlock(e.eventId, "事件文本 / 调度节点", body + "\n\n" + diagnostics, body, diagnostics, "事件文本 / 调度节点"));
                }
            }
            if (blocks.Count == 0) {
                string description = Describe(id);
                // Ending prose is readable content; fact/config diagnostics are not fabricated narrative.
                blocks.Add(CreateBlock(n.Key, "配置说明", description,
                    n.Kind == StoryNodeKind.Ending ? n.Snippet : "", description, n.Kind == StoryNodeKind.Ending ? "结局" : "配置说明"));
            }
            content[id] = blocks; return blocks;
        }
        private static StoryContentBlock CreateBlock(string id, string caption, string text, string body,
            string diagnostics, string kind, string nextPageId = null)
        {
            body = PlainBody(body);
            string summary = body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? kind;
            summary = Regex.Replace(summary, @"\s+", " ").Trim();
            // Avoid cutting a surrogate pair or combining sequence in a short reader heading.
            var elements = new StringInfo(summary);
            if (elements.LengthInTextElements > 48) summary = elements.SubstringByTextElements(0, 48) + "…";
            return new StoryContentBlock { Id = id, Caption = caption, Text = text, BodyText = body,
                Diagnostics = diagnostics ?? "", KindLabel = kind, Summary = summary, NextPageId = nextPageId ?? "" };
        }
        private static string PlainBody(string value)
        {
            // Strip known display markup only; preserve literal comparisons and unresolved text tokens.
            value = Regex.Replace(value ?? "", @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            return Regex.Replace(value, @"</?(?:b|i|u|s|color|size|mark|alpha|font|link|style|nobr|sub|sup)(?:=[^<>]*|\s+[^<>]*)?>", "", RegexOptions.IgnoreCase).Trim();
        }
        // Mark only evidence owned by this page; never imply branch reachability or inherited conditions.
        private static string WithPageMarkers(string label, string conditionKeys, string barConditions, bool hasChoices = false)
            => label + (!string.IsNullOrWhiteSpace(conditionKeys) || !string.IsNullOrWhiteSpace(barConditions) ? " · 条件页" : "")
                + (hasChoices ? " · 选项" : "");
        private static string PageKindLabel(NarrativeConversationStepType kind)
        {
            switch (kind) {
                case NarrativeConversationStepType.Dialogue: return "对话（Dialogue）";
                case NarrativeConversationStepType.PassiveCheck: return "被动检定（PassiveCheck）";
                case NarrativeConversationStepType.LetterReveal: return "信件揭示（LetterReveal）";
                case NarrativeConversationStepType.LetterRead: return "信件阅读（LetterRead）";
                case NarrativeConversationStepType.End: return "分支终点（End）";
                case NarrativeConversationStepType.Gameplay: return "玩法交互（Gameplay）";
                default: return kind.ToString();
            }
        }
        private void AppendReadingFields(StringBuilder body, StringBuilder diagnostics, NarrativeReadingPageDefinition p)
        {
            body.AppendLine(Text(p.titleKey));
            diagnostics.AppendLine("ReadingPage " + p.pageId + "; seq=" + p.seq + "; imageKey=" + p.imageKey);
            if (!string.IsNullOrWhiteSpace(p.pageCondKeys)) diagnostics.AppendLine("显示条件（AND）：" + p.pageCondKeys);
            if (!string.IsNullOrWhiteSpace(p.pageBarConds)) diagnostics.AppendLine("属性条件（原配置）：" + p.pageBarConds);
            foreach (var l in At(readingLines, p.pageId).OrderBy(l => l.seq)) {
                body.AppendLine(Text(l.textKey));
                diagnostics.AppendLine("ReadingLine " + l.lineId + "; seq=" + l.seq + "; mode=" + l.mode + "; speaker=" + l.speakerKey);
            }
        }
        private void AppendReading(StringBuilder b, NarrativeReadingPageDefinition p)
        {
            b.AppendLine().AppendLine("阅读：" + p.pageId + " " + Text(p.titleKey));
            foreach (var l in At(readingLines, p.pageId).OrderBy(l => l.seq)) b.AppendLine(Text(l.textKey));
        }
    }
}
