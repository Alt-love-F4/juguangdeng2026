using System;

namespace Spotlight.Narrative
{
    public static class NarrativeTextSubstitution
    {
        public static string PlayerName(string text, string playerName)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(playerName)) return text;
            return text.Replace("{playername}", playerName, StringComparison.OrdinalIgnoreCase)
                .Replace("｛playername}", playerName, StringComparison.OrdinalIgnoreCase)
                .Replace("｛playername｝", playerName, StringComparison.OrdinalIgnoreCase);
        }

        public static string DrinkName(string text, string drinkName)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(drinkName)) return text;
            return text.Replace("{drinkname}", drinkName, StringComparison.OrdinalIgnoreCase)
                .Replace("｛drinkname}", drinkName, StringComparison.OrdinalIgnoreCase)
                .Replace("｛drinkname｝", drinkName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
