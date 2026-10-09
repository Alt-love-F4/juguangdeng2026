using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.GameFlow
{
    // Presentation fields missing from the current NCS tables; keyed by their stable IDs.
    [CreateAssetMenu(menuName = "Spotlight/Details Display Config")]
    public sealed class DetailsDisplayConfig : ScriptableObject
    {
        public Sprite maleAvatar;
        public Sprite femaleAvatar;
        public Sprite unknownAvatar;
        public List<Character> characters = new List<Character>();
        public List<Progress> progress = new List<Progress>();
        public List<Specialization> specializations = new List<Specialization>();
        [Serializable] public sealed class Character
        {
            public string objectId;
            public Sprite avatar;
            public bool initiallyUnlocked;
        }
        [Serializable] public sealed class Progress
        {
            public string barKey;
            [TextArea] public string description;
            public string lowerDescription;
            public string upperDescription;
            public bool initiallyVisible = true;
        }
        [Serializable] public sealed class Specialization
        {
            public string id;
            public string displayName;
            public Sprite icon;
            [TextArea] public string description;
        }
    }
}
