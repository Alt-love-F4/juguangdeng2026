#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Spotlight.FreeCafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>Apply the serving plate and bell to the authored CoffeeShop workstation.</summary>
    public static class FreeCafeDeliveryDesignBuilder
    {
        private const string ArtFolder = "Assets/Art/Gameplay/咖啡馆/Delivery/";
        private const string PrefabPath = "Assets/Prefabs/FreeCafe/FreeEntity_delivery.prefab";
        [MenuItem("Tools/Free Cafe/Apply Serving Plate And Bell")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/CoffeeShop.unity" || scene.isDirty)
                throw new InvalidOperationException("请在已保存的 CoffeeShop 编辑场景执行");
            var runtime = UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            var service = Array.Find(runtime.areaRoots[(int)AreaId.Counter].GetComponentsInChildren<FreeEntityView>(true), v => v.AuthoringEntityId == "service");
            if (service == null) throw new InvalidOperationException("缺少场景 service 实体");
            Directory.CreateDirectory("output/delivery-design");
            Backup(scene.path, "output/delivery-design/CoffeeShop.before.unity");
            Backup(PrefabPath, "output/delivery-design/FreeEntity_delivery.before.prefab");
            var plate = Import(ArtFolder + "serving-plate.png");
            var bell = Import(ArtFolder + "service-bell.png");
            var library = AssetDatabase.LoadAssetAtPath<CafeArtLibrary>("Assets/Resources/FreeCafe/ArtLibrary.asset");
            var entry = library.Find("delivery.plate");
            if (entry == null)
            {
                entry = new CafeArtEntry { brief = new ArtBrief { id = "delivery.plate", label = "出品盘", category = "entity",
                    outputFolder = ArtFolder, width = plate.texture.width, height = plate.texture.height,
                    transparent = true, worldWidth = 4.4f, worldHeight = 2.1f, states = new[] { "default" } } };
                var entries = new List<CafeArtEntry>(library.entries); entries.Add(entry); library.entries = entries.ToArray();
            }
            entry.whitebox = plate; entry.variants = new[] { new ArtVariant { state = "default", sprite = plate } };
            EditorUtility.SetDirty(library);
            const string catalogPath = "Assets/Resources/FreeCafe/catalog.json";
            var catalog = File.ReadAllText(catalogPath);
            catalog = SetCatalogField(catalog, "delivery", "artId", "\"delivery.plate\"");
            catalog = SetCatalogField(catalog, "delivery", "label", "\"出品盘\"");
            catalog = SetCatalogField(catalog, "delivery", "width", "4.4");
            catalog = SetCatalogField(catalog, "delivery", "height", "2.1");
            catalog = SetCatalogField(catalog, "service", "x", "0");
            catalog = SetCatalogField(catalog, "service", "y", "-1.3");
            File.WriteAllText(catalogPath, catalog);
            AssetDatabase.ImportAsset(catalogPath);

            // Replace the old scene-added bell before the prefab introduces its own.
            foreach (var old in service.GetComponentsInChildren<FreeCafeDeliveryBell>(true))
                if (PrefabUtility.GetCorrespondingObjectFromSource(old) == null)
                    Undo.DestroyObjectImmediate(old.gameObject);
            var oldZone = service.GetComponent<FreeCafeDeliveryZone>();
            if (oldZone != null && PrefabUtility.GetCorrespondingObjectFromSource(oldZone) == null)
                Undo.DestroyObjectImmediate(oldZone);
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Configure(contents.GetComponent<FreeEntityView>(), plate, bell, null);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            Configure(service, plate, bell, runtime);
            Undo.RecordObject(service.transform, "Place serving plate on board");
            service.transform.position = runtime.areaRoots[(int)AreaId.Counter].TransformPoint(new Vector3(0, -1.3f, 0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(service.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        private static string SetCatalogField(string json, string id, string field, string value)
        {
            // Preserve unrelated fields and the source catalog's formatting.
            string pattern = "(\"id\"\\s*:\\s*\"" + Regex.Escape(id) + "\"[^{}]*?\"" + field + "\"\\s*:\\s*)(?:\"[^\"]*\"|-?[0-9.]+)";
            var matches = Regex.Matches(json, pattern);
            if (matches.Count != 1) throw new InvalidOperationException("Catalog field is ambiguous: " + id + "." + field);
            return Regex.Replace(json, pattern, match => match.Groups[1].Value + value);
        }
        private static void Backup(string source, string destination)
        {
            if (!File.Exists(destination)) File.Copy(source, destination);
        }
        private static Sprite Import(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static void Configure(FreeEntityView service, Sprite plate, Sprite bell, FreeCafeRuntime runtime)
        {
            var frame = service.transform.Find("DeliveryFrame");
            if (frame != null) { frame.gameObject.SetActive(false); Record(frame.gameObject); }
            var zone = service.GetComponent<FreeCafeDeliveryZone>();
            if (zone == null) zone = service.gameObject.AddComponent<FreeCafeDeliveryZone>();
            var so = new SerializedObject(zone);
            so.FindProperty("hitboxSize").vector2Value = new Vector2(4.4f, 2.1f);
            so.FindProperty("artworkSize").vector2Value = new Vector2(4.4f, 2.1f);
            so.FindProperty("artworkOffset").vector2Value = new Vector2(0, -.8f);
            so.ApplyModifiedPropertiesWithoutUndo(); Record(zone);
            service.body.sprite = plate; service.body.color = Color.white; service.body.enabled = true;
            float fit = Mathf.Min(zone.ArtworkSize.x / plate.bounds.size.x, zone.ArtworkSize.y / plate.bounds.size.y);
            service.body.transform.localScale = new Vector3(fit, fit, 1);
            service.body.transform.localPosition = zone.ArtworkOffset;
            service.body.sortingOrder = 113;
            service.hitbox.size = zone.HitboxSize; service.hitbox.offset = zone.ArtworkOffset;
            service.label.gameObject.SetActive(false);
            if (service.workspaceCaption != null) { service.workspaceCaption.gameObject.SetActive(false); Record(service.workspaceCaption.gameObject); }
            Record(service.body); Record(service.body.transform); Record(service.hitbox); Record(service.label.gameObject);

            var node = service.transform.Find("DeliveryBell");
            if (node == null)
            {
                node = new GameObject("DeliveryBell", typeof(BoxCollider2D), typeof(FreeCafeDeliveryBell)).transform;
                node.SetParent(service.transform, false);
            }
            node.localPosition = new Vector3(9.2f, -2.5f, -.2f);
            node.localScale = Vector3.one;
            var body = node.Find("BellBody");
            if (body == null) { body = new GameObject("BellBody", typeof(SpriteRenderer)).transform; body.SetParent(node, false); }
            var sr = body.GetComponent<SpriteRenderer>(); sr.sprite = bell; sr.color = Color.white; sr.sortingOrder = 500;
            float bellFit = 1.55f / bell.bounds.size.x;
            body.localPosition = Vector3.zero; body.localScale = new Vector3(bellFit, bellFit, 1);
            var box = node.GetComponent<BoxCollider2D>(); box.size = new Vector2(1.65f, 1.65f); box.offset = Vector2.zero; box.isTrigger = true;
            var caption = node.Find("BellCaption"); if (caption != null) { caption.gameObject.SetActive(false); Record(caption.gameObject); }
            var binding = new SerializedObject(node.GetComponent<FreeCafeDeliveryBell>());
            binding.FindProperty("runtime").objectReferenceValue = runtime;
            binding.FindProperty("bellBody").objectReferenceValue = body;
            binding.FindProperty("dome").objectReferenceValue = sr;
            binding.ApplyModifiedPropertiesWithoutUndo();
            Record(node); Record(body); Record(sr); Record(box); Record(binding.targetObject);
        }
        private static void Record(UnityEngine.Object value)
        {
            EditorUtility.SetDirty(value);
            if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
        }
    }
}
#endif
