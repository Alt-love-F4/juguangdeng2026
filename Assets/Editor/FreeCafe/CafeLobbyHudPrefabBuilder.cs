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
                var dock = Rect(root.transform, "HeaderDock", new Vector2(.5f, 1), Vector2.zero, new Vector2(1920, 210));
                dock.pivot = new Vector2(.5f, 1);
                var orders = Rect(dock, "Orders", new Vector2(0, 1), new Vector2(28, -22), new Vector2(416, 166));
                orders.pivot = new Vector2(0, 1);
                Picture(orders, "图层 3", Art("图层 3"), new Vector2(0, 0), new Vector2(416, 166));
                orders.Find("图层 3").GetComponent<Image>().raycastTarget = true;
                Picture(orders, "任务框 副本 2", Art("任务框 副本 2"), new Vector2(0, 0), new Vector2(406, 158));
                Text(orders, "Heading", "来客订单", new Vector2(0, 54), new Vector2(350, 28), 20, TextAlignmentOptions.Center);
                var order = Rect(orders, "Order0", new Vector2(.5f, .5f), new Vector2(0, -3), new Vector2(350, 72));
                ClickArea(order);
                Text(order, "Label", "等待顾客入座", Vector2.zero, new Vector2(340, 70), 19, TextAlignmentOptions.Center);
                Picture(orders, "左右切换按键 副本", Art("左右切换按键 副本"), new Vector2(0, -58), new Vector2(375, 35));
                ClickArea(Rect(orders, "PreviousOrder", new Vector2(.5f, .5f), new Vector2(-179, -58), new Vector2(44, 38)));
                ClickArea(Rect(orders, "NextOrder", new Vector2(.5f, .5f), new Vector2(179, -58), new Vector2(44, 38)));
                Text(orders, "Page", "暂无订单", new Vector2(0, -58), new Vector2(180, 24), 15, TextAlignmentOptions.Center);

                var resources = Rect(dock, "Resources", new Vector2(1, 1), new Vector2(-48, -28), new Vector2(460, 124));
                resources.pivot = new Vector2(1, 1);
                resources.localScale = Vector3.one * .65f;
                Picture(resources, "花纹 副本", Art("花纹 副本"), new Vector2(0, -19), new Vector2(438, 116));
                Picture(resources, "icon 副本", Art("icon 副本"), new Vector2(-55, 12), new Vector2(280, 98));
                Text(resources, "MoneyTitle", "金币", new Vector2(-60, 35), new Vector2(80, 28), 17, TextAlignmentOptions.Left).color = Cream;
                Text(resources, "ReputationTitle", "声望", new Vector2(120, 35), new Vector2(80, 28), 17, TextAlignmentOptions.Left).color = Cream;
                Text(resources, "Money", "500", new Vector2(-60, 4), new Vector2(80, 39), 28, TextAlignmentOptions.Left).color = Cream;
                Text(resources, "Reputation", "0", new Vector2(120, 4), new Vector2(80, 39), 28, TextAlignmentOptions.Left).color = Cream;
                var menus = Rect(dock, "SecondaryMenus", new Vector2(1, 1), new Vector2(-48, -28), new Vector2(660, 132));
                menus.pivot = new Vector2(1, 1);
                string[] menuNames = { "OrderRecipe", "CoffeeBook", "ReturnToCafe", "PlayerMenu", "EmptySelectedMachine" };
                string[] menuLabels = { "当前配方", "咖啡谱", "返回大厅", "菜单", "清空所选容器" };
                for(int index=0;index<menuNames.Length;index++)
                {
                    var slot=Rect(menus,menuNames[index],new Vector2(.5f,.5f),index<4 ? new Vector2(-246+164*index,28) : new Vector2(0,-28),new Vector2(index<4 ? 154 : 252,44));
                    var button=ClickArea(slot);
                    var image=(Image)button.targetGraphic;
                    image.sprite=Art("二楼");image.color=Color.white;
                    Text(slot,"Label",menuLabels[index],Vector2.zero,new Vector2(index<4 ? 146 : 244,38),22,TextAlignmentOptions.Center);
                }
                menus.gameObject.SetActive(false);
                var floors = Rect(root.transform, "FloorSwitcher", new Vector2(1, 0), new Vector2(-110, 32), new Vector2(156, 132));
                floors.pivot = new Vector2(1, 0);
                string[] floorNames = { "一楼大堂", "二楼会客室", "三楼宿舍" };
                for (int index = 0; index < floorNames.Length; index++)
                {
                    var slot = Rect(floors, "Floor" + (index + 1), new Vector2(.5f, .5f), new Vector2(0, -44 + 44 * index), new Vector2(156, 38));
                    var button = ClickArea(slot);
                    var image = (Image)button.targetGraphic;
                    image.sprite = Art(index == 0 ? "当前楼层" : index == 1 ? "二楼" : "三楼");
                    image.color = Color.white;
                    var colors = button.colors;
                    colors.disabledColor = Color.white;
                    button.colors = colors;
                    Text(slot, "Label", floorNames[index], Vector2.zero, new Vector2(148, 32), 19, TextAlignmentOptions.Center);
                }
                FreeCafeHudKit.Plaque(root.transform, "EndDayButton", "结束这一天",
                    new Vector2(1, 0), new Vector2(-110, 198), new Vector2(220, 52), true, 20);
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
            foreach (string name in new[] { "CafeLedger", "GuestTickets", "CafeLobbyHud", "CurrentArea", "PlayerNavigation", "PlayerTools", "CafeTools", "CafeNote", "OpenCityMap" })
            {
                var old = parent.Find(name);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = "CafeLobbyHud";
            Undo.RegisterCreatedObjectUndo(instance, "Install lobby PSD HUD");
            Stretch((RectTransform)instance.transform);
            var orders = instance.transform.Find("HeaderDock/Orders");
            hud.areaButtons = new Button[4];
            hud.areaButtons[0] = instance.transform.Find("FloorSwitcher/Floor1").GetComponent<Button>();
            hud.areaButtons[2] = instance.transform.Find("FloorSwitcher/Floor2").GetComponent<Button>();
            hud.areaButtons[3] = instance.transform.Find("FloorSwitcher/Floor3").GetComponent<Button>();
            hud.endDayButton = instance.transform.Find("EndDayButton").GetComponent<Button>();
            hud.floorCurrentSprite = hud.areaButtons[0].GetComponent<Image>().sprite;
            hud.floorIdleSprite = hud.areaButtons[2].GetComponent<Image>().sprite;
            hud.mapButton = hud.pauseButton = hud.recipeButton = null;
            hud.hint = hud.selection = null;
            if (hud.runtime != null && hud.runtime.worldCamera != null)
            {
                Undo.RecordObject(hud.runtime.worldCamera, "Cafe letterbox palette");
                hud.runtime.worldCamera.backgroundColor = new Color(.60f, .51f, .43f, 1f);
                EditorUtility.SetDirty(hud.runtime.worldCamera);
            }
            hud.orderButtons = new[] { orders.Find("Order0").GetComponent<Button>() };
            hud.orderLabels = new TMP_Text[] { orders.Find("Order0/Label").GetComponent<TMP_Text>() };
            hud.previousOrder = orders.Find("PreviousOrder").GetComponent<Button>();
            hud.nextOrder = orders.Find("NextOrder").GetComponent<Button>();
            hud.orderPage = orders.Find("Page").GetComponent<TMP_Text>();
            hud.moneyLabel = instance.transform.Find("HeaderDock/Resources/Money").GetComponent<TMP_Text>();
            hud.reputationLabel = instance.transform.Find("HeaderDock/Resources/Reputation").GetComponent<TMP_Text>();
            hud.summary = hud.dayLabel = hud.statusLabel = hud.stockLabel = hud.areaName = null;
            hud.orderFocusSprite = hud.orderIdleSprite = null;
            if(hud.runtime!=null) BindWorkstation(hud,hud.runtime.GetComponent<FreeCafeProductionHud>());
            EditorUtility.SetDirty(hud);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
        }

        public static void BindWorkstation(FreeCafeHud player, FreeCafeProductionHud production)
        {
            if(production==null || player.playerRoot==null) return;
            var menus=player.playerRoot.transform.Find("CafeLobbyHud/HeaderDock/SecondaryMenus");
            if(menus==null) throw new InvalidOperationException("Rebuild the shared cafe HUD before binding workstation menus.");
            Undo.RecordObject(production,"Reuse shared cafe order panel");
            if(production.productionRoot!=null)
                foreach(string path in new[]{"ProductionHeader/OrderTicket","ProductionHeader/StackedOrder0","ProductionHeader/StackedOrder1","ProductionHeader/CafeTools","ProductionHeader/MachineHint","ProductionFooter/ReturnToCafe","ProductionFooter/EmptySelectedMachine","ProductionFooter/Instruction","ProductionFooter/LastResult"})
                {
                    var old=production.productionRoot.transform.Find(path);
                    if(old!=null) Undo.DestroyObjectImmediate(old.gameObject);
                }
            production.orderTitle=production.customerName=production.orderDescription=production.orderProduct=production.orderIndex=production.patienceText=null;
            production.customerAvatar=production.patienceFill=null;
            production.stackedOrderSheets=Array.Empty<GameObject>();
            production.previousOrder=production.nextOrder=null;
            production.instruction=production.result=production.machineStatus=null;
            production.moneyText=production.reputationText=null;
            production.orderRecipe=menus.Find("OrderRecipe").GetComponent<Button>();
            production.bookButton=menus.Find("CoffeeBook").GetComponent<Button>();
            production.returnButton=menus.Find("ReturnToCafe").GetComponent<Button>();
            production.pauseButton=menus.Find("PlayerMenu").GetComponent<Button>();
            production.clearMachine=menus.Find("EmptySelectedMachine").GetComponent<Button>();
            EditorUtility.SetDirty(production);
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
