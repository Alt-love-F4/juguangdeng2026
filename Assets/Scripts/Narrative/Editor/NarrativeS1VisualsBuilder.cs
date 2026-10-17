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
        public const string CafeSourcePath = "Assets/Art/Background/game/SCE_一楼大厅对话场景日景.png";

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
            Undo.RecordObject(registry, "Update S1 background artwork");
            SetSceneMarker(registry, NarrativeBackgroundKeys.CafeScene);
            SetBackground(registry, "oct07.cafe.day", CafeSourcePath);
            SetBackground(registry, "oct07.street.day", "Assets/Art/Background/game/SCE_圣徒街场景1.png");
            SetBackground(registry, "oct07.cafe.night", "Assets/Art/Background/game/SCE_一楼大厅场景夜晚.png");
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
            Debug.Log("[NCS] S1 resources ready: approved game cafe day/night and street artwork; shared presets and background catalog preserved.");
        }
        private static void SetSceneMarker(NarrativeStageVisuals registry, string key)
        {
            var entry = registry.Find(key) ?? new NarrativeStageVisuals.Entry { key = key };
            entry.texture = null;
            entry.tint = Color.white;
            entry.placeholder = false;
            entry.solidColor = false;
            entry.useSceneBackground = true;
            entry.fadeSeconds = .3f;
            registry.SetEntry(entry);
        }
        private static void SetBackground(NarrativeStageVisuals registry, string key, string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
            if (texture == null) throw new InvalidOperationException("Missing narrative source texture: " + path);
            var entry = registry.Find(key) ?? new NarrativeStageVisuals.Entry { key = key };
            entry.texture = texture;
            entry.tint = Color.white;
            entry.placeholder = false;
            entry.solidColor = false;
            registry.SetEntry(entry);
        }
    }
}
