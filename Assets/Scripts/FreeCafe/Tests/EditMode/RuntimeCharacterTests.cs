using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class RuntimeCharacterTests
    {
        private static readonly string[] FormalCharacterIds =
        {
            "laurent", "clovis", "renaud", "elion", "alienor", "queen", "bertrand", "sade",
            "mamion", "lucien", "lesou", "sylvain", "antoine", "lefebvre", "duval", "adele",
            "emmanuel", "sparrow", "thibaut", "octave", "eugene", "colette", "pascal", "marie",
            "guest_a", "guest_b", "guest_c", "guest_d", "soldier_a", "soldier_b", "soldier_c", "soldier_d"
        };

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
        public void FormalCharacterResourcesMatchDesign()
        {
            var configuration=LubanNarrativeLoader.Load();
            var catalog=CharacterCatalog.LoadFromTables();
            try
            {
                Assert.That(FormalCharacterIds.Length,Is.EqualTo(32));
                Assert.That(FormalCharacterIds,Is.Unique);
                foreach(var id in FormalCharacterIds)
                {
                    var obj=configuration.objects.Find(row=>row.objId==id);
                    Assert.That(obj,Is.Not.Null,id+": Object row");
                    Assert.That(obj.portraitPath,Is.EqualTo("Characters/"+id+"/neutral"),id);
                    Assert.That(obj.avtarPath,Is.EqualTo("Characters/"+id+"/avatar"),id);
                    Assert.That(obj.portraitIndoorPath,Is.EqualTo("Characters/"+id+"/seated_placeholder"),id);

                    var profile=catalog.Find(id);
                    Assert.That(profile,Is.Not.Null,id+": catalog profile");
                    Assert.That(profile.portrait,Is.Not.Null,id+": portrait");
                    Assert.That(profile.avatar,Is.Not.Null,id+": avatar");
                    Assert.That(profile.worldSprite,Is.Not.Null,id+": worldSprite");
                    Assert.That(profile.portrait,Is.SameAs(Resources.Load<Sprite>(obj.portraitPath)),id);
                    Assert.That(profile.avatar,Is.SameAs(Resources.Load<Sprite>(obj.avtarPath)),id);
                    Assert.That(profile.worldSprite,Is.SameAs(Resources.Load<Sprite>(obj.portraitIndoorPath)),id);
                    Assert.That(profile.avatar.rect.width,Is.EqualTo(512f),id+": avatar width");
                    Assert.That(profile.avatar.rect.height,Is.EqualTo(512f),id+": avatar height");
                    Assert.That(profile.worldSprite.name,Is.EqualTo("seated_placeholder"),id);
                    Assert.That(profile.worldSprite,Is.Not.SameAs(profile.portrait),id);
                }
                Assert.That(catalog.Find("adele").cafeCustomer,Is.False);
                Assert.That(catalog.Find("eugene").cafeCustomer,Is.True);
            }
            finally { Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void AllNonEmptyObjectResourcePathsLoadSuccessfully()
        {
            var configuration=LubanNarrativeLoader.Load();
            foreach(var obj in configuration.objects)
                foreach(var path in new[]{obj.iconPath,obj.portraitPath,obj.portraitIndoorPath,obj.avtarPath})
                    if(!string.IsNullOrWhiteSpace(path)) Assert.That(Resources.Load<Sprite>(path),Is.Not.Null,obj.objId+":"+path);
        }

        [Test]
        public void NonFormalRowsWithMissingIndoorUsePortraitFallback()
        {
            var configuration=LubanNarrativeLoader.Load();
            var formalIds=new System.Collections.Generic.HashSet<string>(FormalCharacterIds);
            var catalog=CharacterCatalog.LoadFromTables();
            try
            {
                // Formal characters always have seated_placeholder; this compatibility rule only
                // applies if a non-formal NPC row actually omits its indoor resource.
                // Do not assert that the current table contains any such fallback row.
                foreach(var obj in configuration.objects)
                {
                    if(formalIds.Contains(obj.objId) || obj.objType!=NarrativeObjectType.Npc ||
                        string.IsNullOrWhiteSpace(obj.portraitPath) || !string.IsNullOrWhiteSpace(obj.portraitIndoorPath)) continue;
                    var profile=catalog.Find(obj.objId);
                    Assert.That(profile,Is.Not.Null,obj.objId);
                    Assert.That(profile.portrait,Is.Not.Null,obj.objId);
                    Assert.That(profile.worldSprite,Is.SameAs(profile.portrait),obj.objId);
                }
            }
            finally { Object.DestroyImmediate(catalog); }
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
