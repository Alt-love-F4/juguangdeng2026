using System;
using Spotlight.Narrative;

namespace Spotlight.FreeCafe
{
    /// <summary>普通客流只借用通用客人外观；每次到访有独立身份，不占用剧情角色。</summary>
    public sealed class CatalogCustomerIdentitySource : ICafeAvailableCustomerIdentitySource
    {
        private readonly System.Collections.Generic.List<CharacterProfile> profiles = new System.Collections.Generic.List<CharacterProfile>();
        public CatalogCustomerIdentitySource(CharacterCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            catalog.Validate();
            foreach (string id in new[] { "guest_a", "guest_b", "guest_c", "guest_d" })
            {
                var profile = catalog.Find(id);
                if (profile != null) profiles.Add(profile);
            }
        }
        public CafePersonIdentity CreateIdentity(string visitId, int seatIndex)
            => CreateIdentity(visitId, seatIndex, new System.Collections.Generic.HashSet<string>());

        public CafePersonIdentity CreateIdentity(string visitId, int seatIndex, System.Collections.Generic.ISet<string> occupiedPersonIds)
        {
            if (profiles.Count == 0) return null;
            // The guest_* keys select reusable art, not named NCS actors. No story preset is required.
            var profile = profiles[(seatIndex % profiles.Count + profiles.Count) % profiles.Count];
            return new CafePersonIdentity
            {
                personId = "visitor_" + visitId,
                characterId = profile.characterId,
                displayName = "普通客人",
                visualId = profile.characterId,
                role = CafePersonRole.Customer
            };
        }
    }
}
