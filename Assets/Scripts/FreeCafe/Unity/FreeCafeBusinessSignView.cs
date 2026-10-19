using System;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>营业挂牌只呈现世界状态；翻面结束后仍通过原有营业入口结算。</summary>
    [RequireComponent(typeof(FreeEntityView))]
    public sealed class FreeCafeBusinessSignView : MonoBehaviour
    {
        public FreeEntityView entityView;
        public Transform flipRoot;
        public SpriteRenderer artwork;
        [Min(.1f)] public float flipDuration = .48f;
        public bool IsFlipping { get; private set; }
        public Vector3 CaptionWorldPosition => entityView.label.transform.position;
        private bool initialized, shownOpen, targetOpen;
        private float elapsed;
        private Action afterFlip, queuedClose;

        public void Present(bool highlighted = false)
        {
            if (entityView == null || entityView.Runtime == null || artwork == null) return;
            bool open = entityView.Runtime.World.State.open;
            if (!initialized)
            {
                initialized = true;
                shownOpen = targetOpen = open;
                SetCaption(open);
            }
            else if (afterFlip == null && targetOpen != open) BeginFlip(open);
            // Refresh can replace the generic entity caption every frame.
            SetCaption(shownOpen);
            artwork.color = highlighted ? new Color(1f, .96f, .86f) : Color.white;
            UpdateSorting(entityView.body.sortingOrder);
        }

        public void CloseAfterFlip(Action close)
        {
            if (IsFlipping)
            {
                // Opening animation must not swallow a player's immediate close request.
                // Repeated clicks during the closing animation remain idempotent.
                if (targetOpen) queuedClose = close;
                return;
            }
            if (initialized && shownOpen) BeginFlip(false, close);
        }

        private void BeginFlip(bool open, Action completed = null)
        {
            targetOpen = open;
            afterFlip = completed;
            elapsed = 0;
            IsFlipping = true;
        }

        private void Update()
        {
            if (!IsFlipping || flipRoot == null) return;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.1f, flipDuration));
            float eased = t * t * (3f - 2f * t);
            // Positive width on both halves keeps Chinese glyphs readable on the reverse.
            flipRoot.localScale = new Vector3(Mathf.Max(.015f, Mathf.Abs(Mathf.Cos(eased * Mathf.PI))), 1f, 1f);
            flipRoot.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 2f) * 3f);
            if (t >= .5f) { shownOpen = targetOpen; SetCaption(shownOpen); }
            if (t < 1f) return;
            ResetPose();
            var completed = afterFlip;
            afterFlip = null;
            completed?.Invoke();
            var close = queuedClose;
            queuedClose = null;
            if (close != null && entityView != null && entityView.Runtime != null
                && entityView.Runtime.World.State.open) BeginFlip(false, close);
        }

        private void SetCaption(bool open)
        {
            var runtime = entityView.Runtime;
            bool closedToday = !open && runtime != null && !runtime.IsSandboxMode
                && runtime.World.State.businessPhase == BusinessPhase.Night;
            entityView.label.text = open ? "结束营业" : closedToday ? "今日已打烊" : "开始营业";
        }
        private void ResetPose()
        {
            IsFlipping = false;
            if (flipRoot == null) return;
            flipRoot.localScale = Vector3.one;
            flipRoot.localRotation = Quaternion.identity;
        }
        private void OnDisable()
        {
            afterFlip = queuedClose = null;
            initialized = false;
            ResetPose();
        }
        public void UpdateSorting(int bodyOrder)
        {
            if (artwork != null) artwork.sortingOrder = bodyOrder + 1;
            if (entityView != null && entityView.label != null)
                entityView.label.GetComponent<MeshRenderer>().sortingOrder = bodyOrder + 2;
        }
    }
}
