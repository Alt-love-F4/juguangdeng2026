using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    public sealed class OpeningPrologueTests
    {
        private const string TestTag = "opening-prologue-tests";
        private float oldTimeScale;

        [SetUp]
        public void SetUp()
        {
            oldTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UIKit.ClosePanelsByTag(TestTag);
            Time.timeScale = oldTimeScale;
            yield return null;
        }

        [Test]
        public void PrefabIsIndependentAndContainsFinalEightSerializedBeats()
        {
            GameObject prefab = Resources.Load<GameObject>("Art/UIPrefab/OpeningProloguePanel");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<OpeningProloguePanel>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<OpeningProloguePanel>(), Is.Not.InstanceOf<DialoguePanel>());
            Assert.That(prefab.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(600));
            Assert.That(prefab.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<Button>(true), Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<CanvasGroup>(true), Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<TMPro.TMP_Text>(true), Is.Not.Null);
            Assert.That(prefab.GetComponent<OpeningProloguePanel>().BeatCount, Is.EqualTo(8));
        }

        [Test]
        public void EveryBeatExceptTheFinalLineEndsOnComicPanels()
        {
            OpeningProloguePanel prefab = Resources.Load<GameObject>("Art/UIPrefab/OpeningProloguePanel").GetComponent<OpeningProloguePanel>();
            for (int i = 0; i < prefab.BeatCount - 1; i++)
                Assert.That(prefab.BeatHasArt(i), Is.True, $"Beat {i + 1} should end on a comic panel.");
            Assert.That(prefab.BeatHasArt(prefab.BeatCount - 1), Is.False, "The final line has no art.");
            Assert.That(prefab.GetComponentsInChildren<ProloguePanelArt>(true).Length, Is.EqualTo(14));
        }

        [UnityTest]
        public IEnumerator NormalPlaybackRequiresOneAdvancePerBeatAndCompletesOnce()
        {
            OpeningProloguePanel panel = Open();
            SetTiming(panel, .01f, .01f, 0f);
            int completions = 0;
            panel.Play(() => completions++);
            for (int i = 0; i < 8; i++)
            {
                yield return RevealCurrentBeat(panel);
                int before = panel.CurrentBeatIndex;
                panel.Continue();
                Assert.That(completions, Is.Zero);
                if (i < 7)
                {
                    yield return WaitFor(() => panel.CurrentBeatIndex > before || !panel.IsPlaying);
                    Assert.That(completions, Is.Zero);
                }
            }
            yield return WaitFor(() => !panel.IsPlaying);
            panel.Continue();
            panel.Skip();
            Assert.That(completions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FirstInputCompletesRevealAndOnlyNextInputAdvances()
        {
            OpeningProloguePanel panel = Open();
            SetTiming(panel, .01f, .01f, 0f);
            panel.Play(() => { });
            yield return null;
            Assert.That(panel.IsRevealing, Is.True, "Beat 1 starts by revealing text and art.");
            CanvasGroup beatArt = BeatArt(panel, 0);
            panel.Continue();
            yield return WaitFor(() => panel.CanAdvance);
            Assert.That(panel.CurrentBeatIndex, Is.Zero, "Completing the reveal must not turn the page.");
            Assert.That(beatArt.gameObject.activeSelf, Is.True);
            foreach (ProloguePanelArt art in beatArt.GetComponentsInChildren<ProloguePanelArt>())
                Assert.That(art.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(.001f), art.name + " fully shown after reveal.");
            panel.Continue();
            yield return WaitFor(() => panel.CurrentBeatIndex == 1);
            Assert.That(beatArt.gameObject.activeSelf, Is.False, "Previous beat art is cleared on page turn.");
            Assert.That(BeatArt(panel, 1).gameObject.activeSelf, Is.True);
            panel.Skip();
            Assert.That(BeatArt(panel, 1).gameObject.activeSelf, Is.False, "Skip hides every panel.");
        }

        [UnityTest]
        public IEnumerator SkipStopsAnimationAndDuplicateClicksInvokeOneCallback()
        {
            OpeningProloguePanel panel = Open();
            SetTiming(panel, .5f, .5f, 0f);
            int completions = 0;
            panel.Play(() => completions++);
            panel.Continue();
            panel.Continue();
            panel.Skip();
            panel.Skip();
            Assert.That(panel.IsPlaying, Is.False);
            Assert.That(completions, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(completions, Is.EqualTo(1));
            Button skip = (Button)typeof(OpeningProloguePanel).GetField("skipButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);
            Assert.That(skip.interactable, Is.False);
        }

        [UnityTest]
        public IEnumerator PausedScaledTimeStillFadesAndAcceptsSpaceEquivalentContinue()
        {
            OpeningProloguePanel panel = Open();
            SetTiming(panel, .01f, .01f, 0f);
            Time.timeScale = 0f;
            int completions = 0;
            panel.Play(() => completions++);
            yield return RevealCurrentBeat(panel);
            Assert.That(Time.timeScale, Is.Zero);
            panel.Continue();
            Assert.That(completions, Is.Zero);
            panel.Skip();
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.Zero, "The prologue restores the scale it entered with.");
        }

        [UnityTest]
        public IEnumerator ReplayReplacesOldCallbackAndStopCleansIt()
        {
            OpeningProloguePanel panel = Open();
            SetTiming(panel, .01f, .01f, 0f);
            int oldCallback = 0, newCallback = 0;
            panel.Play(() => oldCallback++);
            panel.Play(() => newCallback++);
            panel.Skip();
            Assert.That(oldCallback, Is.Zero);
            Assert.That(newCallback, Is.EqualTo(1));
            panel.Play(() => oldCallback++);
            panel.Stop();
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(oldCallback, Is.Zero);
        }

        private static OpeningProloguePanel Open()
            => UIKit.OpenPanel<OpeningProloguePanel>(UILevel.AlwayTop, tag: TestTag, cachePolicy: PanelCachePolicy.Transient);

        private static void SetTiming(OpeningProloguePanel panel, float fadeIn, float fadeOut, float hold)
        {
            Type type = typeof(OpeningProloguePanel);
            type.GetField("defaultFadeInSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(panel, fadeIn);
            type.GetField("defaultFadeOutSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(panel, fadeOut);
            type.GetField("finalBlackHoldSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(panel, hold);
        }

        private static IEnumerator RevealCurrentBeat(OpeningProloguePanel panel)
        {
            yield return WaitFor(() => panel.IsRevealing || panel.CanAdvance || !panel.IsPlaying);
            if (panel.IsRevealing) panel.Continue();
            yield return WaitFor(() => panel.CanAdvance || !panel.IsPlaying);
            yield return null; // Continue ignores a second press in the frame that fast-forwarded.
        }

        private static CanvasGroup BeatArt(OpeningProloguePanel panel, int index)
        {
            var beats = (OpeningProloguePanel.Beat[])typeof(OpeningProloguePanel).GetField("beats", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);
            return beats[index].art;
        }

        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Opening prologue timed out.");
                yield return null;
            }
        }
    }
}
