using System;

namespace Spotlight.GameFlow
{
    /// <summary>The caller builds snapshots; the panel owns rendering and view state.</summary>
    public interface IDetailsPanelDataSource
    {
        DetailsPanelData ReadDetails();
    }

    public enum DetailsScrollRegion { Specializations, CharacterIntroduction, WorldSelectors }

    /// <summary>Display contract. Events and the data source are released when the panel closes.</summary>
    public interface IDetailsPanel
    {
        DetailsPanelData CurrentData { get; }
        DetailsPanel.Page CurrentPage { get; }
        string SelectedCharacterId { get; }
        string SelectedWorldProgressKey { get; }
        int RelationshipPageIndex { get; }
        int RelationshipPageCount { get; }
        bool TooltipVisible { get; }
        event Action<DetailsPanelData> DataRefreshed;
        event Action<DetailsPanel.Page> PageChanged;
        event Action<string> CharacterSelected;
        event Action<string> WorldProgressSelected;
        event Action<int> RelationshipPageChanged;
        event Action Closed;
        void Refresh(DetailsPanelData data);
        void BindDataSource(IDetailsPanelDataSource source, bool refreshImmediately = true);
        void UnbindDataSource();
        bool RefreshFromSource();
        void SelectPage(DetailsPanel.Page page);
        bool ShowCharacter(string id);
        void ReturnToRelationships();
        void ChangeRelationshipPage(int delta);
        bool SetRelationshipPage(int index);
        bool SelectWorldProgress(string key);
        bool ScrollTo(DetailsScrollRegion region, float normalizedPosition);
        bool ShowAttributeDescription(DetailsAttributeKind kind);
        bool ShowSpecializationDescription(int index);
        void HideDescription();
        void RequestClose();
    }
}
