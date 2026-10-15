using System;
using Spotlight.GameFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>Creates the opening prologue prefab once; all copy is serialized into the prefab.</summary>
    public static class OpeningProloguePrefabBuilder
    {
        private const string PrefabPath = "Assets/Resources/Art/UIPrefab/OpeningProloguePanel.prefab";
        private const string FontPath = "Assets/Resources/Fonts/乐米曲奇方块体 SDF.asset";

        [MenuItem("Tools/Game Flow/Create Missing Opening Prologue Panel")]
        public static void CreateMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) throw new InvalidOperationException("Opening prologue font is missing.");

            GameObject root = new GameObject("OpeningProloguePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Canvas), typeof(GraphicRaycaster));
            try
            {
                Stretch(root.GetComponent<RectTransform>());
                root.GetComponent<Canvas>().sortingOrder = 750;
                OpeningProloguePanel panel = root.AddComponent<OpeningProloguePanel>();
                CanvasGroup group = root.GetComponent<CanvasGroup>();
                Image background = Image(root.transform, "BlackBackground", Color.black, true);
                Stretch(background.rectTransform);
                Button advance = Button(root.transform, "AdvanceArea", new Color(0, 0, 0, 0), true);
                Stretch(advance.GetComponent<RectTransform>());
                TMP_Text narration = Text(root.transform, "Narration", font, 34f, Color.white);
                narration.alignment = TextAlignmentOptions.Center;
                narration.enableWordWrapping = true;
                narration.rectTransform.anchorMin = new Vector2(.12f, .22f);
                narration.rectTransform.anchorMax = new Vector2(.88f, .78f);
                narration.rectTransform.offsetMin = narration.rectTransform.offsetMax = Vector2.zero;
                narration.raycastTarget = false;
                Button skip = Button(root.transform, "SkipButton", new Color(.12f, .12f, .12f, .9f), true);
                skip.GetComponent<RectTransform>().anchorMin = new Vector2(1, 0);
                skip.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);
                skip.GetComponent<RectTransform>().pivot = new Vector2(1, 0);
                skip.GetComponent<RectTransform>().anchoredPosition = new Vector2(-48, 38);
                skip.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 58);
                TMP_Text label = Text(skip.transform, "Label", font, 24f, Color.white);
                Stretch(label.rectTransform); label.alignment = TextAlignmentOptions.Center; label.text = "Skip";

                SerializedObject serialized = new SerializedObject(panel);
                Set(serialized, "canvasGroup", group);
                Set(serialized, "background", background);
                Set(serialized, "narrationText", narration);
                Set(serialized, "advanceArea", advance);
                Set(serialized, "skipButton", skip);
                serialized.FindProperty("defaultFadeInSeconds").floatValue = .8f;
                serialized.FindProperty("defaultFadeOutSeconds").floatValue = .5f;
                serialized.FindProperty("finalBlackHoldSeconds").floatValue = .05f;
                SerializedProperty beats = serialized.FindProperty("beats");
                string[,] copy =
                {
                    { "圣诺特，奥尔狄亚王国的心脏，奥尔狄亚人永恒的骄傲\n\n这座都城在神与王的引领下熠熠生辉\n\n自那神佑者降临于苍茫大地\n\n奥尔狄亚王国已屹立于世间600余年", ".8", ".5" },
                    { "神佑者的血脉庇佑着这个伟大的国度\n\n神佑者的荣光曾欲万古长存", ".8", ".5" },
                    { "然普照万物的光日渐消隐\n\n无名之人的愤怒在街巷间涌动\n\n野心家的欲望在阴影中生长", ".8", ".5" },
                    { "神安能庇佑奥尔狄亚人又一个百年？\n\n抑或，我们将淹没在沸腾的人民之声中？", ".8", ".5" },
                    { "圣诺特，那千万生灵留恋的地方\n\n我曾作为芸芸众生中一员生活在这座大都市\n\n经营着一座平凡的咖啡馆", ".8", ".5" },
                    { "顾客来来往往，车水马龙\n\n我曾想安然度日\n\n在咖啡和茶点的芳香中就此了解余生", ".8", ".5" },
                    { "然而那激情的岁月中，无人可以作壁上观\n\n我亦是如此", ".8", ".5" },
                    { "孩子们，现在，我将为你讲述那段故事——", ".8", ".5" }
                };
                beats.arraySize = copy.GetLength(0);
                for (int i = 0; i < copy.GetLength(0); i++)
                {
                    SerializedProperty beat = beats.GetArrayElementAtIndex(i);
                    beat.FindPropertyRelative("text").stringValue = copy[i, 0];
                    beat.FindPropertyRelative("fadeInSeconds").floatValue = float.Parse(copy[i, 1], System.Globalization.CultureInfo.InvariantCulture);
                    beat.FindPropertyRelative("fadeOutSeconds").floatValue = float.Parse(copy[i, 2], System.Globalization.CultureInfo.InvariantCulture);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[GameFlow] Created OpeningProloguePanel.prefab.");
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
        private static void Set(SerializedObject serialized, string name, UnityEngine.Object value) => serialized.FindProperty(name).objectReferenceValue = value;
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
