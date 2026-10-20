using System;
using System.Linq;
using Spotlight.Narrative;

namespace Spotlight.FreeCafe
{
    /// <summary>Source-authored order dialogue, shared by tutorial and regular business.</summary>
    public static class NarrativeCafeAmbientContent
    {
        // These two source-authored chats only involve Adele and the player. Other chat
        // entries require guests, weather, newspapers or date gates not owned by this cameo.
        private static readonly string[] adeleChats = { "cafe.ambient.chat.chat-01", "cafe.ambient.chat.chat-16" };

        public static bool TryCreateAdeleChat(NarrativeConfiguration configuration, int rotation,
            out Spotlight.GameFlow.DialogueScript script, Func<string, string> resolver = null)
        {
            script = null;
            if (configuration == null) return false;
            var adele = Spotlight.GameFlow.NarrativeCharacterResolver.CreateDialogueCharacter("adele");
            var player = Spotlight.GameFlow.NarrativeCharacterResolver.CreateDialogueCharacter("player");
            for (int attempt = 0; attempt < adeleChats.Length; attempt++)
            {
                string key = adeleChats[(int)((uint)rotation + (uint)attempt) % adeleChats.Length];
                if (!configuration.openingPresentations.Any(p => p.presentationId == key)) continue;
                var lines = new System.Collections.Generic.List<Spotlight.GameFlow.DialogueLine>();
                bool valid = true;
                foreach (var row in configuration.openingLines.Where(l => l.presentationId == key).OrderBy(l => l.seq))
                {
                    string source = configuration.texts.FirstOrDefault(t => t.key == row.textKey)?.content;
                    if (string.IsNullOrWhiteSpace(source)) continue;
                    int separator = source.IndexOf('：');
                    string name = separator < 0 ? string.Empty : source.Substring(0, separator).Trim();
                    bool isAdele = name == "阿黛尔" || name == "艾黛尔";
                    if (!isAdele && name != "{playername}") { valid = false; break; }
                    string text = resolver?.Invoke(row.textKey) ?? source;
                    text = NarrativeTextSubstitution.PlayerName(text, player.DisplayName);
                    separator = text.IndexOf('：');
                    if (separator >= 0) text = text.Substring(separator + 1).TrimStart();
                    lines.Add(new Spotlight.GameFlow.DialogueLine(isAdele ? Spotlight.GameFlow.DialogueSpeaker.Right
                        : Spotlight.GameFlow.DialogueSpeaker.Left, text,
                        replaceLeft: !isAdele, leftCharacter: !isAdele ? player : null,
                        replaceRight: isAdele, rightCharacter: isAdele ? adele : null,
                        source: new Spotlight.GameFlow.DialogueSourceLocation("OpeningPresentation", null, key, row.lineId, row.seq)));
                }
                if (!valid || lines.Count == 0) continue;
                script = new Spotlight.GameFlow.DialogueScript(lines, player, adele, "闲谈");
                return true;
            }
            return false;
        }

        public static bool TryCreateOrderDialogue(CafeWorld world, WorldEntity customer, out Spotlight.GameFlow.DialogueScript script)
        {
            script = null;
            if (!TryOrder(world, customer, out string text)) return false;
            script = CafeDialoguePlayback.CreateCustomerScript(customer.person, text);
            return script != null;
        }

        public static bool TryOrder(CafeWorld world, WorldEntity customer, out string text)
        {
            text = null;
            var runtime = NarrativeRuntime.Instance;
            var configuration = runtime != null && runtime.IsReady ? runtime.Configuration : null;
            if (world == null || customer == null || configuration == null) return false;
            var request = world.State.requests.FirstOrDefault(r => r.customerId == customer.id);
            if (request == null) return false;
            var templates = configuration.openingPresentations.Where(p => p.presentationId.StartsWith("cafe.ambient.order.", StringComparison.Ordinal))
                .OrderBy(p => p.presentationId, StringComparer.Ordinal).ToArray();
            if (templates.Length == 0) return false;
            uint hash = 2166136261;
            foreach (char c in customer.id ?? request.id) hash = unchecked((hash ^ c) * 16777619);
            string key = templates[hash % (uint)templates.Length].presentationId;
            string body = string.Join("\n", configuration.openingLines.Where(l => l.presentationId == key).OrderBy(l => l.seq)
                .Select(l => runtime.ResolveText(l.textKey)));
            string drink = world.Catalog.products.FirstOrDefault(p => p.id == request.productId)?.label;
            if (string.IsNullOrWhiteSpace(drink) || string.IsNullOrWhiteSpace(body)) return false;
            text = NarrativeTextSubstitution.DrinkName(body, drink)
                .Replace("{饮品名}", drink).Replace("{桌号}", customer.id)
                .Replace("{customerName}", customer.person?.displayName ?? "顾客", StringComparison.OrdinalIgnoreCase);
            return true;
        }
    }
}
