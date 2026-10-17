using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Spotlight.Narrative.Editor
{
    public static class NarrativeIntegrationTools
    {
        public const string QueuePath = "Assets/ConfigTables/Data/Narrative/NcsIntegrationQueue.csv";
        public const string ReportPath = "Docs/NcsIntegrationReport.md";

        [MenuItem("Spotlight/Narrative/Integration Report")]
        public static void OpenWindow() => NarrativeIntegrationWindow.Open();

        [MenuItem("Spotlight/Narrative/Sync Integration Queue")]
        public static void SyncQueueFromMenu()
        {
            try
            {
                NarrativeConfiguration configuration = LubanNarrativeLoader.Load();
                NarrativeIntegrationReport report = NarrativeIntegrationReport.Build(configuration);
                string path = SyncQueue(configuration, report, QueuePath);
                AssetDatabase.Refresh();
                Debug.Log("[NCS] Integration queue synced: " + path + " (" + report.Events.Count + " events, "
                    + report.ErrorCount + " errors, " + report.WarningCount + " warnings).");
            }
            catch (Exception error)
            {
                Debug.LogError("[NCS] Integration queue sync failed:\n" + error);
            }
        }

        public static string ExportReport(NarrativeIntegrationReport report, string path = ReportPath)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, report.ToMarkdown(), new UTF8Encoding(false));
            return path.Replace('\\', '/');
        }

        public static string SyncQueue(NarrativeConfiguration configuration, NarrativeIntegrationReport report, string path = QueuePath)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (report == null) throw new ArgumentNullException(nameof(report));
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            Dictionary<string, QueueRow> previous = ReadExistingRows(fullPath);
            var lines = new List<string>
            {
                CsvLine("chapterId", "eventId", "entry", "source", "day", "timeSlot", "prerequisites", "presentation", "checks", "expectedResults", "status", "notes")
            };
            foreach (NarrativeIntegrationEventReport item in report.Events.OrderBy(value => value.EventId, StringComparer.Ordinal))
            {
                QueueRow old = previous.TryGetValue(item.EventId, out QueueRow existing) ? existing : null;
                var dates = configuration.eventDates.Where(value => value != null && value.eventId == item.EventId).ToList();
                string day = string.Join("|", dates.Select(value => value.day).Distinct().OrderBy(value => value));
                string timeSlot = string.Join("|", dates.Select(value => value.timeSlot.ToString()).Distinct().OrderBy(value => value, StringComparer.Ordinal));
                string generatedNotes = string.Join("; ", item.Errors.Concat(item.Warnings).Distinct(StringComparer.Ordinal));
                bool generatedRow = old == null || IsGeneratedQueueStatus(old.Status);
                string status = generatedRow ? QueueStatus(item) : old.Status;
                string notes = generatedRow ? generatedNotes : RefreshKnownGeneratedNotes(old.Notes, item);
                lines.Add(CsvLine(
                    ChapterId(item.EventId),
                    item.EventId,
                    item.Entry.ToString(),
                    string.Join(" | ", item.Sources),
                    day,
                    timeSlot,
                    string.Join(" | ", item.Prerequisites),
                    item.Presentation,
                    string.Join(" | ", item.Checks),
                    string.Join(" | ", item.ExpectedResults),
                    status,
                    notes));
            }
            File.WriteAllText(fullPath, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
            return path.Replace('\\', '/');
        }

        public static string BuildClipboardSummary(NarrativeIntegrationReport report)
        {
            if (report == null) return string.Empty;
            var text = new StringBuilder();
            text.AppendLine("NCS Integration: " + report.Events.Count + " events / " + report.ErrorCount + " errors / " + report.WarningCount + " warnings");
            foreach (NarrativeIntegrationEventReport item in report.Events)
                text.AppendLine(item.Status + "\t" + item.EventId + "\t" + item.Entry + "\t" + item.Presentation
                    + "\t" + string.Join(" | ", item.Sources));
            return text.ToString();
        }

        private static string RefreshKnownGeneratedNotes(string notes, NarrativeIntegrationEventReport item)
        {
            const string currentPrefix = "Invalid background registry entry for ";
            const string oldPrefix = "Background resource check failed for ";
            const string missingTextureMarker = ": Missing narrative background texture:";
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string error in item.Errors)
            {
                if (!error.StartsWith(currentPrefix, StringComparison.Ordinal)) continue;
                int marker = error.IndexOf(missingTextureMarker, currentPrefix.Length, StringComparison.Ordinal);
                if (marker < 0) continue;
                string key = error.Substring(currentPrefix.Length, marker - currentPrefix.Length);
                replacements[oldPrefix + key + missingTextureMarker + " " + key] = error;
            }

            var refreshed = new List<string>();
            var seenBackgroundErrors = new HashSet<string>(StringComparer.Ordinal);
            foreach (string part in (notes ?? string.Empty).Split(new[] { "; " }, StringSplitOptions.None))
            {
                string value = part;
                foreach (KeyValuePair<string, string> replacement in replacements)
                    value = value.Replace(replacement.Key, replacement.Value);
                bool isBackgroundError = value.StartsWith(currentPrefix, StringComparison.Ordinal);
                if (isBackgroundError && !seenBackgroundErrors.Add(value)) continue;
                refreshed.Add(value);
            }
            return string.Join("; ", refreshed);
        }

        private static string QueueStatus(NarrativeIntegrationEventReport item)
            => item.HasErrors ? "blocked" : item.Warnings.Count > 0 ? "review" : "config_pass";

        private static bool IsGeneratedQueueStatus(string status)
            => string.Equals(status, "blocked", StringComparison.Ordinal)
                || string.Equals(status, "review", StringComparison.Ordinal)
                || string.Equals(status, "config_pass", StringComparison.Ordinal);

        private static string ChapterId(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return string.Empty;
            string[] parts = eventId.Split('.');
            if (parts.Length > 2 && (parts[parts.Length - 1].StartsWith("s", StringComparison.OrdinalIgnoreCase)
                || parts[parts.Length - 1].StartsWith("p", StringComparison.OrdinalIgnoreCase)))
                return string.Join(".", parts.Take(parts.Length - 1));
            return parts[0];
        }

        private static string CsvLine(params string[] values)
            => string.Join(",", values.Select(EscapeCsv));

        private static string EscapeCsv(string value)
        {
            string text = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
            if (text.IndexOfAny(new[] { ',', '"' }) >= 0) return "\"" + text.Replace("\"", "\"\"") + "\"";
            return text;
        }

        private static Dictionary<string, QueueRow> ReadExistingRows(string path)
        {
            var result = new Dictionary<string, QueueRow>(StringComparer.Ordinal);
            if (!File.Exists(path)) return result;
            string[] lines = File.ReadAllLines(path);
            for (int i = 1; i < lines.Length; i++)
            {
                List<string> cells = ParseCsvLine(lines[i]);
                if (cells.Count < 12 || string.IsNullOrWhiteSpace(cells[1])) continue;
                result[cells[1]] = new QueueRow { Status = cells[10], Notes = cells[11] };
            }
            return result;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var value = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < (line ?? string.Empty).Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (ch == ',' && !quoted) { result.Add(value.ToString()); value.Clear(); }
                else value.Append(ch);
            }
            result.Add(value.ToString());
            return result;
        }

        private sealed class QueueRow
        {
            public string Status;
            public string Notes;
        }
    }

    public sealed class NarrativeIntegrationWindow : EditorWindow
    {
        private string filter = string.Empty;
        private string selectedEventId;
        private NarrativeConfiguration configuration;
        private NarrativeIntegrationReport report;
        private Vector2 scroll;
        private Vector2 detailScroll;
        private bool showOnlyProblems;

        public static void Open()
        {
            var window = GetWindow<NarrativeIntegrationWindow>("NCS Integration");
            window.minSize = new Vector2(780f, 460f);
            window.Reload();
            window.Show();
        }

        private void OnEnable() => Reload();

        private void Reload()
        {
            try
            {
                configuration = LubanNarrativeLoader.Load();
                report = NarrativeIntegrationReport.Build(configuration, filter);
            }
            catch (Exception error)
            {
                configuration = null;
                report = null;
                Debug.LogError("[NCS] Integration report failed:\n" + error);
            }
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Filter", GUILayout.Width(42f));
            string nextFilter = EditorGUILayout.TextField(filter, EditorStyles.toolbarTextField);
            if (!string.Equals(nextFilter, filter, StringComparison.Ordinal)) { filter = nextFilter; Reload(); }
            showOnlyProblems = GUILayout.Toggle(showOnlyProblems, "Problems", EditorStyles.toolbarButton, GUILayout.Width(72f));
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(64f))) Reload();
            EditorGUILayout.EndHorizontal();

            if (report == null)
            {
                EditorGUILayout.HelpBox("Unable to load Luban configuration. See Console for the exception.", MessageType.Error);
                return;
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Events " + report.Events.Count + "   Errors " + report.ErrorCount + "   Warnings " + report.WarningCount, EditorStyles.boldLabel);
            if (GUILayout.Button("Export Markdown", GUILayout.Width(120f))) Export();
            if (GUILayout.Button("Sync Queue", GUILayout.Width(90f))) SyncQueue();
            if (GUILayout.Button("Copy Summary", GUILayout.Width(100f))) EditorGUIUtility.systemCopyBuffer = NarrativeIntegrationTools.BuildClipboardSummary(report);
            EditorGUILayout.EndHorizontal();
            if (report.GlobalErrors.Count > 0) EditorGUILayout.HelpBox(string.Join("\n", report.GlobalErrors.Take(8)), MessageType.Error);
            if (report.GlobalWarnings.Count > 0) EditorGUILayout.HelpBox(string.Join("\n", report.GlobalWarnings.Take(8)), MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Width(position.width * .47f));
            foreach (NarrativeIntegrationEventReport item in report.Events)
            {
                if (showOnlyProblems && !item.HasErrors && item.Warnings.Count == 0) continue;
                bool selected = string.Equals(selectedEventId, item.EventId, StringComparison.Ordinal);
                string label = item.EventId + "   [" + item.Status + "]";
                if (GUILayout.Toggle(selected, label, "Button")) selectedEventId = item.EventId;
            }
            EditorGUILayout.EndScrollView();
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            NarrativeIntegrationEventReport selectedReport = report.Find(selectedEventId);
            if (selectedReport == null)
            {
                EditorGUILayout.HelpBox("Select an event to inspect its formal route and expected results.", MessageType.Info);
            }
            else DrawDetails(selectedReport);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDetails(NarrativeIntegrationEventReport item)
        {
            EditorGUILayout.LabelField(item.EventId, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Status", item.Status);
            EditorGUILayout.LabelField("Entry", item.Entry.ToString());
            EditorGUILayout.LabelField("Presentation", item.Presentation);
            EditorGUILayout.LabelField("Pages / lines / choices", item.PageCount + " / " + item.LineCount + " / " + item.ChoiceCount);
            EditorGUILayout.LabelField("Letter steps", item.LetterStepCount.ToString());
            EditorGUILayout.LabelField("Reading pages / lines", item.ReadingPageCount + " / " + item.ReadingLineCount);
            DrawList("Sources", item.Sources);
            DrawList("Prerequisites", item.Prerequisites);
            DrawList("Conditions", item.Conditions);
            DrawList("Checks", item.Checks);
            DrawList("Expected results", item.ExpectedResults);
            DrawList("Errors", item.Errors);
            DrawList("Warnings", item.Warnings);
        }

        private static void DrawList(string title, List<string> values)
        {
            if (values == null || values.Count == 0) return;
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (string value in values) EditorGUILayout.LabelField("• " + value, EditorStyles.wordWrappedLabel);
        }

        private void Export()
        {
            string path = NarrativeIntegrationTools.ExportReport(report);
            AssetDatabase.Refresh();
            Debug.Log("[NCS] Integration report exported: " + path);
        }

        private void SyncQueue()
        {
            string path = NarrativeIntegrationTools.SyncQueue(configuration, report);
            AssetDatabase.Refresh();
            Debug.Log("[NCS] Integration queue synced: " + path);
            Reload();
        }
    }
}
