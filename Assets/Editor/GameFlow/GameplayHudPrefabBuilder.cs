using System;
using System.Linq;
using Spotlight.GameFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>Moves authored chrome into its own prefab, independent of the map at runtime.</summary>
    public static class GameplayHudPrefabBuilder
    {
        private const string MapPath = "Assets/Resources/Art/UIPrefab/MapPanel.prefab";
        public const string HudPath = "Assets/Resources/Art/UIPrefab/GameplayHudPanel.prefab";

        [MenuItem("Tools/GameFlow/Extract Shared Gameplay HUD")]
        public static void ExtractExisting()
        {
            var map = PrefabUtility.LoadPrefabContents(MapPath);
            try
            {
                Extract(map);
                PrefabUtility.SaveAsPrefabAsset(map, MapPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(map); }
            AssetDatabase.SaveAssets();
        }

        private static void BindArtwork(GameObject root)
        {
            var artwork = root.transform.Find("ArtworkHud");
            artwork.SetAsLastSibling(); // Header buttons remain clickable while a drawer slides underneath.
            var data = new SerializedObject(root.GetComponent<GameplayHudPanel>());
            Transform legacyEndDay = root.transform.Find("TaskPanel/结束当天");
            if (legacyEndDay != null) UnityEngine.Object.DestroyImmediate(legacyEndDay.gameObject);
            data.FindProperty("endDayButton").objectReferenceValue = null;
            data.FindProperty("pointsFill").objectReferenceValue = artwork.Find("PointsFill").GetComponent<Image>();
            data.FindProperty("dateLabel").objectReferenceValue = artwork.Find("Date/DateText").GetComponent<TextMeshProUGUI>();
            var segments = artwork.GetComponentsInChildren<Image>(true)
                .Where(image => image.name.StartsWith("PointSegment", StringComparison.Ordinal))
                .OrderBy(image => image.name, StringComparer.Ordinal).ToArray();
            var list = data.FindProperty("pointsSegments");
            list.arraySize = segments.Length;
            for (int index = 0; index < segments.Length; index++)
                list.GetArrayElementAtIndex(index).objectReferenceValue = segments[index];
            data.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<Canvas>().sortingOrder = GameplayHudPanel.SortingOrder;
            NormalizeRoot(root);
        }

        private static void NormalizeRoot(GameObject root)
        {
            var rect = (RectTransform)root.transform;
            rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public static void Extract(GameObject map)
        {
            Transform art = map.transform.Find("HUD/ArtworkHud");
            if (art == null)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(HudPath) == null)
                    throw new InvalidOperationException("Map artwork and shared HUD prefab are both missing.");
                var hud = PrefabUtility.LoadPrefabContents(HudPath);
                try
                {
                    BindArtwork(hud);
                    PrefabUtility.SaveAsPrefabAsset(hud, HudPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(hud); }
                return;
            }
            GameObject root = new GameObject("GameplayHudPanel", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var rect = (RectTransform)root.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true; canvas.sortingOrder = GameplayHudPanel.SortingOrder;
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(2400, 1350); scaler.matchWidthOrHeight = .5f;
                root.AddComponent<GraphicRaycaster>();
                root.AddComponent<CanvasGroup>();
                var panel = root.AddComponent<GameplayHudPanel>();
                var controller = new SerializedObject(map.GetComponent<MapSceneController>());
                var header = (TextMeshProUGUI)controller.FindProperty("headerLabel").objectReferenceValue;
                var inventory = map.GetComponentInChildren<MapActionInventoryView>(true);
                var tasks = map.GetComponentInChildren<MapTaskPanel>(true);
                var endDay = (Button)controller.FindProperty("endDayButton").objectReferenceValue;
                art.SetParent(root.transform, false);
                header.transform.SetParent(root.transform, false);
                inventory.transform.SetParent(root.transform, false);
                tasks.transform.SetParent(root.transform, false);
                var data = new SerializedObject(panel);
                void Set(string field, UnityEngine.Object value) => data.FindProperty(field).objectReferenceValue = value;
                Set("artwork", art); Set("header", header); Set("inventory", inventory); Set("tasks", tasks);
                Set("detailsButton", art.Find("详情面板").GetComponent<Button>());
                Set("inventoryButton", art.Find("InventoryToggle").GetComponent<Button>());
                Set("tasksButton", art.Find("TasksToggle").GetComponent<Button>());
                Set("endDayButton", endDay);
                data.FindProperty("showDuration").floatValue = 0;
                data.FindProperty("hideDuration").floatValue = 0;
                data.FindProperty("showOffset").floatValue = 0;
                data.FindProperty("showStartScale").floatValue = 1;
                data.ApplyModifiedPropertiesWithoutUndo();
                foreach (string field in new[] { "headerLabel", "inventoryView", "endDayButton" })
                    controller.FindProperty(field).objectReferenceValue = null;
                controller.ApplyModifiedPropertiesWithoutUndo();
                BindArtwork(root);
                root.SetActive(true);
                NormalizeRoot(root);
                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
                Debug.Log("Extracted shared gameplay HUD: " + HudPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
