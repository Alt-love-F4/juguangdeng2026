using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.GameFlow.Tests
{
    public sealed class PresentationDebugCharacterGalleryTests
    {
        [Test]
        public void GeneratedGalleryCoversEveryConfiguredNonWorldObjectInStableUniqueOrder()
        {
            var objects = new cfg.TbNcsObject(JArray.Parse(Resources.Load<TextAsset>("Luban/tbncsobject").text));
            var texts = new cfg.TbText(JArray.Parse(Resources.Load<TextAsset>("Luban/tbtext").text));
            var script = PresentationDebugCharacterGallery.Create(objects, texts);
            var roster = objects.DataList.Where(item => item != null && !string.IsNullOrWhiteSpace(item.ObjId)
                && !string.Equals(item.ObjType, "world", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.ObjId).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();

            Assert.That(script.Count, Is.EqualTo(roster.Length));
            CollectionAssert.AreEqual(roster, Enumerable.Range(0, script.Count).Select(index => script[index].LeftCharacter.CharacterKey));
            Assert.That(Enumerable.Range(0, script.Count).Select(index => script[index].LeftCharacter.CharacterKey)
                .Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(script.Count));
            Assert.That(PresentationDebugCharacterGallery.Id, Is.EqualTo("characters:all"));
            Assert.That(script.Title, Is.EqualTo("全人物立绘巡览"));
            Assert.That(script.IsPreview, Is.True);
            Assert.That(script.InstantPortraitPreview, Is.True);
            Assert.That(script.WithPreviewContext().InstantPortraitPreview, Is.True);
            Assert.That(script.RebuildAt(0).InstantPortraitPreview, Is.True);
            Assert.That(script.Options, Is.Empty);
        }

        [Test]
        public void FixtureUsesConfiguredBaseAndIndoorPortraitsAndExplicitlyClearsEmptySlots()
        {
            var objects = new cfg.TbNcsObject(new JArray(
                Object("z", "npc", "z.name", "Characters/adele/neutral", "Characters/adele/seated_placeholder", ""),
                Object("a", "npc", "a.name", "Characters/colette/neutral", "Characters/colette/seated_placeholder", "")));
            var texts = new cfg.TbText(new JArray(
                new JObject { ["key"] = "z.name", ["text"] = "Zed" },
                new JObject { ["key"] = "a.name", ["text"] = "Ada" }));

            var script = PresentationDebugCharacterGallery.Create(objects, texts);
            Assert.That(script[0].LeftCharacter.CharacterKey, Is.EqualTo("a"));
            Assert.That(script[0].LeftCharacter.DisplayName, Is.EqualTo("Ada"));
            Assert.That(script[0].LeftCharacter.Portrait, Is.Not.Null);
            Assert.That(script[0].RightCharacter.Portrait, Is.Not.Null);
            Assert.That(script[0].ReplaceLeft && script[0].ReplaceRight && script[0].ReplaceCenter, Is.True);
            Assert.That(script[0].CenterCharacter, Is.Null);
            Assert.That(script[0].Text, Does.Contain("1/2"));
            Assert.That(script[0].Text, Does.Contain("点击下一页继续"));
            Assert.That(script[0].Text, Does.Contain("Ada（a）"));
            Assert.That(script[1].LeftCharacter.CharacterKey, Is.EqualTo("z"));
        }

        [Test]
        public void MissingPortraitIsDiagnosticAndDoesNotPreventLaterCharacters()
        {
            var objects = new cfg.TbNcsObject(new JArray(
                Object("a", "npc", "a.name", "Missing/gallery/portrait", "Missing/gallery/indoor", ""),
                Object("b", "npc", "b.name", "Characters/adele/neutral", "", ""),
                Object("empty", "npc", "empty.name", "", "", ""),
                Object("world", "world", "world.name", "Characters/adele/neutral", "", "")));
            var texts = new cfg.TbText(new JArray(
                new JObject { ["key"] = "a.name", ["text"] = "A" },
                new JObject { ["key"] = "b.name", ["text"] = "B" }));

            var script = PresentationDebugCharacterGallery.Create(objects, texts);
            Assert.That(script.Count, Is.EqualTo(3));
            Assert.That(script[0].LeftCharacter.Portrait, Is.Null);
            Assert.That(script[0].Text, Does.Contain("立绘缺失"));
            Assert.That(script[0].Text, Does.Contain("室内立绘缺失"));
            Assert.That(script[1].LeftCharacter.Portrait, Is.Not.Null);
            Assert.That(script[1].Text, Does.Not.Contain("立绘缺失"));
            Assert.That(script[1].LeftCharacter.CharacterKey, Is.EqualTo("b"));
            Assert.That(script[2].LeftCharacter.CharacterKey, Is.EqualTo("empty"));
            Assert.That(script[2].Text, Does.Contain("未配置资源路径"));
        }

        [Test]
        public void GalleryLinesHaveNoChoicesConditionsOrEventEffects()
        {
            var script = PresentationDebugCharacterGallery.Create(
                new cfg.TbNcsObject(new JArray(Object("person", "npc", "name", "Characters/adele/neutral", "", ""))),
                new cfg.TbText(new JArray(new JObject { ["key"] = "name", ["text"] = "Person" })));

            Assert.That(script.Options, Is.Empty);
            for (int i = 0; i < script.Count; i++)
            {
                DialogueLine line = script[i];
                Assert.That(line.PassiveHint, Is.Null);
                Assert.That(line.AccentFxKey, Is.Empty);
                Assert.That(line.AccentTargetKey, Is.Empty);
                Assert.That(line.SfxKey, Is.Empty);
                Assert.That(line.VoiceKey, Is.Empty);
            }
        }

        private static JObject Object(string id, string type, string name, string portrait, string indoor, string icon)
            => new JObject
            {
                ["objId"] = id,
                ["objType"] = type,
                ["nameText"] = name,
                ["iconPath"] = icon,
                ["portraitPath"] = portrait,
                ["portraitIndoorPath"] = indoor,
                ["avtarPath"] = string.Empty,
                ["descText"] = string.Empty,
                ["tags"] = string.Empty
            };
    }
}
