using System.Collections;
using UnityEngine;

namespace Spotlight.GameFlow
{
    public sealed class MapSidebarView : MonoBehaviour
    {
        public enum SlideSide { Right, Left }
        [SerializeField] private SlideSide slideFrom = SlideSide.Right;
        private const float EdgeInset = 28;
        private const float Duration = .22f;
        private RectTransform rect;
        private CanvasGroup group;
        private Coroutine motion;
        private bool visible;
        private float openness;
        public bool IsVisible => visible;

        public void Initialize()
        {
            if (rect != null) return;
            rect = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            UpdateLayout();
            HideImmediately();
        }

        public void SetVisible(bool value)
        {
            if (rect == null) Initialize();
            if (visible == value) return;
            Spotlight.Audio.GameAudio.Play(value ? Spotlight.Audio.AudioKeys.PanelOpen : Spotlight.Audio.AudioKeys.PanelClose);
            visible = value;
            if (motion != null) StopCoroutine(motion);
            motion = null;
            // Inactive panels do not receive LateUpdate: size them before the first visible frame.
            UpdateLayout();
            if (value && !gameObject.activeSelf)
            {
                openness = 0;
                ApplyPose();
                gameObject.SetActive(true);
            }
            group.interactable = value;
            group.blocksRaycasts = value;
            motion = StartCoroutine(Animate(value));
        }

        public void HideImmediately()
        {
            if (motion != null) StopCoroutine(motion);
            motion = null;
            visible = false;
            openness = 0;
            if (rect != null) ApplyPose();
            if (group != null) { group.alpha = 0; group.blocksRaycasts = false; group.interactable = false; }
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            if (rect == null || !(rect.parent is RectTransform parent) || parent.rect.width <= 1 || parent.rect.height <= 1) return;
            float width = Mathf.Min(550, Mathf.Max(1, parent.rect.width - EdgeInset * 2));
            float height = Mathf.Min(1000, Mathf.Max(1, parent.rect.height - Mathf.Min(350, parent.rect.height * .3f)));
            var anchor = new Vector2(slideFrom == SlideSide.Right ? 1 : 0, .5f);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            // Recompute after the Canvas parent obtains its final screen dimensions.
            rect.sizeDelta = new Vector2(width, height);
            ApplyPose();
        }

        private void ApplyPose()
        {
            float direction = slideFrom == SlideSide.Right ? 1 : -1;
            float hiddenX = direction * (rect.rect.width + EdgeInset);
            float shownX = -direction * EdgeInset;
            rect.anchoredPosition = new Vector2(Mathf.Lerp(hiddenX, shownX, openness), 0);
            if (group != null) group.alpha = openness;
        }

        private void OnDisable()
        {
            if (motion != null) StopCoroutine(motion);
            motion = null;
            visible = false;
            openness = 0;
            if (rect != null) ApplyPose();
            if (group != null) { group.alpha = 0; group.interactable = false; group.blocksRaycasts = false; }
        }

        private IEnumerator Animate(bool opening)
        {
            // Keep the current pose when reversing, with duration proportional to distance.
            float start = openness;
            float target = opening ? 1 : 0;
            float duration = Mathf.Max(.001f, Duration * Mathf.Abs(target - start));
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / duration), 3);
                openness = Mathf.Lerp(start, target, progress);
                ApplyPose();
                yield return null;
            }
            openness = target;
            ApplyPose();
            group.blocksRaycasts = opening;
            motion = null;
            if (!opening) gameObject.SetActive(false);
        }
    }
}
