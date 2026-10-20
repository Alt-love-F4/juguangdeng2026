using System;
using System.Linq;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    public sealed class NarrativePreviewConsistencyTests
    {
        private NarrativeConfiguration configuration;
        private CharacterCatalog characters;
        private string Resolve(string key) => configuration.texts.FirstOrDefault(t => t.key == key)?.content ?? key ?? "";

        [SetUp]
        public void SetUp()
        {
            configuration = LubanNarrativeLoader.Load();
            characters = CharacterCatalog.LoadFromTables();
            NarrativeCharacterResolver.SetCatalog(characters);
        }

        [TearDown]
        public void TearDown()
        {
            NarrativeCharacterResolver.SetCatalog(null);
            if (characters != null) Object.DestroyImmediate(characters);
        }

        [Test]
        public void AllThirtyAmbientPreviewsUseAuthoredActorsAndKeepSourceIdentity()
        {
            var presentations = configuration.openingPresentations.Where(p => p.presentationId.StartsWith("cafe.ambient.", StringComparison.Ordinal)).ToArray();
            Assert.That(presentations.Length, Is.EqualTo(30));
            foreach (var presentation in presentations)
            {
                Assert.That(NarrativePresentationDataAdapter.TryCreateOpeningDialogue(configuration, presentation.presentationId,
                    out var script, out var error), Is.True, error);
                var source = configuration.openingLines.Where(l => l.presentationId == presentation.presentationId
                    && !string.IsNullOrWhiteSpace(Resolve(l.textKey))).OrderBy(l => l.seq).ToArray();
                Assert.That(script.Count, Is.EqualTo(source.Length), presentation.presentationId);
                Assert.That(script.LeftCharacter.CharacterKey, Is.Empty, "Do not introduce later speakers early.");
                Assert.That(script.RightCharacter.CharacterKey, Is.Empty);
                for (int i = 0; i < source.Length; i++)
                {
                    string text = Resolve(source[i].textKey);
                    int separator = text.IndexOf('：');
                    string name = text.Substring(0, separator);
                    string key = name == "阿黛尔" ? "adele" : name == "{playername}" ? "player"
                        : name == "顾客" || name == "老顾客" ? "guest_a"
                        : name == "帕斯卡尔" ? "pascal" : name == "柯莱特" ? "colette"
                        : name == "麻雀" ? "sparrow" : name == "欧仁" ? "eugene" : null;
                    Assert.That(key, Is.Not.Null, name);
                    var line = script[i];
                    var actor = line.ReplaceLeft ? line.LeftCharacter : line.RightCharacter;
                    Assert.That(actor.CharacterKey, Is.EqualTo(key), source[i].lineId);
                    Assert.That(actor.DisplayName, Is.EqualTo(key == "player" ? "你" : name));
                    Assert.That(actor.Portrait, Is.SameAs(NarrativeCharacterResolver.Find(key).PortraitFor(null)));
                    Assert.That(line.Text, Is.EqualTo(text.Substring(separator + 1).TrimStart()));
                    Assert.That(line.Source.SourceLineId, Is.EqualTo(source[i].lineId));
                    Assert.That(line.Source.Sequence, Is.EqualTo(source[i].seq));
                    Assert.That(line.AutoAdvanceSeconds, Is.EqualTo(source[i].autoAdvanceSeconds));
                }
            }
        }

        [Test]
        public void OrderTemplatesKeepUnboundTokensAndClearlyLabelPreview()
        {
            foreach (string id in new[] { "cafe.ambient.order.ord-01", "cafe.ambient.order.ord-08" })
            {
                Assert.That(NarrativePresentationDataAdapter.TryCreateOpeningDialogue(configuration, id, out var script, out var error), Is.True, error);
                Assert.That(script.Title, Does.EndWith("（模板预览）"));
                Assert.That(script[0].Text, Does.Contain(id.EndsWith("01") ? "{饮品名}" : "{桌号}"));
                Assert.That(script[0].RightCharacter.CharacterKey, Is.EqualTo("guest_a"));
                Assert.That(script[1].LeftCharacter.CharacterKey, Is.EqualTo("adele"));
            }
        }

        [Test]
        public void AuthoredPlayerTokenKeepsIdentityWhenRuntimeNameEqualsNpc()
        {
            string ResolveAsAdele(string key) => NarrativeTextSubstitution.PlayerName(Resolve(key), "阿黛尔");
            Assert.That(NarrativePresentationDataAdapter.TryCreateOpeningDialogue(configuration, ResolveAsAdele,
                "cafe.ambient.chat.chat-01", out var script, out var error), Is.True, error);
            Assert.That(script[0].LeftCharacter.CharacterKey, Is.EqualTo("adele"));
            Assert.That(script[1].LeftCharacter.CharacterKey, Is.EqualTo("player"));
            Assert.That(script[1].Text, Is.EqualTo("怎么又让我猜。"));
        }

        [Test]
        public void UnknownAmbientNameFailsInsteadOfImpersonatingGuestOrAdele()
        {
            var row = configuration.openingLines.First(l => l.presentationId == "cafe.ambient.chat.chat-02");
            configuration.texts.First(t => t.key == row.textKey).content = "不存在的角色：你好。";
            Assert.That(NarrativePresentationDataAdapter.TryCreateOpeningDialogue(configuration, row.presentationId,
                out var script, out var error), Is.False);
            Assert.That(script, Is.Null);
            Assert.That(error, Does.Contain("不存在的角色"));
        }

        [Test]
        public void AmbientMultilineBeatsPreserveTimingGesturesAndResolveBodyPlayerToken()
        {
            var row = configuration.openingLines.First(l => l.presentationId == "cafe.ambient.chat.chat-01");
            configuration.openingLines.RemoveAll(l => l.presentationId == row.presentationId && l != row);
            configuration.texts.First(t => t.key == row.textKey).content = "阿黛尔：你好，{playername}。\n{playername}：你好。";
            row.gesture = "nod";
            row.autoAdvanceSeconds = 1.5f;
            Assert.That(NarrativePresentationDataAdapter.TryCreateOpeningDialogue(configuration, row.presentationId,
                out var script, out var error), Is.True, error);
            Assert.That(script.Count, Is.EqualTo(2));
            Assert.That(script[0].LeftCharacter.CharacterKey, Is.EqualTo("adele"));
            Assert.That(script[0].Text, Is.EqualTo("你好，你。"));
            Assert.That(script[1].LeftCharacter.CharacterKey, Is.EqualTo("player"));
            for (int i = 0; i < script.Count; i++)
            {
                Assert.That(script[i].Gesture, Is.EqualTo(DialogueGesture.Nod));
                Assert.That(script[i].AutoAdvanceSeconds, Is.EqualTo(1.5f));
                Assert.That(script[i].Source.SourceLineId, Is.EqualTo(row.lineId));
            }
        }

        [Test]
        public void OpeningNameKeysSelectObjectIdentityAndActualDisplayNames()
        {
            var presentation = configuration.openingPresentations.First(p => p.presentationId == "first_day_cafe");
            presentation.rightNameKey = configuration.objects.First(o => o.objId == "colette").nameText;
            Assert.That(NarrativePresentationDataAdapter.TryCreateOpeningDialogue(configuration, presentation.presentationId,
                out var script, out var error), Is.True, error);
            Assert.That(script.RightCharacter.CharacterKey, Is.EqualTo("colette"));
            Assert.That(script.RightCharacter.DisplayName, Is.EqualTo(Resolve(presentation.rightNameKey)));
            Assert.That(script.RightCharacter.Portrait, Is.SameAs(NarrativeCharacterResolver.Find("colette").PortraitFor(null)));
        }

        [Test]
        public void ConversationAliasesMatchPresenterWithoutChangingRealActorId()
        {
            var catalog = new NarrativeDebugCatalog(configuration, Resolve);
            var aliases = configuration.presentations.conversationLines.Where(l => !string.IsNullOrWhiteSpace(l.speakerNameKey)
                && !string.IsNullOrWhiteSpace(l.speakerKey)).ToArray();
            Assert.That(aliases, Is.Not.Empty);
            foreach (var source in aliases)
            {
                var entry = catalog.Entries.First(e => e.Kind == "Conversation" && e.PageId == source.pageId);
                var script = catalog.CreateScript(entry);
                var line = Enumerable.Range(0, script.Count).Select(i => script[i]).First(l => l.Source.SourceLineId == source.lineId);
                if (line.Speaker == DialogueSpeaker.Offstage)
                    Assert.That(line.SpeakerNameOverride, Is.EqualTo(Resolve(source.speakerNameKey)), source.lineId);
                else
                {
                    var actor = line.LeftCharacter ?? line.CenterCharacter ?? line.RightCharacter;
                    Assert.That(actor.CharacterKey, Is.EqualTo(source.speakerKey), source.lineId);
                    Assert.That(actor.DisplayName, Is.EqualTo(Resolve(source.speakerNameKey)), source.lineId);
                }
            }
        }

        [Test]
        public void PreviewBackgroundUsesUnambiguousEventSetupAndPropertyOverride()
        {
            var page = new NarrativeConversationPageDefinition { pageId = "preview-event.page", presentationKey = "preview-event", seq = 1 };
            configuration.presentations.conversationPages.Add(page);
            configuration.presentations.conversationLines.Add(new NarrativeConversationLineDefinition
                { lineId = "preview-event.line", pageId = page.pageId, seq = 1, textKey = "opening.line.01" });
            configuration.setups.Add(new NarrativeSetupParamsDefinition { setupKey = "preview-event.setup", slotValues = "bg=cafe.day" });
            var definition = new NarrativeEventDefinition { eventId = "preview-event.id", setupKey = "preview-event.setup", presentationKey = page.presentationKey };
            configuration.events.Add(definition);
            var catalog = new NarrativeDebugCatalog(configuration, Resolve);
            var entry = catalog.Entries.First(e => e.Kind == "Conversation" && e.PageId == page.pageId);
            Assert.That(catalog.CreateScript(entry).BackgroundVisual.key, Is.EqualTo("cafe.day"));
            definition.eventProperties.Add(new NarrativeEventProperty { property = "bg", value = NarrativeBackgroundKeys.CafeScene });
            Assert.That(catalog.CreateScript(entry).BackgroundVisual.key, Is.EqualTo(NarrativeBackgroundKeys.CafeScene));
            configuration.events.Add(new NarrativeEventDefinition { eventId = "preview-other.id", presentationKey = page.presentationKey });
            Assert.That(catalog.CreateScript(entry).BackgroundVisual, Is.Null, "Shared event-specific stages are ambiguous.");
        }

        [Test]
        public void PreviewBackgroundInheritsWithinPresentationAndHonorsCurrentOverride()
        {
            var page = new NarrativeConversationPageDefinition { pageId = "preview-test.page", presentationKey = "preview-test", seq = 3 };
            configuration.presentations.conversationPages.Add(page);
            configuration.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { pageId = "preview-test.previous", presentationKey = page.presentationKey, seq = 1, bgOverride = "cafe.day" });
            configuration.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { pageId = "preview-test.conditional", presentationKey = page.presentationKey, seq = 2,
                    bgOverride = "must-not-load-conditional-background", pageCondKeys = "condition" });
            configuration.presentations.conversationLines.Add(new NarrativeConversationLineDefinition
                { lineId = "preview-test.line", pageId = page.pageId, seq = 1, textKey = "opening.line.01" });
            var catalog = new NarrativeDebugCatalog(configuration, Resolve);
            var entry = catalog.Entries.First(e => e.Kind == "Conversation" && e.PageId == page.pageId);
            Assert.That(catalog.CreateScript(entry).BackgroundVisual.key, Is.EqualTo("cafe.day"));
            page.bgOverride = NarrativeBackgroundKeys.CafeScene;
            Assert.That(catalog.CreateScript(entry).BackgroundVisual.key, Is.EqualTo(NarrativeBackgroundKeys.CafeScene));
        }
    }
}
