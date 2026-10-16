using System;
using System.Collections.Generic;
using Spotlight.Narrative;
using UnityEngine;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>Ordinary owner-scoped reading over a retained conversation stage. No NCS settlement or letter flow.</summary>
    public static class ConversationReadingPlayback
    {
        private const string PanelTag = "conversation-reading";
        private static Session active;

        public static bool Play(NarrativeEventRequest request, NarrativeConversationReadingPresentationData data, Action completed)
            => PlayInternal(data, completed, false);

        public static bool PlayPreview(NarrativeEventRequest request, NarrativeConversationReadingPresentationData data, Action completed)
            => PlayInternal(data, completed, true);

        public static bool PlayPreview(NarrativeEventRequest request, NarrativeConversationReadingPresentationData data,
            Action completed, string tag, PanelCachePolicy cachePolicy)
            => PlayInternal(data, completed, true, tag, cachePolicy);

        public static ReadingScript CreateScript(NarrativeConversationReadingPresentationData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.lines.Count == 0) throw new ArgumentException("Conversation reading requires lines.", nameof(data));
            var lines = new List<ReadingLine>(data.lines.Count);
            foreach (var item in data.lines)
            {
                if (item == null) throw new ArgumentException("Conversation reading lines cannot be null.", nameof(data));
                lines.Add(new ReadingLine(item.text, item.speakerName, item.textKey, item.speakerKey, item.voiceKey,
                    item.portrait != null ? item.portrait : string.IsNullOrWhiteSpace(item.portraitPath) ? null : Resources.Load<Sprite>(item.portraitPath),
                    string.Equals(item.mode, "dialogue", StringComparison.OrdinalIgnoreCase) ? ReadingLineKind.Dialogue : ReadingLineKind.Narration));
            }
            var image = string.IsNullOrWhiteSpace(data.imagePath) ? null : Resources.Load<Texture>(data.imagePath);
            return new ReadingScript(new[] { new ReadingPage(data.title, lines, image,
                pageId: data.readingPageId, seq: 1, titleKey: data.titleKey, imageKey: data.imagePath) },
                data.title, data.titleKey);
        }

        private static bool PlayInternal(NarrativeConversationReadingPresentationData data, Action completed, bool preview,
            string tag = PanelTag, PanelCachePolicy cachePolicy = PanelCachePolicy.Reusable)
        {
            if (data == null || data.continuityOwner == null || data.lines.Count == 0 || active != null) return false;
            ReadingPanel existing = UIKit.GetPanel<ReadingPanel>();
            if (existing != null && (existing.IsPlaying || existing.State != PanelState.Close
                && existing.State != PanelState.Cached && existing.State != PanelState.Preloaded)) return false;
            // Resolve/copy everything before opening the shared prefab. Panel never reads mutable NCS rows.
            ReadingScript script = CreateScript(data);
            ReadingPanel panel = UIKit.OpenPanel<ReadingPanel>(UILevel.Pop, tag: tag, cachePolicy: cachePolicy);
            var session = new Session(panel, data.continuityOwner, completed, data.cancelled, data.playbackStopped);
            active = session;
            data.releaseStage = session.Release;
            data.setSuspended = session.SetSuspended;
            try
            {
                DialoguePanel stage = UIKit.GetPanel<DialoguePanel>();
                if (stage != null && stage.OwnsContinuity(data.continuityOwner)) session.stagePause = stage.AcquireReadingPause(session);
                panel.Play(script, session.Complete, session.Cancel);
                // Play starts by stopping the prior script; subscribe only after that initialization stop.
                panel.AllowCancel = preview || data.allowCancel;
                panel.IsSuspended = false;
                panel.PlaybackStopped += session.Stopped;
                return true;
            }
            catch { session.Release(); throw; }
        }

        public static void ReleaseStage(object owner)
        {
            if (active != null && ReferenceEquals(active.Owner, owner)) active.Release();
        }

        private sealed class Session
        {
            public object Owner { get; }
            public IDisposable stagePause;
            private readonly ReadingPanel panel;
            private readonly Action completed, cancelled, playbackStopped;
            private bool terminal, released, stopped;
            public Session(ReadingPanel panel, object owner, Action completed, Action cancelled, Action playbackStopped)
            {
                this.panel = panel; Owner = owner;
                this.completed = completed; this.cancelled = cancelled; this.playbackStopped = playbackStopped;
            }
            private bool Current => !released && ReferenceEquals(active, this);
            public void Stopped()
            {
                if (!Current || terminal || stopped) return;
                stopped = true;
                panel.PlaybackStopped -= Stopped;
                playbackStopped?.Invoke(); // Presenter distinguishes same-stack terminal callback from owner loss.
            }
            public void Complete()
            {
                if (!Current || terminal) return;
                if (stopped && panel != null && panel.IsPlaying) { Release(); return; }
                terminal = true;
                Release();
                completed?.Invoke();
            }
            public void Cancel()
            {
                if (!Current || terminal) return;
                if (stopped && panel != null && panel.IsPlaying) { Release(); return; }
                terminal = true;
                Release();
                cancelled?.Invoke();
            }
            public void SetSuspended(bool value)
            {
                if (Current && !stopped && panel != null) panel.IsSuspended = value;
            }
            public void Release()
            {
                if (released) return;
                released = true;
                bool owned = ReferenceEquals(active, this);
                if (owned) active = null;
                if (panel != null) panel.PlaybackStopped -= Stopped;
                stagePause?.Dispose();
                stagePause = null;
                if (!owned || panel == null) return;
                // A Stop subscriber may have already installed a new legacy reading. Never close its panel.
                if (stopped && panel.IsPlaying) return;
                panel.Stop();
                if (panel.IsPlaying) return;
                panel.AllowCancel = true;
                panel.IsSuspended = false;
                if (panel.State != PanelState.Close && panel.State != PanelState.Cached && panel.State != PanelState.Preloaded)
                    UIKit.ClosePanel(panel);
            }
        }
    }
}
