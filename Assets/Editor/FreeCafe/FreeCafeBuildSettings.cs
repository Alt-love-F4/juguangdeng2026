using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spotlight.FreeCafe.Editor
{
    [InitializeOnLoad]
    internal static class FreeCafeBuildSettings
    {
        private const string EntryScene = "Assets/Scenes/SampleScene.unity";
        private const string GameplayScene = "Assets/Scenes/CoffeeShop.unity";

        static FreeCafeBuildSettings()
        {
            EditorApplication.delayCall += EnsureScenes;
        }

        private static void EnsureScenes()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            AddOrEnable(scenes, EntryScene);
            AddOrEnable(scenes, GameplayScene);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void AddOrEnable(System.Collections.Generic.List<EditorBuildSettingsScene> scenes, string path)
        {
            var scene = scenes.FirstOrDefault(item => item.path == path);
            if (scene != null)
            {
                scene.enabled = true;
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                scenes.Add(new EditorBuildSettingsScene(path, true));
        }
    }
}
