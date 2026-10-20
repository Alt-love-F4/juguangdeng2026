using System;
using System.IO;
using System.Linq;
using Spotlight.FreeCafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>Incremental art update only; no layout rebuild, world creation, stock or narrative writes.</summary>
    public static class FreeCafeLatestCounterArtBuilder
    {
        private const string Root = FreeCafeCounterArtBuilder.Root;
        [MenuItem("Tools/Free Cafe/Apply Latest Counter Exports (2026-10-20)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Exit Play Mode and wait for compilation.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != FreeCafeProductionSceneBuilder.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open and save the existing CoffeeShop scene first.");
            var runtime = UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            if (runtime == null || runtime.artLibrary == null) throw new InvalidOperationException("Missing cafe art library.");
            string dir = Root + "/Entities/source.honey/";
            var closed = Import(dir + "closed.png");
            var opened = Import(dir + "open.png");
            if (closed.rect.size != opened.rect.size) throw new InvalidOperationException("Jar canvases must match.");
            var entry = runtime.artLibrary.Find("source.honey");
            if (entry == null) throw new InvalidOperationException("Missing existing source.honey entry.");
            string prefabPath = FreeCafeEntityPrefabBuilder.PathFor("source.honey");
            string backup = "output/counter-art-20261020/bind-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            File.Copy(scene.path, backup + "/CoffeeShop.unity");
            File.Copy(AssetDatabase.GetAssetPath(runtime.artLibrary), backup + "/ArtLibrary.asset");
            File.Copy(prefabPath, backup + "/FreeEntity_source_honey.prefab");
            entry.variants = new[] { new ArtVariant { state = "default", sprite = closed },
                new ArtVariant { state = "closed", sprite = closed }, new ArtVariant { state = "open", sprite = opened } };
            EditorUtility.SetDirty(runtime.artLibrary);
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Configure(root.GetComponent<FreeEntityView>(), closed, opened);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            // Scene-authored sprite overrides stay in place, except this jar's closed initial state.
            foreach (var view in FreeCafeSceneLayout.FindAuthoredEntities(runtime))
            {
                if (view.PrefabSpecId != "source.honey") continue;
                Configure(view, closed, opened);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view.body);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view.GetComponent<FreeCafeSourceJarVisual>());
            }
            var counter = runtime.areaRoots[(int)AreaId.Counter];
            var backdrop = counter.Find("SuppliedCounterBackdrop");
            if (backdrop == null || backdrop.GetComponent<SpriteRenderer>() == null)
                throw new InvalidOperationException("Existing supplied counter backdrop missing; do not create a second layout.");
            var renderer = backdrop.GetComponent<SpriteRenderer>();
            renderer.sprite = Import(Root + "/Environment/counter.png");
            renderer.enabled = true;
            EditorUtility.SetDirty(renderer);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Latest Counter Art] New sprites, both cup silhouettes, real open/closed jar exports and PSD viewport installed. Layout and gameplay unchanged. Backup: " + backup);
        }
        private static void Configure(FreeEntityView view, Sprite closed, Sprite opened)
        {
            var visual = view.GetComponent<FreeCafeSourceJarVisual>();
            if (visual == null) visual = view.gameObject.AddComponent<FreeCafeSourceJarVisual>();
            visual.closedSprite = closed; visual.openSprite = opened;
            visual.Present(false);
            EditorUtility.SetDirty(view.body); EditorUtility.SetDirty(visual);
        }
        private static Sprite Import(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
