using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Spotlight.Gameplay.EditorTools
{
    /// <summary>
    /// 自动场景装配：编辑器加载后检查 CoffeeShop 场景，缺失的世界物体/玩法面板自动补齐。
    /// 幂等：已存在则跳过；手动删除某部分后下次编译会自动重建该部分。
    /// 标记：CoffeeWorldRoot（世界物体）、GameplayCanvas（玩法 UI）。
    /// </summary>
    [InitializeOnLoad]
    public static class CoffeeGameplaySceneSetup
    {
        private const string ScenePath = "Assets/Scenes/CoffeeShop.unity";
        private const string ArtDir = "Assets/Art/Gameplay";
        private const string FontGuid = "9b3b14dcfb2b88341ba78702f38a6eaa";

        static CoffeeGameplaySceneSetup()
        {
            EditorApplication.delayCall += EnsureScene;
        }

        private static void EnsureScene()
        {
                        if(File.Exists("Assets/Resources/FreeCafe/catalog.json")) return;
if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenePath)) return;

            string yaml = File.ReadAllText(ScenePath);
            bool needWorld = !yaml.Contains("CoffeeWorldRoot");
            // 面板版本标记：存在面板但无 PanelV2 标记 → 旧布局，销毁重建
            bool panelOutdated = yaml.Contains("CoffeeGameplayPanel") && !yaml.Contains("PanelV2");
            bool needCanvas = !yaml.Contains("GameplayCanvas") || panelOutdated;
            bool needPanel = !yaml.Contains("CoffeeGameplayPanel") || panelOutdated;
            if (!needWorld && !needCanvas && !needPanel) return;

            var active = EditorSceneManager.GetActiveScene();
            bool wasActive = active.path == ScenePath;
            Scene scene = wasActive
                ? active
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                if (needWorld)
                    BuildWorld(scene);

                if (panelOutdated)
                {
                    var oldCanvas = FindInScene(scene, "GameplayCanvas");
                    if (oldCanvas != null) Object.DestroyImmediate(oldCanvas);
                }

                if (needCanvas || needPanel)
                    BuildPanelSection(scene);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[CoffeeSetup] CoffeeShop 场景已自动补齐缺失内容。");
            }
            finally
            {
                if (!wasActive)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ---------- 世界物体 ----------

        private static void BuildWorld(Scene scene)
        {
            ApplySpriteSettings();

            // 相机规范化：正交、铺满 10.8 高度
            var camGo = FindInScene(scene, "Main Camera");
            if (camGo != null && camGo.TryGetComponent<Camera>(out var cam))
            {
                cam.orthographic = true;
                cam.orthographicSize = 5.4f;
                camGo.transform.position = new Vector3(0, 0, -10);
            }

            var worldRoot = new GameObject("CoffeeWorldRoot");
            SceneManager.MoveGameObjectToScene(worldRoot, scene);

            // 背景铺满视野
            var bg = CreateSprite(worldRoot.transform, "Background", LoadArtSprite("bg_coffee_shop"),
                Vector3.zero, sortingOrder: -10);
            ScaleToHeight(bg, 10.8f);

            // ========== 按线框图布局 ==========
            // 上半部分：原料架（右侧）
            // 下半部分：设备区（左中）+ 出餐区（右侧）

            // 原料架区域（世界坐标右侧，y≈2.5 高度）
            var ingShelf = new GameObject("IngredientShelf");
            ingShelf.transform.SetParent(worldRoot.transform, false);
            ingShelf.transform.position = new Vector3(5.5f, 2.5f, 0);

            var ingCoffee = CreateSprite(ingShelf.transform, "IngCoffeeBeans", LoadArtSprite("ing_coffee_beans"),
                new Vector3(-1.5f, 0, 0), sortingOrder: 1);
            ScaleToHeight(ingCoffee, 1.2f);
            var ingMilk = CreateSprite(ingShelf.transform, "IngMilk", LoadArtSprite("ing_milk"),
                new Vector3(-0.5f, 0, 0), sortingOrder: 1);
            ScaleToHeight(ingMilk, 1.2f);
            var ingMatcha = CreateSprite(ingShelf.transform, "IngMatcha", LoadArtSprite("ing_matcha"),
                new Vector3(0.5f, 0, 0), sortingOrder: 1);
            ScaleToHeight(ingMatcha, 1.2f);
            var ingChoco = CreateSprite(ingShelf.transform, "IngChocolate", LoadArtSprite("ing_chocolate"),
                new Vector3(1.5f, 0, 0), sortingOrder: 1);
            ScaleToHeight(ingChoco, 1.2f);

            // 设备区（世界坐标左下区域，台面 y≈-2.1）
            // 研磨器
            var grinder = CreateSprite(worldRoot.transform, "MachineGrinder", LoadArtSprite("machine_grinder"),
                new Vector3(-6.5f, -1.2f, 0), sortingOrder: 1);
            ScaleToHeight(grinder, 1.8f);
            var matchaGrinder = CreateSprite(worldRoot.transform, "MachineMatchaGrinder", LoadArtSprite("machine_matcha_grinder"),
                new Vector3(-4.5f, -1.2f, 0), sortingOrder: 1);
            ScaleToHeight(matchaGrinder, 1.8f);

            // 煮制设备（摩卡壶/牛奶壶/可可壶）
            var espresso = CreateSprite(worldRoot.transform, "MachineEspresso", LoadArtSprite("machine_espresso"),
                new Vector3(-2.0f, -1.0f, 0), sortingOrder: 1);
            ScaleToHeight(espresso, 2.2f);
            var milkPot = CreateSprite(worldRoot.transform, "MachineMilkPot", LoadArtSprite("machine_milk_pot"),
                new Vector3(0.0f, -1.2f, 0), sortingOrder: 1);
            ScaleToHeight(milkPot, 1.6f);
            var cocoaPot = CreateSprite(worldRoot.transform, "MachineCocoaPot", LoadArtSprite("machine_cocoa_pot"),
                new Vector3(1.8f, -1.2f, 0), sortingOrder: 1);
            ScaleToHeight(cocoaPot, 1.6f);

            // 杯型选择（世界坐标左侧，台面上方）
            var cupSmall = CreateSprite(worldRoot.transform, "CupSmall", LoadArtSprite("cup_small"),
                new Vector3(-8.0f, -0.8f, 0), sortingOrder: 2);
            ScaleToHeight(cupSmall, 1.0f);
            var cupEmpty = CreateSprite(worldRoot.transform, "CupEmpty", LoadArtSprite("cup_empty"),
                new Vector3(-7.0f, -1.4f, 0), sortingOrder: 2);
            ScaleToHeight(cupEmpty, 1.4f);
            var cupLatte = CreateSprite(worldRoot.transform, "CupLatte", LoadArtSprite("cup_latte"),
                cupEmpty.transform.position, sortingOrder: 2);
            ScaleToHeight(cupLatte, 1.4f);

            // 出餐区（世界坐标右下）
            var serveWindow = CreateSprite(worldRoot.transform, "ServeWindow", LoadArtSprite("serve_window"),
                new Vector3(7.5f, -1.0f, 0), sortingOrder: 1);
            ScaleToHeight(serveWindow, 2.0f);

            // 原料（拉花杯/蜂蜜/奶油顶）- 出餐区下方
            var ingHoney = CreateSprite(worldRoot.transform, "IngHoney", LoadArtSprite("ing_honey"),
                new Vector3(6.5f, -2.8f, 0), sortingOrder: 1);
            ScaleToHeight(ingHoney, 1.0f);
            var ingCream = CreateSprite(worldRoot.transform, "IngCream", LoadArtSprite("ing_cream"),
                new Vector3(7.5f, -2.8f, 0), sortingOrder: 1);
            ScaleToHeight(ingCream, 1.0f);

            // 顾客：画面右侧，站在吧台前（脚底 y≈-3.5）
            var customer = CreateSprite(worldRoot.transform, "Customer", LoadArtSprite("customer_1"),
                new Vector3(4.8f, -1.4f, 0), sortingOrder: 1);
            ScaleToHeight(customer, 4.2f);

            var view = worldRoot.AddComponent<CoffeeShopSceneView>();
            view.customerRenderer = customer;
            view.cupEmptyRenderer = cupEmpty;
            view.cupLatteRenderer = cupLatte;
            view.machineEspresso = espresso;
            view.machineGrinder = grinder;
            view.machineMilkPot = milkPot;
            view.machineCocoaPot = cocoaPot;
            view.machineMatchaGrinder = matchaGrinder;
            view.serveWindow = serveWindow;
            view.cupSmall = cupSmall;
            view.ingCoffeeBeans = ingCoffee;
            view.ingMilk = ingMilk;
            view.ingMatcha = ingMatcha;
            view.ingChocolate = ingChoco;
            view.ingHoney = ingHoney;
            view.ingCream = ingCream;
            view.customerSprites = new[]
            {
                LoadArtSprite("customer_1"),
                LoadArtSprite("customer_2"),
                LoadArtSprite("customer_3")
            };

            // 为世界物体添加碰撞体和交互组件
            AddInteractable(cupSmall.gameObject, InteractableType.CupSmall);
            AddInteractable(cupEmpty.gameObject, InteractableType.CupRegular);
            AddInteractable(cupLatte.gameObject, InteractableType.CupRegular);
            AddInteractable(grinder.gameObject, InteractableType.MachineGrinder);
            AddInteractable(matchaGrinder.gameObject, InteractableType.MachineMatchaGrinder);
            AddInteractable(espresso.gameObject, InteractableType.MachineEspresso);
            AddInteractable(milkPot.gameObject, InteractableType.MachineMilkPot);
            AddInteractable(cocoaPot.gameObject, InteractableType.MachineCocoaPot);
            AddInteractable(ingCoffee.gameObject, InteractableType.IngCoffeeBeans);
            AddInteractable(ingMilk.gameObject, InteractableType.IngMilk);
            AddInteractable(ingMatcha.gameObject, InteractableType.IngMatcha);
            AddInteractable(ingChoco.gameObject, InteractableType.IngChocolate);
            AddInteractable(ingHoney.gameObject, InteractableType.IngHoney);
            AddInteractable(ingCream.gameObject, InteractableType.IngCream);
            AddInteractable(serveWindow.gameObject, InteractableType.ServeWindow);
        }

        private static void AddInteractable(GameObject go, InteractableType type)
        {
            var col = go.AddComponent<BoxCollider2D>();
            // 根据 sprite 大小自动调整碰撞体
            if (go.TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite != null)
            {
                col.size = sr.sprite.bounds.size;
            }
            var interact = go.AddComponent<WorldInteractable>();
            interact.interactType = type;
        }

        private static void ApplySpriteSettings()
        {
            if (!Directory.Exists(ArtDir)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                if (importer.textureType == TextureImporterType.Sprite) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        private static SpriteRenderer CreateSprite(Transform parent, string name, Sprite sprite, Vector3 pos, int sortingOrder)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        /// <summary>按目标世界高度统一缩放，消除不同图片分辨率差异。</summary>
        private static void ScaleToHeight(SpriteRenderer renderer, float targetHeight)
        {
            if (renderer == null || renderer.sprite == null) return;
            float h = renderer.bounds.size.y;
            if (h <= 0.0001f) return;
            float k = targetHeight / h;
            renderer.transform.localScale = new Vector3(k, k, 1f);
        }

        // ---------- 玩法面板 ----------

        private static void BuildPanelSection(Scene scene)
        {
            string yaml = File.ReadAllText(ScenePath); // 场景可能刚被改过世界物体，重新读
            bool hasCanvas = yaml.Contains("GameplayCanvas");
            bool hasPanel = yaml.Contains("CoffeeGameplayPanel");

            GameObject canvasGo = FindInScene(scene, "GameplayCanvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("GameplayCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(canvasGo, scene);
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }

            GameObject panelGo = FindInScene(scene, "CoffeeGameplayPanel");
            if (panelGo == null)
            {
                panelGo = BuildPanel();
                SceneManager.MoveGameObjectToScene(panelGo, scene);
                panelGo.transform.SetParent(canvasGo.transform, false);
            }
            else if (panelGo.transform.parent == null || panelGo.transform.parent.gameObject != canvasGo)
            {
                panelGo.transform.SetParent(canvasGo.transform, false);
            }
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindRecursive(root.transform, name);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        private static Transform FindRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                var found = FindRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject BuildPanel()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
            var root = new GameObject("CoffeeGameplayPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(CoffeeGameplayPanel));
            Stretch((RectTransform)root.transform);

            // 版本标记：布局升级时更换标记名，旧面板会被整体重建
            new GameObject("PanelV2", typeof(RectTransform)).transform.SetParent(root.transform, false);

            // 背景全透明：场景世界才是画面主体，HUD 不再遮挡
            var bg = CreateImage(root.transform, "Background", null, new Color(0f, 0f, 0f, 0f));
            bg.raycastTarget = false;
            Stretch((RectTransform)bg.transform);

            var refs = new Dictionary<string, Object>();

            var topBar = CreateImage(root.transform, "TopBar", null, new Color(0.08f, 0.06f, 0.05f, 0.9f));
            var topRect = (RectTransform)topBar.transform;
            topRect.anchorMin = new Vector2(0, 1); topRect.anchorMax = new Vector2(1, 1);
            topRect.pivot = new Vector2(0.5f, 1);
            topRect.anchoredPosition = Vector2.zero; topRect.sizeDelta = new Vector2(0, 50);

            // TopBar 子物体的 anchoredPosition 相对于条中心：左负右正
            refs["goldText"] = CreateText(topRect, "GoldText", "金币: 0", font, 20, new Vector2(-860, -25), new Vector2(160, 40), TextAlignmentOptions.Left);
            refs["repText"] = CreateText(topRect, "RepText", "声望: 0", font, 20, new Vector2(-680, -25), new Vector2(160, 40), TextAlignmentOptions.Left);
            refs["skillText"] = CreateText(topRect, "SkillText", "技能点: 0", font, 20, new Vector2(-500, -25), new Vector2(160, 40), TextAlignmentOptions.Left);
            refs["timerText"] = CreateText(topRect, "TimerText", "待营业", font, 22, new Vector2(0, -25), new Vector2(300, 40), TextAlignmentOptions.Center);
            refs["pendingCountText"] = CreateText(topRect, "PendingText", "待处理: 0", font, 20, new Vector2(680, -25), new Vector2(180, 40), TextAlignmentOptions.Right);

            var orderPanel = CreateImage(root.transform, "OrderPanel", null, new Color(0.18f, 0.14f, 0.11f, 0.85f));
            var orderRect = (RectTransform)orderPanel.transform;
            orderRect.anchorMin = new Vector2(0, 1); orderRect.anchorMax = new Vector2(0, 1);
            orderRect.pivot = new Vector2(0, 1);
            orderRect.anchoredPosition = new Vector2(20, -60); orderRect.sizeDelta = new Vector2(300, 520);
            refs["orderListText"] = CreateText(orderRect, "OrderListText", "暂无订单", font, 18, Vector2.zero, new Vector2(280, 500), TextAlignmentOptions.TopLeft);

            refs["stepText"] = CreateText(root.transform, "StepText", "步骤: --", font, 26, new Vector2(0, 260), new Vector2(500, 40), TextAlignmentOptions.Center);
            var sliderGo = new GameObject("TimerSlider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(root.transform, false);
            var sliderRect = (RectTransform)sliderGo.transform;
            sliderRect.anchorMin = sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(0, 220); sliderRect.sizeDelta = new Vector2(400, 24);
            var sliderBg = CreateImage(sliderRect, "Background", null, new Color(0.2f, 0.2f, 0.2f, 1f));
            Stretch((RectTransform)sliderBg.transform);
            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sliderRect, false);
            Stretch((RectTransform)fillArea.transform);
            var fill = CreateImage((RectTransform)fillArea.transform, "Fill", null, new Color(0.4f, 0.8f, 0.4f, 1f));
            Stretch((RectTransform)fill.transform);
            var slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = (RectTransform)fill.transform;
            slider.minValue = 0; slider.maxValue = 1;
            refs["timerSlider"] = slider;

            refs["toastText"] = CreateText(root.transform, "ToastText", "", font, 20, new Vector2(0, -300), new Vector2(700, 40), TextAlignmentOptions.Center);

            var btnNormal = LoadUiSprite("btn_normal");
            refs["startDayButton"] = CreateButton(root.transform, "StartDayButton", "开始营业", font, btnNormal, new Vector2(-750, -480));
            refs["takeOrderButton"] = CreateButton(root.transform, "TakeOrderButton", "接单", font, btnNormal, new Vector2(-580, -480));
            refs["selectCupButton"] = CreateButton(root.transform, "SelectCupButton", "选杯", font, btnNormal, new Vector2(-410, -480));
            refs["grindButton"] = CreateButton(root.transform, "GrindButton", "研磨", font, btnNormal, new Vector2(-240, -480));
            refs["serveButton"] = CreateButton(root.transform, "ServeButton", "上餐", font, btnNormal, new Vector2(580, -480));
            refs["discardButton"] = CreateButton(root.transform, "DiscardButton", "倒掉", font, LoadUiSprite("btn_danger_normal"), new Vector2(750, -480));
            refs["addCoffeeButton"] = CreateButton(root.transform, "AddCoffeeButton", "咖啡豆", font, btnNormal, new Vector2(-500, -380));
            refs["addMilkButton"] = CreateButton(root.transform, "AddMilkButton", "牛奶", font, btnNormal, new Vector2(-330, -380));
            refs["addMatchaButton"] = CreateButton(root.transform, "AddMatchaButton", "抹茶", font, btnNormal, new Vector2(-160, -380));
            refs["addChocoButton"] = CreateButton(root.transform, "AddChocoButton", "可可", font, btnNormal, new Vector2(10, -380));
            refs["addHoneyButton"] = CreateButton(root.transform, "AddHoneyButton", "蜂蜜", font, btnNormal, new Vector2(180, -380));
            refs["pauseButton"] = CreateButton(root.transform, "PauseButton", "暂停", font, btnNormal, new Vector2(830, -100));
            refs["quickSaveButton"] = CreateButton(root.transform, "QuickSaveButton", "快存", font, btnNormal, new Vector2(830, -160));

            var panel = root.GetComponent<CoffeeGameplayPanel>();
            var so = new SerializedObject(panel);
            foreach (var kv in refs)
            {
                var prop = so.FindProperty(kv.Key);
                if (prop != null) prop.objectReferenceValue = kv.Value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            return img;
        }

        private static TMP_Text CreateText(Transform parent, string name, string content, TMP_FontAsset font, int size, Vector2 pos, Vector2 dimensions, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos; rect.sizeDelta = dimensions;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = content;
            if (font != null) tmp.font = font;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = Color.white;
            return tmp;
        }

        private static Button CreateButton(Transform parent, string name, string label, TMP_FontAsset font, Sprite bg, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos; rect.sizeDelta = new Vector2(150, 60);
            var img = go.GetComponent<Image>();
            img.sprite = bg;
            img.color = Color.white;
            img.type = Image.Type.Sliced;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var text = CreateText(rect, "Label", label, font, 18, Vector2.zero, new Vector2(140, 40), TextAlignmentOptions.Center);
            text.raycastTarget = false;
            return btn;
        }

        private static Sprite LoadArtSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");
        }

        private static Sprite LoadUiSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/Art/UI/Buttons/{name}.png");
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
