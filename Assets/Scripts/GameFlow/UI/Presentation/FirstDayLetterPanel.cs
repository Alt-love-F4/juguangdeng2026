using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>仅新游戏开场使用：点击拆信、信纸展开、逐字阅读、署名确认。</summary>
    public sealed class FirstDayLetterPanel : UIPanel, IDayFlowPresentation
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
        private string waitingLink;
        private string nextInputLink;
        private int nextInputEnd;
        public int CurrentChoiceGroup { get; private set; } = -1;
        public bool IsWaitingForInput => waitingLink != null;
        private bool EditorOpen => CurrentChoiceGroup >= 0 || (nameEditor != null && nameEditor.gameObject.activeSelf);
        private int expandedCharacterStart;
        private int expandedCharacterLength;
        private FirstDayLetterDraft draft;
        private string hoveredLink;
        private bool InteractiveLetter => draft != null;
        public FirstDayLetterDraft Draft => draft;

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
        private Action finished;
        private bool capturedPose;
        private Vector2 paperPosition;
        private Vector3 paperScale;
        private Vector3 envelopeScale;
        private float typeTimer;
        private int characterCount;

        public bool IsPlaying { get; private set; }
        public LetterStage Stage { get; private set; }
        public int VisibleCharacterCount => letterBody == null ? 0 : letterBody.maxVisibleCharacters;
        private bool NeedsScroll => letterBody.preferredHeight > letterScroll.viewport.rect.height + .5f;

        protected override void OnInit(IUIData data = null)
        {
            CapturePose();
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
            draft = content == null ? new FirstDayLetterDraft() : null;
            hoveredLink = null;
            expandedCharacterStart = expandedCharacterLength = 0;
            CancelPlayerName();
            FirstDayLetterContent resolved = content ?? new FirstDayLetterContent(draft.BuildBody(), FirstDayLetterDraft.Signature);
            letterBody.richText = InteractiveLetter;
            signatureText.richText = InteractiveLetter;
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
            skipButton.gameObject.SetActive(true);
            skipButton.interactable = true;
            signatureText.text = InteractiveLetter ? "—— 永远惦念你的<size=116.667%>叔叔</size>" : resolved.Signature;
            readingHint.text = "";
            letterBody.text = resolved.Body;
            letterBody.maxVisibleCharacters = 0;
            hintGroup.alpha = 1f;
            hintPulse = DOTween.To(() => hintGroup.alpha, value => hintGroup.alpha = value, .45f, .9f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        /// <summary>Button entry point. Repeated clicks cannot restart the opening.</summary>
        public void OpenEnvelope()
        {
            if (!IsPlaying || Stage != LetterStage.Envelope) return;
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
            CacheNextInput();
            letterBody.maxVisibleCharacters = 0;
            Canvas.ForceUpdateCanvases();
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
                UpdateReadingHint();
            });
        }

        private void Update()
        {
            if (!IsPlaying || Stage != LetterStage.Reading || IsWaitingForInput || EditorOpen) return;
            float interval = Mathf.Max(.005f, characterInterval);
            typeTimer += Time.unscaledDeltaTime;
            int advance = Mathf.FloorToInt(typeTimer / interval);
            if (advance == 0) return;
            typeTimer -= advance * interval;
            int visible = Mathf.Min(characterCount, letterBody.maxVisibleCharacters + advance);
            if (InteractiveLetter && nextInputLink != null && visible >= nextInputEnd)
            {
                letterBody.maxVisibleCharacters = nextInputEnd;
                waitingLink = nextInputLink;
                typeTimer = 0f;
                ScrollToCharacter(nextInputEnd - 1);
                ActivateLetterLink(waitingLink);
                UpdateReadingHint();
                return;
            }
            letterBody.maxVisibleCharacters = visible;
            if (visible < characterCount) return;
            Stage = LetterStage.AwaitingSignature;
            signatureButton.gameObject.SetActive(true);
            UpdateReadingHint();
        }

        private void CacheNextInput()
        {
            nextInputLink = null;
            if (!InteractiveLetter) return;
            for (int i = 0; i < letterBody.textInfo.linkCount; i++)
            {
                TMP_LinkInfo link = letterBody.textInfo.linkInfo[i];
                string id = link.GetLinkID();
                bool unfilled = id == "name" ? draft.PlayerName.Length == 0
                    : id.StartsWith("slot:") && int.TryParse(id.Substring(5), out int group) && draft.Selection(group) < 0;
                if (!unfilled) continue;
                nextInputLink = id;
                nextInputEnd = link.linkTextfirstCharacterIndex + link.linkTextLength;
                break;
            }
        }

        private void ScrollToCharacter(int index)
        {
            Canvas.ForceUpdateCanvases();
            float overflow = letterBody.rectTransform.rect.height - letterScroll.viewport.rect.height;
            if (overflow <= 0 || index < 0) return;
            float bottom = -letterBody.textInfo.characterInfo[index].descender;
            float offset = Mathf.Max(0, bottom - letterScroll.viewport.rect.height + letterBody.fontSize * 2);
            letterScroll.StopMovement();
            letterScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(offset / overflow);
        }

        /// <summary>Completion requires a fresh click after all text has appeared.</summary>
        public void ConfirmSignature()
        {
            if (!IsPlaying || Stage != LetterStage.AwaitingSignature || EditorOpen) return;
            if (InteractiveLetter)
            {
                if (!draft.IsComplete) { UpdateReadingHint(); return; }
                PlayerDetailsProfile.Current.ApplyLetter(draft);
            }
            CancelPlayerName();
            Stage = LetterStage.Closing;
            signatureButton.interactable = false;
            readingHint.text = "";
            transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            transition.Append(DOTween.To(() => rootGroup.alpha, value => rootGroup.alpha = value, 0f, closeSeconds));
            transition.Join(paper.DOScale(paperScale * .97f, closeSeconds));
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
            if (!IsPlaying) return;
            transition?.Kill(false);
            transition = null;
            CancelPlayerName();
            // 跳过时保留已选项，其余使用一致的默认建角值。
            if (InteractiveLetter)
            {
                if (string.IsNullOrWhiteSpace(draft.PlayerName)) draft.SetName("咖啡馆馆长");
                for (int i = 0; i < FirstDayLetterDraft.Labels.Length; i++)
                    if (draft.Selection(i) < 0) draft.Select(i, i == 0 ? 1 : 0);
                PlayerDetailsProfile.Current.ApplyLetter(draft);
            }
            FinishLetter();
        }

        public void ActivateLetterLink(string link)
        {
            if (!CanEditLetter || string.IsNullOrEmpty(link) || !IsLinkVisible(link)) return;
            if (link == "name")
            {
                if (nameEditor == null || nameInput == null) return;
                CancelLetterChoice();
                nameEditor.gameObject.SetActive(true);
                nameInput.text = draft.PlayerName;
                nameInput.ActivateInputField();
                UpdateReadingHint();
                return;
            }
            string[] parts = link.Split(':');
            if (parts.Length == 3 && parts[0] == "choice" && int.TryParse(parts[1], out int choiceGroup)
                && int.TryParse(parts[2], out int option) && choiceGroup == CurrentChoiceGroup)
            {
                SelectLetterOption(choiceGroup, option);
                return;
            }
            if (!link.StartsWith("slot:") || !int.TryParse(link.Substring(5), out int group)
                || group < 0 || group >= FirstDayLetterDraft.Labels.Length) return;
            if (CurrentChoiceGroup == group)
            {
                CancelLetterChoice();
                return;
            }
            CancelPlayerName();
            CurrentChoiceGroup = group;
            RefreshInteractiveBody();
            ScrollToCharacter(expandedCharacterStart + expandedCharacterLength - 1);
            UpdateReadingHint();
        }

        private bool CanEditLetter => IsPlaying && InteractiveLetter
            && (Stage == LetterStage.Reading || Stage == LetterStage.AwaitingSignature);

        private bool IsLinkVisible(string id)
        {
            for (int i = 0; i < letterBody.textInfo.linkCount; i++)
            {
                TMP_LinkInfo link = letterBody.textInfo.linkInfo[i];
                if (link.GetLinkID() == id)
                    return link.linkTextfirstCharacterIndex + link.linkTextLength <= letterBody.maxVisibleCharacters;
            }
            return false;
        }

        public bool SelectLetterOption(int group, int option)
        {
            string id = $"slot:{group}";
            if (!CanEditLetter || !IsLinkVisible(id) || !draft.Select(group, option)) return false;
            CurrentChoiceGroup = -1;
            hoveredLink = null;
            RefreshInteractiveBody(id);
            if (waitingLink == id) waitingLink = null;
            UpdateReadingHint();
            return true;
        }

        public void CancelLetterChoice()
        {
            bool wasExpanded = CurrentChoiceGroup >= 0;
            CurrentChoiceGroup = -1;
            if (wasExpanded && CanEditLetter) RefreshInteractiveBody();
            UpdateReadingHint();
        }

        public void SetPlayerName(string value)
        {
            if (!CanEditLetter || !IsLinkVisible("name")) return;
            string previousName = draft.PlayerName;
            draft.SetName(value);
            if (draft.PlayerName.Length == 0)
            {
                draft.SetName(previousName);
                return;
            }
            RefreshInteractiveBody("name");
            if (draft.PlayerName.Length > 0)
            {
                if (waitingLink == "name") waitingLink = null;
                CancelPlayerName();
            }
            UpdateReadingHint();
        }

        public void ConfirmPlayerName()
        {
            if (nameInput != null) SetPlayerName(nameInput.text);
        }

        public void CancelPlayerName()
        {
            if (nameEditor != null) nameEditor.gameObject.SetActive(false);
            UpdateReadingHint();
        }

        public void HoverLetterLink(string link)
        {
            if (!CanEditLetter || hoveredLink == link) return;
            hoveredLink = link;
            RefreshInteractiveBody();
        }

        private void RefreshInteractiveBody(string changedLink = null)
        {
            float scroll = letterScroll.verticalNormalizedPosition;
            int visible = letterBody.maxVisibleCharacters;
            // 先还原到不含临时选项的正文位置，再映射到新展开状态。
            if (expandedCharacterLength > 0 && visible > expandedCharacterStart)
                visible -= Mathf.Min(expandedCharacterLength, visible - expandedCharacterStart);
            int oldStart = 0, oldLength = 0;
            for (int i = 0; changedLink != null && i < letterBody.textInfo.linkCount; i++)
            {
                TMP_LinkInfo link = letterBody.textInfo.linkInfo[i];
                if (link.GetLinkID() != changedLink) continue;
                oldStart = link.linkTextfirstCharacterIndex;
                oldLength = link.linkTextLength;
                break;
            }
            letterBody.text = draft.BuildBody(hoveredLink, CurrentChoiceGroup);
            letterBody.ForceMeshUpdate();
            characterCount = letterBody.textInfo.characterCount;
            CacheNextInput();
            CacheExpandedRange();
            for (int i = 0; changedLink != null && i < letterBody.textInfo.linkCount; i++)
            {
                TMP_LinkInfo link = letterBody.textInfo.linkInfo[i];
                if (link.GetLinkID() == changedLink && visible >= oldStart + oldLength)
                {
                    visible += link.linkTextLength - oldLength;
                    break;
                }
            }
            if (expandedCharacterLength > 0 && visible >= expandedCharacterStart) visible += expandedCharacterLength;
            letterBody.maxVisibleCharacters = Stage == LetterStage.AwaitingSignature ? characterCount : Mathf.Min(visible, characterCount);
            Canvas.ForceUpdateCanvases();
            letterScroll.verticalNormalizedPosition = scroll;
            UpdateReadingHint();
        }

        private void CacheExpandedRange()
        {
            expandedCharacterStart = expandedCharacterLength = 0;
            if (CurrentChoiceGroup < 0) return;
            string slot = $"slot:{CurrentChoiceGroup}";
            string last = $"choice:{CurrentChoiceGroup}:{FirstDayLetterDraft.Labels[CurrentChoiceGroup].Length - 1}";
            for (int i = 0; i < letterBody.textInfo.linkCount; i++)
            {
                TMP_LinkInfo link = letterBody.textInfo.linkInfo[i];
                if (link.GetLinkID() == slot) expandedCharacterStart = link.linkTextfirstCharacterIndex + link.linkTextLength;
                if (link.GetLinkID() == last)
                    expandedCharacterLength = link.linkTextfirstCharacterIndex + link.linkTextLength + 1 - expandedCharacterStart;
            }
        }

        private void UpdateReadingHint()
        {
            if (readingHint == null || !IsPlaying) return;
            if (Stage == LetterStage.Reading)
            {
                readingHint.text = IsWaitingForInput ? "> 请点击正文中的选项填写空位"
                    : EditorOpen ? "> 请点击正文中的选项完成修改" : NeedsScroll ? "信中的文字正在慢慢浮现…  ·  可用滚轮阅读" : "信中的文字正在慢慢浮现…";
                return;
            }
            if (Stage != LetterStage.AwaitingSignature) return;
            bool complete = (!InteractiveLetter || draft.IsComplete) && !EditorOpen;
            signatureButton.interactable = complete;
            readingHint.text = complete ? "> 点击已填文字可修改；点击署名继续" : "> 请完成填写并收起选项，再点击署名继续";
        }

        // Commit completion before invoking external code: callbacks may close or reuse the panel.
        private void FinishLetter()
        {
            if (!IsPlaying) return;
            StopHint();
            waitingLink = null;
            CancelLetterChoice();
            CancelPlayerName();
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
            Action callback = finished;
            finished = null;
            callback?.Invoke();
        }

        public void Stop()
        {
            finished = null;
            pendingContent = null;
            CancelLetterChoice();
            IsPlaying = false;
            Stage = LetterStage.Stopped;
            transition?.Kill(false);
            transition = null;
            StopHint();
            waitingLink = null;
            CancelLetterChoice();
            CancelPlayerName();
            typeTimer = 0f;
            if (letterScroll != null) letterScroll.StopMovement();
            if (rootGroup != null) rootGroup.interactable = rootGroup.blocksRaycasts = false;
            if (envelopeButton != null) envelopeButton.interactable = false;
            if (signatureButton != null) signatureButton.interactable = false;
            if (skipButton != null) skipButton.interactable = false;
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
