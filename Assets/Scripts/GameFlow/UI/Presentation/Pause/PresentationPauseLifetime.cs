using System;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Releases a non-panel presenter if its coroutine host is disabled or destroyed.</summary>
    public sealed class PresentationPauseLifetime : MonoBehaviour
    {
        public Action Interrupted;
        private void OnDisable() => Interrupted?.Invoke();
        private void OnDestroy() { Interrupted?.Invoke(); Interrupted = null; }
    }
}
