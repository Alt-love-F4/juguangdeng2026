using System.Linq;
using NUnit.Framework;
using Spotlight.Narrative;

namespace Spotlight.GameFlow.Tests
{
    public sealed class RoseStagePresentationTests
    {
        [TestCase("p015")]
        [TestCase("p042")]
        [TestCase("p057")]
        [TestCase("p120")]
        [TestCase("p138")]
        public void CharacterNameAtStartOfNarrationDoesNotMakeItDialogue(string suffix)
        {
            var line = LubanNarrativeLoader.Load().presentations.conversationLines
                .Single(x => x.lineId == "rose.1011.s1." + suffix);
            Assert.That(line.speakerKey, Is.Empty);
            Assert.That(line.side, Is.EqualTo("none"));
            Assert.That(line.emote, Is.Empty);
            Assert.That(line.speakerNameKey, Is.Empty, "Narration must not retain a dialogue name override.");
        }

        [Test]
        public void Rose1011StageEntriesAndDeparturesFollowSourceAnchors()
        {
            var c = LubanNarrativeLoader.Load();
            NarrativeConversationLineDefinition Line(string id) => c.presentations.conversationLines
                .Single(x => x.lineId == "rose.1011.s1." + id);
            void Stage(string id, string left, string right, string center)
            {
                var line = Line(id);
                Assert.That(line.stageLeftKey, Is.EqualTo(left), id + " left");
                Assert.That(line.stageRightKey, Is.EqualTo(right), id + " right");
                Assert.That(line.stageCenterKey, Is.EqualTo(center), id + " center");
            }
            Stage("p003", "adele", "sparrow", "-");
            Stage("p043", "", "", "sylvain");
            Stage("p050", "adele", "sparrow", "sylvain");
            Stage("p052.a", "adele", "sparrow", "sylvain");
            Stage("p069", "", "", "-");
            Stage("p103", "-", "-", "-");
            Stage("p116.a", "adele", "sparrow", "sylvain");
            Stage("p126", "", "", "-");
            Assert.That(Line("p043").side, Is.EqualTo("center"));
            foreach (string id in new[] { "p110.a", "p110.b", "p110.c" })
            {
                Assert.That(Line(id).speakerKey, Is.EqualTo("sylvain"), "The offstage lecturer keeps his identity.");
                Assert.That(Line(id).side, Is.EqualTo("none"), "Portrait lecture must not reintroduce its speaker.");
                Stage(id, "", "", "");
            }
            foreach (var line in c.presentations.conversationLines.Where(x => x.lineId.StartsWith("rose.1011.s1.")))
            {
                if (line.speakerKey == "adele") Assert.That(line.side, Is.EqualTo("left"), line.lineId);
                if (line.speakerKey == "sparrow") Assert.That(line.side, Is.EqualTo("right"), line.lineId);
                if (line.speakerKey == "player") Assert.That(line.side, Is.EqualTo("none"), line.lineId);
            }
        }

        [Test]
        public void Rose1016BothVisitorsArriveBeforePlayersQuestionAndAliasStaysLocal()
        {
            var c = LubanNarrativeLoader.Load();
            var arrival = c.presentations.conversationLines.Single(x => x.lineId == "rose.1016.s1.p008");
            Assert.That(arrival.speakerKey, Is.EqualTo("player"));
            Assert.That(arrival.stageLeftKey, Is.EqualTo("alienor"));
            Assert.That(arrival.stageRightKey, Is.EqualTo("sade"));
            Assert.That(arrival.stageCenterKey, Is.EqualTo("-"));
            foreach (var line in c.presentations.conversationLines.Where(x => x.lineId.StartsWith("rose.1016.s1.") && x.speakerKey == "alienor"))
            {
                Assert.That(line.speakerNameKey, Is.EqualTo("rose.1016.s1.alias.alienor"));
                Assert.That(c.texts.Single(t => t.key == line.speakerNameKey).content, Is.EqualTo("埃莉诺"));
            }
        }

        [Test]
        public void StageClearCommandsAndPortraitLectureAreHonoredByProductionAdapter()
        {
            var data = new NarrativeConversationPresentationData();
            data.lines.Add(new NarrativeConversationPresentationLine
            {
                lineId = "portrait", text = "portrait", side = "none",
                replaceStageLeft = true, replaceStageRight = true, replaceStageCenter = true
            });
            data.lines.Add(new NarrativeConversationPresentationLine
            {
                lineId = "lecture", text = "lecture", speakerKey = "sylvain", speakerName = "西尔万", side = "none"
            });
            var script = NarrativeConversationEventPlayback.CreateScript(data);
            Assert.That(script[0].Speaker, Is.EqualTo(DialogueSpeaker.Narrator));
            Assert.That(script[0].ReplaceLeft && script[0].ReplaceRight && script[0].ReplaceCenter, Is.True);
            Assert.That(script[0].LeftCharacter, Is.Null);
            Assert.That(script[0].RightCharacter, Is.Null);
            Assert.That(script[0].CenterCharacter, Is.Null);
            Assert.That(script[1].Speaker, Is.EqualTo(DialogueSpeaker.Offstage));
            Assert.That(script[1].ReplaceLeft || script[1].ReplaceRight || script[1].ReplaceCenter, Is.False);
        }
    }
}
