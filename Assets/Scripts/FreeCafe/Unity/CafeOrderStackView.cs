using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>Presentation-only paper deck. Order selection and delivery remain owned by FreeCafeHud.</summary>
    public sealed class CafeOrderStackView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Serializable]
        public sealed class Card
        {
            public RectTransform root;
            public CanvasGroup group;
            public TMP_Text heading, label, details, number, patience;
            public Image avatar, drink, patienceFill;
        }

        public FreeCafeHud hud;
        public RectTransform cardRoot;
        public Card front, outgoing;
        public Card[] waiting;
        public CoffeeCompendiumPanel iconSource;
        public TMP_Text switchCaption;
        [Header("Motion")]
        [Min(.1f)] public float switchSeconds = .64f;
        [Min(.1f)] public float arrivalSeconds = .48f;
        public Vector2 stackOffset = new Vector2(12, 10);
        public float extractDistance = 116f;
        public bool IsAnimating => switching || arriving;

        private string shownId;
        private int shownCount, direction = 1;
        private float elapsed, hover;
        private bool switching, arriving, hovered;
        private CardData last;
        private struct CardData
        {
            public string id, heading, label, details, number, patience;
            public Sprite avatar, drink;
            public float fraction;
            public Color patienceColor;
        }

        private void Awake()
        {
            if (hud == null) hud = GetComponentInParent<FreeCafeHud>();
            if (iconSource == null && hud != null && hud.recipePanel != null)
                iconSource = hud.recipePanel.GetComponent<CoffeeCompendiumPanel>();
        }

        public void SetDirection(int value) => direction = value < 0 ? -1 : 1;

        public void Show(int firstIndex)
        {
            if (hud == null || hud.runtime == null || hud.runtime.World == null || front?.root == null) return;
            var state = hud.runtime.World.State;
            int count = state.requests.Count;
            firstIndex = count == 0 ? 0 : Mathf.Clamp(firstIndex, 0, count - 1);
            var data = Read(firstIndex, count);
            bool changed = shownId != data.id;
            if (changed && !string.IsNullOrEmpty(shownId) && count > 0 && isActiveAndEnabled)
            {
                // A dedicated pooled sheet keeps the old contents intact while the domain changes immediately.
                Apply(outgoing, last);
                outgoing.root.gameObject.SetActive(true);
                outgoing.root.SetAsLastSibling();
                switching = true;
                arriving = false;
                elapsed = 0;
            }
            else if (changed || count > shownCount)
            {
                switching = false;
                arriving = count > 0 && isActiveAndEnabled;
                elapsed = 0;
            }
            shownId = data.id;
            shownCount = count;
            last = data;
            // The existing HUD owns this cached avatar and its missing-portrait fallback contract.
            if (front.avatar == null) front.avatar = front.root.Find("CharacterAvatar")?.GetComponent<Image>();
            Apply(front, data);
            for (int i = 0; i < waiting.Length; i++)
            {
                bool visible = count > i + 1;
                waiting[i].root.gameObject.SetActive(visible);
                if (visible) Apply(waiting[i], Read((firstIndex + i + 1) % count, count));
            }
            if (switchCaption != null) switchCaption.text = count > 1 ? "切换订单" : count == 1 ? "等待更多来客" : "营业后开始接单";
            Pose();
        }

        private CardData Read(int index, int count)
        {
            if (count == 0) return new CardData { heading = "来客订单", label = "等待顾客入座", details = "先备好原料，等候第一位来客", number = "—", patience = "暂无订单", fraction = 0, patienceColor = new Color(.65f,.52f,.35f) };
            var world = hud.runtime.World;
            var request = world.State.requests[index];
            var product = world.Product(request.productId);
            var customer = world.Find(request.customerId);
            var character = customer?.person == null ? null : hud.runtime.Character(customer.person.characterId);
            bool tutorial = request.id == CafeWorld.TutorialOrderId;
            float fraction = tutorial ? 1 : Mathf.Clamp01(request.patience / Mathf.Max(1, world.Catalog.customerPatience));
            Sprite icon = null;
            if (iconSource != null)
                foreach (var recipe in iconSource.recipes)
                    if (recipe.productId == request.productId) { icon = recipe.icon; break; }
            return new CardData {
                id = request.id, heading = tutorial ? "练习订单" : "来客订单",
                label = (customer == null ? "顾客" : world.PersonName(customer)) + "\n<b>" + FreeCafeProductionText.Recipe(product.label) + "</b>",
                details = world.Spec(world.RequiredCupSpecId(request)).label + "   ·   " + product.price + " 王国币" + (request.remakes > 0 ? "  ·  重做" : ""),
                number = (index + 1).ToString("00"), avatar = character?.avatar, drink = icon,
                patience = tutorial ? "教程 · 不限时" : "等候  " + Mathf.CeilToInt(Mathf.Max(0, request.patience)) + " 秒",
                fraction = fraction, patienceColor = fraction > .5f ? new Color(.34f,.43f,.23f) : fraction > .25f ? new Color(.72f,.45f,.16f) : new Color(.67f,.22f,.14f)
            };
        }

        private static void Apply(Card card, CardData data)
        {
            if (card == null) return;
            if (card.heading != null) card.heading.text = data.heading;
            if (card.label != null) card.label.text = data.label;
            if (card.details != null) card.details.text = data.details;
            if (card.number != null) card.number.text = data.number;
            if (card.patience != null) { card.patience.text = data.patience; card.patience.color = data.patienceColor; }
            if (card.avatar != null) { card.avatar.sprite = data.avatar; card.avatar.gameObject.SetActive(data.avatar != null); }
            if (card.drink != null) { card.drink.sprite = data.drink; card.drink.gameObject.SetActive(data.drink != null); }
            if (card.patienceFill != null) { card.patienceFill.fillAmount = data.fraction; card.patienceFill.color = data.patienceColor; }
        }

        private void Update()
        {
            float previousHover = hover;
            hover = Mathf.MoveTowards(hover, hovered ? 1 : 0, Time.unscaledDeltaTime * 8);
            bool wasAnimating = switching || arriving;
            if (wasAnimating)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= (switching ? switchSeconds : arrivalSeconds)) { switching = arriving = false; }
            }
            if (wasAnimating || !Mathf.Approximately(previousHover, hover)) Pose();
        }

        private void Pose()
        {
            if (front?.root == null) return;
            for (int i = waiting.Length - 1; i >= 0; i--)
            {
                var card = waiting[i];
                card.root.SetSiblingIndex(waiting.Length - 1 - i);
                float depth = i + 1;
                Place(card, stackOffset * depth, 1 - .025f * depth, -1.8f * depth);
                card.group.alpha = 1 - .08f * depth;
            }
            if (switching)
            {
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.1f, switchSeconds));
                float lift = Mathf.Sin(Mathf.PI * t);
                float settle = Smooth(Mathf.InverseLerp(.35f, 1, t));
                Vector2 start = direction > 0 ? stackOffset : stackOffset * Mathf.Min(2, shownCount - 1);
                Vector2 position = Vector2.Lerp(start, Vector2.zero, settle) + new Vector2(lift * extractDistance, lift * 19);
                Place(front, position, Mathf.Lerp(.965f, 1, settle) + lift * .025f, -direction * lift * 7);
                Place(outgoing, new Vector2(-lift * 14, -lift * 9), 1 - .05f * lift, direction * lift * 3);
                outgoing.group.alpha = t < .65f ? 1 : 1 - Smooth(Mathf.InverseLerp(.65f, 1, t));
                if (t < .38f) outgoing.root.SetAsLastSibling(); else front.root.SetAsLastSibling();
                front.group.alpha = 1;
            }
            else
            {
                outgoing.root.gameObject.SetActive(false);
                front.root.SetAsLastSibling();
                float t = arriving ? Mathf.Clamp01(elapsed / Mathf.Max(.1f, arrivalSeconds)) : 1;
                float spring = 1 + 2.70158f * Mathf.Pow(t - 1, 3) + 1.70158f * Mathf.Pow(t - 1, 2);
                Place(front, new Vector2(0, Mathf.LerpUnclamped(15, 0, spring) + hover * 3), Mathf.LerpUnclamped(.96f, 1, spring), Mathf.LerpUnclamped(-2, 0, spring));
                front.group.alpha = arriving ? Mathf.Clamp01(t * 5) : 1;
            }
        }

        private static float Smooth(float t) => t * t * (3 - 2 * t);
        private static void Place(Card card, Vector2 position, float scale, float angle)
        {
            card.root.anchoredPosition = position;
            card.root.localScale = Vector3.one * scale;
            card.root.localRotation = Quaternion.Euler(0, 0, angle);
        }
        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) => hovered = false;
        private void OnDisable()
        {
            switching = arriving = hovered = false;
            hover = 0;
            Pose();
        }
    }
}
