using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>Code-created, blocking avatar modal. No prefab or data-table dependency.</summary>
    public sealed class PlayerAvatarSelection : MonoBehaviour
    {
        private Action finished;
        public bool IsPlaying { get; private set; }

        public static PlayerAvatarSelection Create(Transform owner)
        {
            var root = new GameObject("PlayerAvatarSelection", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PlayerAvatarSelection));
            root.transform.SetParent(owner, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            PlayerAvatarSelection modal = root.GetComponent<PlayerAvatarSelection>();
            modal.Build(root.GetComponent<RectTransform>());
            return modal;
        }

        public void Play(Action onFinished)
        {
            finished = onFinished;
            IsPlaying = true;
            gameObject.SetActive(true);
        }

        /// <summary>Public button/test entry point. Choosing changes only the portrait.</summary>
        public void Select(AvatarKind kind)
        {
            if (!IsPlaying) return;
            PlayerDetailsProfile.Current.SelectAvatar(kind);
            Action callback = finished;
            Stop();
            callback?.Invoke();
        }

        public void Stop()
        {
            IsPlaying = false;
            finished = null;
            gameObject.SetActive(false);
        }

        private void OnDestroy() { finished = null; IsPlaying = false; }

        private void Build(RectTransform root)
        {
            RectTransform backdrop = Rect("Backdrop", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backdrop.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .82f);
            RectTransform card = Rect("Card", backdrop, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(860, 580));
            card.gameObject.AddComponent<Image>().color = new Color(.13f, .12f, .11f, 1);
            Label("Title", card, "选择你的头像", new Vector2(0, 215), new Vector2(780, 70), 42);
            Label("Hint", card, "男女仅影响头像", new Vector2(0, 150), new Vector2(780, 50), 24);
            Choice(card, AvatarKind.Male, "男", -190);
            Choice(card, AvatarKind.Female, "女", 190);
        }

        private void Choice(RectTransform parent, AvatarKind kind, string label, float x)
        {
            RectTransform rect = Rect(kind.ToString(), parent, Vector2.one * .5f, Vector2.one * .5f,
                new Vector2(x, -45), new Vector2(300, 310));
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.26f, .24f, .21f, 1);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => Select(kind));
            RectTransform portrait = Rect("Silhouette", rect, Vector2.one * .5f, Vector2.one * .5f,
                new Vector2(0, 30), new Vector2(200, 220));
            DetailsDisplayConfig config = Resources.Load<DetailsDisplayConfig>("DetailsDisplayConfig");
            Sprite sprite = config == null ? null : (kind == AvatarKind.Female ? config.femaleAvatar : config.maleAvatar);
            if (sprite == null) sprite = Resources.Load<Sprite>("Art/UI/Avatars/" + kind);
            if (sprite != null)
            {
                Image graphic = portrait.gameObject.AddComponent<Image>();
                graphic.sprite = sprite;
                graphic.preserveAspect = true;
                graphic.raycastTarget = false;
            }
            else
            {
                PlayerAvatarSilhouette graphic = portrait.gameObject.AddComponent<PlayerAvatarSilhouette>();
                graphic.Kind = kind;
                graphic.color = new Color(.84f, .79f, .68f, 1);
                graphic.raycastTarget = false;
            }
            Label("Label", rect, label, new Vector2(0, -115), new Vector2(280, 50), 30);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void Label(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
        {
            RectTransform rect = Rect(name, parent, Vector2.one * .5f, Vector2.one * .5f, position, size);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = Resources.Load<TMP_FontAsset>("Fonts/乐米曲奇方块体 SDF") ?? TMP_Settings.defaultFontAsset;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(.94f, .88f, .76f, 1);
            label.raycastTarget = false;
        }
    }

    /// <summary>Temporary portrait geometry until authored avatar art is provided.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PlayerAvatarSilhouette : MaskableGraphic
    {
        [SerializeField] private AvatarKind kind;
        public AvatarKind Kind
        {
            get => kind;
            set { kind = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float unit = Mathf.Min(rect.width, rect.height);
            Vector2 center = rect.center;
            if (kind == AvatarKind.Female)
                Ellipse(mesh, center + Vector2.up * unit * .10f, unit * .23f, unit * .30f);
            Ellipse(mesh, center + Vector2.down * unit * .29f,
                unit * (kind == AvatarKind.Male ? .38f : .31f), unit * .24f);
            Ellipse(mesh, center + Vector2.up * unit * .17f, unit * .17f, unit * .20f);
        }

        private void Ellipse(VertexHelper mesh, Vector2 center, float width, float height)
        {
            const int segments = 48;
            int start = mesh.currentVertCount;
            mesh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle) * width, Mathf.Sin(angle) * height), color, Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}
