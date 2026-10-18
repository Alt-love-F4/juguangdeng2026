using System;
using System.IO;
using Spotlight.FreeCafe;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>Reuse the real entity, portal and tutorial targets; no parallel interaction layer.</summary>
    public static class FreeCafeHangingSignBuilder
    {
        public const string ArtworkPath = "Assets/Art/FreeCafe/Entities/cafe-hanging-sign.png";
        private const float Width = 4f;
        // The imported sprite is cropped to the prop. Its cord is above the text-safe inset.
        private static readonly Vector2 TextSize = new Vector2(3.25f, .72f);
        private static readonly Vector3 TextPosition = new Vector3(0f, -.31f, -.05f);

        public static void ConfigureBusinessSign(FreeEntityView view)
        {
            view.preserveAuthoredPosition = true;
            var sprite = LoadArtwork();
            var sign = view.GetComponent<FreeCafeBusinessSignView>();
            if (sign == null) sign = view.gameObject.AddComponent<FreeCafeBusinessSignView>();
            sign.entityView = view;
            var old = view.transform.Find("BusinessSignVisuals");
            if (old != null) old.gameObject.SetActive(false);
            var status = view.transform.Find("BusinessStatus");
            if (status != null) status.gameObject.SetActive(false);
            if (view.workspaceCaption != null) view.workspaceCaption.gameObject.SetActive(false);
            view.body.enabled = false;
            var root = Child(view.transform, "HangingSign");
            root.localPosition = Vector3.zero;
            root.localScale = Vector3.one;
            root.localRotation = Quaternion.identity;
            sign.flipRoot = root;
            var art = Child(root, "IllustratedBoard");
            sign.artwork = art.GetComponent<SpriteRenderer>();
            if (sign.artwork == null) sign.artwork = art.gameObject.AddComponent<SpriteRenderer>();
            SetArtwork(sign.artwork, sprite);
            view.label.transform.SetParent(root, false);
            ConfigureCaption(view.label, "开始营业");
            view.hitbox.size = new Vector2(Width, Width * sprite.bounds.size.y / sprite.bounds.size.x);
            view.hitbox.offset = Vector2.zero;
            sign.UpdateSorting(view.body.sortingOrder);
        }

        private static void ConfigureCaption(TMP_Text text, string caption)
        {
            text.transform.localPosition = TextPosition;
            text.transform.localScale = Vector3.one;
            text.transform.localRotation = Quaternion.identity;
            text.rectTransform.sizeDelta = TextSize;
            text.text = caption;
            text.fontSize = 6f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 3f;
            text.fontSizeMax = 6f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.margin = new Vector4(.1f, .04f, .1f, .04f);
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(.24f, .12f, .065f);
            text.raycastTarget = false;
        }
        private static void SetArtwork(SpriteRenderer renderer, Sprite sprite)
        {
            renderer.sprite = sprite;
            renderer.color = Color.white;
            float fit = Width / sprite.bounds.size.x;
            renderer.transform.localScale = new Vector3(fit, fit, 1);
        }
        private static Sprite LoadArtwork()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkPath);
            if (sprite == null) throw new InvalidOperationException("Missing cafe hanging-sign sprite: " + ArtworkPath);
            return sprite;
        }
        private static Transform Child(Transform parent, string name)
        {
            var result = parent.Find(name);
            if (result != null) return result;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        [MenuItem("Tools/Free Cafe/Apply Illustrated Cafe Signs")]
        public static void Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/CoffeeShop.unity")
                throw new InvalidOperationException("Open CoffeeShop outside Play Mode before applying cafe signs.");
            Directory.CreateDirectory("output/cafe-signs");
            if (!File.Exists("output/cafe-signs/CoffeeShop-before-signs.unity"))
                File.Copy(scene.path, "output/cafe-signs/CoffeeShop-before-signs.unity");
            string path = FreeCafeEntityPrefabBuilder.PathFor("business.start");
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureBusinessSign(contents.GetComponent<FreeEntityView>());
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            FreeEntityView business = null;
            foreach (var view in UnityEngine.Object.FindObjectsByType<FreeEntityView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (view.AuthoringEntityId == "business.start") { business = view; break; }
            if (business == null) throw new InvalidOperationException("Missing authored business.start entity.");
            ConfigureBusinessSign(business);
            foreach (var portal in UnityEngine.Object.FindObjectsByType<FreeCafePortal>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (portal.destination != AreaId.Counter) continue;
                FreeCafeInteractivePropBuilder.ConfigureBar(portal);
                // The old builder authored a loose sibling caption under the area root.
                foreach (var text in portal.transform.parent.GetComponentsInChildren<TMP_Text>(true))
                    if (text.transform != portal.transform && !text.transform.IsChildOf(portal.transform)
                        && text.text.Contains("进入工作台")) text.gameObject.SetActive(false);
                EditorUtility.SetDirty(portal);
            }
            EditorUtility.SetDirty(business);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
