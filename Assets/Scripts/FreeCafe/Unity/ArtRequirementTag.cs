using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>Every whitebox world entity/background/UI group carries its art contract in the Inspector.</summary>
    public sealed class ArtRequirementTag : MonoBehaviour
    {
        public string assetId;
        public CafeArtLibrary library;
        public string dimensions;
        [TextArea(4,12)] public string prompt;
        [TextArea(2,6)] public string negativePrompt;
        public void Bind(CafeArtLibrary source,string id)
        {
            library=source; assetId=id;
            var entry=source==null ? null : source.Find(id); if(entry==null) return;
            dimensions=entry.brief.width+" × "+entry.brief.height+" | "+(entry.brief.transparent ? "transparent" : "opaque")+" | pivot "+entry.brief.pivot;
            prompt=entry.brief.prompt; negativePrompt=entry.brief.negativePrompt;
        }
    }
}
