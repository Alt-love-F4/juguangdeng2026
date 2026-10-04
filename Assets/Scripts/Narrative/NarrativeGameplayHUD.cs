using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.Narrative
{
    /// <summary>Minimal gameplay-only sample controls for validating the runtime contract.</summary>
    public sealed class NarrativeGameplayHUD : MonoBehaviour
    {
        private NarrativeRuntime runtime;
        private TMP_Text status;
        private GameObject root;
        private Button tea, rest;
        private bool visible;
        public System.Func<bool> CanInteract { get; set; }
        private bool AllowsAction => visible && runtime != null && runtime.IsReady && !runtime.BlocksGameplayInput && Time.timeScale > 0 && (CanInteract?.Invoke() ?? true);

        public static NarrativeGameplayHUD Create(NarrativeRuntime owner)
        {
            GameObject go = new GameObject("NarrativeGameplayHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(NarrativeGameplayHUD));
            DontDestroyOnLoad(go);
            NarrativeGameplayHUD hud = go.GetComponent<NarrativeGameplayHUD>();
            hud.runtime = owner;
            hud.Build();
            owner.BarChanged += hud.OnBarChanged;
            owner.TurnCompleted += hud.Refresh;
            hud.SetVisible(false);
            return hud;
        }

        private void Build()
        {
            Canvas canvas = GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            CanvasScaler scaler = GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            root = new GameObject("GameplayControls", typeof(RectTransform)); root.transform.SetParent(transform, false);
            RectTransform rect = root.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(.2f, .78f); rect.anchorMax = new Vector2(.8f, .91f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            status = AddText(root.transform, "Status", new Vector2(0, .62f), new Vector2(1, 1), 26);
            tea = AddButton(root.transform, "喝茶  +1 信任", new Vector2(0, .08f), new Vector2(.48f, .53f)); tea.onClick.AddListener(DrinkTea);
            rest = AddButton(root.transform, "休息  ·  下一天", new Vector2(.52f, .08f), new Vector2(1, .53f)); rest.onClick.AddListener(Rest);
            Refresh();
        }

        private static TMP_Text AddText(Transform parent, string name, Vector2 min, Vector2 max, float size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            RectTransform r = go.GetComponent<RectTransform>(); r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero;
            TMP_Text t = go.GetComponent<TMP_Text>(); t.font = Resources.Load<TMP_FontAsset>("Fonts/SimHei SDF"); t.fontSize = size; t.color = Color.white; t.raycastTarget = false; return t;
        }

        private static Button AddButton(Transform parent, string label, Vector2 min, Vector2 max)
        {
            GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
            RectTransform r = go.GetComponent<RectTransform>(); r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(.08f, .12f, .18f, .96f);
            Button button = go.GetComponent<Button>();
            TMP_Text text = AddText(go.transform, "Label", Vector2.zero, Vector2.one, 24); text.alignment = TextAlignmentOptions.Center; text.text = label;
            return button;
        }

        private void DrinkTea()
        {
            if (!AllowsAction) return;
            runtime.BeginTurn();
            runtime.AddBar("npc_chen.trust", 1);
            runtime.EndTurn();
            Refresh();
        }

        private void Rest()
        {
            if (!AllowsAction) return;
            runtime.BeginTurn();
            runtime.EndTurn();
            Refresh();
        }

        public void SetVisible(bool value) { visible = value; if (root != null) root.SetActive(value); Refresh(); }
        private void OnBarChanged(string key, int oldValue, int newValue) => Refresh();
        private void Update() { tea.interactable = rest.interactable = AllowsAction; }
        private void OnDestroy()
        {
            if (runtime == null) return;
            runtime.BarChanged -= OnBarChanged;
            runtime.TurnCompleted -= Refresh;
        }

        public void Refresh()
        {
            if (status == null) return;
            int trust = 0; int day = 0;
            if (runtime != null && runtime.Controller != null)
            {
                runtime.Controller.State.TryGetBar("npc_chen.trust", out trust);
                runtime.Controller.State.TryGetBar("world.calendar", out day);
            }
            status.text = "示例玩法  |  信任: " + trust + "  天数: " + day;
        }
    }
}
