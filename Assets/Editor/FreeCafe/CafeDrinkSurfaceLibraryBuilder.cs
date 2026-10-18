using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>
    /// Builds Resources/FreeCafe/DrinkSurfaces.asset: one elliptical surface per product, plus the measured
    /// mouth ellipse of each counter cup sprite. Re-running keeps hand-tuned cup ellipses.
    /// </summary>
    public static class CafeDrinkSurfaceLibraryBuilder
    {
        private const string AssetPath = "Assets/Resources/FreeCafe/DrinkSurfaces.asset";
        private const string SurfaceFolder = "Assets/Art/FreeCafe/DrinkSurfaces";
        private const string CatalogPath = "Assets/Resources/FreeCafe/catalog.json";

        // Measured on Assets/Art/Gameplay/咖啡馆/Sliced/Entities/<cup>/default.png (pixels, origin top-left).
        private static readonly (string specId, Vector2 size, Vector2 center, Vector2 radius)[] MeasuredCups =
        {
            ("cup.small", new Vector2(367f, 257f), new Vector2(156f, 54f), new Vector2(132f, 37f)),
            ("cup.large", new Vector2(439f, 404f), new Vector2(175.5f, 75.7f), new Vector2(150.5f, 61.1f)),
        };

        [MenuItem("Tools/Free Cafe/Rebuild Drink Surface Library")]
        public static CafeDrinkSurfaceLibrary Build()
        {
            var library = AssetDatabase.LoadAssetAtPath<CafeDrinkSurfaceLibrary>(AssetPath);
            bool created = library == null;
            if (created) library = ScriptableObject.CreateInstance<CafeDrinkSurfaceLibrary>();

            var drinks = new List<CafeDrinkSurfaceLibrary.DrinkSurface>();
            foreach (string productId in ProductIds())
            {
                string path = SurfaceFolder + "/surface_" + productId + ".png";
                if (!File.Exists(path)) { Debug.LogWarning("[FreeCafe] No drink surface art for " + productId + " (" + path + ")"); continue; }
                ConfigureImporter(path);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) throw new InvalidOperationException("Drink surface did not import as a sprite: " + path);
                drinks.Add(new CafeDrinkSurfaceLibrary.DrinkSurface { productId = productId, sprite = sprite });
            }
            library.drinks = drinks.ToArray();

            var cups = new List<CafeDrinkSurfaceLibrary.CupMouth>(library.cups ?? Array.Empty<CafeDrinkSurfaceLibrary.CupMouth>());
            foreach (var measured in MeasuredCups)
            {
                if (cups.Exists(cup => cup != null && cup.specId == measured.specId)) continue; // keep Inspector tuning
                cups.Add(new CafeDrinkSurfaceLibrary.CupMouth
                {
                    specId = measured.specId,
                    center = new Vector2(measured.center.x / measured.size.x, 1f - measured.center.y / measured.size.y),
                    radius = new Vector2(measured.radius.x / measured.size.x, measured.radius.y / measured.size.y),
                    inset = .96f
                });
            }
            library.cups = cups.ToArray();

            if (created) AssetDatabase.CreateAsset(library, AssetPath);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("[FreeCafe] Drink surface library: " + library.drinks.Length + " drinks, " + library.cups.Length + " cups.");
            return library;
        }

        private static IEnumerable<string> ProductIds()
        {
            var catalog = JsonUtility.FromJson<CafeCatalog>(File.ReadAllText(CatalogPath));
            foreach (var product in catalog.products)
                if (product != null && !string.IsNullOrEmpty(product.id)) yield return product.id;
        }

        private static void ConfigureImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                && !importer.mipmapEnabled && importer.alphaIsTransparency && importer.wrapMode == TextureWrapMode.Clamp) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
    }
}
