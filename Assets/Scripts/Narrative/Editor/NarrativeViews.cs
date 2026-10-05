using System.Linq;
using System;
using Spotlight.Narrative;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Globalization;
namespace Spotlight.Narrative.Editor
{
    public sealed class NarrativeEventByBarWindow : EditorWindow
    {
        enum Filter { All, AnchorsOnly, NonAnchorsOnly }
        Filter filter;
        NarrativeConfiguration configuration;
        string loadError;
        Vector2 scroll;
        void OnEnable() => Reload();
        void Reload()
        {
            try { configuration = LubanNarrativeLoader.Load(); loadError = null; }
            catch (Exception ex) { configuration = null; loadError = ex.Message; }
        }
        [MenuItem("Spotlight/Narrative/EventByBar and Anchors")]
        static void Open() => GetWindow<NarrativeEventByBarWindow>("EventByBar");
        void OnGUI()
        {
            if (GUILayout.Button("Reload Luban data")) Reload();
            var c = configuration;
            if (c == null) { EditorGUILayout.HelpBox(loadError ?? "Generate Luban data first.", MessageType.Error); return; }
            filter = (Filter)EditorGUILayout.EnumPopup("Event filter", filter);
            EditorGUILayout.HelpBox("Copy suggestions to clipboard only. Paste into the matching Excel sheet data area (row 5 onward, column A). No ScriptableObject or workbook is created. Review IDs and shared event onceOnly before generating Luban.", MessageType.Info);
            if (GUILayout.Button("Copy missing reverse Bar rows")) EditorGUIUtility.systemCopyBuffer = BuildReverseRows(c, false);
            if (GUILayout.Button("Copy missing reverse Trigger rows")) EditorGUIUtility.systemCopyBuffer = BuildReverseRows(c, true);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var ev in c.events)
            {
                var paths = c.triggers.Where(t => t.eventId == ev.eventId).ToList();
                bool anchor = IsAnchor(c, ev);
                if (filter == Filter.AnchorsOnly && !anchor || filter == Filter.NonAnchorsOnly && anchor) continue;
                EditorGUILayout.LabelField(ev.eventId + (anchor ? "  [Anchor]" : ""), EditorStyles.boldLabel);
                foreach (var t in paths) EditorGUILayout.LabelField("  " + t.triggerId + "  " + t.barKey + " @ " + t.threshold + "  reach=" + t.reachCount + (t.enabled ? "" : " [disabled]"));
                if (paths.Count == 0) EditorGUILayout.LabelField("  (no trigger source)");
            }
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Reverse relationships", EditorStyles.boldLabel);
            foreach (var b in c.bars.Where(x => x.barType == NarrativeBarType.Relation)) EditorGUILayout.LabelField(b.barKey + " <- " + string.Join(", ", c.triggers.Where(t => t.barKey == b.barKey).Select(t => t.eventId).Distinct().ToArray()));
            EditorGUILayout.EndScrollView();
        }
        public static bool IsAnchor(NarrativeConfiguration c, NarrativeEventDefinition ev)
        {
            string owner = string.IsNullOrWhiteSpace(ev.conditionOwnerId) ? ev.eventId : ev.conditionOwnerId;
            if (c.conditions.Any(x => string.Equals(x.ownerType, "event", StringComparison.OrdinalIgnoreCase) && x.ownerId == owner)) return false;
            var paths = c.triggers.Where(t => t.eventId == ev.eventId).ToList();
            bool dates = c.eventDates.Any(x => x.eventId == ev.eventId);
            if (paths.Count == 0) return dates;
            return paths.All(t => t.enabled && t.direction != NarrativeTriggerDirection.Down && c.bars.Any(b => b.barKey == t.barKey && b.advanceMode == NarrativeAdvanceMode.Monotonic && b.autoStep > 0 && t.threshold > b.initValue));
        }
        public static string BuildReverseRows(NarrativeConfiguration c, bool triggerRows)
        {
            var rows = new List<string>();
            var barKeys = new HashSet<string>(c.bars.Select(b => b.barKey));
            var triggerIds = new HashSet<string>(c.triggers.Select(t => t.triggerId));
            foreach (var bar in c.bars.Where(b => b.barType == NarrativeBarType.Relation && !string.IsNullOrWhiteSpace(b.ownerId) && !string.IsNullOrWhiteSpace(b.targetId)))
            {
                string prefix = bar.ownerId + ".";
                if (string.IsNullOrEmpty(bar.barKey) || !bar.barKey.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string id = bar.barKey.Substring(prefix.Length) + "_reverse";
                string key = bar.targetId + "." + id;
                if (barKeys.Contains(key)) continue;
                if (!triggerRows)
                {
                    barKeys.Add(key);
                    rows.Add(Row(id, bar.targetId, bar.ownerId, "relation", bar.min, bar.max,
                        bar.direction == NarrativeDirection.HighIsGood ? "highIsGood" : "lowIsGood",
                        bar.advanceMode == NarrativeAdvanceMode.EventDriven ? "event_driven" : bar.advanceMode.ToString().ToLowerInvariant(),
                        bar.initValue, bar.nameText, bar.derivedFormula, string.Join(",", bar.derivedSources),
                        string.Join(",", bar.derivedWeights.Select(w => w.ToString(CultureInfo.InvariantCulture))), bar.autoStep));
                }
                else foreach (var trigger in c.triggers.Where(t => t.barKey == bar.barKey))
                {
                    string triggerId = trigger.triggerId + "_reverse";
                    if (!triggerIds.Add(triggerId)) continue;
                    rows.Add(Row(triggerId, key, trigger.threshold, trigger.direction.ToString().ToLowerInvariant(),
                        trigger.crossType.ToString().ToLowerInvariant(), trigger.onceType.ToString().ToLowerInvariant(),
                        trigger.reachCount, trigger.resetOnExit, trigger.eventId, trigger.priority, trigger.enabled));
                }
            }
            return string.Join("\n", rows);
        }
        static string Row(params object[] cells) => "\t" + string.Join("\t", cells.Select(cell =>
            Convert.ToString(cell, CultureInfo.InvariantCulture).Replace("\t", " ").Replace("\r", " ").Replace("\n", " ")));
    }
}
