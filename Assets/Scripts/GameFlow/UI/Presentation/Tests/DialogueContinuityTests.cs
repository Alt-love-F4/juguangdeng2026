using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow.Tests
{
    public sealed class DialogueContinuityTests
    {
        [Test]
        public void StableIdentityIgnoresDisplayNameAndUnspecifiedExpressionKeepsCurrentSprite()
        {
            var texture = new Texture2D(8, 8);
            var smile = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.zero);
            try
            {
                var current = new DialogueCharacter("adele", "旧名称", smile);
                var omitted = new DialogueCharacter("adele", "新名称", null, false);
                Assert.That(DialogueCharacter.SameIdentity(current, omitted), Is.True);
                Assert.That(DialogueCharacter.Resolve(current, omitted).Portrait, Is.SameAs(smile));
                var explicitDefault = new DialogueCharacter("adele", "阿黛尔", null, true);
                Assert.That(DialogueCharacter.Resolve(current, explicitDefault).Portrait, Is.Null);
                Assert.That(DialogueCharacter.SameIdentity(current, new DialogueCharacter("other", "旧名称", smile)), Is.False);
                Assert.That(DialogueCharacter.SameIdentity(new DialogueCharacter("同名", smile), new DialogueCharacter("同名", smile)), Is.False);
            }
            finally { Object.DestroyImmediate(smile); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void SeekFoldsEarlierStageKeepsTargetCuesAndSourceAndIsolatesPreview()
        {
            var owner = new object();
            var first = new DialogueCharacter("a", "A", null);
            var second = new DialogueCharacter("b", "B", null);
            var source = new DialogueSourceLocation("Conversation", "event", "presentation", "line-3", 30, "page-2");
            var script = new DialogueScript(new[]
            {
                new DialogueLine(DialogueSpeaker.Left, "first", replaceLeft: true, leftCharacter: first, sfxKey: "skipped"),
                new DialogueLine(DialogueSpeaker.Right, "second", replaceRight: true, rightCharacter: second),
                new DialogueLine(DialogueSpeaker.Left, "target", accentFxKey: PresentationEffectIds.ShakeLight, sfxKey: "target-sfx", source: source)
            }, continuityOwner: owner, continues: true);
            var rebuilt = script.RebuildAt(2);
            Assert.That(rebuilt.Count, Is.EqualTo(1));
            Assert.That(rebuilt.LeftCharacter, Is.SameAs(first));
            Assert.That(rebuilt.RightCharacter, Is.SameAs(second));
            Assert.That(rebuilt[0].Source, Is.SameAs(source));
            Assert.That(rebuilt[0].SfxKey, Is.EqualTo("target-sfx"));
            Assert.That(rebuilt[0].AccentFxKey, Is.EqualTo(PresentationEffectIds.ShakeLight));
            Assert.That(rebuilt.IsPreview, Is.True);
            Assert.That(rebuilt.ContinuityOwner, Is.Null);
            Assert.That(rebuilt.Continues, Is.False);
            Assert.That(script.WithFirstPageHint("hint").ContinuityOwner, Is.SameAs(owner));
            Assert.That(script.WithFirstPageHint("hint").Continues, Is.True);
        }

        [Test]
        public void ContinuityCanBeAddedToAndRetainedByHintedDialogueCopies()
        {
            var owner = new object();
            var script = new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Narrator, "教程对话") });
            var continued = script.WithContinuity(owner, continues: true);
            var hinted = continued.WithFirstPageHint("检定提示");
            var final = hinted.WithContinuity(owner, continues: false);

            Assert.That(continued.ContinuityOwner, Is.SameAs(owner));
            Assert.That(continued.Continues, Is.True);
            Assert.That(hinted.ContinuityOwner, Is.SameAs(owner));
            Assert.That(hinted.Continues, Is.True);
            Assert.That(hinted[0].PassiveHint, Is.EqualTo("检定提示"));
            Assert.That(final.ContinuityOwner, Is.SameAs(owner));
            Assert.That(final.Continues, Is.False);
            Assert.Throws<System.ArgumentNullException>(() => script.WithContinuity(null, continues: true));
        }

        [Test]
        public void ConversationStartsEmptySupportsCenterAndPreservesNarratorEffects()
        {
            var data = new NarrativeConversationPresentationData { eventId = "event", presentationKey = "talk", continuityOwner = new object() };
            data.lines.Add(new NarrativeConversationPresentationLine { text = "旁白", lineId = "n", seq = 10,
                accentFxKey = PresentationEffectIds.ShakeLight });
            data.lines.Add(new NarrativeConversationPresentationLine { speakerKey = "test-unknown-character", speakerName = "角色", side = "center", text = "第二句", lineId = "c", seq = 20 });
            var script = NarrativeConversationEventPlayback.CreateScript(data);
            Assert.That(script.LeftCharacter.Portrait, Is.Null);
            Assert.That(script.RightCharacter.Portrait, Is.Null);
            Assert.That(script.CenterCharacter, Is.Null);
            Assert.That(script[0].AccentFxKey, Is.EqualTo(PresentationEffectIds.ShakeLight));
            Assert.That(script[1].ReplaceCenter, Is.True);
            Assert.That(script[1].ReplaceRight, Is.False);
            Assert.That(script[1].CenterCharacter.PortraitSpecified, Is.False);
            Assert.That(script[1].Source.SourceLineId, Is.EqualTo("c"));
            Assert.That(script[1].Source.Sequence, Is.EqualTo(20));
            Assert.That(script.ContinuityOwner, Is.SameAs(data.continuityOwner));
        }
    }
}
