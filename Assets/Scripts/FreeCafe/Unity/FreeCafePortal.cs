using UnityEngine;
using UnityEngine.EventSystems;
namespace Spotlight.FreeCafe
{
    public sealed class FreeCafePortal : MonoBehaviour,IPointerClickHandler,IPointerEnterHandler,IPointerExitHandler
    {
        public FreeCafeRuntime runtime;
        public AreaId destination;
        public SpriteRenderer visual;
        public void OnPointerClick(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left && runtime!=null) runtime.Enter(destination); }
        public void OnPointerEnter(PointerEventData e) { if(visual!=null) visual.color=new Color(1,.85f,.4f); }
        public void OnPointerExit(PointerEventData e) { if(visual!=null) visual.color=Color.white; }
    }
}
