using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow
{
    public abstract class GameFlowPanel : UIPanel
    {
        [Header("UIKit DOTween transition")]
        [SerializeField, Min(0f)] private float showDuration = 0.22f;
        [SerializeField, Min(0f)] private float hideDuration = 0.14f;
        [SerializeField, Min(0f)] private float showOffset = 22f;
        [SerializeField, Min(0f)] private float hideOffset = 12f;
        [SerializeField, Range(0.9f, 1f)] private float showStartScale = 0.98f;
        [SerializeField, Range(0.9f, 1f)] private float hideEndScale = 0.98f;
        [SerializeField] private Ease showEase = Ease.OutCubic;
        [SerializeField] private Ease hideEase = Ease.InCubic;
        [SerializeField] private bool modal;
        [SerializeField] private CanvasGroup canvasGroup;
        private Action afterAnimatedClose;
        private RectTransform panelRect;
        private Vector2 basePosition;
        private Vector3 baseScale;

        protected GameFlowBootstrap Flow { get; private set; }

        /// <summary>场景内面板（非 UIKit 实例化）由钩子直接设置 Flow 时调用。</summary>
        protected void SetFlow(GameFlowBootstrap flow) => Flow = flow;

        protected void SetModal(bool value) => modal = value;

        protected virtual void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            panelRect = transform as RectTransform;
            if (panelRect != null)
            {
                basePosition = panelRect.anchoredPosition;
                baseScale = panelRect.localScale;
            }
            ResetInteractionState();
        }

        protected override void OnOpen(IUIData data = null)
        {
            IGameFlowPanelData panelData = data as IGameFlowPanelData;
            if (panelData?.Flow != null)
                Flow = panelData.Flow;
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            ResetInteractionState();
            if (modal)
                UIKit.SetPanelModal(this, true);
            if (panelRect != null)
            {
                basePosition = panelRect.anchoredPosition;
                baseScale = panelRect.localScale;
                ConfigureAnimations();
            }
        }

        protected override void OnBlur()
        {
            if (canvasGroup != null) canvasGroup.interactable = false;
        }

        protected override void OnFocus()
        {
            OnShow();
        }

        protected override void OnWillHide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        protected override void OnShow()
        {
            ResetInteractionState();
            // UIKit panels share a root canvas; keep the entire navigation stack above
            // the scene-owned map (sorting orders 10 and 30), including save confirmations.
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.rootCanvas.transform != transform)
                canvas.rootCanvas.sortingOrder = Mathf.Max(canvas.rootCanvas.sortingOrder, 100);
            if (EventSystem.current == null) return;
            Selectable target = GetDefaultSelectable();
            if (target == null || !target.IsActive() || !target.IsInteractable())
                foreach (Selectable candidate in GetComponentsInChildren<Selectable>())
                    if (candidate.IsActive() && candidate.IsInteractable()) { target = candidate; break; }
            if (target != null && target.IsActive() && target.IsInteractable())
                EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        protected override void OnDidHide()
        {
            ResetVisualState();
            Action callback = afterAnimatedClose;
            afterAnimatedClose = null;
            if (callback == null) return;
            UIKit.ClosePanel(this);
            Flow = null;
            callback();
        }

        protected override void OnClose()
        {
            ResetInteractionState();
        }

        public void CloseAnimated(Action afterClose = null)
        {
            if (afterAnimatedClose != null) return;
            if (State != PanelState.Open && State != PanelState.Opening)
            {
                UIKit.ClosePanel(this);
                afterClose?.Invoke();
                return;
            }
            afterAnimatedClose = afterClose ?? (() => { });
            Hide();
        }

        private void ConfigureAnimations()
        {
            if (panelRect == null) return;
            Vector2 position = basePosition;
            var show = new CompositeAnimation(CompositeMode.Parallel)
                .Add(new DOTweenFadeAnimation(showDuration, 0f, 1f, showEase))
                .Add(new DOTweenSlideAnimation(showDuration, position + Vector2.down * showOffset, position, showEase))
                .Add(new DOTweenScaleAnimation(showDuration, Vector3.one * showStartScale, Vector3.one, showEase));
            var hide = new CompositeAnimation(CompositeMode.Parallel)
                .Add(new DOTweenFadeAnimation(hideDuration, 1f, 0f, hideEase))
                .Add(new DOTweenSlideAnimation(hideDuration, position, position + Vector2.down * hideOffset, hideEase))
                .Add(new DOTweenScaleAnimation(hideDuration, Vector3.one, Vector3.one * hideEndScale, hideEase));
            SetShowAnimation(show);
            SetHideAnimation(hide);
        }

        private void ResetVisualState()
        {
            ResetInteractionState();
            if (panelRect != null)
            {
                panelRect.anchoredPosition = basePosition;
                panelRect.localScale = baseScale;
            }
        }

        private void ResetInteractionState()
        {
            if (canvasGroup == null)
                return;
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }
}
