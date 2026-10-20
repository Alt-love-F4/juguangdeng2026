using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>开场互动信件与快照驱动的通用读信；两种模式不共享结算流程。</summary>
    public sealed partial class FirstDayLetterPanel : UIPanel, IDayFlowPresentation
    {
        public enum LetterStage { Stopped, Envelope, Opening, Reading, AwaitingSignature, Closing, Finished }

        [Header("Prefab bindings")]
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private CanvasGroup envelopeGroup;
        [SerializeField] private RectTransform envelope;
        [SerializeField] private Image envelopeArt;
        [SerializeField] private Sprite envelopeClosedSprite;
        [SerializeField] private Sprite envelopeOpenSprite;
        [SerializeField] private Button envelopeButton;
        [SerializeField] private CanvasGroup hintGroup;
        [SerializeField] private CanvasGroup paperGroup;
        [SerializeField] private RectTransform paper;
        [SerializeField] private TMP_Text letterBody;
        [SerializeField] private TMP_Text signatureText;
        [SerializeField] private Button signatureButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private TMP_Text readingHint;
        [SerializeField] private ScrollRect letterScroll;
        [SerializeField] private CanvasGroup nameEditor;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button nameConfirmButton;
        [SerializeField] private Button nameCancelButton;
        private FirstDayLetterDraft draft;
        private string hoveredLink;
        private bool InteractiveLetter => draft != null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const bool CanSkipOpeningLetter = true;
#else
        private const bool CanSkipOpeningLetter = false;
#endif
        public FirstDayLetterDraft Draft => draft;
        private FirstDayLetterDraft pendingDraft;
        public Func<FirstDayLetterDraft, bool> SubmitDraft { get; set; }
        public Func<bool> SubmitDefault { get; set; }
        public Action<FirstDayLetterDraft> DraftChanged { get; set; }
        public void SetDraft(FirstDayLetterDraft value) => pendingDraft = value;

        [Header("Legacy draft (explicit injected previews only)")]
        [SerializeField, HideInInspector, TextArea(8, 30)] private string letter =
            "亲爱的孩子：\n\n" +
            "当你读到这封信时，我大概已经不能再亲手给你煮一杯咖啡了。请不要为此责怪自己。" +
            "你走过的路、做过的决定，都值得被好好珍惜。\n\n" +
            "我把圣诺特的沸点咖啡馆留给你。钥匙和交接的手续已经安排妥当，那里会是你新的起点。" +
            "它不算大，却收留过许多疲惫的人，也藏着我们还没来得及一起度过的日子。\n\n" +
            "我一直盼着与你重逢，听你说说这些年去了哪里，又变成了什么样的人。" +
            "没能等到这一天，是我最放不下的遗憾。\n\n" +
            "愿你在忙碌时也记得吃饭，在难过时也有人陪伴。经营一家咖啡馆之前，先照顾好自己。" +
            "不必成为我，只要成为你自己。\n\n" +
            "如果有一天，你听见门铃响起，就替我对推门而来的人说一声：欢迎。\n\n" +
            "而这一次，是我想对你说——欢迎回家。";
        [SerializeField] private string signature = "—— 永远惦念你的叔叔";
        private FirstDayLetterContent pendingContent;

        [Header("Playback (unscaled time)")]
        [SerializeField, Min(.005f)] private float characterInterval = .026f;
        [SerializeField, Min(0f)] private float openSeconds = .65f;
        [SerializeField, Min(0f)] private float closeSeconds = .4f;
        [SerializeField, Min(0f)] private float paperRise = 1100f;

        private Sequence transition;
        private Tween hintPulse;
        private Tween signaturePulse;
        private Action finished;
        private bool capturedPose;
        private bool coffeeCupExitStarted;
        private Vector3 signatureButtonScale;
        private Vector2 paperPosition;
        private Vector3 paperScale;
        private Vector3 envelopeScale;
        private float typeTimer;
        private int characterCount;

        public bool IsPlaying { get; private set; }
        public LetterStage Stage { get; private set; }
        public bool UseCoffeeCupExit { get; set; }
        public int VisibleCharacterCount => letterBody == null ? 0 : letterBody.maxVisibleCharacters;

        /// <summary>滚动文案只在正文确实超出可视区时出现；重建布局后再判断，避免旧尺寸误判。</summary>
        private bool HasLetterOverflow()
        {
            if (letterBody == null || letterScroll == null || letterScroll.viewport == null) return false;
            float content = Mathf.Max(letterBody.preferredHeight, letterScroll.content != null ? letterScroll.content.rect.height : 0f);
            return content > letterScroll.viewport.rect.height + 1f;
        }

        private bool LetterAtBottom()
        {
            if (letterScroll == null) return true;
            return letterScroll.verticalNormalizedPosition <= .01f;
        }


        protected override void OnInit(IUIData data = null)
        {
            Spotlight.Audio.UIButtonAudio.Install(gameObject);
            Spotlight.Audio.UIButtonAudio.Configure(envelopeButton, string.Empty);
            Spotlight.Audio.UIButtonAudio.Configure(signatureButton, string.Empty);
            Spotlight.Audio.UIButtonAudio.Configure(nameCancelButton, Spotlight.Audio.AudioKeys.Cancel);
            CapturePose();
            signatureButtonScale = signatureButton.transform.localScale;
            envelopeButton.onClick.AddListener(OpenEnvelope);
            signatureButton.onClick.AddListener(ConfirmSignature);
            skipButton.onClick.AddListener(SkipLetter);
            if (nameConfirmButton != null) nameConfirmButton.onClick.AddListener(ConfirmPlayerName);
            if (nameCancelButton != null) nameCancelButton.onClick.AddListener(CancelPlayerName);
            signatureText.richText = true;
        }

        protected override void OnOpen(IUIData data = null)
        {
            Stop();
            UseCoffeeCupExit = false;
            UIKit.SetPanelModal(this, true);
        }

        protected override void OnShow()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
        }

        /// <summary>在 Play 前注入已由调用方解析好的 NCS 内容。</summary>
        public void SetContent(FirstDayLetterContent content)
        {
            if (IsPlaying) throw new InvalidOperationException("Set letter content before playback.");
            pendingContent = content;
        }

        /// <summary>默认使用硬编码互动信件；显式注入仍可用于独立演出测试。</summary>
        public void Play(Action onFinished)
        {
            FirstDayLetterContent content = pendingContent;
            pendingContent = null;
            Play(content, onFinished);
        }

        /// <summary>使用指定内容播放信件，不让面板直接依赖 NCS 全局状态。</summary>
        public void Play(FirstDayLetterContent content, Action onFinished)
        {
            ValidateBindings();
            Stop();
            CapturePose();
            RestorePose();
            UIKit.SetPanelLevel(this, UILevel.Pop);
            UIKit.SetPanelModal(this, true);
            draft = content == null ? pendingDraft ?? new FirstDayLetterDraft() : null;
            pendingDraft = null;
            hoveredLink = null;
            coffeeCupExitStarted = false;
            CancelPlayerName();
            FirstDayLetterContent resolved = content ?? new FirstDayLetterContent(draft.BuildBody(), FirstDayLetterDraft.Signature);
            letterBody.richText = InteractiveLetter;
            signatureText.richText = true;
            finished = onFinished;
            IsPlaying = true;
            Stage = LetterStage.Envelope;
            if (envelopeArt != default && envelopeClosedSprite != default) envelopeArt.sprite = envelopeClosedSprite;
            rootGroup.alpha = 1f;
            rootGroup.interactable = rootGroup.blocksRaycasts = true;
            envelopeGroup.gameObject.SetActive(true);
            envelopeGroup.alpha = 1f;
            envelopeButton.interactable = true;
            paperGroup.gameObject.SetActive(false);
            paperGroup.alpha = 0f;
            signatureButton.gameObject.SetActive(false);
            signatureButton.interactable = false;
            skipButton.gameObject.SetActive(CanSkipOpeningLetter);
            skipButton.interactable = CanSkipOpeningLetter;
            signatureText.text = EmphasizeSignature(InteractiveLetter
                ? "—— 永远惦念你的<size=116.667%>叔叔</size>"
                : resolved.Signature);
            readingHint.text = "";
            letterBody.text = resolved.Body;
            letterBody.maxVisibleCharacters = 0;
            letterScroll.onValueChanged.RemoveListener(InteractiveScrolled);
            letterScroll.onValueChanged.AddListener(InteractiveScrolled);
            hintGroup.alpha = 1f;
            hintPulse = DOTween.To(() => hintGroup.alpha, value => hintGroup.alpha = value, .45f, .9f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        /// <summary>Button entry point. Repeated clicks cannot restart the opening.</summary>
        public void OpenEnvelope()
        {
            if (genericLetterMode) { OpenGenericEnvelope(); return; }
            if (!IsPlaying || Stage != LetterStage.Envelope) return;
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Envelope, this);
            Stage = LetterStage.Opening;
            envelopeButton.interactable = false;
            if (envelopeArt != null && envelopeOpenSprite != null) envelopeArt.sprite = envelopeOpenSprite;
            StopHint();
            paperGroup.gameObject.SetActive(true);
            paper.anchoredPosition = paperPosition + Vector2.down * paperRise;
            paper.localScale = paperScale * .96f;
            letterBody.maxVisibleCharacters = int.MaxValue;
            letterBody.ForceMeshUpdate();
            characterCount = letterBody.textInfo.characterCount;
            letterBody.maxVisibleCharacters = 0;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(letterScroll.content);
            letterScroll.verticalNormalizedPosition = 1f;
            transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            transition.Append(DOTween.To(() => paper.anchoredPosition,
                value => paper.anchoredPosition = value, paperPosition, openSeconds).SetEase(Ease.OutCubic));
            transition.Join(paper.DOScale(paperScale, openSeconds).SetEase(Ease.OutCubic));
            transition.Join(DOTween.To(() => paperGroup.alpha, value => paperGroup.alpha = value, 1f, openSeconds));
            transition.Join(DOTween.To(() => envelopeGroup.alpha, value => envelopeGroup.alpha = value, 0f, openSeconds * .6f));
            transition.Join(envelope.DOScale(envelopeScale * .92f, openSeconds));
            transition.OnComplete(() =>
            {
                transition = null;
                if (!IsPlaying || Stage != LetterStage.Opening) return;
                envelopeGroup.gameObject.SetActive(false);
                Stage = LetterStage.Reading;
                typeTimer = 0f;
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(letterScroll.content);
                readingHint.text = HasLetterOverflow()
                    ? "信中的文字正在慢慢浮现…  ·  滚轮 / 拖动阅读"
                    : "信中的文字正在慢慢浮现…";
            });
        }

        private void Update()
        {
            if (genericLetterMode) { UpdateGenericLetter(); return; }
            if (!IsPlaying) return;
            if (Stage == LetterStage.AwaitingSignature)
            {
                if (signatureButton.interactable)
                {
                    StartSignatureEmphasis();
                    if (string.IsNullOrEmpty(hoveredLink))
                    {
                        if (!HasLetterOverflow() || LetterAtBottom())
                            readingHint.text = "\u003e \u8bfb\u5b8c\u540e\uff0c\u70b9\u51fb\u4e0a\u65b9\u300c\u53d4\u53d4\u300d\u7f72\u540d\u7ee7\u7eed";
                    }
                }
                else StopSignatureEmphasis();
                return;
            }
            if (Stage != LetterStage.Reading) return;
            typeTimer += Time.unscaledDeltaTime;
            int visible = Mathf.Min(characterCount,
                Mathf.FloorToInt(typeTimer / Mathf.Max(.005f, characterInterval)));
            letterBody.maxVisibleCharacters = visible;
            if (visible < characterCount) return;
            Stage = LetterStage.AwaitingSignature;
            signatureButton.gameObject.SetActive(true);
            signatureButton.interactable = true;
            UpdateReadingHint();
        }

        /// <summary>Completion requires a fresh click after all text has appeared.</summary>
        public void ConfirmSignature()
        {
            if (genericLetterMode) return; // Generic letters never submit character creation or sign.
            if (!IsPlaying || Stage != LetterStage.AwaitingSignature) return;
            if (InteractiveLetter)
            {
                if (!draft.IsComplete) { UpdateReadingHint(); return; }
                if (SubmitDraft != null && !SubmitDraft(draft)) return;
            }
            CancelPlayerName();
            Stage = LetterStage.Closing;
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Signature, this);
            signatureButton.interactable = false;
            readingHint.text = "";
            transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            if (UseCoffeeCupExit) transition.AppendCallback(BeginCoffeeCupExit);
            transition.Append(DOTween.To(() => rootGroup.alpha, value => rootGroup.alpha = value, 0f, closeSeconds));
            transition.Join(paper.DOScale(paperScale * .97f, closeSeconds));
            if (UseCoffeeCupExit) transition.AppendInterval(Mathf.Max(0f, .45f - closeSeconds));
            transition.OnComplete(() =>
            {
                transition = null;
                if (!IsPlaying || Stage != LetterStage.Closing) return;
                FinishLetter();
            });
        }

        /// <summary>跳过整段信件演出，立即调用完成回调并进入第一天流程。</summary>
        public void SkipLetter()
        {
            if (genericLetterMode || !CanSkipOpeningLetter) return; // Only editor/development opening letters may be skipped.
            if (!IsPlaying) return;
            if (InteractiveLetter && SubmitDefault != null && !SubmitDefault()) return;
            transition?.Kill(false);
            transition = null;
            CancelPlayerName();
            if (UseCoffeeCupExit)
            {
                Stage = LetterStage.Closing;
                rootGroup.interactable = rootGroup.blocksRaycasts = false;
                envelopeButton.interactable = false;
                signatureButton.interactable = false;
                skipButton.interactable = false;
                BeginCoffeeCupExit();
                transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
                transition.AppendInterval(.45f);
                transition.OnComplete(() =>
                {
                    transition = null;
                    if (IsPlaying && Stage == LetterStage.Closing) FinishLetter();
                });
                return;
            }
            FinishLetter();
        }

        public void ActivateLetterLink(string link)
        {
            if (!IsPlaying || !InteractiveLetter || (Stage != LetterStage.Reading && Stage != LetterStage.AwaitingSignature) || string.IsNullOrEmpty(link)) return;
            if (link == "name")
            {
                if (nameEditor == null || nameInput == null) return;
                nameEditor.gameObject.SetActive(true);
                nameInput.text = draft.PlayerName;
                nameInput.ActivateInputField();
                return;
            }
            string[] parts = link.Split(':');
            if (parts.Length == 3 && parts[0] == "choice" && draft.Select(parts[1], parts[2]))
                RefreshInteractiveBody();
        }

        public bool SelectLetterOption(int group, int option)
        {
            if (!IsPlaying || !InteractiveLetter || (Stage != LetterStage.Reading && Stage != LetterStage.AwaitingSignature) || !draft.Select(group, option)) return false;
            RefreshInteractiveBody();
            return true;
        }

        public void SetPlayerName(string value)
        {
            if (!IsPlaying || !InteractiveLetter) return;
            draft.SetName(value);
            RefreshInteractiveBody();
        }

        public void ConfirmPlayerName()
        {
            if (nameInput == null) return;
            SetPlayerName(nameInput.text);
            if (draft != null && draft.PlayerName.Length > 0) CancelPlayerName();
        }

        public void CancelPlayerName()
        {
            if (nameEditor != null) nameEditor.gameObject.SetActive(false);
        }

        public void HoverLetterLink(string link)
        {
            if (!IsPlaying || !InteractiveLetter || hoveredLink == link || (Stage != LetterStage.Reading && Stage != LetterStage.AwaitingSignature)) return;
            hoveredLink = link;
            RefreshInteractiveBody();
        }

        private void RefreshInteractiveBody()
        {
            DraftChanged?.Invoke(draft);
            float scroll = letterScroll.verticalNormalizedPosition;
            // 修改选项后保留已读比例；不会重启打字或跳到信件顶部。
            float progress = characterCount > 0 ? Mathf.Clamp01((float)letterBody.maxVisibleCharacters / characterCount) : 0f;
            letterBody.text = draft.BuildBody(hoveredLink);
            letterBody.ForceMeshUpdate();
            characterCount = letterBody.textInfo.characterCount;
            if (Stage == LetterStage.AwaitingSignature) letterBody.maxVisibleCharacters = int.MaxValue;
            else
            {
                letterBody.maxVisibleCharacters = Mathf.FloorToInt(progress * characterCount);
                typeTimer = letterBody.maxVisibleCharacters * characterInterval;
            }
            Canvas.ForceUpdateCanvases();
            letterScroll.verticalNormalizedPosition = scroll;
            UpdateReadingHint();
        }

        private void InteractiveScrolled(Vector2 _) => UpdateReadingHint();

        private void UpdateReadingHint()
        {
            string effect = InteractiveLetter ? draft.HoverEffect(hoveredLink) : "";
            bool scrollable = HasLetterOverflow();
            if (Stage == LetterStage.Reading)
            {
                if (effect.Length > 0) { readingHint.text = effect; return; }
                readingHint.text = scrollable ? "信中的文字正在慢慢浮现…  ·  滚轮 / 拖动阅读" : "信中的文字正在慢慢浮现…";
                return;
            }
            if (Stage != LetterStage.AwaitingSignature) return;
            bool complete = !InteractiveLetter || draft.IsComplete;
            signatureButton.interactable = complete;
            if (effect.Length > 0) { readingHint.text = effect; return; }
            if (!complete) { readingHint.text = "> 请填写姓名并完成五组选项，再点击署名继续"; return; }
            // 正文放不下时先把玩家引到信尾，避免只看半封信就点了署名。
            readingHint.text = scrollable && !LetterAtBottom()
                ? "滚轮 / 拖动往下读 · 读到底后点击署名"
                : "> 读完后，点击署名继续";
        }

        private static string EmphasizeSignature(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            const string uncle = "\u53d4\u53d4";
            const string start = "<color=#9B3B32><b><u>";
            const string end = "</u></b></color>";
            return value.Contains(uncle) ? value.Replace(uncle, start + uncle + end) : start + value + end;
        }

        private void StartSignatureEmphasis()
        {
            if (signaturePulse != null && signaturePulse.IsActive()) return;
            signatureButton.transform.localScale = signatureButtonScale;
            signaturePulse = signatureButton.transform.DOScale(signatureButtonScale * 1.045f, .85f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void StopSignatureEmphasis()
        {
            signaturePulse?.Kill(false);
            signaturePulse = null;
            if (signatureButton != null) signatureButton.transform.localScale = signatureButtonScale;
        }

        private void BeginCoffeeCupExit()
        {
            if (!UseCoffeeCupExit || coffeeCupExitStarted) return;
            coffeeCupExitStarted = true;
            CoffeeCupScreenTransition.BeginClose(.45f);
        }

        // Commit completion before invoking external code: callbacks may close or reuse the panel.
        private void FinishLetter()
        {
            if (!IsPlaying) return;
            StopHint();
            StopSignatureEmphasis();
            typeTimer = 0f;
            letterScroll.StopMovement();
            readingHint.text = "";
            Stage = LetterStage.Finished;
            IsPlaying = false;
            rootGroup.alpha = 0f;
            rootGroup.interactable = rootGroup.blocksRaycasts = false;
            envelopeButton.interactable = false;
            signatureButton.interactable = false;
            skipButton.interactable = false;
            UseCoffeeCupExit = false;
            coffeeCupExitStarted = false;
            Action callback = finished;
            finished = null;
            callback?.Invoke();
        }

        public void Stop()
        {
            Action letterCancelled = StopGenericLetter();
            Spotlight.Audio.GameAudio.StopOwner(this);
            finished = null;
            pendingContent = null;
            if (UseCoffeeCupExit && coffeeCupExitStarted) CoffeeCupScreenTransition.Clear();
            coffeeCupExitStarted = false;
            IsPlaying = false;
            Stage = LetterStage.Stopped;
            transition?.Kill(false);
            transition = null;
            StopHint();
            StopSignatureEmphasis();
            CancelPlayerName();
            typeTimer = 0f;
            if (letterScroll != null)
            {
                letterScroll.StopMovement();
                letterScroll.onValueChanged.RemoveListener(InteractiveScrolled);
            }
            if (rootGroup != null) rootGroup.interactable = rootGroup.blocksRaycasts = false;
            if (envelopeButton != null) envelopeButton.interactable = false;
            if (signatureButton != null) signatureButton.interactable = false;
            if (skipButton != null) skipButton.interactable = false;
            letterCancelled?.Invoke();
        }

        private void StopHint()
        {
            hintPulse?.Kill(false);
            hintPulse = null;
        }

        private void CapturePose()
        {
            ValidateBindings();
            if (capturedPose) return;
            capturedPose = true;
            paperPosition = paper.anchoredPosition;
            paperScale = paper.localScale;
            envelopeScale = envelope.localScale;
        }

        private void RestorePose()
        {
            paper.anchoredPosition = paperPosition;
            paper.localScale = paperScale;
            envelope.localScale = envelopeScale;
        }

        private void ValidateBindings()
        {
            if (rootGroup == null || envelopeGroup == null || envelope == null || envelopeButton == null
                || hintGroup == null || paperGroup == null || paper == null || letterBody == null
                || signatureText == null || signatureButton == null || skipButton == null
                || readingHint == null || letterScroll == null)
                throw new InvalidOperationException("FirstDayLetterPanel has incomplete prefab bindings.");
        }

        protected override void OnClose() => Stop();
        protected override void OnWillHide() => Stop();
        private void OnDisable() => Stop();
        protected override void OnBeforeDestroy()
        {
            Stop();
            if (envelopeButton != null) envelopeButton.onClick.RemoveListener(OpenEnvelope);
            if (signatureButton != null) signatureButton.onClick.RemoveListener(ConfirmSignature);
            if (skipButton != null) skipButton.onClick.RemoveListener(SkipLetter);
            if (nameConfirmButton != null) nameConfirmButton.onClick.RemoveListener(ConfirmPlayerName);
            if (nameCancelButton != null) nameCancelButton.onClick.RemoveListener(CancelPlayerName);
            base.OnBeforeDestroy();
        }
    }
}
