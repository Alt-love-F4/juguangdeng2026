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
        [SerializeField] private Button continueButton;
        [SerializeField] private Button cancelButton;
        [Header("Playback (unscaled time)")]
        [SerializeField, Min(0f)] private float enterSeconds = .4f;
        [SerializeField, Min(0f)] private float exitSeconds = .2f;
        [SerializeField, Min(0f)] private float focusSeconds = .18f;
        [SerializeField, Min(0f)] private float charactersPerSecond = 35f;
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
        private Sprite defaultLeft;
        private Sprite defaultRight;
        private Texture defaultBackground;
        private Vector2 boxPosition;
        private Sequence transition;
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

        public static bool IsAnyPlaying => active != null && active.IsPlaying;
        public static bool BlocksEscapeThisFrame => IsAnyPlaying || lastCancelFrame == Time.frameCount;
        public bool IsPlaying { get; private set; }
        public bool IsTyping => IsPlaying && ready && dialogueText.maxVisibleCharacters < pageEnd;
        public int LineIndex { get; private set; } = -1;
        public int PageNumber => page;

        protected override void OnInit(IUIData data = null)
        {
            if (canvasGroup == null || background == null || leftPortrait == null || rightPortrait == null
                || dialogueBox == null || speakerText == null || dialogueText == null || continueButton == null)
                throw new InvalidOperationException("DialoguePanel has incomplete prefab bindings.");
            defaultLeft = leftPortrait.sprite;
            defaultRight = rightPortrait.sprite;
            defaultBackground = background.texture;
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
            continueButton.onClick.AddListener(Advance);
            if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
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
            Stop();
            script = value;
            activeLines.Clear();
            for (int i = 0; i < value.Count; i++) activeLines.Add(value[i]);
            SelectedOptionId = string.Empty;
            left = value.LeftCharacter ?? new DialogueCharacter(defaultLeftName, defaultLeft);
            right = value.RightCharacter ?? new DialogueCharacter(defaultRightName, defaultRight);
            center = value.CenterCharacter;
            background.texture = value.Background != null ? value.Background : defaultBackground;
            if (titleText != null) titleText.text = string.IsNullOrEmpty(value.Title) ? "角色对话" : value.Title;
            finished = onFinished;
            cancelled = onCancelled;
            active = this;
            IsPlaying = true;
            lastAdvanceFrame = Time.frameCount;
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = canvasGroup.interactable = true;
            continueButton.interactable = false;
            leftPortrait.color = rightPortrait.color = centerPortrait.color = Color.white;
            UnityEngine.EventSystems.EventSystem system = UnityEngine.EventSystems.EventSystem.current;
            if (system != null) system.SetSelectedGameObject(null);
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

        private Sequence Sequence() => DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        private static Tween Move(RectTransform rect, Vector2 target, float seconds)
            => DOTween.To(() => rect.anchoredPosition, v => rect.anchoredPosition = v, target, seconds);

        private void ShowLine(int index)
        {
            LineIndex = index;
            DialogueLine line = activeLines[index];
            if (line.ReplaceLeft) left = line.LeftCharacter;
            if (line.ReplaceRight) right = line.RightCharacter;
            if (line.ReplaceCenter) center = line.CenterCharacter;
            ApplyPortrait(leftPortrait, leftPortraitFrame, left);
            ApplyPortrait(rightPortrait, rightPortraitFrame, right);
            ApplyPortrait(centerPortrait, centerPortraitFrame, center);
            UpdatePortraitLayout(centerPortrait.enabled);
            RefreshSpeakerName();
            dialogueText.text = line.Text;
            dialogueText.maxVisibleCharacters = int.MaxValue;
            dialogueText.pageToDisplay = 1;
            Canvas.ForceUpdateCanvases();
            dialogueText.ForceMeshUpdate();
            ShowPage(1);
            if (ready) AnimateFocus();
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
            revealed = charactersPerSecond <= 0f && ready ? pageEnd : pageStart;
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
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Cancel(); return; }
            if (!ready || blockingEffects > 0 || portraitChange != null || optionsVisible) return;
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
            if (!IsPlaying || !ready || optionsVisible || exiting || blockingEffects > 0 || portraitChange != null || lastAdvanceFrame == Time.frameCount) return;
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
            hintText.text = IsTyping || dialogueText.maxVisibleCharacters < pageEnd ? "空格 / 回车 / 点击 · 显示全文"
                : page < dialogueText.textInfo.pageCount ? "空格 / 回车 / 点击 · 下一页"
                : LineIndex + 1 < activeLines.Count ? "空格 / 回车 / 点击 · 继续"
                : script.Options.Count > 0 && string.IsNullOrEmpty(SelectedOptionId) ? "即将显示选项"
                : string.IsNullOrEmpty(SelectedOptionId) ? "空格 / 回车 / 点击 · 结束对话"
                : "已选择回应 · 空格 / 回车 / 点击结束";
        }

        public void Cancel()
        {
            if (!IsPlaying) return;
            lastCancelFrame = Time.frameCount;
            Action callback = cancelled;
            Stop();
            CloseSelf();
            callback?.Invoke();
        }

        /// <summary>静默中止，不触发完成/取消回调；关闭、隐藏、销毁均调用此方法。</summary>
        public void Stop()
        {
            finished = cancelled = null;
            CancelEffectWork();
            script = null;
            activeLines.Clear();
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
            if (dialogueBox != null) dialogueBox.anchoredPosition = boxPosition;
            if (effects != null) effects.Reset();
        }

        protected override void OnClose() => Stop();
        protected override void OnWillHide() => Stop();
        protected override void OnBeforeDestroy()
        {
            Stop();
            if (effects != null) effects.Dispose();
            if (continueButton != null) continueButton.onClick.RemoveListener(Advance);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
            base.OnBeforeDestroy();
        }
        private void OnDisable() => Stop();
    }
}
