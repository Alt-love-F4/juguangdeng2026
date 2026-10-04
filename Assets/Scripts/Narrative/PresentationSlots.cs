using System;
using System.Collections.Generic;
using System.Globalization;

namespace Spotlight.Narrative
{
    public static class PresentationSlots
    {
        public static Dictionary<string, string> Parse(NarrativeSetupParamsDefinition setup)
        {
            var slots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string entry in (setup?.slotValues ?? string.Empty).Split(','))
            {
                int equals = entry.IndexOf('=');
                if (equals <= 0) continue;
                slots[entry.Substring(0, equals).Trim()] = entry.Substring(equals + 1).Trim();
            }
            if (setup?.parameters != null)
                foreach (NarrativeAtomParameter parameter in setup.parameters)
                    if (parameter != null && !string.IsNullOrWhiteSpace(parameter.slot)) slots[parameter.slot.Trim()] = parameter.value ?? string.Empty;
            return slots;
        }

        public static float Number(string value, float fallback = 0f)
            => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) ? number : fallback;

        public static string[] Sequence(string value)
            => (value ?? string.Empty).Replace("->", ",").Split(new[] { ',', ';', ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }
}
