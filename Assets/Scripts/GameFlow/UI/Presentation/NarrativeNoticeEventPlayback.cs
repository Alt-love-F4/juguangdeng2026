using System;
using Spotlight.Narrative;
using UnityEngine;
using YokiFrame;

namespace Spotlight.GameFlow
{
    public static class NarrativeNoticeEventPlayback
    {
        public static bool Play(NarrativeEventRequest request, NarrativeNoticePresentationData data, Action completed)
            => Play(request, data, completed, "narrative-notice", PanelCachePolicy.Reusable);

        public static bool Play(NarrativeEventRequest request, NarrativeNoticePresentationData data, Action completed,
            string panelTag, PanelCachePolicy cachePolicy)
        {
            if (data == null) return false;
            NoticePanel existing = UIKit.GetPanel<NoticePanel>();
            if (existing != null && existing.State != PanelState.Close && existing.State != PanelState.Cached
                && existing.State != PanelState.Preloaded
                && !string.Equals(existing.Tag, panelTag, StringComparison.Ordinal))
                throw new InvalidOperationException("A notice panel is already open in another scope.");
            NoticePanel panel = UIKit.OpenPanel<NoticePanel>(UILevel.Pop, tag: panelTag, cachePolicy: cachePolicy);
            bool notified = false;
            // Stop precedes CloseSelf and the finish/cancel callback. Only the latter may advance the event.
            void Stopped() => panel.PlaybackStopped -= Stopped;
            void Complete()
            {
                if (notified) return;
                notified = true;
                panel.PlaybackStopped -= Stopped;
                UIKit.ClosePanel(panel);
                completed?.Invoke();
            }
            try
            {
                var notice = new NoticeData(data.body, data.title, data.time, LoadImage(data.imageKey),
                    noticeId: data.presentationKey, titleKey: data.titleKey, timeKey: data.timeKey,
                    imageKey: data.imageKey, textKey: data.textKey, barConds: data.barConds);
                panel.Play(notice, Complete, Complete);
                if (!notified) panel.PlaybackStopped += Stopped;
                return true;
            }
            catch
            {
                panel.PlaybackStopped -= Stopped;
                UIKit.ClosePanel(panel);
                throw;
            }
        }

        public static Texture LoadImage(string resourcePath)
            => string.IsNullOrWhiteSpace(resourcePath) ? null : Resources.Load<Texture>(resourcePath);
    }
}
