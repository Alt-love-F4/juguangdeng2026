using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow
{
    public static class NarrativeCharacterResolver
    {
        public static NarrativeCharacterPresentationData Resolve(string key, string displayName, string portraitPath)
            => new NarrativeCharacterPresentationData { key = key, name = displayName, portraitPath = portraitPath };
    }
}
