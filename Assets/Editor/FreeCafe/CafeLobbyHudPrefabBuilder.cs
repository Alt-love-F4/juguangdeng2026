using System;
using System.Linq;
using Spotlight.FreeCafe;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>Uses the lobby PSD's sprite layers on the existing cafe Canvas.</summary>
    public static class CafeLobbyHudPrefabBuilder
    {
        public const string SourcePath = "Assets/Art/Gameplay/咖啡馆/大厅ui.prefab";
        public const string PrefabPath = "Assets/Prefabs/FreeCafe/CafeLobbyHud.prefab";
        private static readonly Color Ink = new Color(.30f, .17f, .07f);
        private static readonly Color Cream = new Color(1f, .94f, .78f);
        private static TMP_FontAsset Body => FreeCafeHudKit.Body;

        [MenuItem("Tools/Free Cafe/Rebuild Lobby HUD From PSD")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before rebuilding the lobby HUD.");
            BuildPrefab();
            Scene scene = SceneManager.GetSceneByPath(FreeCafeProductionSceneBuilder.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(FreeCafeProductionSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("CoffeeShop has unsaved edits. Save it before applying the lobby HUD.");
                var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FreeCafeHud>(true)).Single();
                Apply(hud);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("Lobby HUD uses the PSD order frame and currency artwork, with live cafe bindings and no duplicated gameplay header/background.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void BuildPrefab()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null || Body == null) throw new InvalidOperationException("Missing lobby PSD prefab or cafe font.");
            var layers = source.GetComponentsInChildren<SpriteRenderer>(true);
            Sprite Art(string name) => layers.First(layer => layer.name == name && layer.sprite != null).sprite;
            var root = new GameObject("CafeLobbyHud", typeof(RectTransform));
            root.layer = 5;
            Stretch((RectTransform)root.transform);
            try
            {
                var orders = Rect(root.transform, "Orders", new Vector2(0, 1), new Vector2(28, -24), new Vector2(528, 216));
                orders.pivot = new Vector2(0, 1);
                Picture(orders, "图层 3", Art("图层 3"), new Vector2(0, 0), new Vector2(528, 216));
                orders.Find("图层 3").GetComponent<Image>().raycastTarget = true;
                Picture(orders, "任务框 副本 2", Art("任务框 副本 2"), new Vector2(0, 0), new Vector2(516, 204));
                Text(orders, "Heading", "来客订单", new Vector2(0, 72), new Vector2(450, 34), 25, TextAlignmentOptions.Center);
                var order = Rect(orders, "Order0", new Vector2(.5f, .5f), new Vector2(0, -5), new Vector2(448, 94));
                ClickArea(order);
                Text(order, "Label", "等待顾客入座", Vector2.zero, new Vector2(436, 90), 22, TextAlignmentOptions.Center);
                Picture(orders, "左右切换按键 副本", Art("左右切换按键 副本"), new Vector2(0, -76), new Vector2(476, 44));
                ClickArea(Rect(orders, "PreviousOrder", new Vector2(.5f, .5f), new Vector2(-228, -76), new Vector2(56, 48)));
                ClickArea(Rect(orders, "NextOrder", new Vector2(.5f, .5f), new Vector2(228, -76), new Vector2(56, 48)));
                Text(orders, "Page", "暂无订单", new Vector2(0, -76), new Vector2(180, 28), 18, TextAlignmentOptions.Center);

                var resources = Rect(root.transform, "Resources", new Vector2(1, 1), new Vector2(-28, -24), new Vector2(460, 124));
                resources.pivot = new Vector2(1, 1);
                Picture(resources, "花纹 副本", Art("花纹 副本"), new Vector2(0, -19), new Vector2(438, 116));
                Picture(resources, "icon 副本", Art("icon 副本"), new Vector2(-55, 12), new Vector2(280, 98));
                Text(resources, "MoneyTitle", "金币", new Vector2(-60, 35), new Vector2(80, 28), 17, TextAlignmentOptions.Left).color = Cream;
                Text(resources, "ReputationTitle", "声望", new Vector2(120, 35), new Vector2(80, 28), 17, TextAlignmentOptions.Left).color = Cream;
                Text(resources, "Money", "500", new Vector2(-60, 4), new Vector2(80, 39), 28, TextAlignmentOptions.Left).color = Cream;
                Text(resources, "Reputation", "0", new Vector2(120, 4), new Vector2(80, 39), 28, TextAlignmentOptions.Left).color = Cream;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static void Apply(FreeCafeHud hud)
        {
            if (hud.playerRoot == null) throw new InvalidOperationException("Cafe player interface is missing.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { BuildPrefab(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); }
            Undo.RecordObject(hud, "Bind lobby PSD HUD");
            var parent = hud.playerRoot.transform;
            foreach (string name in new[] { "CafeLedger", "GuestTickets", "CafeLobbyHud", "CurrentArea" })
            {
                var old = parent.Find(name);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = "CafeLobbyHud";
            Undo.RegisterCreatedObjectUndo(instance, "Install lobby PSD HUD");
            Stretch((RectTransform)instance.transform);
            var orders = instance.transform.Find("Orders");
            hud.orderButtons = new[] { orders.Find("Order0").GetComponent<Button>() };
            hud.orderLabels = new TMP_Text[] { orders.Find("Order0/Label").GetComponent<TMP_Text>() };
            hud.previousOrder = orders.Find("PreviousOrder").GetComponent<Button>();
            hud.nextOrder = orders.Find("NextOrder").GetComponent<Button>();
            hud.orderPage = orders.Find("Page").GetComponent<TMP_Text>();
            hud.moneyLabel = instance.transform.Find("Resources/Money").GetComponent<TMP_Text>();
            hud.reputationLabel = instance.transform.Find("Resources/Reputation").GetComponent<TMP_Text>();
            hud.summary = hud.dayLabel = hud.statusLabel = hud.stockLabel = hud.areaName = null;
            hud.orderFocusSprite = hud.orderIdleSprite = null;
            EditorUtility.SetDirty(hud);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            FreeCafeHudKit.Place(rect, anchor, position, size);
            return rect;
        }

        private static void Picture(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
            => FreeCafeHudKit.Picture(parent, name, sprite, new Vector2(.5f, .5f), position, size);

        private static TMP_Text Text(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var rect = Rect(parent, name, new Vector2(.5f, .5f), position, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Body;
            label.text = text;
            label.fontSize = fontSize;
            label.fontSizeMax = fontSize;
            label.fontSizeMin = fontSize * .75f;
            label.enableAutoSizing = true;
            label.color = Ink;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        private static Button ClickArea(RectTransform rect)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Spotlight.Audio.UIButtonAudio.Configure(button);
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
