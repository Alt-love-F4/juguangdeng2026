using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Spotlight.GameFlow;

namespace Spotlight.Narrative.Editor.Tests
{
    public sealed class NarrativeArtPlaceholderTests
    {
        [Test]public void HistoricalRequirementCardsAreCountedIndependentlyFromCurrentCoverage()
        {
            var manifest=NarrativeArtPlaceholderBuilder.ReadManifest();
            Assert.That(manifest.requirements.Length,Is.EqualTo(NarrativeArtPlaceholderBuilder.ExpectedRequirementCount));
            Assert.That(manifest.requirements.Select(r=>r.id).Distinct(System.StringComparer.Ordinal).Count(),Is.EqualTo(115));
            var cards=manifest.requirements.Where(NarrativeArtPlaceholderBuilder.HasHistoricalCard).ToArray();
            Assert.That(cards.Length,Is.EqualTo(52));
            foreach(var item in cards)
            {
                Assert.That(item.assetPath,Does.StartWith("Assets/Art/NarrativePlaceholders/"));Assert.That(File.Exists(item.assetPath),Is.True);
                Assert.That(item.requirement,Is.Not.Empty);Assert.That(item.sourceEvidence,Is.Not.Empty);
                var texture=AssetDatabase.LoadAssetAtPath<Texture>(item.assetPath);Assert.That(texture,Is.Not.Null,item.id);Assert.That(texture.width,Is.GreaterThanOrEqualTo(768));
            }
        }

        [Test]public void CurrentArtUsesProductionSourcesForCg07Ch35AndP04()
        {
            var requirements=NarrativeArtPlaceholderBuilder.ReadManifest().requirements;
            var registry=AssetDatabase.LoadAssetAtPath<NarrativeStageVisuals>(NarrativeArtPlaceholderBuilder.RegistryPath);
            var characters=NarrativeCharacterArtAudit.Load();
            var config=LubanNarrativeLoader.Load();
            var endings=NarrativeEndingArtworkCatalog.Load();

            var cg07=NarrativeArtPlaceholderBuilder.InspectCurrentArt(requirements.Single(r=>r.id=="CG07"),registry,characters,config,endings);
            Assert.That(cg07.Connected,Is.True);
            Assert.That(cg07.Key,Is.EqualTo("cg.marie_moonlit"));
            Assert.That(cg07.Asset,Is.Not.Null);
            Assert.That(cg07.AssetPath,Does.Not.StartWith("Assets/Art/NarrativePlaceholders/"));

            var ch35=NarrativeArtPlaceholderBuilder.InspectCurrentArt(requirements.Single(r=>r.id=="CH35"),registry,characters,config,endings);
            var tutorial=Resources.Load<DialogueVisualsAsset>(NarrativeArtPlaceholderBuilder.TutorialVisualsPath);
            Assert.That(ch35.Connected,Is.True);
            Assert.That(ch35.Key,Is.EqualTo("Dialogue/CafeTutorialVisuals.centerPortrait"));
            Assert.That(ch35.Asset,Is.SameAs(tutorial.CenterPortrait));
            Assert.That(ch35.AssetPath,Is.EqualTo(AssetDatabase.GetAssetPath(tutorial.CenterPortrait)));

            var p04=NarrativeArtPlaceholderBuilder.InspectCurrentArt(requirements.Single(r=>r.id=="P04"),registry,characters,config,endings);
            Assert.That(p04.Connected,Is.True);
            Assert.That(p04.Key,Is.EqualTo("prop.king_coins"));
            Assert.That(p04.Asset,Is.Not.Null);
            Assert.That(p04.AssetPath,Does.Not.StartWith("Assets/Art/NarrativePlaceholders/"));

            var summary=NarrativeArtPlaceholderBuilder.Summarize(requirements,registry,characters,config,endings);
            Assert.That(summary.Total,Is.EqualTo(115));
            Assert.That(summary.HistoricalCards,Is.EqualTo(52));
            Assert.That(summary.EndingNewImages,Is.EqualTo(15));
            Assert.That(summary.EndingReuses,Is.EqualTo(1));
            Assert.That(summary.EndingNoNewArtwork,Is.EqualTo(0));
        }

        [Test]public void RefusingCafeEndingHasAnExplicitRequirementCardInsteadOfNoArtwork()
        {
            var item=NarrativeArtPlaceholderBuilder.ReadManifest().requirements.Single(r=>r.id=="END-IF0");
            var catalog=NarrativeEndingArtworkCatalog.Load();
            var entry=catalog.Find("arrival.ending.if0");
            Assert.That(item.disposition,Is.EqualTo("placeholder"));
            Assert.That(item.assetPath,Is.EqualTo("Assets/Art/NarrativePlaceholders/Endings/END-IF0.png"));
            Assert.That(entry,Is.Not.Null);
            Assert.That(entry.artwork,Is.Not.Null);
            Assert.That(entry.artworkIsPlaceholder,Is.True);
            Assert.That(entry.status,Is.EqualTo(NarrativeEndingArtworkCatalog.ArtStatus.RequiredMissing));
            Assert.That(AssetDatabase.GetAssetPath(entry.artwork),Is.EqualTo(item.assetPath));
            var snapshot=NarrativeEndingEcho.ResolveArtwork(entry.endingId);
            Assert.That(snapshot.Sprite,Is.EqualTo(entry.artwork));
            Assert.That(snapshot.IsPlaceholder,Is.True);
            Assert.That(snapshot.StatusLabel,Does.Contain("非最终插画"));
            var current=NarrativeArtPlaceholderBuilder.InspectCurrentArt(item,null,null,null,catalog);
            Assert.That(current.RequirementCard,Is.True);
            Assert.That(current.Connected,Is.False);
            Assert.That(current.NoNewArtwork,Is.False);
        }

        [Test]public void ProposedKeysAndRequirementCardRegistryEntriesDoNotCountAsProductionHooks()
        {
            var registry=AssetDatabase.LoadAssetAtPath<NarrativeStageVisuals>(NarrativeArtPlaceholderBuilder.RegistryPath);
            var config=LubanNarrativeLoader.Load();
            var audit=NarrativeCharacterArtAudit.Load();
            var status=NarrativeArtPlaceholderBuilder.InspectCurrentArt(new NarrativeArtPlaceholderBuilder.Requirement
            {
                id="__audit_unregistered__",category="CG／插画",
                proposedUnregisteredKeys=new[]{"cg.marie_moonlit","art.requirement.CG07"}
            },registry,audit,config,NarrativeEndingArtworkCatalog.Load());
            Assert.That(status.Connected,Is.False);
            Assert.That(status.Key,Is.Null.Or.Empty);
        }
        [Test]public void OrdinaryCafeMarkerAndExistingRealBackgroundsRemainUnchanged()
        {
            var registry=new SerializedObject(AssetDatabase.LoadMainAssetAtPath(NarrativeArtPlaceholderBuilder.RegistryPath));var entries=registry.FindProperty("entries");
            foreach(string key in new[]{"scene.cafe","street.day","cafe.door.thibaut"})
            {
                SerializedProperty entry=null;for(int i=0;i<entries.arraySize;i++){var e=entries.GetArrayElementAtIndex(i);if(e.FindPropertyRelative("key").stringValue==key)entry=e;}
                Assert.That(entry,Is.Not.Null,key);Assert.That(entry.FindPropertyRelative("placeholder").boolValue,Is.False,key);
                if(key=="scene.cafe"){Assert.That(entry.FindPropertyRelative("useSceneBackground").boolValue,Is.True);Assert.That(entry.FindPropertyRelative("texture").objectReferenceValue,Is.Null);}
                else{var path=AssetDatabase.GetAssetPath(entry.FindPropertyRelative("texture").objectReferenceValue);Assert.That(path,Does.Not.StartWith("Assets/Art/NarrativePlaceholders/"));}
            }
        }
        [Test]public void DeliveredCgArtUsesStableKeysWithoutActivatingPendingStoryHooks()
        {
            var visuals=AssetDatabase.LoadAssetAtPath<Spotlight.GameFlow.NarrativeStageVisuals>(NarrativeArtPlaceholderBuilder.RegistryPath);
            foreach(var binding in new[]
            {
                new[]{"rose.club.knight_guidance","Assets/Art/NarrativeCG/CG03_knight_guidance.jpg"},
                new[]{"cg.adele_sampling","Assets/Art/NarrativeCG/CG04_adele_sampling.jpg"},
                new[]{"cg.burning_letter","Assets/Art/NarrativeCG/CG05_burning_letter.jpg"},
                new[]{"cg.duke_blessing","Assets/Art/NarrativeCG/CG06_duke_blessing.jpg"}
            })
            {
                var entry=visuals.Find(binding[0]);Assert.That(entry,Is.Not.Null,binding[0]);
                Assert.That(entry.placeholder,Is.False,binding[0]);
                Assert.That(AssetDatabase.GetAssetPath(entry.texture),Is.EqualTo(binding[1]),binding[0]);
                Assert.That(AssetDatabase.LoadAssetAtPath<Texture>(binding[1]),Is.Not.Null,binding[1]);
            }

            Assert.That(AssetDatabase.GetAssetPath(visuals.Find("street.day").texture),Is.EqualTo("Assets/Art/Background/game/SCE_圣徒街场景1.png"));
            Assert.That(AssetDatabase.GetAssetPath(visuals.Find("oct07.street.day").texture),Is.EqualTo("Assets/Art/Background/game/SCE_圣徒街场景1.png"));
            Assert.That(visuals.Find("cg.protagonist_turn").placeholder,Is.True);
            var config=LubanNarrativeLoader.Load();
            Assert.That(config.presentations.conversationPages.Any(p=>p.bgOverride=="cg.duke_blessing"),Is.False);
            Assert.That(AssetDatabase.GetAssetPath(visuals.Find("cathedral.day").texture),Is.EqualTo("Assets/Art/Background/game/SCE_圣卷大教堂场景日景.png"));
        }
        [Test]public void OnlyAuthoredSpecialCompositionsSwitchToCardsAndRestoreTheNormalScene()
        {
            var c=LubanNarrativeLoader.Load();
            var sampling=c.presentations.conversationPages.Single(p=>p.pageId=="rose.1012.s1.outside.sampling");Assert.That(sampling.bgOverride,Is.EqualTo("cg.adele_sampling"));
            var outside=c.presentations.conversationPages.Single(p=>p.pageId=="rose.1012.s1.outside");Assert.That(outside.seq,Is.GreaterThan(sampling.seq));Assert.That(outside.bgOverride,Is.EqualTo("cafe.exterior.day"));
            var moon=c.presentations.conversationPages.Single(p=>p.pageId=="revolution.1029.s1.page.p013.1");
            Assert.That(moon.bgOverride,Is.EqualTo("cg.marie_moonlit"));
            Assert.That(c.presentations.conversationPages.Single(p=>p.pageId=="revolution.1029.s1.page.p021.1").bgOverride,Is.EqualTo("scene.cafe"));
            var rain=c.presentations.conversationPages.Single(p=>p.pageId=="revolution.route.1123.s1.page.p229.1");
            Assert.That(rain.bgOverride,Is.EqualTo("cg.rainy_triad"));
            Assert.That(c.presentations.conversationPages.Single(p=>p.pageId=="revolution.route.1123.s1.page.p232.1").bgOverride,Is.Not.EqualTo("cg.rainy_triad"));
            for(int i=1;i<=4;i++)
            {
                var burn=c.presentations.conversationPages.Single(p=>p.pageId=="church.1020.s1.p142.branch"+i);Assert.That(burn.bgOverride,Is.EqualTo("cg.burning_letter"));
                var after=c.presentations.conversationPages.Single(p=>p.pageId=="church.1020.s1.p144.branch"+i);Assert.That(after.seq,Is.GreaterThan(burn.seq));Assert.That(after.bgOverride,Is.EqualTo("scene.cafe"));
                Assert.That(after.pageCondKeys,Is.EqualTo(burn.pageCondKeys));
            }
        }
    }
}
