using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    public sealed partial class DialoguePanel
    {
        [Header("Shared effect library (no NCS runtime required)")]
        [SerializeField] private PresentationEffectLibrary effectLibrary;
        private PresentationEffects effects;
        private Coroutine portraitChange;
        private bool entranceComplete;
        private readonly List<PresentationEffectHandle> blockingHandles = new List<PresentationEffectHandle>();
        private int blockingEffects
        {
            get
            {
                for (int i = blockingHandles.Count - 1; i >= 0; i--)
                    if (blockingHandles[i].IsDone) blockingHandles.RemoveAt(i);
                return blockingHandles.Count;
            }
        }
        public bool OwnsContinuity(object owner) => owner != null && script != null
            && ReferenceEquals(script.ContinuityOwner, owner);
        public PresentationEffects Effects => effects;
        public bool IsEffectBlocking => blockingEffects > 0 || portraitChange != null || effects != null && effects.IsBackgroundTransitioning;
        public bool IsReady => ready && !IsEffectBlocking;
        public RectTransform EffectStage { get; private set; }
        public Image EffectOverlay { get; private set; }
        public Image LeftPortrait => leftPortrait;
        public Image RightPortrait => rightPortrait;
        public Image CenterPortrait => centerPortrait;
        public DialogueCharacter LeftCharacter => left;
        public DialogueCharacter RightCharacter => right;
        public DialogueCharacter CenterCharacter => center;

        private void InitializeEffects()
        {
            if (effectLibrary == null) throw new InvalidOperationException("DialoguePanel needs the shared PresentationEffectLibrary asset.");
            // Keep the modal raycast blocker stationary; only visible content belongs to the camera/shake stage.
            var go = new GameObject("EffectStage", typeof(RectTransform));
            EffectStage = go.GetComponent<RectTransform>();
            EffectStage.SetParent(transform, false);
            EffectStage.anchorMin = Vector2.zero; EffectStage.anchorMax = Vector2.one;
            EffectStage.offsetMin = EffectStage.offsetMax = Vector2.zero;
            // Preserve sibling order, including nested PSD artwork and its modal options overlay.
            for (int i = 0; i < transform.childCount;)
            {
                Transform child = transform.GetChild(i);
                if (child == EffectStage || child.name == "InputBlocker") { i++; continue; }
                child.SetParent(EffectStage, false);
            }
            if (artworkRoot == null)
            {
                background.transform.SetAsFirstSibling();
                if (leftPortraitFrame != null) leftPortraitFrame.transform.SetSiblingIndex(1);
                if (centerPortraitFrame != null) centerPortraitFrame.transform.SetSiblingIndex(2);
                if (rightPortraitFrame != null) rightPortraitFrame.transform.SetSiblingIndex(3);
                dialogueBox.SetAsLastSibling();
            }
            var overlayObject = new GameObject("EffectOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayObject.transform.SetParent(transform, false);
            EffectOverlay = overlayObject.GetComponent<Image>();
            EffectOverlay.rectTransform.anchorMin = Vector2.zero;
            EffectOverlay.rectTransform.anchorMax = Vector2.one;
            EffectOverlay.rectTransform.offsetMin = EffectOverlay.rectTransform.offsetMax = Vector2.zero;
            EffectOverlay.raycastTarget = false;
            effects = new PresentationEffects(effectLibrary, EffectStage, EffectOverlay, leftPortrait,
                leftPortraitFrame, rightPortrait, rightPortraitFrame, inactiveTint, focusSeconds, centerPortrait, centerPortraitFrame, background, dialogueBox);
        }
        private void EnterVisiblePortraits()
        {
            if (leftPortrait.enabled) effects.Play(PresentationEffectIds.EnterShadowWalk, 0);
            if (rightPortrait.enabled) effects.Play(PresentationEffectIds.EnterShadowWalk, 1);
            if (centerPortrait.enabled) effects.Play(PresentationEffectIds.EnterShadowWalk, 2);
        }
        private void PlayLineAccent(DialogueLine line)
        {
            if (!string.IsNullOrWhiteSpace(line.AccentFxKey)) PlayEffect(line.AccentFxKey, line.AccentTargetKey);
            else if (line.Gesture == DialogueGesture.Shake) PlayEffect(PresentationEffectIds.AccentShakeStartle);
            else if (line.Gesture == DialogueGesture.Nod) effects.Nod(SpeakerSlot(line.Speaker));
        }

        /// <summary>Shared effect-ID input. A future NCS adapter resolves its target ID and calls this method.</summary>
        public PresentationEffectHandle PlayEffect(string fx, string targetKey = null, Action completed = null)
            => PlayEffect(fx, targetKey, false, completed);
        public PresentationEffectHandle PlayEffect(string fx, string targetKey, bool blocking, Action completed = null)
        {
            if (!IsPlaying) throw new InvalidOperationException("Start a presentation before playing an effect.");
            bool portrait = fx == PresentationEffectIds.EnterShadowWalk || fx == PresentationEffectIds.AccentShakeStartle
                || fx == PresentationEffectIds.PortraitFadeIn || fx == PresentationEffectIds.PortraitFadeOut || fx == PresentationEffectIds.PortraitNod;
            if (fx == PresentationEffectIds.BackgroundPan && !string.IsNullOrWhiteSpace(targetKey) && targetKey != "background")
                throw new ArgumentException("Background pan target must be background.");
            int slot = portrait ? ResolveEffectSlot(targetKey) : -1;
            PresentationEffectHandle handle = effects.Play(fx, slot, completed);
            if (blocking && !handle.IsDone) blockingHandles.Add(handle);
            return handle;
        }
        private int ResolveEffectSlot(string targetKey)
        {
            if (string.IsNullOrWhiteSpace(targetKey) || targetKey == "speaker")
            {
                DialogueSpeaker who = activeLines[LineIndex].Speaker;
                if (who == DialogueSpeaker.Narrator || who == DialogueSpeaker.Offstage) throw new ArgumentException("Speaker has no portrait: supply a target character or side.");
                targetKey = who == DialogueSpeaker.Left ? "left" : who == DialogueSpeaker.Center ? "center" : "right";
            }
            int slot = targetKey == "left" || (left != null && !string.IsNullOrEmpty(left.CharacterKey) && left.CharacterKey == targetKey) ? 0
                : targetKey == "right" || (right != null && !string.IsNullOrEmpty(right.CharacterKey) && right.CharacterKey == targetKey) ? 1
                : targetKey == "center" || (center != null && !string.IsNullOrEmpty(center.CharacterKey) && center.CharacterKey == targetKey) ? 2 : -1;
            if (slot < 0) throw new ArgumentException("Effect target is not on stage: " + targetKey);
            if (!(slot == 0 ? leftPortrait.enabled : slot == 1 ? rightPortrait.enabled : centerPortrait.enabled))
                throw new ArgumentException("Effect target has no visible portrait: " + targetKey);
            return slot;
        }
        public PresentationEffectHandle PlayCameraCue(float zoom = 1f, float shake = 0f, float duration = .3f,
            Vector2? focus = null, Action completed = null)
        {
            if (!IsPlaying) throw new InvalidOperationException("Start a presentation before moving the camera stage.");
            return effects.Camera(zoom, shake, duration, focus ?? Vector2.zero, completed);
        }
        public PresentationEffectHandle PlayFade(string type, float duration = .3f, Action atBlack = null, Action completed = null)
        {
            if (!IsPlaying) throw new InvalidOperationException("Start a presentation before fading.");
            PresentationEffectHandle handle = effects.Fade(type, duration, atBlack, completed);
            blockingHandles.Add(handle);
            return handle;
        }
        public RawImage BackgroundLayer => background;
        private void ApplyBackground(DialogueScript value, bool seamless)
        {
            var visual = value.BackgroundVisual;
            if (visual != null && visual.useSceneBackground)
            {
                // Explicit scene-backed dialogue must expose the authored gameplay scene.
                // Cancel any incoming CG layer before disabling the main background.
                effects.SetBackground(null, Color.white);
                background.enabled = false;
                effects.HideBackgroundBacking();
                return;
            }
            if (visual == null && value.Background == null)
            {
                if (!seamless) { effects.SetBackground(null, Color.white); background.enabled = false; effects.HideBackgroundBacking(); }
                return;
            }
            effects.SetBackground(value.Background, visual == null ? Color.white : visual.placeholder ? visual.placeholderColor : visual.tint,
                seamless ? visual?.fadeSeconds ?? .3f : 0f);
        }
        public void SetBackgroundTint(Color tint) => background.color = tint;
        internal PresentationEffectHandle RevealInitialBackground(float seconds) => effects.RevealBackground(seconds);
        internal void HideBackgroundBacking() => effects.HideBackgroundBacking();
        public void StopEffects()
        {
            if (effects != null) effects.Reset();
            blockingHandles.Clear();
        }
        private void CancelEffectWork()
        {
            entranceComplete = false;
            if (background != null) background.color = Color.white;
            if (portraitChange != null) { StopCoroutine(portraitChange); portraitChange = null; }
            StopEffects();
        }
        private void PrepareNextLine(int index)
        {
            if (script.InstantPortraitPreview)
            {
                ShowLine(index);
                effects.Tick(0f);
                return;
            }
            DialogueLine line = activeLines[index];
            if (!ChangesIdentity(line.ReplaceLeft, left, line.LeftCharacter)
                && !ChangesIdentity(line.ReplaceRight, right, line.RightCharacter)
                && !ChangesIdentity(line.ReplaceCenter, center, line.CenterCharacter)) { ShowLine(index); return; }
            ready = false;
            continueButton.interactable = false;
            portraitChange = StartCoroutine(ChangeCharacters(index));
        }
        private static bool ChangesIdentity(bool replace, DialogueCharacter current, DialogueCharacter incoming)
        {
            if (!replace || current == null && incoming == null) return false;
            if (incoming != null && incoming.Portrait != null && string.IsNullOrEmpty(incoming.CharacterKey))
                Debug.LogWarning("Dialogue character has no stable CharacterKey; using explicit replacement compatibility mode.");
            return !DialogueCharacter.SameIdentity(current, incoming);
        }

        private IEnumerator ChangeCharacters(int index)
        {
            yield return null; // Ensure StartCoroutine has returned before the routine may clear its owner field.
            while (ReadingPaused) yield return null;
            DialogueLine line = activeLines[index];
            bool changeLeft = ChangesIdentity(line.ReplaceLeft, left, line.LeftCharacter);
            bool changeRight = ChangesIdentity(line.ReplaceRight, right, line.RightCharacter);
            bool changeCenter = ChangesIdentity(line.ReplaceCenter, center, line.CenterCharacter);
            PresentationEffectHandle a = changeLeft && leftPortrait.enabled ? effects.Play(PresentationEffectIds.PortraitFadeOut, 0) : null;
            PresentationEffectHandle b = changeRight && rightPortrait.enabled ? effects.Play(PresentationEffectIds.PortraitFadeOut, 1) : null;
            PresentationEffectHandle c = changeCenter && centerPortrait.enabled ? effects.Play(PresentationEffectIds.PortraitFadeOut, 2) : null;
            while ((a != null && !a.IsDone) || (b != null && !b.IsDone) || (c != null && !c.IsDone)) yield return null;
            if ((a != null && !a.IsCompleted) || (b != null && !b.IsCompleted) || (c != null && !c.IsCompleted)) { FinishPortraitChange(); yield break; }
            while (ReadingPaused) yield return null;
            ShowLine(index);
            a = changeLeft && leftPortrait.enabled ? effects.Play(PresentationEffectIds.EnterShadowWalk, 0) : null;
            b = changeRight && rightPortrait.enabled ? effects.Play(PresentationEffectIds.EnterShadowWalk, 1) : null;
            c = changeCenter && centerPortrait.enabled ? effects.Play(PresentationEffectIds.EnterShadowWalk, 2) : null;
            while ((a != null && !a.IsDone) || (b != null && !b.IsDone) || (c != null && !c.IsDone)) yield return null;
            while (ReadingPaused) yield return null;
            FinishPortraitChange();
            if (IsPlaying && script != null) PlayLineAccent(activeLines[LineIndex]);
        }
        /// <summary>Default left/right switch: fade out, swap resources while hidden without mirroring, then fade in.</summary>
        public void SwapPortraits(Action completed = null)
        {
            if (!IsReady || exiting) return;
            ready = false;
            continueButton.interactable = false;
            portraitChange = StartCoroutine(SwapPortraitRoutine(completed));
        }
        private IEnumerator SwapPortraitRoutine(Action completed)
        {
            yield return null;
            while (ReadingPaused) yield return null;
            PresentationEffectHandle a = effects.Play(PresentationEffectIds.PortraitFadeOut, 0);
            PresentationEffectHandle b = effects.Play(PresentationEffectIds.PortraitFadeOut, 1);
            while (!a.IsDone || !b.IsDone) yield return null;
            if (!a.IsCompleted || !b.IsCompleted) { FinishPortraitChange(); yield break; }
            while (ReadingPaused) yield return null;
            DialogueCharacter original = left; left = right; right = original;
            ApplyPortrait(leftPortrait, leftPortraitFrame, left);
            ApplyPortrait(rightPortrait, rightPortraitFrame, right);
            UpdatePortraitLayout(centerPortrait.enabled);
            RefreshSpeakerName();
            a = effects.Play(PresentationEffectIds.PortraitFadeIn, 0);
            b = effects.Play(PresentationEffectIds.PortraitFadeIn, 1);
            while (!a.IsDone || !b.IsDone) yield return null;
            while (ReadingPaused) yield return null;
            bool success = a.IsCompleted && b.IsCompleted;
            FinishPortraitChange();
            if (success) completed?.Invoke();
        }
        private void FinishPortraitChange()
        {
            portraitChange = null;
            if (!IsPlaying || script == null) return;
            ready = true;
            continueButton.interactable = !optionsVisible;
            effects.SetFocus(activeLines[LineIndex].Speaker);
        }
        private static int SpeakerSlot(DialogueSpeaker speaker)
            => speaker == DialogueSpeaker.Left ? 0 : speaker == DialogueSpeaker.Right ? 1
                : speaker == DialogueSpeaker.Center ? 2 : -1;

        private void RefreshSpeakerName()
        {
            DialogueLine line = activeLines[LineIndex];
            string name = line.Speaker == DialogueSpeaker.Left ? left?.DisplayName
                : line.Speaker == DialogueSpeaker.Right ? right?.DisplayName
                : line.Speaker == DialogueSpeaker.Center ? center?.DisplayName : "旁白";
            speakerText.text = string.IsNullOrEmpty(line.SpeakerNameOverride) ? name ?? "旁白" : line.SpeakerNameOverride;
        }
    }
}

namespace Spotlight.GameFlow
{
    /// <summary>End-of-script single-choice UI. Presentation snapshots only; no NCS settlement or save mutation.</summary>
    public sealed partial class DialoguePanel
    {
        private readonly List<Button> optionButtons = new List<Button>();
        [SerializeField] private GameObject optionRoot;
        [SerializeField] private CanvasGroup optionGroup;
        private TMP_Text optionPrompt;
        [SerializeField] private RectTransform optionContent;
        [SerializeField] private ScrollRect optionScroll;
        [SerializeField] private DialogueOptionView optionTemplate;
        [SerializeField] private Image optionDimmer;
        private float optionsBaseHeight;
        private Tween optionFeedback;
        private bool optionsVisible;
        public bool IsChoosing => IsPlaying && optionsVisible;
        public IReadOnlyList<DialogueOption> DisplayedOptions => script?.Options ?? Array.Empty<DialogueOption>();
        public string SelectedOptionId { get; private set; } = string.Empty;

        private void CreateOptionOverlay()
        {
            if (optionRoot != null && optionTemplate != null)
            {
                optionsBaseHeight = ((RectTransform)optionRoot.transform).rect.height;
                optionRoot.SetActive(false);
                optionTemplate.gameObject.SetActive(false);
                return;
            }
            // Owned by this panel, like EffectStage. Existing prefabs need no destructive rebuilding.
            optionRoot = OptionObject(transform, "DialogueOptions", typeof(Image), typeof(CanvasGroup));
            var rect = (RectTransform)optionRoot.transform;
            OptionRect(rect, new Vector2(.10f, .35f), new Vector2(.70f, .77f));
            Image surface = optionRoot.GetComponent<Image>();
            surface.color = new Color(.035f, .027f, .022f, .98f);
            optionGroup = optionRoot.GetComponent<CanvasGroup>();
            optionPrompt = OptionText(rect, "Prompt", new Vector2(.05f, .81f), new Vector2(.95f, .97f), 26f);
            optionPrompt.color = new Color(1f, .88f, .66f);
            optionPrompt.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject viewport = OptionObject(rect, "Viewport", typeof(Image), typeof(RectMask2D));
            var viewRect = (RectTransform)viewport.transform;
            OptionRect(viewRect, new Vector2(.05f, .07f), new Vector2(.95f, .78f));
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, .01f);
            GameObject content = OptionObject(viewRect, "Content", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            optionContent = (RectTransform)content.transform;
            optionContent.anchorMin = new Vector2(0f, 1f); optionContent.anchorMax = Vector2.one;
            optionContent.pivot = new Vector2(.5f, 1f);
            optionContent.offsetMin = optionContent.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 9f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            optionScroll = viewport.AddComponent<ScrollRect>();
            optionScroll.viewport = viewRect; optionScroll.content = optionContent;
            optionScroll.horizontal = false; optionScroll.vertical = true;
            optionScroll.movementType = ScrollRect.MovementType.Clamped;
            optionScroll.scrollSensitivity = 35f;
            optionRoot.SetActive(false);
        }

        private bool TryShowOptions()
        {
            if (!IsPlaying || !IsReady || exiting || optionsVisible || !string.IsNullOrEmpty(SelectedOptionId)
                || script.Options.Count == 0 || IsTyping || page < dialogueText.textInfo.pageCount
                || LineIndex != activeLines.Count - 1) return false;
            IsSkipping = false;
            skipElapsed = 0f;
            optionsVisible = true;
            ClearOptionButtons();
            if (optionPrompt != null) optionPrompt.text = string.IsNullOrEmpty(script.OptionPrompt) ? "你要怎么回应？" : script.OptionPrompt;
            for (int i = 0; i < script.Options.Count; i++)
            {
                DialogueOption option = script.Options[i];
                if (optionTemplate != null)
                {
                    DialogueOptionView view = Instantiate(optionTemplate, optionContent, false);
                    view.name = "Option_" + option.OptionId;
                    view.Bind(option, optionEnabledSprite, optionDisabledSprite, id => SelectOption(id));
                    view.gameObject.SetActive(true);
                    optionButtons.Add(view.Button);
                    continue;
                }
                GameObject row = OptionObject(optionContent, "Option_" + option.OptionId,
                    typeof(Image), typeof(Button), typeof(LayoutElement));
                LayoutElement size = row.GetComponent<LayoutElement>();
                size.minHeight = 58f; size.preferredHeight = 76f;
                Image image = row.GetComponent<Image>();
                image.sprite = option.Enabled ? optionEnabledSprite : optionDisabledSprite;
                image.type = option.Enabled && optionEnabledSprite != null || !option.Enabled && optionDisabledSprite != null
                    ? Image.Type.Sliced : Image.Type.Simple;
                image.color = Color.white;
                Button button = row.GetComponent<Button>();
                button.targetGraphic = image; button.interactable = option.Enabled;
                button.transition = Selectable.Transition.ColorTint;
                // Input is an explicit click, never the dialogue's Space/Enter submission.
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                ColorBlock colors = button.colors;
                colors.highlightedColor = new Color(1f, .89f, .68f);
                colors.pressedColor = new Color(.64f, .80f, .66f);
                colors.disabledColor = new Color(.48f, .48f, .48f);
                button.colors = colors;
                TMP_Text label = OptionText(row.transform, "Label", new Vector2(.035f, .10f), new Vector2(.965f, .90f), 24f);
                label.textWrappingMode = TextWrappingModes.Normal;
                label.overflowMode = TextOverflowModes.Overflow;
                label.text = (i + 1) + ".  " + option.Text;
                if (!option.Enabled)
                {
                    label.text += "\n[" + (string.IsNullOrEmpty(option.RequirementHint) ? "条件未满足" : option.RequirementHint) + "]";
                    label.color = new Color(.40f, .27f, .20f);
                }
                Canvas.ForceUpdateCanvases();
                float availableWidth = Mathf.Max(120f, optionContent.rect.width - 28f);
                float preferredTextHeight = label.GetPreferredValues(label.text, availableWidth, 0f).y;
                size.preferredHeight = Mathf.Max(58f, preferredTextHeight + 28f);
                string id = option.OptionId;
                button.onClick.AddListener(() => SelectOption(id));
                Spotlight.Audio.UIButtonAudio.Configure(button, string.Empty);
                optionButtons.Add(button);
            }
            optionRoot.SetActive(true);
            optionGroup.alpha = 1f;
            optionGroup.interactable = optionGroup.blocksRaycasts = true;
            continueButton.interactable = false;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(optionContent);
            if (optionTemplate != null)
            {
                RectTransform rect = (RectTransform)optionRoot.transform;
                float available = artworkRoot != null ? artworkRoot.rect.height * .53f : optionsBaseHeight;
                float height = Mathf.Min(optionContent.rect.height, Mathf.Max(optionsBaseHeight, available));
                // Fit small lists exactly at the PSD center; keep larger lists bounded and scrollable.
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                LayoutRebuilder.ForceRebuildLayoutImmediate(optionContent);
            }
            if (optionDimmer != null) optionDimmer.gameObject.SetActive(true);
            optionScroll.verticalNormalizedPosition = 1f;
            UpdateHint();
            return true;
        }

        /// <summary>Accept only a visible, enabled option once. Results are not executed here.</summary>
        public Func<string, bool> OptionSelectionRequested { get; set; }

        public bool SelectOption(string optionId)
        {
            if (ReadingPaused || InputSuspended || IsExternalInputBlocked
                || !IsChoosing || !string.IsNullOrEmpty(SelectedOptionId) || !IsReady) return false;
            int index = -1;
            for (int i = 0; i < script.Options.Count; i++)
                if (string.Equals(script.Options[i].OptionId, optionId, StringComparison.Ordinal)) { index = i; break; }
            if (index < 0 || !script.Options[index].Enabled) return false;
            DialogueOption option = script.Options[index];
            if (OptionSelectionRequested != null && !OptionSelectionRequested(optionId)) return false;
            RecordSelectedOption(option.Text);
            SelectedOptionId = option.OptionId;
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Confirm);
            optionGroup.interactable = false;
            for (int i = 0; i < optionButtons.Count; i++) optionButtons[i].interactable = false;
            HideOptions();
            optionsVisible = false;
            hold = 0f;
            if (option.Response != null)
            {
                activeLines.Add(AssignSpeakerToOpenSlot(option.Response, new[] { left, right, center }));
                PrepareNextLine(activeLines.Count - 1);
            }
            else if (OptionSelectionRequested != null)
            {
                // The selected NCS branch owns the response, not a feedback page.
                SeamlessChoicePending = true;
                IsPlaying = ready = false;
                Action callback = finished;
                finished = null;
                callback?.Invoke();
            }
            continueButton.interactable = IsReady;
            UpdateHint();
            return true;
        }

        public bool SeamlessChoicePending { get; private set; }

        private void ResetOptions()
        {
            SeamlessChoicePending = false;
            OptionSelectionRequested = null;
            optionFeedback?.Kill(false);
            optionFeedback = null;
            optionsVisible = false;
            SelectedOptionId = string.Empty;
            HideOptions();
        }

        private void HideOptions()
        {
            if (optionGroup != null) optionGroup.interactable = optionGroup.blocksRaycasts = false;
            if (optionScroll != null) optionScroll.StopMovement();
            if (optionRoot != null) optionRoot.SetActive(false);
            if (optionDimmer != null) optionDimmer.gameObject.SetActive(false);
            ClearOptionButtons();
        }

        private void ClearOptionButtons()
        {
            for (int i = 0; i < optionButtons.Count; i++)
            {
                Button button = optionButtons[i];
                if (button == null) continue;
                button.onClick.RemoveAllListeners();
                button.gameObject.SetActive(false);
                button.transform.SetParent(null, false);
                Destroy(button.gameObject);
            }
            optionButtons.Clear();
        }

        private static GameObject OptionObject(Transform parent, string name, params Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            foreach (Type component in components) go.AddComponent(component);
            return go;
        }

        private static void OptionRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private TMP_Text OptionText(Transform parent, string name, Vector2 min, Vector2 max, float size)
        {
            GameObject go = OptionObject(parent, name, typeof(TextMeshProUGUI));
            OptionRect((RectTransform)go.transform, min, max);
            TMP_Text label = go.GetComponent<TMP_Text>();
            label.font = dialogueText.font != null ? dialogueText.font : speakerText.font; label.fontSize = size;
            label.enableAutoSizing = true; label.fontSizeMin = 17f; label.fontSizeMax = size;
            label.richText = false; label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = new Color(.96f, .94f, .87f);
            return label;
        }
    }
}
