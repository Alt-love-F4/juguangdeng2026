using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TextCore.LowLevel;
using Spotlight.GameFlow;

namespace Spotlight.GameFlow.Editor
{
    public static class NoticePanelPrefabRebuilder
    {
        private const string PrefabPath = "Assets/Resources/Art/UIPrefab/NoticePanel.prefab";
        private const string ReferencePath = "Assets/Art/Gameplay/报纸通知（改后）.prefab";
        private const string FontPath = "Assets/Resources/Fonts/SILVER(for 报纸通知）.asset";
        private const string FontSourcePath = "Assets/Resources/Fonts/SILVER(for 报纸通知）.TTF";
        private const float WorldWidth = 40.96f;
        private const float WorldHeight = 23.04f;
        private const float CanvasWidth = 1920f;
        private const float CanvasHeight = 1080f;

        [MenuItem("Tools/Game Flow/Rebuild Notice Panel (Newspaper UGUI)")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before rebuilding NoticePanel.");

            GameObject reference = PrefabUtility.LoadPrefabContents(ReferencePath);
            try
            {
                TMP_FontAsset font = EnsureFont();
                if (font == null) throw new InvalidOperationException("Could not create TMP font asset from " + FontSourcePath);

                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                    AssetDatabase.DeleteAsset(PrefabPath);

                var root = new GameObject("NoticePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Canvas), typeof(GraphicRaycaster));
                try
                {
                    var rootRect = root.GetComponent<RectTransform>();
                    root.transform.localScale = Vector3.one;
                    root.transform.localPosition = Vector3.zero;
                    rootRect.localRotation = Quaternion.identity;
                    Stretch(rootRect);
                    var canvas = root.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 700;
                    var scaler = root.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(CanvasWidth, CanvasHeight);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = .5f;

                    Image blocker = MakeImage(root.transform, "InputBlocker", Vector2.zero, Vector2.one, new Color(.035f, .025f, .02f, .42f));
                    blocker.raycastTarget = true;

                    Transform artwork = new GameObject("NewspaperArtwork", typeof(RectTransform)).transform;
                    artwork.SetParent(root.transform, false);
                    artwork.localScale = Vector3.one;
                    Stretch(artwork as RectTransform);
                    int order = 0;
                    foreach (var sr in reference.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        if (sr.sprite == null) continue;
                        Image image = MakeImage(artwork, sr.gameObject.name, Vector2.zero, Vector2.zero, Color.white);
                        image.sprite = sr.sprite;
                        image.raycastTarget = false;
                        var rect = image.rectTransform;
                        Vector3 p = sr.transform.position;
                        Vector3 s = sr.transform.localScale;
                        rect.anchorMin = rect.anchorMax = new Vector2(p.x / WorldWidth, p.y / WorldHeight);
                        rect.pivot = new Vector2(.5f, .5f);
                        rect.anchoredPosition = Vector2.zero;
                        rect.sizeDelta = new Vector2(sr.sprite.rect.width / sr.sprite.pixelsPerUnit * CanvasWidth / WorldWidth * s.x,
                            sr.sprite.rect.height / sr.sprite.pixelsPerUnit * CanvasHeight / WorldHeight * s.y);
                        image.transform.SetSiblingIndex(order++);
                    }

                    Transform content = new GameObject("DynamicContent", typeof(RectTransform)).transform;
                    content.SetParent(root.transform, false);
                    content.localScale = Vector3.one;
                    Stretch(content as RectTransform);

                    // Central newspaper page is the editable/dynamic page from the reference composition.
                    TMP_Text title = MakeText(content, "Title", "事件通知", new Vector2(585, 745), new Vector2(750, 82), font, 54, TextAlignmentOptions.Center);
                    TMP_Text time = MakeText(content, "Time", "星期三，xxxx年x月x日", new Vector2(585, 695), new Vector2(750, 42), font, 25, TextAlignmentOptions.Center);
                    TMP_Text body = MakeText(content, "Body", "", new Vector2(585, 420), new Vector2(750, 200), font, 34, TextAlignmentOptions.TopLeft);
                    body.enableWordWrapping = true;
                    body.overflowMode = TextOverflowModes.Overflow;
                    TMP_Text progressLabel = MakeText(content, "ProgressLabel", "", new Vector2(585, 175), new Vector2(750, 32), font, 22, TextAlignmentOptions.Center);
                    TMP_Text hint = MakeText(content, "Hint", "空格 / 回车 / 点击确认关闭", new Vector2(585, 48), new Vector2(750, 32), font, 20, TextAlignmentOptions.Center);

                    RawImage noticeImage = MakeRawImage(content, "NoticeImage", new Vector2(720, 455), new Vector2(480, 205));
                    noticeImage.color = Color.white;
                    var fitter = noticeImage.gameObject.AddComponent<AspectRatioFitter>();
                    fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

                    Slider progress = MakeProgress(content, "ProgressBar", new Vector2(755, 135), new Vector2(410, 12));
                    progress.gameObject.SetActive(false);
                    Button confirm = MakeButton(content, "Confirm", "确认", new Vector2(850, 78), new Vector2(220, 52), font);
                    Button cancel = MakeButton(root.transform, "Cancel", "", new Vector2(0, 0), Vector2.zero, font);
                    cancel.gameObject.SetActive(false);

                    var panel = root.AddComponent<NoticePanel>();
                    var serialized = new SerializedObject(panel);
                    Set(serialized, "canvasGroup", root.GetComponent<CanvasGroup>());
                    Set(serialized, "noticeImage", noticeImage);
                    Set(serialized, "titleText", title);
                    Set(serialized, "timeText", time);
                    Set(serialized, "bodyText", body);
                    Set(serialized, "progressLabel", progressLabel);
                    Set(serialized, "progressBar", progress);
                    Set(serialized, "hintText", hint);
                    Set(serialized, "confirmButton", confirm);
                    Set(serialized, "cancelButton", cancel);
                    Set(serialized, "enterSeconds", .2f);
                    Set(serialized, "exitSeconds", .15f);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    root.GetComponent<CanvasGroup>().alpha = 0f;
                    root.GetComponent<CanvasGroup>().blocksRaycasts = false;
                    root.GetComponent<CanvasGroup>().interactable = false;
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                    Debug.Log("[GameFlow] Rebuilt NoticePanel as newspaper UGUI/TMP layout.");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            finally { PrefabUtility.UnloadPrefabContents(reference); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static TMP_FontAsset EnsureFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (existing != null && existing.atlasTextures != null && existing.atlasTextures.Length > 0 && existing.atlasTextures[0] != null)
                return existing;
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(FontPath);
                AssetDatabase.Refresh();
            }
            var source = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
            if (source == null) return null;
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (font == null) return null;
            AssetDatabase.CreateAsset(font, FontPath);
            if (font.material != null) AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures)
                if (atlas != null) AssetDatabase.AddObjectToAsset(atlas, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        }

        private static void Set(SerializedObject so, string name, UnityEngine.Object value) { so.FindProperty(name).objectReferenceValue = value; }
        private static void Set(SerializedObject so, string name, float value) { so.FindProperty(name).floatValue = value; }

        private static Image MakeImage(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.color = color;
            var rect = image.rectTransform; rect.anchorMin = min; rect.anchorMax = max;
            if (min != max) rect.offsetMin = rect.offsetMax = Vector2.zero;
            return image;
        }
        private static RawImage MakeRawImage(Transform parent, string name, Vector2 center, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)); go.transform.SetParent(parent, false);
            var image = go.GetComponent<RawImage>(); Position(image.rectTransform, center, size); image.raycastTarget = false; return image;
        }
        private static TMP_Text MakeText(Transform parent, string name, string value, Vector2 center, Vector2 size, TMP_FontAsset font, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>(); Position(text.rectTransform, center, size); text.font = font; text.fontSize = fontSize; text.text = value; text.color = new Color(.25f, .12f, .07f, 1f); text.alignment = alignment; text.raycastTarget = false; text.enableWordWrapping = true; return text;
        }
        private static Slider MakeProgress(Transform parent, string name, Vector2 center, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider)); go.transform.SetParent(parent, false); var s = go.GetComponent<Slider>(); Position(s.transform as RectTransform, center, size); return s;
        }
        private static Button MakeButton(Transform parent, string name, string label, Vector2 center, Vector2 size, TMP_FontAsset font)
        {
            var image = MakeImage(parent, name, Vector2.zero, Vector2.zero, new Color(.26f, .13f, .08f, .96f)); Position(image.rectTransform, center, size); var b = image.gameObject.AddComponent<Button>(); b.targetGraphic = image;
            if (!string.IsNullOrEmpty(label)) { var text = MakeText(image.transform, "Label", label, Vector2.zero, size, font, 24, TextAlignmentOptions.Center); text.color = new Color(.95f, .85f, .67f, 1f); }
            return b;
        }
        private static void Position(RectTransform rect, Vector2 center, Vector2 size) { rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = new Vector2(center.x - CanvasWidth * .5f, center.y - CanvasHeight * .5f); rect.sizeDelta = size; }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
