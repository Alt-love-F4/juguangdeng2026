using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight.FreeCafe
{
    /// <summary>Ends the current day when the player clicks the highlighted dormitory bed.</summary>
    [DisallowMultipleComponent]
    public sealed class FreeCafeBedDayEnd : MonoBehaviour, IPointerClickHandler
    {
        public FreeCafeRuntime runtime;
        public DayFlowController dayFlow;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            if (runtime == null) runtime = GetComponentInParent<FreeCafeRuntime>();
            if (runtime == null || runtime.World == null || runtime.InputBlocked) return;
            if (runtime.World.State.currentArea != AreaId.Basement) return;
            if (dayFlow == null) dayFlow = FindFirstObjectByType<DayFlowController>();
            dayFlow?.EndDayFromDormitory();
        }
    }
}
