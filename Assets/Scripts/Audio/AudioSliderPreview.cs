using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Spotlight.Audio
{
    [DisallowMultipleComponent, RequireComponent(typeof(Slider))]
    public sealed class AudioSliderPreview : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDeselectHandler
    {
        private Slider slider;
        private bool changed;
        private void Awake() => slider = GetComponent<Slider>();
        private void OnEnable()
        {
            if (slider == null) slider = GetComponent<Slider>();
            changed = false;
            slider.onValueChanged.AddListener(ValueChanged);
        }
        private void OnDisable() { if (slider != null) slider.onValueChanged.RemoveListener(ValueChanged); changed = false; }
        private void ValueChanged(float value) => changed = true;
        public void OnPointerDown(PointerEventData eventData) { changed = false; }
        public void OnPointerUp(PointerEventData eventData) => Preview();
        public void OnDeselect(BaseEventData eventData) => Preview();
        private void Preview()
        {
            if (slider == null || !slider.IsInteractable() || !changed) return;
            changed = false;
            GameAudio.Play(AudioKeys.VolumePreview);
        }
    }
}
