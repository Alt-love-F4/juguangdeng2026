using System;
using System.IO;
using Spotlight.FreeCafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>Incrementally binds parts split from CoffeeShop's supplied grinder sprite.</summary>
    public static class FreeCafeGrinderArtBuilder
    {
        private const string Root = "Assets/Art/Gameplay/\u5496\u5561\u9986/Sliced/Entities/";
        private const string Parts = Root + "machine.grinder/Animated/";
        [MenuItem("Tools/Free Cafe/Apply Grinder Animation And Pickup Art")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Exit Play Mode and wait for compilation before binding artwork.");
            var runtime = UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            if (runtime == null || SceneManager.GetActiveScene().path != "Assets/Scenes/CoffeeShop.unity")
                throw new InvalidOperationException("Open the existing CoffeeShop scene first.");
            var library = runtime.artLibrary;
            if (library == null) throw new InvalidOperationException("The scene has no art library.");
            string backup = "output/grinder-polish/backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            File.Copy(AssetDatabase.GetAssetPath(library), backup + "/ArtLibrary.asset");
            foreach (string id in new[] { "machine.grinder", "beans", "grounds" })
            {
                string path = FreeCafeEntityPrefabBuilder.PathFor(id);
                File.Copy(path, backup + "/" + Path.GetFileName(path));
            }
            var body = Import(Parts + "body.png", new Vector2(.5f, .5f));
            var arm = Import(Parts + "crank-arm.png", new Vector2(43f / 318, 14f / 56));
            var knob = Import(Parts + "crank-knob.png", new Vector2(60f / 118, 2f / 132));
            var closed = Import(Parts + "drawer-closed.png", new Vector2(.5f, .5f));
            var filled = Import(Parts + "drawer-filled.png", new Vector2(.5f, .5f));
            var beans = Import(Root + "beans/handful.png", new Vector2(.5f, .5f));
            Bind(library, "machine.grinder", body); Bind(library, "beans", beans);
            Bind(library, "grounds", filled); Bind(library, "equipment.grinder.drawer", filled);
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library);
            var catalog = JsonUtility.FromJson<CafeCatalog>(runtime.catalogAsset.text);
            foreach (string id in new[] { "machine.grinder", "beans", "grounds" })
            {
                string path = FreeCafeEntityPrefabBuilder.PathFor(id);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view = root.GetComponent<FreeEntityView>();
                    var spec = Array.Find(catalog.entities, e => e.id == id);
                    view.body.sprite = library.Resolve(spec.artId);
                    var bounds = view.body.sprite.bounds.size;
                    float fit = Mathf.Min(spec.width / bounds.x, spec.height / bounds.y);
                    view.body.transform.localScale = new Vector3(fit, fit, 1);
                    view.hitbox.size = new Vector2(spec.width, spec.height);
                    if (id == "machine.grinder")
                    {
                        var visual = root.GetComponent<FreeCafeGrinderVisual>();
                        if (visual == null) visual = root.AddComponent<FreeCafeGrinderVisual>();
                        visual.crankArm = Part(view, "CrankArm", arm, visual.crankPivot, 2);
                        visual.crankKnob = Part(view, "CrankGrip", knob, visual.crankPivot + (Vector3)visual.crankReach, 3);
                        visual.closedDrawer = Part(view, "ClosedDrawer", closed, visual.drawerOrigin, 1);
                        visual.filledDrawer = Part(view, "FilledDrawer", filled, visual.drawerOrigin, 1);
                        visual.filledDrawer.transform.localScale = Vector3.one * (2.52f / filled.bounds.size.x);
                        visual.filledDrawer.enabled = false;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log("[Grinder Art] Bound scene artwork, animated parts and matching pickups. Backup: " + backup);
        }

        private static SpriteRenderer Part(FreeEntityView view, string name, Sprite sprite, Vector3 position, int order)
        {
            var part = view.body.transform.Find(name);
            if (part == null) { part = new GameObject(name).transform; part.SetParent(view.body.transform, false); }
            var renderer = part.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = part.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sortingLayerID = view.body.sortingLayerID;
            renderer.sortingOrder = view.body.sortingOrder + order;
            part.localPosition = position; part.localRotation = Quaternion.identity; part.localScale = Vector3.one;
            return renderer;
        }

        private static Sprite Import(string path, Vector2 pivot)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing image: " + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Bind(CafeArtLibrary library, string id, Sprite sprite)
        {
            var entry = library.Find(id);
            if (entry == null) throw new InvalidOperationException("Scene art library is missing " + id);
            entry.variants = new[] { new ArtVariant { state = "default", sprite = sprite } };
        }
    }
}
