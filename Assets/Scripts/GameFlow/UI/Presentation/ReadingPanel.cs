using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>Linear reading with no character stage or choices. Input matches the dialogue reveal/advance flow.</summary>
    public sealed class ReadingPanel : UIPanel, IReadingPresentation
    {
        [Header("Prefab bindings")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RawImage pageImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private Image speakerPortrait;
        [SerializeField] private TMP_Text readingText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private Button backButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button cancelButton;
        [SerializeField, Min(0f)] private float charactersPerSecond = 45f;
        [SerializeField, Min(0f)] private float enterSeconds = .22f;
        [SerializeField, Min(0f)] private float exitSeconds = .16f;

        private IDisposable pauseOwner;
        public event Action PlaybackStopped;
        private ReadingScript script;
        private Action finished;
        private Action cancelled;
        private Sequence transition;
        private bool ready;
        private bool exiting;
        private int textPage = 1;
        private int pageStart;
        private int pageEnd;
        private float revealed;
        private int lastInputFrame;

        public bool IsPlaying { get; private set; }
        public int PageIndex { get; private set; }
        public int LineIndex { get; private set; }
        public bool IsTyping => IsPlaying && revealed < pageEnd;

        protected override void OnInit(IUIData data = null)
        {
            Spotlight.Audio.UIButtonAudio.Install(gameObject);
            Spotlight.Audio.UIButtonAudio.Configure(continueButton, string.Empty);
            Spotlight.Audio.UIButtonAudio.Configure(backButton, string.Empty);
            Spotlight.Audio.UIButtonAudio.Configure(cancelButton, string.Empty);
            if (continueButton != null) continueButton.onClick.AddListener(Advance);
            if (backButton != null) backButton.onClick.AddListener(Back);
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

        public void Play(ReadingScript value, Action onFinished = null, Action onCancelled = null)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (canvasGroup == null || continueButton == null || readingText == null)
                throw new InvalidOperationException("ReadingPanel has incomplete prefab bindings.");
            Stop();
            pauseOwner = PresentationPauseRegistry.Acquire(nameof(ReadingPanel));
            try
            {
            script = value;
            finished = onFinished;
            cancelled = onCancelled;
            PageIndex = LineIndex = 0;
            IsPlaying = true;
            lastInputFrame = Time.frameCount;
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = canvasGroup.interactable = true;
            ClearSelection();
            RenderLine();
            transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            transition.Append(DOTween.To(() => canvasGroup.alpha, v => canvasGroup.alpha = v, 1f, enterSeconds));
            transition.OnComplete(() => { transition = null; ready = true; UpdateButtons(); });
            }
            catch { Stop(); throw; }
        }

        public void Advance()
        {
            if (!TakeInput()) return;
            if (IsTyping)
            {
                revealed = pageEnd;
                readingText.maxVisibleCharacters = pageEnd;
                UpdateHint();
            }
            else if (textPage < readingText.textInfo.pageCount) { Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.PageTurn); ShowTextPage(textPage + 1); }
            else if (LineIndex + 1 < script[PageIndex].Lines.Count) { LineIndex++; RenderLine(); }
            else if (PageIndex + 1 < script.Count) { PageIndex++; LineIndex = 0; RenderLine(); }
            else Finish();
        }

        public void Back()
        {
            if (!TakeInput()) return;
            if (textPage > 1) { Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.PageTurn); ShowTextPage(textPage - 1, true); return; }
            if (LineIndex > 0) LineIndex--;
            else if (PageIndex > 0) { PageIndex--; LineIndex = script[PageIndex].Lines.Count - 1; }
            else return;
            RenderLine(true);
        }

        private bool TakeInput()
        {
            if (!IsPlaying || !ready || exiting || DialoguePanel.IsExternalInputBlocked || lastInputFrame == Time.frameCount) return false;
            lastInputFrame = Time.frameCount;
            return true;
        }

        private void Update()
        {
            if (!IsPlaying || exiting || DialoguePanel.IsExternalInputBlocked) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Cancel(); return; }
            if (!ready) return;
            if (IsTyping)
            {
                revealed = Mathf.Min(pageEnd, revealed + Time.unscaledDeltaTime * charactersPerSecond);
                readingText.maxVisibleCharacters = (int)revealed;
                if (!IsTyping) UpdateHint();
            }
            if (keyboard == null) return;
            if (keyboard.leftArrowKey.wasPressedThisFrame) Back();
            else if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame) Advance();
        }

        private void RenderLine(bool showLastTextPage = false)
        {
            ReadingPage current = script[PageIndex];
            ReadingLine line = current.Lines[LineIndex];
            Spotlight.Audio.GameAudio.StopOwner(this);
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.PageTurn);
            Spotlight.Audio.GameAudio.PlayNarrative(line.voiceKey, this, voice: true);
            bool hasImage = current.Image != null;
            if (pageImage != null)
            {
                pageImage.texture = current.Image; pageImage.gameObject.SetActive(hasImage);
                var fitter = pageImage.GetComponent<AspectRatioFitter>();
                if (hasImage && fitter != null) fitter.aspectRatio = current.Image.width / (float)Mathf.Max(1, current.Image.height);
            }
            if (titleText != null) titleText.text = !string.IsNullOrEmpty(current.Title) ? current.Title : script.Title;
            bool dialogue = line.Kind == ReadingLineKind.Dialogue;
            if (speakerText != null) { speakerText.text = line.Speaker; speakerText.gameObject.SetActive(dialogue); }
            if (speakerPortrait != null)
            {
                speakerPortrait.sprite = line.Portrait;
                speakerPortrait.gameObject.SetActive(dialogue && line.Portrait != null);
            }
            // With no illustration, reclaim the right column so the reading copy remains legible.
            readingText.rectTransform.anchorMin = new Vector2(.055f, dialogue ? .22f : .16f);
            readingText.rectTransform.anchorMax = new Vector2(hasImage ? .50f : .94f, dialogue ? .68f : .82f);
            readingText.rectTransform.offsetMin = readingText.rectTransform.offsetMax = Vector2.zero;
            readingText.text = line.Text;
            readingText.overflowMode = TextOverflowModes.Page;
            readingText.maxVisibleCharacters = int.MaxValue;
            readingText.pageToDisplay = 1;
            Canvas.ForceUpdateCanvases();
            readingText.ForceMeshUpdate();
            ShowTextPage(showLastTextPage ? Mathf.Max(1, readingText.textInfo.pageCount) : 1, showLastTextPage);
        }

        private void ShowTextPage(int value, bool revealImmediately = false)
        {
            textPage = value;
            readingText.pageToDisplay = value;
            TMP_TextInfo info = readingText.textInfo;
            if (info.characterCount == 0 || info.pageCount == 0) pageStart = pageEnd = 0;
            else
            {
                TMP_PageInfo range = info.pageInfo[value - 1];
                pageStart = range.firstCharacterIndex;
                pageEnd = Math.Min(info.characterCount, range.lastCharacterIndex + 1);
            }
            revealed = revealImmediately || charactersPerSecond <= 0f ? pageEnd : pageStart;
            readingText.maxVisibleCharacters = (int)revealed;
            if (progressText != null) progressText.text = "页 " + (PageIndex + 1) + " / " + script.Count
                + " · 行 " + (LineIndex + 1) + " / " + script[PageIndex].Lines.Count
                + (info.pageCount > 1 ? " · 文本 " + textPage + " / " + info.pageCount : string.Empty);
            UpdateButtons();
            UpdateHint();
        }

        private void UpdateHint()
        {
            if (hintText != null) hintText.text = IsTyping ? "空格 / 回车：补全文字" : "空格 / 回车：继续 · ←：返回";
        }

        private void UpdateButtons()
        {
            if (backButton != null) backButton.interactable = ready && !exiting && (PageIndex > 0 || LineIndex > 0 || textPage > 1);
            if (continueButton != null) continueButton.interactable = ready && !exiting;
        }

        private void Finish()
        {
            exiting = true;
            UpdateButtons();
            transition?.Kill(false);
            transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            transition.Append(DOTween.To(() => canvasGroup.alpha, v => canvasGroup.alpha = v, 0f, exitSeconds));
            transition.OnComplete(() =>
            {
                Action callback = finished;
                Stop();
                callback?.Invoke();
            });
        }

        public void Cancel()
        {
            if (!IsPlaying || DialoguePanel.IsExternalInputBlocked) return;
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Cancel);
            Action callback = cancelled;
            Stop();
            CloseSelf();
            callback?.Invoke();
        }

        /// <summary>Silent interruption; cancellation notification belongs to Cancel, not hide/destroy.</summary>
        public void Stop()
        {
            Spotlight.Audio.GameAudio.StopOwner(this);
            pauseOwner?.Dispose();
            pauseOwner = null;
            Action stopped = PlaybackStopped;
            PlaybackStopped = null;
            finished = cancelled = null;
            IsPlaying = ready = exiting = false;
            transition?.Kill(false);
            transition = null;
            script = null;
            if (canvasGroup != null) { canvasGroup.alpha = 0f; canvasGroup.blocksRaycasts = canvasGroup.interactable = false; }
            stopped?.Invoke();
        }

        private static void ClearSelection()
        {
            var system = UnityEngine.EventSystems.EventSystem.current;
            if (system != null) system.SetSelectedGameObject(null);
        }
        protected override void OnClose() => Stop();
        protected override void OnWillHide() => Stop();
        private void OnDisable() => Stop();
        protected override void OnBeforeDestroy()
        {
            Stop();
            if (continueButton != null) continueButton.onClick.RemoveListener(Advance);
            if (backButton != null) backButton.onClick.RemoveListener(Back);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
            base.OnBeforeDestroy();
        }
    }
}
