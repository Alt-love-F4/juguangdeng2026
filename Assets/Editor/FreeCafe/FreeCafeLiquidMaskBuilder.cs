using System;
using System.IO;
using Spotlight.GameFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>Bind generated masks to the actual scene's prefab library without rebuilding its layout.</summary>
    public static class FreeCafeLiquidMaskBuilder
    {
        private const string MaskDirectory = "Assets/Art/Gameplay/咖啡馆/LiquidMasks/";
        [Serializable] private sealed class Shape { public string specId; public string source; }
        [Serializable] private sealed class Manifest { public float distanceRange; public float wallThickness; public Shape[] shapes; }

        [MenuItem("Tools/Free Cafe/Apply Container Liquid Masks")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Exit Play Mode and wait for compilation before binding masks.");
            var runtime = UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            if (runtime == null || runtime.prefabLibrary == null || runtime.gameObject.scene.path != "Assets/Scenes/CoffeeShop.unity")
                throw new InvalidOperationException("Open the existing CoffeeShop scene with its configured entity prefab library.");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("Tools/FreeCafe/liquid-mask-shapes.json"));
            string backup = "output/liquid-masks/backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            var scene = runtime.gameObject.scene;
            File.Copy(scene.path, Path.Combine(backup, "CoffeeShop.unity"));
            foreach (var shape in manifest.shapes)
            {
                var prefab = runtime.prefabLibrary.Require(shape.specId);
                string path = AssetDatabase.GetAssetPath(prefab);
                if (prefab.body == null || AssetDatabase.GetAssetPath(prefab.body.sprite) != shape.source)
                    throw new InvalidOperationException("Mask source no longer matches the scene's container prefab: " + shape.specId);
                string maskPath = MaskDirectory + shape.specId + ".png";
                AssetDatabase.ImportAsset(maskPath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(maskPath);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false; // Keep signed-distance RGB beyond the transparent contour.
                importer.isReadable = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
                var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
                File.Copy(path, Path.Combine(backup, Path.GetFileName(path)));
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view = root.GetComponent<FreeEntityView>();
                    view.ConfigureLiquidContainerMask(mask, true, manifest.distanceRange, manifest.wallThickness);
                    // Never persist per-instance UI materials in a prefab.
                    foreach (var liquid in root.GetComponentsInChildren<GravityLiquidImage>(true))
                        liquid.GetComponent<Image>().material = null;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            // Scene overrides can retain older presentation anchors. Refresh only the matching containers.
            foreach (var view in UnityEngine.Object.FindObjectsByType<FreeEntityView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (view.gameObject.scene != scene) continue;
                foreach (var shape in manifest.shapes)
                {
                    if (shape.specId != view.PrefabSpecId) continue;
                    var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(MaskDirectory + shape.specId + ".png");
                    view.ConfigureLiquidContainerMask(mask, true, manifest.distanceRange, manifest.wallThickness);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                    EditorUtility.SetDirty(view);
                    break;
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[FreeCafe Liquid Masks] Bound " + manifest.shapes.Length + " vessel silhouettes. Backup: " + backup);
        }
    }
}
