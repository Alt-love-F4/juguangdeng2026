using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Ending art is keyed by the stable NCS endingId; it never changes NCS selection or text.</summary>
    [CreateAssetMenu(menuName = "Spotlight/Presentation/Narrative Ending Artwork Catalog")]
    public sealed class NarrativeEndingArtworkCatalog : ScriptableObject
    {
        public enum ArtStatus
        {
            NeedsReview,
            MissingNeedsReview,
            RequiredMissing,
            OptionalMissing,
            NotRequired
        }

        [Serializable]
        public sealed class Entry
        {
            public string endingId;
            public ArtStatus status = ArtStatus.NeedsReview;
            public Sprite artwork;
            public bool artworkIsPlaceholder;
            public string artRequirementId;
            [TextArea(2, 5)] public string notes;
        }

        public const string ResourcesPath = "Dialogue/NarrativeEndingArtworkCatalog";
        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;
        public Entry Find(string endingId) => string.IsNullOrWhiteSpace(endingId)
            ? null : entries.Find(entry => entry != null && string.Equals(entry.endingId, endingId, StringComparison.Ordinal));

        public static NarrativeEndingArtworkCatalog Load() => Resources.Load<NarrativeEndingArtworkCatalog>(ResourcesPath);

        public static string StatusLabel(ArtStatus status)
        {
            switch (status)
            {
                case ArtStatus.MissingNeedsReview: return "结局图需求待确认";
                case ArtStatus.RequiredMissing: return "结局插画素材待补";
                case ArtStatus.OptionalMissing: return "未配置结局插画（可选）";
                case ArtStatus.NotRequired: return "本结局不需要专属插画";
                default: return "结局插画画面需求待确认";
            }
        }

#if UNITY_EDITOR
        public void SynchronizeEnabledEndings(IEnumerable<string> endingIds)
        {
            if (endingIds == null) throw new ArgumentNullException(nameof(endingIds));
            if (entries == null) entries = new List<Entry>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string endingId in endingIds)
            {
                if (string.IsNullOrWhiteSpace(endingId) || !unique.Add(endingId)) continue;
                if (Find(endingId) == null)
                {
                    entries.Add(new Entry
                    {
                        endingId = endingId,
                        status = ArtStatus.NeedsReview,
                        notes = "NCS结局已启用，但尚未登记专属结局图。请先确认画面需求；此记录不是已批准的美术订单。"
                    });
                }
            }
        }
#endif
    }
}
