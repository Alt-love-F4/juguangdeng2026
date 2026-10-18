using System;
using Spotlight.FreeCafe;
using Spotlight.GameFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.FreeCafe.Editor
{
    public static class FreeCafeInteractivePropBuilder
    {
        public const string ArrowArtworkPath = "Assets/Art/FreeCafe/Whitebox/ui.portal.png";

        public static void ConfigureBar(FreeCafePortal portal)
        {
            RemoveEntranceMarker(portal);
            RemoveChild(portal.transform, "CafeBarProp");
            var prop = ConfigureArrow(portal.transform, "EntranceArrow", Vector3.zero, 0f, 10);
            var collider = portal.GetComponent<BoxCollider2D>();
            // Include the actual counter as well as the arrow. Keep the portal's edited transform.
            FitClickArea(collider, new Bounds(new Vector3(2.7f, 1.55f, 0f), new Vector3(8.2f, 6.6f, 0f)), prop.artwork);
            portal.visual = prop.artwork;
            portal.propView = prop;
            portal.entranceMarker = null;
            EditorUtility.SetDirty(portal);
        }

        public static void ConfigureBed(Transform areaRoot, FreeCafeRuntime runtime)
        {
            // These old visual-only mockups must not return when the entrance setup is reapplied.
            foreach (string name in new[] { "WHITEBOX_future.press", "WHITEBOX_future.newspaper", "AreaArtNote" })
                RemoveChild(areaRoot, name);
            var root = areaRoot.Find("DayBedProp");
            if (root == null)
            {
                root = new GameObject("DayBedProp").transform;
                root.SetParent(areaRoot, false);
            }
            // Foreground left-hand bed in the authored dormitory background (area-local coordinates).
            root.localPosition = new Vector3(-19.2f, -6.2f, .1f);
            RemoveChild(root, "BedArtwork");
            var prop = ConfigureArrow(root, "BedEntrance", new Vector3(0f, 2.2f, -.02f), -90f, 20);
            var collider = prop.gameObject.GetComponent<BoxCollider2D>();
            if (collider == null) collider = prop.gameObject.AddComponent<BoxCollider2D>();
            FitClickArea(collider, new Bounds(new Vector3(0f, -.3f, 0f), new Vector3(11.4f, 5.4f, 0f)), prop.artwork);
            var dayEnd = prop.GetComponent<FreeCafeBedDayEnd>();
            if (dayEnd == null) dayEnd = prop.gameObject.AddComponent<FreeCafeBedDayEnd>();
            dayEnd.runtime = runtime;
            dayEnd.dayFlow = UnityEngine.Object.FindFirstObjectByType<DayFlowController>();
            EditorUtility.SetDirty(root.gameObject);
        }

        private static FreeCafeInteractivePropView ConfigureArrow(Transform owner, string name, Vector3 position, float angle, int order)
        {
            var root = owner.Find(name);
            if (root == null)
            {
                root = new GameObject(name).transform;
                root.SetParent(owner, false);
            }
            // The interaction root stays unrotated so its hitbox covers the furniture.
            var prop = root.GetComponent<FreeCafeInteractivePropView>();
            if (prop == null) prop = root.gameObject.AddComponent<FreeCafeInteractivePropView>();
            prop.artwork = Renderer(root, "Artwork", position, angle, 2.4f, order + 1, Color.white);
            prop.highlight = Renderer(root, "Highlight", position, angle, 2.55f, order, new Color(1f, .78f, .28f, .03f));
            prop.SetHovered(false);
            return prop;
        }

        private static SpriteRenderer Renderer(Transform parent, string name, Vector3 position, float angle, float width, int order, Color color)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }
            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArrowArtworkPath);
            if (renderer.sprite == null) throw new InvalidOperationException("Missing entrance arrow: " + ArrowArtworkPath);
            renderer.color = color;
            renderer.sortingOrder = order;
            child.localPosition = position;
            child.localRotation = Quaternion.Euler(0f, 0f, angle);
            float fit = width / renderer.sprite.bounds.size.x;
            child.localScale = new Vector3(fit, fit, 1f);
            return renderer;
        }

        private static void FitClickArea(BoxCollider2D collider, Bounds furniture, SpriteRenderer arrow)
        {
            var bounds = furniture;
            var art = arrow.bounds;
            for (int corner = 0; corner < 4; corner++)
            {
                var world = new Vector3((corner & 1) == 0 ? art.min.x : art.max.x,
                    (corner & 2) == 0 ? art.min.y : art.max.y, art.center.z);
                var local = collider.transform.InverseTransformPoint(world);
                local.z = 0f;
                bounds.Encapsulate(local);
            }
            collider.offset = new Vector2(bounds.center.x, bounds.center.y);
            collider.size = new Vector2(bounds.size.x + .3f, bounds.size.y + .3f);
            collider.isTrigger = true;
        }

        private static void RemoveChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        private static void RemoveEntranceMarker(FreeCafePortal portal)
        {
            foreach (string child in new[] { "EntranceFrame", "EntranceHalo" }) RemoveChild(portal.transform, child);
            var marker = portal.GetComponent<FreeCafeEntranceMarker>();
            if (marker != null) UnityEngine.Object.DestroyImmediate(marker);
            var oldArrow = portal.GetComponent<SpriteRenderer>();
            if (oldArrow != null) oldArrow.enabled = false;
        }

        [MenuItem("Tools/Free Cafe/Apply Cafe Entrance Arrows")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/CoffeeShop.unity")
                throw new InvalidOperationException("Open CoffeeShop outside Play Mode before applying entrance arrows.");
            if (AssetDatabase.LoadAssetAtPath<Sprite>(ArrowArtworkPath) == null)
                throw new InvalidOperationException("Import the entrance arrow sprite first.");
            var runtime = UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            if (runtime == null || runtime.areaRoots == null) throw new InvalidOperationException("Missing FreeCafeRuntime area roots.");
            foreach (var portal in UnityEngine.Object.FindObjectsByType<FreeCafePortal>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (portal.destination == AreaId.Counter) ConfigureBar(portal);
            ConfigureBed(runtime.areaRoots[(int)AreaId.Basement], runtime);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
