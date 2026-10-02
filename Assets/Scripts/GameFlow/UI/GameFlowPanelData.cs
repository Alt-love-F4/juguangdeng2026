using System;
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
        public ResultPanelData(GameFlowBootstrap flow, string title, string message, Action closed)
        {
            Flow = flow;
            Title = title;
            Message = message;
            Closed = closed;
        }

        public GameFlowBootstrap Flow { get; }
        public string Title { get; }
        public string Message { get; }
        public Action Closed { get; }
    }
}
