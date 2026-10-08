using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Narrative
{
    [Serializable]
    public sealed class NarrativeMapPositionDefinition
    {
        public string positionKey;
        public string nameText;
        public Vector2 position;
        public bool isImportant;
        public string defaultAction;
        public List<string> extendedActions = new List<string>();
        public string imgPath;
        public bool enabled = true;
    }
}
