using System;
using System.Collections.Generic;
using System.IO;
using Spotlight.FreeCafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>Applies ONLY the supplied sliced PSD art; does not regenerate any missing art or UI.</summary>
    public static class FreeCafeCounterArtBuilder
    {
        public const string Root = "Assets/Art/Gameplay/咖啡馆/Sliced";
        [MenuItem("Tools/Free Cafe/Apply Supplied Counter PSD Art")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("先退出 Play Mode");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.isDirty) throw new InvalidOperationException("先保存当前场景，避免覆盖未保存修改");
            scene = EditorSceneManager.OpenScene(FreeCafeProductionSceneBuilder.ScenePath);
            var runtime = UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            if (runtime == null) throw new InvalidOperationException("缺少 FreeCafeRuntime");
            string backup = "output/counter-art-backup/" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup); File.Copy(scene.path, backup + "/CoffeeShop.unity");
            File.Copy(AssetDatabase.GetAssetPath(runtime.artLibrary), backup + "/ArtLibrary.asset");
            foreach (string path in Directory.GetFiles(FreeCafeEntityPrefabBuilder.PrefabDirectory, "*.prefab")) File.Copy(path, backup + "/" + Path.GetFileName(path));
            var catalog = JsonUtility.FromJson<CafeCatalog>(runtime.catalogAsset.text);
            var entries = new List<CafeArtEntry>(runtime.artLibrary.entries);
            var affected = new HashSet<string>();
            foreach (var dir in Directory.GetDirectories(Root + "/Entities"))
            {
                string id = Path.GetFileName(dir);
                var variants = new List<ArtVariant>();
                foreach (var file in Directory.GetFiles(dir, "*.png")) variants.Add(new ArtVariant { state = Path.GetFileNameWithoutExtension(file), sprite = Import(file) });
                Bind(entries, id, variants.ToArray()); affected.Add(id);
            }
            foreach (string size in new[] { "small", "large" })
            {
                string id = "source.cups." + size;
                Bind(entries, id, new[] { new ArtVariant { state = "default", sprite = Import(Root + "/Entities/cup." + size + "/default.png") } });
                affected.Add(id);
            }
            runtime.artLibrary.entries = entries.ToArray(); EditorUtility.SetDirty(runtime.artLibrary);
            runtime.prefabLibrary = FreeCafeEntityPrefabBuilder.EnsureCatalog(catalog, runtime.artLibrary, runtime.entityPrefab, affected);
            foreach (var spec in catalog.entities)
            {
                bool cup = spec.id.StartsWith("cup.") || spec.id.StartsWith("source.cups.");
                bool extractor = spec.id == "container.extractor" || spec.id == "container.extractor.filter";
                if (!cup && !extractor) continue;
                string path = FreeCafeEntityPrefabBuilder.PathFor(spec.id); var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view = root.GetComponent<FreeEntityView>();
                    if (cup)
                    {
                        var stack = root.GetComponent<FreeCafeCupStackVisual>(); if (stack == null) stack = root.AddComponent<FreeCafeCupStackVisual>();
                        stack.Present(spec, catalog, runtime.artLibrary, spec.initialStock, false);
                    }
                    if (extractor)
                    {
                        var visual = root.GetComponent<FreeCafeExtractorVisual>(); if (visual == null) visual = root.AddComponent<FreeCafeExtractorVisual>();
                        visual.staticArtwork = true;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            FreeCafeEntityPrefabBuilder.RebuildAuthoredLayout(runtime, catalog);
            foreach (var view in FreeCafeSceneLayout.FindAuthoredEntities(runtime))
            {
                var entry = Array.Find(catalog.layout, e => e.id == view.AuthoringEntityId);
                if (entry == null || entry.area != AreaId.Counter) continue;
                var spec = Array.Find(catalog.entities, e => e.id == entry.specId);
                // The new authoring layout is in area space, even when existing entities live in scaled subgroups.
                var parent = view.transform.parent;
                view.transform.SetParent(runtime.areaRoots[(int)entry.area], true);
                // Only supplied art is resized to PSD world pixels. Preserve authored sizes of missing-art tools.
                if (affected.Contains(spec.artId))
                { view.transform.localScale = Vector3.one; view.transform.localRotation = Quaternion.identity; }
                view.ConfigureAuthoring(spec, runtime.artLibrary, entry.id, entry.x, entry.y);
                view.transform.SetParent(parent, true);
                var stack = view.GetComponent<FreeCafeCupStackVisual>();
                if (stack != null) stack.Present(spec, catalog, runtime.artLibrary, spec.initialStock, false);
                Record(view); Record(view.transform); Record(view.body); Record(view.body.transform); Record(view.hitbox); Record(view.label.transform);
            }
            var counter = runtime.areaRoots[(int)AreaId.Counter];
            // Replace the work area's existing backdrop only. No extra scene/canvas/input system.
            var backdrop = counter.Find("SuppliedCounterBackdrop");
            if (backdrop == null) { backdrop = new GameObject("SuppliedCounterBackdrop").transform; backdrop.SetParent(counter, false); }
            backdrop.localPosition = Vector3.zero;
            var renderer = backdrop.GetComponent<SpriteRenderer>(); if (renderer == null) renderer = backdrop.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = Import(Root + "/Environment/counter.png"); renderer.sortingOrder = -100; renderer.color = Color.white;
            float fit = 22.044444f / renderer.sprite.bounds.size.x; backdrop.localScale = new Vector3(fit, fit, 1);
            foreach (var child in counter.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (child == renderer || child.GetComponentInParent<FreeEntityView>() != null) continue;
                if (child.name.StartsWith("Background") || child.name == "CounterTop" || child.name == "WorkSurface") child.enabled = false;
            }
            foreach (var text in counter.GetComponentsInChildren<TMPro.TMP_Text>(true))
                if (text.name == "AreaArtNote" || text.name == "ProductionNote") text.gameObject.SetActive(false);
            EditorUtility.SetDirty(runtime); EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("[Counter PSD] 素材/上下壶/杯堆/背景已接入。缺图保留。备份：" + backup);
        }
        [MenuItem("Tools/Free Cafe/Preview Supplied Counter (Play Mode)")]
        public static void Preview()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("先进入 Play Mode");
            var runtime = UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            runtime.areaManager.panSmoothTime = 0; runtime.World.EnterProduction();
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
        }
        private static void Record(UnityEngine.Object obj) { EditorUtility.SetDirty(obj); PrefabUtility.RecordPrefabInstancePropertyModifications(obj); }
        private static void Bind(List<CafeArtEntry> entries, string id, ArtVariant[] variants)
        {
            var entry = entries.Find(e => e.brief != null && e.brief.id == id);
            if (entry == null) { entry = new CafeArtEntry { brief = new ArtBrief { id = id, label = id } }; entries.Add(entry); }
            entry.variants = variants;
        }
        private static Sprite Import(string path)
        {
            path = path.Replace('\\', '/'); AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("无法导入 " + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100; importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
