using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// Full-screen coffee transition used while a scene or gameplay payload is loading.
    /// The panel owns the transition material, while UIKit still owns panel lifetime.
    /// </summary>
    public sealed class ShortLoadingPanel : GameFlowPanel
    {
        [Header("Prefab bindings")]
        [SerializeField] private Image transitionImage;
        [SerializeField, Min(0f)] private float coverDuration = 0.55f;
        [SerializeField, Min(0f)] private float disappearDuration = 0.55f;

        [Header("Cup pattern movement")]
        [Tooltip("整体滚动速度，单位为每秒移动的屏幕 UV 距离；0 表示静止。")]
        [SerializeField, Min(0f)] private float movementSpeed = 0.08f;
        [Tooltip("整体移动方向：X 正值向右，Y 正值向上；只使用方向，不使用向量长度。")]
        [SerializeField] private Vector2 movementDirection = Vector2.one;

        private static readonly int TransitionId = Shader.PropertyToID("_Transition");
        private static readonly int DisappearingId = Shader.PropertyToID("_Disappearing");
        private static readonly int CupScrollOffsetId = Shader.PropertyToID("_CupScrollOffset");

        private Vector2 cupScrollOffset;
        private Material runtimeMaterial;
        private CanvasGroup panelCanvasGroup;
        private Action coveredCallbacks;
        private Action finishedCallbacks;
        private bool endRequested;
        private bool closeRequested;
        private bool covered;

        /// <summary>True after the opening transition has fully covered the screen.</summary>
        public bool IsCovered => covered;

        protected override void Awake()
        {
            SetModal(true);
            base.Awake();
            panelCanvasGroup = GetComponent<CanvasGroup>();
            EnsureRuntimeMaterial();
            ConfigureTransitionAnimations();
        }

        protected override void OnOpen(IUIData data = null)
        {
            base.OnOpen(data);
            EnsureRuntimeMaterial();
            ConfigureTransitionAnimations();
            // UIKit re-opens an already visible panel without replaying its show animation.
            bool alreadyVisible = State == PanelState.Open;
            covered = alreadyVisible;
            if (!alreadyVisible) cupScrollOffset = Vector2.zero;
            endRequested = false;
            closeRequested = false;
            coveredCallbacks = null;
            finishedCallbacks = null;
            SetShaderState(alreadyVisible ? 1f : 0f, 0f);
        }

        protected override void OnShow()
        {
            base.OnShow();
            if (panelCanvasGroup == null) panelCanvasGroup = GetComponent<CanvasGroup>();
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = true;
            }
            if (transitionImage != null) transitionImage.raycastTarget = true;
        }

        protected override void OnWillHide()
        {
            base.OnWillHide();
            // GameFlowPanel disables raycasts by default; this transition must remain modal
            // until UIKit has completed the hide animation.
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = true;
            }
        }

        protected override void OnDidHide()
        {
            base.OnDidHide();
            // EndLoading can be requested while another owner is already hiding this panel.
            if (endRequested && !closeRequested && State == PanelState.Hide)
                BeginClose();
        }

        protected override void OnClose()
        {
            base.OnClose();
            if (!closeRequested) finishedCallbacks = null;
            covered = false;
            endRequested = false;
            closeRequested = false;
            coveredCallbacks = null;
            SetShaderState(0f, 0f);
        }

        protected override void OnBeforeDestroy()
        {
            coveredCallbacks = null;
            finishedCallbacks = null;
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
                runtimeMaterial = null;
            }
            base.OnBeforeDestroy();
        }

        /// <summary>
        /// Starts or resets the opening transition. The callback runs once the screen is opaque.
        /// </summary>
        public void BeginLoading(Action covered = null)
        {
            if (covered != null)
            {
                if (IsCovered) covered();
                else coveredCallbacks += covered;
            }

            if (closeRequested || State == PanelState.Hiding || State == PanelState.Closing)
                return;

            endRequested = false;
            if (!IsCovered && State != PanelState.Opening && State != PanelState.Open)
            {
                EnsureRuntimeMaterial();
                SetShaderState(0f, 0f);
            }
        }

        /// <summary>
        /// Requests the opening cover to finish first, then exits and closes through UIKit.
        /// </summary>
        public void EndLoading(Action finished = null)
        {
            if (State == PanelState.Cached || State == PanelState.Close)
            {
                finished?.Invoke();
                return;
            }
            if (finished != null) finishedCallbacks += finished;
            endRequested = true;

            if (IsCovered)
                BeginClose();
        }

        private void BeginClose()
        {
            if (closeRequested) return;
            closeRequested = true;
            CloseAnimated(CompleteClose);
        }

        private void CompleteClose()
        {
            Action callbacks = finishedCallbacks;
            finishedCallbacks = null;
            closeRequested = false;
            callbacks?.Invoke();
        }

        private void ConfigureTransitionAnimations()
        {
            SetShowAnimation(new MaterialTransitionAnimation(
                runtimeMaterial, coverDuration, true, OnCovered));
            SetHideAnimation(new MaterialTransitionAnimation(
                runtimeMaterial, disappearDuration, false, OnDisappeared));
        }

        private void OnCovered()
        {
            covered = true;
            Action callbacks = coveredCallbacks;
            coveredCallbacks = null;
            callbacks?.Invoke();
            if (endRequested) BeginClose();
        }

        private void OnDisappeared()
        {
            covered = false;
        }

        private void EnsureRuntimeMaterial()
        {
            if (transitionImage == null || runtimeMaterial != null) return;

            Material source = transitionImage.material;
            if (source != null)
                runtimeMaterial = new Material(source);
            else
            {
                Shader shader = Shader.Find("Spotlight/UI/Coffee Transition");
                if (shader != null) runtimeMaterial = new Material(shader);
            }

            if (runtimeMaterial == null) return;
            runtimeMaterial.name = "ShortLoadingPanel (Runtime)";
            transitionImage.material = runtimeMaterial;
            transitionImage.raycastTarget = true;
        }

        private void SetShaderState(float transition, float disappearing)
        {
            if (runtimeMaterial == null) return;
            runtimeMaterial.SetFloat(TransitionId, Mathf.Clamp01(transition));
            runtimeMaterial.SetFloat(DisappearingId, Mathf.Clamp01(disappearing));
            runtimeMaterial.SetVector(CupScrollOffsetId, cupScrollOffset);
        }

        private void Update()
        {
            if (runtimeMaterial == null) return;

            // Accumulate unscaled displacement so pause does not stop the loading effect,
            // and changing speed/direction live never jumps the pattern to a new position.
            cupScrollOffset += movementDirection.normalized * Mathf.Max(0f, movementSpeed) * Time.unscaledDeltaTime;
            runtimeMaterial.SetVector(CupScrollOffsetId, cupScrollOffset);
        }

        private sealed class MaterialTransitionAnimation : IUIAnimation
        {
            private readonly Material material;
            private readonly float duration;
            private readonly bool appearing;
            private readonly Action complete;
            private Tween tween;

            public MaterialTransitionAnimation(
                Material material,
                float duration,
                bool appearing,
                Action complete)
            {
                this.material = material;
                this.duration = Mathf.Max(0f, duration);
                this.appearing = appearing;
                this.complete = complete;
            }

            public float Duration => duration;
            public bool IsPlaying => tween != null && tween.IsActive() && tween.IsPlaying();

            public void Play(RectTransform target, Action onComplete = null)
            {
                Stop();
                if (material == null)
                {
                    complete?.Invoke();
                    onComplete?.Invoke();
                    return;
                }

                SetStartState();
                tween = DOTween.To(
                        () => appearing
                            ? material.GetFloat(TransitionId)
                            : material.GetFloat(DisappearingId),
                        value => SetProgress(value),
                        1f,
                        duration)
                    .SetEase(Ease.InOutCubic)
                    .SetUpdate(true)
                    .SetTarget(material)
                    .OnComplete(() =>
                    {
                        tween = null;
                        complete?.Invoke();
                        onComplete?.Invoke();
                    });
            }

            public void Stop()
            {
                Tween current = tween;
                tween = null;
                if (current != null && current.IsActive()) current.Kill(false);
            }

            public void Reset(RectTransform target)
            {
                Stop();
                SetStartState();
            }

            public void SetToEndState(RectTransform target)
            {
                Stop();
                SetProgress(1f);
            }

            public void Recycle()
            {
                Stop();
            }

            private void SetStartState()
            {
                if (material == null) return;
                if (appearing)
                {
                    material.SetFloat(TransitionId, 0f);
                    material.SetFloat(DisappearingId, 0f);
                }
                else
                {
                    material.SetFloat(TransitionId, 1f);
                    material.SetFloat(DisappearingId, 0f);
                }
            }

            private void SetProgress(float value)
            {
                if (material == null) return;
                if (appearing)
                {
                    material.SetFloat(TransitionId, Mathf.Clamp01(value));
                    material.SetFloat(DisappearingId, 0f);
                }
                else
                {
                    material.SetFloat(TransitionId, 1f);
                    material.SetFloat(DisappearingId, Mathf.Clamp01(value));
                }
            }
        }
    }
}
