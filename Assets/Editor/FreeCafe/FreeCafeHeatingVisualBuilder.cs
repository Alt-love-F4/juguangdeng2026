using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spotlight.FreeCafe.Editor
{
    public static class FreeCafeHeatingVisualBuilder
    {
        [MenuItem("Tools/Free Cafe/Apply Heating And Boiling Animation")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Exit Play Mode and wait for compilation.");
            var runtime = UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            if (runtime == null || runtime.prefabLibrary == null || runtime.gameObject.scene.path != "Assets/Scenes/CoffeeShop.unity")
                throw new InvalidOperationException("Open the existing CoffeeShop scene.");
            string backup = "output/heating-animation/backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            var scene = runtime.gameObject.scene;
            File.Copy(scene.path, Path.Combine(backup, "CoffeeShop.unity"));
            var shader = Shader.Find("Spotlight/FreeCafe/Thermal Effects");
            if (shader == null) throw new InvalidOperationException("Thermal shader is missing.");
            const string materialPath = "Assets/Resources/FreeCafe/ThermalEffects.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            else material.shader = shader;
            EditorUtility.SetDirty(material);
            string[] ids = { "machine.stove", "machine.heater", "machine.brewer", "cup.small", "cup.large", "container.extractor", "container.extractor.filter", "container.milk-pitcher" };
            foreach (string id in ids)
            {
                var prefab = runtime.prefabLibrary.Require(id);
                string path = AssetDatabase.GetAssetPath(prefab);
                File.Copy(path, Path.Combine(backup, Path.GetFileName(path)));
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Configure(root.GetComponent<FreeEntityView>());
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var view in UnityEngine.Object.FindObjectsByType<FreeEntityView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (view.gameObject.scene != scene || Array.IndexOf(ids, view.PrefabSpecId) < 0) continue;
                Configure(view);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[FreeCafe Heating] Heat sources and boiling visuals configured separately. Backup: " + backup);
        }
        private static void Configure(FreeEntityView view)
        {
            string id = view.PrefabSpecId;
            if (id == "machine.stove" || id == "machine.heater")
            {
                var heat = view.GetComponent<FreeCafeHeatingVisual>();
                if (heat == null) heat = view.gameObject.AddComponent<FreeCafeHeatingVisual>();
                bool stove = id == "machine.stove";
                heat.burnerUV = stove ? new Vector2(.49f, .735f) : new Vector2(.55f, .105f);
                heat.flameSizeUV = stove ? new Vector2(.32f, .115f) : new Vector2(.20f, .105f);
                heat.glowSizeUV = stove ? new Vector2(.39f, .10f) : new Vector2(.27f, .075f);
                heat.flameCount = stove ? 7 : 4;
                heat.sortingOffset = stove ? 1 : 2;
                EditorUtility.SetDirty(heat);
                if (PrefabUtility.IsPartOfPrefabInstance(heat)) PrefabUtility.RecordPrefabInstancePropertyModifications(heat);
            }
            if (id == "machine.stove") return;
            var boil = view.GetComponent<FreeCafeBoilingVisual>();
            if (boil == null) boil = view.gameObject.AddComponent<FreeCafeBoilingVisual>();
            switch (id)
            {
                case "machine.heater": boil.steamOriginUV = new Vector2(.075f, .66f); boil.steamSizeUV = new Vector2(.27f, .23f); break;
                case "machine.brewer": boil.steamOriginUV = new Vector2(.055f, .73f); boil.steamSizeUV = new Vector2(.24f, .29f); break;
                case "cup.small": boil.steamOriginUV = new Vector2(.43f, .79f); boil.steamSizeUV = new Vector2(.42f, .40f); break;
                case "cup.large": boil.steamOriginUV = new Vector2(.40f, .80f); boil.steamSizeUV = new Vector2(.40f, .32f); break;
                case "container.extractor": boil.steamOriginUV = new Vector2(.40f, .75f); boil.steamSizeUV = new Vector2(.23f, .32f); break;
                case "container.extractor.filter": boil.steamOriginUV = new Vector2(.50f, .84f); boil.steamSizeUV = new Vector2(.46f, .22f); break;
                case "container.milk-pitcher": boil.steamOriginUV = new Vector2(.42f, .76f); boil.steamSizeUV = new Vector2(.34f, .28f); break;
            }
            boil.vibrationHeight = id == "machine.heater" ? .002f : .0012f;
            EditorUtility.SetDirty(boil);
            if (PrefabUtility.IsPartOfPrefabInstance(boil)) PrefabUtility.RecordPrefabInstancePropertyModifications(boil);
        }
    }
}
