using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight.FreeCafe
{
    [DisallowMultipleComponent]
    public sealed class FreeCafeDeliveryBell : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private FreeCafeRuntime runtime;
        [SerializeField] private Transform bellBody;
        [SerializeField] private SpriteRenderer dome;
        private Coroutine press;
        private Vector3 restScale;
        private Quaternion restRotation;
        private Color restColor = Color.white;
        public SpriteRenderer Visual => dome;
        public Transform TutorialAnchor => bellBody != null ? bellBody : transform;
        private FreeCafeRuntime Owner
        {
            get
            {
                if (runtime == null)
                {
                    var entity = GetComponentInParent<FreeEntityView>();
                    if (entity != null) runtime = entity.Runtime;
                }
                return runtime;
            }
        }
        public bool CanInteract => Owner != null && isActiveAndEnabled && !Owner.InputBlocked
            && !Owner.IsDragging && Owner.IsCurrentAreaTransform(transform);
        private void Awake()
        {
            if (bellBody != null) { restScale = bellBody.localScale; restRotation = bellBody.localRotation; }
            if (dome != null) restColor = dome.color;
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left || !CanInteract) return;
            Owner.RingDeliveryBell();
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Bell);
            if (press != null) StopCoroutine(press);
            press = StartCoroutine(Press());
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanInteract) return;
            if (dome != null) dome.color = new Color(1f, .94f, .73f);
            Owner.ShowHint("点击按铃，交付案板上盘子里的咖啡（当前显示的订单）");
        }
        public void OnPointerExit(PointerEventData eventData) { if (dome != null) dome.color = restColor; }
        private IEnumerator Press()
        {
            float elapsed = 0f;
            while (elapsed < .24f)
            {
                elapsed += Time.unscaledDeltaTime;
                float pulse = Mathf.Sin(Mathf.Clamp01(elapsed / .24f) * Mathf.PI);
                if (bellBody != null)
                {
                    bellBody.localScale = new Vector3(restScale.x, restScale.y * (1f - .12f * pulse), restScale.z);
                    bellBody.localRotation = restRotation * Quaternion.Euler(0, 0, Mathf.Sin(elapsed * 75) * pulse * 3);
                }
                yield return null;
            }
            ResetPose();
            press = null;
        }
        private void ResetPose()
        {
            if (bellBody != null && restScale != Vector3.zero)
            { bellBody.localScale = restScale; bellBody.localRotation = restRotation; }
        }
        private void OnDisable()
        {
            if (press != null) StopCoroutine(press);
            press = null;
            ResetPose();
            if (dome != null) dome.color = restColor;
        }
    }
}
