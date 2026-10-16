using System;
using Spotlight.GameFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>Legacy bootstrap for a missing DateChangePanel prefab. Never replaces an existing asset.</summary>
    public static class DateChangePanelPrefabRebuilder
    {
        private const string PrefabPath = "Assets/Resources/Art/UIPrefab/DateChangePanel.prefab";
        private const string BackgroundPath = "Assets/Resources/Art/Gameplay/DayFlow/phase_date_transition_clock.png";
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Resources/Fonts/乐米曲奇方块体 SDF.asset");

        [MenuItem("Tools/Game Flow/Rebuild Date Change Panel (Clock)")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before rebuilding the date panel.");
            if (Font == null) throw new InvalidOperationException("SimHei SDF font is missing.");
            Texture2D backgroundTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            if (backgroundTexture == null) throw new InvalidOperationException("Missing generated clock background: " + BackgroundPath);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                throw new InvalidOperationException("DateChangePanel already exists. Edit the prefab instead of replacing its GUID and artwork.");

            GameObject root = new GameObject("DateChangePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Canvas), typeof(GraphicRaycaster));
            try
            {
                Stretch(root.GetComponent<RectTransform>());
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 700;
                DateChangePanel panel = root.AddComponent<DateChangePanel>();

                RawImage background = Child(root.transform, "Background", typeof(RawImage)).GetComponent<RawImage>();
                Stretch(background.rectTransform);
                background.texture = backgroundTexture;
                background.color = Color.white;
                background.raycastTarget = false;

                Image blocker = Image(root.transform, "InputBlocker", Vector2.zero, Vector2.one,
                    new Color(.01f, .008f, .012f, .05f));
                blocker.raycastTarget = true;

                RectTransform content = Child(root.transform, "Content").GetComponent<RectTransform>();
                Stretch(content);

                TMP_Text title = Text(content, "Title", "TIME  /  NEW DAY", new Vector2(86f, -55f),
                    new Vector2(700f, 54f), 27f, new Color(.92f, .72f, .73f));
                title.characterSpacing = 5f;

                TMP_Text dayCaption = Text(content, "DayCaption", "NEW DAY", new Vector2(1200f, -365f),
                    new Vector2(360f, 45f), 24f, new Color(.96f, .78f, .74f));
                dayCaption.alignment = TextAlignmentOptions.Center;
                dayCaption.characterSpacing = 4f;

                TMP_Text dayNumber = Text(content, "DayNumber", "01", new Vector2(1190f, -415f),
                    new Vector2(360f, 170f), 118f, new Color(.98f, .78f, .73f));
                dayNumber.alignment = TextAlignmentOptions.Center;
                dayNumber.fontStyle = FontStyles.Bold;

                TMP_Text previous = Text(content, "PreviousDate", "昨日", new Vector2(1020f, -695f),
                    new Vector2(360f, 42f), 20f, new Color(.79f, .62f, .66f));
                previous.alignment = TextAlignmentOptions.Center;

                TMP_Text detail = Text(content, "Detail", "新的一天", new Vector2(1190f, -695f),
                    new Vector2(580f, 48f), 24f, new Color(.98f, .87f, .81f));
                detail.alignment = TextAlignmentOptions.Center;

                // The triangle is a TMP glyph so the panel does not need a new sprite asset.
                TMP_Text marker = Text(content, "TransitionMarker", "▶", new Vector2(1112f, -487f),
                    new Vector2(78f, 78f), 42f, new Color(.98f, .80f, .76f));
                marker.alignment = TextAlignmentOptions.Center;

                // A transparent RectTransform hook lets DateChangePanel animate the clock motif later
                // without baking the animation into the background artwork.
                RectTransform hand = Child(content, "ClockHand").GetComponent<RectTransform>();
                hand.anchorMin = hand.anchorMax = new Vector2(0f, 0f);
                hand.pivot = new Vector2(.5f, .5f);
                hand.anchoredPosition = new Vector2(1600f, 540f);
                hand.sizeDelta = new Vector2(4f, 4f);

                Button continueButton = Button(root.transform, "Continue", "", new Vector2(0f, 0f), new Vector2(1f, 1f));
                continueButton.gameObject.SetActive(false);

                SerializedObject serialized = new SerializedObject(panel);
                Set(serialized, "canvasGroup", root.GetComponent<CanvasGroup>());
                Set(serialized, "content", content);
                Set(serialized, "background", background);
                Set(serialized, "titleText", title);
                Set(serialized, "detailText", detail);
                Set(serialized, "continueButton", continueButton);
                Set(serialized, "dayNumberText", dayNumber);
                Set(serialized, "dayCaptionText", dayCaption);
                Set(serialized, "previousDateText", previous);
                Set(serialized, "transitionMarker", marker.rectTransform);
                Set(serialized, "clockHand", hand);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[DayFlow] Rebuilt DateChangePanel with clock/date transition layout.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject Child(Transform parent, string name, params Type[] components)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            foreach (Type type in components) go.AddComponent(type);
            return go;
        }

        private static TMP_Text Text(Transform parent, string name, string value, Vector2 position,
            Vector2 size, float fontSize, Color color)
        {
            TMP_Text text = Child(parent, name, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            TopLeft(text.rectTransform, position, size);
            text.font = Font;
            text.fontSize = fontSize;
            text.text = value;
            text.color = color;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            return text;
        }

        private static Image Image(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            Image image = Child(parent, name, typeof(Image)).GetComponent<Image>();
            image.rectTransform.anchorMin = min;
            image.rectTransform.anchorMax = max;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            image.color = color;
            return image;
        }

        private static Button Button(Transform parent, string name, string label, Vector2 min, Vector2 max)
        {
            Image image = Image(parent, name, min, max, new Color(0f, 0f, 0f, 0f));
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static void TopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Set(SerializedObject serialized, string name, Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) throw new InvalidOperationException("Missing serialized field: " + name);
            property.objectReferenceValue = value;
        }
    }
}
