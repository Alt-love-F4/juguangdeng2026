using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>Scrollable action cards. Unavailable buttons remain clickable to explain rejected requirements.</summary>
    public sealed class MapActionDetailsView : MonoBehaviour
    {
        private RectTransform content;
        private TextMeshProUGUI title;
        private MapActionInventoryView inventory;
        private string position;
        private Func<bool> controls;
        private Action endDay, enterCafe;
        private bool busy;
        public bool IsBusy => busy;
        private float nextRefresh;
        private string signature;
        private readonly List<Card> cards = new List<Card>();
        private sealed class Card
        {
            public NarrativeActionDefinition action;
            public Button button;
            public TextMeshProUGUI type;
            public readonly List<TextMeshProUGUI> requirements = new List<TextMeshProUGUI>();
        }
        public void Initialize(MapActionInventoryView inventoryView, Func<bool> canControl, Action finishDay, Action cafe)
        {
            inventory = inventoryView; controls = canControl; endDay = finishDay; enterCafe = cafe;
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, .5f);
            rect.pivot = new Vector2(1, .5f);
            rect.anchoredPosition = new Vector2(-32, 0);
            rect.sizeDelta = new Vector2(520, 610);
            GetComponent<Image>().color = new Color(.035f, .065f, .065f, .97f);
            title = Label("Location", rect, "行动详情", 24);
            Top(title.rectTransform, 20, -12, 430, 42);
            var close = Button("Close", rect, "×", () => { if (!busy) gameObject.SetActive(false); });
            Top((RectTransform)close.transform, 462, -12, 38, 38);
            var viewport = Rect("Viewport", rect);
            Top(viewport, 12, -66, 496, 530);
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .1f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.horizontal = false; scroll.vertical = true;
            scroll.scrollSensitivity = 35; scroll.movementType = ScrollRect.MovementType.Clamped;
            content = Rect("ActionList", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            scroll.content = content;
        }
        public void Show(string positionKey, string locationName)
        {
            if (busy) return;
            position = positionKey; title.text = locationName + " · 行动详情";
            gameObject.SetActive(true); Rebuild();
        }
        public void Refresh() { if (!busy && isActiveAndEnabled) Rebuild(); }
        private void Update()
        {
            if (busy || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .2f;
            var actions = NarrativeRuntime.Instance?.Actions;
            if (actions == null) return;
            string next = string.Join("|", actions.GetMountedActions(position).Select(a => a.actionKey));
            if (next != signature) Rebuild(); else foreach (var card in cards) UpdateCard(card);
        }
        private void Rebuild()
        {
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            cards.Clear();
            var actions = NarrativeRuntime.Instance?.Actions;
            var values = actions?.GetMountedActions(position).ToList() ?? new List<NarrativeActionDefinition>();
            signature = string.Join("|", values.Select(a => a.actionKey));
            float y = 0;
            if (position == "cafe")
            {
                bool inCafe = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "CoffeeShop";
                var button = Button("EnterCafe", content, inCafe ? "当前正处于咖啡馆" : "进入咖啡馆", () => enterCafe?.Invoke());
                button.interactable = !inCafe;
                Top((RectTransform)button.transform, 8, -y, 480, 48); y += 60;
            }
            foreach (var action in values)
            {
                var card = new Card { action = action };
                var root = Rect("Action_" + action.actionKey, content);
                root.gameObject.AddComponent<Image>().color = new Color(.08f, .13f, .13f);
                var name = Label("Name", root, Text(action.nameText, string.IsNullOrEmpty(action.nameText) ? action.actionKey : action.nameText), 24);
                Top(name.rectTransform, 16, -40, 444, 38);
                card.type = Label("Type", root, "", 16);
                card.type.color = new Color(.66f, .85f, .78f);
                Top(card.type.rectTransform, 16, -10, 444, 26);
                var desc = Label("Description", root, Text(action.descText, string.IsNullOrEmpty(action.descText) ? "暂无行动描述" : action.descText), 18);
                float descHeight = Mathf.Max(50, desc.GetPreferredValues(desc.text, 444, 0).y + 8);
                Top(desc.rectTransform, 16, -82, 444, descHeight);
                float ry = 90 + descHeight;
                var requirements = action.parsedRequirements;
                if (requirements.Count == 0)
                {
                    var free = Label("Free", root, "无需求 · 免费执行", 16);
                    free.color = new Color(.65f, .88f, .7f);
                    Top(free.rectTransform, 16, -ry, 444, 34); ry += 44;
                }
                else
                {
                    for (int i = 0; i < requirements.Count; i++)
                    {
                        var box = Rect("Requirement_" + i, root);
                        Top(box, 16 + (i % 2) * 226, -ry - (i / 2) * 82, 218, 74);
                        box.gameObject.AddComponent<Image>().color = new Color(.04f, .08f, .08f);
                        var label = Label("Label", box, "", 15);
                        Stretch(label.rectTransform, 8);
                        card.requirements.Add(label);
                    }
                    ry += ((requirements.Count + 1) / 2) * 82 + 8;
                }
                card.button = Button("Execute", root, "执行行动", () => OnExecute(card));
                Top((RectTransform)card.button.transform, 16, -ry, 444, 46);
                float height = ry + 60;
                Top(root, 8, -y, 480, height); y += height + 14;
                cards.Add(card); UpdateCard(card);
            }
            if (values.Count == 0)
            {
                var empty = Label("Empty", content, "此时此地无事可做", 20);
                Top(empty.rectTransform, 16, -y, 460, 60); y += 70;
            }
            content.sizeDelta = new Vector2(0, y);
        }
        private void OnDisable()
        {
            StopAllCoroutines(); busy = false;
            foreach (Transform child in transform.root)
                if (child.name == "ConsumptionEffect") Destroy(child.gameObject);
        }
        private void UpdateCard(Card card)
        {
            var actions = NarrativeRuntime.Instance?.Actions;
            if (actions == null) return;
            var action = card.action;
            bool available = actions.IsAvailable(action.actionKey, out var checks);
            string type = action.type switch
            {
                NarrativeActionType.Daily => "每天行动", NarrativeActionType.Once => "一次性行动",
                NarrativeActionType.Future => "未来行动", NarrativeActionType.Progress => "进度型行动",
                NarrativeActionType.Timed => "时限行动", _ => "可重复行动"
            };
            if (action.IsTimed) type += " · 剩余 " + actions.GetRemainingDays(action.actionKey) + " 天";
            if (action.type == NarrativeActionType.Future) type += " · " + actions.GetRemainingDays(action.actionKey) + " 天后开放";
            if (action.IsProgress) type += " · 进度 " + actions.GetProgress(action.actionKey) + " / " + action.progressTarget;
            if (action.mustExecute) type += " · 今日必做";
            card.type.text = type;
            for (int i = 0; i < card.requirements.Count; i++)
            {
                bool ok = i < checks.Count && checks[i].satisfied;
                var requirement = action.parsedRequirements[i];
                string current = i < checks.Count ? checks[i].currentValue : "";
                card.requirements[i].text = (ok ? "已满足  " : "未满足  ") + RequirementName(requirement) +
                    (string.IsNullOrEmpty(current) ? "" : "\n当前：" + current);
                card.requirements[i].color = ok ? new Color(.65f, .94f, .76f) : new Color(1, .56f, .48f);
            }
            string caption = action.endDay && actions.HasBlockingActions() ? "有需要进行的行动" :
                action.type == NarrativeActionType.Future ? "等待开放" : available ? (action.endDay ? "结束一天" : "执行行动") : "暂不可用 · 查看原因";
            card.button.GetComponentInChildren<TextMeshProUGUI>().text = caption;
            card.button.GetComponent<Image>().color = available ? new Color(.18f, .4f, .32f) : new Color(.22f, .24f, .24f);
            card.button.interactable = !busy;
        }
        private string RequirementName(NarrativeActionRequirement r) => r.type switch
        {
            NarrativeActionRequirementType.Item => "物品：" + Text("item." + r.key + ".name", r.key) + " × " + r.value,
            NarrativeActionRequirementType.Bar => "进度：" + Text("bar." + r.key + ".name", r.key) + " " + r.op + " " + r.value,
            NarrativeActionRequirementType.Condition => "拥有条件：" + Text(r.key, r.key),
            NarrativeActionRequirementType.NotCondition => "不能拥有：" + Text(r.key, r.key),
            _ => "行动点 × " + r.value
        };
        private void OnExecute(Card card)
        {
            if (busy || !(controls?.Invoke() ?? true)) return;
            var actions = NarrativeRuntime.Instance?.Actions;
            if (actions == null) return;
            if (!actions.IsAvailable(card.action.actionKey, out var checks))
            {
                Notice(card.action.endDay && actions.HasBlockingActions() ? "有需要进行的行动" : "要求不满足");
                for (int i = 0; i < checks.Count; i++) if (!checks[i].satisfied) StartCoroutine(Shake((RectTransform)card.requirements[i].transform.parent));
                if (checks.All(c => c.satisfied)) StartCoroutine(Shake((RectTransform)card.button.transform));
                return;
            }
            StartCoroutine(Execute(card));
        }
        private IEnumerator Execute(Card card)
        {
            busy = true;
            foreach (var c in cards) c.button.interactable = false;
            // Show the costs first; the controller rechecks and consumes them atomically afterwards.
            for (int i = 0; i < card.action.parsedRequirements.Count; i++)
            {
                var requirement = card.action.parsedRequirements[i];
                if (requirement.type == NarrativeActionRequirementType.Item)
                {
                    yield return inventory.Reveal(requirement.key);
                    yield return Fly(inventory.FindSlot(requirement.key), (RectTransform)card.requirements[i].transform.parent, "×" + requirement.value);
                }
                else if (requirement.type == NarrativeActionRequirementType.ActionPoint)
                    yield return Fly(inventory.PointsRect, (RectTransform)card.requirements[i].transform.parent, "−" + requirement.value + " 行动点");
            }
            bool finished = false;
            bool accepted = NarrativeRuntime.Instance.Actions.Execute(card.action.actionKey, success =>
            {
                finished = true;
                if (success && card.action.endDay) { busy = false; endDay?.Invoke(); }
                if (!success) Notice("行动事件未能执行");
            });
            if (!accepted) { Notice("要求不满足"); finished = true; }
            while (!finished) yield return null;
            busy = false; inventory.Refresh(); Rebuild();
        }
        private IEnumerator Fly(RectTransform source, RectTransform target, string text)
        {
            if (source == null || target == null) yield break;
            var effect = Rect("ConsumptionEffect", transform.root);
            effect.sizeDelta = new Vector2(124, 70);
            var background = effect.gameObject.AddComponent<Image>();
            background.color = new Color(.12f, .32f, .22f, .98f); background.raycastTarget = false;
            var itemLabel = source.GetComponentInChildren<TextMeshProUGUI>();
            var label = Label("Label", effect, source == inventory.PointsRect ? text : (itemLabel?.text.Split('\n')[0] ?? "物品") + "\n" + text, 20);
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(.82f, 1, .7f);
            Stretch(label.rectTransform, 4);
            Vector3 start = source.position, end = target.position;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / .45f)
            {
                effect.position = Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 65;
                effect.localScale = Vector3.one * Mathf.Lerp(1, .65f, t);
                yield return null;
            }
            Destroy(effect.gameObject);
            var image = target.GetComponent<Image>();
            Color original = image.color; image.color = new Color(.23f, .48f, .3f);
            yield return new WaitForSecondsRealtime(.12f);
            if (image != null) image.color = original;
        }
        private IEnumerator Shake(RectTransform target)
        {
            Vector2 origin = target.anchoredPosition;
            for (float t = 0; t < .35f; t += Time.unscaledDeltaTime)
            {
                target.anchoredPosition = origin + Vector2.right * Mathf.Sin(t * 80) * 8 * (1 - t / .35f);
                yield return null;
            }
            if (target != null) target.anchoredPosition = origin;
        }
        private void Notice(string message)
        {
            var root = Rect("RequirementNotice", transform.root);
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
            root.sizeDelta = new Vector2(380, 120);
            root.gameObject.AddComponent<Image>().color = new Color(.16f, .07f, .07f, .98f);
            var label = Label("Message", root, message, 24); Stretch(label.rectTransform, 16);
            label.alignment = TextAlignmentOptions.Center;
            Destroy(root.gameObject, 2);
        }
        internal static string Text(string key, string fallback)
        {
            string value = NarrativeRuntime.Instance?.ResolveText(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }
        internal static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform;
        }
        internal static void Top(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
        }
        internal static void Stretch(RectTransform rect, float margin)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin); rect.offsetMax = new Vector2(-margin, -margin);
        }
        internal static TextMeshProUGUI Label(string name, Transform parent, string text, float size)
        {
            var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Resources.Load<TMP_FontAsset>("Fonts/CactusClassicalSerif SDF") ?? TMP_Settings.defaultFontAsset;
            label.text = text; label.fontSize = size; label.color = Color.white; label.raycastTarget = false; return label;
        }
        internal static Button Button(string name, Transform parent, string text, UnityEngine.Events.UnityAction click)
        {
            var rect = Rect(name, parent); var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.12f, .22f, .2f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var label = Label("Label", rect, text, 20); Stretch(label.rectTransform, 4); label.alignment = TextAlignmentOptions.Center;
            button.onClick.AddListener(click); return button;
        }
    }

    public sealed class MapActionInventoryView : MonoBehaviour
    {
        private ScrollRect scroll;
        private RectTransform content;
        private TextMeshProUGUI points;
        private readonly Dictionary<string, RectTransform> slots = new Dictionary<string, RectTransform>();
        private string signature = "__uninitialized";
        private float nextRefresh;
        public RectTransform PointsRect => points.rectTransform;
        public void Initialize(TextMeshProUGUI pointsLabel)
        {
            points = pointsLabel;
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.anchoredPosition = new Vector2(0, 78); rect.sizeDelta = new Vector2(560, 88);
            GetComponent<Image>().color = new Color(.04f, .07f, .07f, .96f);
            var viewport = MapActionDetailsView.Rect("Viewport", rect); MapActionDetailsView.Stretch(viewport, 6);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
            content = MapActionDetailsView.Rect("Items", viewport);
            content.anchorMin = content.anchorMax = new Vector2(0, .5f); content.pivot = new Vector2(0, .5f);
            scroll.content = content; Refresh();
        }
        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .2f; Refresh();
        }
        public void Refresh()
        {
            var runtime = NarrativeRuntime.Instance;
            if (runtime?.Actions == null) return;
            points.text = "行动点 " + runtime.Actions.ActionPoints.Current + " / " + runtime.Actions.ActionPoints.Max + "    |    第 " + runtime.Actions.CurrentDay + " 天";
            string next = string.Join("|", runtime.MapInventory.Items.Select(p => p.Key + ":" + p.Value));
            if (next == signature) return; signature = next;
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            slots.Clear(); int index = 0;
            foreach (var item in runtime.MapInventory.Items)
            {
                var slot = MapActionDetailsView.Rect("Item_" + item.Key, content);
                slot.anchorMin = slot.anchorMax = new Vector2(0, .5f); slot.pivot = new Vector2(0, .5f);
                slot.anchoredPosition = new Vector2(index++ * 112, 0); slot.sizeDelta = new Vector2(104, 70);
                slot.gameObject.AddComponent<Image>().color = new Color(.12f, .2f, .18f);
                var label = MapActionDetailsView.Label("NameAndCount", slot, MapActionDetailsView.Text("item." + item.Key + ".name", item.Key) + "\n× " + item.Value, 16);
                MapActionDetailsView.Stretch(label.rectTransform, 5); label.alignment = TextAlignmentOptions.Center;
                slots[item.Key] = slot;
            }
            if (index == 0)
            {
                var empty = MapActionDetailsView.Label("Empty", content, "物品栏为空", 18);
                empty.rectTransform.sizeDelta = new Vector2(250, 60); empty.rectTransform.anchoredPosition = new Vector2(270, 0);
                empty.alignment = TextAlignmentOptions.Center;
            }
            content.sizeDelta = new Vector2(Mathf.Max(548, index * 112), 76);
        }
        public RectTransform FindSlot(string key) => slots.TryGetValue(key, out var slot) ? slot : null;
        public IEnumerator Reveal(string key)
        {
            Refresh(); Canvas.ForceUpdateCanvases();
            var slot = FindSlot(key); if (slot == null) yield break;
            scroll.StopMovement();
            float max = Mathf.Max(0, content.rect.width - scroll.viewport.rect.width);
            float target = max > 0 ? Mathf.Clamp01((slot.anchoredPosition.x - (scroll.viewport.rect.width - slot.rect.width) / 2) / max) : 0;
            float start = scroll.horizontalNormalizedPosition;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / .25f)
            {
                scroll.horizontalNormalizedPosition = Mathf.Lerp(start, target, t); yield return null;
            }
            scroll.horizontalNormalizedPosition = target;
        }
    }
}
