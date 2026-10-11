using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class RuntimeCharacterTests
    {
        [Test]
        public void CatalogRejectsDuplicateIds()
        {
            var catalog=ScriptableObject.CreateInstance<CharacterCatalog>();
            try
            {
                Assert.Throws<System.ArgumentException>(()=>catalog.Configure(new[] {
                    new CharacterProfile{characterId="person",displayName="A"},
                    new CharacterProfile{characterId="person",displayName="B"}
                }));
            }
            finally { Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void DefaultCharactersCreateIndependentVisitsAndPersistTheirDefinitions()
        {
            var catalog=Resources.Load<CharacterCatalog>("Characters/CharacterCatalog");
            Assert.That(catalog,Is.Not.Null);
            catalog.Validate();
            var source=new CatalogCustomerIdentitySource(catalog);
            var a=source.CreateIdentity("order_1",0);
            var b=source.CreateIdentity("order_2",0);
            Assert.That(a.personId,Is.Not.EqualTo(b.personId));
            Assert.That(a.characterId,Is.EqualTo(b.characterId));
            Assert.That(a.narrativeCharacterId,Is.Null.Or.Empty);
            var data=JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text);
            var world=new CafeWorld(data,source);
            world.StartDay();
            var saved=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            Assert.That(world.Restore(saved).success,Is.True);
            foreach(var request in world.State.requests)
            {
                var profile=catalog.Find(world.Find(request.customerId).person.characterId);
                Assert.That(profile,Is.Not.Null);
                Assert.That(profile.worldSprite,Is.Not.Null);
                Assert.That(profile.portrait,Is.Not.Null);
            }
        }
    }
}
