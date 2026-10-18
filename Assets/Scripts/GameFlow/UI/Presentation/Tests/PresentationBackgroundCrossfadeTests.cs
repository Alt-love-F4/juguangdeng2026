using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow.Tests
{
    /// <summary>Deterministic runner tests: no scene loading, automatic Update, or cup shader required.</summary>
    public sealed class PresentationBackgroundCrossfadeTests
    {
        private GameObject root;
        private PresentationEffectLibrary library;
        private PresentationEffects effects;
        private RawImage background;
        private Image overlay, left, right;
        private Texture2D first, second, third;
        private CanvasGroup panelAlpha;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("BackgroundCrossfadeTest", typeof(RectTransform), typeof(CanvasGroup));
            panelAlpha = root.GetComponent<CanvasGroup>();
            panelAlpha.alpha = .85f;
            library = ScriptableObject.CreateInstance<PresentationEffectLibrary>();
            overlay = Create<Image>("Overlay");
            left = Create<Image>("Left");
            right = Create<Image>("Right");
            background = Create<RawImage>("Background");
            background.transform.SetAsFirstSibling();
            background.rectTransform.sizeDelta = new Vector2(1000f, 600f);
            first = new Texture2D(2, 2);
            second = new Texture2D(2, 2);
            third = new Texture2D(2, 2);
            background.texture = first;
            effects = new PresentationEffects(library, root.GetComponent<RectTransform>(), overlay,
                left, null, right, null, Color.gray, .1f, background: background);
        }

        private T Create<T>(string name) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            go.transform.SetParent(root.transform, false);
            return go.GetComponent<T>();
        }

        private RawImage Incoming => background.transform.Find("BackgroundCrossfade")?.GetComponent<RawImage>();

        [TearDown]
        public void TearDown()
        {
            effects?.Dispose();
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(library);
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
            UnityEngine.Object.DestroyImmediate(third);
        }

        [Test]
        public void SameVisualDoesNotChangePoseAlphaVisibilityOrRunningMotion()
        {
            var motion = effects.Play(PresentationEffectIds.BackgroundPan);
            effects.Tick(.2f);
            Vector2 position = background.rectTransform.anchoredPosition;
            Vector3 scale = background.rectTransform.localScale;
            var same = effects.SetBackground(first, Color.white, .2f);
            Assert.That(same.IsCompleted, Is.True);
            Assert.That(motion.IsDone, Is.False);
            Assert.That(background.rectTransform.anchoredPosition, Is.EqualTo(position));
            Assert.That(background.rectTransform.localScale, Is.EqualTo(scale));
            Assert.That(background.enabled, Is.True);
            Assert.That(background.color, Is.EqualTo(Color.white));
            Assert.That(Incoming, Is.Null);
            effects.Tick(.1f);
            Assert.That(background.rectTransform.anchoredPosition, Is.Not.EqualTo(position));
        }

        [Test]
        public void IdenticalInFlightTargetReusesHandleAndDoesNotRestartFadeOrMotion()
        {
            var fade = effects.SetBackground(second, Color.cyan, .4f);
            var motion = effects.Play(PresentationEffectIds.BackgroundPan);
            effects.Tick(.1f);
            float alpha = Incoming.color.a;
            Vector2 position = background.rectTransform.anchoredPosition;
            Assert.That(effects.SetBackground(second, Color.cyan, 5f), Is.SameAs(fade));
            Assert.That(Incoming.color.a, Is.EqualTo(alpha));
            Assert.That(background.rectTransform.anchoredPosition, Is.EqualTo(position));
            Assert.That(motion.IsDone, Is.False);
            effects.Tick(.3f);
            Assert.That(fade.IsCompleted, Is.True);
            Assert.That(motion.IsDone, Is.False);
            AssertCleared();
        }

        [Test]
        public void DifferentImageKeepsOldLayerOpaqueWhileNewLayerFadesAboveIt()
        {
            background.uvRect = new Rect(.1f, .2f, .8f, .7f);
            var handle = effects.SetBackground(second, Color.cyan, .2f);
            Assert.That(background.texture, Is.SameAs(first));
            Assert.That(background.color.a, Is.EqualTo(1f));
            Assert.That(Incoming.texture, Is.SameAs(second));
            Assert.That(Incoming.color, Is.EqualTo(new Color(0f, 1f, 1f, 0f)));
            Assert.That(Incoming.transform.parent, Is.SameAs(background.transform));
            Assert.That(Incoming.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(Incoming.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(Incoming.uvRect, Is.EqualTo(background.uvRect));
            Assert.That(Incoming.raycastTarget, Is.False);
            effects.Tick(.1f);
            Assert.That(background.texture, Is.SameAs(first));
            Assert.That(background.color.a, Is.EqualTo(1f));
            Assert.That(Incoming.color.a, Is.EqualTo(.5f).Within(.001f));
            Assert.That(overlay.color.a, Is.Zero);
            Assert.That(panelAlpha.alpha, Is.EqualTo(.85f));
            Assert.That(root.transform.Find("CoffeeCupTransition"), Is.Null);
            effects.Tick(.1f);
            Assert.That(handle.IsCompleted, Is.True, "The authored .2s duration must not be clamped to .8s.");
            Assert.That(effects.IsBackgroundTransitioning, Is.False);
            Assert.That(background.texture, Is.SameAs(second));
            Assert.That(background.color, Is.EqualTo(Color.cyan));
            AssertCleared();
        }

        [Test]
        public void SameTextureWithDifferentTintStillCrossfades()
        {
            var handle = effects.SetBackground(first, Color.red, .2f);
            effects.Tick(.1f);
            Assert.That(handle.IsDone, Is.False);
            Assert.That(background.color, Is.EqualTo(Color.white));
            Assert.That(Incoming.texture, Is.SameAs(first));
            Assert.That(Incoming.color, Is.EqualTo(new Color(1f, 0f, 0f, .5f)));
        }

        [Test]
        public void CurrentActualTintRatherThanStaleRequestedTintDefinesSameVisual()
        {
            background.color = Color.red; // Public panel tint overrides write the RawImage directly.
            Assert.That(effects.SetBackground(first, Color.red, .2f).IsCompleted, Is.True);
            Assert.That(Incoming, Is.Null);
            Assert.That(effects.SetBackground(first, Color.white, .2f).IsDone, Is.False);
            Assert.That(background.color, Is.EqualTo(Color.red));
        }

        [Test]
        public void NullTextureIsSolidColourAndCanFadeInBothDirections()
        {
            var solid = effects.SetBackground(null, Color.black, .2f);
            Assert.That(Incoming.texture, Is.Null);
            Assert.That(Incoming.enabled, Is.True);
            effects.Tick(.2f);
            Assert.That(solid.IsCompleted, Is.True);
            Assert.That(background.texture, Is.Null);
            Assert.That(background.color, Is.EqualTo(Color.black));
            Assert.That(background.enabled, Is.True);
            var image = effects.SetBackground(second, Color.white, .2f);
            effects.Tick(.1f);
            Assert.That(background.texture, Is.Null);
            Assert.That(background.color, Is.EqualTo(Color.black));
            effects.Tick(.1f);
            Assert.That(image.IsCompleted, Is.True);
            Assert.That(background.texture, Is.SameAs(second));
        }

        [Test]
        public void PausedTickDoesNotAdvanceCrossfadeOrFinishHandle()
        {
            var handle = effects.SetBackground(second, Color.white, .2f);
            effects.Tick(.05f);
            float alpha = Incoming.color.a;
            effects.SetPaused(true);
            effects.Tick(10f);
            Assert.That(Incoming.color.a, Is.EqualTo(alpha));
            Assert.That(handle.IsDone, Is.False);
            effects.SetPaused(false);
            effects.Tick(.2f);
            Assert.That(handle.IsCompleted, Is.True);
            AssertCleared();
        }

        [Test]
        public void ReentryCancelsOldHandleCommitsOldTargetAndReusesSingleLayer()
        {
            var old = effects.SetBackground(second, Color.red, .4f);
            effects.Tick(.1f);
            RawImage layer = Incoming;
            var current = effects.SetBackground(third, Color.cyan, .2f);
            Assert.That(old.Status, Is.EqualTo(PresentationEffectStatus.Cancelled));
            Assert.That(background.texture, Is.SameAs(second));
            Assert.That(background.color, Is.EqualTo(Color.red));
            Assert.That(Incoming, Is.SameAs(layer));
            Assert.That(Incoming.texture, Is.SameAs(third));
            Assert.That(Incoming.color.a, Is.Zero);
            effects.Tick(1f); // A large tick must both commit and clear the overlay.
            Assert.That(current.IsCompleted, Is.True);
            Assert.That(background.texture, Is.SameAs(third));
            AssertCleared();
        }

        [Test]
        public void ResetCancelsTransitionRestoresMotionAndAllowsReuse()
        {
            var handle = effects.SetBackground(second, Color.cyan, .2f);
            var motion = effects.Play(PresentationEffectIds.BackgroundPan);
            effects.Tick(.1f);
            effects.Reset();
            Assert.That(handle.Status, Is.EqualTo(PresentationEffectStatus.Cancelled));
            Assert.That(motion.Status, Is.EqualTo(PresentationEffectStatus.Cancelled));
            Assert.That(effects.IsBackgroundTransitioning, Is.False);
            Assert.That(background.texture, Is.SameAs(second));
            Assert.That(background.color, Is.EqualTo(Color.cyan));
            Assert.That(background.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(background.rectTransform.localScale, Is.EqualTo(Vector3.one));
            AssertCleared();
            Assert.That(effects.SetBackground(third, Color.white).IsCompleted, Is.True);
            Assert.That(background.texture, Is.SameAs(third));
        }

        [Test]
        public void HideBackingDoesNotReenableHiddenBackgroundDuringCancellation()
        {
            var handle = effects.SetBackground(second, Color.cyan, .2f);
            background.enabled = false;
            effects.HideBackgroundBacking();
            Assert.That(handle.Status, Is.EqualTo(PresentationEffectStatus.Cancelled));
            Assert.That(background.enabled, Is.False);
            AssertCleared();
        }

        [Test]
        public void RevealAndCloseAffectOnlyBackgroundAndRespectPauseAndReset()
        {
            var reveal = effects.RevealBackground(.1f);
            Assert.That(background.color.a, Is.Zero);
            effects.Tick(.05f);
            Assert.That(background.color.a, Is.EqualTo(.5f).Within(.001f));
            Assert.That(panelAlpha.alpha, Is.EqualTo(.85f));
            effects.SetPaused(true);
            effects.Tick(1f);
            Assert.That(reveal.IsDone, Is.False);
            effects.SetPaused(false);
            effects.Tick(.05f);
            Assert.That(reveal.IsCompleted, Is.True);
            var close = effects.CloseBackground(.1f);
            effects.Tick(.1f);
            Assert.That(close.IsCompleted, Is.True);
            Assert.That(background.color.a, Is.Zero);
            Assert.That(overlay.color.a, Is.Zero);
            Assert.That(panelAlpha.alpha, Is.EqualTo(.85f));
            effects.Reset();
            Assert.That(background.color.a, Is.EqualTo(1f));
            Assert.That(effects.CloseBackground(0f).IsCompleted, Is.True);
            Assert.That(background.color.a, Is.Zero);
            Assert.That(effects.RevealBackground(0f).IsCompleted, Is.True);
            Assert.That(background.color.a, Is.EqualTo(1f));
        }

        [Test]
        public void RevealAndCloseCancelPreviousBackgroundWorkWithoutLeavingIncomingVisible()
        {
            var fade = effects.SetBackground(second, Color.cyan, .4f);
            effects.Tick(.1f);
            var reveal = effects.RevealBackground(.2f);
            Assert.That(fade.Status, Is.EqualTo(PresentationEffectStatus.Cancelled));
            Assert.That(background.texture, Is.SameAs(second));
            Assert.That(background.color.a, Is.Zero);
            AssertCleared();
            effects.Tick(.1f);
            var close = effects.CloseBackground(.2f);
            Assert.That(reveal.Status, Is.EqualTo(PresentationEffectStatus.Cancelled));
            effects.Tick(.1f);
            Assert.That(background.color.a, Is.EqualTo(.5f).Within(.001f));
            effects.Reset();
            Assert.That(close.Status, Is.EqualTo(PresentationEffectStatus.Cancelled));
            Assert.That(background.color, Is.EqualTo(Color.cyan));
            AssertCleared();
        }

        [Test]
        public void ImmediateOrInitiallyHiddenBackgroundDoesNotCreateFadeLayer()
        {
            Assert.That(effects.SetBackground(second, Color.white, 0f).IsCompleted, Is.True);
            Assert.That(Incoming, Is.Null);
            background.enabled = false;
            Assert.That(effects.SetBackground(third, Color.cyan, .2f).IsCompleted, Is.True);
            Assert.That(background.enabled, Is.True);
            Assert.That(background.texture, Is.SameAs(third));
            Assert.That(Incoming, Is.Null);
        }

        [Test]
        public void HiddenIdenticalImageIsReenabledAndHiddenNullWhiteBecomesSolidPlaceholder()
        {
            background.enabled = false;
            Assert.That(effects.SetBackground(first, Color.white, .2f).IsCompleted, Is.True);
            Assert.That(background.enabled, Is.True);
            background.texture = null;
            background.color = Color.white;
            background.enabled = false;
            Assert.That(effects.SetBackground(null, Color.white, .2f).IsCompleted, Is.True);
            Assert.That(background.enabled, Is.True);
            Assert.That(background.texture, Is.Null);
            Assert.That(background.color, Is.EqualTo(Color.white));
            Assert.That(Incoming, Is.Null);
        }

        [Test]
        public void InvalidDurationsLeaveActiveTransitionIntact()
        {
            var handle = effects.SetBackground(second, Color.white, .2f);
            Assert.Throws<ArgumentOutOfRangeException>(() => effects.SetBackground(third, Color.white, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => effects.RevealBackground(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => effects.CloseBackground(-1f));
            Assert.That(handle.IsDone, Is.False);
            Assert.That(Incoming.texture, Is.SameAs(second));
        }

        [Test]
        public void DisposeCancelsAndReleasesIncomingTextureWithoutTouchingPanelAlpha()
        {
            var handle = effects.SetBackground(second, Color.white, .2f);
            RawImage layer = Incoming;
            effects.Dispose();
            effects.Dispose();
            Assert.That(handle.Status, Is.EqualTo(PresentationEffectStatus.Cancelled));
            Assert.That(effects.IsBackgroundTransitioning, Is.False);
            if (layer != null) // Play Mode destroys on the next frame; cleanup must be immediate.
            {
                Assert.That(layer.gameObject.activeSelf, Is.False);
                Assert.That(layer.enabled, Is.False);
                Assert.That(layer.texture, Is.Null);
            }
            Assert.That(panelAlpha.alpha, Is.EqualTo(.85f));
            Assert.Throws<ObjectDisposedException>(() => effects.SetBackground(first, Color.white));
        }

        private void AssertCleared()
        {
            Assert.That(Incoming, Is.Not.Null);
            Assert.That(Incoming.enabled, Is.False);
            Assert.That(Incoming.texture, Is.Null);
            Assert.That(Incoming.color.a, Is.Zero);
        }
    }
}
