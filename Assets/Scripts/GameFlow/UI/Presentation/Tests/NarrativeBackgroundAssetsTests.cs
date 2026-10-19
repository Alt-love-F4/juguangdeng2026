using System;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    public sealed class NarrativeBackgroundAssetsTests
    {
        [TestCase("cafe.day", "SCE_一楼大厅对话场景日景")]
        [TestCase("cafe.dawn", "SCE_一楼大厅场景清晨")]
        [TestCase("cafe.dusk", "SCE_一楼大厅场景傍晚")]
        [TestCase("cafe.night", "SCE_一楼大厅场景夜晚")]
        [TestCase("cafe.paper_flowers", "SCE_一楼大厅场景纸花装饰")]
        [TestCase("cafe.second_floor.day", "SCE_二楼会客室对话场景日景")]
        [TestCase("cafe.second_floor.cluttered", "SCE_二楼会客室场景杂乱")]
        [TestCase("cafe.dormitory.day", "SCE_三楼宿舍对话场景日景")]
        [TestCase("cafe.bedroom.dawn", "SCE_三楼主角房间场景清晨")]
        [TestCase("street.day", "SCE_圣徒街场景1")]
        [TestCase("street.night", "SCE_圣徒街场景夜晚")]
        [TestCase("cafe.exterior.day", "SCE_沸点咖啡馆店面场景日景")]
        [TestCase("cafe.exterior.declining", "SCE_沸点咖啡馆店面场景萧条")]
        [TestCase("club.exterior", "SCE_布塔克俱乐部门口场景底图")]
        [TestCase("club.reception", "SCE_布塔克俱乐部会客室场景底图")]
        [TestCase("cathedral.day", "SCE_圣卷大教堂场景日景")]
        [TestCase("market.day", "SCE_大堂区露天市场场景日景")]
        [TestCase("market.shop.day", "SCE_大堂区杂货铺内部场景日景")]
        [TestCase("suburb.day", "SCE_圣西尔万郊区场景日景")]
        [TestCase("old.street.day", "SCE_旧城区街道场景日景")]
        [TestCase("old.house.day", "SCE_吕西尼昂旧宅门前场景底图")]
        [TestCase("detention.interior", "SCE_教会看守所内部场景底图")]
        [TestCase("detention.dawn", "SCE_教会看守所内部场景清晨")]
        [TestCase("detention.dusk", "SCE_教会看守所内部场景傍晚")]
        [TestCase("cafe.door.thibaut", "SCE_咖啡馆闭门场景蒂博剪影")]
        [TestCase("cafe.bar.day", "SCE_咖啡馆吧台场景底图")]
        [TestCase("cafe.workstation.day", "SCE_咖啡馆操作台场景底图")]
        [TestCase("cafe.serving.day", "SCE_咖啡馆出餐台场景底图")]
        public void CanonicalKeysReferenceApprovedArtwork(string key, string textureName)
        {
            var background = NarrativeStageVisuals.Resolve(key);
            Assert.That(background.texture, Is.Not.Null, key);
            Assert.That(background.texture.name, Is.EqualTo(textureName));
            Assert.That(background.placeholder, Is.False);
            Assert.That(background.solidColor, Is.False);
            Assert.That(background.tint, Is.EqualTo(Color.white));
        }

        [Test]
        public void OperatingFloorBaseImagesAreNeverUsedAsDialogueBackgrounds()
        {
            Assert.That(NarrativeStageVisuals.Resolve("cafe.day").texture.name,
                Is.EqualTo("SCE_一楼大厅对话场景日景"));
            Assert.That(NarrativeStageVisuals.Resolve("cafe.second_floor.day").texture.name,
                Is.EqualTo("SCE_二楼会客室对话场景日景"));
            Assert.That(NarrativeStageVisuals.Resolve("cafe.dormitory.day").texture.name,
                Is.EqualTo("SCE_三楼宿舍对话场景日景"));
            Assert.That(NarrativeStageVisuals.Resolve("SCE_一楼大厅场景底图.png").texture.name,
                Is.EqualTo("SCE_一楼大厅对话场景日景"));
            Assert.That(NarrativeStageVisuals.Resolve("SCE_二楼会客室场景底图.png").texture.name,
                Is.EqualTo("SCE_二楼会客室对话场景日景"));
            Assert.That(NarrativeStageVisuals.Resolve("SCE_ 三楼宿舍场景底图.png").texture.name,
                Is.EqualTo("SCE_三楼宿舍对话场景日景"));
        }

        [TestCase("oct07.cafe.day", "cafe.day")]
        [TestCase("oct07.cafe.night", "cafe.night")]
        [TestCase("oct07.street.day", "street.day")]
        [TestCase("neutral.cafe.dusk", "cafe.dusk")]
        [TestCase("rose.club.reception", "club.reception")]
        [TestCase("church.cathedral.day", "cathedral.day")]
        [TestCase("church.1104.s1.background.morning.wake", "cafe.bedroom.dawn")]
        [TestCase("church.1104.s1.background.far.street.dusk", "street.night")]
        [TestCase("church.1104.s1.background.far.street.wisdom.pass", "street.day")]
        [TestCase("church.1104.s1.background.far.street.wisdom.fail", "street.day")]
        [TestCase("church.1104.s1.background.far.street.merge", "street.day")]
        [TestCase("church.1104.s1.background.near.old.perception", "old.street.day")]
        [TestCase("future.event.old.street.day", "old.street.day")]
        [TestCase("future.event.old.street", "old.street.day")]
        [TestCase("church.1104.s1.background.near.old.heirloom", "old.house.day")]
        [TestCase("church.1201.s1.background.near.market", "market.day")]
        [TestCase("church.1201.s1.background.far.suburb", "suburb.day")]
        [TestCase("future.event.cafe.second_floor.cluttered", "cafe.second_floor.cluttered")]
        [TestCase("future.event.cafe.door.thibaut", "cafe.door.thibaut")]
        [TestCase("future.event.detention.dawn", "detention.dawn")]
        [TestCase("SCE_圣徒街场景夜晚.png", "street.night")]
        [TestCase("Assets/Art/Background/game/SCE_一楼大厅场景傍晚.png", "cafe.dusk")]
        public void ExistingAndFutureSemanticKeysReuseTheCatalog(string key, string canonicalKey)
        {
            var background = NarrativeStageVisuals.Resolve(key);
            Assert.That(background.key, Is.EqualTo(key));
            Assert.That(background.texture, Is.SameAs(NarrativeStageVisuals.Resolve(canonicalKey).texture));
            Assert.That(background.placeholder, Is.False);
        }

        [TestCase("Art/Gameplay/DayFlow/city_map_placeholder", "arrival.start.street", "street.day")]
        [TestCase("Art/Gameplay/DayFlow/city_map_placeholder", "arrival.start.cafe", "cafe.exterior.declining")]
        [TestCase("oct07.cafe.day", "revolution.1014.s1.intro", "cafe.dawn")]
        [TestCase("oct07.cafe.day", "rose.1012.s1.preparation", "cafe.paper_flowers")]
        [TestCase("oct07.cafe.day", "rose.1012.s1.sampling_player", "cafe.exterior.day")]
        [TestCase("oct07.cafe.day", "rose.1012.s1.brew_pass", "cafe.workstation.day")]
        [TestCase("oct07.cafe.day", "unrelated.event", "cafe.day")]
        [TestCase("oct07.cafe.day", "revolution.1014.s10", "cafe.day")]
        [TestCase("cafe.night", "revolution.1014.s1.intro", "cafe.night")]
        public void ScopedMigrationsOnlyReplaceDeclaredLegacyKeys(string sourceKey, string context, string expectedKey)
        {
            Assert.That(NarrativeStageVisuals.Resolve(sourceKey, context).texture,
                Is.SameAs(NarrativeStageVisuals.Resolve(expectedKey).texture));
        }

        [Test]
        public void EarlyCafeTutorialUsesTheSameRoomBackgroundUntilTheWorkstationScene()
        {
            Texture cafeBackground = NarrativeStageVisuals.Resolve("cafe.day").texture;
            foreach (string context in new[]
            {
                "first_day_cafe", "cafe_tutorial.customer_arrives", "cafe_tutorial.perception_success",
                "cafe_tutorial.perception_failure", "cafe_tutorial.order_confirmed"
            })
                Assert.That(NarrativeStageVisuals.Resolve("cafe.day", context).texture, Is.SameAs(cafeBackground), context);

            Assert.That(NarrativeStageVisuals.Resolve("cafe.day", "cafe_tutorial.workstation_open").texture,
                Is.SameAs(NarrativeStageVisuals.Resolve("cafe.workstation.day").texture));
        }

        [Test]
        public void ConversationAdapterSuppliesPageContext()
        {
            var data = new NarrativeConversationPresentationData
            {
                presentationKey = "arrival.start",
                backgroundPath = "Art/Gameplay/DayFlow/city_map_placeholder"
            };
            data.lines.Add(new NarrativeConversationPresentationLine { pageId = "arrival.start.street", text = "街道" });
            Assert.That(NarrativeConversationEventPlayback.CreateScript(data).Background,
                Is.SameAs(NarrativeStageVisuals.Resolve("street.day").texture));
        }

        [Test]
        public void EveryAuthoredConversationBackgroundHasAnExplicitMapping()
        {
            var config = LubanNarrativeLoader.Load();
            int checkedPages = 0;
            foreach (var page in config.presentations.conversationPages)
            {
                if (string.IsNullOrWhiteSpace(page.bgOverride)) continue;
                var background = NarrativeStageVisuals.Resolve(page.bgOverride, page.pageId);
                Assert.That(background, Is.Not.Null, page.pageId);
                if (background.placeholder)
                {
                    // User-marked unfinished CGs are deliberately labelled local white cards,
                    // not a missing resource, approved finished art or an accidental black fill.
                    string card = page.bgOverride == "rose.club.knight_guidance" ? "CG03_"
                        : page.bgOverride == "cg.adele_sampling" ? "CG04_"
                        : page.bgOverride == "cg.burning_letter" ? "CG05_"
                        : page.bgOverride == "cg.marie_moonlit" ? "CG07_"
                        : page.bgOverride == "cg.rainy_triad" ? "CG09_" : null;
                    Assert.That(card, Is.Not.Null, "Unreviewed background placeholder: " + page.pageId);
                    Assert.That(background.texture, Is.Not.Null, page.pageId);
                    Assert.That(background.texture.name, Does.StartWith(card), page.pageId);
                    Assert.That(background.placeholderColor, Is.EqualTo(Color.white), page.pageId);
                    Assert.That(background.useSceneBackground || background.solidColor, Is.False, page.pageId);
                }
                Assert.That(background.texture != null || background.solidColor || background.useSceneBackground, Is.True, page.pageId);
                checkedPages++;
            }
            Assert.That(checkedPages, Is.GreaterThan(0));
        }

        [Test]
        public void CafeSceneMarkerSuppressesFallbackArtAndSurvivesScriptCopies()
        {
            var marker = NarrativeStageVisuals.Resolve(NarrativeBackgroundKeys.CafeScene);
            Assert.That(marker.useSceneBackground, Is.True);
            Assert.That(marker.texture, Is.Null);
            Assert.That(marker.placeholder || marker.solidColor, Is.False);
            var script = new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Narrator, "店内") },
                background: NarrativeStageVisuals.Resolve("street.day").texture, backgroundVisual: marker);
            marker.useSceneBackground = false;
            Assert.That(script.Background, Is.Null);
            Assert.That(script.BackgroundVisual.useSceneBackground, Is.True);
            Assert.That(script.WithPreviewContext().BackgroundVisual.useSceneBackground, Is.True);
            Assert.That(script.RebuildAt(0).BackgroundVisual.useSceneBackground, Is.True);
            Assert.That(script.WithFirstPageHint("提示").BackgroundVisual.useSceneBackground, Is.True);
            Assert.That(NarrativeStageVisuals.Resolve(NarrativeBackgroundKeys.CafeScene).useSceneBackground, Is.True);
        }

        [Test]
        public void PublishedCafePagesExplicitlyUseTheLiveSceneWithoutChangingOutsideLocations()
        {
            var config = LubanNarrativeLoader.Load();
            int scenePages = 0;
            foreach (var page in config.presentations.conversationPages)
            {
                Assert.That(page.bgOverride, Is.Not.EqualTo("oct07.cafe.day"));
                Assert.That(page.bgOverride, Is.Not.EqualTo("oct07.cafe.night"));
                if (page.bgOverride == NarrativeBackgroundKeys.CafeScene) scenePages++;
                if (page.pageId == "oct07.s1.street")
                    Assert.That(page.bgOverride, Is.EqualTo("oct07.street.day"));
            }
            Assert.That(scenePages, Is.GreaterThanOrEqualTo(47));
        }

        [Test]
        public void SceneMarkersCannotAlsoDeclareArtworkOrBlackFill()
        {
            var library = ScriptableObject.CreateInstance<NarrativeStageVisuals>();
            try
            {
                Assert.Throws<InvalidOperationException>(() => library.SetEntry(new NarrativeStageVisuals.Entry
                { key = "invalid.scene", useSceneBackground = true, solidColor = true }));
                Assert.Throws<InvalidOperationException>(() => library.SetEntry(new NarrativeStageVisuals.Entry
                { key = "invalid.scene", useSceneBackground = true, texture = NarrativeStageVisuals.Resolve("street.day").texture }));
            }
            finally { Object.DestroyImmediate(library); }
        }

        [TestCase("rose.club.blindfold")]
        [TestCase("church.execution.black")]
        public void AuthoredBlackIsIntentionalRatherThanAMissingBackground(string key)
        {
            var background = NarrativeStageVisuals.Resolve(key);
            Assert.That(background.texture, Is.Null);
            Assert.That(background.solidColor, Is.True);
            Assert.That(background.placeholder, Is.False);
            Assert.That(background.tint, Is.EqualTo(Color.black));
        }

        [Test]
        public void ExactEntriesOverrideAliasesAndReturnedSnapshotsAreIndependent()
        {
            var library = Object.Instantiate(Resources.Load<NarrativeStageVisuals>("Dialogue/NarrativeStageVisuals"));
            try
            {
                var explicitBackground = library.Find("cafe.dusk");
                explicitBackground.key = "future.event.street.day";
                library.SetEntry(explicitBackground);
                Assert.That(library.Find(explicitBackground.key).texture, Is.SameAs(explicitBackground.texture));
                var alias = library.Find("future.event.cafe.day");
                alias.tint = Color.red;
                alias.texture = null;
                Assert.That(library.Find("future.event.cafe.day").tint, Is.EqualTo(Color.white));
                Assert.That(library.Find("cafe.day").texture, Is.Not.Null);
            }
            finally { Object.DestroyImmediate(library); }
        }

        [TestCase("unknown.location.day")]
        [TestCase("future.street.day.extra")]
        [TestCase("future.notstreet.day")]
        [TestCase("Art/Gameplay/DayFlow/city_map_placeholder")]
        public void UnknownLocationsAreNotSilentlyReplacedByUnrelatedArt(string key)
        {
            var library = Resources.Load<NarrativeStageVisuals>("Dialogue/NarrativeStageVisuals");
            Assert.That(library.Find(key), Is.Null);
        }

        [Test]
        public void MissingUnknownResourceStillReportsAnError()
        {
            Assert.Throws<InvalidOperationException>(() => NarrativeStageVisuals.Resolve("unknown.location.day"));
            Assert.That(NarrativeStageVisuals.Resolve(null), Is.Null);
        }
    }
}
