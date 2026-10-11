using System;
using Spotlight.Narrative;

namespace Spotlight.FreeCafe
{
    /// <summary>手动配置适配器；只创建身份快照，不推进叙事或结算事件。</summary>
    public sealed class CatalogCustomerIdentitySource : ICafeCustomerIdentitySource
    {
        private readonly CharacterCatalog catalog;
        public CatalogCustomerIdentitySource(CharacterCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            catalog.Validate();
        }
        public CafePersonIdentity CreateIdentity(string visitId, int seatIndex)
        {
            var profiles = catalog.Characters;
            if (profiles.Count == 0) return null;
            // 座位只选择外观模板；实例身份来自到访 ID，不将座位误用作人物 ID。
            var profile = profiles[seatIndex % profiles.Count];
            return new CafePersonIdentity
            {
                personId = "visitor_" + visitId,
                characterId = profile.characterId,
                displayName = profile.displayName,
                visualId = profile.characterId,
                role = CafePersonRole.Customer
            };
        }
    }
}
