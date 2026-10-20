using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Dialogue-only sizing. Visible rects are baked from alpha by the editor, never read back at runtime.</summary>
    [CreateAssetMenu(menuName = "Spotlight/Dialogue/Portrait Sizing", fileName = "DialoguePortraitSizing")]
    public sealed class DialoguePortraitSizing : ScriptableObject
    {
        [Tooltip("Visible height relative to the portrait slot height. Width is not constrained.")]
        [Range(.1f, 1.5f)] public float heightRatio = .88f;
        public List<Entry> entries = new List<Entry>();

        [Serializable]
        public sealed class Entry
        {
            public string characterId;
            public string displayName;
            public Sprite sprite;
            [Tooltip("Alpha bounds in normalized sprite coordinates; baked in Edit Mode.")]
            public Rect visibleRect = new Rect(0, 0, 1, 1);
            [Min(.01f)] public float heightScale = 1f;
            [Tooltip("Offset in portrait-slot UI pixels, after normalization.")]
            public Vector2 offset;
        }

        public Entry Find(string characterId, Sprite sprite)
        {
            Entry fallback = null;
            foreach (var entry in entries)
            {
                if (entry == null || entry.sprite != sprite) continue;
                if (entry.characterId == characterId) return entry;
                if (fallback == null) fallback = entry;
            }
            return fallback;
        }
    }
}
