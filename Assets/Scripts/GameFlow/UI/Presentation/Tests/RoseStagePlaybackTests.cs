#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    /// <summary>Real shared-scene playback; expectations require the reviewed Rose stage migration.</summary>
    public sealed class RoseStagePlaybackTests
    {
        private const string Rose1011 = "rose.1011.s1";
        private const string Rose1016 = "rose.1016.s1";
        private const float TimeoutSeconds = 30f;
        private NarrativeChapterPreview preview;
        private DialoguePanel stage;
        private Scene scene, previousScene;
        private int[] previousScenes, previousHosts;
        private bool ownsScene, captured;
        private float previousTimeScale;
        private Func<bool> previousDialogueBlocker, previousPresenterBlocker;
        private NarrativeRuntime previousRuntime;
        private int pauseOwners, completions, sourceFrame;
        private string observedSource;
        private Queue<string> choices;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(Application.isPlaying, Is.True, "Run this fixture as Play Mode tests.");
            previousScene = SceneManager.GetActiveScene();
            previousScenes = SceneHandles();
            previousHosts = PresenterHosts();
            previousTimeScale = Time.timeScale;
            previousDialogueBlocker = DialoguePanel.ExternalInputBlocked;
            previousPresenterBlocker = NarrativePresenter.ExternalInputBlocked;
            previousRuntime = NarrativeRuntime.Instance;
            pauseOwners = PresentationPauseRegistry.OwnerCount;
            completions = 0;
            captured = true;
            ownsScene = false;
            stage = null;
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False, "Never replace another owner's dialogue.");
            Assert.That(UIKit.GetPanel<ReadingPanel>()?.IsPlaying ?? false, Is.False);
            Assert.That(previousRuntime != null && previousRuntime.IsReady && previousRuntime.Controller.IsBusy,
                Is.False, "Never interrupt a formal chapter.");

            scene = SceneManager.GetSceneByPath(PresentationDebugSceneController.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                ownsScene = true;
                scene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                    new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null;
            }
            Assert.That(scene.isLoaded, Is.True);
            var controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PresentationDebugSceneController>(true)).Single();
            Assert.That(controller.CurrentPanel, Is.Null, "Do not stop or replace a scene-owned preview.");
            SceneManager.SetActiveScene(scene);
            DialoguePanel.ExternalInputBlocked = null;
            NarrativePresenter.ExternalInputBlocked = null;
            // All playback uses unscaled time. Preserve even a caller's paused/slow timeScale.
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!captured) yield break;
            try
            {
                // Teardown must restore scene setup even when a playback assertion has already failed.
                if (preview != null)
                {
                    preview.Dispose();
                    preview.Dispose();
                    preview = null;
                    yield return null;
                }
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                if (ownsScene && scene.IsValid() && scene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
            finally
            {
                DialoguePanel.ExternalInputBlocked = previousDialogueBlocker;
                NarrativePresenter.ExternalInputBlocked = previousPresenterBlocker;
                Time.timeScale = previousTimeScale;
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                captured = false;
            }
            Assert.That(DialoguePanel.ExternalInputBlocked, Is.SameAs(previousDialogueBlocker));
            Assert.That(NarrativePresenter.ExternalInputBlocked, Is.SameAs(previousPresenterBlocker));
            Assert.That(Time.timeScale, Is.EqualTo(previousTimeScale));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(previousScene));
            CollectionAssert.AreEqual(previousScenes, SceneHandles(), "Restore the caller's loaded scene setup.");
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners));
            if (stage != null)
            {
                Assert.That(stage.CurrentSource, Is.Null);
                AssertStage(null, null, null);
                Assert.That(DialoguePanel.IsAnyPlaying, Is.False);
                Assert.That(UIKit.GetPanel<ReadingPanel>()?.IsPlaying ?? false, Is.False);
            }
            CollectionAssert.AreEqual(previousHosts, PresenterHosts(), "Dispose must release the preview presenter host.");
            Assert.That(NarrativeRuntime.Instance, Is.SameAs(previousRuntime));
        }

        [UnityTest]
        public IEnumerator PreOrderKeepsTwoActorsUntilSylvainActuallySpeaksAtP043()
        {
            Begin("p020", "p032");
            preview.PlayRose1011Construction(6, () => completions++);
            yield return Reach(Rose1011, "p003");
            AssertStage("adele", "sparrow", null);
            yield return Reach(Rose1011, "p042", _ => AssertStage("adele", "sparrow", null));
            AssertStage("adele", "sparrow", null);
            yield return Reach(Rose1011, "p043");
            AssertStage("adele", "sparrow", "sylvain");
            yield return DisposePreview();
        }

        [UnityTest]
        public IEnumerator IndependentPostBrewEntriesRestoreThreeActorsAndQ3EarlyExitClearsOnlyCenter()
        {
            foreach (bool lowGrade in new[] { false, true })
            {
                Begin("p060");
                preview.PlayRose1011PostBrewConstruction(6, lowGrade, completed: () => completions++);
                yield return Reach(Rose1011, lowGrade ? "p050" : "p052.a");
                AssertStage("adele", "sparrow", "sylvain");
                yield return Reach(Rose1011, "p068");
                AssertStage("adele", "sparrow", "sylvain");
                yield return Reach(Rose1011, "p069");
                AssertStage("adele", "sparrow", null);
                yield return DisposePreview();
            }
        }

        [UnityTest]
        public IEnumerator LongRouteClearsPortraitLectureRestoresTrioThenKeepsSylvainGoneAfterCard()
        {
            Begin("p062", "p080");
            preview.PlayRose1011PostBrewConstruction(6, false, true, true, () => completions++);
            yield return Reach(Rose1011, "p052.a");
            AssertStage("adele", "sparrow", "sylvain");
            yield return Reach(Rose1011, "p103");
            AssertStage(null, null, null);
            foreach (string line in new[] { "p110.a", "p110.b", "p110.c" })
                yield return Reach(Rose1011, line, _ => AssertStage(null, null, null));
            yield return Reach(Rose1011, "p116.a");
            AssertStage("adele", "sparrow", "sylvain");
            yield return Reach(Rose1011, "p126");
            AssertStage("adele", "sparrow", null);
            yield return ReadCard();
            yield return Reach(Rose1011, "p132", _ => AssertStage("adele", "sparrow", null));
            yield return Reach(Rose1011, "p140", _ => AssertStage("adele", "sparrow", null));
            yield return DisposePreview();
        }

        [UnityTest]
        public IEnumerator Rose1016StartsEmptyAndBothVisitorsArriveBeforeTheirDialogueWithLocalAlias()
        {
            Begin();
            preview.PlayRose1016Construction(7, () => completions++);
            yield return Reach(Rose1016, "p004");
            AssertStage(null, null, null);
            yield return Reach(Rose1016, "p008");
            AssertStage("alienor", "sade", null);
            foreach (string line in new[] { "p009", "p014" })
            {
                yield return Reach(Rose1016, line);
                AssertStage("alienor", "sade", null);
                Assert.That(stage.LeftCharacter.DisplayName, Is.EqualTo("埃莉诺"), line + " must use the local alias.");
            }
            yield return DisposePreview();
        }

        [UnityTest]
        public IEnumerator Rose1012IndependentStagesAndHistoryRoutesKeepActorsAtSourceAnchors()
        {
            foreach (string isolated in new[] { "preparation", "brew_retry" })
            {
                Begin();
                preview.PlayRose1012Construction(isolated);
                yield return Reach("rose.1012.s1", isolated == "preparation" ? "p004.a" : "p016");
                AssertStage("adele", null, null);
                yield return DisposePreview();
            }
            foreach (bool appreciation in new[] { false, true })
            foreach (bool helped in new[] { false, true })
            {
                Begin();
                preview.PlayRose1012Construction("post_brew", appreciation, helped, true);
                yield return Reach("rose.1012.s1", "p014");
                AssertStage("adele", null, null);
                yield return Reach("rose.1012.s1", "p029");
                AssertStage("adele", null, null);
                yield return Reach("rose.1012.s1", "p032");
                AssertStage(null, "alienor", null);
                Assert.That(stage.RightCharacter.DisplayName, Is.EqualTo("埃莉诺"));
                if (!appreciation)
                {
                    yield return Reach("rose.1012.s1", "p050");
                    AssertStage(null, null, null);
                    yield return Reach("rose.1012.s1", "p052");
                    AssertStage("adele", null, null);
                    yield return Reach("rose.1012.s1", "p053");
                }
                else
                {
                    yield return Reach("rose.1012.s1", "p060");
                    AssertStage("adele", "alienor", null);
                    yield return Reach("rose.1012.s1", "p064");
                    AssertStage(null, "alienor", null);
                    yield return Reach("rose.1012.s1", helped ? "p074" : "p083");
                    AssertStage(null, "alienor", null);
                    yield return Reach("rose.1012.s1", "p085");
                    AssertStage("adele", "alienor", null);
                }
                yield return DisposePreview();
            }
        }

        private void Begin(params string[] selected)
        {
            Assert.That(preview, Is.Null);
            choices = new Queue<string>(selected.Select(id => Rose1011 + ".choice." + id));
            observedSource = null;
            sourceFrame = Time.frameCount;
            preview = new NarrativeChapterPreview();
        }

        private IEnumerator Reach(string chapter, string suffix, Action<DialoguePanel> inspect = null)
        {
            string target = chapter + "." + suffix;
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (true)
            {
                CheckDeadline(deadline, "source " + target);
                TrackStage();
                if (stage != null && stage.IsPlaying && stage.CurrentSource != null)
                {
                    Assert.That(stage.CurrentSource.EventId, Is.EqualTo(chapter));
                    inspect?.Invoke(stage);
                    if (stage.CurrentSource.SourceLineId == target)
                    {
                        // Observe the actual source for a complete frame, not a stale pre-transition slot.
                        if (stage.IsReady && !stage.IsTyping && Time.frameCount > sourceFrame) yield break;
                    }
                    else AdvanceDialogue();
                }
                else AdvanceDialogue(); // Choice-only pages have no source location.
                yield return null;
            }
        }

        private void TrackStage()
        {
            var current = DialoguePanel.Active;
            if (current != null) stage = current;
            if (stage == null || !stage.IsPlaying) return;
            stage.SetPreviewReading(0f, false);
            string source = stage.CurrentSource?.SourceLineId;
            if (source == observedSource) return;
            observedSource = source;
            sourceFrame = Time.frameCount;
        }

        private void AdvanceDialogue()
        {
            if (stage == null || !stage.IsPlaying || !stage.IsReady || stage.IsTyping
                || stage.ReadingPaused || Time.frameCount <= sourceFrame) return;
            if (stage.IsChoosing)
            {
                Assert.That(choices.Count, Is.GreaterThan(0), "Unexpected decision at " + observedSource);
                Assert.That(stage.SelectOption(choices.Dequeue()), Is.True, "The expected source choice must be visible and unlocked.");
            }
            else stage.Advance();
        }

        private IEnumerator ReadCard()
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            ReadingPanel card;
            while ((card = UIKit.GetPanel<ReadingPanel>()) == null || !card.IsPlaying)
            {
                CheckDeadline(deadline, "card ReadingPanel");
                TrackStage();
                AssertStage("adele", "sparrow", null);
                AdvanceDialogue();
                yield return null;
            }
            var seenLines = new HashSet<int>();
            while (card.IsPlaying)
            {
                CheckDeadline(deadline, "card completion");
                AssertStage("adele", "sparrow", null);
                Assert.That(stage.CurrentSource?.SourceLineId, Is.EqualTo(Rose1011 + ".p126"));
                Assert.That(stage.ReadingPaused, Is.True, "The card must retain its own dialogue stage.");
                seenLines.Add(card.LineIndex);
                // ReadingPanel has no public IsReady; its entrance CanvasGroup plus public typing state
                // gate real Advance calls. Never modify its speed/private fields or simulate completion.
                if (card.GetComponent<CanvasGroup>().alpha >= 1f)
                {
                    bool wasTyping = card.IsTyping;
                    int row = card.LineIndex;
                    card.Advance();
                    if (wasTyping) Assert.That(card.LineIndex, Is.EqualTo(row), "Reveal must not consume a card row.");
                }
                yield return null;
            }
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, seenLines, "Read all three actual card rows.");
        }

        private void AssertStage(string left, string right, string center)
        {
            Assert.That(stage, Is.Not.Null);
            AssertSlot(stage.LeftCharacter, left, "left");
            AssertSlot(stage.RightCharacter, right, "right");
            AssertSlot(stage.CenterCharacter, center, "center");
            // Missing sparrow/sylvain art must not be replaced with another actor's portrait.
            // Identity/empty-slot assertions deliberately do not require a sprite to exist.
        }

        private static void AssertSlot(DialogueCharacter actual, string expected, string slot)
            => Assert.That(actual?.CharacterKey ?? string.Empty, Is.EqualTo(expected ?? string.Empty), slot + " identity");

        private IEnumerator DisposePreview()
        {
            var owned = preview;
            owned.Dispose();
            owned.Dispose();
            preview = null;
            yield return null;
            Assert.That(owned.IsPlaying, Is.False);
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False);
            Assert.That(UIKit.GetPanel<ReadingPanel>()?.IsPlaying ?? false, Is.False);
            if (stage != null)
            {
                Assert.That(stage.CurrentSource, Is.Null);
                AssertStage(null, null, null);
            }
            Assert.That(completions, Is.Zero, "Disposal of a preview is not chapter completion.");
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners), "Preview leaked a pause owner.");
            Assert.That(Time.timeScale, Is.EqualTo(previousTimeScale), "Preview must not own simulation time.");
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
            Assert.That(NarrativeRuntime.Instance, Is.SameAs(previousRuntime));
            CollectionAssert.AreEqual(previousHosts, PresenterHosts());
        }

        private void CheckDeadline(float deadline, string operation)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), operation + " timed out at " + preview.Source);
            Assert.That(Time.timeScale, Is.EqualTo(previousTimeScale));
        }

        private static int[] SceneHandles()
            => Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).handle.GetHashCode()).ToArray();

        private static int[] PresenterHosts()
            => Resources.FindObjectsOfTypeAll<NarrativePresentationUI>().Select(host => host.GetInstanceID()).OrderBy(id => id).ToArray();
    }
}
#endif
