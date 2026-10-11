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
        private string builtPosition, builtTitle;
        private float contentHeight;
        private ScrollRect scroll;
        private float revealStarted = float.NegativeInfinity;
        private CanvasGroup revealGroup;
        private const float Width = 820;
        private const float CardWidth = 796;
        private readonly List<Card> cards = new List<Card>();
        private sealed class Card
        {
            public NarrativeActionDefinition action;
            public Button button;
            public TextMeshProUGUI type;
            public Image progress;
            public RectTransform repeats;
            public int repeatCount = -1;
            public readonly List<TextMeshProUGUI> requirements = new List<TextMeshProUGUI>();
        }
        public void Initialize(MapActionInventoryView inventoryView, Func<bool> canControl, Action finishDay, Action cafe)
        {
            inventory = inventoryView; controls = canControl; endDay = finishDay; enterCafe = cafe;
            if (transform.Find("Viewport/ActionList") is RectTransform existingContent)
            {
                content = existingContent;
                scroll = content.parent.GetComponent<ScrollRect>();
                title = transform.Find("Location").GetComponent<TextMeshProUGUI>();
                var existingClose = transform.Find("Close").GetComponent<UnityEngine.UI.Button>();
                existingClose.onClick.RemoveAllListeners();
                existingClose.onClick.AddListener(() => { if (!busy) gameObject.SetActive(false); });
                return;
            }
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(-280, -120);
            rect.sizeDelta = new Vector2(Width, 440);
            GetComponent<Image>().color = Color.clear;
            title = Label("Location", rect, "行动详情", 24);
            Top(title.rectTransform, 24, -8, Width - 90, 42);
            title.gameObject.SetActive(false);
            var close = Button("Close", rect, "×", () => { if (!busy) gameObject.SetActive(false); });
            Top((RectTransform)close.transform, Width - 104, -12, 28, 28);
            close.GetComponent<Image>().color = Color.clear;
            close.gameObject.SetActive(false);
            var viewport = Rect("Viewport", rect);
            Stretch(viewport, 0);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.horizontal = false; scroll.vertical = true;
            scroll.scrollSensitivity = 35; scroll.movementType = ScrollRect.MovementType.Clamped;
            content = Rect("ActionList", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            scroll.content = content;
            close.transform.SetAsLastSibling();
        }
        public void Show(string positionKey, string locationName)
        {
            if (busy) return;
            bool changed = position != positionKey;
            position = positionKey; title.text = locationName;
            bool opening = !gameObject.activeSelf || changed;
            gameObject.SetActive(true); Rebuild();
            if (opening)
            {
                revealStarted = Time.unscaledTime;
                revealGroup = GetComponent<CanvasGroup>();
                if (revealGroup == null) revealGroup = gameObject.AddComponent<CanvasGroup>();
                revealGroup.alpha = 0;
            }
            if (changed) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1; }
        }
        public void Refresh() { if (!busy && isActiveAndEnabled) Rebuild(); }
        private void LateUpdate()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null) return;
            var rect = (RectTransform)transform;
            UpdatePanelHeight();
            float scale = Mathf.Min(1, Mathf.Max(.1f, (parent.rect.width - 64) / Width));
            float t = Mathf.Clamp01((Time.unscaledTime - revealStarted) / .26f);
            float pop = 1 + 2.7f * Mathf.Pow(t - 1, 3) + 1.7f * Mathf.Pow(t - 1, 2);
            Vector3 targetScale = Vector3.one * scale * Mathf.LerpUnclamped(.82f, 1, pop);
            if (!rect.localScale.Equals(targetScale)) rect.localScale = targetScale;
            float alpha = Mathf.Clamp01(t * 2);
            if (revealGroup != null && revealGroup.alpha != alpha) revealGroup.alpha = alpha;
            float offset = Mathf.Min(280, Mathf.Max(0, (parent.rect.width - Width) * .5f - 32));
            var targetPosition = new Vector2(-offset, -120);
            if (!rect.anchoredPosition.Equals(targetPosition)) rect.anchoredPosition = targetPosition;
        }
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
            var actions = NarrativeRuntime.Instance?.Actions;
            var values = actions?.GetMountedActions(position).ToList() ?? new List<NarrativeActionDefinition>();
            string nextSignature = string.Join("|", values.Select(a => a.actionKey));
            if (builtPosition == position && builtTitle == title.text && signature == nextSignature)
            {
                foreach (var card in cards) UpdateCard(card);
                UpdatePanelHeight();
                return;
            }
            // Empty places share the same artwork and controls. Reuse their text geometry
            // instead of destroying the entire popup when selecting another empty place.
            bool cafe = position == "cafe" || position == "大堂区";
            bool builtCafe = builtPosition == "cafe" || builtPosition == "大堂区";
            if (builtPosition != null && signature == "" && values.Count == 0 && cafe == builtCafe)
            {
                var heading = content.Find("LocationOverview/Name")?.GetComponent<TextMeshProUGUI>();
                if (heading != null)
                {
                    heading.text = title.text;
                    builtPosition = position; builtTitle = title.text;
                    UpdatePanelHeight();
                    return;
                }
            }
            for (int childIndex = content.childCount - 1; childIndex >= 0; childIndex--)
            {
                var child = content.GetChild(childIndex).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            cards.Clear();
            signature = nextSignature;
            builtPosition = position; builtTitle = title.text;
            float y = 0;
            if (position == "cafe" || position == "大堂区")
            {
                bool inCafe = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "CoffeeShop";
                var button = Button("EnterCafe", content, inCafe ? "当前正处于咖啡馆" : "进入咖啡馆", () => enterCafe?.Invoke());
                button.interactable = !inCafe;
                button.GetComponent<Image>().sprite = null;
                button.GetComponent<Image>().color = new Color(.95f, .89f, .68f);
                var cafeLabel = button.GetComponentInChildren<TextMeshProUGUI>();
                cafeLabel.fontSize = 30;
                cafeLabel.rectTransform.pivot = new Vector2(.5f, .5f);
                Top((RectTransform)button.transform, 24, -y, CardWidth - 24, 68); y += 84;
            }
            foreach (var action in values)
            {
                var card = new Card { action = action };
                var root = Rect("Action_" + action.actionKey, content);
                MapVisualTheme.Skin(root.gameObject.AddComponent<Image>(), MapVisualTheme.Current?.panel);
                var emblem = MapVisualTheme.Art("Emblem", root, MapVisualTheme.Current?.actionEmblem, Vector2.zero, new Vector2(82, 82));
                emblem.gameObject.SetActive(true);
                Top(emblem.rectTransform, 22, -20, 82, 82);
                var name = Label("Name", root, Text(action.nameText, string.IsNullOrEmpty(action.nameText) ? title.text : action.nameText), 43);
                MapVisualTheme.Font(name, true);
                name.fontStyle = FontStyles.Bold;
                Top(name.rectTransform, 145, -38, CardWidth - 252, 64);
                name.enableAutoSizing = true; name.fontSizeMin = 30; name.fontSizeMax = 43;
                name.overflowMode = TextOverflowModes.Ellipsis;
                if (action.IsTimed)
                {
                    var timer = MapVisualTheme.Art("Timer", root, MapVisualTheme.Current?.timer, Vector2.zero, new Vector2(30, 30));
                    Top(timer.rectTransform, CardWidth - 116, -51, 30, 30);
                }
                var divider = MapVisualTheme.Art("Divider", root, MapVisualTheme.Current?.actionDivider, Vector2.zero, new Vector2(CardWidth - 56, 6));
                divider.gameObject.SetActive(true);
                Top(divider.rectTransform, 28, -105, CardWidth - 56, 6);
                card.type = Label("Type", root, "", 22);
                var desc = Label("Description", root, Text(action.descText, string.IsNullOrEmpty(action.descText) ? "暂无行动描述" : action.descText), 24);
                MapVisualTheme.Font(desc);
                float descWidth = action.parsedRequirements.Count > 0 ? 500 : 632;
                float descHeight = Mathf.Max(86, desc.GetPreferredValues(desc.text, descWidth, 0).y + 8);
                Top(desc.rectTransform, 36, -148, descWidth, descHeight);
                float requirementHeight = 0;
                var requirements = action.parsedRequirements;
                if (requirements.Count > 0)
                {
                    var caption = Label("RequirementsTitle", root, "需要", 22);
                    Top(caption.rectTransform, 551, -88, 120, 30);
                    caption.alignment = TextAlignmentOptions.Right;
                    for (int i = 0; i < requirements.Count; i++)
                    {
                        var box = Rect("Requirement_" + i, root);
                        Top(box, 574, -151 - i * 100, 90, 90);
                        MapVisualTheme.Skin(box.gameObject.AddComponent<Image>(), MapVisualTheme.Current?.requirement);
                        var label = Label("Label", box, "", 17);
                        Stretch(label.rectTransform, 6); label.alignment = TextAlignmentOptions.Center;
                        card.requirements.Add(label);
                    }
                    requirementHeight = 28 + requirements.Count * 100;
                }
                float footer = 168 + Mathf.Max(descHeight, requirementHeight);
                Top(card.type.rectTransform, 36, -footer, 420, 36);
                if (action.type == NarrativeActionType.Repeat && !action.IsProgress)
                {
                    card.repeats = Rect("RepeatSegments", root);
                    Top(card.repeats, 36, -footer - 42, 382, 18);
                    var layout = card.repeats.gameObject.AddComponent<HorizontalLayoutGroup>();
                    layout.spacing = 8; layout.childControlWidth = true; layout.childControlHeight = true;
                    layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
                    footer += 24;
                }
                if (action.IsProgress)
                {
                    var track = Rect("ProgressTrack", root);
                    Top(track, 36, -footer - 42, 382, 18);
                    track.gameObject.AddComponent<Image>().color = new Color(.74f, .54f, .37f, .35f);
                    card.progress = Rect("Fill", track).gameObject.AddComponent<Image>();
                    Stretch(card.progress.rectTransform, 0);
                    card.progress.sprite = MapVisualTheme.Current?.progress;
                    card.progress.type = Image.Type.Filled; card.progress.fillMethod = Image.FillMethod.Horizontal;
                    card.progress.raycastTarget = false; card.progress.color = new Color(.78f, .49f, .20f);
                    footer += 24;
                }
                card.button = Button("Execute", root, "执行", () => OnExecute(card));
                MapVisualTheme.Skin(card.button.GetComponent<Image>(), MapVisualTheme.Current?.disabledExecute);
                float height = Mathf.Max(414, footer + 116);
                Top((RectTransform)card.button.transform, CardWidth * 575 / 1067, -height * 342 / 554, CardWidth * 492 / 1067, height * 212 / 554);
                card.button.GetComponent<Image>().preserveAspect = true;
                var executeLabel = card.button.GetComponentInChildren<TextMeshProUGUI>();
                ConfigureExecuteLabel(executeLabel);
                Top(root, 12, -y, CardWidth, height); y += height + 24;
                cards.Add(card); UpdateCard(card);
            }
            if (values.Count == 0)
            {
                var overview = Rect("LocationOverview", content);
                Top(overview, 12, -y, CardWidth, 414);
                MapVisualTheme.Skin(overview.gameObject.AddComponent<Image>(), MapVisualTheme.Current?.panel);
                var emblem = MapVisualTheme.Art("Emblem", overview, MapVisualTheme.Current?.actionEmblem, Vector2.zero, new Vector2(82, 82));
                Top(emblem.rectTransform, 22, -20, 82, 82);
                var divider = MapVisualTheme.Art("Divider", overview, MapVisualTheme.Current?.actionDivider, Vector2.zero, new Vector2(CardWidth - 56, 6));
                Top(divider.rectTransform, 28, -105, CardWidth - 56, 6);
                var heading = Label("Name", overview, title.text, 43);
                MapVisualTheme.Font(heading, true);
                heading.fontStyle = FontStyles.Bold;
                Top(heading.rectTransform, 145, -38, CardWidth - 252, 64);
                heading.enableAutoSizing = true; heading.fontSizeMin = 30; heading.fontSizeMax = 43;
                var empty = Label("Empty", overview, "此时此地无事可做", 24);
                Top(empty.rectTransform, 36, -148, 600, 94);
                var captionArea = Rect("ExecuteCaptionArea", overview);
                Top(captionArea, CardWidth * 575 / 1067, -414f * 342 / 554, CardWidth * 492 / 1067, 414f * 212 / 554);
                var emptySword = captionArea.gameObject.AddComponent<Image>();
                MapVisualTheme.Skin(emptySword, MapVisualTheme.Current?.execute);
                emptySword.preserveAspect = true;
                emptySword.color = new Color(.46f, .43f, .34f);
                emptySword.raycastTarget = false;
                var caption = Label("ExecuteCaption", captionArea, "暂无行动", 34);
                ConfigureExecuteLabel(caption);
                caption.color = new Color(.97f, .92f, .75f);
                y += 438;
            }
            contentHeight = y;
            content.sizeDelta = new Vector2(0, y);
            UpdatePanelHeight();
            // Let the normal Canvas pass build the new graphics once. A global forced
            // rebuild here also flushed every unrelated HUD/ScrollRect in this frame.
        }
        private void UpdatePanelHeight()
        {
            if (!(transform.parent is RectTransform parent)) return;
            float maximumHeight = Mathf.Min(800, parent.rect.height - 250);
            var size = new Vector2(Width, Mathf.Clamp(contentHeight, 230, Mathf.Max(230, maximumHeight)));
            var rect = (RectTransform)transform;
            if (!rect.sizeDelta.Equals(size)) rect.sizeDelta = size;
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
            card.type.color = MapVisualTheme.Accent;
            if (card.progress != null) card.progress.fillAmount = action.progressTarget > 0
                ? Mathf.Clamp01((float)actions.GetProgress(action.actionKey) / action.progressTarget) : 0;
            UpdateRepeatSegments(card, actions);
            for (int i = 0; i < card.requirements.Count; i++)
            {
                bool ok = i < checks.Count && checks[i].satisfied;
                var requirement = action.parsedRequirements[i];
                card.requirements[i].text = RequirementName(requirement);
                card.requirements[i].color = ok ? MapVisualTheme.Ink : new Color(.65f, .23f, .14f);
            }
            string caption = action.endDay && actions.HasBlockingActions() ? "有需要进行的行动" :
                action.type == NarrativeActionType.Future ? "等待开放" : available ? (action.endDay ? "结束一天" : "执行") : "查看需求";
            card.button.GetComponentInChildren<TextMeshProUGUI>().text = caption;
                var image = card.button.GetComponent<Image>();
                image.color = available ? Color.white : new Color(.46f, .43f, .34f);
            card.button.GetComponentInChildren<TextMeshProUGUI>().color = new Color(.97f, .92f, .75f);
            card.button.interactable = !busy;
        }
        private static void ConfigureExecuteLabel(TextMeshProUGUI label)
        {
            MapVisualTheme.Font(label, true);
            label.fontSize = 38; label.enableAutoSizing = true;
            label.fontSizeMin = 24; label.fontSizeMax = 38;
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform, 0);
            // The sword's text body excludes its pointed tip and right-hand hilt.
            label.rectTransform.offsetMin = new Vector2(24, 24);
            label.rectTransform.offsetMax = new Vector2(-58, -18);
        }

        private void UpdateRepeatSegments(Card card, NarrativeActionController actions)
        {
            if (card.repeats == null) return;
            int count = card.action.IsProgress ? Mathf.Max(0, card.action.progressTarget - actions.GetProgress(card.action.actionKey)) : int.MaxValue;
            foreach (var costs in card.action.parsedRequirements
                .Where(r => r.type == NarrativeActionRequirementType.Item || r.type == NarrativeActionRequirementType.ActionPoint)
                .GroupBy(r => (r.type, r.key)))
            {
                long cost = costs.Sum(r => (long)Mathf.Max(0, r.value));
                if (cost == 0) continue;
                int stock = costs.Key.type == NarrativeActionRequirementType.ActionPoint ? actions.ActionPoints.Current : NarrativeRuntime.Instance.MapInventory.GetCount(costs.Key.key);
                count = Mathf.Min(count, (int)(stock / cost));
            }
            if (count == card.repeatCount) return;
            card.repeatCount = count;
            for (int i = card.repeats.childCount - 1; i >= 0; i--)
            {
                var child = card.repeats.GetChild(i);
                child.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
            // Unbounded actions have no finite resource-derived partition count.
            if (count == int.MaxValue)
            {
                var label = Label("Unlimited", card.repeats, "不限次数", 18);
                label.color = MapVisualTheme.Accent;
                return;
            }
            for (int i = 0; i < count; i++)
            {
                var segment = Rect("Segment_" + i, card.repeats).gameObject.AddComponent<Image>();
                segment.color = i == 0 ? new Color(.78f, .49f, .20f) : new Color(.78f, .49f, .20f, .4f);
                segment.raycastTarget = false;
            }
            if (count == 0)
            {
                var label = Label("NoExecutions", card.repeats, "剩余可执行 0 次", 18);
                label.color = MapVisualTheme.Accent;
            }
        }

        private string RequirementName(NarrativeActionRequirement r) => r.type switch
        {
            NarrativeActionRequirementType.Item => Text("item." + r.key + ".name", r.key) + "\n× " + r.value,
            NarrativeActionRequirementType.Bar => Text("bar." + r.key + ".name", r.key) + "\n" + r.op + " " + r.value,
            NarrativeActionRequirementType.Condition => "拥有条件：" + Text(r.key, r.key),
            NarrativeActionRequirementType.NotCondition => "不能拥有：" + Text(r.key, r.key),
            _ => "行动点\n× " + r.value
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
            if (target == null) yield break;
            Vector2 origin = target.anchoredPosition;
            for (float t = 0; t < .35f; t += Time.unscaledDeltaTime)
            {
                if (target == null || !target.gameObject.activeInHierarchy) yield break;
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
            if (string.IsNullOrEmpty(key)) return fallback;
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
            label.text = text; label.fontSize = size; MapVisualTheme.Font(label); label.raycastTarget = false; return label;
        }
        internal static Button Button(string name, Transform parent, string text, UnityEngine.Events.UnityAction click)
        {
            var rect = Rect(name, parent); var image = rect.gameObject.AddComponent<Image>();
            MapVisualTheme.Skin(image, MapVisualTheme.Current?.tracking);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var label = Label("Label", rect, text, 20); Stretch(label.rectTransform, 4); label.alignment = TextAlignmentOptions.Center;
            button.onClick.AddListener(click); return button;
        }
    }

}
