namespace Spotlight.GameFlow
{
    /// <summary>Display-only integration contract. The caller owns real gameplay data and persistence.</summary>
    public interface IDetailsPanel
    {
        DetailsPanelData CurrentData { get; }
        DetailsPanel.Page CurrentPage { get; }
        void Refresh(DetailsPanelData data);
        void SelectPage(DetailsPanel.Page page);
        void RequestClose();
    }
}
