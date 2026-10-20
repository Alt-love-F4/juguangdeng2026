using System;
using Spotlight.FreeCafe;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    /// <summary>Owns a single host request, including any pending scene-arrival callback.</summary>
    internal sealed class NarrativeCafeGameplayBridgeSession : IDisposable
    {
        private Action<NarrativeGameplayResult> completion;
        private Action<NarrativeCafeGameplayBridgeSession> released;
        private NarrativeCafeGameplayBridge observation;
        private readonly string ownerKey;
        public CafeWorld World { get; private set; }
        public bool IsDisposed { get; private set; }
        public bool IsReady => !IsDisposed && observation != null;
        public string GameplayHint => observation?.GameplayHint;

        public NarrativeCafeGameplayBridgeSession(Action<NarrativeGameplayResult> completion,
            Action<NarrativeCafeGameplayBridgeSession> released, string ownerKey = null)
        {
            this.completion = completion;
            this.released = released;
            this.ownerKey = string.IsNullOrWhiteSpace(ownerKey) ? "narrative-cafe:" + Guid.NewGuid().ToString("N") : ownerKey;
        }

        public bool Attach(CafeWorld world, string key, out string error, System.Collections.Generic.IReadOnlyList<CafePersonIdentity> customers = null)
        {
            if (IsDisposed) { error = "Narrative cafe request was cancelled before scene arrival."; return false; }
            if (observation != null) { error = "Narrative cafe request already owns a world observation."; return false; }
            if (!NarrativeCafeGameplayBridge.TryStart(world, ownerKey, key, Complete, out var handle, out error, customers)) return false;
            if (IsDisposed)
            {
                handle.Dispose();
                error = "Narrative cafe request was completed or cancelled during order reservation.";
                return false;
            }
            World = world;
            observation = handle;
            return true;
        }

        public void Cancel() => Complete(new NarrativeGameplayResult { cancelled = true });

        public void CompleteHostCommand() => Complete(new NarrativeGameplayResult { passed = true });

        private void Complete(NarrativeGameplayResult result)
        {
            if (IsDisposed) return;
            var callback = completion;
            Dispose();
            callback?.Invoke(result);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            var handle = observation;
            observation = null;
            completion = null;
            handle?.Dispose();
            var onReleased = released;
            released = null;
            onReleased?.Invoke(this);
        }
    }
}
