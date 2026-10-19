using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Spotlight.Narrative.Editor.Tests
{
    public sealed class NarrativeArtPlaceholderTests
    {
        [Test]public void MarkedRequirementsHaveLocalCardsWithoutReplacingExistingArtwork()
        {
            var manifest=NarrativeArtPlaceholderBuilder.ReadManifest();Assert.That(manifest.requirements.Length,Is.EqualTo(99));
            var cards=manifest.requirements.Where(r=>r.disposition=="placeholder").ToArray();Assert.That(cards.Length,Is.EqualTo(37));
            var registry=new SerializedObject(AssetDatabase.LoadMainAssetAtPath(NarrativeArtPlaceholderBuilder.RegistryPath));var entries=registry.FindProperty("entries");
            foreach(var item in cards)
            {
                Assert.That(item.assetPath,Does.StartWith("Assets/Art/NarrativePlaceholders/"));Assert.That(File.Exists(item.assetPath),Is.True);
                Assert.That(item.requirement,Is.Not.Empty);Assert.That(item.sourceEvidence,Is.Not.Empty);
                var texture=AssetDatabase.LoadAssetAtPath<Texture>(item.assetPath);Assert.That(texture,Is.Not.Null,item.id);Assert.That(texture.width,Is.GreaterThanOrEqualTo(768));
                SerializedProperty entry=null;for(int i=0;i<entries.arraySize;i++){var e=entries.GetArrayElementAtIndex(i);if(e.FindPropertyRelative("key").stringValue=="art.requirement."+item.id)entry=e;}
                Assert.That(entry,Is.Not.Null,item.id);Assert.That(entry.FindPropertyRelative("placeholder").boolValue,Is.True,item.id);
                Assert.That(entry.FindPropertyRelative("texture").objectReferenceValue,Is.EqualTo(texture));
            }
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
