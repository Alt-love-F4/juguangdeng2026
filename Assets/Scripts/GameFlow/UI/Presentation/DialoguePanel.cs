using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>兼容双角色的三角色对话演出。UIKit 管理实例；不推进日程、不修改剧情逻辑状态。</summary>
    public sealed partial class DialoguePanel : UIPanel, IDialoguePresentation, IPresentationEffectPlayer
    {
        [Header("Prefab bindings")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RawImage background;
        [SerializeField] private Image leftPortrait;
        [SerializeField] private Image rightPortrait;
        [SerializeField] private Image centerPortrait;
        [SerializeField] private Image leftPortraitFrame;
        [SerializeField] private Image rightPortraitFrame;
        [SerializeField] private Image centerPortraitFrame;
        [SerializeField] private RectTransform dialogueBox;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private TMP_Text passiveHintText;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Sprite optionEnabledSprite;
        [SerializeField] private Sprite optionDisabledSprite;
        [SerializeField] private RectTransform artworkRoot;
        [SerializeField] private GameObject psdBackground;
        [SerializeField] private Button dialogueBodyButton;
        [SerializeField] private Button skipButton;
        [Header("Adaptive dialogue box (UGUI reference pixels)")]
        [SerializeField] private bool adaptDialogueHeight;
        [SerializeField, Min(1)] private int maximumDialogueLines = 3;
        [SerializeField, Min(1f)] private float dialogueMinHeight = 251.72f;
        [SerializeField, Min(0f)] private float dialoguePaddingHeight = 137f;
        [Header("Playback (unscaled time)")]
        [SerializeField, Min(0f)] private float enterSeconds = .4f;
        [SerializeField, Min(0f)] private float exitSeconds = .2f;
        [SerializeField, Min(0f)] private float focusSeconds = .18f;
        [SerializeField, Min(0f)] private float charactersPerSecond = 35f;
        [SerializeField, Min(.02f)] private float skipPageSeconds = .1f;
        [SerializeField] private Color inactiveTint = new Color(.5f, .47f, .43f, .85f);
        [SerializeField] private string defaultLeftName = "咖啡馆店主";
        [SerializeField] private string defaultRightName = "印刷工";

        private static DialoguePanel active;
        private static int lastCancelFrame = -1;
        private DialogueScript script;
        private readonly List<DialogueLine> activeLines = new List<DialogueLine>();
        private DialogueCharacter left;
        private DialogueCharacter right;
        private DialogueCharacter center;
        private PortraitLayout leftFrameLayout, rightFrameLayout, centerFrameLayout;
        private PortraitLayout leftImageLayout, rightImageLayout, centerImageLayout;
        private bool threePortraitLayout;
        private int artworkPortraitSlots = -1;
        private Sprite defaultLeft;
        private Sprite defaultRight;
        private Vector2 boxPosition;
        private Sequence transition;
        private IDisposable pauseOwner;
        public event Action PlaybackStopped;
        private Action finished;
        private Action cancelled;
        private bool ready;
        private bool exiting;
        private int page;
        private int pageStart;
        private int pageEnd;
        private int lastAdvanceFrame = -1;
        private float revealed;
        private float hold;
        private float skipElapsed;
        private float configuredCharactersPerSecond;

        public bool IsSkipping { get; private set; }
        private bool allowCancel = true;
        public bool AllowCancel
        {
            get => allowCancel;
            set
            {
                allowCancel = value;
                if (cancelButton != null) cancelButton.interactable = value;
            }
        }

        public static bool IsAnyPlaying => active != null && active.IsPlaying;
        public static bool BlocksEscapeThisFrame => IsAnyPlaying || lastCancelFrame == Time.frameCount;
        public bool IsPlaying { get; private set; }
        public bool InputSuspended { get; set; }
        public bool IsTyping => IsPlaying && ready && dialogueText.maxVisibleCharacters < pageEnd;
        public int LineIndex { get; private set; } = -1;
        public int PageNumber => page;

        protected override void OnInit(IUIData data = null)
        {
            Spotlight.Audio.UIButtonAudio.Install(gameObject);
            Spotlight.Audio.UIButtonAudio.Configure(continueButton, string.Empty);
            Spotlight.Audio.UIButtonAudio.Configure(dialogueBodyButton, string.Empty);
            Spotlight.Audio.UIButtonAudio.Configure(cancelButton, string.Empty);
            if (canvasGroup == null || background == null || leftPortrait == null || rightPortrait == null
                || dialogueBox == null || speakerText == null || dialogueText == null || continueButton == null)
                throw new InvalidOperationException("DialoguePanel has incomplete prefab bindings.");
            configuredCharactersPerSecond = charactersPerSecond;
            defaultLeft = leftPortrait.sprite;
            defaultRight = rightPortrait.sprite;
            boxPosition = dialogueBox.anchoredPosition;
            dialogueText.overflowMode = TextOverflowModes.Page;
            // 台词作为纯文本，避免外部剧本中的 <...> 被当成 TMP 标签。
            dialogueText.richText = false;
            EnsureCenterPortrait();
            leftFrameLayout = new PortraitLayout(leftPortraitFrame?.rectTransform);
            rightFrameLayout = new PortraitLayout(rightPortraitFrame?.rectTransform);
            leftImageLayout = new PortraitLayout(leftPortrait.rectTransform);
            rightImageLayout = new PortraitLayout(rightPortrait.rectTransform);
            centerFrameLayout = new PortraitLayout(centerPortraitFrame?.rectTransform);
            centerImageLayout = new PortraitLayout(centerPortrait.rectTransform);
            InitializeEffects();
            CreateOptionOverlay();
            CreateHistoryView();
            continueButton.onClick.AddListener(Advance);
            if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
            if (dialogueBodyButton != null) dialogueBodyButton.onClick.AddListener(Advance);
            if (skipButton != null) skipButton.onClick.AddListener(Skip);
        }

        protected override void OnOpen(IUIData data = null)
        {
            Stop();
            UIKit.SetPanelModal(this, true);
        }

        protected override void OnShow()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
        }

        public void Play(DialogueScript value, Action onFinished = null, Action onCancelled = null)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            for (int i = 0; i < value.Count; i++)
                if (!string.IsNullOrWhiteSpace(value[i].AccentFxKey)) effectLibrary.Find(value[i].AccentFxKey);
            bool seamless = SeamlessChoicePending && script != null && value.ContinuityOwner != null
                && ReferenceEquals(script.ContinuityOwner, value.ContinuityOwner);
            CaptureHistory();
            if (!seamless) Stop();
            else { ResetOptions(); finished = cancelled = null; ready = false; }
            if (pauseOwner == null) pauseOwner = PresentationPauseRegistry.Acquire(nameof(DialoguePanel));
            try
            {
            script = value;
            BeginHistory(value, seamless);
            if (!value.IsPreview) charactersPerSecond = configuredCharactersPerSecond;
            activeLines.Clear();
            if (passiveHintText != null) { passiveHintText.text = string.Empty; passiveHintText.gameObject.SetActive(false); }
            for (int i = 0; i < value.Count; i++) activeLines.Add(value[i]);
            SelectedOptionId = string.Empty;
            if (!seamless)
            {
            left = value.LeftCharacter ?? new DialogueCharacter(defaultLeftName, defaultLeft);
            right = value.RightCharacter ?? new DialogueCharacter(defaultRightName, defaultRight);
            center = value.CenterCharacter;
            background.texture = value.Background;
            background.enabled = value.Background != null;
            }
            if (psdBackground != null) psdBackground.SetActive(false);
            if (titleText != null) titleText.text = string.IsNullOrEmpty(value.Title) ? "角色对话" : value.Title;
            finished = onFinished;
            cancelled = onCancelled;
            if (cancelButton != null) cancelButton.interactable = AllowCancel;
            active = this;
            IsPlaying = true;
            lastAdvanceFrame = Time.frameCount;
            canvasGroup.alpha = seamless ? 1f : 0f;
            canvasGroup.blocksRaycasts = canvasGroup.interactable = true;
            continueButton.interactable = false;
            if (!seamless) leftPortrait.color = rightPortrait.color = centerPortrait.color = Color.white;
            UnityEngine.EventSystems.EventSystem system = UnityEngine.EventSystems.EventSystem.current;
            if (system != null) system.SetSelectedGameObject(null);
            if (seamless)
            {
                if (value.Background != null) { background.texture = value.Background; background.enabled = true; }
                dialogueBox.anchoredPosition = boxPosition;
                entranceComplete = true;
                LineIndex = -1;
                PrepareNextLine(0);
                return;
            }
            ShowLine(0);
            dialogueBox.anchoredPosition = boxPosition + new Vector2(0f, -32f);
            EnterVisiblePortraits();
            transition = Sequence();
            transition.Append(DOTween.To(() => canvasGroup.alpha, v => canvasGroup.alpha = v, 1f, enterSeconds));
            transition.Join(Move(dialogueBox, boxPosition, enterSeconds).SetEase(Ease.OutCubic));

            transition.OnComplete(() =>
            {
                transition = null;
                entranceComplete = true;
            });
            }
            catch { Stop(); throw; }
        }

        private Sequence Sequence() => DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        private static Tween Move(RectTransform rect, Vector2 target, float seconds)
            => DOTween.To(() => rect.anchoredPosition, v => rect.anchoredPosition = v, target, seconds);

        private void ShowLine(int index)
        {
            CaptureHistory();
            Spotlight.Audio.GameAudio.StopOwner(voiceOwner);
            LineIndex = index;
            DialogueLine line = activeLines[index];
            if (!IsSkipping)
            {
                Spotlight.Audio.GameAudio.PlayNarrative(line.SfxKey, this);
                Spotlight.Audio.GameAudio.PlayNarrative(line.VoiceKey, voiceOwner, voice: true);
            }
            if (line.ReplaceLeft) left = DialogueCharacter.Resolve(left, line.LeftCharacter);
            if (line.ReplaceRight) right = DialogueCharacter.Resolve(right, line.RightCharacter);
            if (line.ReplaceCenter) center = DialogueCharacter.Resolve(center, line.CenterCharacter);
            ApplyPortrait(leftPortrait, leftPortraitFrame, left);
            ApplyPortrait(rightPortrait, rightPortraitFrame, right);
            ApplyPortrait(centerPortrait, centerPortraitFrame, center);
            UpdatePortraitLayout(centerPortrait.enabled);
            RefreshSpeakerName();
            dialogueText.text = line.Text;
            dialogueText.maxVisibleCharacters = int.MaxValue;
            dialogueText.pageToDisplay = 1;
            Canvas.ForceUpdateCanvases();
            FitDialogueHeight();
            dialogueText.ForceMeshUpdate();
            ShowPage(1);
            if (ready) AnimateFocus();
        }

        private void FitDialogueHeight()
        {
            if (!adaptDialogueHeight) return;
            // Measure the whole line, not the typewriter's visible characters, so it never jumps while typing.
            float width = Mathf.Max(1f, dialogueText.rectTransform.rect.width);
            float preferred = dialogueText.GetPreferredValues(dialogueText.text, width, Mathf.Infinity).y;
            string lines = "字";
            for (int i = 1; i < maximumDialogueLines; i++) lines += "\n字";
            float maximum = dialogueText.GetPreferredValues(lines, width, Mathf.Infinity).y + 2f;
            float height = Mathf.Max(dialogueMinHeight, Mathf.Min(preferred + 2f, maximum) + dialoguePaddingHeight);
            dialogueBox.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            Vector2 offset = dialogueText.rectTransform.offsetMin;
            offset.y = Mathf.Max(72f, height - 65f - maximum);
            dialogueText.rectTransform.offsetMin = offset;
            Canvas.ForceUpdateCanvases();
        }

        // Old prefab variants need not be rebuilt merely to gain the optional third slot.
        private void EnsureCenterPortrait()
        {
            if (centerPortrait != null) return;
            GameObject frame = OptionObject(transform, "CenterPortraitFrame", typeof(Image));
            centerPortraitFrame = frame.GetComponent<Image>();
            OptionRect(centerPortraitFrame.rectTransform, new Vector2(.36f, .33f), new Vector2(.64f, .89f));
            centerPortraitFrame.color = leftPortraitFrame != null ? leftPortraitFrame.color : Color.clear;
            centerPortraitFrame.raycastTarget = false;
            centerPortrait = OptionObject(frame.transform, "CenterPortrait", typeof(Image)).GetComponent<Image>();
            OptionRect(centerPortrait.rectTransform, Vector2.zero, Vector2.one);
            centerPortrait.preserveAspect = true;
            centerPortrait.raycastTarget = false;
            ApplyPortrait(centerPortrait, centerPortraitFrame, null);
        }

        private readonly struct PortraitLayout
        {
            private readonly RectTransform rect;
            private readonly Vector2 anchorMin, anchorMax, position, size, pivot;
            public PortraitLayout(RectTransform rect)
            {
                this.rect = rect;
                anchorMin = rect != null ? rect.anchorMin : Vector2.zero;
                anchorMax = rect != null ? rect.anchorMax : Vector2.zero;
                position = rect != null ? rect.anchoredPosition : Vector2.zero;
                size = rect != null ? rect.sizeDelta : Vector2.zero;
                pivot = rect != null ? rect.pivot : new Vector2(.5f, .5f);
            }
            public void Restore()
            {
                if (rect == null) return;
                rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot;
                rect.anchoredPosition = position; rect.sizeDelta = size;
            }
        }

        private void UpdatePortraitLayout(bool showThird)
        {
            if (artworkRoot != null)
            {
                int slots = (leftPortrait.enabled ? 1 : 0) | (rightPortrait.enabled ? 2 : 0)
                    | (centerPortrait.enabled ? 4 : 0);
                if (artworkPortraitSlots == slots) return;
                artworkPortraitSlots = slots;
                leftFrameLayout.Restore(); rightFrameLayout.Restore(); centerFrameLayout.Restore();
                leftImageLayout.Restore(); rightImageLayout.Restore(); centerImageLayout.Restore();
                int count = (leftPortrait.enabled ? 1 : 0) + (centerPortrait.enabled ? 1 : 0)
                    + (rightPortrait.enabled ? 1 : 0);
                int index = 0;
                if (leftPortrait.enabled) PlaceArtworkPortrait(leftPortrait, leftPortraitFrame, index++, count);
                if (centerPortrait.enabled) PlaceArtworkPortrait(centerPortrait, centerPortraitFrame, index++, count);
                if (rightPortrait.enabled) PlaceArtworkPortrait(rightPortrait, rightPortraitFrame, index, count);
                effects?.RefreshPortraitPositions();
                return;
            }
            if (threePortraitLayout == showThird) return;
            threePortraitLayout = showThird;
            if (showThird)
            {
                SetPortraitBounds(leftPortrait, leftPortraitFrame, .035f, .315f);
                SetPortraitBounds(centerPortrait, centerPortraitFrame, .36f, .64f);
                SetPortraitBounds(rightPortrait, rightPortraitFrame, .685f, .965f);
            }
            else
            {
                // Keep all original artist-authored offsets/sizes for existing two-person scripts.
                leftFrameLayout.Restore(); rightFrameLayout.Restore();
                leftImageLayout.Restore(); rightImageLayout.Restore();
                centerFrameLayout.Restore(); centerImageLayout.Restore();
            }
            effects?.RefreshPortraitPositions();
        }

        private static void PlaceArtworkPortrait(Image image, Image frame, int index, int count)
        {
            RectTransform outer = frame != null ? frame.rectTransform : image.rectTransform;
            float center = count == 1 ? .5f : count == 2 ? .25f + index * .5f : .175f + index * .325f;
            float shift = center - (outer.anchorMin.x + outer.anchorMax.x) * .5f;
            outer.anchorMin += new Vector2(shift, 0f);
            outer.anchorMax += new Vector2(shift, 0f);
        }

        private static void SetPortraitBounds(Image image, Image frame, float min, float max)
        {
            RectTransform outer = frame != null ? frame.rectTransform : image.rectTransform;
            OptionRect(outer, new Vector2(min, .33f), new Vector2(max, .89f));
            outer.pivot = new Vector2(.5f, .5f);
            if (frame != null) OptionRect(image.rectTransform, Vector2.zero, Vector2.one);
        }

        private static void ApplyPortrait(Image image, Image frame, DialogueCharacter character)
        {
            image.sprite = character == null ? null : character.Portrait;
            image.enabled = image.sprite != null;
            if (frame != null) frame.enabled = image.enabled;
        }

        private void ShowPage(int number)
        {
            page = number;
            if (passiveHintText != null)
            {
                passiveHintText.text = number == 1 ? activeLines[LineIndex].PassiveHint ?? string.Empty : string.Empty;
                passiveHintText.gameObject.SetActive(!string.IsNullOrEmpty(passiveHintText.text));
            }
            dialogueText.pageToDisplay = number;
            TMP_TextInfo info = dialogueText.textInfo;
            if (info.characterCount == 0 || info.pageCount == 0)
                pageStart = pageEnd = 0;
            else
            {
                TMP_PageInfo range = info.pageInfo[number - 1];
                pageStart = range.firstCharacterIndex;
                pageEnd = Math.Min(info.characterCount, range.lastCharacterIndex + 1);
            }
            revealed = IsSkipping || charactersPerSecond <= 0f && ready ? pageEnd : pageStart;
            skipElapsed = 0f;
            hold = 0f;
            dialogueText.maxVisibleCharacters = (int)revealed;
            if (progressText != null)
                progressText.text = (LineIndex + 1) + " / " + activeLines.Count
                    + (info.pageCount > 1 ? "    页 " + page + " / " + info.pageCount : string.Empty);
            UpdateHint();
        }

        private void AnimateFocus()
        {
            effects.SetFocus(activeLines[LineIndex].Speaker);
            PlayLineAccent(activeLines[LineIndex]);
        }

        private void Update()
        {
            CaptureHistory();
            ReadingInput();
            UpdateControlSkip();
            if (ReadingInputBlocked) return;
            if (effects != null) effects.Tick(Time.unscaledDeltaTime);
            if (!IsPlaying || exiting) return;
            if (!ready && entranceComplete && !effects.IsPortraitTransitioning && portraitChange == null && blockingEffects == 0)
            {
                ready = true;
                continueButton.interactable = true;
                AnimateFocus();
            }
            if (script == null || !IsPlaying) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && AllowCancel) { Cancel(); return; }
            if (!ready || blockingEffects > 0 || portraitChange != null || optionsVisible) return;
            if (IsSkipping)
            {
                if (TryShowOptions()) return;
                skipElapsed += Time.unscaledDeltaTime;
                if (skipElapsed >= Mathf.Max(.02f, skipPageSeconds)) Advance();
                return;
            }
            if (IsTyping)
            {
                revealed += Time.unscaledDeltaTime * charactersPerSecond;
                dialogueText.maxVisibleCharacters = Math.Min(pageEnd, (int)revealed);
                if (charactersPerSecond <= 0f) { revealed = pageEnd; dialogueText.maxVisibleCharacters = pageEnd; }
                if (!IsTyping) UpdateHint();
            }
            else if (activeLines[LineIndex].AutoAdvanceSeconds > 0f)
            {
                hold += Time.unscaledDeltaTime;
                if (hold >= activeLines[LineIndex].AutoAdvanceSeconds) Advance();
            }
            if (TryShowOptions()) return;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame)) Advance();
        }

        /// <summary>第一次提交只补完当前页；下一次才翻页/换句。进退场期间不接受提交。</summary>
        public void Advance()
        {
            if (ReadingInputBlocked || !IsPlaying || !ready || optionsVisible || exiting || blockingEffects > 0 || portraitChange != null || lastAdvanceFrame == Time.frameCount) return;
            lastAdvanceFrame = Time.frameCount;
            if (IsTyping)
            {
                revealed = pageEnd;
                dialogueText.maxVisibleCharacters = pageEnd;
                hold = 0f;
                UpdateHint();
                TryShowOptions();
                return;
            }
            if (page < dialogueText.textInfo.pageCount) { ShowPage(page + 1); return; }
            if (LineIndex + 1 < activeLines.Count) { PrepareNextLine(LineIndex + 1); return; }
            if (TryShowOptions()) return;
            CaptureHistory();
            if (script.Continues)
            {
                SeamlessChoicePending = true;
                IsPlaying = ready = false;
                Action continuation = finished;
                finished = null;
                continuation?.Invoke();
                return;
            }
            IsSkipping = false;
            exiting = true;
            ready = false;
            continueButton.interactable = false;
            effects.Play(PresentationEffectIds.PortraitFadeOut, 0);
            effects.Play(PresentationEffectIds.PortraitFadeOut, 1);
            if (centerPortrait.enabled) effects.Play(PresentationEffectIds.PortraitFadeOut, 2);
            transition = Sequence();
            transition.Append(DOTween.To(() => canvasGroup.alpha, v => canvasGroup.alpha = v, 0f, exitSeconds));
            transition.Join(Move(dialogueBox, boxPosition + new Vector2(0f, -24f), exitSeconds));
            transition.OnComplete(() =>
            {
                transition = null;
                Action callback = finished;
                Stop();
                callback?.Invoke();
            });
        }

        private void UpdateHint()
        {
            if (hintText == null) return;
            if (optionsVisible) { hintText.text = "点击选项作答 · 空格 / 回车不会代选"; return; }
            if (IsSkipping) { hintText.text = "快速播放中 · 遇到选项自动停止"; return; }
            hintText.text = IsTyping || dialogueText.maxVisibleCharacters < pageEnd ? "空格 / 回车 / 点击 · 显示全文"
                : page < dialogueText.textInfo.pageCount ? "空格 / 回车 / 点击 · 下一页"
                : LineIndex + 1 < activeLines.Count ? "空格 / 回车 / 点击 · 继续"
                : script.Options.Count > 0 && string.IsNullOrEmpty(SelectedOptionId) ? "即将显示选项"
                : string.IsNullOrEmpty(SelectedOptionId) ? "空格 / 回车 / 点击 · 结束对话"
                : "已选择回应 · 空格 / 回车 / 点击结束";
        }

        /// <summary>Reveal each page briefly in unscaled time, stopping at choices without selecting them.</summary>
        public void Skip()
        {
            if (ReadingInputBlocked || !IsPlaying || !IsReady || exiting || script == null || optionsVisible) return;
            controlSkipping = false;
            if (IsSkipping) return;
            IsSkipping = true;
            Spotlight.Audio.GameAudio.StopOwner(voiceOwner);
            skipElapsed = 0f;
            revealed = pageEnd;
            dialogueText.maxVisibleCharacters = pageEnd;
            UpdateHint();
            TryShowOptions();
        }

        public void Cancel()
        {
            if (ReadingInputBlocked || !IsPlaying || !AllowCancel) return;
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Cancel);
            lastCancelFrame = Time.frameCount;
            Action callback = cancelled;
            Stop();
            CloseSelf();
            callback?.Invoke();
        }

        /// <summary>静默中止，不触发完成/取消回调；关闭、隐藏、销毁均调用此方法。</summary>
        public void Stop()
        {
            StopReading();
            Spotlight.Audio.GameAudio.StopOwner(voiceOwner);
            Spotlight.Audio.GameAudio.StopOwner(this);
            pauseOwner?.Dispose();
            pauseOwner = null;
            Action stopped = PlaybackStopped;
            PlaybackStopped = null;
            IsSkipping = false;
            controlSkipping = false;
            skipElapsed = 0f;
            finished = cancelled = null;
            AllowCancel = true;
            InputSuspended = false;
            CancelEffectWork();
            script = null;
            activeLines.Clear();
            if (passiveHintText != null) { passiveHintText.text = string.Empty; passiveHintText.gameObject.SetActive(false); }
            ResetOptions();
            left = right = center = null;
            if (centerPortrait != null) ApplyPortrait(centerPortrait, centerPortraitFrame, null);
            UpdatePortraitLayout(false);
            IsPlaying = ready = exiting = false;
            LineIndex = -1;
            if (active == this) active = null;
            transition?.Kill(false);
            transition = null;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = canvasGroup.interactable = false;
            }
            if (continueButton != null) continueButton.interactable = false;
            if (dialogueBox != null)
            {
                dialogueBox.anchoredPosition = boxPosition;
                if (adaptDialogueHeight) dialogueBox.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, dialogueMinHeight);
            }
            if (effects != null) effects.Reset();
            stopped?.Invoke();
        }

        protected override void OnClose() => Stop();
        protected override void OnWillHide() => Stop();
        protected override void OnBeforeDestroy()
        {
            Stop();
            if (effects != null) effects.Dispose();
            if (continueButton != null) continueButton.onClick.RemoveListener(Advance);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
            if (dialogueBodyButton != null) dialogueBodyButton.onClick.RemoveListener(Advance);
            if (skipButton != null) skipButton.onClick.RemoveListener(Skip);
            base.OnBeforeDestroy();
        }
        private readonly object voiceOwner = new object();
        private void OnDisable() => Stop();
    }
}
