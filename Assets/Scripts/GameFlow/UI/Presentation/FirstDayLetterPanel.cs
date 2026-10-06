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
        [SerializeField] private Button envelopeButton;
        [SerializeField] private CanvasGroup hintGroup;
        [SerializeField] private CanvasGroup paperGroup;
        [SerializeField] private RectTransform paper;
        [SerializeField] private TMP_Text letterBody;
        [SerializeField] private TMP_Text signatureText;
        [SerializeField] private Button signatureButton;
        [SerializeField] private TMP_Text readingHint;
        [SerializeField] private ScrollRect letterScroll;

        [Header("Letter content (editable draft)")]
        [SerializeField, TextArea(8, 30)] private string letter =
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

        public void Play(Action onFinished)
        {
            ValidateBindings();
            Stop();
            CapturePose();
            RestorePose();
            finished = onFinished;
            IsPlaying = true;
            Stage = LetterStage.Envelope;
            rootGroup.alpha = 1f;
            rootGroup.interactable = rootGroup.blocksRaycasts = true;
            envelopeGroup.gameObject.SetActive(true);
            envelopeGroup.alpha = 1f;
            envelopeButton.interactable = true;
            paperGroup.gameObject.SetActive(false);
            paperGroup.alpha = 0f;
            signatureButton.gameObject.SetActive(false);
            signatureButton.interactable = false;
            signatureText.text = signature;
            readingHint.text = "";
            letterBody.text = letter;
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
            StopHint();
            paperGroup.gameObject.SetActive(true);
            paper.anchoredPosition = paperPosition + Vector2.down * paperRise;
            paper.localScale = paperScale * .96f;
            letterBody.maxVisibleCharacters = int.MaxValue;
            letterBody.ForceMeshUpdate();
            characterCount = letterBody.textInfo.characterCount;
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
                readingHint.text = NeedsScroll ? "信中的文字正在慢慢浮现…  ·  可用滚轮阅读" : "信中的文字正在慢慢浮现…";
            });
        }

        private void Update()
        {
            if (!IsPlaying || Stage != LetterStage.Reading) return;
            typeTimer += Time.unscaledDeltaTime;
            int visible = Mathf.Min(characterCount,
                Mathf.FloorToInt(typeTimer / Mathf.Max(.005f, characterInterval)));
            letterBody.maxVisibleCharacters = visible;
            if (visible < characterCount) return;
            Stage = LetterStage.AwaitingSignature;
            signatureButton.gameObject.SetActive(true);
            signatureButton.interactable = true;
            readingHint.text = NeedsScroll ? "可用滚轮读完信件  ·  点击叔叔的署名继续" : "读完后，点击叔叔的署名继续";
        }

        /// <summary>Completion requires a fresh click after all text has appeared.</summary>
        public void ConfirmSignature()
        {
            if (!IsPlaying || Stage != LetterStage.AwaitingSignature) return;
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
                Stage = LetterStage.Finished;
                IsPlaying = false;
                rootGroup.interactable = rootGroup.blocksRaycasts = false;
                Action callback = finished;
                finished = null;
                callback?.Invoke();
            });
        }

        public void Stop()
        {
            finished = null;
            IsPlaying = false;
            Stage = LetterStage.Stopped;
            transition?.Kill(false);
            transition = null;
            StopHint();
            typeTimer = 0f;
            if (letterScroll != null) letterScroll.StopMovement();
            if (rootGroup != null) rootGroup.interactable = rootGroup.blocksRaycasts = false;
            if (envelopeButton != null) envelopeButton.interactable = false;
            if (signatureButton != null) signatureButton.interactable = false;
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
                || signatureText == null || signatureButton == null || readingHint == null || letterScroll == null)
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
            base.OnBeforeDestroy();
        }
    }
}
