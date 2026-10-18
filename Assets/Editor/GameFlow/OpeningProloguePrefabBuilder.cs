using System;
using Spotlight.GameFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>
    /// Builds the opening prologue prefab; all copy, comic-panel layout and entrance timing are serialized
    /// into the prefab so they stay editable in the Inspector after generation.
    /// </summary>
    public static class OpeningProloguePrefabBuilder
    {
        private const string PrefabPath = "Assets/Resources/Art/UIPrefab/OpeningProloguePanel.prefab";
        private const string FontPath = "Assets/Resources/Fonts/乐米曲奇方块体 SDF.asset";
        private const string ArtFolder = "Assets/Art/UI/OpeningPrologue";
        private const float RefWidth = 1920f, RefHeight = 1080f;

        private struct PanelSpec
        {
            public string sprite;
            public Rect rect; // normalized screen rect (x, y from bottom-left, w, h)
            public ProloguePanelArt.Entrance entrance;
            public float delay, duration;
            public PanelSpec(string sprite, Rect rect, ProloguePanelArt.Entrance entrance, float delay, float duration)
            { this.sprite = sprite; this.rect = rect; this.entrance = entrance; this.delay = delay; this.duration = duration; }
        }

        private struct BeatSpec
        {
            public string text;
            public PanelSpec[] panels;
            public float artBottom;
        }

        private static BeatSpec[] Beats()
        {
            const ProloguePanelArt.Entrance Split = ProloguePanelArt.Entrance.SplitVertical, Zoom = ProloguePanelArt.Entrance.ZoomIn,
                FlyL = ProloguePanelArt.Entrance.FlyFromLeft, FlyR = ProloguePanelArt.Entrance.FlyFromRight,
                FlyT = ProloguePanelArt.Entrance.FlyFromTop, FlyB = ProloguePanelArt.Entrance.FlyFromBottom;
            return new[]
            {
                new BeatSpec { artBottom = .410f,
                    text = "圣诺特，奥尔狄亚王国的心脏，奥尔狄亚人永恒的骄傲\n\n这座都城在神与王的引领下熠熠生辉\n\n自那神佑者降临于苍茫大地\n\n奥尔狄亚王国已屹立于世间600余年",
                    panels = new[] {
                        new PanelSpec("prologue_01_a", new Rect(.3926f, .4100f, .1187f, .4890f), FlyB, 0f, 2.1f),
                        new PanelSpec("prologue_01_b", new Rect(.5237f, .4557f, .0837f, .4543f), FlyT, .6f, 1.9f) } },
                new BeatSpec { artBottom = .505f,
                    text = "神佑者的血脉庇佑着这个伟大的国度\n\n神佑者的荣光曾欲万古长存",
                    panels = new[] {
                        new PanelSpec("prologue_02_a", new Rect(.2864f, .5051f, .4272f, .3097f), Split, .2f, 2.2f) } },
                new BeatSpec { artBottom = .410f,
                    text = "然普照万物的光日渐消隐\n\n无名之人的愤怒在街巷间涌动\n\n野心家的欲望在阴影中生长",
                    panels = new[] {
                        new PanelSpec("prologue_03_a", new Rect(.3289f, .4141f, .0824f, .3452f), FlyT, 0f, 1.8f),
                        new PanelSpec("prologue_03_b", new Rect(.4467f, .4100f, .0913f, .4346f), FlyB, .5f, 1.8f),
                        new PanelSpec("prologue_03_c", new Rect(.5678f, .4133f, .1034f, .4967f), FlyT, 1f, 1.8f) } },
                new BeatSpec { artBottom = .410f,
                    text = "神安能庇佑奥尔狄亚人又一个百年？\n\n抑或，我们将淹没在沸腾的人民之声中？",
                    panels = new[] {
                        new PanelSpec("prologue_04_a", new Rect(.3792f, .4100f, .1132f, .4991f), FlyL, 0f, 1.9f),
                        new PanelSpec("prologue_04_b", new Rect(.5065f, .4100f, .1143f, .5000f), FlyR, .45f, 1.9f) } },
                new BeatSpec { artBottom = .436f,
                    text = "圣诺特，那千万生灵留恋的地方\n\n我曾作为芸芸众生中一员生活在这座大都市\n\n经营着一座平凡的咖啡馆",
                    panels = new[] {
                        new PanelSpec("prologue_05_a", new Rect(.2884f, .6886f, .2311f, .1952f), FlyL, 0f, 1.9f),
                        new PanelSpec("prologue_05_b", new Rect(.3131f, .4361f, .3985f, .2269f), FlyR, .6f, 1.9f) } },
                new BeatSpec { artBottom = .469f,
                    text = "顾客来来往往，车水马龙\n\n我曾想安然度日\n\n在咖啡和茶点的芳香中就此了解余生",
                    panels = new[] {
                        new PanelSpec("prologue_06_a", new Rect(.2637f, .4688f, .0937f, .3823f), FlyB, 0f, 1.8f),
                        new PanelSpec("prologue_06_b", new Rect(.4051f, .4821f, .3312f, .2566f), FlyR, .5f, 1.9f) } },
                new BeatSpec { artBottom = .463f,
                    text = "然而那激情的岁月中，无人可以作壁上观\n\n我亦是如此",
                    panels = new[] {
                        new PanelSpec("prologue_07_a", new Rect(.2579f, .7367f, .3450f, .1206f), FlyL, 0f, 1.9f),
                        new PanelSpec("prologue_07_b", new Rect(.3350f, .4627f, .4071f, .2249f), Zoom, .8f, 2.2f) } },
                new BeatSpec { artBottom = -1f, text = "孩子们，现在，我将为你讲述那段故事——", panels = Array.Empty<PanelSpec>() }
            };
        }

        [MenuItem("Tools/Game Flow/Create Missing Opening Prologue Panel")]
        public static void CreateMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
            Build();
        }

        [MenuItem("Tools/Game Flow/Rebuild Opening Prologue Panel")]
        public static void Rebuild() => Build();

        private static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) throw new InvalidOperationException("Opening prologue font is missing.");
            BeatSpec[] specs = Beats();

            GameObject root = new GameObject("OpeningProloguePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Canvas), typeof(GraphicRaycaster));
            try
            {
                Stretch(root.GetComponent<RectTransform>());
                root.GetComponent<Canvas>().sortingOrder = 750;
                OpeningProloguePanel panel = root.AddComponent<OpeningProloguePanel>();
                CanvasGroup group = root.GetComponent<CanvasGroup>();
                Image background = Image(root.transform, "BlackBackground", Color.black, true);
                Stretch(background.rectTransform);

                RectTransform artRoot = new GameObject("BeatArt", typeof(RectTransform)).GetComponent<RectTransform>();
                artRoot.SetParent(root.transform, false);
                Stretch(artRoot);
                CanvasGroup[] beatArt = new CanvasGroup[specs.Length];
                for (int i = 0; i < specs.Length; i++)
                    if (specs[i].panels.Length > 0) beatArt[i] = BuildBeatArt(artRoot, i, specs[i].panels);

                Button advance = Button(root.transform, "AdvanceArea", new Color(0, 0, 0, 0), true);
                Stretch(advance.GetComponent<RectTransform>());
                TMP_Text narration = Text(root.transform, "Narration", font, 34f, Color.white);
                narration.alignment = TextAlignmentOptions.Center;
                narration.textWrappingMode = TextWrappingModes.Normal;
                narration.enableAutoSizing = true;
                narration.fontSizeMin = 22f;
                narration.fontSizeMax = 34f;
                narration.lineSpacing = -8f;
                narration.characterSpacing = 4f;
                narration.rectTransform.anchorMin = new Vector2(.12f, .22f);
                narration.rectTransform.anchorMax = new Vector2(.88f, .78f);
                narration.rectTransform.offsetMin = narration.rectTransform.offsetMax = Vector2.zero;
                narration.raycastTarget = false;
                Button skip = Button(root.transform, "SkipButton", new Color(.12f, .12f, .12f, .9f), true);
                RectTransform skipRect = skip.GetComponent<RectTransform>();
                skipRect.anchorMin = skipRect.anchorMax = skipRect.pivot = new Vector2(1, 0);
                skipRect.anchoredPosition = new Vector2(-48, 38);
                skipRect.sizeDelta = new Vector2(180, 58);
                TMP_Text label = Text(skip.transform, "Label", font, 24f, Color.white);
                Stretch(label.rectTransform); label.alignment = TextAlignmentOptions.Center; label.text = "Skip";

                SerializedObject serialized = new SerializedObject(panel);
                Set(serialized, "canvasGroup", group);
                Set(serialized, "background", background);
                Set(serialized, "narrationText", narration);
                Set(serialized, "advanceArea", advance);
                Set(serialized, "skipButton", skip);
                serialized.FindProperty("defaultFadeInSeconds").floatValue = .8f;
                serialized.FindProperty("defaultFadeOutSeconds").floatValue = .8f;
                serialized.FindProperty("finalBlackHoldSeconds").floatValue = .05f;
                serialized.FindProperty("charStaggerSeconds").floatValue = .055f;
                serialized.FindProperty("charRisePixels").floatValue = 14f;
                SerializedProperty beats = serialized.FindProperty("beats");
                beats.arraySize = specs.Length;
                for (int i = 0; i < specs.Length; i++)
                {
                    bool hasArt = beatArt[i] != null;
                    SerializedProperty beat = beats.GetArrayElementAtIndex(i);
                    beat.FindPropertyRelative("text").stringValue = specs[i].text;
                    beat.FindPropertyRelative("fadeInSeconds").floatValue = .8f;
                    beat.FindPropertyRelative("fadeOutSeconds").floatValue = .8f;
                    beat.FindPropertyRelative("art").objectReferenceValue = beatArt[i];
                    beat.FindPropertyRelative("artDelaySeconds").floatValue = 0f; // art enters together with the copy
                    // With art, the copy settles in the band beneath the panels; the art-less finale stays centred.
                    beat.FindPropertyRelative("textArea").vector2Value = hasArt
                        ? new Vector2(.06f, specs[i].artBottom - .035f)
                        : new Vector2(.30f, .70f);
                    beat.FindPropertyRelative("fontSize").floatValue = hasArt ? 32f : 38f;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[GameFlow] Built OpeningProloguePanel.prefab with comic-panel beats.");
        }

        private static CanvasGroup BuildBeatArt(RectTransform parent, int index, PanelSpec[] panels)
        {
            GameObject beatGo = new GameObject($"Beat{index + 1:00}Art", typeof(RectTransform), typeof(CanvasGroup));
            beatGo.transform.SetParent(parent, false);
            Stretch(beatGo.GetComponent<RectTransform>());
            CanvasGroup beatGroup = beatGo.GetComponent<CanvasGroup>();
            beatGroup.alpha = 0f; beatGroup.interactable = false; beatGroup.blocksRaycasts = false;
            foreach (PanelSpec spec in panels)
            {
                Sprite sprite = LoadSprite(spec.sprite);
                GameObject frameGo = new GameObject(spec.sprite, typeof(RectTransform), typeof(CanvasGroup), typeof(RectMask2D), typeof(ProloguePanelArt));
                frameGo.transform.SetParent(beatGo.transform, false);
                RectTransform frame = frameGo.GetComponent<RectTransform>();
                // Centre-anchored in reference pixels so the panels keep their aspect on any screen ratio.
                frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);
                frame.sizeDelta = new Vector2(spec.rect.width * RefWidth, spec.rect.height * RefHeight);
                frame.anchoredPosition = new Vector2((spec.rect.center.x - .5f) * RefWidth, (spec.rect.center.y - .5f) * RefHeight);
                Image art = Image(frame, "Art", Color.white, false);
                Stretch(art.rectTransform);
                art.sprite = sprite;

                SerializedObject so = new SerializedObject(frameGo.GetComponent<ProloguePanelArt>());
                Set(so, "frame", frame);
                Set(so, "art", art.rectTransform);
                Set(so, "group", frameGo.GetComponent<CanvasGroup>());
                Set(so, "mask", frameGo.GetComponent<RectMask2D>());
                so.FindProperty("entrance").enumValueIndex = (int)spec.entrance;
                so.FindProperty("delay").floatValue = spec.delay;
                so.FindProperty("duration").floatValue = spec.duration;
                so.ApplyModifiedPropertiesWithoutUndo();
                frameGo.GetComponent<CanvasGroup>().alpha = 0f;
            }
            return beatGroup;
        }

        private static Sprite LoadSprite(string name)
        {
            string path = $"{ArtFolder}/{name}.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                throw new InvalidOperationException($"Opening prologue art is missing: {path}");
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException($"Opening prologue sprite failed to import: {path}");
            return sprite;
        }

        private static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponent<TMP_Text>(); text.font = font; text.fontSize = size; text.color = color; text.raycastTarget = false;
            return text;
        }
        private static Image Image(Transform parent, string name, Color color, bool raycast)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = raycast; return image;
        }
        private static Button Button(Transform parent, string name, Color color, bool raycast)
        {
            Image image = Image(parent, name, color, raycast);
            Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.None; return button;
        }
        private static void Set(SerializedObject serialized, string name, Object value) => serialized.FindProperty(name).objectReferenceValue = value;
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
