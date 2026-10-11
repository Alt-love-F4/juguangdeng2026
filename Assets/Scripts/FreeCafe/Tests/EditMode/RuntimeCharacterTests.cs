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
        public void ObjectResourcesAndIndoorFallbackMatchDesign()
        {
            var configuration=LubanNarrativeLoader.Load();
            var catalog=CharacterCatalog.LoadFromTables();
            Assert.That(configuration.objects.Count,Is.EqualTo(38));
            Assert.That(catalog.Find("adele").cafeCustomer,Is.False);
            Assert.That(catalog.Find("eugene").cafeCustomer,Is.True);
            foreach(var obj in configuration.objects)
            {
                foreach(var path in new[]{obj.portraitPath,obj.portraitIndoorPath,obj.avtarPath})
                    if(!string.IsNullOrWhiteSpace(path)) Assert.That(Resources.Load<Sprite>(path),Is.Not.Null,obj.objId+":"+path);
                if(obj.objType==NarrativeObjectType.Npc && !string.IsNullOrWhiteSpace(obj.portraitPath) && string.IsNullOrWhiteSpace(obj.portraitIndoorPath))
                    Assert.That(catalog.Find(obj.objId).worldSprite,Is.EqualTo(catalog.Find(obj.objId).portrait));
            }
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void DefaultCharactersCreateIndependentVisitsAndPersistTheirDefinitions()
        {
            var catalog=CharacterCatalog.LoadFromTables();
            Assert.That(catalog,Is.Not.Null);
            catalog.Validate();
            var source=new CatalogCustomerIdentitySource(catalog);
            var a=source.CreateIdentity("order_1",0);
            var b=source.CreateIdentity("order_2",0);
            Assert.That(a.personId,Is.EqualTo(b.personId), "同一个正式角色跨到访保留身份");
            Assert.That(a.characterId,Is.EqualTo(b.characterId));
            Assert.That(a.narrativeCharacterId,Is.EqualTo(a.characterId));
            var next=source.CreateIdentity("order_3",0,new System.Collections.Generic.HashSet<string>{a.personId});
            Assert.That(next.personId,Is.Not.EqualTo(a.personId));
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
