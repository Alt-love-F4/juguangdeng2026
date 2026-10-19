using System;
using UnityEngine;
using YokiFrame;

namespace Spotlight.GameFlow
{
    public interface IGameFlowPanelData : IUIData
    {
        GameFlowBootstrap Flow { get; }
    }

    public sealed class GameFlowPanelData : IGameFlowPanelData
    {
        public GameFlowPanelData(GameFlowBootstrap flow, bool fromGameplay = false)
        {
            Flow = flow;
            FromGameplay = fromGameplay;
        }

        public GameFlowBootstrap Flow { get; }
        public bool FromGameplay { get; }
        public bool CanSave => Flow == null || Flow.CanSaveCurrentSession;
    }

    public sealed class ConfirmationPanelData : IGameFlowPanelData
    {
        public ConfirmationPanelData(GameFlowBootstrap flow, string title, string message,
            string acceptLabel, Action accepted, string cancelLabel, Action cancelled)
        {
            Flow = flow;
            Title = title;
            Message = message;
            AcceptLabel = acceptLabel;
            Accepted = accepted;
            CancelLabel = cancelLabel;
            Cancelled = cancelled;
        }

        public GameFlowBootstrap Flow { get; }
        public string Title { get; }
        public string Message { get; }
        public string AcceptLabel { get; }
        public Action Accepted { get; }
        public string CancelLabel { get; }
        public Action Cancelled { get; }
    }

    public sealed class ResultPanelData : IGameFlowPanelData
    {
        public ResultPanelData(GameFlowBootstrap flow, string title, string message, Action closed, string closeLabel = "关闭", string endingId = null,
            Sprite endingArtwork = null, bool endingArtworkIsPlaceholder = false, string endingArtworkStatus = null, string endingEcho = null)
        {
            Flow = flow;
            Title = title;
            Message = message;
            Closed = closed;
            CloseLabel = closeLabel;
            EndingId = endingId;
            EndingArtwork = endingArtwork;
            EndingArtworkIsPlaceholder = endingArtworkIsPlaceholder;
            EndingArtworkStatus = endingArtworkStatus ?? string.Empty;
            EndingEcho = endingEcho ?? string.Empty;
        }

        public GameFlowBootstrap Flow { get; }
        public string Title { get; }
        public string Message { get; }
        public Action Closed { get; }
        public string EndingId { get; }
        public bool IsEnding => !string.IsNullOrWhiteSpace(EndingId);
        public string CloseLabel { get; }
        public Sprite EndingArtwork { get; }
        public bool EndingArtworkIsPlaceholder { get; }
        public string EndingArtworkStatus { get; }
        public string EndingEcho { get; }
    }
}
