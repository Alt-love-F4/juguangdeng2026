using System;
using UnityEngine;

public static class TextLocalizationService
{
    private static cfg.TbText table;
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
        cfg.TextEntry entry = key != null && table != null ? table.GetOrDefault(key) : null;
        if (entry != null)
        {
            text = entry.Text ?? string.Empty;
            return true;
        }

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
            Debug.LogError("[TextLocalization] Missing Resources/Luban/tbtext.json. Run Assets/ConfigTables/GenerateAll.bat.");
            return;
        }

        try
        {
            table = new cfg.TbText(Newtonsoft.Json.Linq.JArray.Parse(data.text));
        }
        catch (Exception exception)
        {
            Debug.LogError("[TextLocalization] Invalid Luban text data: " + exception.Message);
        }
    }
}
