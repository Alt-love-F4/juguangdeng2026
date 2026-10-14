using System;
using Spotlight.Narrative;
using UnityEngine;
using YokiFrame;

namespace Spotlight.GameFlow
{
    public static class NarrativeNoticeEventPlayback
    {
        public static bool Play(NarrativeEventRequest request, NarrativeNoticePresentationData data, Action completed)
        {
            if (data == null) return false;
            NoticePanel panel = UIKit.OpenPanel<NoticePanel>(UILevel.Pop, tag: "narrative-notice", cachePolicy: PanelCachePolicy.Reusable);
            try
            {
                var notice = new NoticeData(data.body, data.title, data.time, LoadImage(data.imageKey),
                    noticeId: data.presentationKey, titleKey: data.titleKey, timeKey: data.timeKey,
                    imageKey: data.imageKey, textKey: data.textKey, barConds: data.barConds);
                bool notified = false;
                void Complete() { if (notified) return; notified = true; completed?.Invoke(); }
                panel.Play(notice, Complete, Complete);
                panel.PlaybackStopped += Complete;
                return true;
            }
            catch
            {
                UIKit.ClosePanel(panel);
                throw;
            }
        }

        public static Texture LoadImage(string resourcePath)
            => string.IsNullOrWhiteSpace(resourcePath) ? null : Resources.Load<Texture>(resourcePath);
    }
}
