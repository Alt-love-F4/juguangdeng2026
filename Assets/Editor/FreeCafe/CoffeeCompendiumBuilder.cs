using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>
    /// Builds the 咖啡宝典 prefab (book frame, tabs, motion nodes and all copy) and swaps it in for the old
    /// CoffeeBookPanel in CoffeeShop. Page contents are laid out at runtime from the live CafeWorld.
    /// </summary>
    public static class CoffeeCompendiumBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/FreeCafe/CoffeeCompendiumPanel.prefab";
        private const string ScenePath = "Assets/Scenes/CoffeeShop.unity";
        private const string ArtFolder = "Assets/Art/UI/CoffeeCompendium/";
        private const string FontPath = "Assets/Resources/Fonts/CactusClassicalSerif SDF.asset";
        private static readonly Vector2 BookSize = new Vector2(1494f, 965f);

        [MenuItem("Tools/Free Cafe/Rebuild Coffee Compendium")]
        public static void RebuildAndBind()
        {
            BuildPrefab();
            BindScene();
        }

        [MenuItem("Tools/Free Cafe/Rebuild Coffee Compendium Prefab Only")]
        public static GameObject BuildPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) throw new InvalidOperationException("Missing font " + FontPath);
            ImportSprites();

            var root = new GameObject("CoffeeCompendiumPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(CoffeeCompendiumPanel));
            try
            {
                var rect = (RectTransform)root.transform;
                Stretch(rect);
                var dim = root.GetComponent<Image>();
                dim.color = new Color(.07f, .04f, .025f, .72f);
                dim.raycastTarget = true;
                var panel = root.GetComponent<CoffeeCompendiumPanel>();
                panel.rootGroup = root.GetComponent<CanvasGroup>();
                panel.font = font;

                var book = Node(root.transform, "Book", Vector2.zero, BookSize);
                panel.bookRoot = book;

                // Tabs first so they sit behind the cover; the selected one is moved in front at runtime.
                string[] tabLabels = { "目录", "教程", "订购", "补货" };
                string[] tabNames = { "TabRecipes", "TabTutorials", "TabShop", "TabRestock" };
                for (int i = 0; i < 4; i++)
                {
                    bool left = i < 2;
                    float x = left ? -BookSize.x * .5f - 28f : BookSize.x * .5f + 28f;
                    float y = i % 2 == 0 ? 250f : 110f;
                    var tab = Node(book, tabNames[i], new Vector2(x, y), new Vector2(150f, 96f));
                    var image = tab.gameObject.AddComponent<Image>();
                    image.sprite = Sprite(left ? "tab_idle" : "tab_idle_right");
                    var button = tab.gameObject.AddComponent<Button>();
                    button.targetGraphic = image;
                    var label = Label(tab, tabLabels[i], font, new Vector2(left ? -26f : 26f, 0f), new Vector2(90f, 60f), 30f);
                    label.characterSpacing = 8f;
                    panel.tabButtons[i] = button;
                    panel.tabImages[i] = image;
                    panel.tabLabels[i] = label;
                }

                var cover = Node(book, "Spread", Vector2.zero, BookSize).gameObject.AddComponent<Image>();
                cover.sprite = Sprite("book_spread");
                cover.raycastTarget = true; // swallow clicks so they do not reach the dim layer

                panel.leftPage = Node(book, "LeftPage", new Vector2(-342f, 22f), new Vector2(535f, 750f));
                panel.rightPage = Node(book, "RightPage", new Vector2(344f, 22f), new Vector2(535f, 750f));
                panel.leftGroup = panel.leftPage.gameObject.AddComponent<CanvasGroup>();
                panel.rightGroup = panel.rightPage.gameObject.AddComponent<CanvasGroup>();

                // Turning-page overlays hinge on the spine.
                panel.flipLeft = Node(book, "FlipLeft", new Vector2(725f - 747f, 482.5f - 454.5f), new Vector2(685f, 885f));
                panel.flipLeft.pivot = new Vector2(1f, .5f);
                panel.flipLeft.anchoredPosition = new Vector2(725f - 747f, 482.5f - 454.5f);
                AddImage(panel.flipLeft, Sprite("page_left"));
                panel.flipRight = Node(book, "FlipRight", new Vector2(763f - 747f, 482.5f - 457.5f), new Vector2(675f, 879f));
                panel.flipRight.pivot = new Vector2(0f, .5f);
                panel.flipRight.anchoredPosition = new Vector2(763f - 747f, 482.5f - 457.5f);
                AddImage(panel.flipRight, Sprite("page_right"));
                panel.flipLeft.gameObject.SetActive(false);
                panel.flipRight.gameObject.SetActive(false);

                var close = Node(book, "Close", new Vector2(BookSize.x * .5f - 18f, BookSize.y * .5f - 18f), new Vector2(84f, 84f));
                var closeImage = AddImage(close, Sprite("seal_red"));
                closeImage.raycastTarget = true;
                panel.closeButton = close.gameObject.AddComponent<Button>();
                panel.closeButton.targetGraphic = closeImage;
                Label(close, "×", font, new Vector2(0f, 3f), new Vector2(70f, 70f), 46f).color = new Color(.97f, .9f, .78f);

                var money = Node(book, "Money", new Vector2(0f, -BookSize.y * .5f + 6f), new Vector2(300f, 48f));
                var moneyImage = AddImage(money, Sprite("pill"));
                moneyImage.type = Image.Type.Sliced;
                moneyImage.color = new Color(.25f, .14f, .08f, .96f);
                panel.moneyLabel = Label(money, "王国币  0", font, Vector2.zero, new Vector2(280f, 44f), 24f);
                panel.moneyLabel.color = new Color(.97f, .88f, .66f);

                var toast = Node(book, "Toast", new Vector2(0f, -440f), new Vector2(760f, 50f));
                var toastImage = AddImage(toast, Sprite("pill"));
                toastImage.type = Image.Type.Sliced;
                toastImage.color = new Color(.30f, .10f, .05f, .92f);
                panel.toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
                panel.toastGroup.alpha = 0f;
                panel.toastGroup.blocksRaycasts = false;
                panel.toastLabel = Label(toast, "", font, Vector2.zero, new Vector2(730f, 44f), 21f);
                panel.toastLabel.color = new Color(.98f, .92f, .8f);

                panel.rowCard = Sprite("row_card");
                panel.couponCard = Sprite("coupon_card");
                panel.sealRed = Sprite("seal_red");
                panel.sealGray = Sprite("seal_gray");
                panel.divider = Sprite("divider");
                panel.arrow = Sprite("arrow");
                panel.lockIcon = Sprite("lock");
                panel.pill = Sprite("pill");
                panel.stampRing = Sprite("stamp_ring");
                panel.ornament = Sprite("ornament_branch");
                panel.tabIdleLeft = Sprite("tab_idle");
                panel.tabSelectedLeft = Sprite("tab_selected");
                panel.tabIdleRight = Sprite("tab_idle_right");
                panel.tabSelectedRight = Sprite("tab_selected_right");
                WriteCopy(panel);

                root.SetActive(false);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[FreeCafe] Built " + PrefabPath);
                return prefab;
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ------------------------------------------------------------------ copy

        private static void WriteCopy(CoffeeCompendiumPanel panel)
        {
            panel.recipeOrder = new[] { "blackcoffee", "cold_brew", "milkcoffee", "creamcoffee", "mocha", "cocoa", "matchalatte", "espresso", "macchiato", "latte", "honeylatte" };
            panel.recipes = new[]
            {
                Recipe("blackcoffee", "巷口清晨的第一杯",
                    "粗粝、微苦、干脆利落。码头工人与报童最常点的就是它——一枚铜币，换一整个上午的清醒。",
                    "加糖等额外原料会让它变成「迷之饮品」。",
                    "在磨豆机中研磨咖啡豆，得到咖啡粉。",
                    "把咖啡粉与清水倒进同一个容器，混合成冷萃。",
                    "放上炉台并点击加热，到温热或沸腾即成黑咖啡。",
                    "倒入小杯，放上出餐口按铃。"),
                Recipe("cold_brew", "不必生火的耐心之作",
                    "咖啡粉与清水静静相会，不经炉火。口感圆润，最合夏日午后与不愿久等的客人。",
                    "加热后会变成黑咖啡；冷却下来又会回到冷萃。",
                    "研磨咖啡豆，得到咖啡粉。",
                    "把咖啡粉与清水在容器中混合，常温即为冷萃。",
                    "不要加热，直接倒入小杯出品。"),
                Recipe("milkcoffee", "黑咖啡遇上一壶热牛奶",
                    "比黑咖啡温柔，比拿铁朴素。老主顾们说，这是能喝出家里味道的一杯。",
                    "牛奶一定要热；这杯不需要拉花。",
                    "按黑咖啡的做法，准备好黑咖啡。",
                    "把牛奶加热到温热以上。",
                    "将热牛奶与黑咖啡一同倒入大杯。"),
                Recipe("creamcoffee", "一朵奶油，压住苦味",
                    "在热黑咖啡上放一勺浓奶油，看它慢慢化开。剧院散场后的绅士们偏爱这一杯。",
                    "不要加牛奶或拉花，否则就认不出它了。",
                    "准备好黑咖啡，倒入大杯。",
                    "从奶油罐取出奶油顶，加到咖啡上。"),
                Recipe("mocha", "可可与咖啡的甜蜜同盟",
                    "黑咖啡里化开可可粉，再盖上一层奶油。孩子们眼馋，大人们也舍不得说不。",
                    "摩卡不需要牛奶——加了反而认不出来。",
                    "准备好黑咖啡，倒入大杯。",
                    "加入可可粉。",
                    "放上奶油顶，即可出品。"),
                Recipe("cocoa", "冬夜里捧在手心的暖意",
                    "不含一滴咖啡，却是店里最受欢迎的冬季饮品。可可的香气能让整条街的人回头。",
                    "热不热看温度，不看原料——别忘了加热。",
                    "把可可粉与清水混合成可可液。",
                    "加入牛奶，与可可液混合。",
                    "放上炉台加热，再倒入大杯出品。"),
                Recipe("matchalatte", "远方来的翠绿访客",
                    "商船从远方带来的抹茶粉，与牛奶相遇后清香细腻，是贵妇人下午茶的新宠。",
                    "少了牛奶或混入其他原料，都会变成「迷之饮品」。",
                    "把抹茶粉与清水混合成抹茶液。",
                    "加入热牛奶混合。",
                    "倒入大杯出品。"),
                Recipe("espresso", "七秒半的手艺",
                    "以精密萃取滤杯计时注水。时间、水量与均匀，差一分都瞒不过行家的舌头。",
                    "加热普通冷萃只会得到黑咖啡，代替不了精粹。",
                    "研磨咖啡粉，装入精密萃取滤杯。",
                    "用热水计时注入：约 7.5 秒、200 ml，保持均匀。",
                    "停止注水结算后，才得到精粹咖啡。",
                    "倒入小杯出品。"),
                Recipe("macchiato", "精粹之上的一点白",
                    "在精粹咖啡上点缀一层拉花。浓烈与绵柔之间，只隔着薄薄一层白。",
                    "热牛奶代替不了拉花；图案随你发挥。",
                    "萃取精粹咖啡，倒入小杯。",
                    "向拉花杯加入牛奶并打奶。",
                    "把拉花杯拖到杯子上，完成拉花。"),
                Recipe("latte", "杯面上的一颗心",
                    "精粹、热牛奶与拉花，三者缺一不可。店里的招牌，也是检验咖啡师手艺的试金石。",
                    "图案不限，但一定要完成拉花才算拿铁。",
                    "萃取精粹咖啡，倒入大杯。",
                    "加入热牛奶。",
                    "向拉花杯加入牛奶并打奶，再拖到杯上完成拉花。"),
                Recipe("honeylatte", "一勺蜂蜜，一日好心情",
                    "在拿铁里调进蜂蜜，甜得恰到好处。花市的姑娘们每周都会来点上一杯。",
                    "蜂蜜必不可少，少了它就只是普通拿铁。",
                    "萃取精粹咖啡，加入热牛奶。",
                    "加入一份蜂蜜。",
                    "向拉花杯打奶，再拖到杯上完成拉花。"),
            };
            panel.practices = new[]
            {
                new CoffeeCompendiumPanel.PracticeCopy { extraction = false, title = "黑咖啡 · 基础练习", icon = Sprite("drink_blackcoffee"),
                    description = "跟着指引走一遍：研磨咖啡豆、粉水混合成冷萃、上炉台加热，最后装杯按铃出品。" },
                new CoffeeCompendiumPanel.PracticeCopy { extraction = true, title = "精粹咖啡 · 萃取练习", icon = Sprite("drink_espresso"),
                    description = "学习精密萃取：咖啡粉装入滤杯，以热水计时注入，体会时间、水量与均匀。" },
            };
            panel.shopItems = new[]
            {
                new CoffeeCompendiumPanel.ShopItemCopy { latte = false, title = "精粹萃取组", subtitle = "精密萃取壶  ·  专用滤杯",
                    pitch = "以计时注水萃取出浓郁的精粹咖啡。从此精粹咖啡可以入单，也为日后的拿铁打下底子。",
                    artIds = new[] { "container.extractor", "container.extractor.filter" } },
                new CoffeeCompendiumPanel.ShopItemCopy { latte = true, title = "拉花套装", subtitle = "拉花杯  ·  打奶搅拌器",
                    pitch = "打出绵密的奶，在杯面画出心形与叶片。配合精粹萃取组，拿铁、玛奇朵、蜂蜜拿铁皆可上单。",
                    artIds = new[] { "container.milk-pitcher", "machine.mixer" } },
            };
            panel.counterGuide = new[]
            {
                "拖拽原料与容器即可操作：豆子拖进磨豆机，粉和水倒进同一个容器。",
                "咖啡粉与清水混合即是冷萃；放上炉台并点击加热，就成了黑咖啡。",
                "牛奶倒进拉花杯打奶，再拖到饮品杯上完成拉花。",
                "饮品倒入对应杯型，放到出餐口，按铃交给客人。",
                "评价只看最终成分与用量，不看加料顺序；多放少放都会影响评分。",
                "做坏了也别慌，倒进废弃桶重来就好。",
            };
            panel.catalogIntro = "这本手记记录着店里每一杯咖啡的做法。已掌握的配方随时可以照做；尚未解锁的，也不妨先认认它们的模样。";
            panel.tutorialIntro = "吧台练习会一步步提示操作。练习不消耗店里的存货，结束后台面恢复原样，随时可以重来。";
            panel.shopMasthead = "敬启者：本行承办王都各大咖啡馆之器具，货真价实，下单当日即送抵吧台。以下两件乃镇店之宝，诚邀阁下订购。";
            panel.shopFinePrint = "· 营业前或打烊后办理订购，营业中恕不接单。\n· 须累计足够的成功出品方可订购，以免糟蹋好货。\n· 货款以王国币当面结清，概不赊账。\n· 器具到店即可在吧台使用，相应配方随之收录于本宝典。";
            panel.restockIntro = "「补满」会把货架补到满额，只按缺少的份数计价。好豆子单价更高，做出的咖啡也能卖出更好的价钱。";
        }

        private static CoffeeCompendiumPanel.RecipeCopy Recipe(string id, string tagline, string story, string tip, params string[] steps)
            => new CoffeeCompendiumPanel.RecipeCopy { productId = id, icon = Sprite("drink_" + id), tagline = tagline, story = story, tip = tip, steps = steps };

        // ------------------------------------------------------------------ scene binding

        [MenuItem("Tools/Free Cafe/Bind Coffee Compendium In CoffeeShop")]
        public static void BindScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? BuildPrefab();
            var scene = EditorSceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                int bound = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var hud in root.GetComponentsInChildren<FreeCafeHud>(true)) { BindPlayer(hud, prefab); bound++; }
                    foreach (var hud in root.GetComponentsInChildren<FreeCafeProductionHud>(true)) { BindProduction(hud, prefab); bound++; }
                }
                if (bound == 0) throw new InvalidOperationException("CoffeeShop has no FreeCafeHud / FreeCafeProductionHud to bind.");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[FreeCafe] Coffee compendium bound to " + bound + " HUD(s) in CoffeeShop.");
            }
            finally
            {
                if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static GameObject Replace(GameObject previous, Transform fallbackParent, GameObject prefab, FreeCafeRuntime runtime)
        {
            if (previous != null && previous.GetComponent<CoffeeCompendiumPanel>() != null
                && PrefabUtility.GetCorrespondingObjectFromSource(previous) == prefab)
            {
                previous.GetComponent<CoffeeCompendiumPanel>().runtime = runtime;
                previous.SetActive(false);
                return previous;
            }
            var parent = previous != null && previous.transform.parent != null ? previous.transform.parent : fallbackParent;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = "CoffeeCompendiumPanel";
            instance.transform.SetAsLastSibling();
            Stretch((RectTransform)instance.transform);
            instance.GetComponent<CoffeeCompendiumPanel>().runtime = runtime;
            instance.SetActive(false);
            if (previous != null) Object.DestroyImmediate(previous);
            return instance;
        }

        private static void BindPlayer(FreeCafeHud hud, GameObject prefab)
        {
            var parent = hud.playerRoot != null ? hud.playerRoot.transform : hud.transform;
            hud.recipePanel = Replace(hud.recipePanel, parent, prefab, hud.runtime);
            hud.recipeTitle = null;
            hud.recipeBody = null;
            hud.recipeCloseButton = null;
            EditorUtility.SetDirty(hud);
        }

        public static void BindProduction(FreeCafeProductionHud hud, GameObject prefab = null)
        {
            prefab = prefab != null ? prefab : AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? BuildPrefab();
            hud.bookPanel = Replace(hud.bookPanel, hud.transform, prefab, hud.runtime);
            hud.bookTitle = hud.bookBody = hud.bookNote = null;
            hud.previousRecipe = hud.nextRecipe = hud.closeBook = null;
            hud.bookIcon = null;
            EditorUtility.SetDirty(hud);
        }

        // ------------------------------------------------------------------ helpers

        private static void ImportSprites()
        {
            var borders = new Dictionary<string, Vector4>
            {
                { "pill", new Vector4(11, 11, 11, 11) },
                { "row_card", new Vector4(18, 18, 18, 18) },
                { "coupon_card", new Vector4(26, 26, 26, 26) },
            };
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("~")) continue;
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                Vector4 border = borders.TryGetValue(name, out var b) ? b : Vector4.zero;
                bool dirty = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
                    || importer.mipmapEnabled || importer.spriteBorder != border || !importer.alphaIsTransparency;
                if (!dirty) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.spriteBorder = border;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        private static Sprite Sprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + name + ".png");
            if (sprite == null) throw new InvalidOperationException("Missing compendium sprite " + ArtFolder + name + ".png");
            return sprite;
        }

        private static RectTransform Node(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image AddImage(RectTransform rect, Sprite sprite)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Label(Transform parent, string value, TMP_FontAsset font, Vector2 position, Vector2 size, float fontSize)
        {
            var rect = Node(parent, "Label", position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(.42f, .30f, .20f);
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(.5f, .5f);
        }
    }
}
