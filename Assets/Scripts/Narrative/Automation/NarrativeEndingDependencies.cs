using System;
using System.Collections.Generic;

namespace Spotlight.Narrative
{
    /// <summary>
    /// A bounded, configuration-only reverse evidence graph, not a reachability solver.
    /// References [n] link shared dependencies without recursively expanding cycles.
    /// </summary>
    public static class NarrativeEndingDependencies
    {
        private const int MaxNodes = 3000;

        public static List<string> Explain(NarrativeConfiguration config, string endingId)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(endingId)) throw new ArgumentException("An ending ID is required.", nameof(endingId));
            return new Explanation(config).Run(endingId);
        }

        private sealed class Node
        {
            public int number;
            public string kind, id;
            public object row;
        }

        private sealed class Explanation
        {
            private readonly NarrativeConfiguration config;
            private readonly List<string> lines = new List<string>();
            private readonly Dictionary<string, Node> nodes = new Dictionary<string, Node>(StringComparer.Ordinal);
            private readonly Queue<Node> pending = new Queue<Node>();
            private readonly Dictionary<string, List<NarrativeConditionDefinition>> gates = new Dictionary<string, List<NarrativeConditionDefinition>>(StringComparer.Ordinal);
            private readonly Dictionary<string, List<NarrativeResultDefinition>> producers = new Dictionary<string, List<NarrativeResultDefinition>>(StringComparer.Ordinal);
            private readonly Dictionary<string, List<NarrativeResultDefinition>> queues = new Dictionary<string, List<NarrativeResultDefinition>>(StringComparer.Ordinal);
            private readonly Dictionary<string, List<NarrativeEventDefinition>> events = new Dictionary<string, List<NarrativeEventDefinition>>(StringComparer.Ordinal);
            private readonly Dictionary<string, List<NarrativeChoiceDefinition>> choices = new Dictionary<string, List<NarrativeChoiceDefinition>>(StringComparer.Ordinal);
            private readonly Dictionary<string, List<NarrativeChoiceDefinition>> choiceOwners = new Dictionary<string, List<NarrativeChoiceDefinition>>(StringComparer.Ordinal);
            private readonly Dictionary<string, List<NarrativeConditionKeyDefinition>> facts = new Dictionary<string, List<NarrativeConditionKeyDefinition>>(StringComparer.Ordinal);
            private readonly Dictionary<object, int> rowIds = new Dictionary<object, int>();
            private int references;
            private bool truncated;

            public Explanation(NarrativeConfiguration config)
            {
                this.config = config;
                if (config.conditions != null)
                    foreach (var row in config.conditions)
                    {
                        if (row == null) continue;
                        Identify(row);
                        Index(gates, Owner(row.ownerType, row.ownerId), row);
                    }
                if (config.results != null)
                    foreach (var row in config.results)
                    {
                        if (row == null) continue;
                        Identify(row);
                        if ((row.resultType == NarrativeResultType.SetFlag || row.resultType == NarrativeResultType.SetSwitch)
                            && bool.TryParse(row.value, out bool value) && value)
                            Index(producers, ResultTarget(row), row);
                        if (row.resultType == NarrativeResultType.QueueEvent)
                            Index(queues, Strip(row.target, "event:"), row);
                    }
                if (config.events != null)
                    foreach (var row in config.events) if (row != null) Index(events, row.eventId, row);
                if (config.choices != null)
                    foreach (var row in config.choices)
                    {
                        if (row == null) continue;
                        Index(choices, row.choiceId, row);
                        Index(choiceOwners, string.IsNullOrWhiteSpace(row.resultOwnerId) ? row.choiceId : row.resultOwnerId, row);
                    }
                if (config.conditionKeys != null)
                    foreach (var row in config.conditionKeys) if (row != null) Index(facts, row.key, row);
            }

            public List<string> Run(string endingId)
            {
                lines.Add("Configuration evidence / possible dependency risks only. Groups: AND within, OR between; producers and QUEUE_EVENT sources are alternatives. A risk on one branch is not a whole-ending verdict.");
                lines.Add("Runtime/host writes, timing, result order, presentation page availability, checks and event scheduling are not solved. Node limit: 3000; repeated nodes are references, not proof of a blocking cycle.");
                Link(null, "ending", endingId, null, "root");
                while (pending.Count > 0)
                {
                    var node = pending.Dequeue();
                    switch (node.kind)
                    {
                        case "ending": Ending(node); break;
                        case "event": Event(node); break;
                        case "choice": Choice(node); break;
                        case "condition": Condition(node, (NarrativeConditionDefinition)node.row); break;
                        case "result": Result(node, (NarrativeResultDefinition)node.row); break;
                        case "fact": Fact(node); break;
                    }
                }
                if (truncated) lines.Add("Possible dependency risk: graph truncated at the 3000-node/reference budget; omitted evidence remains unknown.");
                return lines;
            }

            private void Ending(Node node)
            {
                bool found = false;
                if (config.endings != null)
                    foreach (var row in config.endings)
                        if (row != null && row.endingId == node.id)
                        {
                            found = true;
                            Note(node, "ending enabled=" + row.enabled + ", priority=" + row.priority + " (metadata only)");
                        }
                if (!found) Risk(node, "ending definition is missing; inspecting matching conditions only");
                if (!Gates(node, "ending", node.id)) Risk(node, "no ending conditions are configured");
            }

            private void Event(Node node)
            {
                if (!events.TryGetValue(node.id, out var definitions))
                {
                    Risk(node, "event definition not found; this may be a virtual result owner/host entry");
                    Gates(node, "event", node.id);
                }
                else foreach (var row in definitions)
                {
                    string owner = string.IsNullOrWhiteSpace(row.conditionOwnerId) ? row.eventId : row.conditionOwnerId;
                    Gates(node, "event", owner);
                    if (row.eventProperties == null) continue;
                    foreach (var property in row.eventProperties)
                        if (property != null && string.Equals(property.property, "blockedBy", StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(property.value))
                            Risk(node, "authored event blockedBy=" + property.value + "; evidence for this event branch only");
                }
                if (queues.TryGetValue(node.id, out var incoming))
                    foreach (var result in incoming)
                        Link(node, "result", RowId(result), result, "optional QUEUE_EVENT source (not the only entry)");
            }

            private void Choice(Node node)
            {
                if (!choices.TryGetValue(node.id, out var definitions)) Risk(node, "choice definition is missing");
                else foreach (var row in definitions)
                    Note(node, "choice group=" + row.groupKey + ", resultOwner=" + row.resultOwnerId
                        + (string.IsNullOrWhiteSpace(row.checkId) ? "" : ", check=" + row.checkId + " (not evaluated)"));
                Gates(node, "choice", node.id);
                Note(node, "choice conditions traced; presentation/page placement and actual selection remain unproven");
            }

            private bool Gates(Node parent, string type, string id)
            {
                if (!gates.TryGetValue(Owner(type, id), out var conditions)) return false;
                foreach (var row in conditions)
                    Link(parent, "condition", RowId(row), row, type + ":" + id + " gate; OR group " + row.groupIndex + " / AND member");
                return true;
            }

            private void Condition(Node node, NarrativeConditionDefinition row)
            {
                Note(node, "condition " + row.conditionId + " owner=" + Owner(row.ownerType, row.ownerId)
                    + " group=" + row.groupIndex + ": " + row.condType + " " + row.target + " " + row.op + " " + row.value
                    + (row.notFlag ? " notFlag=true" : ""));
                if (IsAuthoredFamily(row.target))
                    Note(node, "authored integration/construction/blocked gate; branch-local evidence only");
                if (row.notFlag || !string.IsNullOrWhiteSpace(row.op))
                {
                    Risk(node, "unsupported/invalid condition modifiers; not interpreted");
                    return;
                }
                string type = row.condType?.Trim().ToUpperInvariant();
                if (type == "EVENT_HAPPENED")
                {
                    Link(node, "event", Strip(row.target, "event:"), null, "requires event history; schedule not proven");
                    return;
                }
                if (type != "BOOL_EQ" || !bool.TryParse(row.value, out bool expected) || string.IsNullOrWhiteSpace(row.target))
                {
                    Note(node, "non-positive-bool or unsupported condition retained as evidence; not evaluated");
                    return;
                }
                string target = row.target;
                string ownerType = NormalType(row.ownerType);
                if (ownerType == "result" || ownerType == "action")
                {
                    target = target.Trim();
                    while (target.StartsWith("not:", StringComparison.Ordinal))
                    {
                        expected = !expected;
                        target = target.Substring(4).Trim();
                    }
                    if (expected && target.StartsWith("choice:", StringComparison.Ordinal))
                    {
                        Link(node, "choice", target.Substring(7), null, "requires selected choice; trace choice conditions");
                        return;
                    }
                    if (expected && target.StartsWith("anyChoice:", StringComparison.Ordinal))
                    {
                        foreach (string id in target.Substring(10).Split(';'))
                            Link(node, "choice", id, null, "alternative anyChoice member (OR)");
                        return;
                    }
                    if (target.IndexOf(':') >= 0)
                    {
                        Note(node, "presentation/numeric/negative-choice guard is not resolved: " + target);
                        return;
                    }
                }
                if (!expected)
                {
                    Note(node, "requires false; true producers are not prerequisites (clear/order effects not solved)");
                    return;
                }
                Link(node, "fact", target, null, "requires true");
            }

            private void Fact(Node node)
            {
                bool initiallyTrue = false, initiallyFalse = false;
                if (facts.TryGetValue(node.id, out var declarations))
                    foreach (var row in declarations)
                    {
                        initiallyTrue |= row.initialValue;
                        initiallyFalse |= !row.initialValue;
                    }
                Note(node, "initial=" + (initiallyTrue && initiallyFalse ? "conflicting declarations" : initiallyTrue ? "true" : initiallyFalse ? "false" : "undeclared/unknown"));
                if (initiallyTrue) Note(node, "initial true is an alternative source; producer gates need not all execute");
                if (producers.TryGetValue(node.id, out var writers))
                {
                    foreach (var row in writers)
                        Link(node, "result", RowId(row), row, "alternative SET_FLAG/SET_SWITCH true producer");
                }
                else if (!initiallyTrue)
                    Risk(node, "no configured SET_FLAG/SET_SWITCH true producer; runtime/host writes and other OR branches are not ruled out");
            }

            private void Result(Node node, NarrativeResultDefinition row)
            {
                Note(node, "result " + row.resultId + " event/resultOwner=" + row.eventId + " " + row.resultType + " " + row.target + "=" + row.value);
                Gates(node, "result", row.resultId);
                // Choices may settle virtual result owners, with or without explicit choice: guards.
                bool choiceOwner = choiceOwners.TryGetValue(row.eventId ?? "", out var owners);
                if (choiceOwner)
                    foreach (var choice in owners)
                        Link(node, "choice", choice.choiceId, null, "alternative selected-choice result owner");
                if (events.ContainsKey(row.eventId ?? "") || !choiceOwner)
                    Link(node, "event", row.eventId, null, "producer event/owner gates");
            }

            private void Link(Node parent, string kind, string id, object row, string evidence)
            {
                id = id ?? "";
                string key = kind + ":" + id;
                if (nodes.TryGetValue(key, out var existing))
                {
                    // Bound dense cyclic graphs as well as the number of distinct nodes.
                    if (references++ < MaxNodes)
                        lines.Add(Prefix(parent) + " -> [" + existing.number + "] " + evidence + " (shared/cycle reference; not expanded again)");
                    else truncated = true;
                    return;
                }
                if (nodes.Count >= MaxNodes) { truncated = true; return; }
                var node = new Node { number = nodes.Count + 1, kind = kind, id = id, row = row };
                nodes.Add(key, node);
                pending.Enqueue(node);
                string label = row is NarrativeResultDefinition result ? result.resultId
                    : row is NarrativeConditionDefinition condition ? condition.conditionId : id;
                lines.Add(Prefix(parent) + " -> [" + node.number + "] " + kind + ":" + label + " — " + evidence);
            }

            private void Note(Node node, string text) => lines.Add(Prefix(node) + " " + text);
            private void Risk(Node node, string text) => Note(node, "Possible dependency risk (this branch only): " + text + ".");
            private static string Prefix(Node node) => node == null ? "Root" : "[" + node.number + "]";
            private void Identify(object row) { if (!rowIds.ContainsKey(row)) rowIds.Add(row, rowIds.Count); }
            private string RowId(object row) => rowIds[row].ToString(System.Globalization.CultureInfo.InvariantCulture);
            private static string NormalType(string type) => string.IsNullOrWhiteSpace(type) ? "event" : type.Trim().ToLowerInvariant();
            private static string Owner(string type, string id) => NormalType(type) + ":" + id;
            private static string Strip(string value, string prefix) => value != null && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value.Substring(prefix.Length) : value;
            private static string ResultTarget(NarrativeResultDefinition row) => Strip(row.target, row.resultType == NarrativeResultType.SetFlag ? "flag:" : "switch:");
            private static bool IsAuthoredFamily(string key)
            {
                if (key == null) return false;
                return Family(key, "integration") || Family(key, "construction") || Family(key, "blocked");
            }
            private static bool Family(string key, string family) => key == family || key.StartsWith(family + ".", StringComparison.Ordinal);
            private static void Index<T>(Dictionary<string, List<T>> index, string key, T row)
            {
                if (string.IsNullOrWhiteSpace(key)) return;
                if (!index.TryGetValue(key, out var rows)) index.Add(key, rows = new List<T>());
                rows.Add(row);
            }
        }
    }
}
