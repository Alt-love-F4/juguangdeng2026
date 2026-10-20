using System;
using Spotlight.FreeCafe;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    // Explicit authored participants: never infer a customer from the last speaker (often the player).
    public static class NarrativeCafeCustomers
    {
        public static CafePersonIdentity[] Resolve(string pageId, NarrativeConfiguration config)
        {
            string[] ids;
            switch (pageId)
            {
                case "rose.1011.s1.brew_gate": ids = new[] { "sylvain" }; break;
                case "rose.1019.s1.brew_gate": ids = new[] { "sylvain", "sylvain" }; break;
                case "rose.1024.s1.brew_gate": case "rose.1024.s2.brew_gate": ids = new[] { "bertrand" }; break;
                case "rose.1016.s1.brew_gate": ids = new[] { "alienor", "sade" }; break;
                case "rose.1103.s1.brew_gate": ids = new[] { "alienor" }; break;
                case "church.1014.s2.serve.thibaut": ids = new[] { "thibaut" }; break;
                case "church.1105.s1.serve.duke": case "church.1105.s2.serve.duke": ids = new[] { "elion" }; break;
                case "church.1111.s1.serve.lucien": ids = new[] { "lucien" }; break;
                case "church.route.1113.s1.taste.lefebvre": ids = new[] { "lefebvre" }; break;
                default: return null;
            }
            return Array.ConvertAll(ids, id =>
            {
                var obj = config.objects.Find(o => o.objId == id);
                if (obj == null) throw new InvalidOperationException("Missing narrative customer: " + id);
                string name = config.texts.Find(t => t.key == obj.nameText)?.content ?? id;
                // Preserve identities concealed by the source dialogue.
                if (id == "bertrand") name = "男孩";
                if (id == "thibaut") name = "客人";
                if (id == "alienor" && pageId == "rose.1016.s1.brew_gate") name = "埃莉诺";
                return new CafePersonIdentity { personId = "story:" + id, characterId = id,
                    narrativeCharacterId = id, displayName = name, visualId = id, role = CafePersonRole.Customer };
            });
        }
    }
}
