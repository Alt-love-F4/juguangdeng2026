using System;
using System.IO;
using System.Linq;
using Spotlight.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.Gameplay.EditorTools
{
    public static class CoffeeWorkspaceBuilder
    {
        private const string ScenePath = "Assets/Scenes/CoffeeShop.unity";
        private const string PrefabDir = "Assets/Resources/Interaction";
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/SimHei SDF.asset");
        [MenuItem("Tools/Spotlight/Validate Cafe Tables")]
        public static void Validate()
        {
            GameplayConfigService.ResetCache(); GameplayConfigService.EnsureLoaded();
            var definitions = GameplayConfigService.AllEntities.ToDictionary(d => d.id);
            foreach (var d in definitions.Values)
            {
                if (string.IsNullOrWhiteSpace(d.id) || !new[] { "item", "source", "slot", "machine", "npc" }.Contains(d.role)) throw new InvalidOperationException("无效实体角色：" + d.id);
                if (d.role == "source" && (!definitions.TryGetValue(d.item_id, out var item) || item.role != "item")) throw new InvalidOperationException("无效物品来源：" + d.id);
                if (d.role == "machine" && GameplayConfigService.GetDevice(d.behavior_id) == null) throw new InvalidOperationException("设备配置缺失：" + d.id);
                if (!string.IsNullOrEmpty(d.sprite_path) && SpriteFor(d.sprite_path) == null) throw new InvalidOperationException("美术资源缺失：" + d.sprite_path);
                if (d.capacity < 1) throw new InvalidOperationException("容量必须大于0：" + d.id);
            }
            foreach (var recipe in GameplayConfigService.AllRecipes)
            {
                var steps = RecipeStepParser.ForRecipe(recipe);
                if (steps.First().Kind != RecipeStepKind.SelectCup || steps.Last().Kind != RecipeStepKind.Serve) throw new InvalidOperationException("配方必须以选杯开始，上餐结束：" + recipe.id);
            }
            Debug.Log("[CafeValidation] 配表引用、角色、美术与配方步骤检查通过。");
        }
        [MenuItem("Tools/Spotlight/Build Cafe Workspace")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play Mode");
            Validate();
            Directory.CreateDirectory(PrefabDir + "/Items"); Directory.CreateDirectory(PrefabDir + "/Entities"); Directory.CreateDirectory(PrefabDir + "/UI");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("请先打开 CoffeeShop 场景");
            var runtimeComponent = Object.FindObjectsByType<CoffeeWorldRuntime>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(x => x.gameObject.scene == scene);
            if (runtimeComponent != null)
            {
                var workspaceRoot = runtimeComponent.transform.parent != null &&
                                    runtimeComponent.transform.parent.name == "InteractionRuntime"
                    ? runtimeComponent.transform.parent.gameObject
                    : runtimeComponent.gameObject;
                Object.DestroyImmediate(workspaceRoot);
            }
            var generated = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "CoffeeWorkspace"); if (generated != null) Object.DestroyImmediate(generated);
            var generatedCanvas = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "CafeHUD"); if (generatedCanvas != null) Object.DestroyImmediate(generatedCanvas);
            // Preserve the original art/layout as disabled migration reference; never rebuild user art files.
            var legacy = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "CoffeeWorldRoot");
            if (legacy != null) foreach (Transform child in legacy.transform) if (child.name != "Background") child.gameObject.SetActive(false);
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == "GameplayCanvas" || g.name == "UnityCliBridge_Sample_Temp"))
            { old.SetActive(false); foreach (var panel in old.GetComponentsInChildren<CoffeeGameplayPanel>(true)) Object.DestroyImmediate(panel); }
            var cam = Object.FindFirstObjectByType<Camera>();
            cam.tag = "MainCamera"; cam.orthographic = true; cam.orthographicSize = 5.4f; cam.transform.position = new Vector3(0, 0, -10);
            if (cam.GetComponent<Physics2DRaycaster>() == null) cam.gameObject.AddComponent<Physics2DRaycaster>();
            if (Object.FindFirstObjectByType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var workspace = new GameObject("CoffeeWorkspace"); var runtime = workspace.AddComponent<CoffeeWorldRuntime>();
            var cafeEntities = new GameObject("CafeEntities"); cafeEntities.transform.SetParent(workspace.transform, false);
            var counterEntities = new GameObject("ServingCounterEntities"); counterEntities.transform.SetParent(workspace.transform, false);
            var sources = new GameObject("ItemSources"); sources.transform.SetParent(counterEntities.transform, false);
            var stations = new GameObject("Stations"); stations.transform.SetParent(counterEntities.transform, false);
            var placements = new GameObject("PlacementSlots"); placements.transform.SetParent(counterEntities.transform, false);
            var carryables = new GameObject("CarryableItems"); carryables.transform.SetParent(counterEntities.transform, false);
            var prefabs = GameplayConfigService.AllEntities.Select(CreateEntityPrefab).ToArray();
            runtime.itemPrefabs = prefabs.Where(p => p is CarryableEntity).Cast<CarryableEntity>().ToArray();
            runtime.npcSprites = GameplayConfigService.AllNpcs.Select(n => SpriteFor(n.sprite_path)).ToArray();
            runtime.finishedCupSprite = SpriteFor("Assets/Art/Gameplay/cup_latte.png");
            foreach (var prefab in prefabs.Where(p => !(p is CarryableEntity)))
            {
                var prefabEntity = prefab.GetComponent<InteractiveEntity>();
                var parent = prefabEntity is NpcEntity ? cafeEntities.transform : prefabEntity is ItemSourceEntity ? sources.transform : GameplayConfigService.GetEntity(prefabEntity.definitionId).role == "machine" ? stations.transform : placements.transform;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
                var entity = instance.GetComponent<InteractiveEntity>();
                var d = GameplayConfigService.GetEntity(entity.definitionId); entity.transform.position = new Vector3(d.x, d.y, 0);
            }
            runtime.carryableRoot = carryables.transform;
            var workspaceAsset = PrefabUtility.SaveAsPrefabAsset(workspace, PrefabDir + "/CoffeeWorkspace.prefab");
            Object.DestroyImmediate(workspace); PrefabUtility.InstantiatePrefab(workspaceAsset, scene);
            var beanSelectionButtonPrefab = CreateBeanSelectionButtonPrefab();
            var hud = BuildHud(beanSelectionButtonPrefab); var hudAsset = PrefabUtility.SaveAsPrefabAsset(hud, PrefabDir + "/CafeHUD.prefab");
            Object.DestroyImmediate(hud); PrefabUtility.InstantiatePrefab(hudAsset, scene);
            CoffeeLocationSceneBuilder.Build(scene);
            Debug.Log("[CafeBuild] 咖啡店已装配为数据驱动的实体预制体；原布局保留为禁用参考。");
        }
        private static Sprite SpriteFor(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        private static Sprite SlotSprite()
        {
            const string path = "Assets/Art/Gameplay/workbench_slot.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(96, 36); var pixels = new Color[96 * 36];
                for (int y = 0; y < 36; y++) for (int x = 0; x < 96; x++) pixels[y * 96 + x] = x < 3 || y < 3 || x >= 93 || y >= 33 ? new Color(.88f, .7f, .38f, 1) : new Color(.24f, .16f, .12f, .8f);
                texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 100; importer.SaveAndReimport();
            }
            return SpriteFor(path);
        }
        private static InteractiveEntity CreateEntityPrefab(EntityDefinition d)
        {
            var go = new GameObject(d.id); var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = string.IsNullOrEmpty(d.sprite_path) ? SlotSprite() : SpriteFor(d.sprite_path); renderer.sortingOrder = d.role == "slot" ? 0 : 3;
            go.transform.localScale = Vector3.one * (d.height / Mathf.Max(.01f, renderer.sprite.bounds.size.y));
            var collider = go.AddComponent<BoxCollider2D>(); collider.size = renderer.sprite.bounds.size; collider.isTrigger = true;
            InteractiveEntity entity = d.role switch { "item" => go.AddComponent<CarryableEntity>(), "source" => go.AddComponent<ItemSourceEntity>(), "npc" => go.AddComponent<NpcEntity>(), _ => go.AddComponent<PlacementEntity>() };
            entity.definitionId = d.id;
            if (d.role != "item")
            {
                var label = new GameObject("Label", typeof(TextMeshPro)); label.transform.SetParent(go.transform, false);
                label.transform.localScale = Vector3.one / go.transform.localScale.x; label.transform.localPosition = new Vector3(0, -renderer.sprite.bounds.extents.y - .18f / go.transform.localScale.x, -.1f);
                var text = label.GetComponent<TextMeshPro>(); text.font = Font; text.fontSize = 2.3f; text.alignment = TextAlignmentOptions.Center; text.text = d.name; text.color = new Color(1, .95f, .8f); text.GetComponent<MeshRenderer>().sortingOrder = 10;
                text.rectTransform.sizeDelta = new Vector2(3.5f, .7f);
            }
            if (d.role == "source" && d.item_id.StartsWith("cup."))
            {
                for (int i = 1; i < 3; i++) { var extra = new GameObject("Stack" + i, typeof(SpriteRenderer)); extra.transform.SetParent(go.transform, false); extra.transform.localPosition = new Vector3(i * .16f, i * .14f, .01f); var sprite = extra.GetComponent<SpriteRenderer>(); sprite.sprite = renderer.sprite; sprite.sortingOrder = 2; }
            }
            string path = PrefabDir + (d.role == "item" ? "/Items/" : "/Entities/") + d.id + ".prefab";
            var asset = PrefabUtility.SaveAsPrefabAsset(go, path); Object.DestroyImmediate(go); return asset.GetComponent<InteractiveEntity>();
        }
        private static GameObject BuildHud(GameObject beanSelectionButtonPrefab)
        {
            var canvas = new GameObject("CafeHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvasComponent = canvas.GetComponent<Canvas>(); canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay; canvasComponent.overrideSorting = true; canvasComponent.sortingOrder = 500;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1f;
            var root = Rect(canvas.transform, "CoffeeGameplayPanel", Vector2.zero, new Vector2(1920, 1080)); Stretch(root);
            root.gameObject.AddComponent<CanvasGroup>(); var panel = root.gameObject.AddComponent<CoffeeGameplayPanel>();
            Rect(root, "PanelV3", Vector2.zero, Vector2.zero);
            var top = Box(root, "TopBar", new Vector2(0, 500), new Vector2(1920, 80), new Color(.13f, .1f, .08f, .95f));
            panel.goldText = Text(top, "GoldText", "金币", new Vector2(-810, 5), new Vector2(220, 40), 24);
            panel.repText = Text(top, "RepText", "声望", new Vector2(-580, 5), new Vector2(200, 40), 24);
            panel.skillText = Text(top, "SkillText", "技能点", new Vector2(-360, 5), new Vector2(200, 40), 24);
            panel.timerText = Text(top, "TimerText", "待营业", new Vector2(210, 5), new Vector2(760, 55), 23);
            panel.pendingCountText = Text(top, "PendingText", "待接待", new Vector2(790, 5), new Vector2(220, 40), 23);
            var order = Box(root, "OrderCard", new Vector2(-800, 240), new Vector2(270, 370), new Color(.13f, .1f, .08f, .86f));
            Text(order, "Title", "今日订单", new Vector2(0, 150), new Vector2(230, 40), 24);
            panel.orderListText = Text(order, "OrderListText", "暂无订单", new Vector2(0, -20), new Vector2(235, 275), 19); panel.orderListText.alignment = TextAlignmentOptions.TopLeft;
            panel.stepText = Text(root, "StepText", "开始营业", new Vector2(0, 400), new Vector2(1100, 60), 26);
            panel.timerSlider = Slider(root, "ProcessProgress", new Vector2(0, 357), new Vector2(480, 18));
            var stock = Box(root, "Inventory", new Vector2(400, 165), new Vector2(500, 235), new Color(.13f, .1f, .08f, .88f));
            Text(stock, "Title", "库存 / 补货", new Vector2(0, 94), new Vector2(440, 32), 20); panel.inventoryRows = stock;
            int index = 0; foreach (var item in GameplayConfigService.AllIngredients)
            { var row = Rect(stock, item.id, new Vector2(0, 64 - index++ * 29), new Vector2(470, 28)); Text(row, "Stock", item.comment, new Vector2(-42, 0), new Vector2(365, 27), 17).alignment = TextAlignmentOptions.Left; Button(row, "Refill", "补货", new Vector2(190, 0), new Vector2(70, 25), 16); }
            var upgrades = Box(root, "DeviceUpgrades", new Vector2(-365, 180), new Vector2(485, 207), new Color(.13f, .1f, .08f, .88f));
            Text(upgrades, "Title", "设备升级", new Vector2(0, 80), new Vector2(420, 30), 20); panel.deviceRows = upgrades;
            index = 0; foreach (var item in GameplayConfigService.AllDevices)
            { var row = Rect(upgrades, item.id, new Vector2(0, 49 - index++ * 28), new Vector2(460, 27)); Text(row, "Device", item.comment, new Vector2(-45, 0), new Vector2(355, 27), 16).alignment = TextAlignmentOptions.Left; Button(row, "Upgrade", "升级", new Vector2(187, 0), new Vector2(68, 25), 16); }
            var bottom = Box(root, "CommandBar", new Vector2(0, -438), new Vector2(1920, 204), new Color(.13f, .1f, .08f, .94f));
            panel.hintText = Text(bottom, "InteractionHint", "拖出杯子与原料，松开到合法放置点", new Vector2(0, 74), new Vector2(1740, 37), 21);
            panel.startDayButton = Button(bottom, "StartDayButton", "开始营业", new Vector2(-830, 17), new Vector2(145, 44));
            panel.takeOrderButton = Button(bottom, "TakeOrderButton", "交流 / 接单", new Vector2(-665, 17), new Vector2(150, 44));
            panel.selectCupButton = Button(bottom, "SelectCupButton", "取杯", new Vector2(-505, 17), new Vector2(140, 44));
            var actionButtonObject = (GameObject)PrefabUtility.InstantiatePrefab(beanSelectionButtonPrefab, bottom);
            actionButtonObject.name = "ActionButton";
            var actionRect = (RectTransform)actionButtonObject.transform;
            actionRect.anchorMin = actionRect.anchorMax = actionRect.pivot = new Vector2(.5f, .5f);
            actionRect.anchoredPosition = new Vector2(-330, 17);
            actionRect.sizeDelta = new Vector2(190, 44);
            panel.beanSelectionButton = actionButtonObject.GetComponent<BeanSelectionButton>();
            panel.grindButton = panel.beanSelectionButton != null ? panel.beanSelectionButton.Button : actionButtonObject.GetComponent<Button>();
            panel.serveButton = Button(bottom, "ServeButton", "上餐", new Vector2(-150, 17), new Vector2(135, 44));
            panel.discardButton = Button(bottom, "DiscardButton", "放弃订单", new Vector2(10, 17), new Vector2(150, 44));
            panel.clearItemsButton = Button(bottom, "ClearItemsButton", "收回闲置物品", new Vector2(205, 17), new Vector2(210, 44));
            panel.endDayButton = Button(bottom, "EndDayButton", "提前打烊", new Vector2(405, 17), new Vector2(150, 44));
            panel.pauseButton = Button(bottom, "PauseButton", "暂停", new Vector2(635, 17), new Vector2(125, 44));
            panel.quickSaveButton = Button(bottom, "QuickSaveButton", "快速存档", new Vector2(810, 17), new Vector2(165, 44));
            Text(bottom, "ShortcutLabel", "辅助加料：", new Vector2(-840, -53), new Vector2(140, 35), 18);
            panel.addCoffeeButton = Button(bottom, "AddCoffeeButton", "咖啡豆", new Vector2(-680, -53), new Vector2(140, 38));
            panel.addMilkButton = Button(bottom, "AddMilkButton", "牛奶", new Vector2(-510, -53), new Vector2(140, 38));
            panel.addMatchaButton = Button(bottom, "AddMatchaButton", "抹茶", new Vector2(-340, -53), new Vector2(140, 38));
            panel.addChocoButton = Button(bottom, "AddChocoButton", "可可", new Vector2(-170, -53), new Vector2(140, 38));
            panel.addHoneyButton = Button(bottom, "AddHoneyButton", "蜂蜜", new Vector2(0, -53), new Vector2(140, 38));
            Text(bottom, "Note", "世界拖拽与辅助按钮使用同一套规则 · Esc 暂停", new Vector2(525, -53), new Vector2(710, 38), 18);
            BuildMinigames(root, panel); LayoutLocationHud(root, panel); return canvas;
        }
        private static void LayoutLocationHud(RectTransform root, CoffeeGameplayPanel panel)
        {
            var context = root.gameObject.AddComponent<CoffeeLocationHudUI>();
            var top = (RectTransform)root.Find("TopBar");
            top.anchorMin = new Vector2(0, 1); top.anchorMax = Vector2.one; top.pivot = new Vector2(.5f, 1);
            top.offsetMin = new Vector2(0, -160); top.offsetMax = Vector2.zero;
            top.GetComponent<Image>().color = Color.black;
            Move(top.Find("GoldText"), top, new Vector2(-815, -30), new Vector2(210, 42), true);
            Move(top.Find("RepText"), top, new Vector2(-590, -30), new Vector2(200, 42), true);
            Move(top.Find("SkillText"), top, new Vector2(-380, -30), new Vector2(210, 42), true);
            Move(top.Find("TimerText"), top, new Vector2(80, -30), new Vector2(640, 48), true);
            Move(top.Find("PendingText"), top, new Vector2(510, -30), new Vector2(230, 42), true);
            Move(panel.stepText.transform, top, new Vector2(-230, -95), new Vector2(1400, 45), true);
            Move(panel.timerSlider.transform, top, new Vector2(-225, -139), new Vector2(550, 12), true);
            context.ordersButton = Button(top, "OrdersToggle", "订单", new Vector2(735, -75), new Vector2(105, 42));
            context.inventoryButton = Button(top, "InventoryToggle", "库存", new Vector2(855, -75), new Vector2(105, 42));
            context.upgradesButton = Button(top, "UpgradesToggle", "设备升级", new Vector2(795, -126), new Vector2(220, 40));
            // Button helper uses center anchors; switch the menu buttons to the bar's top edge.
            foreach (var button in new[] { context.ordersButton, context.inventoryButton, context.upgradesButton })
                ((RectTransform)button.transform).anchorMin = ((RectTransform)button.transform).anchorMax = new Vector2(.5f, 1);
            context.ordersPanel = root.Find("OrderCard").gameObject;
            context.inventoryPanel = panel.inventoryRows.gameObject; context.upgradesPanel = panel.deviceRows.gameObject;
            Move(context.ordersPanel.transform, root, new Vector2(-800, 220), new Vector2(270, 370));
            Move(context.inventoryPanel.transform, root, new Vector2(620, 255), new Vector2(500, 235));
            Move(context.upgradesPanel.transform, root, new Vector2(620, 260), new Vector2(485, 207));
            context.ordersPanel.SetActive(false); context.inventoryPanel.SetActive(false); context.upgradesPanel.SetActive(false);
            var bottom = (RectTransform)root.Find("CommandBar");
            bottom.anchorMin = bottom.anchorMax = Vector2.zero; bottom.pivot = Vector2.zero;
            bottom.anchoredPosition = Vector2.zero; bottom.sizeDelta = new Vector2(1490, 160);
            bottom.GetComponent<Image>().color = Color.black;
            Move(panel.hintText.transform, bottom, new Vector2(-20, 52), new Vector2(1400, 30));
            panel.hintText.fontSize = 18;
            var firstRow = new[] { panel.startDayButton, panel.takeOrderButton, panel.selectCupButton, panel.grindButton, panel.serveButton, panel.discardButton, panel.clearItemsButton, panel.endDayButton, panel.pauseButton, panel.quickSaveButton };
            for (int i = 0; i < firstRow.Length; i++)
            {
                Move(firstRow[i].transform, bottom, new Vector2(-670 + i * 145, 10), new Vector2(138, 40));
                var label = firstRow[i].GetComponentInChildren<TMP_Text>(); if (label != null) label.fontSize = 16;
            }
            var shortcuts = new[] { panel.addCoffeeButton, panel.addMilkButton, panel.addMatchaButton, panel.addChocoButton, panel.addHoneyButton };
            for (int i = 0; i < shortcuts.Length; i++) Move(shortcuts[i].transform, bottom, new Vector2(-520 + i * 145, -43), new Vector2(134, 35));
            Move(bottom.Find("ShortcutLabel"), bottom, new Vector2(-670, -43), new Vector2(140, 35));
            Move(bottom.Find("Note"), bottom, new Vector2(475, -43), new Vector2(475, 34));
            bottom.Find("Note").GetComponent<TMP_Text>().text = "右侧进入出餐台 · Esc 暂停";
            bottom.Find("Note").GetComponent<TMP_Text>().fontSize = 16;
            context.businessControls = new[] { panel.startDayButton.gameObject, panel.takeOrderButton.gameObject, panel.discardButton.gameObject, panel.endDayButton.gameObject };
            context.counterControls = new[] { panel.selectCupButton.gameObject, panel.grindButton.gameObject, panel.serveButton.gameObject, panel.clearItemsButton.gameObject, panel.addCoffeeButton.gameObject, panel.addMilkButton.gameObject, panel.addMatchaButton.gameObject, panel.addChocoButton.gameObject, panel.addHoneyButton.gameObject, bottom.Find("ShortcutLabel").gameObject };
        }
        private static void Move(Transform target, Transform parent, Vector2 position, Vector2 size, bool top = false)
        {
            // Only used on the temporary HUD before it is saved as a prefab.
            target.SetParent(parent, false); var rect = (RectTransform)target;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, top ? 1 : .5f);
            rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        private static void BuildMinigames(RectTransform root, CoffeeGameplayPanel panel)
        {
            var beans = Popup(root, "BeanSelectionPopup", "咖啡豆筛选 · 选择深棕色好豆"); panel.beanPopup = beans.gameObject;
            var bean = beans.gameObject.AddComponent<BeanSelectionMinigame>(); panel.beanSelectionPanel = bean;
            var grid = Rect(beans, "BeanGrid", new Vector2(0, 15), new Vector2(460, 335)); bean.grid = grid.gameObject.AddComponent<GridLayoutGroup>(); bean.grid.cellSize = new Vector2(75, 65); bean.grid.spacing = new Vector2(17, 14); bean.grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; bean.grid.constraintCount = 5;
            var cell = Box(beans, "BeanCellTemplate", new Vector2(10000, 10000), new Vector2(75, 65), new Color(.4f, .2f, .1f)); cell.gameObject.AddComponent<UnityEngine.UI.Button>();
            var cellAsset = PrefabUtility.SaveAsPrefabAsset(cell.gameObject, PrefabDir + "/BeanCell.prefab"); Object.DestroyImmediate(cell.gameObject); bean.cellPrefab = cellAsset;
            bean.scoreText = Text(beans, "BeanScore", "", new Vector2(0, -170), new Vector2(510, 35), 22); bean.hintText = Text(beans, "BeanHint", "", new Vector2(0, 215), new Vector2(580, 70), 19);
            bean.confirmButton = Button(beans, "ConfirmBeansButton", "确认筛选", new Vector2(0, -230), new Vector2(200, 46));
            var latte = Popup(root, "LatteArtPopup", "自由拉花 · 在咖啡表面拖动绘制"); panel.lattePopup = latte.gameObject;
            var area = Rect(latte, "LatteDrawingCanvas", new Vector2(0, 15), new Vector2(360, 360)); var raw = area.gameObject.AddComponent<RawImage>();
            var art = area.gameObject.AddComponent<LatteArtMinigame>(); panel.latteArtPanel = art; art.canvasImage = raw;
            art.hintText = Text(latte, "LatteHint", "", new Vector2(0, 220), new Vector2(600, 55), 19); art.progressSlider = Slider(latte, "LatteCoverage", new Vector2(0, -182), new Vector2(360, 15));
            art.finishButton = Button(latte, "FinishLatteButton", "完成拉花", new Vector2(120, -235), new Vector2(210, 46)); art.resetButton = Button(latte, "ResetLatteButton", "重新绘制", new Vector2(-120, -235), new Vector2(210, 46));
            var cocoa = Popup(root, "CocoaQTEPopup", "可可搅拌 · 在绿区停住指针"); panel.cocoaPopup = cocoa.gameObject;
            var qte = cocoa.gameObject.AddComponent<CocoaQTEMinigame>(); panel.cocoaQTEPanel = qte;
            qte.trackSlider = Slider(cocoa, "CocoaTrack", new Vector2(0, 25), new Vector2(540, 40));
            qte.targetZone = Box(qte.trackSlider.transform, "TargetZone", Vector2.zero, new Vector2(100, 40), new Color(.28f, .66f, .38f, .8f)).GetComponent<Image>();
            qte.indicator = Box(qte.trackSlider.transform, "Pointer", Vector2.zero, new Vector2(8, 54), new Color(1, .96f, .84f)).GetComponent<Image>();
            qte.hintText = Text(cocoa, "CocoaHint", "", new Vector2(0, 165), new Vector2(580, 75), 23); qte.attemptText = Text(cocoa, "CocoaAttempts", "", new Vector2(0, -45), new Vector2(500, 40), 24);
            qte.actionButton = Button(cocoa, "StopCocoaButton", "现在停！", new Vector2(0, -140), new Vector2(240, 60), 26);
            var pause = Popup(root, "SandboxPause", "游戏已暂停"); panel.pausePopup = pause.gameObject; panel.resumeButton = Button(pause, "ResumeButton", "继续营业", Vector2.zero, new Vector2(240, 60), 25);
            beans.gameObject.SetActive(false); latte.gameObject.SetActive(false); cocoa.gameObject.SetActive(false); pause.gameObject.SetActive(false);
        }
        private static GameObject CreateBeanSelectionButtonPrefab()
        {
            var path = PrefabDir + "/UI/BeanSelectionButton.prefab";
            var go = new GameObject("BeanSelectionButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(BeanSelectionButton));
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(190, 44);
            var image = go.GetComponent<Image>();
            image.color = new Color(.43f, .28f, .15f, 1f);
            image.raycastTarget = true;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var text = Text(rect, "Label", "使用设备", Vector2.zero, new Vector2(182, 40), 20);
            var component = go.GetComponent<BeanSelectionButton>();
            component.SetReferences(button, text, image);
            var asset = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return asset;
        }

        private static RectTransform Popup(Transform parent, string name, string title)
        {
            var blocker = Box(parent, name, Vector2.zero, new Vector2(1920, 1080), new Color(.04f, .025f, .02f, .83f)); blocker.GetComponent<Image>().raycastTarget = true;
            var content = Box(blocker, "Content", Vector2.zero, new Vector2(680, 640), new Color(.18f, .12f, .09f, 1));
            Text(content, "Title", title, new Vector2(0, 285), new Vector2(620, 50), 25);
            // Return the blocker as the popup root, with controls centered; the content is only its visual background.
            return blocker;
        }
        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform; rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static RectTransform Box(Transform parent, string name, Vector2 p, Vector2 size, Color color)
        { var rect = Rect(parent, name, p, size); var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return rect; }
        private static TMP_Text Text(Transform parent, string name, string value, Vector2 p, Vector2 size, int fontSize)
        { var rect = Rect(parent, name, p, size); var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = Font; text.text = value; text.fontSize = fontSize; text.color = new Color(1, .95f, .83f); text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; return text; }
        private static Button Button(Transform parent, string name, string value, Vector2 p, Vector2 size, int fontSize = 20)
        { var rect = Box(parent, name, p, size, new Color(.43f, .28f, .15f, 1)); rect.GetComponent<Image>().raycastTarget = true; var button = rect.gameObject.AddComponent<Button>(); Text(rect, "Label", value, Vector2.zero, size - new Vector2(8, 4), fontSize); return button; }
        private static Slider Slider(Transform parent, string name, Vector2 p, Vector2 size)
        {
            var rect = Box(parent, name, p, size, new Color(.08f, .06f, .04f, 1)); var slider = rect.gameObject.AddComponent<Slider>();
            var fill = Box(rect, "Fill", Vector2.zero, size, new Color(.8f, .59f, .27f, 1)); Stretch(fill); slider.fillRect = fill; slider.interactable = false; slider.minValue = 0; slider.maxValue = 1; return slider;
        }
    }
}
