using System.Linq;
using Spotlight.FreeCafe;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Editor
{
    public static class CoffeeBookPanelBuilder
    {
        private const string SourcePath = "Assets/Art/Gameplay/\u5496\u5561\u9986/\u5496\u5561\u8c31ui.prefab";
        private const string PrefabPath = "Assets/Prefabs/FreeCafe/CoffeeBookPanel.prefab";
        private const string FontPath = "Assets/Resources/Fonts/SimHei SDF.asset";
        private const float PixelsPerUnit = 40f;

        public static void ApplySavedPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if(prefab != null) ApplyToCoffeeShop(prefab);
        }

        private sealed class Layer
        {
            public string Name;
            public Sprite Sprite;
            public Vector3 Position;
        }

        [MenuItem("Tools/Free Cafe/Validate Coffee Book Panel")]
        public static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if(prefab == null || source == null) throw new System.InvalidOperationException("Missing coffee book prefab.");
            var view = prefab.GetComponent<CoffeeBookPanelView>();
            if(view == null || view.artRoot == null || view.title == null || view.body == null || view.note == null || view.page == null
                || view.previous == null || view.next == null || view.close == null || view.marker == null || view.bodyScroll == null
                || view.entries.Length != 5 || view.entryLabels.Length != 5)
                throw new System.InvalidOperationException("Incomplete coffee book view references.");
            for(int index = 0; index < source.transform.childCount; index++)
                if(view.artRoot.GetChild(index).name != source.transform.GetChild(index).name)
                    throw new System.InvalidOperationException("PSD layer hierarchy/order changed.");
            if(prefab.GetComponentsInChildren<SpriteRenderer>(true).Length != 0
                || prefab.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length != 0
                || prefab.GetComponentsInChildren<Canvas>(true).Length != 0)
                throw new System.InvalidOperationException("Coffee book must use UGUI/TMP on the existing Canvas.");
            if(view.artRoot.Find("精粹咖啡").GetComponent<Image>().enabled || view.artRoot.Find("图层 1").GetComponent<Image>().enabled)
                throw new System.InvalidOperationException("PSD reference background or raster text is still visible.");
            Debug.Log("CoffeeBookPanel validation passed: PSD hierarchy/order, UGUI/TMP, serialized bindings and scroll viewport.");
        }

        [MenuItem("Tools/Free Cafe/Clear Coffee Book Preview")]
        public static void ClearPreview()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path != "Assets/Scenes/PresentationDebugScene.unity") return;
            var canvas = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "DebugRoomCanvas");
            if(canvas == null) return;
            var preview = canvas.transform.Find("CoffeeBookPreview");
            if(preview != null) Undo.DestroyObjectImmediate(preview.gameObject);
        }

        [MenuItem("Tools/Free Cafe/Capture Coffee Book Preview")]
        public static void CapturePreview()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path != "Assets/Scenes/PresentationDebugScene.unity") return;
            var canvasRoot = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "DebugRoomCanvas");
            var cameraRoot = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "DebugCamera");
            if(canvasRoot == null || cameraRoot == null) return;
            var canvas = canvasRoot.GetComponent<Canvas>();
            var camera = cameraRoot.GetComponent<Camera>();
            var mode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            float distance = canvas.planeDistance;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(1920, 1080, 24);
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            System.IO.Directory.CreateDirectory(".unity/capture");
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                image.Apply();
                System.IO.File.WriteAllBytes(".unity/capture/coffee-book-preview.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                canvas.renderMode = mode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = distance;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
                Canvas.ForceUpdateCanvases();
            }
        }

        [MenuItem("Tools/Free Cafe/Preview Coffee Book In Presentation Debug Scene")]
        public static void Preview()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path != "Assets/Scenes/PresentationDebugScene.unity")
            {
                Debug.LogWarning("Open the shared PresentationDebugScene before previewing CoffeeBookPanel.");
                return;
            }
            var canvas = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "DebugRoomCanvas");
            if(canvas == null) return;
            var previousPreview = canvas.transform.Find("CoffeeBookPreview");
            if(previousPreview != null) Undo.DestroyObjectImmediate(previousPreview.gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if(prefab == null) return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            instance.name = "CoffeeBookPreview";
            Undo.RegisterCreatedObjectUndo(instance, "Preview coffee book");
            instance.SetActive(true);
            instance.GetComponent<CoffeeBookPanelView>().ShowPreview();
            Selection.activeGameObject = instance;
        }

        [MenuItem("Tools/Free Cafe/Rebuild Coffee Book Panel")]
        public static void Rebuild()
        {
            if(EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode before rebuilding CoffeeBookPanel.");
                return;
            }
            var source = PrefabUtility.LoadPrefabContents(SourcePath);
            if(source == null)
            {
                Debug.LogError("CoffeeBookPanel source prefab not found: " + SourcePath);
                return;
            }
            var layers = source.transform.Cast<Transform>()
                .Select(child => new { Transform = child, Renderer = child.GetComponent<SpriteRenderer>() })
                .Where(item => item.Renderer != null && item.Renderer.sprite != null)
                .Select(item => new Layer { Name = item.Transform.name, Sprite = item.Renderer.sprite, Position = item.Transform.localPosition })
                .ToList();
            var origin = layers.First(layer => layer.Name == "图层 5").Position;
            PrefabUtility.UnloadPrefabContents(source);

            var root = new GameObject("CoffeeBookPanel", typeof(RectTransform), typeof(Image), typeof(CoffeeBookPanelView));
            root.layer = 5;
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;
            root.GetComponent<Image>().color = new Color(.08f, .035f, .015f, .42f);
            var art = new GameObject("咖啡谱ui", typeof(RectTransform));
            art.layer = 5;
            art.transform.SetParent(root.transform, false);
            var artRect = art.GetComponent<RectTransform>();
            artRect.sizeDelta = new Vector2(1467.2f, 599.6f);
            rootRect.pivot = new Vector2(.5f, .5f);
            foreach(var layer in layers)
            {
                var imageObject = new GameObject(layer.Name, typeof(RectTransform), typeof(Image));
                imageObject.layer = 5;
                imageObject.transform.SetParent(art.transform, false);
                var imageRect = imageObject.GetComponent<RectTransform>();
                imageRect.anchorMin = imageRect.anchorMax = new Vector2(.5f, .5f);
                imageRect.pivot = layer.Sprite.pivot / layer.Sprite.rect.size;
                imageRect.anchoredPosition = (layer.Position - origin) * PixelsPerUnit;
                imageRect.sizeDelta = layer.Sprite.bounds.size * PixelsPerUnit;
                var image = imageObject.GetComponent<Image>();
                image.sprite = layer.Sprite;
                image.raycastTarget = false;
                if(layer.Name == "图层 1" || layer.Name == "精粹咖啡") image.enabled = false;
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var title = Text(art.transform.Find("精粹咖啡"), "CoffeeBookTitle", "精粹咖啡", font, new Vector2(-36f, 237f), new Vector2(430f, 48f), 30f, TextAlignmentOptions.Center);
            title.color = new Color(1f, .96f, .87f);
            title.fontStyle = FontStyles.Bold;
            var body = Text(art.transform, "CoffeeBookBody", "", font, new Vector2(135f, -15f), new Vector2(550f, 410f), 23f, TextAlignmentOptions.TopLeft);
            body.color = new Color(.19f, .12f, .07f);
            body.textWrappingMode = TextWrappingModes.Normal;
            var note = Text(art.transform.Find("图层 8"), "BookNote", "注意", font, Vector2.zero, new Vector2(190f, 365f), 20f, TextAlignmentOptions.TopLeft);
            note.color = Color.white;
            art.transform.Find("图层 8").GetComponent<Image>().color = new Color(.55f, .5f, .45f);
            note.textWrappingMode = TextWrappingModes.Normal;
            AddButton(art.transform, "PreviousRecipe", "‹ 上一页", font, new Vector2(-60f, -255f), new Vector2(140f, 42f));
            AddButton(art.transform, "NextRecipe", "下一页 ›", font, new Vector2(340f, -255f), new Vector2(140f, 42f));
            var close = art.transform.Find("选项底图");
            close.GetComponent<RectTransform>().anchoredPosition = new Vector2(665f, -255f);
            var closeImage = close.GetComponent<Image>();
            closeImage.raycastTarget = true;
            close.gameObject.AddComponent<Button>().targetGraphic = closeImage;
            Text(close, "Label", "关闭", font, Vector2.zero, new Vector2(82f, 42f), 20f, TextAlignmentOptions.Center).color = Color.white;
            ConfigureView(root.GetComponent<CoffeeBookPanelView>(), art.transform, title, body, note, font);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            if(prefab == null) { Debug.LogError("CoffeeBookPanel prefab save failed."); return; }
            ApplyToCoffeeShop(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("CoffeeBookPanel rebuilt as UGUI from PSD layer prefab.");
        }

        private static void ConfigureView(CoffeeBookPanelView view, Transform art, TMP_Text title, TMP_Text body, TMP_Text note, TMP_FontAsset font)
        {
            view.artRoot = (RectTransform)art;
            view.title = title;
            view.body = body;
            view.note = note;
            view.previous = FindButton(art.gameObject, "PreviousRecipe");
            view.next = FindButton(art.gameObject, "NextRecipe");
            view.close = art.Find("选项底图").GetComponent<Button>();
            view.marker = (RectTransform)art.Find("选中标");
            view.page = Text(art, "RecipePage", "01 / 01", font, new Vector2(140f, -255f), new Vector2(170f, 42f), 18f, TextAlignmentOptions.Center);
            view.page.color = new Color(.35f, .18f, .08f);
            view.entries = new Button[5];
            view.entryLabels = new TMP_Text[5];
            var track = (RectTransform)art.Find("进度条");
            var trackImage = track.GetComponent<Image>();
            trackImage.enabled = false;
            var clip = new GameObject("TrackViewport", typeof(RectTransform), typeof(RectMask2D));
            clip.layer = 5;
            clip.transform.SetParent(track, false);
            ((RectTransform)clip.transform).sizeDelta = new Vector2(104f, 430f);
            var rule = new GameObject("TrackImage", typeof(RectTransform), typeof(Image));
            rule.layer = 5;
            rule.transform.SetParent(clip.transform, false);
            var ruleRect = (RectTransform)rule.transform;
            ruleRect.anchorMin = ruleRect.anchorMax = new Vector2(.5f, 1f);
            ruleRect.pivot = new Vector2(.5f, 1f);
            ruleRect.sizeDelta = track.sizeDelta;
            rule.GetComponent<Image>().sprite = trackImage.sprite;
            rule.GetComponent<Image>().raycastTarget = false;
            for(int index = 0; index < 5; index++)
            {
                var button = AddButton(art, "Recipe" + index, index == 0 ? "精粹咖啡" : "尚未解锁", font,
                    new Vector2(-268f, 181f - index * 83f), new Vector2(180f, 70f));
                view.entries[index] = button;
                view.entryLabels[index] = button.GetComponentInChildren<TMP_Text>();
                view.entryLabels[index].fontSize = 22f;
                view.entryLabels[index].alignment = TextAlignmentOptions.Left;
            }
            var viewport = new GameObject("RecipeViewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image), typeof(ScrollRect));
            viewport.layer = 5;
            viewport.transform.SetParent(art, false);
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.sizeDelta = body.rectTransform.sizeDelta;
            viewportRect.anchoredPosition = body.rectTransform.anchoredPosition;
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            body.transform.SetParent(viewport.transform, false);
            body.rectTransform.anchorMin = new Vector2(0f, 1f);
            body.rectTransform.anchorMax = Vector2.one;
            body.rectTransform.pivot = new Vector2(.5f, 1f);
            body.rectTransform.anchoredPosition = Vector2.zero;
            body.rectTransform.sizeDelta = new Vector2(0f, 410f);
            var layout = body.gameObject.AddComponent<ContentSizeFitter>();
            layout.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = body.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            view.bodyScroll = scroll;
            Text(art, "ScrollHint", "滚动查看完整配方", font, new Vector2(135f, -225f), new Vector2(450f, 20f), 14f, TextAlignmentOptions.Center).color = new Color(.4f, .25f, .14f, .75f);
            view.ShowPreview();
        }

        private static TMP_Text Text(Transform parent, string name, string value, TMP_FontAsset font, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var objectRoot = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            objectRoot.layer = 5;
            objectRoot.transform.SetParent(parent, false);
            var rect = objectRoot.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = objectRoot.GetComponent<TextMeshProUGUI>();
            text.text = value; text.font = font; text.fontSize = fontSize; text.alignment = alignment; text.raycastTarget = false;
            return text;
        }

        private static Button AddButton(Transform parent, string name, string label, TMP_FontAsset font, Vector2 position, Vector2 size)
        {
            var objectRoot = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            objectRoot.layer = 5;
            objectRoot.transform.SetParent(parent, false);
            var rect = objectRoot.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = objectRoot.GetComponent<Image>(); image.color = new Color(0f, 0f, 0f, .01f);
            var button = objectRoot.GetComponent<Button>(); button.targetGraphic = image;
            var text = Text(objectRoot.transform, "Label", label, font, Vector2.zero, size, 18f, TextAlignmentOptions.Center); text.color = new Color(.35f, .18f, .08f);
            return button;
        }

        private static void ApplyToCoffeeShop(GameObject prefab)
        {
            const string path = "Assets/Scenes/CoffeeShop.unity";
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if(!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                foreach(var sceneRoot in scene.GetRootGameObjects())
                {
                    var hud = sceneRoot.GetComponentInChildren<FreeCafeProductionHud>(true);
                    if(hud != null) ApplyToProduction(hud, prefab);
                    var playerHud = sceneRoot.GetComponentInChildren<FreeCafeHud>(true);
                    if(playerHud != null) ApplyToPlayer(playerHud, prefab);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if(!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ApplyToProduction(FreeCafeProductionHud hud, GameObject prefab)
        {
            if(hud == null || hud.bookPanel == null) return;
            var oldPanel = hud.bookPanel;
            var parent = oldPanel.transform.parent != null ? oldPanel.transform.parent : hud.transform;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = "CoffeeBookPanel";
            instance.transform.SetAsLastSibling();
            instance.SetActive(false);
            var view = instance.GetComponent<CoffeeBookPanelView>();
            hud.bookPanel = instance;
            hud.bookTitle = view.title;
            hud.bookBody = view.body;
            hud.bookNote = view.note;
            hud.previousRecipe = view.previous;
            hud.nextRecipe = view.next;
            hud.closeBook = view.close;
            hud.bookIcon = null;
            Object.DestroyImmediate(oldPanel);
            EditorUtility.SetDirty(hud);
        }

        private static void ApplyToPlayer(FreeCafeHud hud, GameObject prefab)
        {
            if(hud.recipePanel == null) return;
            var previous = hud.recipePanel;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, previous.transform.parent);
            instance.name = "CoffeeBookPanel";
            instance.transform.SetAsLastSibling();
            instance.SetActive(false);
            var view = instance.GetComponent<CoffeeBookPanelView>();
            hud.recipePanel = instance;
            hud.recipeTitle = view.title;
            hud.recipeBody = view.body;
            hud.recipeCloseButton = view.close;
            Object.DestroyImmediate(previous);
            EditorUtility.SetDirty(hud);
        }

        private static Button FindButton(GameObject root, string name) { var child = root.transform.Find(name); return child == null ? null : child.GetComponent<Button>(); }
    }
}
