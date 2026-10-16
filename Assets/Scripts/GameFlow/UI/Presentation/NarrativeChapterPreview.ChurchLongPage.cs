using System;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    public sealed partial class NarrativeChapterPreview
    {
        /// <summary>Source-only E fixture: no presenter, audio, history or business work during reconstruction.</summary>
        private sealed class ChurchLongPageConstruction
        {
            private const string Chapter = "church.1104.s1";
            private readonly NarrativeConfiguration config;
            private readonly NarrativeConversationPageDefinition target;
            private readonly Dictionary<string, string> fixtures = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly HashSet<string> selected = new HashSet<string>(StringComparer.Ordinal);
            private readonly List<string> path = new List<string>();
            private readonly int stage, perception, wisdom;
            private readonly bool? revolution;
            private int nextPage;
            private DialogueCharacter left, right, center;
            public readonly List<string> PreviousChoices = new List<string>();
            public string Stage => stage == 0 ? "morning" : stage == 1 ? "tour" : "night";
            public bool Reached { get; private set; }

            public ChurchLongPageConstruction(NarrativeConfiguration config, string pageId, string fee, string route,
                string night, int perception, int wisdom, bool? revolution)
            {
                this.config = config;
                this.perception = perception; this.wisdom = wisdom; this.revolution = revolution;
                AddFixture("fee", fee, "free", "one", "quoted");
                AddFixture("route", route, "far", "near");
                AddFixture("night", night, "wait", "ask", "hurry");
                var root = config.events.Single(e => e.eventId == Chapter);
                var pages = config.presentations.conversationPages.Where(p => p.presentationKey == root.presentationKey)
                    .OrderBy(p => p.seq).ToArray();
                target = pages.SingleOrDefault(p => p.pageId == pageId);
                if (target == null || target.stepType != NarrativeConversationStepType.Dialogue
                    && target.stepType != NarrativeConversationStepType.PassiveCheck
                    || !config.presentations.conversationLines.Any(l => l.pageId == target.pageId && !string.IsNullOrWhiteSpace(l.textKey)))
                    throw new ArgumentException("Expected an E Dialogue or PassiveCheck page with body: " + pageId, nameof(pageId));
                stage = PageStage(target);
                if (stage == 2 && !revolution.HasValue)
                    throw new InvalidOperationException("Night construction requires an explicit revolution fixture; unknown is not false.");

                // Earlier phases are not replayed or promoted to facts. Seed their explicit local choices only.
                foreach (var page in pages.Where(p => PageStage(p) < stage && !string.IsNullOrWhiteSpace(p.optionGroupKey)))
                {
                    string choice = RequireFixture(page.optionGroupKey);
                    PreviousChoices.Add(choice); selected.Add(choice);
                }
                foreach (var page in pages.Where(p => PageStage(p) == stage && p.seq <= target.seq))
                {
                    if (!Visible(page)) continue;
                    if (page.stepType == NarrativeConversationStepType.PassiveCheck && !Check(page.checkId)) continue;
                    if (page.stepType != NarrativeConversationStepType.Dialogue && page.stepType != NarrativeConversationStepType.PassiveCheck)
                        throw new InvalidOperationException("A control step precedes the preview target: " + page.pageId);
                    path.Add(page.pageId);
                    if (page == target) return; // Its options, if any, remain genuinely interactive.
                    if (!string.IsNullOrWhiteSpace(page.optionGroupKey)) selected.Add(RequireFixture(page.optionGroupKey));
                }
                throw new InvalidOperationException("Preview target is unreachable with these fixtures: " + pageId);
            }

            private void AddFixture(string decision, string value, params string[] allowed)
            {
                if (value != null && !allowed.Contains(value))
                    throw new ArgumentException("Unsupported " + decision + " fixture: " + value, decision);
                fixtures.Add(Chapter + "." + decision + ".choices", value == null ? null : Chapter + "." + decision + "." + value);
            }

            private string RequireFixture(string group)
            {
                if (!fixtures.TryGetValue(group, out string choice) || choice == null)
                    throw new InvalidOperationException("An explicit skipped-decision fixture is required: " + group);
                if (!config.choices.Any(c => c.groupKey == group && c.choiceId == choice))
                    throw new InvalidOperationException("Preview choice is not in the source option group: " + choice);
                return choice;
            }

            private static int PageStage(NarrativeConversationPageDefinition page)
            {
                var gates = (page.pageCondKeys ?? string.Empty).Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim()).Where(k => k.StartsWith("checkpointStage:", StringComparison.Ordinal)).ToArray();
                if (gates.Length != 1 || !int.TryParse(gates[0].Substring(16), out int value) || value < 0 || value > 2)
                    throw new InvalidOperationException("Expected one bounded E checkpoint stage: " + page.pageId);
                return value;
            }

            private bool Visible(NarrativeConversationPageDefinition page)
            {
                if (!string.IsNullOrWhiteSpace(page.pageBarConds))
                    throw new InvalidOperationException("Unsupported E page bar guard: " + page.pageId);
                return (page.pageCondKeys ?? string.Empty).Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .All(k => Guard(k.Trim()));
            }

            private bool Guard(string key)
            {
                if (key.StartsWith("not:", StringComparison.Ordinal)) return !Guard(key.Substring(4));
                if (key.StartsWith("checkpointStage:", StringComparison.Ordinal)) return key.Substring(16) == stage.ToString();
                if (key.StartsWith("choice:", StringComparison.Ordinal)) return selected.Contains(key.Substring(7));
                if (key.StartsWith("anyChoice:", StringComparison.Ordinal)) return key.Substring(10).Split(';').Any(selected.Contains);
                if (key.StartsWith("check:", StringComparison.Ordinal)) return Check(key.Substring(6));
                if (key == "dependency:E-B03.revolution")
                    return revolution ?? throw new InvalidOperationException("Revolution fixture unavailable.");
                throw new InvalidOperationException("Unsupported E preview guard: " + key);
            }

            private bool Check(string checkId)
            {
                var definition = config.abilityChecks.SingleOrDefault(c => c.checkId == checkId);
                if (definition == null || definition.mode != NarrativeAbilityCheckMode.Inline
                    || !string.IsNullOrWhiteSpace(definition.successEventId) || !string.IsNullOrWhiteSpace(definition.failureEventId)
                    || !string.IsNullOrWhiteSpace(definition.successResultOwnerId) || !string.IsNullOrWhiteSpace(definition.failureResultOwnerId))
                    throw new InvalidOperationException("Expected a result-free E inline check: " + checkId);
                return NarrativeAbilityCheckEvaluator.Evaluate(definition,
                    config.abilityCheckConditions.Where(c => c.checkId == checkId).ToArray(),
                    id => id == "perception" ? (true, perception) : id == "wisdom" ? (true, wisdom) : (false, 0)).passed;
            }

            public bool Playback(NarrativeEventRequest request, NarrativeConversationPresentationData data, Action done)
            {
                if (Reached) return NarrativeConversationEventPlayback.PlayPreview(request, data, done);
                string pageId = data.lines.Count > 0 ? data.lines[0].pageId : null;
                // Refuse a source/config mismatch before playing any later visible UI.
                if (nextPage >= path.Count || pageId != path[nextPage++])
                    throw new InvalidOperationException("Preview did not reach its expected source page: " + target.pageId);
                if (pageId == target.pageId)
                {
                    var targetScript = NarrativeConversationEventPlayback.CreateScript(data);
                    InheritFirstLine(data.lines[0], targetScript[0]);
                    Reached = true;
                    return NarrativeConversationEventPlayback.PlayPreview(request, data, done);
                }
                if (data.options.Count > 0)
                {
                    var option = data.options.SingleOrDefault(o => fixtures.Values.Contains(o.id));
                    if (option == null || !option.enabled || data.selectChoice?.Invoke(option.id) != true)
                        throw new InvalidOperationException("Skipped preview choice rejected: " + pageId);
                }
                var script = NarrativeConversationEventPlayback.CreateScript(data);
                for (int i = 0; i < script.Count; i++)
                {
                    var line = script[i];
                    if (line.ReplaceLeft) left = DialogueCharacter.Resolve(left, line.LeftCharacter);
                    if (line.ReplaceRight) right = DialogueCharacter.Resolve(right, line.RightCharacter);
                    if (line.ReplaceCenter) center = DialogueCharacter.Resolve(center, line.CenterCharacter);
                }
                done?.Invoke();
                return true;
            }

            private static NarrativeCharacterPresentationData Snapshot(DialogueCharacter character)
                => character == null ? null : new NarrativeCharacterPresentationData
                { key = character.CharacterKey, name = character.DisplayName, portrait = character.Portrait };

            private void InheritFirstLine(NarrativeConversationPresentationLine first, DialogueLine line)
            {
                // A source replacement (including '-') wins. Otherwise resolve the actual shared script's
                // implicit speaker command against the inherited slot, retaining same-identity expressions.
                if (!first.replaceStageLeft && first.stageLeft == null)
                {
                    first.stageLeft = Snapshot(line.ReplaceLeft ? DialogueCharacter.Resolve(left, line.LeftCharacter) : left);
                    first.replaceStageLeft = true;
                }
                if (!first.replaceStageRight && first.stageRight == null)
                {
                    first.stageRight = Snapshot(line.ReplaceRight ? DialogueCharacter.Resolve(right, line.RightCharacter) : right);
                    first.replaceStageRight = true;
                }
                if (!first.replaceStageCenter && first.stageCenter == null)
                {
                    first.stageCenter = Snapshot(line.ReplaceCenter ? DialogueCharacter.Resolve(center, line.CenterCharacter) : center);
                    first.replaceStageCenter = true;
                }
            }
        }
    }
}
