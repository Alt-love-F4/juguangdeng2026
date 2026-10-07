using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>双角色对话演出。UIKit 管理实例；不推进日程、不修改剧情逻辑状态。</summary>
    public sealed class DialoguePanel : UIPanel, IDialoguePresentation
    {
        [Header("Prefab bindings")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RawImage background;
        [SerializeField] private Image leftPortrait;
        [SerializeField] private Image rightPortrait;
        [SerializeField] private Image leftPortraitFrame;
        [SerializeField] private Image rightPortraitFrame;
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
        private DialogueCharacter left;
        private DialogueCharacter right;
        private Sprite defaultLeft;
        private Sprite defaultRight;
        private Texture defaultBackground;
        private Vector2 boxPosition;
        private Vector2 leftPosition;
        private Vector2 rightPosition;
        private Vector3 leftScale;
        private Vector3 rightScale;
        private Sequence transition;
        private Sequence focus;
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
            leftPosition = leftPortrait.rectTransform.anchoredPosition;
            rightPosition = rightPortrait.rectTransform.anchoredPosition;
            leftScale = leftPortrait.transform.localScale;
            rightScale = rightPortrait.transform.localScale;
            dialogueText.overflowMode = TextOverflowModes.Page;
            // 台词作为纯文本，避免外部剧本中的 <...> 被当成 TMP 标签。
            dialogueText.richText = false;
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
            Stop();
            script = value;
            left = value.LeftCharacter ?? new DialogueCharacter(defaultLeftName, defaultLeft);
            right = value.RightCharacter ?? new DialogueCharacter(defaultRightName, defaultRight);
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
            leftPortrait.color = rightPortrait.color = Color.white;
            UnityEngine.EventSystems.EventSystem system = UnityEngine.EventSystems.EventSystem.current;
            if (system != null) system.SetSelectedGameObject(null);
            ShowLine(0);
            dialogueBox.anchoredPosition = boxPosition + new Vector2(0f, -32f);
            leftPortrait.rectTransform.anchoredPosition = leftPosition + new Vector2(-65f, 0f);
            rightPortrait.rectTransform.anchoredPosition = rightPosition + new Vector2(65f, 0f);
            transition = Sequence();
            transition.Append(DOTween.To(() => canvasGroup.alpha, v => canvasGroup.alpha = v, 1f, enterSeconds));
            transition.Join(Move(dialogueBox, boxPosition, enterSeconds).SetEase(Ease.OutCubic));
            transition.Join(Move(leftPortrait.rectTransform, leftPosition, enterSeconds).SetEase(Ease.OutCubic));
            transition.Join(Move(rightPortrait.rectTransform, rightPosition, enterSeconds).SetEase(Ease.OutCubic));
            transition.OnComplete(() =>
            {
                transition = null;
                ready = true;
                continueButton.interactable = true;
                AnimateFocus();
            });
        }

        private Sequence Sequence() => DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        private static Tween Move(RectTransform rect, Vector2 target, float seconds)
            => DOTween.To(() => rect.anchoredPosition, v => rect.anchoredPosition = v, target, seconds);

        private void ShowLine(int index)
        {
            LineIndex = index;
            DialogueLine line = script[index];
            if (line.ReplaceLeft) left = line.LeftCharacter;
            if (line.ReplaceRight) right = line.RightCharacter;
            ApplyPortrait(leftPortrait, leftPortraitFrame, left);
            ApplyPortrait(rightPortrait, rightPortraitFrame, right);
            string name = line.Speaker == DialogueSpeaker.Left ? left?.DisplayName
                : line.Speaker == DialogueSpeaker.Right ? right?.DisplayName : "旁白";
            speakerText.text = string.IsNullOrEmpty(line.SpeakerNameOverride) ? name ?? "旁白" : line.SpeakerNameOverride;
            dialogueText.text = line.Text;
            dialogueText.maxVisibleCharacters = int.MaxValue;
            dialogueText.pageToDisplay = 1;
            Canvas.ForceUpdateCanvases();
            dialogueText.ForceMeshUpdate();
            ShowPage(1);
            if (ready) AnimateFocus();
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
            revealed = pageStart;
            hold = 0f;
            dialogueText.maxVisibleCharacters = charactersPerSecond <= 0f ? pageEnd : pageStart;
            if (progressText != null)
                progressText.text = (LineIndex + 1) + " / " + script.Count
                    + (info.pageCount > 1 ? "    页 " + page + " / " + info.pageCount : string.Empty);
            UpdateHint();
        }

        private void AnimateFocus()
        {
            focus?.Kill(false);
            RestorePortraitPose();
            DialogueLine line = script[LineIndex];
            bool leftSpeaking = line.Speaker == DialogueSpeaker.Left;
            bool rightSpeaking = line.Speaker == DialogueSpeaker.Right;
            focus = Sequence();
            focus.Append(DOTween.To(() => leftPortrait.color, v => leftPortrait.color = v,
                leftSpeaking || line.Speaker == DialogueSpeaker.Narrator ? Color.white : inactiveTint, focusSeconds));
            focus.Join(DOTween.To(() => rightPortrait.color, v => rightPortrait.color = v,
                rightSpeaking || line.Speaker == DialogueSpeaker.Narrator ? Color.white : inactiveTint, focusSeconds));
            focus.Join(leftPortrait.transform.DOScale(leftScale * (leftSpeaking ? 1.025f : 1f), focusSeconds));
            focus.Join(rightPortrait.transform.DOScale(rightScale * (rightSpeaking ? 1.025f : 1f), focusSeconds));
            RectTransform portrait = leftSpeaking ? leftPortrait.rectTransform : rightSpeaking ? rightPortrait.rectTransform : null;
            if (portrait != null && line.Gesture != DialogueGesture.None)
            {
                Vector2 pose = portrait.anchoredPosition;
                Vector2 offset = line.Gesture == DialogueGesture.Nod ? new Vector2(0f, -14f) : new Vector2(12f, 0f);
                focus.Append(Move(portrait, pose + offset, .1f).SetLoops(4, LoopType.Yoyo));
            }
        }

        private void Update()
        {
            if (!IsPlaying || exiting) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Cancel(); return; }
            if (!ready) return;
            if (IsTyping)
            {
                revealed += Time.unscaledDeltaTime * charactersPerSecond;
                dialogueText.maxVisibleCharacters = Math.Min(pageEnd, (int)revealed);
                if (!IsTyping) UpdateHint();
            }
            else if (script[LineIndex].AutoAdvanceSeconds > 0f)
            {
                hold += Time.unscaledDeltaTime;
                if (hold >= script[LineIndex].AutoAdvanceSeconds) Advance();
            }
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame)) Advance();
        }

        /// <summary>第一次提交只补完当前页；下一次才翻页/换句。进退场期间不接受提交。</summary>
        public void Advance()
        {
            if (!IsPlaying || !ready || exiting || lastAdvanceFrame == Time.frameCount) return;
            lastAdvanceFrame = Time.frameCount;
            if (IsTyping)
            {
                revealed = pageEnd;
                dialogueText.maxVisibleCharacters = pageEnd;
                hold = 0f;
                UpdateHint();
                return;
            }
            if (page < dialogueText.textInfo.pageCount) { ShowPage(page + 1); return; }
            if (LineIndex + 1 < script.Count) { ShowLine(LineIndex + 1); return; }
            exiting = true;
            ready = false;
            continueButton.interactable = false;
            focus?.Kill(false);
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
            hintText.text = IsTyping || dialogueText.maxVisibleCharacters < pageEnd ? "空格 / 回车 / 点击 · 显示全文"
                : page < dialogueText.textInfo.pageCount ? "空格 / 回车 / 点击 · 下一页"
                : LineIndex + 1 < script.Count ? "空格 / 回车 / 点击 · 继续" : "空格 / 回车 / 点击 · 结束对话";
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
            script = null;
            left = right = null;
            IsPlaying = ready = exiting = false;
            LineIndex = -1;
            if (active == this) active = null;
            transition?.Kill(false);
            focus?.Kill(false);
            transition = focus = null;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = canvasGroup.interactable = false;
            }
            if (continueButton != null) continueButton.interactable = false;
            if (dialogueBox != null) dialogueBox.anchoredPosition = boxPosition;
            if (leftPortrait != null && rightPortrait != null) RestorePortraitPose();
        }

        private void RestorePortraitPose()
        {
            leftPortrait.rectTransform.anchoredPosition = leftPosition;
            rightPortrait.rectTransform.anchoredPosition = rightPosition;
            leftPortrait.transform.localScale = leftScale;
            rightPortrait.transform.localScale = rightScale;
        }

        protected override void OnClose() => Stop();
        protected override void OnWillHide() => Stop();
        protected override void OnBeforeDestroy()
        {
            Stop();
            if (continueButton != null) continueButton.onClick.RemoveListener(Advance);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
            base.OnBeforeDestroy();
        }
        private void OnDisable() => Stop();
    }
}
