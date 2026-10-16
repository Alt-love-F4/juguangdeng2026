using NUnit.Framework;
using Spotlight.Narrative;

namespace Spotlight.GameFlow.Tests
{
    public sealed class ConversationStageSlotTests
    {
        [Test]
        public void UnknownStageObjectIsDiagnosedBeforePlayback()
        {
            var config = LubanNarrativeLoader.Load();
            config.presentations.conversationLines[0].stageLeftKey = "missing.stage.actor";
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(config),
                Has.Some.Contains("unknown stage Object missing.stage.actor"));
        }

        [Test]
        public void NarratorCanPlaceTwoActorsAndRemoveOnlyOne()
        {
            var data = new NarrativeConversationPresentationData();
            data.lines.Add(new NarrativeConversationPresentationLine { text = "arrival", side = "none",
                replaceStageLeft = true, replaceStageRight = true,
                stageLeft = new NarrativeCharacterPresentationData { key = "alienor", name = "埃莉诺" },
                stageRight = new NarrativeCharacterPresentationData { key = "adele", name = "阿黛尔" } });
            data.lines.Add(new NarrativeConversationPresentationLine { text = "departure", side = "none", replaceStageRight = true });
            var script = NarrativeConversationEventPlayback.CreateScript(data);
            Assert.That(script[0].ReplaceLeft && script[0].ReplaceRight, Is.True);
            Assert.That(script[0].LeftCharacter.CharacterKey, Is.EqualTo("alienor"));
            Assert.That(script[0].RightCharacter.CharacterKey, Is.EqualTo("adele"));
            Assert.That(script[1].ReplaceLeft, Is.False);
            Assert.That(script[1].ReplaceRight, Is.True);
            Assert.That(script[1].RightCharacter, Is.Null);
        }

        [Test]
        public void OptionsWithoutLinesRemainSupported()
        {
            var data = new NarrativeConversationPresentationData();
            data.options.Add(new NarrativeConversationPresentationOption { id = "fixture", text = "choose", enabled = true });
            Assert.That(NarrativeConversationEventPlayback.CreateScript(data).Options.Count, Is.EqualTo(1));
        }

        [Test]
        public void OfficialChurchStageCommandsAreAnchoredToSourceLines()
        {
            var config = LubanNarrativeLoader.Load();
            var greeting = config.presentations.conversationLines.Find(l => l.lineId == "church.1104.s1.p015");
            Assert.That(greeting.stageLeftKey, Is.EqualTo("alienor"));
            Assert.That(greeting.stageRightKey, Is.EqualTo("adele"));
            var departure = config.presentations.conversationLines.Find(l => l.lineId == "church.1104.s1.p426");
            Assert.That(departure.stageLeftKey, Is.Empty);
            Assert.That(departure.stageRightKey, Is.EqualTo("-"));
        }
    }
}
