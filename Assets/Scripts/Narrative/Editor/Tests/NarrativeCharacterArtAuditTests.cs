using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spotlight.Narrative.Editor.Tests
{
    public sealed class NarrativeCharacterArtAuditTests
    {
        [Test]
        public void Ch18UsesCurrentSparrowSpritesAndReportsUnwiredChineseExpressions()
        {
            var audit = NarrativeCharacterArtAudit.Load();
            var characters = audit.Inspect(Requirement("CH18"));
            Assert.That(characters.Select(c => c.Id), Is.EqualTo(new[] { "sparrow" }));
            var sparrow = characters.Single();
            Assert.That(sparrow.ObjectExists, Is.True);
            Assert.That(sparrow.Name, Is.EqualTo("麻雀"));
            Assert.That(sparrow.Portrait.Path, Is.EqualTo("Characters/sparrow/neutral"));
            Assert.That(sparrow.Avatar.Path, Is.EqualTo("Characters/sparrow/avatar"));
            Assert.That(sparrow.Indoor.Path, Is.EqualTo("Characters/sparrow/seated_placeholder"));
            foreach (var resource in new[] { sparrow.Portrait, sparrow.Avatar, sparrow.Indoor })
            {
                Assert.That(resource.Available, Is.True, resource.Path);
                Assert.That(resource.Error, Is.Null.Or.Empty, resource.Path);
                Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(resource.AssetPath), Is.SameAs(resource.Sprite));
            }
            Assert.That(sparrow.Portrait.Placeholder, Is.False);
            Assert.That(sparrow.Avatar.Placeholder, Is.False);
            Assert.That(sparrow.Indoor.Placeholder, Is.True);
            Assert.That(NarrativeCharacterArtAudit.Summary(characters), Is.EqualTo("基础已配置"));

            var config = LubanNarrativeLoader.Load();
            var lines = config.presentations.conversationLines
                .Where(l => l.speakerKey == "sparrow").ToArray();
            Assert.That(sparrow.ConversationLines, Is.EqualTo(lines.Length));
            Assert.That(sparrow.OnstageConversationLines, Is.GreaterThan(0));
            Assert.That(sparrow.OffstageConversationLines, Is.GreaterThan(0));
            Assert.That(sparrow.OnstageConversationLines + sparrow.OffstageConversationLines, Is.EqualTo(sparrow.ConversationLines));
            Assert.That(lines.Count(l => l.side == "right"), Is.EqualTo(sparrow.OnstageConversationLines));
            Assert.That(lines.Count(l => l.side == "none"), Is.EqualTo(sparrow.OffstageConversationLines));

            // This root intentionally hides the portrait. A wired base sprite does not override side=none.
            var roseArrivalLines = lines.Where(l => l.pageId.StartsWith("rose.1011.s1.", StringComparison.Ordinal)).ToArray();
            Assert.That(roseArrivalLines.Length, Is.EqualTo(17));
            Assert.That(roseArrivalLines.All(l => l.side == "none"), Is.True);
            var roseArrivalConfig = new NarrativeConfiguration();
            roseArrivalConfig.objects.Add(config.objects.Single(o => o.objId == "sparrow"));
            roseArrivalConfig.presentations.conversationLines.AddRange(roseArrivalLines);
            var roseArrival = new NarrativeCharacterArtAudit(roseArrivalConfig).Inspect("sparrow");
            Assert.That(roseArrival.OnstageConversationLines, Is.Zero);
            Assert.That(roseArrival.OffstageConversationLines, Is.EqualTo(17));
            Assert.That(roseArrival.Portrait.Available, Is.True);
            Assert.That(roseArrival.FallbackEmotes, Does.Contain("开心"), "The audit inventories offstage emotes too; the presenter still respects side=none.");
            Assert.That(sparrow.ConversationLines, Is.EqualTo(lines.Length));
            Assert.That(sparrow.UsageExamples.Any(e => e.StartsWith("Conversation / ", StringComparison.Ordinal)), Is.True);
            foreach (var emote in new[] { "开心", "生气", "迷惑" })
            {
                Assert.That(lines.Any(l => l.emote == emote), Is.True, emote);
                Assert.That(sparrow.FallbackEmotes, Does.Contain(emote));
            }
            Assert.That(sparrow.FallbackEmotes, Does.Not.Contain("平静"));

            var catalog = CharacterCatalog.LoadFromTables();
            try
            {
                var profile = catalog.Find("sparrow");
                Assert.That(profile, Is.Not.Null);
                Assert.That(sparrow.Portrait.Sprite, Is.SameAs(profile.portrait));
                Assert.That(sparrow.Avatar.Sprite, Is.SameAs(profile.avatar));
                Assert.That(sparrow.Indoor.Sprite, Is.SameAs(profile.worldSprite));
                Assert.That(sparrow.CafeCustomer, Is.EqualTo(profile.cafeCustomer));
                foreach (var emote in sparrow.FallbackEmotes)
                    Assert.That(profile.PortraitFor(emote), Is.SameAs(profile.portrait), emote);
            }
            finally { Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void Ch17KeepsSorelAndEmmanuelSeparateSoLoadedArtCannotHideMissingStoryArt()
        {
            var characters = NarrativeCharacterArtAudit.Load().Inspect(Requirement("CH17"));
            Assert.That(characters.Select(c => c.Id), Is.EquivalentTo(new[] { "sorel", "emmanuel" }));
            var sorel = characters.Single(c => c.Id == "sorel");
            var emmanuel = characters.Single(c => c.Id == "emmanuel");
            Assert.That(sorel, Is.Not.SameAs(emmanuel));
            Assert.That(sorel.ObjectExists, Is.True);
            Assert.That(sorel.Portrait.Path, Is.Empty);
            Assert.That(sorel.Portrait.Available, Is.False);
            Assert.That(sorel.Portrait.Error, Is.Not.Null.And.Not.Empty);
            Assert.That(sorel.OnstageConversationLines, Is.GreaterThan(0), "CH17 is missing art actually needed onstage.");
            Assert.That(emmanuel.Portrait.Available, Is.True);
            Assert.That(emmanuel.Portrait.Path, Is.EqualTo("Characters/emmanuel/neutral"));
            Assert.That(NarrativeCharacterArtAudit.Summary(characters), Is.EqualTo("部分剧情 ID 缺图"));
        }

        [Test]
        public void Ch04DistinguishesAnUnusedObjectIdFromMissingOnstageStoryArt()
        {
            var characters = NarrativeCharacterArtAudit.Load().Inspect(Requirement("CH04"));
            Assert.That(characters.Select(c => c.Id), Is.EquivalentTo(new[] { "church.duke", "elion" }));
            var duke = characters.Single(c => c.Id == "church.duke");
            var elion = characters.Single(c => c.Id == "elion");
            Assert.That(duke.ObjectExists, Is.True);
            Assert.That(duke.Portrait.Available, Is.False);
            Assert.That(duke.Portrait.Path, Is.Empty);
            Assert.That(duke.Portrait.Error, Is.Not.Null.And.Not.Empty);
            Assert.That(duke.OnstageConversationLines, Is.Zero);
            Assert.That(elion.Portrait.Available, Is.True);
            Assert.That(elion.OnstageConversationLines, Is.GreaterThan(0));
            Assert.That(NarrativeCharacterArtAudit.Summary(characters), Is.EqualTo("部分 ID 未配置（未登场）"));
        }

        [Test]
        public void InvalidResourceIsReportedWithoutPreventingAnotherCharacterFromLoading()
        {
            const string invalidPath = "Characters/__NarrativeCharacterArtAuditTests_missing__/neutral";
            var config = new NarrativeConfiguration();
            config.objects.Add(new NarrativeObjectDefinition
            {
                objId = "broken", portraitPath = invalidPath, avtarPath = "", iconPath = "", portraitIndoorPath = ""
            });
            config.objects.Add(new NarrativeObjectDefinition
            {
                objId = "valid", portraitPath = "Characters/sparrow/neutral", iconPath = "Characters/sparrow/avatar"
            });
            config.presentations.conversationLines.Add(new NarrativeConversationLineDefinition
            {
                lineId = "audit.broken.onstage", speakerKey = "broken", side = "right"
            });
            var characters = new NarrativeCharacterArtAudit(config).Inspect(new NarrativeArtPlaceholderBuilder.Requirement
            {
                objectEvidence = new[] { Evidence("broken"), Evidence("valid") }
            });
            Assert.That(characters.Select(c => c.Id), Is.EqualTo(new[] { "broken", "valid" }));
            var broken = characters[0];
            Assert.That(broken.Portrait.Path, Is.EqualTo(invalidPath));
            Assert.That(broken.Portrait.Available, Is.False);
            Assert.That(broken.Portrait.Error, Does.Contain("broken").And.Contain(invalidPath));
            Assert.That(broken.Avatar.Path, Is.Empty);
            Assert.That(broken.Avatar.Available, Is.False);
            Assert.That(broken.Avatar.Error, Is.Not.Null.And.Not.Empty);
            // An omitted indoor path follows the same base-portrait compatibility rule as the catalog.
            Assert.That(broken.Indoor.Path, Is.EqualTo(invalidPath));
            Assert.That(broken.Indoor.Error, Does.Contain(invalidPath));
            var valid = characters[1];
            Assert.That(valid.Portrait.Available, Is.True);
            Assert.That(valid.Avatar.Sprite, Is.SameAs(Resources.Load<Sprite>("Characters/sparrow/avatar")));
            Assert.That(valid.Indoor.Sprite, Is.SameAs(valid.Portrait.Sprite));
            Assert.That(valid.Portrait.Error, Is.Null.Or.Empty);
            Assert.That(NarrativeCharacterArtAudit.Summary(characters), Is.EqualTo("部分剧情 ID 缺图"));
        }

        [Test]
        public void NewRequirementUsesOnlyHistoricalObjectIdsAndIgnoresObsoletePathsAndDisplayNames()
        {
            // Old manifests carried paths beside objId. Those paths are historical evidence, not current bindings.
            var historical = JsonUtility.FromJson<NarrativeArtPlaceholderBuilder.Requirement>(
                "{\"objectEvidence\":[{\"objId\":\"sparrow\",\"portraitPath\":\"\","
                + "\"portraitIndoorPath\":\"Characters/obsolete/missing\"},{\"objId\":\"sparrow\"},"
                + "{\"objId\":\"\"},{\"objId\":\"   \"}]}");
            var requirement = new NarrativeArtPlaceholderBuilder.Requirement
            {
                id = "new_requirement", name = "旧名称与现配置无关", configuration = "portraitPath=空",
                objectEvidence = historical.objectEvidence.Concat(new NarrativeArtPlaceholderBuilder.ObjectEvidence[] { null }).ToArray()
            };
            var audit = NarrativeCharacterArtAudit.Load();
            var characters = audit.Inspect(requirement);
            Assert.That(characters.Select(c => c.Id), Is.EqualTo(new[] { "sparrow" }));
            Assert.That(characters[0].Portrait.Path, Is.EqualTo("Characters/sparrow/neutral"));
            Assert.That(characters[0].Portrait.Available, Is.True);
            Assert.That(characters[0].Indoor.Path, Is.EqualTo("Characters/sparrow/seated_placeholder"));
            Assert.That(audit.Inspect(new NarrativeArtPlaceholderBuilder.Requirement { name = "麻雀" }), Is.Empty);
            Assert.That(NarrativeCharacterArtAudit.Summary(Array.Empty<NarrativeCharacterArtAudit.CharacterStatus>()),
                Is.EqualTo("无明确角色映射"));
        }

        [Test]
        public void ChineseAndEnglishEmotesRemainFallbackUntilTheCatalogActuallyBindsExpressions()
        {
            var config = new NarrativeConfiguration();
            config.objects.Add(new NarrativeObjectDefinition { objId = "sparrow", portraitPath = "Characters/sparrow/neutral" });
            var emotes = new[] { "开心", "生气", "happy", "angry", "happy", "平静", "常态", "neutral", "normal", "", null };
            for (int i = 0; i < emotes.Length; i++)
                config.presentations.conversationLines.Add(new NarrativeConversationLineDefinition
                {
                    lineId = "audit.emote." + i, speakerKey = "sparrow", emote = emotes[i], side = i % 2 == 0 ? "none" : "right"
                });
            var status = new NarrativeCharacterArtAudit(config).Inspect("sparrow");
            Assert.That(status.Portrait.Available, Is.True);
            Assert.That(File.Exists(status.Portrait.AssetPath), Is.True, "Existing artwork alone does not bind expressions.");
            Assert.That(status.OnstageConversationLines, Is.EqualTo(5));
            Assert.That(status.OffstageConversationLines, Is.EqualTo(6));
            Assert.That(config.presentations.conversationLines.Where(l => l.side != "none").Any(l => l.emote == "开心" || l.emote == "happy"), Is.False);
            Assert.That(status.FallbackEmotes, Is.EquivalentTo(new[] { "开心", "生气", "happy", "angry" }),
                "Fallback reporting includes all non-base emotes, even expressions used only offstage.");
            var catalog = CharacterCatalog.LoadFromTables();
            try
            {
                var profile = catalog.Find("sparrow");
                Assert.That(profile, Is.Not.Null);
                Assert.That(profile.portraitHappy, Is.Null);
                Assert.That(profile.portraitAngry, Is.Null);
                foreach (var emote in status.FallbackEmotes)
                    Assert.That(profile.PortraitFor(emote), Is.SameAs(status.Portrait.Sprite), emote);
            }
            finally { Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void ReadingAuditLeavesRuntimeRegistryAndConfigurationSourcesUntouched()
        {
            var runtimeBefore = NarrativeRuntime.Instance;
            var runtimeObjectsBefore = Resources.FindObjectsOfTypeAll<NarrativeRuntime>().Select(r => r.GetInstanceID()).ToArray();
            var registry = AssetDatabase.LoadMainAssetAtPath(NarrativeArtPlaceholderBuilder.RegistryPath);
            Assert.That(registry, Is.Not.Null);
            string registryBefore = EditorJsonUtility.ToJson(registry);
            bool registryDirtyBefore = EditorUtility.IsDirty(registry);
            var filesBefore = SourceFingerprints();

            var audit = NarrativeCharacterArtAudit.Load();
            foreach (var requirement in NarrativeArtPlaceholderBuilder.ReadManifest().requirements)
                NarrativeCharacterArtAudit.Summary(audit.Inspect(requirement));
            audit.Inspect("sparrow");
            var missing = audit.Inspect("__NarrativeCharacterArtAuditTests_unknown_object__");
            Assert.That(missing.ObjectExists, Is.False);
            Assert.That(missing.Portrait.Path, Is.Empty);
            Assert.That(missing.Portrait.Error, Is.Not.Null.And.Not.Empty);

            Assert.That(NarrativeRuntime.Instance, Is.SameAs(runtimeBefore));
            Assert.That(Resources.FindObjectsOfTypeAll<NarrativeRuntime>().Select(r => r.GetInstanceID()),
                Is.EquivalentTo(runtimeObjectsBefore));
            Assert.That(EditorJsonUtility.ToJson(registry), Is.EqualTo(registryBefore));
            Assert.That(EditorUtility.IsDirty(registry), Is.EqualTo(registryDirtyBefore));
            var filesAfter = SourceFingerprints();
            Assert.That(filesAfter.Keys, Is.EquivalentTo(filesBefore.Keys), "Source files must not be created or deleted by an audit.");
            foreach (var file in filesBefore)
                Assert.That(filesAfter[file.Key], Is.EqualTo(file.Value), file.Key);
        }

        [Test]
        public void AllObjectsSeparateRequiredOnstageGapsFromNoPortraitActors()
        {
            var audit = NarrativeCharacterArtAudit.Load();
            var configuration = LubanNarrativeLoader.Load();
            var objects = audit.InspectAllObjects();
            Assert.That(objects.Length, Is.EqualTo(configuration.objects.Count));
            Assert.That(objects.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(objects.Length));

            var sorel = audit.Inspect("sorel");
            Assert.That(sorel.ObjectExists, Is.True);
            Assert.That(sorel.NoPortrait, Is.False);
            Assert.That(sorel.CanAppearOnstage, Is.True);
            Assert.That(sorel.Portrait.Available, Is.False);
            Assert.That(audit.MissingOnstageNpcPortraitIds(), Does.Contain("sorel"));

            var genericWorker = audit.Inspect("revolution.actor.young_worker");
            Assert.That(genericWorker.NoPortrait, Is.True);
            Assert.That(genericWorker.CanAppearOnstage, Is.True);
            Assert.That(genericWorker.Portrait.Available, Is.False);
            Assert.That(audit.MissingOnstageNpcPortraitIds(), Does.Not.Contain(genericWorker.Id));
            Assert.That(NarrativeCharacterArtAudit.Summary(new[] { genericWorker }), Is.EqualTo("全部标记 no_portrait"));
        }

        [Test]
        public void GuestVariantInventoryCountsPortraitAndAvatarPoolsWithoutAddingDemandIds()
        {
            string root = Path.Combine(Path.GetTempPath(), "NarrativeGuestVariantAudit_" + Guid.NewGuid().ToString("N"));
            try
            {
                WriteVariant(root, "guest_a", "portrait_01.png");
                WriteVariant(root, "guest_a", "avatar_01.png");
                WriteVariant(root, "guest_b", "portrait_02.png");
                WriteVariant(root, "guest_d", "avatar_03.png");
                WriteVariant(root, "guest_e", "portrait_ignored.png");
                WriteVariant(root, "guest_c", "other_ignored.png");

                var counts = NarrativeCharacterArtAudit.CountGuestVariants(root);
                Assert.That(counts.Pools.Select(pool => pool.GuestId),
                    Is.EqualTo(new[] { "guest_a", "guest_b", "guest_c", "guest_d" }));
                Assert.That(counts.Pools.Select(pool => pool.Portraits), Is.EqualTo(new[] { 1, 1, 0, 0 }));
                Assert.That(counts.Pools.Select(pool => pool.Avatars), Is.EqualTo(new[] { 1, 0, 0, 1 }));
                Assert.That(counts.Portraits, Is.EqualTo(2));
                Assert.That(counts.Avatars, Is.EqualTo(2));
                Assert.That(counts.Total, Is.EqualTo(4));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void WriteVariant(string root, string guestId, string fileName)
        {
            string directory = Path.Combine(root, guestId, "variants");
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, fileName), Array.Empty<byte>());
        }

        private static NarrativeArtPlaceholderBuilder.Requirement Requirement(string id)
            => NarrativeArtPlaceholderBuilder.ReadManifest().requirements.Single(r => r.id == id);

        private static NarrativeArtPlaceholderBuilder.ObjectEvidence Evidence(string id)
            => new NarrativeArtPlaceholderBuilder.ObjectEvidence { objId = id };

        private static Dictionary<string, string> SourceFingerprints()
        {
            var paths = new[]
            {
                NarrativeArtPlaceholderBuilder.RegistryPath,
                NarrativeArtPlaceholderBuilder.RegistryPath + ".meta",
                NarrativeArtPlaceholderBuilder.ManifestPath,
                "Docs/NarrativeAdditionalArtPlaceholderManifest.json",
                "Tools/Characters/character-art-manifest.json"
            }.Concat(Directory.GetFiles("Docs", "Narrative*Art*Manifest.json", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles("Assets/ConfigTables/Data", "*.xlsx", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles("Assets/Settings/Resources/Luban", "*.json", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles("Assets/Resources/Characters/sparrow", "*", SearchOption.AllDirectories));
            using (var sha = SHA256.Create())
                return paths.Distinct(StringComparer.Ordinal).ToDictionary(path => path,
                    path => Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))), StringComparer.Ordinal);
        }
    }
}
