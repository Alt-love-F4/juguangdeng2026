using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// The root Graphic owns the hit area; child icons/labels share coffee-coloured
    /// hover, focus and press feedback without moving or shrinking that hit area.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class SaveSlotButtonFeedback : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [SerializeField] private Color highlightedColor = new Color(.42f, .24f, .12f, 1f);
        [SerializeField] private Color pressedColor = new Color(.28f, .14f, .065f, 1f);
        [SerializeField, Range(.8f, 1f)] private float pressedScale = .94f;
        [SerializeField, Min(0f)] private float transitionDuration = .08f;
        private Button button;
        private Graphic[] graphics;
        private Color[] normalColors;
        private Vector3[] normalScales;
        private bool hovered;
        private bool focused;
        private bool pointerPressed;
        private bool submitPressed;
        private Tween feedbackTween;
        private Coroutine submitRoutine;
        private float currentState;

        private void Awake() => Initialize();

        // Also called by the slot for initially inactive action objects. Their
        // Awake can be delayed until the first selection, but hit settings must not be.
        public void Initialize()
        {
            if (button == null) button = GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            Graphic hit = GetComponent<Graphic>();
            if (hit == null)
            {
                Image image = gameObject.AddComponent<Image>();
                image.color = Color.clear;
                hit = image;
            }
            hit.raycastTarget = true;
            hit.canvasRenderer.cullTransparentMesh = false;
            // Do not force canvasRenderer.cull=false: RectMask2D must still clip
            // actions outside the scroll viewport.
            button.targetGraphic = hit;
            EnsureGraphics();
        }

        private void OnEnable()
        {
            Initialize();
            ResetVisual();
        }

        private void OnDisable()
        {
            ResetVisual();
            EventSystem system = EventSystem.current;
            if (system != null && !system.alreadySelecting && system.currentSelectedGameObject == gameObject)
                system.SetSelectedGameObject(null);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = IsUsable();
            RefreshVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            RefreshVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsUsable()) return;
            hovered = true;
            pointerPressed = true;
            RefreshVisual();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            pointerPressed = false;
            RefreshVisual();
        }

        public void OnSelect(BaseEventData eventData)
        {
            focused = IsUsable();
            RefreshVisual();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            focused = false;
            RefreshVisual();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!IsUsable()) return;
            if (submitRoutine != null) StopCoroutine(submitRoutine);
            submitPressed = true;
            RefreshVisual();
            submitRoutine = StartCoroutine(ReleaseSubmit());
        }

        private IEnumerator ReleaseSubmit()
        {
            yield return new WaitForSecondsRealtime(.12f);
            submitRoutine = null;
            submitPressed = false;
            RefreshVisual();
        }

        public void RefreshAvailability()
        {
            if (!IsUsable()) ResetVisual();
        }

        private void RefreshVisual()
        {
            if (!IsUsable()) { ResetVisual(); return; }
            float target = submitPressed || (pointerPressed && hovered) ? 2f : hovered || focused ? 1f : 0f;
            StopTween();
            if (transitionDuration <= 0f) { Apply(target); return; }
            feedbackTween = DOTween.To(() => currentState, Apply, target, transitionDuration)
                .SetUpdate(true).SetEase(Ease.OutQuad).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        public void ResetVisual()
        {
            hovered = focused = pointerPressed = submitPressed = false;
            if (submitRoutine != null) StopCoroutine(submitRoutine);
            submitRoutine = null;
            StopTween();
            Apply(0f);
        }

        private void StopTween()
        {
            feedbackTween?.Kill(false);
            feedbackTween = null;
        }

        private void OnDestroy() => StopTween();
        private bool IsUsable() => button != null && button.IsActive() && button.IsInteractable();

        private void EnsureGraphics()
        {
            if (graphics != null) return;
            Graphic[] all = GetComponentsInChildren<Graphic>(true);
            var owned = new System.Collections.Generic.List<Graphic>(all.Length);
            foreach (Graphic graphic in all)
            {
                if (graphic.transform == transform || graphic.GetComponentInParent<Button>(true) != button) continue;
                // Child decoration must not steal the rectangular action's raycast.
                graphic.raycastTarget = false;
                owned.Add(graphic);
            }
            graphics = owned.ToArray();
            normalColors = new Color[graphics.Length];
            normalScales = new Vector3[graphics.Length];
            for (int i = 0; i < graphics.Length; i++)
            {
                normalColors[i] = graphics[i].color;
                normalScales[i] = graphics[i].transform.localScale;
            }
        }

        private void Apply(float state)
        {
            // Inactive buttons may receive ResetVisual before their Awake runs.
            if (button == null) Initialize();
            EnsureGraphics();
            currentState = state;
            float highlight = Mathf.Clamp01(state);
            float press = Mathf.Clamp01(state - 1f);
            Color coffee = Color.Lerp(highlightedColor, pressedColor, press);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] == null) continue;
                Color color = Color.Lerp(normalColors[i], coffee, highlight);
                color.a = normalColors[i].a;
                graphics[i].color = color;
                graphics[i].transform.localScale = normalScales[i] * Mathf.Lerp(1f, pressedScale, press);
            }
        }
    }
}
