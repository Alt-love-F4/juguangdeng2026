using System;
using System.Linq;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    public sealed class October07S1PresentationTests
    {
        private NarrativeConfiguration config;
        private CharacterCatalog catalog;
        [SetUp] public void Setup()
        {
            config = LubanNarrativeLoader.Load(); catalog = CharacterCatalog.LoadFromTables();
            NarrativeCharacterResolver.SetCatalog(catalog);
        }
        [TearDown] public void Cleanup()
        { NarrativeCharacterResolver.SetCatalog(null); if (catalog != null) Object.DestroyImmediate(catalog); }

        private DialogueScript Page(string id)
        {
            var page = config.presentations.conversationPages.Single(p => p.pageId == id);
            string Text(string key) => config.texts.FirstOrDefault(t => t.key == key)?.content ?? string.Empty;
            var data = new NarrativeConversationPresentationData { eventId = "oct07.s1", presentationKey = page.presentationKey,
                backgroundPath = page.bgOverride, continuityOwner = this, continues = page.seq == 1 };
            foreach (var row in config.presentations.conversationLines.Where(l => l.pageId == id).OrderBy(l => l.seq))
            {
                var speaker = config.objects.Find(o => o.objId == row.speakerKey);
                data.lines.Add(new NarrativeConversationPresentationLine { lineId = row.lineId, pageId = row.pageId, seq = row.seq,
                    speakerKey = row.speakerKey, speakerName = row.speakerKey == "player" ? "自定义店长" : Text(speaker?.nameText),
                    portraitPath = speaker?.portraitPath, side = row.side, emote = row.emote, text = Text(row.textKey),
                    accentFxKey = row.accentFxKey, accentTargetKey = row.accentTargetKey });
            }
            return NarrativeConversationEventPlayback.CreateScript(data);
        }
        [Test] public void ActualS1ConfigurationHasFortyLinesTwoBackgroundPagesAndOneTerminalResult()
        {
            var root = config.events.Single(e => e.eventId == "oct07.s1");
            Assert.That(root.entry, Is.EqualTo(NarrativeEntryPoint.MapAction)); Assert.That(root.blocksSave && root.onceOnly);
            var pages = config.presentations.conversationPages.Where(p => p.presentationKey == root.presentationKey).OrderBy(p => p.seq).ToArray();
            Assert.That(pages.Select(p => p.pageId), Is.EqualTo(new[] { "oct07.s1.cafe", "oct07.s1.street" }));
            var lines = config.presentations.conversationLines.Where(l => pages.Any(p => p.pageId == l.pageId)).ToArray();
            Assert.That(lines.Length, Is.EqualTo(40)); Assert.That(lines.Select(l => l.lineId).Distinct().Count(), Is.EqualTo(40));
            Assert.That(lines.Count(l => l.accentFxKey == PresentationEffectIds.PortraitNod), Is.EqualTo(1));
            Assert.That(lines.Count(l => l.accentFxKey == PresentationEffectIds.BackgroundPan), Is.EqualTo(1));
            Assert.That(lines.Where(l => l.speakerKey == "player" || l.speakerKey == "oct07.passerby.a").All(l => l.side == "none"));
            var result = config.results.Single(r => r.eventId == root.eventId);
            Assert.That(result.resultType, Is.EqualTo(NarrativeResultType.SetFlag)); Assert.That(result.target, Does.Contain("story.oct07.s1.completed"));
        }
        [Test] public void ActualS1OffstageNamesDoNotReplaceAdeleAndOmittedExpressionsRemainUnspecified()
        {
            foreach (string pageId in new[] { "oct07.s1.cafe", "oct07.s1.street" })
            {
                var script = Page(pageId); Assert.That(script.LeftCharacter.Portrait, Is.Null); Assert.That(script.RightCharacter.Portrait, Is.Null);
                for (int i = 0; i < script.Count; i++)
                {
                    var source = config.presentations.conversationLines.Single(l => l.lineId == script[i].Source.SourceLineId);
                    if (source.side == "none" && !string.IsNullOrWhiteSpace(source.speakerKey))
                    {
                        Assert.That(script[i].Speaker, Is.EqualTo(DialogueSpeaker.Offstage));
                        Assert.That(script[i].ReplaceLeft || script[i].ReplaceRight || script[i].ReplaceCenter, Is.False);
                        Assert.That(script[i].SpeakerNameOverride, Is.EqualTo(source.speakerKey == "player" ? "自定义店长" : "路人A"));
                    }
                    if (string.IsNullOrWhiteSpace(source.speakerKey))
                    {
                        Assert.That(script[i].Speaker, Is.EqualTo(DialogueSpeaker.Narrator));
                        Assert.That(script[i].ReplaceLeft || script[i].ReplaceRight || script[i].ReplaceCenter, Is.False);
                    }
                    if (source.speakerKey == "adele")
                    {
                        Assert.That(script[i].ReplaceRight);
                        Assert.That(script[i].RightCharacter.PortraitSpecified, Is.EqualTo(!string.IsNullOrWhiteSpace(source.emote)));
                    }
                }
                Assert.That(script.BackgroundVisual, Is.Not.Null); Assert.That(script.WithPreviewContext().IsPreview);
            }
        }
        [Test] public void BackgroundRegistryMapsOriginalCafeAndExplicitStreetPlaceholder()
        {
            var cafe = NarrativeStageVisuals.Resolve("oct07.cafe.day"); var street = NarrativeStageVisuals.Resolve("oct07.street.day");
            Assert.That(cafe.texture, Is.Not.Null); Assert.That(cafe.placeholder, Is.False);
            Assert.That(street.placeholder); Assert.That(street.texture, Is.Null); Assert.That(street.fadeSeconds, Is.EqualTo(.3f));
            var script = Page("oct07.s1.cafe");
            cafe.tint = Color.red; Assert.That(script.BackgroundVisual.tint, Is.EqualTo(Color.white));
            Assert.That(script.WithPreviewContext().BackgroundVisual.texture, Is.SameAs(script.BackgroundVisual.texture));
        }
        [Test] public void ActualSharedPresetsHaveSingleNodAndBackgroundOnlyPanParameters()
        {
            var library = Resources.Load<PresentationEffectLibrary>("Dialogue/PresentationEffectLibrary");
            var nod = library.Find(PresentationEffectIds.PortraitNod); var pan = library.Find(PresentationEffectIds.BackgroundPan);
            Assert.That(nod.duration, Is.EqualTo(.25f)); Assert.That(nod.strength, Is.GreaterThan(0).And.LessThan(15));
            Assert.That(pan.duration, Is.EqualTo(1.6f)); Assert.That(pan.strength, Is.EqualTo(.02f)); Assert.That(pan.zoom, Is.GreaterThanOrEqualTo(1.04f));
        }
        [Test] public void BackgroundPanLeavesStageAndPortraitsStillAndRestoresPoseOnCompleteOrCancel()
        {
            var root = new GameObject("S1EffectTest", typeof(RectTransform)); var library = ScriptableObject.CreateInstance<PresentationEffectLibrary>();
            PresentationEffects effects = null;
            try
            {
                Image Image(string name) { var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.transform.SetParent(root.transform, false); return go.GetComponent<Image>(); }
                var stage = root.GetComponent<RectTransform>(); var overlay = Image("Overlay"); var left = Image("Left"); var right = Image("Right");
                var bgObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)); bgObject.transform.SetParent(root.transform, false);
                var bg = bgObject.GetComponent<RawImage>(); bg.rectTransform.sizeDelta = new Vector2(1000, 600);
                effects = new PresentationEffects(library, stage, overlay, left, null, right, null, Color.gray, .1f, background: bg);
                var position = stage.anchoredPosition; var scale = stage.localScale; var portraitPosition = right.rectTransform.anchoredPosition;
                var pan = effects.Play(PresentationEffectIds.BackgroundPan); effects.Tick(.4f);
                Assert.That(Mathf.Abs(bg.rectTransform.anchoredPosition.x), Is.EqualTo(20f).Within(.01f));
                Assert.That(stage.anchoredPosition, Is.EqualTo(position)); Assert.That(stage.localScale, Is.EqualTo(scale));
                Assert.That(right.rectTransform.anchoredPosition, Is.EqualTo(portraitPosition));
                effects.Tick(1.2f); Assert.That(pan.IsCompleted); Assert.That(bg.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                var cancelled = effects.Play(PresentationEffectIds.BackgroundPan); effects.Tick(.2f); effects.Reset();
                Assert.That(cancelled.Status, Is.EqualTo(PresentationEffectStatus.Cancelled)); Assert.That(bg.rectTransform.localScale, Is.EqualTo(Vector3.one));
            }
            finally { effects?.Dispose(); Object.DestroyImmediate(root); Object.DestroyImmediate(library); }
        }
    }
}
