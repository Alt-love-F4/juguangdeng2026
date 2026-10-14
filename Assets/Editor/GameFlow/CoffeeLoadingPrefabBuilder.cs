#if UNITY_EDITOR
using Spotlight.GameFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>Explicit artwork setup; never runs automatically over artist edits.</summary>
    public static class CoffeeLoadingPrefabBuilder
    {
        private const string Root = "Assets/Resources/Art/UIPrefab/";
        private const string Art = "Assets/Art/Gameplay/loadingUI/UI_加载背景/";

        [MenuItem("Tools/Game Flow/Apply Coffee Long Loading Artwork")]
        public static void ApplyLongLoading()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Root + "LoadingPanel.prefab");
            try
            {
                ApplyLongArtwork(root);
                PrefabUtility.SaveAsPrefabAsset(root, Root + "LoadingPanel.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        public static void ApplyLongArtwork(GameObject root)
        {
                var panel = root.GetComponent<LoadingPanel>();
                var background = root.transform.Find("Background").GetComponent<Image>();
                background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "图层 2.png");
                background.color = new Color(.72f, .63f, .56f, 1f);
                background.raycastTarget = true;
                var cup = root.transform.Find("Panel").GetComponent<Image>();
                cup.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "咖啡logo-大.png");
                cup.color = new Color(.24f, .24f, .24f, 1f);
                cup.preserveAspect = true;
                cup.raycastTarget = false;
                Rect(cup.rectTransform, new Vector2(.5f, .59f), new Vector2(.5f, .59f));
                cup.rectTransform.sizeDelta = new Vector2(480f, 330f);
                Image fill = ImageChild(cup.transform, "CoffeeFill");
                Rect(fill.rectTransform, Vector2.zero, Vector2.one);
                fill.sprite = cup.sprite;
                fill.color = Color.white;
                fill.preserveAspect = true;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Vertical;
                fill.fillOrigin = (int)Image.OriginVertical.Bottom;
                fill.fillAmount = 0f;
                root.transform.Find("LoadingLogo").gameObject.SetActive(false);
                var title = root.transform.Find("LoadingTitleText").GetComponent<TMP_Text>();
                Rect(title.rectTransform, new Vector2(.2f, .79f), new Vector2(.8f, .87f));
                title.text = "正在调制下一段旅程";
                title.fontSize = 34f;
                title.color = new Color(1f, .9f, .75f);
                title.raycastTarget = false;
                var tip = root.transform.Find("LoadingTipText").GetComponent<TMP_Text>();
                Rect(tip.rectTransform, new Vector2(.18f, .16f), new Vector2(.82f, .23f));
                tip.fontSize = 23f;
                tip.color = new Color(1f, .88f, .73f);
                tip.raycastTarget = false;
                var bar = root.transform.Find("ProgressBar").GetComponent<RectTransform>();
                Rect(bar, new Vector2(.32f, .32f), new Vector2(.68f, .333f));
                var track = bar.Find("Track").GetComponent<Image>();
                track.sprite = null;
                track.color = new Color(.2f, .075f, .035f, 1f);
                track.raycastTarget = false;
                var barFill = bar.Find("Fill").GetComponent<Image>();
                Rect(barFill.rectTransform, Vector2.zero, new Vector2(0f, 1f));
                barFill.sprite = null;
                barFill.color = new Color(1f, .83f, .59f, 1f);
                barFill.raycastTarget = false;
                Transform existing = root.transform.Find("PercentageText");
                TMP_Text percentage = existing != null ? existing.GetComponent<TMP_Text>() : new GameObject("PercentageText", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                percentage.transform.SetParent(root.transform, false);
                Rect(percentage.rectTransform, new Vector2(.4f, .25f), new Vector2(.6f, .305f));
                percentage.font = title.font;
                percentage.fontSize = 32f;
                percentage.alignment = TextAlignmentOptions.Center;
                percentage.color = title.color;
                percentage.text = "0%";
                percentage.raycastTarget = false;
                var so = new SerializedObject(panel);
                so.FindProperty("coffeeFill").objectReferenceValue = fill;
                so.FindProperty("percentageText").objectReferenceValue = percentage;
                so.FindProperty("logo").objectReferenceValue = cup.rectTransform;
                so.FindProperty("progressFill").objectReferenceValue = barFill.rectTransform;
                so.FindProperty("showOffset").floatValue = 0f;
                so.FindProperty("hideOffset").floatValue = 0f;
                so.FindProperty("showStartScale").floatValue = 1f;
                so.FindProperty("hideEndScale").floatValue = 1f;
                so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/Game Flow/Create Coffee Short Loading Panel")]
        public static void CreateShortLoading()
        {
            const string materialPath = "Assets/Resources/UI/CoffeeTransitionUI.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Spotlight/UI/Coffee Transition"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BackgroundTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "图层 2.png"));
            material.SetTexture("_CupTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "咖啡logo-大.png"));
            material.SetVector("_CupGrid", new Vector4(8f, 7f, 0f, 0f));
            material.SetFloat("_CupScale", .46f);
            material.SetVector("_CupJitter", Vector4.zero);
            material.SetFloat("_Transition", 0f);
            material.SetFloat("_Disappearing", 0f);
            EditorUtility.SetDirty(material);
            string path = Root + "ShortLoadingPanel.prefab";
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null
                ? PrefabUtility.LoadPrefabContents(path)
                : new GameObject("ShortLoadingPanel", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                Rect(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
                var image = ImageChild(root.transform, "CoffeeTransition");
                Rect(image.rectTransform, Vector2.zero, Vector2.one);
                image.sprite = null;
                image.color = Color.white;
                image.raycastTarget = true;
                image.material = material;
                var panel = root.GetComponent<ShortLoadingPanel>() ?? root.AddComponent<ShortLoadingPanel>();
                var so = new SerializedObject(panel);
                so.FindProperty("transitionImage").objectReferenceValue = image;
                so.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
                so.FindProperty("modal").boolValue = true;
                so.FindProperty("coverDuration").floatValue = .65f;
                so.FindProperty("disappearDuration").floatValue = .65f;
                so.FindProperty("movementSpeed").floatValue = .08f;
                so.FindProperty("movementDirection").vector2Value = Vector2.one;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
        }

        private static Image ImageChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.GetComponent<Image>();
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static void Rect(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.localScale = Vector3.one;
        }
    }
}
#endif
