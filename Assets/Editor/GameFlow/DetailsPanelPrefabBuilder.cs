using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>Builds only the missing display-only prefab; never overwrites an artist-edited asset.</summary>
    public static class DetailsPanelPrefabBuilder
    {
        public const string PrefabPath = "Assets/Resources/Art/UIPrefab/DetailsPanel.prefab";
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/SimHei SDF.asset");
        private static readonly Color Surface = new Color(.12f, .10f, .16f, 1f);
        private static readonly Color Card = new Color(.18f, .14f, .21f, 1f);
        private static readonly Color Warm = new Color(1f, .86f, .70f, 1f);
        private static readonly Color Muted = new Color(.74f, .70f, .77f, 1f);
        private static readonly Color Pink = new Color(.96f, .66f, .68f, 1f);

        [MenuItem("Tools/Game Flow/Create Missing Details Panel")]
        public static void CreateMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != default) return;
            if (Font == default) throw new InvalidOperationException("Missing SimHei SDF font.");
            GameObject root = new GameObject("DetailsPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Canvas), typeof(GraphicRaycaster));
            try
            {
                Rect(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 800;
                DetailsPanel panel = root.AddComponent<DetailsPanel>();
                Button outside = Btn(root.transform, "OutsideClose", string.Empty, Vector2.zero, Vector2.one);
                outside.GetComponent<Image>().color = new Color(.02f, .015f, .03f, .72f);
                outside.transition = Selectable.Transition.None;
                // The window is a sibling above the full-screen button; all interior gaps block outside dismissal.
                Image frame = Box(root.transform, "Window", new Vector2(.13f, .12f), new Vector2(.87f, .88f), Surface);
                RectTransform window = frame.rectTransform;
                Box(window, "TopAccent", new Vector2(0f, .993f), Vector2.one, Pink);
                Txt(window, "Title", "详情", new Vector2(.035f, .885f), new Vector2(.25f, .975f), 42f, Warm);
                Txt(window, "Subtitle", "馆长档案 / 展示原型", new Vector2(.28f, .90f), new Vector2(.74f, .955f), 20f, Muted);
                Button close = Btn(window, "CloseButton", "关闭  ×", new Vector2(.85f, .895f), new Vector2(.965f, .963f));
                string[] titles = { "我的信息", "人物关系", "世界情况" };
                var tabs = new Button[3];
                for (int i = 0; i < 3; i++)
                {
                    float left = .035f + i * .31f;
                    tabs[i] = Btn(window, "Tab_" + i, titles[i], new Vector2(left, .793f), new Vector2(left + .30f, .867f));
                }
                Box(window, "TabDivider", new Vector2(.035f, .777f), new Vector2(.965f, .779f), new Color(.35f, .26f, .32f));
                var pages = new GameObject[3];
                for (int i = 0; i < 3; i++)
                {
                    pages[i] = Child(window, ((DetailsPanel.Page)i).ToString());
                    Rect(pages[i].GetComponent<RectTransform>(), new Vector2(.035f, .07f), new Vector2(.965f, .755f));
                }
                var preview = DetailsPanelData.CreatePlaceholder(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/UI/Icons/ic_star.png"));
                BuildMyInfo(pages[0].transform, panel, preview);
                BuildEmptyPage(pages[1].transform, preview.Relationships);
                BuildEmptyPage(pages[2].transform, preview.World);
                Txt(window, "Footer", "全部内容均为占位样例 · 悬停查看说明 · 点击窗口外或按 Esc 关闭", new Vector2(.035f, .015f), new Vector2(.965f, .055f), 18f, Muted);

                Image tip = Box(window, "Tooltip", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Color(.26f, .18f, .25f, .99f));
                tip.rectTransform.pivot = Vector2.zero;
                tip.rectTransform.sizeDelta = new Vector2(380f, 150f);
                tip.raycastTarget = false;
                tip.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
                TMP_Text tipTitle = Txt(tip.transform, "Title", string.Empty, new Vector2(.05f, .66f), new Vector2(.95f, .94f), 25f, Warm);
                TMP_Text tipBody = Txt(tip.transform, "Body", string.Empty, new Vector2(.05f, .08f), new Vector2(.95f, .62f), 21f, Color.white);
                tipBody.alignment = TextAlignmentOptions.TopLeft;
                tip.gameObject.SetActive(false);

                var data = new SerializedObject(panel);
                Bind(data, "closeButton", close);
                Bind(data, "outsideButton", outside);
                Bind(data, "window", window);
                Bind(data, "tooltip", tip.rectTransform);
                Bind(data, "tooltipTitle", tipTitle);
                Bind(data, "tooltipBody", tipBody);
                SerializedProperty buttons = data.FindProperty("tabButtons");
                SerializedProperty roots = data.FindProperty("pages");
                buttons.arraySize = roots.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    buttons.GetArrayElementAtIndex(i).objectReferenceValue = tabs[i];
                    roots.GetArrayElementAtIndex(i).objectReferenceValue = pages[i];
                    pages[i].SetActive(i == 0);
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                BindContentComponents(panel);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[DetailsPanel] Display-only placeholder prefab created: " + PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void BuildMyInfo(Transform parent, DetailsPanel panel, DetailsPanelData preview)
        {
            Image profile = Box(parent, "ProfileCard", new Vector2(0f, .59f), Vector2.one, Card);
            Image avatar = Box(profile.transform, "AvatarFrame", new Vector2(.025f, .10f), new Vector2(.185f, .90f), new Color(.29f, .23f, .30f));
            avatar.gameObject.AddComponent<RectMask2D>();
            GameObject silhouette = Child(avatar.transform, "AvatarSilhouette", typeof(CanvasRenderer), typeof(DetailsAvatarPlaceholder));
            Rect(silhouette.GetComponent<RectTransform>(), new Vector2(.1f, .20f), new Vector2(.9f, .95f));
            DetailsAvatarPlaceholder graphic = silhouette.GetComponent<DetailsAvatarPlaceholder>();
            graphic.color = new Color(.07f, .055f, .10f);
            graphic.raycastTarget = false;
            Txt(avatar.transform, "PlaceholderLabel", "头像占位", new Vector2(.05f, .03f), new Vector2(.95f, .20f), 17f, Muted, TextAlignmentOptions.Center);
            Txt(profile.transform, "Name", preview.Profile.Name, new Vector2(.215f, .72f), new Vector2(.965f, .93f), 30f, Warm);
            Txt(profile.transform, "Background", preview.Profile.Background, new Vector2(.215f, .34f), new Vector2(.965f, .70f), 23f, Color.white);
            Txt(profile.transform, "CafeIntro", preview.Profile.Introduction, new Vector2(.215f, .10f), new Vector2(.965f, .31f), 22f, Muted);

            Txt(parent, "AttributesHeading", "主角属性", new Vector2(0f, .505f), new Vector2(.4f, .575f), 27f, Warm);
            Txt(parent, "AttributesHint", "0—20 · 悬停查看属性作用", new Vector2(.53f, .505f), new Vector2(1f, .575f), 18f, Muted, TextAlignmentOptions.MidlineRight);
            for (int i = 0; i < preview.Attributes.Count; i++)
            {
                DetailsAttributeData attribute = preview.Attributes[i];
                float left = i * .255f;
                Image row = Box(parent, "Attribute_" + i, new Vector2(left, .36f), new Vector2(left + .235f, .495f), Card);
                Txt(row.transform, "Name", attribute.Name, new Vector2(.08f, .36f), new Vector2(.5f, .94f), 25f, Color.white);
                int value = attribute.Value;
                Txt(row.transform, "Value", value + " / 20", new Vector2(.52f, .36f), new Vector2(.92f, .94f), 25f, Pink, TextAlignmentOptions.MidlineRight);
                Image track = Box(row.transform, "Track", new Vector2(.08f, .17f), new Vector2(.92f, .25f), new Color(.08f, .06f, .11f));
                Box(track.transform, "Fill", Vector2.zero, new Vector2(value / 20f, 1f), Pink).raycastTarget = false;
                Hover(row.gameObject, panel, attribute.Name, attribute.Description);
            }

            Txt(parent, "SpecializationsHeading", "主角专精", new Vector2(0f, .275f), new Vector2(.4f, .345f), 27f, Warm);
            Txt(parent, "SpecializationsHint", "徽章墙 · 以下专精均为展示样例", new Vector2(.5f, .275f), new Vector2(1f, .345f), 18f, Muted, TextAlignmentOptions.MidlineRight);
            for (int i = 0; i < preview.Specializations.Count; i++)
            {
                DetailsSpecializationData specialization = preview.Specializations[i];
                int column = i % 3;
                int row = i / 3;
                float left = column * .34f;
                float bottom = .145f - row * .135f;
                Image badge = Box(parent, "Specialization_" + i, new Vector2(left, bottom), new Vector2(left + .32f, bottom + .12f), Card);
                Image emblem = Box(badge.transform, "Icon", new Vector2(.045f, .18f), new Vector2(.18f, .82f), Warm);
                emblem.sprite = specialization.Icon;
                emblem.preserveAspect = true;
                emblem.raycastTarget = false;
                if (specialization.Icon == default) Txt(emblem.transform, "FallbackIcon", "★", Vector2.zero, Vector2.one, 25f, Warm, TextAlignmentOptions.Center);
                Txt(badge.transform, "Name", specialization.Name, new Vector2(.23f, .12f), new Vector2(.94f, .9f), 23f, Color.white);
                Hover(badge.gameObject, panel, specialization.Name, specialization.Description);
            }
        }

        private static void BuildEmptyPage(Transform page, DetailsSectionData data)
        {
            Box(page, "PlaceholderCard", new Vector2(0f, .08f), new Vector2(1f, .94f), Card);
            Txt(page, "Title", data.Title, new Vector2(.07f, .70f), new Vector2(.93f, .84f), 30f, Warm);
            Txt(page, "Heading", data.Heading, new Vector2(.07f, .44f), new Vector2(.93f, .66f), 38f, Pink, TextAlignmentOptions.Center);
            Txt(page, "Description", data.Description, new Vector2(.12f, .23f), new Vector2(.88f, .43f), 25f, Muted, TextAlignmentOptions.Center);
        }

        /// <summary>Non-destructive migration: keep existing components/file IDs and add only missing views/bindings.</summary>
        [MenuItem("Tools/Game Flow/Bind Details Panel Components")]
        public static void BindExisting()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            CreateMissing();
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                BindContentComponents(root.GetComponent<DetailsPanel>());
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void BindContentComponents(DetailsPanel panel)
        {
            Transform window = panel.transform.Find("Window");
            Transform page = window.Find("MyInfo");
            Transform profile = page.Find("ProfileCard");
            Transform avatar = profile.Find("AvatarFrame");
            Transform avatarView = avatar.Find("AvatarImage");
            if (avatarView == default)
            {
                Image image = Box(avatar, "AvatarImage", new Vector2(.05f, .05f), new Vector2(.95f, .95f), Color.white);
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.gameObject.SetActive(false);
                avatarView = image.transform;
            }
            Transform badgeArea = page.Find("Specializations");
            if (badgeArea == default)
            {
                badgeArea = Child(page, "Specializations", typeof(Image), typeof(RectMask2D), typeof(ScrollRect)).transform;
                Rect((RectTransform)badgeArea, new Vector2(0f, .01f), new Vector2(1f, .265f));
                badgeArea.GetComponent<Image>().color = Color.clear;
                Transform content = Child(badgeArea, "Content", typeof(GridLayoutGroup), typeof(ContentSizeFitter)).transform;
                RectTransform contentRect = (RectTransform)content;
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = Vector2.one;
                contentRect.pivot = new Vector2(.5f, 1f);
                contentRect.anchoredPosition = Vector2.zero;
                contentRect.sizeDelta = Vector2.zero;
                GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 3;
                grid.cellSize = new Vector2(400f, 64f);
                grid.spacing = new Vector2(24f, 10f);
                content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                ScrollRect scroll = badgeArea.GetComponent<ScrollRect>();
                scroll.content = contentRect;
                scroll.viewport = (RectTransform)badgeArea;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 26f;
                // Existing badge objects and every label/icon/hover component remain the same objects.
                for (int i = 0; i < 6; i++) page.Find("Specialization_" + i).SetParent(content, false);
                TMP_Text empty = Txt(badgeArea, "Empty", "暂无专精", new Vector2(.05f, .1f), new Vector2(.95f, .9f), 23f, Muted, TextAlignmentOptions.Center);
                empty.gameObject.SetActive(false);
            }
            Transform badges = badgeArea.Find("Content");
            var data = new SerializedObject(panel);
            Bind(data, "playerNameText", profile.Find("Name").GetComponent<TMP_Text>());
            Bind(data, "backgroundText", profile.Find("Background").GetComponent<TMP_Text>());
            Bind(data, "introductionText", profile.Find("CafeIntro").GetComponent<TMP_Text>());
            Bind(data, "avatarImage", avatarView.GetComponent<Image>());
            Bind(data, "avatarPlaceholder", avatar.Find("AvatarSilhouette").gameObject);
            Bind(data, "avatarPlaceholderLabel", avatar.Find("PlaceholderLabel").GetComponent<TMP_Text>());
            Bind(data, "subtitleText", window.Find("Subtitle").GetComponent<TMP_Text>());
            Bind(data, "footerText", window.Find("Footer").GetComponent<TMP_Text>());
            Bind(data, "specializationContent", (RectTransform)badges);
            Bind(data, "specializationViewport", (RectTransform)badgeArea);
            Bind(data, "specializationGrid", badges.GetComponent<GridLayoutGroup>());
            Bind(data, "specializationScroll", badgeArea.GetComponent<ScrollRect>());
            Bind(data, "emptySpecializationsText", badgeArea.Find("Empty").GetComponent<TMP_Text>());
            Bind(data, "specializationsHintText", page.Find("SpecializationsHint").GetComponent<TMP_Text>());
            Bind(data, "placeholderSpecializationIcon", badges.Find("Specialization_0/Icon").GetComponent<Image>().sprite);
            SerializedProperty attributes = data.FindProperty("attributeBindings");
            attributes.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                Transform row = page.Find("Attribute_" + i);
                SerializedProperty binding = attributes.GetArrayElementAtIndex(i);
                binding.FindPropertyRelative("nameText").objectReferenceValue = row.Find("Name").GetComponent<TMP_Text>();
                binding.FindPropertyRelative("valueText").objectReferenceValue = row.Find("Value").GetComponent<TMP_Text>();
                binding.FindPropertyRelative("fill").objectReferenceValue = (RectTransform)row.Find("Track/Fill");
                binding.FindPropertyRelative("hover").objectReferenceValue = row.GetComponent<DetailsHoverTarget>();
            }
            BindSection(data.FindProperty("relationshipsBinding"), window.Find("Relationships"));
            BindSection(data.FindProperty("worldBinding"), window.Find("World"));
            SerializedProperty items = data.FindProperty("specializationItems");
            items.arraySize = 6;
            for (int i = 0; i < 6; i++)
            {
                Transform row = badges.Find("Specialization_" + i);
                DetailsSpecializationItem item = row.GetComponent<DetailsSpecializationItem>();
                if (item == default) item = row.gameObject.AddComponent<DetailsSpecializationItem>();
                var itemData = new SerializedObject(item);
                Bind(itemData, "icon", row.Find("Icon").GetComponent<Image>());
                Bind(itemData, "nameText", row.Find("Name").GetComponent<TMP_Text>());
                Bind(itemData, "hover", row.GetComponent<DetailsHoverTarget>());
                Transform fallback = row.Find("Icon/FallbackIcon");
                if (fallback == default) fallback = Txt(row.Find("Icon"), "FallbackIcon", "★", Vector2.zero, Vector2.one, 25f, Warm, TextAlignmentOptions.Center).transform;
                fallback.gameObject.SetActive(false);
                Bind(itemData, "fallbackIcon", fallback.GetComponent<TMP_Text>());
                itemData.ApplyModifiedPropertiesWithoutUndo();
                items.GetArrayElementAtIndex(i).objectReferenceValue = item;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindSection(SerializedProperty binding, Transform page)
        {
            binding.FindPropertyRelative("title").objectReferenceValue = page.Find("Title").GetComponent<TMP_Text>();
            binding.FindPropertyRelative("heading").objectReferenceValue = page.Find("Heading").GetComponent<TMP_Text>();
            binding.FindPropertyRelative("description").objectReferenceValue = page.Find("Description").GetComponent<TMP_Text>();
        }

        private static void Hover(GameObject target, DetailsPanel panel, string title, string description)
        {
            var data = new SerializedObject(target.AddComponent<DetailsHoverTarget>());
            Bind(data, "panel", panel);
            data.FindProperty("title").stringValue = title;
            data.FindProperty("description").stringValue = description;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Bind(SerializedObject data, string name, Object value) => data.FindProperty(name).objectReferenceValue = value;
        private static GameObject Child(Transform parent, string name, params Type[] types)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.layer = LayerMask.NameToLayer("UI");
            child.transform.SetParent(parent, false);
            foreach (Type type in types) child.AddComponent(type);
            return child;
        }
        private static void Rect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static Image Box(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            Image image = Child(parent, name, typeof(Image)).GetComponent<Image>();
            Rect(image.rectTransform, min, max);
            image.color = color;
            return image;
        }
        private static TMP_Text Txt(Transform parent, string name, string text, Vector2 min, Vector2 max, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var label = Child(parent, name, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            Rect(label.rectTransform, min, max);
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Min(16f, size);
            label.fontSizeMax = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }
        private static Button Btn(Transform parent, string name, string text, Vector2 min, Vector2 max)
        {
            Image image = Box(parent, name, min, max, Color.white);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(.29f, .20f, .28f);
            colors.highlightedColor = new Color(.42f, .28f, .36f);
            colors.pressedColor = new Color(.55f, .34f, .38f);
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            // Disable navigation so a tab click cannot implicitly select an unrelated button.
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (!string.IsNullOrEmpty(text)) Txt(button.transform, "Label", text, new Vector2(.04f, .05f), new Vector2(.96f, .95f), 26f, Warm, TextAlignmentOptions.Center);
            return button;
        }
    }
}
