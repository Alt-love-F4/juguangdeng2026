using System;
using Spotlight.GameFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>Creates missing assets only. Never overwrites artist-edited prefabs or scenes.</summary>
    public static class DayFlowPanelPrefabBuilder
    {
        private const string Root = "Assets/Resources/Art/UIPrefab";
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Resources/Fonts/SimHei SDF.asset");

        [MenuItem("Tools/Game Flow/Create Missing Day Flow Panels")]
        public static void CreateMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before creating panel prefabs.");
            if (Font == null) throw new InvalidOperationException("SimHei SDF font is missing.");
            Create<DateChangePanel>("phase_date_transition", "新的一天", "昨日  →  今日", "日期变更");
            Create<PreBusinessPanel>("phase_pre_business", "经营前阶段",
                "今天的咖啡馆即将开始营业。这里可以接入开店前事件、昨日结算和准备工作。", "进入准备");
            Create<CafeOpeningPanel>("phase_business_transition", "咖啡馆营业",
                "店铺点灯、整理台面，今天的客人即将到来。", "开始营业");
            Create<BusinessEndPanel>("phase_pre_leisure", "经营结束",
                "今日营业已结束。可以查看结算，随后前往城区。", "进入闲暇");
            Create<CityMapPanel>("city_map_placeholder", "闲暇时段 · 圣诺特城区",
                "这里是城区地图占位。之后可接入天气、行动力、地点和人物可用性。", "前往城区");
            Create<PreNightPanel>("phase_pre_night", "夜晚前阶段",
                "城区活动结束，准备返回住房。这里可以接入错过事件和返程事件。", "返回住房");
            Create<HomeNightPanel>("home_exterior_night", "夜晚 · 住房外景",
                "这里是住房外景占位。夜晚活动选项将在此处接入。", "结束夜晚");
            Create<DayEndPanel>("day_end_transition", "一天结束",
                "夜晚活动完成。准备进入下一天。", "进入下一天");
            CreateBusinessControls();
            Debug.Log("[DayFlow] Missing panel prefabs created. Existing prefabs and scenes were not overwritten.");
        }

        private static void Create<T>(string art, string title, string detail, string button)
            where T : DayFlowPresentationPanel
        {
            string path = Root + "/" + typeof(T).Name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Resources/Art/Gameplay/DayFlow/" + art + ".png");
            if (texture == null) throw new InvalidOperationException("Missing phase artwork: " + art);
            GameObject root = new GameObject(typeof(T).Name, typeof(RectTransform), typeof(CanvasGroup), typeof(Canvas), typeof(GraphicRaycaster));
            try
            {
                Stretch(root.GetComponent<RectTransform>());
                ConfigureCanvas(root);
                var panel = root.AddComponent<T>();
                var image = Child(root.transform, "Background", typeof(RawImage)).GetComponent<RawImage>();
                Stretch(image.rectTransform);
                image.texture = texture;
                image.raycastTarget = false;
                var shade = Child(root.transform, "Shade", typeof(Image)).GetComponent<Image>();
                Stretch(shade.rectTransform);
                shade.color = new Color(.015f, .02f, .03f, .3f);
                shade.raycastTarget = true;

                RectTransform card = Child(root.transform, "Content").GetComponent<RectTransform>();
                card.anchorMin = card.anchorMax = new Vector2(0f, .5f);
                card.pivot = new Vector2(0f, .5f);
                card.anchoredPosition = new Vector2(96f, -170f);
                card.sizeDelta = new Vector2(1000f, 360f);
                Image backdrop = Child(card, "CardBackground", typeof(Image)).GetComponent<Image>();
                Stretch(backdrop.rectTransform);
                backdrop.color = new Color(.035f, .027f, .022f, .85f);
                backdrop.raycastTarget = false;
                TMP_Text titleLabel = Text(card, "Title", title, new Vector2(40f, -28f), new Vector2(920f, 80f), 46f);
                titleLabel.color = new Color(1f, .88f, .66f);
                TMP_Text detailLabel = Text(card, "Detail", detail, new Vector2(40f, -116f), new Vector2(920f, 126f), 26f);
                Button next = Button(card, "Continue", button, new Vector2(40f, -270f), new Vector2(340f, 66f));
                var serialized = new SerializedObject(panel);
                Set(serialized, "canvasGroup", root.GetComponent<CanvasGroup>());
                Set(serialized, "content", card);
                Set(serialized, "background", image);
                Set(serialized, "titleText", titleLabel);
                Set(serialized, "detailText", detailLabel);
                Set(serialized, "continueButton", next);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (typeof(T) == typeof(DateChangePanel)) next.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void CreateBusinessControls()
        {
            string path = Root + "/BusinessControlsPanel.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            GameObject root = new GameObject("BusinessControlsPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Canvas), typeof(GraphicRaycaster));
            try
            {
                Stretch(root.GetComponent<RectTransform>());
                ConfigureCanvas(root);
                var panel = root.AddComponent<BusinessControlsPanel>();
                RectTransform info = Child(root.transform, "BusinessInfo", typeof(Image)).GetComponent<RectTransform>();
                TopLeft(info, new Vector2(64f, -64f), new Vector2(620f, 130f));
                info.GetComponent<Image>().color = new Color(.035f, .027f, .022f, .8f);
                info.GetComponent<Image>().raycastTarget = false;
                Text(info, "Title", "经营阶段 · FreeCafe", new Vector2(24f, -16f), new Vector2(570f, 42f), 30f);
                Text(info, "Detail", "制作、接客完成后，可以结束营业进入闲暇。", new Vector2(24f, -70f), new Vector2(570f, 50f), 22f);
                Button end = Button(root.transform, "EndBusiness", "结束营业并进入闲暇", Vector2.zero, new Vector2(380f, 76f));
                RectTransform rect = end.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-64f, 170f);
                var serialized = new SerializedObject(panel);
                Set(serialized, "endBusinessButton", end);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void ConfigureCanvas(GameObject root)
        {
            // The scene-owned FreeCafeHUD uses order 600. These panels must cover it.
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 700;
        }

        private static GameObject Child(Transform parent, string name, params Type[] components)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            foreach (Type type in components) go.AddComponent(type);
            return go;
        }
        private static TMP_Text Text(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize)
        {
            var label = Child(parent, name, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            TopLeft(label.rectTransform, position, size);
            label.font = Font;
            label.fontSize = fontSize;
            label.text = text;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false;
            return label;
        }
        private static Button Button(Transform parent, string name, string text, Vector2 position, Vector2 size)
        {
            GameObject go = Child(parent, name, typeof(Image), typeof(Button));
            TopLeft(go.GetComponent<RectTransform>(), position, size);
            Image image = go.GetComponent<Image>();
            image.color = new Color(.38f, .22f, .10f, .98f);
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var label = Text(go.transform, "Label", text, Vector2.zero, size, 25f);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
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
            => serialized.FindProperty(name).objectReferenceValue = value;
    }
}
