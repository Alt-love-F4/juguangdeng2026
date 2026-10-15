using System;
using UnityEngine;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>业务侧入口：数据可来自任意 IDialogueScriptSource，UI 实例/资源由 UIKit 管理。</summary>
    public static class DialoguePlayback
    {
        public static DialoguePanel Play(IDialogueScriptSource source, Action onFinished = null, Action onCancelled = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return Play(source.CreateScript(), onFinished, onCancelled);
        }

        public static DialoguePanel Play(DialogueScript script, Action onFinished = null, Action onCancelled = null)
        {
            if (script == null) throw new ArgumentNullException(nameof(script));
            if (DialoguePanel.IsAnyPlaying) throw new InvalidOperationException("A dialogue is already playing. Stop it before replacing it.");
            DialoguePanel panel = UIKit.GetPanel<DialoguePanel>();
            if (panel == null || !panel.SeamlessChoicePending)
                panel = UIKit.OpenPanel<DialoguePanel>(UILevel.Pop, tag: "character-dialogue",
                    cachePolicy: PanelCachePolicy.Reusable);
            try
            {
                panel.Play(script, () =>
                {
                    if (!panel.SeamlessChoicePending) UIKit.ClosePanel(panel);
                    onFinished?.Invoke();
                }, onCancelled);
                if (!string.IsNullOrWhiteSpace(script.MusicKey)) Spotlight.Audio.GameAudio.OverrideMusic(panel, script.MusicKey);
                return panel;
            }
            catch
            {
                UIKit.ClosePanel(panel);
                throw;
            }
        }

        public static DialoguePanel PlayPreview(DialogueScript script, Action onFinished = null, Action onCancelled = null)
        {
            if (script == null) throw new ArgumentNullException(nameof(script));
            return Play(script.RebuildAt(0), onFinished, onCancelled);
        }

        /// <summary>Release only the stage belonging to this completed/cancelled event.</summary>
        public static void ReleaseStage(object owner)
        {
            var panel = UIKit.GetPanel<DialoguePanel>();
            if (panel != null && panel.OwnsContinuity(owner)) Stop();
        }

        /// <summary>Read a selected NCS opening without queueing/settling an NCS event.</summary>
        public static DialoguePanel PlayNcsOpening(
            string presentationId = NarrativePresentationDataAdapter.DefaultOpeningPresentationId,
            Action onFinished = null, Action onCancelled = null)
            => Play(new LubanOpeningDialogueSource(presentationId), onFinished, onCancelled);

        public static void Stop()
        {
            DialoguePanel panel = UIKit.GetPanel<DialoguePanel>();
            if (panel == null) return;
            panel.Stop();
            UIKit.ClosePanel(panel);
        }
    }
}
