#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    /// <summary>Real DialoguePlayback/DialoguePanel lifecycle coverage, isolated in the shared preview scene.</summary>
    public sealed class DialogueBackgroundContinuityTests
    {
        private const string Tag = "dialogue-background-continuity-tests";
        private Scene scene;
        private object owner;
        private Texture2D first, second;
        private float originalTimeScale;
        private int originalPauseOwners;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalTimeScale = Time.timeScale;
            originalPauseOwners = PresentationPauseRegistry.OwnerCount;
            owner = new object();
            first = new Texture2D(2, 2) { name = "ContinuityBackgroundFirst" };
            second = new Texture2D(2, 2) { name = "ContinuityBackgroundSecond" };
            scene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DialoguePlayback.ReleaseStage(owner);
            UIKit.ClosePanelsByTag(Tag);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            UnityEngine.Object.Destroy(first);
            UnityEngine.Object.Destroy(second);
            Time.timeScale = originalTimeScale;
            yield return null;
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(originalPauseOwners));
        }

        private DialogueScript Page(NarrativeStageVisuals.Entry visual = null, bool continues = true)
            => new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Narrator, "背景连续性测试") },
                continuityOwner: owner, continues: continues, backgroundVisual: visual);

        private static NarrativeStageVisuals.Entry Image(Texture texture, Color tint)
            => new NarrativeStageVisuals.Entry { key = texture.name, texture = texture, tint = tint, fadeSeconds = .3f };

        private DialoguePanel Play(DialogueScript script, Action completed = null)
            => DialoguePlayback.Play(script, completed, null, Tag, PanelCachePolicy.Transient);

        private static RawImage Incoming(DialoguePanel panel)
            => panel.BackgroundLayer.transform.Find("BackgroundCrossfade")?.GetComponent<RawImage>();

        private static void AssertNoIncoming(DialoguePanel panel)
        {
            RawImage layer = Incoming(panel);
            if (layer == null) return;
            Assert.That(layer.enabled, Is.False);
            Assert.That(layer.texture, Is.Null);
            Assert.That(layer.color.a, Is.Zero);
        }

        private static void AssertOpaquePanel(DialoguePanel panel)
            => Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(.001f));

        private static IEnumerator Ready(DialoguePanel panel)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!panel.IsReady)
            {
                Assert.That(panel.IsPlaying, Is.True, "The page must remain active while waiting for entrance/background effects.");
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Dialogue never became ready.");
                yield return null;
            }
        }

        private static IEnumerator CompletePage(DialoguePanel panel)
        {
            yield return Ready(panel);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (panel != null && panel.IsPlaying)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Dialogue did not finish after becoming ready.");
                if (panel.IsReady) panel.Advance();
                yield return null; // Separate typewriter reveal/advance and respect the frame input guard.
            }
        }

        [UnityTest]
        public IEnumerator SameTextureAndTintReusePanelWithoutRestartingBackgroundMotion()
        {
            int completed = 0;
            var panel = Play(Page(Image(first, Color.cyan)), () => completed++);
            AssertOpaquePanel(panel); // Background must already be opaque during initial box entrance.
            yield return CompletePage(panel);
            Assert.That(panel.SeamlessChoicePending, Is.True);
            Assert.That(completed, Is.EqualTo(1));

            // The previous page has completed; its continuity stage is retained, but the
            // panel-level cue API intentionally rejects calls while IsPlaying is false.
            var motion = panel.Effects.Play(PresentationEffectIds.BackgroundPan);
            panel.Effects.Tick(.2f);
            Vector2 position = panel.BackgroundLayer.rectTransform.anchoredPosition;
            Vector3 scale = panel.BackgroundLayer.rectTransform.localScale;
            Assert.That(position.sqrMagnitude, Is.GreaterThan(.01f));
            var next = Play(Page(Image(first, Color.cyan), continues: false), () => completed++);
            Assert.That(next, Is.SameAs(panel));
            Assert.That(motion.IsDone, Is.False, "The second page must not cancel a same-background action.");
            Assert.That(next.BackgroundLayer.rectTransform.anchoredPosition, Is.EqualTo(position));
            Assert.That(next.BackgroundLayer.rectTransform.localScale, Is.EqualTo(scale));
            Assert.That(next.BackgroundLayer.texture, Is.SameAs(first));
            Assert.That(next.BackgroundLayer.color, Is.EqualTo(Color.cyan));
            Assert.That(next.Effects.IsBackgroundTransitioning, Is.False);
            AssertNoIncoming(next);
            AssertOpaquePanel(next);
            next.Effects.Tick(.1f);
            Assert.That(next.BackgroundLayer.rectTransform.anchoredPosition, Is.Not.EqualTo(position));
            Assert.That(motion.IsDone, Is.False);

            yield return CompletePage(next);
            Assert.That(completed, Is.EqualTo(2));
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False);
            DialoguePlayback.ReleaseStage(owner); // Also safe after the terminal page has closed itself.
        }

        [UnityTest]
        public IEnumerator DifferentImagesCrossfadeAboveOpaqueOldLayerWithoutFadingPanel()
        {
            int completed = 0;
            var panel = Play(Page(Image(first, Color.white)), () => completed++);
            yield return CompletePage(panel);
            var next = Play(Page(Image(second, Color.cyan), continues: false), () => completed++);
            Assert.That(next, Is.SameAs(panel));
            Assert.That(next.Effects.IsBackgroundTransitioning, Is.True);
            Assert.That(next.IsReady, Is.False);
            var layer = Incoming(next);
            Assert.That(layer, Is.Not.Null);
            Assert.That(layer.enabled, Is.True);
            Assert.That(layer.transform.parent, Is.SameAs(next.BackgroundLayer.transform));
            Assert.That(next.BackgroundLayer.texture, Is.SameAs(first));
            Assert.That(next.BackgroundLayer.color.a, Is.EqualTo(1f));
            Assert.That(layer.texture, Is.SameAs(second));
            Assert.That(layer.color.a, Is.Zero);
            AssertOpaquePanel(next);

            next.Effects.Tick(.15f);
            Assert.That(next.BackgroundLayer.texture, Is.SameAs(first));
            Assert.That(next.BackgroundLayer.color.a, Is.EqualTo(1f));
            Assert.That(layer.color.a, Is.EqualTo(.5f).Within(.001f));
            AssertOpaquePanel(next);
            next.Advance();
            Assert.That(completed, Is.EqualTo(1), "Input must not finish a page during a background change.");
            yield return Ready(next);
            Assert.That(next.BackgroundLayer.texture, Is.SameAs(second));
            Assert.That(next.BackgroundLayer.color, Is.EqualTo(Color.cyan));
            Assert.That(next.Effects.IsBackgroundTransitioning, Is.False);
            AssertNoIncoming(next);
            AssertOpaquePanel(next);
            yield return CompletePage(next);
            Assert.That(completed, Is.EqualTo(2));
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False);
            DialoguePlayback.ReleaseStage(owner);
        }

        [UnityTest]
        public IEnumerator SceneImageSolidAndUnspecifiedPagesPreserveCorrectBackgroundInheritance()
        {
            // Scene-backed pages hide the narrative RawImage, while unspecified pages inherit
            // the previous stage. An explicit null-texture visual is instead a real solid colour.
            var sceneVisual = NarrativeStageVisuals.Resolve(NarrativeBackgroundKeys.CafeScene);
            Assert.That(sceneVisual.useSceneBackground, Is.True);
            var panel = Play(Page(sceneVisual));
            Assert.That(panel.BackgroundLayer.enabled, Is.False);
            yield return CompletePage(panel);

            Assert.That(Play(Page()), Is.SameAs(panel));
            Assert.That(panel.BackgroundLayer.enabled, Is.False, "An omitted background must inherit the live cafe scene.");
            AssertNoIncoming(panel);
            yield return CompletePage(panel);

            var white = new NarrativeStageVisuals.Entry { key = "test.solid.white", solidColor = true,
                texture = null, tint = Color.white, fadeSeconds = .3f };
            Assert.That(Play(Page(white)), Is.SameAs(panel));
            Assert.That(panel.BackgroundLayer.enabled, Is.True, "Scene null+white must not suppress an explicit white solid.");
            Assert.That(panel.BackgroundLayer.texture, Is.Null);
            Assert.That(panel.BackgroundLayer.color, Is.EqualTo(Color.white));
            AssertOpaquePanel(panel);
            yield return CompletePage(panel);

            Assert.That(Play(Page()), Is.SameAs(panel));
            Assert.That(panel.BackgroundLayer.enabled, Is.True);
            Assert.That(panel.BackgroundLayer.texture, Is.Null);
            Assert.That(panel.BackgroundLayer.color, Is.EqualTo(Color.white));
            Assert.That(panel.Effects.IsBackgroundTransitioning, Is.False);
            yield return CompletePage(panel);

            Assert.That(Play(Page(sceneVisual)), Is.SameAs(panel));
            Assert.That(panel.BackgroundLayer.enabled, Is.False);
            AssertNoIncoming(panel);
            yield return CompletePage(panel);

            Assert.That(Play(Page(Image(first, Color.cyan))), Is.SameAs(panel));
            Assert.That(panel.BackgroundLayer.enabled, Is.True);
            Assert.That(panel.BackgroundLayer.texture, Is.SameAs(first));
            Assert.That(panel.BackgroundLayer.color, Is.EqualTo(Color.cyan));
            AssertOpaquePanel(panel);
            yield return CompletePage(panel);

            Assert.That(Play(Page()), Is.SameAs(panel));
            Assert.That(panel.BackgroundLayer.enabled, Is.True);
            Assert.That(panel.BackgroundLayer.texture, Is.SameAs(first));
            Assert.That(panel.BackgroundLayer.color, Is.EqualTo(Color.cyan));
            Assert.That(panel.Effects.IsBackgroundTransitioning, Is.False);
            AssertNoIncoming(panel);
            yield return CompletePage(panel);
            Assert.That(panel.SeamlessChoicePending, Is.True);
            DialoguePlayback.ReleaseStage(owner);
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False);
            if (panel != null) Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.Zero);
            yield return null;
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(originalPauseOwners));
        }

        [UnityTest]
        public IEnumerator ReleaseStageDuringCrossfadeClearsOwnedLayersAndSuppressesCompletion()
        {
            var panel = Play(Page(Image(first, Color.white)));
            yield return CompletePage(panel);
            bool completed = false;
            Play(Page(Image(second, Color.cyan)), () => completed = true);
            panel.Effects.Tick(.1f);
            RawImage layer = Incoming(panel);
            Assert.That(layer.enabled, Is.True);
            Assert.That(layer.color.a, Is.GreaterThan(0f).And.LessThan(1f));
            DialoguePlayback.ReleaseStage(owner);
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False);
            if (panel != null) Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.Zero);
            if (layer != null)
            {
                Assert.That(layer.enabled, Is.False);
                Assert.That(layer.texture, Is.Null);
            }
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(completed, Is.False);
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(originalPauseOwners));
        }
    }
}
#endif
