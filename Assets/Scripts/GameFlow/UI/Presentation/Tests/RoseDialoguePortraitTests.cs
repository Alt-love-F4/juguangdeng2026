#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YokiFrame;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    public sealed class RoseDialoguePortraitTests
    {
        private NarrativeConfiguration config;
        private CharacterCatalog catalog;
        private Scene scene;
        private DialoguePanel panel;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            config = LubanNarrativeLoader.Load();
            catalog = CharacterCatalog.LoadFromTables();
            NarrativeCharacterResolver.SetCatalog(catalog);
            scene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            panel = UIKit.OpenPanel<DialoguePanel>(UILevel.Pop, tag: "rose-portrait-tests", cachePolicy: PanelCachePolicy.Transient);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            UIKit.ClosePanelsByTag("rose-portrait-tests");
            NarrativeCharacterResolver.SetCatalog(null);
            if (catalog != null) Object.DestroyImmediate(catalog);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        private DialogueScript Script(NarrativeConversationLineDefinition row)
        {
            var actor = config.objects.Find(o => o.objId == row.speakerKey);
            var data = new NarrativeConversationPresentationData();
            data.lines.Add(new NarrativeConversationPresentationLine
            {
                lineId = row.lineId, pageId = row.pageId, seq = row.seq,
                speakerKey = row.speakerKey, speakerName = actor?.nameText,
                portraitPath = actor?.portraitPath, side = row.side, emote = row.emote,
                text = config.texts.Find(t => t.key == row.textKey)?.content
            });
            return NarrativeConversationEventPlayback.CreateScript(data);
        }

        [Test]
        public void AllRoseNpcSpeechUsesAvailablePortraitsAndNarrationStaysOffstage()
        {
            var rows = config.presentations.conversationLines.Where(l => l.pageId.StartsWith("rose.", StringComparison.Ordinal)).ToArray();
            Assert.That(rows.Length, Is.GreaterThan(500));
            foreach (var row in rows)
            {
                var line = Script(row)[0];
                if (string.IsNullOrWhiteSpace(row.speakerKey))
                    Assert.That(line.Speaker, Is.EqualTo(DialogueSpeaker.Narrator), row.lineId);
                else if (row.speakerKey == "player")
                    Assert.That(line.Speaker, Is.EqualTo(DialogueSpeaker.Offstage), row.lineId);
                else
                {
                    Assert.That(row.side, Is.EqualTo("right"), row.lineId);
                    Assert.That(line.ReplaceRight, Is.True, row.lineId);
                    Assert.That(line.RightCharacter.Portrait, Is.Not.Null, row.lineId);
                }
            }
        }

        private IEnumerator ShowRootSpeakers(string root, params string[] expected)
        {
            var rows = config.presentations.conversationLines.Where(l => l.pageId.StartsWith(root + ".", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(l.speakerKey) && l.speakerKey != "player").ToArray();
            Assert.That(rows.Select(l => l.speakerKey).Distinct(), Is.EquivalentTo(expected));
            foreach (string speaker in expected)
            {
                panel.Play(Script(rows.First(l => l.speakerKey == speaker)));
                float deadline = Time.realtimeSinceStartup + 5f;
                while (!panel.IsReady)
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                    yield return null;
                }
                Assert.That(panel.RightPortrait.enabled, Is.True, speaker);
                Assert.That(panel.RightPortrait.gameObject.activeInHierarchy, Is.True, speaker);
                Assert.That(panel.RightPortrait.sprite, Is.SameAs(catalog.Find(speaker).portrait), speaker);
                Assert.That(panel.RightPortrait.color.a, Is.GreaterThan(.9f), speaker);
                Assert.That(panel.RightPortrait.rectTransform.rect.height, Is.GreaterThan(0f), speaker);
                panel.Stop();
            }
        }

        [UnityTest]
        public IEnumerator LetterDestinationShowsButakeAndEnvoy()
        { yield return ShowRootSpeakers("rose.1009.s1", "rose.butake", "rose.envoy"); }

        [UnityTest]
        public IEnumerator October11ShowsAdeleSparrowAndSylvain()
        { yield return ShowRootSpeakers("rose.1011.s1", "adele", "sparrow", "sylvain"); }
    }
}
#endif
