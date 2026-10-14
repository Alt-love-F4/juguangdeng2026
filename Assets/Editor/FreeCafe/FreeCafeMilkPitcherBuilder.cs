using System;
using System.Collections.Generic;
using Spotlight.FreeCafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spotlight.FreeCafe.Editor
{
    public static class FreeCafeMilkPitcherBuilder
    {
        private const string SpritePath = "Assets/Art/FreeCafe/Entities/container.milk-pitcher/sprite.png";

        [MenuItem("Tools/Free Cafe/Install Milk Pitcher")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("先退出 Play Mode 并等待编译完成");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.isDirty) throw new InvalidOperationException("请先保存场景；安装不会覆盖未保存的修改");
            if (scene.path != FreeCafeProductionSceneBuilder.ScenePath)
                scene = EditorSceneManager.OpenScene(FreeCafeProductionSceneBuilder.ScenePath);
            var runtime = UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            if (runtime == null) throw new InvalidOperationException("CoffeeShop 缺少 FreeCafeRuntime");
            AssetDatabase.ImportAsset(SpritePath);
            var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("缺少拉花杯图片");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 512;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            var art = runtime.artLibrary;
            var entry = art.Find("container.milk-pitcher");
            if (entry == null)
            {
                entry = new CafeArtEntry { brief = new ArtBrief { id = "container.milk-pitcher", label = "拉花杯", category = "entity",
                    pivot = "center (0.5,0.5)", prompt = "Warm hand-drawn stainless steel milk frothing pitcher, left spout, right handle, isolated sprite.",
                    negativePrompt = "No text, no logo, no background.",
                    width = 1024, height = 1024, worldWidth = 1.2f, worldHeight = 1.4f, transparent = true,
                    outputFolder = "Assets/Art/FreeCafe/Entities/container.milk-pitcher", states = new[] { "empty", "milk", "full" } } };
                var entries = new List<CafeArtEntry>(art.entries); entries.Add(entry); art.entries = entries.ToArray();
            }
            entry.whitebox = sprite;
            entry.variants = new[] { new ArtVariant { state = "default", sprite = sprite } };
            EditorUtility.SetDirty(art);
            var catalog = JsonUtility.FromJson<CafeCatalog>(runtime.catalogAsset.text);
            runtime.prefabLibrary = FreeCafeEntityPrefabBuilder.EnsureCatalog(catalog, art, runtime.entityPrefab,
                new HashSet<string> { "container.milk-pitcher" });
            foreach (var view in FreeCafeSceneLayout.FindAuthoredEntities(runtime))
                if (view.PrefabSpecId == "tool.latte-art") Undo.DestroyObjectImmediate(view.gameObject);
            FreeCafeEntityPrefabBuilder.RebuildAuthoredLayout(runtime, catalog);
            var hud = runtime.GetComponent<FreeCafeProductionHud>();
            if (hud != null && hud.miniGameButton != null) hud.miniGameButton.gameObject.SetActive(false);
            EditorUtility.SetDirty(runtime);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[FreeCafe] 已安装拉花杯；旧拉花入口已移入 GM 诊断面板");
        }
    }
}
