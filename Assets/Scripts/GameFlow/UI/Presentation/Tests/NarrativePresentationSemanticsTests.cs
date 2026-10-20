using System;
using System.Linq;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    public sealed class NarrativePresentationSemanticsTests
    {
        private NarrativeConfiguration config;
        private CharacterCatalog catalog;
        [SetUp] public void Setup()
        {
            config = LubanNarrativeLoader.Load();
            catalog = CharacterCatalog.LoadFromTables(config);
            NarrativeCharacterResolver.SetCatalog(catalog);
        }
        [TearDown] public void Cleanup()
        {
            NarrativeCharacterResolver.SetCatalog(null);
            Object.DestroyImmediate(catalog);
        }
        private string Text(string key) => config.texts.Find(t => t.key == key)?.content;
        private DialogueLine Line(NarrativeConversationLineDefinition row)
        {
            var data = new NarrativeConversationPresentationData();
            data.lines.Add(new NarrativeConversationPresentationLine
            {
                lineId=row.lineId, pageId=row.pageId, speakerKey=row.speakerKey, side=row.side,
                emote=row.emote, text=Text(row.textKey), speakerName=Text(row.speakerNameKey)
            });
            return NarrativeConversationEventPlayback.CreateScript(data)[0];
        }
        [Test] public void ConfirmedNamedNarrationNoLongerIntroducesOrSpeaksAsCharacter()
        {
            foreach (string id in new[] { "church.1104.s1.p238.line", "church.1104.s1.p430.line", "church.route.1112.s1.p064.line" })
            {
                var row=config.presentations.conversationLines.Single(l=>l.lineId==id);
                Assert.That(row.speakerKey, Is.Null.Or.Empty, id);
                var line=Line(row);
                Assert.That(line.Speaker, Is.EqualTo(DialogueSpeaker.Narrator), id);
                Assert.That(line.ReplaceLeft || line.ReplaceRight || line.ReplaceCenter, Is.False, id);
            }
        }
        [Test] public void AllDuvalEpilogueSpeechKeepsNameWithoutPortrait()
        {
            var rows=config.presentations.conversationLines.Where(l=>l.pageId.StartsWith("revolution.route.1124.s1.", StringComparison.Ordinal)
                && Text(l.speakerNameKey)=="杜瓦尔").ToArray();
            Assert.That(rows.Length, Is.EqualTo(64));
            foreach(var row in rows)
            {
                Assert.That(row.speakerKey, Is.EqualTo("duval"));
                var line=Line(row);
                Assert.That(line.Speaker, Is.EqualTo(DialogueSpeaker.Offstage));
                Assert.That(line.SpeakerNameOverride, Is.EqualTo("杜瓦尔"));
                Assert.That(line.ReplaceLeft || line.ReplaceRight || line.ReplaceCenter, Is.False);
            }
        }
        [Test] public void AlienorChangesClothesOnlyAfterAuthoredChangeAndBaseAppearanceIsExplicitlyRestored()
        {
            var indoor=Resources.Load<Sprite>("Characters/alienor/indoor");
            Assert.That(indoor, Is.Not.Null);
            var rows=config.presentations.conversationLines.Where(l=>l.pageId.StartsWith("rose.1103.s1.", StringComparison.Ordinal)
                && l.speakerKey=="alienor").ToArray();
            Assert.That(rows.Length, Is.EqualTo(40));
            foreach(var row in rows)
            {
                var actor=Line(row).RightCharacter;
                Assert.That(actor.CharacterKey, Is.EqualTo("alienor"));
                Assert.That(actor.Portrait, Is.SameAs(row.pageId=="rose.1103.s1.rain" ? catalog.Find("alienor").portrait : indoor), row.lineId);
                Assert.That(actor.PortraitSpecified, Is.True, "Do not leak costume through stage continuity.");
            }
            var outside=config.presentations.conversationLines.First(l=>l.speakerKey=="alienor" && !l.pageId.StartsWith("rose.1103.s1.", StringComparison.Ordinal));
            Assert.That(Line(outside).RightCharacter.Portrait, Is.SameAs(catalog.Find("alienor").portrait));
            Assert.That(Line(outside).RightCharacter.PortraitSpecified, Is.True);
        }
    }
}
