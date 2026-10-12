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
        private void Awake() { if(bellBody!=null) restScale=bellBody.localScale; }
        public void OnPointerClick(PointerEventData eventData)
        {
            if(eventData.button!=PointerEventData.InputButton.Left || runtime==null || runtime.InputBlocked || runtime.IsDragging) return;
            runtime.DeliverCup();
            if(press!=null) StopCoroutine(press);
            press=StartCoroutine(Press());
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            if(runtime!=null && !runtime.InputBlocked)
            {
                if(dome!=null) dome.color=new Color(1f,.93f,.5f);
                runtime.ShowHint("点击按铃，交付方框内的杯子（当前显示的订单）");
            }
        }
        public void OnPointerExit(PointerEventData eventData) { if(dome!=null) dome.color=new Color(.95f,.72f,.25f); }
        private IEnumerator Press()
        {
            float elapsed=0f;
            while(elapsed<.24f)
            {
                elapsed+=Time.unscaledDeltaTime;
                if(bellBody!=null) bellBody.localScale=new Vector3(restScale.x,restScale.y*(1f-.18f*Mathf.Sin(Mathf.Clamp01(elapsed/.24f)*Mathf.PI)),restScale.z);
                yield return null;
            }
            if(bellBody!=null) bellBody.localScale=restScale;
            press=null;
        }
        private void OnDisable()
        {
            if(press!=null) StopCoroutine(press);
            press=null;
            if(bellBody!=null && restScale!=Vector3.zero) bellBody.localScale=restScale;
        }
    }
}
