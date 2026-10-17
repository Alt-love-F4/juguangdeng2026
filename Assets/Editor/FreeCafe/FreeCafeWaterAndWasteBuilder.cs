using System;
using System.Collections.Generic;
using System.Linq;
using Spotlight.FreeCafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>Applies only the water pitcher and waste pail, preserving the authored cafe.</summary>
    public static class FreeCafeWaterAndWasteBuilder
    {
        [MenuItem("Tools/Free Cafe/Apply Water Pitcher And Waste Pail")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Exit Play Mode and wait for compilation.");
            var scene = SceneManager.GetSceneByPath(FreeCafeProductionSceneBuilder.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(FreeCafeProductionSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save CoffeeShop before applying the props.");
                var app = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FreeCafeRuntime>(true)).Single();
                var catalog = JsonUtility.FromJson<CafeCatalog>(app.catalogAsset.text);
                var ids = new HashSet<string> { "source.water", "trash" };
                var entries = new List<CafeArtEntry>(app.artLibrary.entries);
                foreach (var spec in catalog.entities.Where(spec => ids.Contains(spec.id)))
                {
                    string path = "Assets/Art/FreeCafe/Entities/" + spec.artId + "/default.png";
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 100;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    var entry = entries.Find(item => item.brief.id == spec.artId);
                    if (entry == null)
                    {
                        entry = new CafeArtEntry { brief = new ArtBrief { id = spec.artId, label = spec.label,
                            category = "entity", outputFolder = "Assets/Art/FreeCafe/Entities/" + spec.artId,
                            transparent = true, worldWidth = spec.width, worldHeight = spec.height, states = new[] { "default" } }, whitebox = sprite };
                        entries.Add(entry);
                    }
                    entry.variants = new[] { new ArtVariant { state = "default", sprite = sprite } };
                }
                app.artLibrary.entries = entries.ToArray();
                EditorUtility.SetDirty(app.artLibrary);
                app.prefabLibrary = FreeCafeEntityPrefabBuilder.EnsureCatalog(catalog, app.artLibrary, app.entityPrefab, ids);
                string waterPath = FreeCafeEntityPrefabBuilder.PathFor("source.water");
                var waterPrefab = PrefabUtility.LoadPrefabContents(waterPath);
                try
                {
                    var waterView = waterPrefab.GetComponent<FreeEntityView>();
                    waterView.pourOrigin.localPosition = new Vector3(-.602f, .648f, 0);
                    PrefabUtility.SaveAsPrefabAsset(waterPrefab, waterPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(waterPrefab); }
                foreach (var view in FreeCafeSceneLayout.FindAuthoredEntities(app).Where(view => ids.Contains(view.PrefabSpecId)))
                {
                    var layout = Array.Find(catalog.layout, entry => entry.id == view.AuthoringEntityId);
                    var spec = Array.Find(catalog.entities, entry => entry.id == view.PrefabSpecId);
                    view.ConfigureAuthoring(spec, app.artLibrary, layout.id, layout.x, layout.y);
                    var area = app.areaRoots[(int)layout.area];
                    view.transform.position = area.TransformPoint(new Vector3(layout.x, layout.y, 0));
                    var parentScale = view.transform.parent.lossyScale;
                    view.transform.localScale = new Vector3(area.lossyScale.x / parentScale.x, area.lossyScale.y / parentScale.y, area.lossyScale.z / parentScale.z);
                    // Water pours from the left-hand lip of its new artwork.
                    if (spec.id == "source.water" && view.pourOrigin != null)
                        view.pourOrigin.localPosition = new Vector3(-spec.width * .43f, spec.height * .36f, 0);
                    foreach (var target in new UnityEngine.Object[] { view, view.transform, view.body, view.body.transform,
                        view.hitbox, view.label, view.label.transform, view.pourOrigin })
                        if (target != null) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                }
                EditorUtility.SetDirty(app);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("Water pitcher and waste pail updated: upper right / lower left, reusable water container and larger waste target.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
