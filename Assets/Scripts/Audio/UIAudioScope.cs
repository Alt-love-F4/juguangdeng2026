using UnityEngine;

namespace Spotlight.Audio
{
    /// <summary>Optional nested UI scope. For example: ui.cafe.recipe.ingredients.</summary>
    [DisallowMultipleComponent]
    public sealed class UIAudioScope : MonoBehaviour
    {
        [SerializeField] private string prefix = "ui.common";
        public string Prefix => prefix;

        // Component names avoid dependencies from Audio onto the UI assemblies.
        public static string DefaultPrefix(string componentName)
        {
            switch (componentName)
            {
                case "MainMenuPanel": return "ui.menu";
                case "SettingsPanel": return "ui.system.settings";
                case "SaveManagementPanel": case "SaveSlotView": return "ui.system.save";
                case "PauseMenuPanel": return "ui.system.pause";
                case "ConfirmationPanel": return "ui.system.confirmation";
                case "DetailsPanel": return "ui.details";
                case "MapTaskPanel": return "ui.map.tasks";
                case "MapActionDetailsView": return "ui.map.action";
                case "MapActionInventoryView": return "ui.map.inventory";
                case "MapSceneController": return "ui.map.location";
                case "FreeCafeProductionHud": return "ui.cafe.production";
                case "FreeCafeLatteArtBench": return "ui.cafe.latte";
                case "BusinessControlsPanel": return "ui.cafe.business";
                case "DialoguePanel": case "DialogueOptionView": return "ui.story.dialogue";
                case "ReadingPanel": return "ui.story.reading";
                case "FirstDayLetterPanel": return "ui.story.letter";
                case "NoticePanel": return "ui.story.notice";
                case "ResultPanel": return "ui.system.result";
                default: return null;
            }
        }

        public static string Resolve(AudioCatalog catalog, Transform target, string key)
        {
            // Empty keys intentionally silence buttons whose business event plays audio.
            if (catalog == null || string.IsNullOrEmpty(key) || !key.StartsWith("ui.", System.StringComparison.Ordinal) || key.IndexOf('.', 3) >= 0) return key;
            string action = key.Substring(3);
            for (Transform node = target; node != null; node = node.parent)
            {
                var scope = node.GetComponent<UIAudioScope>();
                if (scope != null && !string.IsNullOrWhiteSpace(scope.prefix))
                    return Find(catalog, scope.prefix, action, key);
                foreach (var component in node.GetComponents<MonoBehaviour>())
                {
                    if (component == null) continue;
                    string inferred = DefaultPrefix(component.GetType().Name);
                    if (inferred != null) return Find(catalog, inferred, action, key);
                }
            }
            return key;
        }

        private static string Find(AudioCatalog catalog, string prefix, string action, string fallback)
        {
            while (prefix.StartsWith("ui.", System.StringComparison.Ordinal))
            {
                string candidate = prefix + "." + action;
                // An explicitly disabled cue must not fall back to an audible parent.
                if (catalog.Find(candidate) != null) return candidate;
                int separator = prefix.LastIndexOf('.');
                prefix = prefix.Substring(0, separator);
            }
            return fallback;
        }
    }
}
