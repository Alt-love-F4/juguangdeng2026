using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Spotlight.Narrative.Editor
{
    public static class NarrativeLubanTools
    {
        static System.Diagnostics.Process generation;
        [MenuItem("Spotlight/Narrative/Generate Luban")]
        public static void Generate()
        {
            if (generation != null) { Debug.LogWarning("[NCS] Luban generation is already running."); return; }
            string script = Path.GetFullPath("Assets/ConfigTables/GenerateAll.bat");
            if (!File.Exists(script)) { Debug.LogError("[NCS] Missing Luban generator: " + script); return; }
            generation = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = script,
                WorkingDirectory = Path.GetDirectoryName(script),
                UseShellExecute = true
            });
            EditorApplication.update += CheckGeneration;
            Debug.Log("[NCS] GenerateAll.bat started; generation writes cs-json, not ScriptableObjects.");
        }
        static void CheckGeneration()
        {
            if (generation == null || !generation.HasExited) return;
            int exitCode = generation.ExitCode;
            generation.Dispose(); generation = null;
            EditorApplication.update -= CheckGeneration;
            if (exitCode != 0) { Debug.LogError("[NCS] Luban generation failed with exit code " + exitCode); return; }
            const string dataDirectory = "Assets/Settings/Resources/Luban";
            if (!Directory.Exists(dataDirectory)) { Debug.LogError("[NCS] Missing generated data directory: " + dataDirectory); return; }
            foreach (string path in Directory.GetFiles(dataDirectory, "*.json"))
                AssetDatabase.ImportAsset(path.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            Validate();
        }

        [MenuItem("Spotlight/Narrative/Validate Luban")]
        public static void Validate()
        {
            try
            {
                var config = LubanNarrativeLoader.Load();
                var errors = NarrativeConfigurationValidator.Validate(config);
                ExportIntegrationArtifacts(config);
                if (errors.Count > 0) { Debug.LogError("[NCS] Luban validation failed:\n" + string.Join("\n", errors)); return; }
                foreach (string warning in NarrativeConfigurationValidator.Warnings(config)) Debug.LogWarning("[NCS] " + warning);
                int count = config.events.Count;
                int anchors = config.events.Count(ev => NarrativeEventByBarWindow.IsAnchor(config, ev));
                float ratio = count == 0 ? 0 : (float)anchors / count;
                Debug.Log($"[NCS] Luban validation passed. Anchors: {anchors}/{count} ({ratio:P1}). No asset created.");
                if (anchors == 0) Debug.LogWarning("[NCS] No guaranteed anchor events: review narrative progression.");
                else if (ratio > 0.4f) Debug.LogWarning("[NCS] Anchors exceed 40%: confirm a mostly linear narrative is intended.");
            }
            catch (Exception ex) { Debug.LogError("[NCS] Luban load/validation failed:\n" + ex); }
        }

        private static void ExportIntegrationArtifacts(NarrativeConfiguration configuration = null)
        {
            try
            {
                var config = configuration ?? LubanNarrativeLoader.Load();
                var report = NarrativeIntegrationReport.Build(config);
                string reportPath = NarrativeIntegrationTools.ExportReport(report);
                string queuePath = NarrativeIntegrationTools.SyncQueue(config, report);
                AssetDatabase.Refresh();
                Debug.Log($"[NCS] Integration artifacts updated: {queuePath}, {reportPath}. Events={report.Events.Count}, errors={report.ErrorCount}, warnings={report.WarningCount}.");
            }
            catch (Exception error)
            {
                Debug.LogError("[NCS] Integration artifact export failed:\n" + error);
            }
        }
    }
}
