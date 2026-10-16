using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    /// <summary>One accepted request per controller/day at the host's real business safe boundary.</summary>
    public sealed class RoseBusinessEntry
    {
        public const string EventId = "rose.1012.s1";
        private NarrativeController observed;
        private int observedDay;
        private bool requested;

        public void Reset()
        {
            observed = null;
            observedDay = 0;
            requested = false;
        }

        public bool TryRequest(NarrativeController controller, int day, bool businessReady)
            => TryRequest(controller, day, businessReady, false);

        public bool TryRetry(NarrativeController controller, int day, bool businessReady)
            => TryRequest(controller, day, businessReady, true);

        private bool TryRequest(NarrativeController controller, int day, bool businessReady, bool explicitRetry)
        {
            if (controller == null || !businessReady || day != 7) return false;
            if (!ReferenceEquals(observed, controller) || observedDay != day)
            {
                observed = controller;
                observedDay = day;
                requested = false;
            }
            if ((!explicitRetry && requested) || controller.IsBusy || !controller.IsSafeBoundary || !controller.CanCaptureSnapshot || controller.IsEnded
                || controller.HasPendingBusinessCheckpoint || controller.HasPendingNightCheckpoint
                || controller.HasPendingServiceCheckpoint
                || !controller.State.TryGetBar("world.calendar", out int calendar) || calendar != day
                || !controller.State.TryGetCondition("story.rose.1011.s1.completed", out bool roseCompleted) || !roseCompleted
                || !controller.State.TryGetCondition("story.church.1009.s2.completed", out bool churchCompleted) || !churchCompleted
                || !controller.State.TryGetCondition("story.rose.1012.s1.completed", out bool completed) || completed
                || controller.State.HasHappenedEvent(EventId)) return false;
            // QueueEvent owns source gates and entry validation; never turn this into a construction preview.
            bool previousRequested = requested;
            requested = true;
            // Leave requested set if dispatch throws: a partially played root needs an explicit retry.
            if (controller.QueueEvent(EventId)) return true;
            requested = previousRequested;
            return false;
        }
    }
}
