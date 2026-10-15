using System;
using System.IO;
using Spotlight.GameFlow;
using UnityEditor;
using UnityEngine;

namespace Spotlight.Narrative.Editor
{
    /// <summary>Builds resource aliases/presets only. Never modifies source art, scenes or narrative tables.</summary>
    public static class NarrativeS1VisualsBuilder
    {
        public const string RegistryPath = "Assets/Resources/Dialogue/NarrativeStageVisuals.asset";
        public const string EffectsPath = "Assets/Resources/Dialogue/PresentationEffectLibrary.asset";
        public const string CafeSourcePath = "Assets/Art/Background/架空咖啡馆背景_01.png";

        [MenuItem("Spotlight/Narrative/Build S1 Visual Resources")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Build persistent S1 resources in Edit Mode.");
            var cafe = AssetDatabase.LoadAssetAtPath<Texture>(CafeSourcePath);
            if (cafe == null) throw new InvalidOperationException("Missing S1 source texture: " + CafeSourcePath);
            Directory.CreateDirectory("Assets/Resources/Dialogue");
            var registry = AssetDatabase.LoadAssetAtPath<NarrativeStageVisuals>(RegistryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<NarrativeStageVisuals>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
            }
            Undo.RecordObject(registry, "Add missing S1 background aliases");
            if (registry.Find("oct07.cafe.day") == null)
                registry.SetEntry(new NarrativeStageVisuals.Entry { key = "oct07.cafe.day", texture = cafe, fadeSeconds = .3f });
            // Asset search found no matching Saint Street artwork. Do not substitute another interior.
            if (registry.Find("oct07.street.day") == null)
                registry.SetEntry(new NarrativeStageVisuals.Entry { key = "oct07.street.day", placeholder = true,
                    placeholderColor = new Color(.23f, .25f, .28f, 1f), fadeSeconds = .3f });
            if (registry.Find("oct07.cafe.night") == null)
                registry.SetEntry(new NarrativeStageVisuals.Entry { key = "oct07.cafe.night", texture = cafe,
                    tint = new Color(.42f, .48f, .62f, 1f), fadeSeconds = .3f });
            EditorUtility.SetDirty(registry);
            const string passivePath = "Assets/Resources/Dialogue/PassiveCheckPresentation.asset";
            var passive = AssetDatabase.LoadAssetAtPath<PassiveCheckPresentationConfig>(passivePath);
            if (passive == null)
            {
                passive = ScriptableObject.CreateInstance<PassiveCheckPresentationConfig>();
                AssetDatabase.CreateAsset(passive, passivePath);
            }
            var fixedWords = new System.Collections.Generic.List<PassiveCheckPresentationConfig.FixedWord>(passive.fixedWords ?? Array.Empty<PassiveCheckPresentationConfig.FixedWord>());
            if (!fixedWords.Exists(w => w != null && w.checkId == "oct07.s2.perception"))
            {
                Undo.RecordObject(passive, "Add S2 passive hint wording");
                fixedWords.Add(new PassiveCheckPresentationConfig.FixedWord { checkId = "oct07.s2.perception", word = "已察觉" });
                passive.fixedWords = fixedWords.ToArray();
                EditorUtility.SetDirty(passive);
            }
            var effects = AssetDatabase.LoadAssetAtPath<PresentationEffectLibrary>(EffectsPath);
            if (effects == null)
            {
                effects = ScriptableObject.CreateInstance<PresentationEffectLibrary>();
                AssetDatabase.CreateAsset(effects, EffectsPath);
            }
            Undo.RecordObject(effects, "Add missing shared narrative effect presets");
            effects.EnsureNarrativePresets();
            EditorUtility.SetDirty(effects);
            AssetDatabase.SaveAssets();
            Debug.Log("[NCS] S1 resources ready: original cafe day/night aliases; explicit street placeholder; shared nod/pan presets; S2 fixed passive hint. Existing keys/presets preserved.");
        }
    }
}
