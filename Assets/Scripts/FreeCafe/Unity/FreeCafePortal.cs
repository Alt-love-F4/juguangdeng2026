using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight.FreeCafe
{
    public sealed class FreeCafePortal : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public FreeCafeRuntime runtime;
        public AreaId destination;
        public SpriteRenderer visual;
        public FreeCafeEntranceMarker entranceMarker;
        public FreeCafeInteractivePropView propView;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && runtime != null && runtime.IsCurrentAreaTransform(transform))
                runtime.Enter(destination);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (propView != null) propView.SetHovered(true);
            else if (entranceMarker != null) entranceMarker.SetHovered(true);
            else if (visual != null) visual.color = new Color(1f, .85f, .4f);
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (propView != null) propView.SetHovered(false);
            else if (entranceMarker != null) entranceMarker.SetHovered(false);
            else if (visual != null) visual.color = Color.white;
        }
    }
}
