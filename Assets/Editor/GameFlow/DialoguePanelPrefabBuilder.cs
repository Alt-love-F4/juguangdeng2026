using System;
using Spotlight.GameFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>只生成缺失资产，绝不覆盖美术编辑过的预制体/剧本或修改源图片导入设置。</summary>
    [InitializeOnLoad]
    public static class DialoguePanelPrefabBuilder
    {
        private const string PrefabPath = "Assets/Resources/Art/UIPrefab/DialoguePanel.prefab";
        private const string SamplePath = "Assets/Resources/Dialogue/DialoguePreview.asset";
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/SimHei SDF.asset");
        private static readonly Color Gold = new Color(1f, .88f, .66f);

        static DialoguePanelPrefabBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode
                    && (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null
                        || AssetDatabase.LoadAssetAtPath<DialogueScriptAsset>(SamplePath) == null))
                    CreateMissing();
            };
        }

        [MenuItem("Tools/Game Flow/Create Missing Dialogue Assets")]
        public static void CreateMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before creating dialogue assets.");
            if (Font == null) throw new InvalidOperationException("SimHei SDF font is missing.");
            Sprite left = Portrait("角色设计_女咖啡馆店主_法式线描");
            Sprite right = Portrait("角色设计_男革命印刷工_法式线描");
            Texture2D background = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Resources/Art/Gameplay/DayFlow/phase_business_transition.png");
            if (background == null) throw new InvalidOperationException("Missing cafe background.");
            CreatePanel(left, right, background);
            CreateSample(left, right, background);
            AssetDatabase.SaveAssets();
            Debug.Log("[Dialogue] Missing dialogue assets created; existing assets preserved.");
        }

        private static Sprite Portrait(string name)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Gameplay/Characters/" + name + ".png"))
                if (asset is Sprite sprite) return sprite;
            throw new InvalidOperationException("Missing imported portrait sprite: " + name);
        }

        private static void CreatePanel(Sprite left, Sprite right, Texture2D texture)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
            GameObject root = new GameObject("DialoguePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Canvas), typeof(GraphicRaycaster));
            try
            {
                Rect(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 800;
                DialoguePanel panel = root.AddComponent<DialoguePanel>();
                RawImage background = Child(root.transform, "Background", typeof(RawImage)).GetComponent<RawImage>();
                Rect(background.rectTransform, Vector2.zero, Vector2.one);
                background.texture = texture;
                background.raycastTarget = false;
                Image shade = Image(root.transform, "InputBlocker", Vector2.zero, Vector2.one, new Color(.035f, .027f, .022f, .48f));
                shade.raycastTarget = true;
                Image header = Image(root.transform, "Header", new Vector2(0f, .91f), Vector2.one, new Color(.035f, .027f, .022f, .88f));
                TMP_Text title = Text(header.transform, "Title", "咖啡馆 · 不期而至的访客", new Vector2(.035f, .12f), new Vector2(.75f, .88f), 30f, Gold);
                Button cancel = Button(header.transform, "Cancel", "Esc · 退出", new Vector2(.83f, .15f), new Vector2(.965f, .85f));
                Image leftImage = Character(root.transform, "LeftPortrait", left, .08f, .40f);
                Image rightImage = Character(root.transform, "RightPortrait", right, .60f, .92f);
                Image box = Image(root.transform, "DialogueBox", new Vector2(.065f, .055f), new Vector2(.935f, .325f), new Color(.035f, .027f, .022f, .96f));
                Image rule = Image(box.transform, "GoldRule", new Vector2(0f, .985f), Vector2.one, Gold);
                rule.raycastTarget = false;
                TMP_Text speaker = Text(box.transform, "Speaker", "咖啡馆店主", new Vector2(.025f, .77f), new Vector2(.68f, .96f), 29f, Gold);
                TMP_Text progress = Text(box.transform, "Progress", "1 / 7", new Vector2(.75f, .79f), new Vector2(.975f, .94f), 19f, new Color(.72f, .67f, .57f));
                progress.alignment = TextAlignmentOptions.MidlineRight;
                TMP_Text dialogue = Text(box.transform, "Dialogue", "欢迎光临。", new Vector2(.025f, .23f), new Vector2(.975f, .75f), 28f, new Color(.96f, .94f, .87f));
                dialogue.overflowMode = TextOverflowModes.Page;
                dialogue.richText = false;
                TMP_Text hint = Text(box.transform, "Hint", "空格 / 回车 / 点击 · 继续", new Vector2(.025f, .035f), new Vector2(.65f, .19f), 19f, new Color(.72f, .67f, .57f));
                Button next = Button(box.transform, "Continue", "继续  >", new Vector2(.81f, .035f), new Vector2(.975f, .20f));
                var serialized = new SerializedObject(panel);
                Set(serialized, "canvasGroup", root.GetComponent<CanvasGroup>());
                Set(serialized, "background", background);
                Set(serialized, "leftPortrait", leftImage);
                Set(serialized, "rightPortrait", rightImage);
                Set(serialized, "dialogueBox", box.rectTransform);
                Set(serialized, "titleText", title);
                Set(serialized, "speakerText", speaker);
                Set(serialized, "dialogueText", dialogue);
                Set(serialized, "progressText", progress);
                Set(serialized, "hintText", hint);
                Set(serialized, "continueButton", next);
                Set(serialized, "cancelButton", cancel);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Image Character(Transform parent, string name, Sprite sprite, float minX, float maxX)
        {
            Image frame = Image(parent, name + "Frame", new Vector2(minX - .004f, .30f), new Vector2(maxX + .004f, .885f), new Color(.23f, .16f, .10f, .95f));
            frame.raycastTarget = false;
            Image portrait = Image(parent, name, new Vector2(minX, .307f), new Vector2(maxX, .878f), Color.white);
            portrait.sprite = sprite;
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            return portrait;
        }

        private static void CreateSample(Sprite left, Sprite right, Texture2D background)
        {
            if (AssetDatabase.LoadAssetAtPath<DialogueScriptAsset>(SamplePath) != null) return;
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Dialogue"))
                AssetDatabase.CreateFolder("Assets/Resources", "Dialogue");
            var sample = ScriptableObject.CreateInstance<DialogueScriptAsset>();
            var serialized = new SerializedObject(sample);
            serialized.FindProperty("title").stringValue = "咖啡馆 · 不期而至的访客";
            Set(serialized, "background", background);
            CharacterData(serialized.FindProperty("leftCharacter"), "咖啡馆店主", left);
            CharacterData(serialized.FindProperty("rightCharacter"), "印刷工", right);
            string[] text =
            {
                "傍晚的最后一缕光穿过玻璃。门铃响起，一位带着油墨气味的客人走进咖啡馆。",
                "欢迎光临。今天想喝点什么？",
                "一杯热咖啡，不加糖。还有……我想在这里等一个人。",
                "当然。靠窗的位置还空着，你可以慢慢等。",
                "谢谢。有些话，只有在这样安静的地方才说得出口。",
                "城里的印刷机已经停了三天，街上却比往常更加热闹。有人在寻找一封没有署名的信，也有人只想找个地方坐下来。\n\n我原本以为今天只是普通的一天，直到他把那张折了四次的纸放在桌上。纸边沾着油墨，字迹却非常清楚。那不是一张订单，而是一份名单。\n\n我没有问名单上是谁。他也没有解释。门外又传来一阵脚步声，我们同时看向窗边。\n\n有些故事，从一杯咖啡开始，却不会在咖啡凉下来时结束。",
                "那就先喝一口吧。无论外面发生什么，这杯咖啡都还是热的。"
            };
            DialogueSpeaker[] speakers = { DialogueSpeaker.Narrator, DialogueSpeaker.Left, DialogueSpeaker.Right,
                DialogueSpeaker.Left, DialogueSpeaker.Right, DialogueSpeaker.Narrator, DialogueSpeaker.Left };
            SerializedProperty lines = serialized.FindProperty("lines");
            lines.arraySize = text.Length;
            for (int i = 0; i < text.Length; i++)
            {
                SerializedProperty line = lines.GetArrayElementAtIndex(i);
                line.FindPropertyRelative("speaker").enumValueIndex = (int)speakers[i];
                line.FindPropertyRelative("text").stringValue = text[i];
                line.FindPropertyRelative("gesture").enumValueIndex = i == 3 ? (int)DialogueGesture.Nod : (int)DialogueGesture.None;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(sample, SamplePath);
        }

        private static void CharacterData(SerializedProperty property, string name, Sprite sprite)
        {
            property.FindPropertyRelative("displayName").stringValue = name;
            property.FindPropertyRelative("portrait").objectReferenceValue = sprite;
        }
        private static void Set(SerializedObject serialized, string name, Object value)
            => serialized.FindProperty(name).objectReferenceValue = value;
        private static GameObject Child(Transform parent, string name, params Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            foreach (Type type in components) go.AddComponent(type);
            return go;
        }
        private static void Rect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static Image Image(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var image = Child(parent, name, typeof(Image)).GetComponent<Image>();
            Rect(image.rectTransform, min, max);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
        private static TMP_Text Text(Transform parent, string name, string value, Vector2 min, Vector2 max, float size, Color color)
        {
            var text = Child(parent, name, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            Rect(text.rectTransform, min, max);
            text.font = Font;
            text.fontSize = size;
            text.text = value;
            text.color = color;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.TopLeft;
            return text;
        }
        private static Button Button(Transform parent, string name, string label, Vector2 min, Vector2 max)
        {
            Image image = Image(parent, name, min, max, new Color(.18f, .12f, .075f, .98f));
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            TMP_Text text = Text(image.transform, "Label", label, new Vector2(.02f, .02f), new Vector2(.98f, .98f), 21f, Gold);
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}

