using System.Collections.Generic;
using System.Linq;
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
            try
            {
                var characters=configuration.objects.Where(obj=>obj.objType!=NarrativeObjectType.World).ToArray();
                Assert.That(characters,Is.Not.Empty);
                Assert.That(catalog.Characters.Select(profile=>profile.characterId),Is.EquivalentTo(characters.Select(obj=>obj.objId)));
                foreach(var obj in characters)
                {
                    var profile=catalog.Find(obj.objId);
                    Assert.That(profile.cafeCustomer,Is.EqualTo(obj.objType==NarrativeObjectType.Npc && CharacterCatalog.HasTag(obj.tags,"cafe_customer")),obj.objId);
                    foreach(var path in new[]{obj.portraitPath,obj.portraitIndoorPath,obj.avtarPath})
                        if(!string.IsNullOrWhiteSpace(path)) Assert.That(Resources.Load<Sprite>(path),Is.Not.Null,obj.objId+":"+path);
                    if(!string.IsNullOrWhiteSpace(obj.portraitPath) && string.IsNullOrWhiteSpace(obj.portraitIndoorPath))
                        Assert.That(profile.worldSprite,Is.EqualTo(profile.portrait),obj.objId);
                }
            }
            finally { Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void DefaultIdentitySourceUsesOnlyCurrentlySelectedTableCharacters()
        {
            var catalog=CharacterCatalog.LoadFromTables();
            try
            {
                var selected=catalog.Characters.Where(profile=>profile.cafeCustomer && profile.worldSprite!=null).ToArray();
                var source=new CatalogCustomerIdentitySource(catalog);
                if(selected.Length==0)
                    Assert.That(source.CreateIdentity("order_default",0),Is.Null,"No cafe_customer selection means no named default visitor.");
                for(int i=0;i<selected.Length;i++)
                {
                    var identity=source.CreateIdentity("order_default_"+i,i);
                    Assert.That(identity,Is.Not.Null);
                    Assert.That(identity.characterId,Is.EqualTo(selected[i].characterId));
                }
            }
            finally { Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void SelectedTableCharactersCreateIndependentVisitsAndPersistTheirDefinitions()
        {
            var tableCatalog=CharacterCatalog.LoadFromTables();
            var catalog=ScriptableObject.CreateInstance<CharacterCatalog>();
            try
            {
                var configuration=LubanNarrativeLoader.Load();
                // Explicit test selection from real NPC definitions; production tags remain authoritative for defaults.
                var selected=configuration.objects.Where(obj=>obj.objType==NarrativeObjectType.Npc)
                    .Select(obj=>tableCatalog.Find(obj.objId))
                    .Where(profile=>profile.worldSprite!=null && profile.portrait!=null).Take(2).ToArray();
                Assert.That(selected,Has.Length.EqualTo(2),"Identity isolation needs two sprite-backed table NPCs.");
                catalog.Configure(selected.Select(profile=>new CharacterProfile {
                    characterId=profile.characterId,displayName=profile.displayName,
                    portrait=profile.portrait,worldSprite=profile.worldSprite,cafeCustomer=true
                }));
                catalog.Validate();
                var source=new CatalogCustomerIdentitySource(catalog);
                var a=source.CreateIdentity("order_1",0);
                var b=source.CreateIdentity("order_2",0);
                Assert.That(a,Is.Not.Null);
                Assert.That(b,Is.Not.Null.And.Not.SameAs(a));
                Assert.That(a.personId,Is.EqualTo(selected[0].characterId),"同一个正式角色跨到访保留身份");
                Assert.That(b.personId,Is.EqualTo(a.personId));
                Assert.That(a.characterId,Is.EqualTo(b.characterId));
                Assert.That(a.narrativeCharacterId,Is.EqualTo(a.characterId));
                var next=source.CreateIdentity("order_3",0,new HashSet<string>{a.personId});
                Assert.That(next,Is.Not.Null);
                Assert.That(next.personId,Is.EqualTo(selected[1].characterId));
                var data=JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text);
                data.economy.initialSeats=selected.Length;
                data.economy.baseCustomers=data.economy.maxCustomers=selected.Length;
                var world=new CafeWorld(data,source);
                Assert.That(world.StartDay().success,Is.True);
                Assert.That(world.State.requests,Has.Count.EqualTo(selected.Length));
                var identities=world.State.requests.ToDictionary(request=>request.customerId,
                    request=>JsonUtility.ToJson(world.Find(request.customerId).person));
                Assert.That(world.State.requests.Select(request=>world.Find(request.customerId).person.characterId),
                    Is.EquivalentTo(selected.Select(profile=>profile.characterId)));
                var saved=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
                Assert.That(world.Restore(saved).success,Is.True);
                Assert.That(world.State.requests,Has.Count.EqualTo(selected.Length));
                foreach(var request in world.State.requests)
                {
                    var identity=world.Find(request.customerId).person;
                    Assert.That(JsonUtility.ToJson(identity),Is.EqualTo(identities[request.customerId]));
                    var profile=catalog.Find(identity.characterId);
                    Assert.That(profile,Is.Not.Null);
                    Assert.That(profile.worldSprite,Is.Not.Null);
                    Assert.That(profile.portrait,Is.Not.Null);
                }
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(tableCatalog);
            }
        }
    }
}
