using System;
using Spotlight.GameFlow;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>
    /// 在现有主菜单 Prefab 上增量挂接动效与氛围节点；不重建 PSD 版面，可重复执行。
    /// 新增贴图由 Tools/MainMenuArt/generate_menu_motion_art.py 按原画配色程序化生成。
    /// </summary>
    public static class MainMenuMotionInstaller
    {
        private const string ArtRoot = "Assets/Art/Func/MainMenuMotion/";
        private const string SteamPath = "Assets/Resources/Art/Gameplay/DayFlow/DateChange/date_smoke.png";
        private const string BackgroundName = "夜空 副本";
        // PSD 导出的图层名使用不间断空格。
        private const string LogoName = "已插入图像 图像";
        private const string DividerName = "图层 6";
        // Logo 为单张图：第 302/365 行以下是英文副标题，用互补的纵向填充拆成两层，像素不重叠也不留缝。
        private const float TitleFill = 302f / 365f;
        private static readonly string[] ButtonNames = { "SavesButton", "StartButton", "SettingsButton", "QuitButton" };

        [MenuItem("Tools/Game Flow/Install Main Menu Motion")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            ConfigureTextures();
            GameObject root = PrefabUtility.LoadPrefabContents(MainMenuArtworkBuilder.PanelPath);
            try
            {
                Configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, MainMenuArtworkBuilder.PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool mist = path.EndsWith("mist_band.png", StringComparison.Ordinal);
                importer.textureType = mist ? TextureImporterType.Default : TextureImporterType.Sprite;
                if (!mist) importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = mist ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }

        private static void Configure(GameObject root)
        {
            Transform composition = Require(root.transform, "PSDComposition");
            if (composition.GetComponent<RectMask2D>() == null) composition.gameObject.AddComponent<RectMask2D>();

            // 背景与氛围放进独立子 Canvas：常驻漂移只重建这一批网格。
            RectTransform motionRoot = Find(composition, "BackgroundMotion") as RectTransform;
            if (motionRoot == null)
            {
                motionRoot = new GameObject("BackgroundMotion", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup)).GetComponent<RectTransform>();
                motionRoot.SetParent(composition, false);
                Stretch(motionRoot);
                Transform background = Require(composition, BackgroundName);
                motionRoot.SetSiblingIndex(background.GetSiblingIndex());
                background.SetParent(motionRoot, false);
            }
            CanvasGroup backgroundGroup = motionRoot.GetComponent<CanvasGroup>();
            backgroundGroup.interactable = false;
            backgroundGroup.blocksRaycasts = false;

            // 光束 → 云雾 → 飞鸟 → 蒸汽 → 书页 → 浮尘，自远而近叠放在原画之上。
            Image rays = EnsureImage(motionRoot, "LightRays", Sprite("light_rays.png"), new Color(1f, .9f, .75f, 0f));
            Place(rays.rectTransform, new Vector2(0f, .42f), new Vector2(.46f, 1f), new Vector2(0f, 1f));
            var mist = new RawImage[2];
            mist[0] = EnsureRaw(motionRoot, "MistFar", new Color(1f, .97f, .92f, .38f), new Rect(0f, 0f, 1.6f, 1f));
            Place(mist[0].rectTransform, new Vector2(-.02f, .36f), new Vector2(1.02f, .56f), new Vector2(.5f, .5f));
            mist[1] = EnsureRaw(motionRoot, "MistNear", new Color(1f, .96f, .9f, .22f), new Rect(.4f, 0f, 1.1f, 1f));
            Place(mist[1].rectTransform, new Vector2(-.02f, .4f), new Vector2(1.02f, .62f), new Vector2(.5f, .5f));

            var birdFrames = new[] { Sprite("bird_0.png"), Sprite("bird_1.png"), Sprite("bird_2.png") };
            var birds = new Image[4];
            for (int i = 0; i < birds.Length; i++)
            {
                // 原画描线的深棕墨色：飞过米白云团与橙色建筑时都清楚，又和线稿同一语言。
                birds[i] = EnsureImage(motionRoot, "Bird" + i, birdFrames[0], new Color(.23f, .15f, .13f, 0f));
                Point(birds[i].rectTransform, new Vector2(84f, 42f));
            }

            Sprite steamSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SteamPath);
            if (steamSprite == null) throw new InvalidOperationException("Missing steam sprite: " + SteamPath);
            RectTransform steamRoot = Find(motionRoot, "CoffeeSteam") as RectTransform;
            if (steamRoot == null)
            {
                steamRoot = new GameObject("CoffeeSteam", typeof(RectTransform)).GetComponent<RectTransform>();
                steamRoot.SetParent(motionRoot, false);
            }
            // 杯口位于原画约 (0.59, 0.18)；蒸汽区域按比例锚定，适配不同分辨率。
            Place(steamRoot, new Vector2(.53f, .15f), new Vector2(.67f, .54f), new Vector2(.5f, .5f));
            steamRoot.SetAsLastSibling();
            var wisps = new Graphic[4];
            for (int i = 0; i < wisps.Length; i++)
            {
                Image wisp = EnsureImage(steamRoot, "SteamWisp" + i, steamSprite, new Color(1f, .96f, .9f, 0f));
                Stretch(wisp.rectTransform);
                wisps[i] = wisp;
            }

            Image paper = EnsureImage(motionRoot, "PaperSlip", Sprite("paper_slip.png"), new Color(1f, 1f, 1f, 0f));
            Point(paper.rectTransform, new Vector2(150f, 112f));
            paper.transform.SetAsLastSibling();
            var motes = new Graphic[12];
            for (int i = 0; i < motes.Length; i++)
            {
                motes[i] = EnsureImage(motionRoot, "DustMote" + i, Sprite("dust_mote.png"), new Color(1f, .95f, .85f, 0f));
                float size = 9f + (i * 7 % 5) * 2.5f;
                Point(motes[i].rectTransform, new Vector2(size, size));
                motes[i].transform.SetAsLastSibling();
            }

            Image title = Require(composition, LogoName).GetComponent<Image>();
            title.type = Image.Type.Filled;
            title.fillMethod = Image.FillMethod.Vertical;
            title.fillOrigin = (int)Image.OriginVertical.Top;
            title.fillAmount = TitleFill;
            // 用标题自身作模板：扫光只落在笔画上，不会在空白处出现光带。
            Mask titleMask = title.GetComponent<Mask>();
            if (titleMask == null) titleMask = title.gameObject.AddComponent<Mask>();
            titleMask.showMaskGraphic = true;
            Image gleam = EnsureImage(title.transform, "TitleGleam", Sprite("title_gleam.png"), new Color(1f, .97f, .9f, 0f));
            Place(gleam.rectTransform, new Vector2(-.2f, -.1f), new Vector2(0f, 1.1f), new Vector2(.5f, .5f));
            gleam.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -14f);

            Transform subtitleTransform = Find(composition, "EnglishSubtitle");
            Image subtitle;
            if (subtitleTransform == null)
            {
                subtitle = Object.Instantiate(title, composition);
                subtitle.name = "EnglishSubtitle";
                subtitle.transform.SetSiblingIndex(title.transform.GetSiblingIndex() + 1);
            }
            else subtitle = subtitleTransform.GetComponent<Image>();
            // 副标题由标题复制而来：去掉复制过去的模板与扫光，避免重复。
            foreach (Mask extra in subtitle.GetComponents<Mask>()) Object.DestroyImmediate(extra, true);
            for (int i = subtitle.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(subtitle.transform.GetChild(i).gameObject, true);
            subtitle.type = Image.Type.Filled;
            subtitle.fillMethod = Image.FillMethod.Vertical;
            subtitle.fillOrigin = (int)Image.OriginVertical.Bottom;
            subtitle.fillAmount = 1f - TitleFill;

            Image divider = Require(composition, DividerName).GetComponent<Image>();
            divider.type = Image.Type.Filled;
            divider.fillMethod = Image.FillMethod.Horizontal;
            divider.fillOrigin = (int)Image.OriginHorizontal.Left;
            divider.fillAmount = 1f;

            var slots = new CanvasGroup[ButtonNames.Length];
            for (int i = 0; i < ButtonNames.Length; i++)
            {
                Button button = FindButton(composition, ButtonNames[i]);
                slots[i] = EnsureSlot(button);
                ConfigureHover(button);
            }
            Button saves = FindButton(composition, "SavesButton");
            // 继续按钮的叠层贴图已停用；统一使用与其他按钮相同的悬停规则。
            foreach (ContinueGameHoverOverlay overlay in saves.GetComponents<ContinueGameHoverOverlay>())
                Object.DestroyImmediate(overlay, true);

            // 转场纸页铺满整个面板（不受 PSD 比例框限制），默认隐藏。
            RectTransform page = Find(root.transform, "TransitionPage") as RectTransform;
            if (page == null)
            {
                page = new GameObject("TransitionPage", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                page.SetParent(root.transform, false);
            }
            page.SetAsLastSibling();
            Stretch(page);
            Image pageSurface = page.GetComponent<Image>();
            pageSurface.sprite = Sprite("page_sheet.png");
            pageSurface.color = Color.white;
            pageSurface.raycastTarget = true;
            Image shadow = EnsureImage(page, "PageShadow", Sprite("page_shadow.png"), Color.white);
            RectTransform shadowRect = shadow.rectTransform;
            shadowRect.anchorMin = new Vector2(0f, 0f);
            shadowRect.anchorMax = new Vector2(0f, 1f);
            shadowRect.pivot = new Vector2(1f, .5f);
            shadowRect.anchoredPosition = Vector2.zero;
            shadowRect.sizeDelta = new Vector2(80f, 60f);
            page.gameObject.SetActive(false);

            MainMenuAmbience ambience = root.GetComponent<MainMenuAmbience>();
            if (ambience == null) ambience = root.AddComponent<MainMenuAmbience>();
            var ambienceFields = new SerializedObject(ambience);
            ambienceFields.FindProperty("backgroundMotion").objectReferenceValue = motionRoot;
            // 比例值（相对背景尺寸）；显式写入，避免旧版本序列化的像素值残留。
            ambienceFields.FindProperty("drift").vector2Value = new Vector2(.0075f, .0075f);
            ambienceFields.FindProperty("parallax").vector2Value = new Vector2(.0085f, .0085f);
            ambienceFields.FindProperty("lightAlpha").floatValue = .28f;
            ambienceFields.FindProperty("moteAlpha").floatValue = .7f;
            SetArray(ambienceFields.FindProperty("steam"), wisps);
            SetArray(ambienceFields.FindProperty("mist"), mist);
            ambienceFields.FindProperty("lightRays").objectReferenceValue = rays;
            SetArray(ambienceFields.FindProperty("birds"), birds);
            SetArray(ambienceFields.FindProperty("birdFrames"), birdFrames);
            SetArray(ambienceFields.FindProperty("motes"), motes);
            ambienceFields.FindProperty("paperSlip").objectReferenceValue = paper;
            ambienceFields.FindProperty("gleam").objectReferenceValue = gleam;
            ambienceFields.ApplyModifiedPropertiesWithoutUndo();

            MainMenuMotion motion = root.GetComponent<MainMenuMotion>();
            if (motion == null) motion = root.AddComponent<MainMenuMotion>();
            var fields = new SerializedObject(motion);
            fields.FindProperty("background").objectReferenceValue = backgroundGroup;
            fields.FindProperty("backgroundMotion").objectReferenceValue = motionRoot;
            fields.FindProperty("title").objectReferenceValue = title;
            fields.FindProperty("subtitle").objectReferenceValue = subtitle;
            fields.FindProperty("divider").objectReferenceValue = divider;
            fields.FindProperty("ambience").objectReferenceValue = ambience;
            fields.FindProperty("page").objectReferenceValue = page;
            fields.FindProperty("pageSurface").objectReferenceValue = pageSurface;
            // 背景静止时保留 5% 放大量，常驻漂移（≤1.6%）与视差都不会露出边缘。
            fields.FindProperty("backgroundScale").floatValue = 1.05f;
            fields.FindProperty("backgroundStartScale").floatValue = 1.12f;
            SetArray(fields.FindProperty("buttons"), slots);
            fields.ApplyModifiedPropertiesWithoutUndo();

            var panelFields = new SerializedObject(root.GetComponent<MainMenuPanel>());
            panelFields.FindProperty("motion").objectReferenceValue = motion;
            panelFields.ApplyModifiedPropertiesWithoutUndo();
        }

        // 入场位移作用在外层槽位，悬停位移作用在按钮本身，两者不会争抢同一个 anchoredPosition。
        private static CanvasGroup EnsureSlot(Button button)
        {
            var rect = (RectTransform)button.transform;
            Transform parent = rect.parent;
            string slotName = button.name + "Slot";
            if (parent.name != slotName)
            {
                var slot = new GameObject(slotName, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
                slot.SetParent(parent, false);
                slot.SetSiblingIndex(rect.GetSiblingIndex());
                slot.anchorMin = rect.anchorMin;
                slot.anchorMax = rect.anchorMax;
                slot.pivot = rect.pivot;
                slot.anchoredPosition = rect.anchoredPosition;
                slot.sizeDelta = rect.sizeDelta;
                rect.SetParent(slot, false);
                Stretch(rect);
                parent = slot;
            }
            return parent.GetComponent<CanvasGroup>();
        }

        private static void ConfigureHover(Button button)
        {
            Shadow shadow = button.GetComponent<Shadow>();
            if (shadow == null) shadow = button.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(4f, -5f);
            shadow.useGraphicAlpha = true;
            shadow.effectColor = new Color(.16f, .08f, .05f, 0f);
            var feedback = new SerializedObject(button.GetComponent<ButtonClickFeedback>());
            feedback.FindProperty("hoverScale").floatValue = 1.04f;
            feedback.FindProperty("hoverOffset").vector2Value = new Vector2(6f, 0f);
            feedback.FindProperty("hoverShadow").objectReferenceValue = shadow;
            feedback.FindProperty("hoverShadowColor").colorValue = new Color(.16f, .08f, .05f, .45f);
            feedback.FindProperty("hoverDuration").floatValue = .14f;
            feedback.FindProperty("releaseOvershoot").floatValue = 3f;
            feedback.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite Sprite(string file)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + file);
            if (sprite == null) throw new InvalidOperationException("Missing generated sprite: " + ArtRoot + file);
            return sprite;
        }

        private static Image EnsureImage(Transform parent, string name, Sprite sprite, Color color)
        {
            Transform existing = Find(parent, name);
            Image image = existing != null ? existing.GetComponent<Image>()
                : new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RawImage EnsureRaw(Transform parent, string name, Color color, Rect uv)
        {
            Transform existing = Find(parent, name);
            RawImage image = existing != null ? existing.GetComponent<RawImage>()
                : new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(parent, false);
            image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "mist_band.png");
            if (image.texture == null) throw new InvalidOperationException("Missing generated texture: mist_band.png");
            image.color = color;
            image.uvRect = uv;
            image.raycastTarget = false;
            return image;
        }

        private static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        // 点状元素：锚点由运行时脚本逐帧设置，这里只定尺寸。
        private static void Point(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static Button FindButton(Transform composition, string name)
        {
            foreach (Button button in composition.GetComponentsInChildren<Button>(true))
                if (button.name == name) return button;
            throw new InvalidOperationException("Missing main menu button: " + name);
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static Transform Find(Transform parent, string name)
        {
            foreach (Transform child in parent)
                if (child.name == name) return child;
            return null;
        }

        private static Transform Require(Transform parent, string name)
        {
            Transform found = Find(parent, name);
            if (found == null) throw new InvalidOperationException("Missing main menu node: " + name);
            return found;
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
