using System;
using System.Collections.Generic;
using UnityEngine;

public static class TextLocalizationService
{
    [Serializable]
    private sealed class Row
    {
        public string key;
        public string text;
        public string comment;
    }

    [Serializable]
    private sealed class Document
    {
        public Row[] entries;
    }

    private static Dictionary<string, string> table;
    private static bool loadAttempted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        table = null;
        loadAttempted = false;
    }

    public static bool TryGetText(string key, out string text)
    {
        EnsureTableLoaded();
        if (key != null && table != null && table.TryGetValue(key, out text))
            return true;

        text = string.Empty;
        return false;
    }

    private static void EnsureTableLoaded()
    {
        if (loadAttempted)
            return;

        loadAttempted = true;
        TextAsset data = Resources.Load<TextAsset>("Luban/tbtext");
        if (data == null)
        {
            Debug.LogError("[TextLocalization] Missing Resources/Luban/tbtext.json. Run Assets/ConfigTables/GenerateText.bat.");
            return;
        }

        try
        {
            // Luban exports a root array; JsonUtility requires an object wrapper.
            Document document = JsonUtility.FromJson<Document>("{\"entries\":" + data.text + "}");
            if (document == null || document.entries == null)
                throw new FormatException("Expected a JSON array of text rows.");

            var loaded = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Row row in document.entries)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.key))
                    throw new FormatException("Text row has an empty key.");
                loaded.Add(row.key, row.text ?? string.Empty);
            }
            table = loaded;
        }
        catch (Exception exception)
        {
            Debug.LogError("[TextLocalization] Invalid Luban text data: " + exception.Message);
        }
    }
}
