using System;
using System.Linq;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>Converts the artist's 4096x2304 PSD composition into a 2048x1152 UGUI composition.</summary>
    public static class DetailsPanelPrefabBuilder
    {
        public const string PrefabPath = "Assets/Resources/Art/UIPrefab/DetailsPanel.prefab";
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Map/SourceHanSerifCN-Heavy-4 SDF.asset");
        private static TMP_FontAsset BodyFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Map/SourceHanSansSC-Regular-2 SDF.asset");
        private static readonly Color Ink = new Color(.35f, .19f, .07f);

        [MenuItem("Tools/Game Flow/Create Missing Details Panel")]
        public static void CreateMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == default) Rebuild();
        }

        [MenuItem("Tools/Game Flow/Bind Details Panel Components")]
        public static void BindExisting()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == default || prefab.GetComponent<DetailsPagesView>() == default) Rebuild();
            else if (prefab.transform.Find("Window/SharedFrame") == default) MergeExistingSharedFrame();
        }

        [MenuItem("Tools/Game Flow/Apply Formal Details Art")]
        public static void ApplyFormalArtToPrefab() => Rebuild();

        [MenuItem("Tools/Game Flow/Rebuild Details From PSD")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            ImportDerivedSprite("Assets/Art/Func/UGUI/WorldIdleButton.png");
            var root = new GameObject("DetailsPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
            try
            {
                Stretch((RectTransform)root.transform);
                var canvas = root.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 800;
                var panel = root.AddComponent<DetailsPanel>();
                var data = new SerializedObject(panel);
                var outside = Image(root.transform, "OutsideClose", 0, 0, 2048, 1152, null);
                outside.color = Color.clear;
                outside.raycastTarget = true;
                Stretch(outside.rectTransform);
                Set(data, "outsideButton", Button(outside));
                var window = Rect(root.transform, "Window", 0, 0, 2048, 1152);
                window.anchorMin = window.anchorMax = new Vector2(.5f, .5f);
                window.anchoredPosition = Vector2.zero;
                window.gameObject.AddComponent<DetailsCompositionFit>();
                Set(data, "window", window);
                var shield = Image(window, "WindowShield", 273, 166, 1549, 828, null);
                shield.color = Color.clear;
                shield.raycastTarget = true;
                var names = new[] { "我的信息", "人物信息", "世界情况" };
                var pages = new RectTransform[3];
                for (int i = 0; i < 3; i++)
                {
                    pages[i] = Rect(window, ((DetailsPanel.Page)i).ToString(), 0, 0, 2048, 1152);
                    ConvertPsd(pages[i], names[i]);
                    TabLabels(pages[i]);
                    data.FindProperty("pages").arraySize = 3;
                    data.FindProperty("pages").GetArrayElementAtIndex(i).objectReferenceValue = pages[i].gameObject;
                }
                var tabs = data.FindProperty("tabButtons"); tabs.arraySize = 3;
                // API order remains MyInfo/Relationships/World; visual order follows the PSD.
                int[] x = { 653, 528, 777 };
                for (int i = 0; i < 3; i++)
                {
                    var hit = Image(window, "Tab_" + i, x[i], 135, 119, 105, null);
                    hit.color = Color.clear; hit.raycastTarget = true;
                    tabs.GetArrayElementAtIndex(i).objectReferenceValue = Button(hit);
                }
                var close = Image(window, "CloseButton", 1700, 197, 86, 119, null);
                close.color = Color.clear; close.raycastTarget = true;
                Set(data, "closeButton", Button(close));
                BuildMyInfo(pages[0], panel, data);
                BuildRelationships(pages[1], data);
                BuildWorld(pages[2], data);
                BuildTooltip(window, data);
                BindPagesView(root);
                // Old informational labels remain bound but do not add text outside the PSD composition.
                Set(data, "subtitleText", HiddenText(window, "Subtitle"));
                Set(data, "footerText", HiddenText(window, "Footer"));
                Set(data, "unknownCharacterAvatar", Sprite("人物信息", "问号"));
                data.ApplyModifiedPropertiesWithoutUndo();
                MergeSharedFrame(root);
                pages[1].gameObject.SetActive(false); pages[2].gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("DetailsPanel rebuilt from the three PSD prefabs, preserving source layer coordinates and draw order.");
            }
            finally { Object.DestroyImmediate(root); }
        }


        [MenuItem("Tools/Game Flow/Merge Details Shared Frame")]
        public static void MergeExistingSharedFrame()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (root.transform.Find("Window/SharedFrame") != default) return;
                MergeSharedFrame(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("DetailsPanel now shares one frame, paper surface, tab bar and close decoration. Page content keeps its PSD coordinates.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void MergeSharedFrame(GameObject root)
        {
            var window = root.transform.Find("Window");
            var pages = new[] { window.Find("MyInfo"), window.Find("Relationships"), window.Find("World") };
            var frame = Rect(window, "SharedFrame", 0, 0, 2048, 1152);
            frame.SetSiblingIndex(1); // Above the click shield, below each page's content.
            var chrome = Rect(window, "SharedChrome", 0, 0, 2048, 1152);
            chrome.SetSiblingIndex(window.Find("Tab_0").GetSiblingIndex());
            var serialized = new SerializedObject(root.GetComponent<DetailsPanel>());

            // Keep exactly one disabled map layer, preserving the user's decision to hide it.
            Move(pages[0].Find("组 3"), frame, "MapBackground").gameObject.SetActive(false);
            Object.DestroyImmediate(pages[1].Find("组 3").gameObject);
            Object.DestroyImmediate(pages[2].Find("其他位置图钉").gameObject);
            Move(pages[0].Find("图层 3"), frame, "Backplate");
            Object.DestroyImmediate(pages[1].Find("图层 3 副本").gameObject);
            Object.DestroyImmediate(pages[2].Find("图层 3 副本 2").gameObject);

            string[] paperNames = { "底图", "咖啡底图 副本", "柱子 副本 2" };
            var styles = new DetailsPageSurfaceBinding[3];
            for (int i = 0; i < 3; i++) styles[i] = new DetailsPageSurfaceBinding(pages[i].Find(paperNames[i]).GetComponent<Image>());
            var paper = Move(pages[0].Find(paperNames[0]), frame, "PaperSurface").GetComponent<Image>();
            Object.DestroyImmediate(pages[1].Find(paperNames[1]).gameObject);
            Object.DestroyImmediate(pages[2].Find(paperNames[2]).gameObject);
            styles[0].Render(paper);
            Set(serialized, "paperSurface", paper);

            // Serialize value bindings through Unity's serializer, avoiding runtime hierarchy searches.
            var paperProperty = serialized.FindProperty("paperStyles"); paperProperty.arraySize = 3;
            string[] selectedNames = { "页签底（选中）", "页签底（选中） 副本", "页签底（选中） 副本 2" };
            var positions = serialized.FindProperty("selectedTabPositions"); positions.arraySize = 3;
            Vector2 selectedPosition = ((RectTransform)pages[0].Find(selectedNames[0])).anchoredPosition;
            int[] tabX = { 653, 528, 777 };
            for (int i = 0; i < 3; i++)
            {
                // Copy the plain serialized fields into the owning panel's inline array.
                var element = paperProperty.GetArrayElementAtIndex(i);
                WritePaperStyle(element, styles[i]);
                positions.GetArrayElementAtIndex(i).vector2Value = selectedPosition + new Vector2(tabX[i] - tabX[0], 0);
            }
            var selected = Move(pages[0].Find(selectedNames[0]), chrome, "SelectedTab");
            Set(serialized, "selectedTabArtwork", selected);
            Object.DestroyImmediate(pages[1].Find(selectedNames[1]).gameObject);
            Object.DestroyImmediate(pages[2].Find(selectedNames[2]).gameObject);
            Object.DestroyImmediate(pages[0].Find("页签（未选中）").gameObject);
            for (int i = 1; i < 3; i++) Object.DestroyImmediate(pages[i].Find("页签（未选中） 副本").gameObject);
            var idle = serialized.FindProperty("idleTabArtwork"); idle.arraySize = 3;
            Sprite idleSprite = EnsureIdleTabSprite();
            for (int i = 0; i < 3; i++)
            {
                var image = Image(chrome, "IdleTab_" + i, tabX[i], 135, 119.5f, 96.5f, idleSprite);
                idle.GetArrayElementAtIndex(i).objectReferenceValue = image;
                image.enabled = i != 0;
            }
            selected.SetAsLastSibling();
            string[] labels = { "人物关系_TMP", "我的信息_TMP", "世界情况_TMP" };
            foreach (string label in labels)
            {
                Move(pages[0].Find(label), chrome, label);
                for (int i = 1; i < 3; i++) Object.DestroyImmediate(pages[i].Find(label).gameObject);
            }
            var hints = serialized.FindProperty("pageHints"); hints.arraySize = 3;
            for (int i = 0; i < 3; i++) hints.GetArrayElementAtIndex(i).stringValue = pages[i].Find("PageHint_TMP").GetComponent<TMP_Text>().text;
            Set(serialized, "pageHint", Move(pages[0].Find("PageHint_TMP"), chrome, "PageHint").GetComponent<TMP_Text>());
            for (int i = 1; i < 3; i++) Object.DestroyImmediate(pages[i].Find("PageHint_TMP").gameObject);
            Move(pages[0].Find("退出按键"), chrome, "CloseArtwork");
            Object.DestroyImmediate(pages[1].Find("退出按键").gameObject);
            Object.DestroyImmediate(pages[2].Find("退出按键 副本").gameObject);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Move(Transform layer, Transform parent, string name)
        {
            // All layer parents use the same composition space; retain local PSD coordinates.
            layer.SetParent(parent, false);
            layer.name = name;
            return layer;
        }

        private static void WritePaperStyle(SerializedProperty destination, DetailsPageSurfaceBinding style)
        {
            var temporary = ScriptableObject.CreateInstance<PaperStyleSerialization>();
            try
            {
                temporary.Style = style;
                var data = new SerializedObject(temporary);
                var source = data.FindProperty("Style");
                foreach (string name in new[] { "sprite", "position", "size", "pivot", "scale", "color", "visible" })
                {
                    var from = source.FindPropertyRelative(name);
                    var to = destination.FindPropertyRelative(name);
                    switch (from.propertyType)
                    {
                        case SerializedPropertyType.ObjectReference: to.objectReferenceValue = from.objectReferenceValue; break;
                        case SerializedPropertyType.Vector2: to.vector2Value = from.vector2Value; break;
                        case SerializedPropertyType.Vector3: to.vector3Value = from.vector3Value; break;
                        case SerializedPropertyType.Color: to.colorValue = from.colorValue; break;
                        case SerializedPropertyType.Boolean: to.boolValue = from.boolValue; break;
                    }
                }
            }
            finally { Object.DestroyImmediate(temporary); }
        }
        private sealed class PaperStyleSerialization : ScriptableObject
        {
            public DetailsPageSurfaceBinding Style;
        }

        private static Sprite EnsureIdleTabSprite()
        {
            const string path = "Assets/Art/Func/UGUI/DetailsIdleTab.png";
            if (!File.Exists(path))
            {
                var source = new Texture2D(2, 2);
                var cropped = new Texture2D(239, 193);
                try
                {
                    source.LoadImage(File.ReadAllBytes("Assets/Art/Func/我的信息/页签（未选中）.png"));
                    cropped.SetPixels(source.GetPixels(0, 0, 239, 193)); cropped.Apply();
                    File.WriteAllBytes(path, cropped.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(source); Object.DestroyImmediate(cropped); }
                AssetDatabase.ImportAsset(path);
            }
            ImportDerivedSprite(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void BindPagesView(GameObject root)
        {
            var view = root.AddComponent<DetailsPagesView>();
            var data = new SerializedObject(view);
            string r = "Window/Relationships/RelationshipsView/";
            string w = "Window/World/WorldView/";
            Set(data, "characterGrid", root.transform.Find(r + "CharacterGrid"));
            Set(data, "portrait", root.transform.Find("Window/Relationships/大头像显示处").GetComponent<Image>());
            Set(data, "characterName", root.transform.Find(r + "Name").GetComponent<TMP_Text>());
            Set(data, "introductionContent", root.transform.Find(r + "IntroductionScroll/Content"));
            Set(data, "specialInformation", root.transform.Find(r + "SpecialInformation").GetComponent<TMP_Text>());
            Set(data, "familiarityValue", root.transform.Find(r + "FamiliarityValue").GetComponent<TMP_Text>());
            Set(data, "headingFont", Font);
            Set(data, "familiarityFill", root.transform.Find(r + "FamiliarityFill").GetComponent<Image>());
            Set(data, "previous", root.transform.Find(r + "Previous").GetComponent<Button>());
            Set(data, "next", root.transform.Find(r + "Next").GetComponent<Button>());
            Set(data, "pageLabel", root.transform.Find(r + "Page").GetComponent<TMP_Text>());
            Set(data, "selectorContent", root.transform.Find(w + "Selectors/Content"));
            Set(data, "worldDescription", root.transform.Find(w + "Description").GetComponent<TMP_Text>());
            Set(data, "worldValue", root.transform.Find(w + "Value").GetComponent<TMP_Text>());
            Set(data, "lower", root.transform.Find(w + "Lower").GetComponent<TMP_Text>());
            Set(data, "upper", root.transform.Find(w + "Upper").GetComponent<TMP_Text>());
            Set(data, "lowerDescription", root.transform.Find(w + "LowerDescription").GetComponent<TMP_Text>());
            Set(data, "upperDescription", root.transform.Find(w + "UpperDescription").GetComponent<TMP_Text>());
            Set(data, "worldFill", root.transform.Find(w + "Fill").GetComponent<Image>());
            Set(data, "characterSilhouette", Sprite("人物信息", "大头像显示处"));
            Set(data, "worldSelectedButton", Sprite("世界情况", "选中按键"));
            Set(data, "worldIdleButton", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Func/UGUI/WorldIdleButton.png"));
            data.ApplyModifiedPropertiesWithoutUndo();
            // The introduction sets its own preferred content height from TMP's measured text.
            Object.DestroyImmediate(root.transform.Find(r + "IntroductionScroll/Content").GetComponent<ContentSizeFitter>());
        }

        private static void ImportDerivedSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == default) throw new InvalidOperationException("Missing derived sprite: " + path);
            if (importer.textureType == TextureImporterType.Sprite) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static void ConvertPsd(RectTransform page, string sourceName)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Func/" + sourceName + ".prefab");
            if (source == default) throw new InvalidOperationException("Missing PSD prefab: " + sourceName);
            // Source exports currently have flat layers. Recursively copy groups too, retaining their names.
            var groups = new System.Collections.Generic.Dictionary<Transform, RectTransform> { [source.transform] = page };
            foreach (var renderer in source.GetComponentsInChildren<SpriteRenderer>(true).OrderBy(r => r.sortingOrder).ThenByDescending(r => r.transform.position.z))
            {
                Transform parent = renderer.transform.parent;
                EnsureGroup(parent, source.transform, page, groups);
                Sprite sprite = renderer.sprite;
                string path = AssetDatabase.GetAssetPath(sprite);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (sprite == default || importer == default) throw new InvalidOperationException("Missing PSD layer sprite: " + renderer.name);
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                var image = Image(groups[parent], renderer.name, 0, 0, width * .5f, height * .5f, sprite);
                Vector3 position = source.transform.InverseTransformPoint(renderer.transform.position);
                Vector3 scale = renderer.transform.lossyScale;
                image.rectTransform.pivot = sprite.pivot / sprite.rect.size;
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
                image.rectTransform.anchoredPosition = new Vector2(position.x * 50f - 1024f, position.y * 50f - 576f);
                image.rectTransform.localScale = new Vector3(scale.x / source.transform.lossyScale.x, scale.y / source.transform.lossyScale.y, 1);
                image.rectTransform.localRotation = renderer.transform.rotation;
                image.color = renderer.color;
                // Keep the original layer objects and bounds for inspection; dynamic UGUI replaces their content.
                bool text = renderer.name == "文本" || renderer.name == "-悬停查看相关属性-" || renderer.name == "文字" || renderer.name == "文字 副本" || renderer.name == "点击查看相关信息";
                bool dynamic = sourceName == "人物信息" && (renderer.name == "问号" || renderer.name == "进度")
                    || sourceName == "世界情况" && (renderer.name == "按键（未选中）" || renderer.name == "选中按键" || renderer.name == "进度条高亮" || renderer.name == "滑动条");
                image.enabled = renderer.enabled && !text && !dynamic;
                // Map imagery is supplied by the scene behind this modal, never by a details page.
                if (renderer.name == "组 3" || renderer.name == "其他位置图钉") image.gameObject.SetActive(false);
            }
        }

        private static void EnsureGroup(Transform parent, Transform sourceRoot, RectTransform page, System.Collections.Generic.Dictionary<Transform, RectTransform> groups)
        {
            if (groups.ContainsKey(parent)) return;
            EnsureGroup(parent.parent, sourceRoot, page, groups);
            // A transparent, full-composition group keeps all descendant pixel coordinates unchanged.
            groups[parent] = Rect(groups[parent.parent], parent.name, 0, 0, 2048, 1152);
        }

        private static void TabLabels(Transform page)
        {
            Text(page, "人物关系_TMP", "人物\n关系", 529, 142, 116, 82, 32, true);
            Text(page, "我的信息_TMP", "我的\n信息", 653, 142, 116, 82, 32, true);
            Text(page, "世界情况_TMP", "世界\n情况", 777, 142, 116, 82, 32, true);
            Text(page, "PageHint_TMP", page.name == "World" ? "点击查看相关信息" : page.name == "Relationships" ? "--点击可以查看结识人物信息--" : "-悬停查看相关属性-", 950, 183, 420, 36, 23, true);
        }

        private static void BuildMyInfo(RectTransform page, DetailsPanel panel, SerializedObject data)
        {
            Set(data, "playerNameText", Text(page, "PlayerName", "主角id", 728, 345, 928, 43, 34));
            Set(data, "backgroundText", HiddenText(page, "Background"));
            Set(data, "introductionText", Text(page, "Introduction", "", 733, 389, 924, 69, 23));
            var avatar = Image(page, "Avatar", 400, 318, 200, 235, null); avatar.preserveAspect = true;
            Set(data, "avatarImage", avatar);
            var placeholder = Rect(page, "AvatarPlaceholder", 400, 318, 200, 235);
            Set(data, "avatarPlaceholder", placeholder.gameObject);
            Set(data, "avatarPlaceholderLabel", HiddenText(placeholder, "Label"));
            var defaultTip = Rect(page, "DefaultTooltip", 465, 625, 210, 298);
            Text(defaultTip, "Title", "智慧", 18, 52, 174, 43, 29, true);
            Text(defaultTip, "Body", "影响分析、推理与解决问题", 20, 113, 170, 170, 23);
            Set(data, "defaultTooltip", defaultTip.gameObject);
            Text(page, "AttributesTitle", "主角属性", 728, 468, 300, 42, 32);
            Text(page, "AttributesHint", "-悬停查看相关属性-", 1490, 489, 190, 32, 21, true);
            var row = Rect(page, "Attributes", 760, 526, 744, 124);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40; layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var bindings = data.FindProperty("attributeBindings"); bindings.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var card = Image(row, "Attribute_" + i, 0, 0, 156, 124, null); card.color = Color.clear; card.raycastTarget = true;
                var name = Text(card.transform, "Name", "", 58, 9, 95, 43, 30, true); name.color = new Color(1, .96f, .83f);
                var value = Text(card.transform, "Value", "0 / 20", 4, 68, 148, 47, 35, true);
                var hover = card.gameObject.AddComponent<DetailsHoverTarget>();
                var binding = bindings.GetArrayElementAtIndex(i);
                binding.FindPropertyRelative("nameText").objectReferenceValue = name;
                binding.FindPropertyRelative("valueText").objectReferenceValue = value;
                binding.FindPropertyRelative("hover").objectReferenceValue = hover;
            }
            Text(page, "SpecializationsTitle", "主角专精", 728, 663, 300, 42, 32);
            var hint = Text(page, "SpecializationsHint", "-悬停查看相关属性-", 1490, 678, 190, 28, 21, true);
            Set(data, "specializationsHintText", hint);
            var content = Scroll(page, "Specializations", 773, 725, 890, 165, true);
            var scroll = content.parent.GetComponent<ScrollRect>();
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            grid.spacing = new Vector2(12, 12); grid.cellSize = new Vector2(288, 64);
            var empty = Text(scroll.transform, "Empty", "暂无专精", 0, 0, 890, 165, 28, true);
            Set(data, "emptySpecializationsText", empty);
            Set(data, "specializationContent", content); Set(data, "specializationViewport", scroll.viewport);
            Set(data, "specializationScroll", scroll); Set(data, "specializationGrid", grid);
            var template = Image(page, "SpecializationTemplate", 0, 0, 288, 64, null);
            template.color = new Color(.53f, .32f, .12f, .15f); template.raycastTarget = true;
            var icon = Image(template.transform, "Icon", 10, 9, 46, 46, null); icon.preserveAspect = true;
            var item = template.gameObject.AddComponent<DetailsSpecializationItem>();
            var itemData = new SerializedObject(item);
            Set(itemData, "icon", icon); Set(itemData, "nameText", Text(template.transform, "Name", "", 65, 4, 212, 56, 23));
            Set(itemData, "fallbackIcon", Text(icon.transform, "FallbackIcon", "★", 0, 0, 46, 46, 26, true));
            Set(itemData, "hover", template.gameObject.AddComponent<DetailsHoverTarget>());
            itemData.ApplyModifiedPropertiesWithoutUndo(); template.gameObject.SetActive(false);
            data.FindProperty("specializationItems").arraySize = 1;
            data.FindProperty("specializationItems").GetArrayElementAtIndex(0).objectReferenceValue = item;
        }

        private static void BuildRelationships(RectTransform page, SerializedObject data)
        {
            BindSection(data.FindProperty("relationshipsBinding"), page);
            var view = Rect(page, "RelationshipsView", 0, 0, 2048, 1152);

            Text(view, "Name", "人物姓名", 406, 616, 240, 46, 32, true);
            Scroll(view, "IntroductionScroll", 410, 673, 238, 229, true);
            Text(view, "SpecialInformationTitle", "人物特殊信息：", 695, 304, 406, 40, 26);
            var special = Text(view, "SpecialInformation", "", 695, 344, 406, 55, 20);
            special.raycastTarget = true; special.gameObject.AddComponent<DetailsHoverTarget>();
            Text(view, "FamiliarityLabel", "熟\n悉\n度", 357, 925, 23, 60, 18, true);
            var familiarityValue = Text(view, "FamiliarityValue", "0 / 100", 321, 277, 90, 28, 20, true);
            familiarityValue.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
            var fill = Image(view, "FamiliarityFill", 363, 336, 11, 582, Sprite("人物信息", "进度"));
            fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical; fill.fillAmount = 0;
            var grid = Rect(view, "CharacterGrid", 674, 281, 1018, 636).gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 7;
            grid.cellSize = new Vector2(145.4f, 127.2f); grid.spacing = Vector2.zero;
            var previous = Image(view, "Previous", 1430, 929, 110, 25, null); previous.color = Color.clear; previous.raycastTarget = true;
            Button(previous); Text(previous.transform, "Label", "上一页", 0, 0, 110, 30, 20, true);
            var next = Image(view, "Next", 1580, 874, 110, 43, null); next.color = Color.clear; next.raycastTarget = true;
            Button(next); Text(next.transform, "Label", "下一页", 0, 0, 110, 43, 27, true);
            Text(view, "Page", "1 / 1", 1545, 929, 75, 25, 18, true);
        }

        private static void BuildWorld(RectTransform page, SerializedObject data)
        {
            BindSection(data.FindProperty("worldBinding"), page);
            var view = Rect(page, "WorldView", 0, 0, 2048, 1152);
            var content = Scroll(view, "Selectors", 369, 349, 255, 512, true);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true; layout.childControlHeight = false;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; layout.spacing = 29;
            Text(view, "Description", "", 768, 398, 836, 34, 23);
            Text(view, "Value", "", 774, 524, 833, 28, 23);
            var fill = Image(view, "Fill", 744, 547, 876, 16, Sprite("世界情况", "进度条高亮"));
            fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            Text(view, "Lower", "0", 750, 566, 150, 33, 23);
            Text(view, "Upper", "10", 1457, 566, 150, 33, 23, false, TextAlignmentOptions.MidlineRight);
            Text(view, "LowerDescription", "", 770, 610, 402, 150, 22);
            Text(view, "UpperDescription", "", 1200, 610, 405, 150, 22, false, TextAlignmentOptions.TopRight);
        }

        private static void BindSection(SerializedProperty property, Transform page)
        {
            property.FindPropertyRelative("title").objectReferenceValue = HiddenText(page, "Title");
            property.FindPropertyRelative("heading").objectReferenceValue = HiddenText(page, "Heading");
            property.FindPropertyRelative("description").objectReferenceValue = HiddenText(page, "Description");
        }

        private static void BuildTooltip(Transform parent, SerializedObject data)
        {
            var tip = Rect(parent, "Tooltip", 465, 625, 210, 298);
            var image = tip.gameObject.AddComponent<Image>(); image.sprite = Sprite("我的信息", "悬浮显示框"); image.raycastTarget = false;
            tip.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            Set(data, "tooltip", tip);
            Set(data, "tooltipTitle", Text(tip, "Title", "", 18, 52, 174, 43, 29, true));
            Set(data, "tooltipBody", Text(tip, "Body", "", 20, 113, 170, 170, 23));
            tip.gameObject.SetActive(false);
        }

        private static TMP_Text HiddenText(Transform parent, string name)
        {
            var text = Text(parent, name, "", 0, 0, 1, 1, 18); text.gameObject.SetActive(false); return text;
        }
        private static void Set(SerializedObject data, string name, Object value) => data.FindProperty(name).objectReferenceValue = value;
        private static Sprite Sprite(string folder, string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Func/" + folder + "/" + name + ".png");
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(w, h); rect.anchoredPosition = new Vector2(x + w / 2, -y - h / 2); return rect;
        }
        private static Image Image(Transform parent, string name, float x, float y, float w, float h, Sprite sprite)
        {
            var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false; return image;
        }
        private static TMP_Text Text(Transform parent, string name, string value, float x, float y, float w, float h, float size, bool center = false, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            if (size >= 26)
            {
                int lines = value.Split('\n').Length;
                text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(h, size * 1.45f * lines));
            }
            text.font = size >= 26 ? Font : BodyFont; text.text = value; text.color = Ink; text.fontSize = size;
            text.alignment = center ? TextAlignmentOptions.Center : alignment; text.raycastTarget = false;
            text.enableAutoSizing = true; text.fontSizeMin = size * .7f; text.fontSizeMax = size;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }
        private static Button Button(Image image)
        {
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.transition = Selectable.Transition.None; button.navigation = new Navigation { mode = Navigation.Mode.None }; return button;
        }
        private static RectTransform Scroll(Transform parent, string name, float x, float y, float w, float h, bool fitter)
        {
            var viewport = Rect(parent, name, x, y, w, h);
            var raycast = viewport.gameObject.AddComponent<Image>(); raycast.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect(viewport, "Content", 0, 0, w, h);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(0, 0); content.anchoredPosition = Vector2.zero;
            if (fitter) content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
            return content;
        }
    }
}
