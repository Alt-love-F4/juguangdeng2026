using UnityEngine;
namespace Spotlight.GameFlow.Tests
{
    public sealed class DayFlowTestEndpoint : MonoBehaviour, IDayFlowGameplayEndpoint
    {
        public bool active;
        public bool finished;
        public int starts;
        public int ends;
        public bool DayFlowBusinessActive => active;
        public bool DayFlowBusinessFinished => finished;
        public void StartDayFlowBusiness() { active = true; finished = false; starts++; }
        public void EndDayFlowBusiness() { active = false; finished = true; ends++; }
    }
}
