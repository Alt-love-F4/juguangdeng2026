using System;
using Spotlight.Narrative;

namespace Spotlight.FreeCafe
{
    /// <summary>手动配置适配器；只创建身份快照，不推进叙事或结算事件。</summary>
    public sealed class CatalogCustomerIdentitySource : ICafeAvailableCustomerIdentitySource
    {
        private readonly CharacterProfile catalogFallback;
        private readonly System.Collections.Generic.List<CharacterProfile> profiles = new System.Collections.Generic.List<CharacterProfile>();
        public CatalogCustomerIdentitySource(CharacterCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            catalog.Validate();
            catalogFallback = catalog.Find("guest_a");
            foreach (var profile in catalog.Characters)
                if (profile.cafeCustomer) profiles.Add(profile);
        }
        public CafePersonIdentity CreateIdentity(string visitId, int seatIndex)
            => CreateIdentity(visitId, seatIndex, new System.Collections.Generic.HashSet<string>());

        public CafePersonIdentity CreateIdentity(string visitId, int seatIndex, System.Collections.Generic.ISet<string> occupiedPersonIds)
        {
            if (profiles.Count == 0) return null;
            CharacterProfile profile = null;
            for (int i = 0; i < profiles.Count; i++)
            {
                var candidate = profiles[(seatIndex + i) % profiles.Count];
                if (!occupiedPersonIds.Contains(candidate.characterId)) { profile = candidate; break; }
            }
            if (profile == null)
            {
                if (catalogFallback == null) return null;
                return new CafePersonIdentity { personId = "visitor_" + visitId, characterId = catalogFallback.characterId,
                    narrativeCharacterId = catalogFallback.characterId, displayName = catalogFallback.displayName,
                    visualId = catalogFallback.characterId, role = CafePersonRole.Customer };
            }
            return new CafePersonIdentity
            {
                personId = profile.characterId,
                narrativeCharacterId = profile.characterId,
                characterId = profile.characterId,
                displayName = profile.displayName,
                visualId = profile.characterId,
                role = CafePersonRole.Customer
            };
        }
    }
}
