using System;
using System.Collections.Generic;
using UnityEngine;

// Archived legacy prototype. Active text data comes from Assets/ConfigTables/Data/Text.xlsx.
[CreateAssetMenu(fileName = "TextTable_Archived", menuName = "Archive/Localization/Legacy Text Table")]
public sealed class TextTable : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string key;
        [TextArea(2, 5)] public string text;
        [TextArea(1, 3)] public string comment;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private Dictionary<string, string> lookup;

    public bool TryGetText(string key, out string text)
    {
        BuildLookupIfNeeded();
        return lookup.TryGetValue(key, out text);
    }

    private void BuildLookupIfNeeded()
    {
        if (lookup != null)
            return;

        lookup = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Entry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            if (!lookup.ContainsKey(entry.key))
                lookup.Add(entry.key, entry.text ?? string.Empty);
            else
                Debug.LogWarning($"[TextTable] Duplicate key ignored: {entry.key}", this);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        lookup = null;
    }
#endif
}
