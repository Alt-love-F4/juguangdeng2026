using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Spotlight.Narrative
{
    public sealed class NarrativePresentationUI : MonoBehaviour
    {
        private CanvasGroup group;
        private TMP_Text title, body, hint;
        private Image background, portraitA, portraitB, fade;
        private GameObject dialogue, choicesPanel;
        private RectTransform choicesContent;
        private bool submitted, revealNow;
        private bool visible;
        private bool suspended;
        private GameObject noticeRoot;
        private TMP_Text noticeText;
        private float noticeUntil;
        public bool IsProgressing { get; private set; }
        public bool IsSuspended => suspended;
        public event Action Escape;

        public static NarrativePresentationUI Create()
        {
            GameObject go = new GameObject("NarrativePresentation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(NarrativePresentationUI));
            DontDestroyOnLoad(go);
            Canvas canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 250;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            NarrativePresentationUI ui = go.GetComponent<NarrativePresentationUI>(); ui.Build(); return ui;
        }

        private void Build()
        {
            group = gameObject.AddComponent<CanvasGroup>();
            Image blocker = Image("Blocker", transform, Vector2.zero, Vector2.one, new Color(0, 0, 0, .55f));
            blocker.gameObject.AddComponent<Button>().onClick.AddListener(Submit);
            background = Image("Background", transform, Vector2.zero, Vector2.one, Color.white); background.raycastTarget = false;
            portraitA = Image("PortraitA", transform, new Vector2(.05f, .2f), new Vector2(.4f, .92f), Color.white);
            portraitB = Image("PortraitB", transform, new Vector2(.6f, .2f), new Vector2(.95f, .92f), Color.white);
            portraitA.preserveAspect = portraitB.preserveAspect = true; portraitA.raycastTarget = portraitB.raycastTarget = false;
            fade = Image("Fade", transform, Vector2.zero, Vector2.one, new Color(0, 0, 0, 0)); fade.raycastTarget = false;
            dialogue = Image("Dialogue", transform, new Vector2(.045f, .055f), new Vector2(.955f, .34f), new Color(.035f, .035f, .045f, .97f)).gameObject;
            dialogue.AddComponent<Button>().onClick.AddListener(Submit);
            title = Text("Title", dialogue.transform, new Vector2(.035f, .74f), new Vector2(.965f, .94f), 30);
            Image bodyViewport = Image("BodyViewport", dialogue.transform, new Vector2(.035f, .08f), new Vector2(.965f, .72f), Color.clear);
            bodyViewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect bodyScroll = bodyViewport.gameObject.AddComponent<ScrollRect>(); bodyScroll.horizontal = false;
            body = Text("Body", bodyViewport.transform, Vector2.zero, Vector2.one, 27);
            RectTransform bodyRect = body.rectTransform; bodyRect.anchorMin = new Vector2(0, 1); bodyRect.anchorMax = Vector2.one; bodyRect.pivot = new Vector2(.5f, 1);
            ContentSizeFitter bodyFitter = body.gameObject.AddComponent<ContentSizeFitter>(); bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            bodyScroll.viewport = bodyViewport.rectTransform; bodyScroll.content = bodyRect;
            bodyViewport.gameObject.AddComponent<Button>().onClick.AddListener(Submit);
            hint = Text("Notice", transform, new Vector2(.05f, .92f), new Vector2(.95f, .98f), 22); hint.alignment = TextAlignmentOptions.Center;
            choicesPanel = Image("Choices", transform, new Vector2(.12f, .38f), new Vector2(.88f, .84f), new Color(.035f, .035f, .045f, .98f)).gameObject;
            ScrollRect scroll = choicesPanel.AddComponent<ScrollRect>(); scroll.horizontal = false;
            Image viewport = Image("Viewport", choicesPanel.transform, new Vector2(.025f, .025f), new Vector2(.975f, .975f), Color.clear);
            viewport.gameObject.AddComponent<RectMask2D>();
            choicesContent = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            choicesContent.SetParent(viewport.transform, false); choicesContent.anchorMin = new Vector2(0, 1); choicesContent.anchorMax = Vector2.one; choicesContent.pivot = new Vector2(.5f, 1); choicesContent.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = choicesContent.GetComponent<VerticalLayoutGroup>(); layout.spacing = 12; layout.padding = new RectOffset(8, 8, 8, 8); layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.childControlWidth = true;
            choicesContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport.rectTransform; scroll.content = choicesContent;
            background.enabled = portraitA.enabled = portraitB.enabled = false;
            Hide();
        }

        private static Image Image(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            RectTransform r = go.GetComponent<RectTransform>(); r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero;
            Image result = go.GetComponent<Image>(); result.color = color; return result;
        }

        public static TMP_Text Text(string name, Transform parent, Vector2 min, Vector2 max, float size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            RectTransform r = go.GetComponent<RectTransform>(); r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero;
            TMP_Text text = go.GetComponent<TMP_Text>(); text.font = Resources.Load<TMP_FontAsset>("Fonts/SimHei SDF"); text.fontSize = size; text.color = Color.white; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow; return text;
        }

        public void Show(string heading)
        {
            visible = true; group.alpha = suspended ? 0 : 1; group.blocksRaycasts = group.interactable = !suspended; title.text = heading ?? string.Empty;
            ClearChoices(); hint.text = string.Empty; ResetSubmit();
            background.enabled = portraitA.enabled = portraitB.enabled = false;
            fade.color = new Color(0, 0, 0, 0); body.text = string.Empty;
        }

        public void ShowLine(string text)
        {
            body.text = text ?? string.Empty; body.maxVisibleCharacters = 0; revealNow = false; IsProgressing = true;
            body.rectTransform.anchoredPosition = Vector2.zero;
            LayoutRebuilder.ForceRebuildLayoutImmediate(body.rectTransform);
        }

        public IEnumerator Reveal(Func<bool> skipped)
        {
            body.ForceMeshUpdate();
            int count = body.textInfo.characterCount;
            float visibleCharacters = 0;
            while (body.maxVisibleCharacters < count && !revealNow && !skipped())
            {
                if (suspended) { yield return null; continue; }
                visibleCharacters += Time.unscaledDeltaTime * 40f;
                body.maxVisibleCharacters = Mathf.Min(count, (int)visibleCharacters);
                yield return null;
            }
            body.maxVisibleCharacters = int.MaxValue;
            IsProgressing = false; revealNow = false; ResetSubmit();
        }

        private void Submit()
        {
            if (!visible || suspended || choicesPanel.activeSelf) return;
            if (IsProgressing) revealNow = true;
            else submitted = true;
        }
        public void RevealImmediately() { if (IsProgressing) revealNow = true; }
        public void ResetSubmit() => submitted = false;
        public bool ConsumeSubmit() { bool result = submitted; submitted = false; return result; }
        public void ShowHint(string text)
        {
            hint.text = text ?? string.Empty;
            if (visible) return;
            if (noticeRoot == null)
            {
                noticeRoot = new GameObject("NarrativeNotice", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                DontDestroyOnLoad(noticeRoot);
                Canvas canvas = noticeRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 251;
                CanvasScaler scaler = noticeRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
                Image banner = Image("Banner", noticeRoot.transform, new Vector2(.08f, .88f), new Vector2(.92f, .97f), new Color(.025f, .025f, .025f, .96f)); banner.raycastTarget = false;
                noticeText = Text("Message", banner.transform, new Vector2(.025f, .05f), new Vector2(.975f, .95f), 24); noticeText.alignment = TextAlignmentOptions.Center;
                noticeText.enableAutoSizing = true; noticeText.fontSizeMin = 16; noticeText.fontSizeMax = 24;
            }
            noticeRoot.SetActive(true); noticeText.text = text ?? string.Empty; noticeUntil = Time.unscaledTime + 3f;
        }
        public void SetBackground(Sprite sprite) { background.sprite = sprite; background.enabled = sprite != null; }
        public Image Portrait(bool right) => right ? portraitB : portraitA;

        public void ShowChoices(IReadOnlyList<NarrativeChoiceDefinition> choices, Func<string, string> resolve, Func<string, bool> unlocked, Action<string> selected)
        {
            ClearChoices(); choicesPanel.SetActive(true); ResetSubmit();
            foreach (NarrativeChoiceDefinition choice in choices)
            {
                Image row = Image("Choice_" + choice.choiceId, choicesContent, Vector2.zero, Vector2.one, new Color(.17f, .19f, .22f));
                Button button = row.gameObject.AddComponent<Button>(); button.interactable = unlocked(choice.choiceId);
                LayoutElement element = row.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = 104; element.minHeight = 104;
                TMP_Text label = Text("Label", row.transform, new Vector2(.035f, .1f), new Vector2(.965f, .9f), 25); label.alignment = TextAlignmentOptions.MidlineLeft;
                label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 25; label.text = resolve(choice.textKey);
                label.color = button.interactable ? Color.white : new Color(.6f, .6f, .6f);
                string id = choice.choiceId; button.onClick.AddListener(() => selected(id));
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(choicesContent);
        }

        public void ClearChoices()
        {
            choicesPanel.SetActive(false);
            for (int i = choicesContent.childCount - 1; i >= 0; i--)
            {
                GameObject child = choicesContent.GetChild(i).gameObject; child.SetActive(false); Destroy(child);
            }
        }

        public IEnumerator Fade(string type, float duration, Func<bool> skipped)
        {
            float start = string.Equals(type, "in", StringComparison.OrdinalIgnoreCase) ? 1f : fade.color.a;
            float end = string.Equals(type, "in", StringComparison.OrdinalIgnoreCase) ? 0f : 1f;
            fade.color = new Color(0, 0, 0, start);
            for (float elapsed = 0; elapsed < duration && !skipped(); elapsed += Time.unscaledDeltaTime)
            {
                if (suspended) elapsed -= Time.unscaledDeltaTime;
                else fade.color = new Color(0, 0, 0, Mathf.Lerp(start, end, elapsed / duration));
                yield return null;
            }
            fade.color = new Color(0, 0, 0, end);
        }

        public void SetSuspended(bool value)
        {
            suspended = value;
            group.alpha = visible && !value ? 1 : 0;
            group.interactable = group.blocksRaycasts = visible && !value;
        }

        public void Hide()
        {
            visible = false; group.alpha = 0; group.blocksRaycasts = group.interactable = false;
            ClearChoices(); IsProgressing = false; ResetSubmit();
        }
        private void Update()
        {
            if (noticeRoot != null && noticeRoot.activeSelf && Time.unscaledTime >= noticeUntil) noticeRoot.SetActive(false);
            if (!visible || suspended) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) Escape?.Invoke();
            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) Submit();
        }
        public void Dispose()
        {
            if (noticeRoot != null) { noticeRoot.SetActive(false); Destroy(noticeRoot); }
            gameObject.SetActive(false); Destroy(gameObject);
        }
    }
}
