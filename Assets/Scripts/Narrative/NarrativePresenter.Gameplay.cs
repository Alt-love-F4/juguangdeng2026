using System;
using System.Collections;

namespace Spotlight.Narrative
{
    public sealed partial class NarrativePresenter
    {
        public Func<NarrativeEventRequest, NarrativeGameplayPresentationData, Action<NarrativeGameplayResult>, IDisposable> GameplayPlayback { get; set; }
        public event Action<string, bool> GameplayCompleted;
        public bool IsAwaitingGameplay => activeGameplay != null && !activeGameplay.Disposed;

        // One stable wrapper per presentation: Guarded captures it before gameplay temporarily releases
        // the underlying registry token. A resumed token must not leak or dispose another owner's pause.
        private sealed class PresenterPauseLease : IDisposable
        {
            private IDisposable current;
            private bool disposed;
            public void Suspend() { var token = current; current = null; token?.Dispose(); }
            public void Resume()
            {
                if (!disposed && current == null) current = Spotlight.GameFlow.PresentationPauseRegistry.Acquire("NarrativePresenter");
            }
            public void Dispose() { if (disposed) return; disposed = true; Suspend(); }
        }

        private sealed class GameplayOperation : IDisposable
        {
            private IDisposable handle;
            public bool Disposed { get; private set; }
            public bool Received, Passed, Cancelled;
            public string Error;
            public void SetHandle(IDisposable value)
            {
                if (Disposed) value?.Dispose();
                else handle = value;
            }
            public void Dispose()
            {
                if (Disposed) return;
                Disposed = true;
                var old = handle; handle = null;
                old?.Dispose();
            }
        }
        private GameplayOperation activeGameplay;

        private void CancelGameplay()
        {
            var pending = activeGameplay;
            activeGameplay = null;
            if (pending == null) return;
            try { pending.Dispose(); } catch (Exception error) { UnityEngine.Debug.LogException(error); }
        }

        private void ReleaseGameplayVisuals(ContinuityStage stage)
        {
            // Do not end the batch or touch any other owner's pause/visual state.
            suspendActiveLetter = null;
            var conversation = stage.ConversationRelease; stage.ConversationRelease = null;
            var steps = stage.StepReleases.ToArray(); stage.StepReleases.Clear();
            InvokeStageRelease(conversation);
            foreach (var release in steps) InvokeStageRelease(release);
            resources.RestoreCamera();
            resources.StopAudio();
            ui.Hide();
        }

        private IEnumerator GameplayStep(NarrativeEventDefinition definition, NarrativeConversationPageDefinition page, ContinuityStage stage)
        {
            if (GameplayPlayback == null) throw new InvalidOperationException("Gameplay playback is not connected: " + page.pageId);
            if (string.IsNullOrWhiteSpace(page.gameplayKey)) throw new InvalidOperationException("Gameplay key is missing: " + page.pageId);
            long version = sessionVersion;
            var pending = new GameplayOperation();
            activeGameplay = pending;
            try
            {
                ReleaseGameplayVisuals(stage);
                if (disposed || sessionVersion != version) yield break;
                pauseOwner?.Suspend();
                IDisposable handle = GameplayPlayback(request, new NarrativeGameplayPresentationData
                {
                    eventId = definition.eventId, pageId = page.pageId, gameplayKey = page.gameplayKey, continuityOwner = stage.Owner
                }, result =>
                {
                    if (disposed || sessionVersion != version || !ReferenceEquals(activeGameplay, pending) || pending.Disposed || pending.Received) return;
                    pending.Received = true;
                    if (result == null) pending.Error = "Gameplay returned a null result: " + page.pageId;
                    else { pending.Passed = result.passed; pending.Cancelled = result.cancelled; }
                });
                pending.SetHandle(handle); // Also disposes handles returned after synchronous cancellation.
                if (handle == null) throw new InvalidOperationException("Gameplay host rejected request: " + page.pageId);
                while (!pending.Received && !disposed && sessionVersion == version) yield return null;
                if (disposed || sessionVersion != version) yield break;
                if (pending.Error != null) throw new InvalidOperationException(pending.Error);
                if (pending.Cancelled) { AbortPresentation("gameplay_cancelled"); yield break; }
                // Dispose while callbacks are still latched, then reacquire BEFORE publishing local state.
                pending.Dispose();
                if (disposed || sessionVersion != version) yield break;
                pauseOwner?.Resume();
                GameplayCompleted?.Invoke(page.pageId, pending.Passed);
            }
            finally
            {
                if (ReferenceEquals(activeGameplay, pending)) activeGameplay = null;
                // Iterator disposal during StopAllCoroutines must not strand the pause lease
                // if a host's cancellation handle itself throws.
                try { pending.Dispose(); } catch (Exception error) { UnityEngine.Debug.LogException(error); }
            }
        }
    }
}
