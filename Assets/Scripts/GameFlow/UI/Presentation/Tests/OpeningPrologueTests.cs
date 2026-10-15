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

        [UnityTest]
        public IEnumerator NormalPlaybackRequiresOneAdvancePerBeatAndCompletesOnce()
        {
            OpeningProloguePanel panel = Open();
            SetTiming(panel, .01f, .01f, 0f);
            int completions = 0;
            panel.Play(() => completions++);
            yield return WaitFor(() => panel.CanAdvance);
            for (int i = 0; i < 8; i++)
            {
                int before = panel.CurrentBeatIndex;
                panel.Continue();
                Assert.That(completions, Is.Zero);
                if (i < 7)
                {
                    yield return WaitFor(() => panel.CurrentBeatIndex > before || !panel.IsPlaying);
                    Assert.That(completions, Is.Zero);
                    yield return WaitFor(() => panel.CanAdvance);
                }
            }
            yield return WaitFor(() => !panel.IsPlaying);
            panel.Continue();
            panel.Skip();
            Assert.That(completions, Is.EqualTo(1));
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
            yield return WaitFor(() => panel.CanAdvance);
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
