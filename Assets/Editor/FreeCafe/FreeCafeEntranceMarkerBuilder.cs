using System.IO;
using Spotlight.FreeCafe;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>Creates presentation only; the owning portal/interaction supplies the action.</summary>
    public static class FreeCafeEntranceMarkerBuilder
    {
        private const string Folder = "Assets/Art/FreeCafe/Entities";
        private const string FramePath = Folder + "/entrance-marker-frame.png";
        private const string HaloPath = Folder + "/entrance-marker-halo.png";

        public static void ConfigurePortal(FreeCafePortal portal, TMP_FontAsset font)
        {
            // Retire the old arrow and the previous illustrated workbench plaque.
            var arrow = portal.GetComponent<SpriteRenderer>();
            if (arrow != null) arrow.enabled = false;
            string[] obsolete = { "IllustratedBoard", "SignCaption", "HangingSign" };
            foreach (string name in obsolete)
            {
                var old = portal.transform.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            portal.transform.localScale = Vector3.one;
            portal.transform.localRotation = Quaternion.identity;
            portal.entranceMarker = Configure(portal.transform, new Vector2(4.4f, 2.45f));
            portal.visual = portal.entranceMarker.frame;
            var hitbox = portal.GetComponent<BoxCollider2D>();
            if (hitbox == null) hitbox = portal.gameObject.AddComponent<BoxCollider2D>();
            hitbox.offset = Vector2.zero;
            hitbox.size = new Vector2(4.4f, 2.45f);
        }

        /// <summary>Reusable for a workbench, doorway or bed without introducing gameplay logic.</summary>
        public static FreeCafeEntranceMarker Configure(Transform owner, Vector2 size, int sortingOrder = 80)
        {
            EnsureArtwork();
            var marker = owner.GetComponent<FreeCafeEntranceMarker>();
            if (marker == null) marker = owner.gameObject.AddComponent<FreeCafeEntranceMarker>();
            marker.frame = Renderer(owner, "EntranceFrame", FramePath, size, sortingOrder);
            marker.halo = Renderer(owner, "EntranceHalo", HaloPath, size, sortingOrder - 1);
            marker.SetHovered(false);
            EditorUtility.SetDirty(marker);
            return marker;
        }

        private static SpriteRenderer Renderer(Transform owner, string name, string path, Vector2 size, int order)
        {
            var child = owner.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(owner, false);
            }
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            renderer.sortingOrder = order;
            renderer.enabled = true;
            child.localScale = new Vector3(size.x / renderer.sprite.bounds.size.x,
                size.y / renderer.sprite.bounds.size.y, 1f);
            return renderer;
        }

        private static void EnsureArtwork()
        {
            Directory.CreateDirectory(Folder);
            if (!File.Exists(FramePath)) CreateArtwork(FramePath, false);
            if (!File.Exists(HaloPath)) CreateArtwork(HaloPath, true);
        }

        private static void CreateArtwork(string path, bool halo)
        {
            const int width = 440, height = 245;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            // Rounded rectangular outline with transparent centre; a dark keyline keeps
            // the cream frame readable on both light floors and dark furnishings.
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector2 q = new Vector2(Mathf.Abs(x - (width - 1) * .5f) - 197f,
                    Mathf.Abs(y - (height - 1) * .5f) - 99.5f);
                float distance = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude
                    + Mathf.Min(Mathf.Max(q.x, q.y), 0) - 12f;
                float edge = Mathf.Abs(distance);
                if (halo)
                    pixels[y * width + x] = new Color(1, 1, 1, Mathf.Exp(-edge * edge / 85f));
                else
                {
                    float alpha = 1f - Mathf.SmoothStep(3.5f, 4.5f, edge);
                    float light = 1f - Mathf.SmoothStep(1.1f, 2.2f, edge);
                    pixels[y * width + x] = new Color(Mathf.Lerp(.24f, 1f, light),
                        Mathf.Lerp(.19f, 1f, light), Mathf.Lerp(.13f, 1f, light), alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }
}
