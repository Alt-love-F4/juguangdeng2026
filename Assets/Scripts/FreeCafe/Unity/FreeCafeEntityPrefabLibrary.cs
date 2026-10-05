using System;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    [Serializable]
    public sealed class FreeCafeEntityPrefabEntry
    {
        public string specId;
        public FreeEntityView prefab;
    }

    /// <summary>Editor-authored prefab registry. Runtime falls back to the generic whitebox prefab.</summary>
    [CreateAssetMenu(menuName="Free Cafe/Entity Prefab Library")]
    public sealed class FreeCafeEntityPrefabLibrary : ScriptableObject
    {
        public FreeCafeEntityPrefabEntry[] entries;

        public FreeEntityView Resolve(string specId)
        {
            if (entries==null) return null;
            foreach (var entry in entries)
                if (entry!=null && entry.specId==specId && entry.prefab!=null) return entry.prefab;
            return null;
        }
    }
}
