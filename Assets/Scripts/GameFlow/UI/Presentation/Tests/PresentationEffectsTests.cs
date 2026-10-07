#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    public sealed class PresentationEffectsTests
    {
        private Scene scene;
        private PresentationDebugSceneController controller;
        private DialoguePanel panel;
        private float timeScale;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            timeScale = Time.timeScale;
            scene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var candidate = root.GetComponent<PresentationDebugSceneController>();
                if (candidate != null) controller = candidate;
            }
            Assert.That(controller, Is.Not.Null, "Shared scene controller must be loaded before preview launch.");
            controller.PlayEffects();
            panel = controller.CurrentPanel as DialoguePanel;
            Assert.That(panel, Is.Not.Null);
            yield return Ready();
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (controller != null) controller.StopCurrent();
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Time.timeScale = timeScale;
        }
        private IEnumerator Ready()
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!panel.IsReady)
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
        }
        [UnityTest]
        public IEnumerator DefaultEntranceMirrorsOnlyRightPortraitAndKeepsNameOnLeft()
        {
            Assert.That(panel.RightPortrait.transform.localScale.x, Is.LessThan(0));
            Assert.That(panel.LeftPortrait.transform.localScale.x, Is.GreaterThan(0));
            Assert.That(panel.EffectStage.localScale, Is.EqualTo(Vector3.one));
            foreach (TMPro.TMP_Text label in panel.GetComponentsInChildren<TMPro.TMP_Text>())
                Assert.That(label.transform.localScale.x, Is.GreaterThan(0));
            Assert.That(panel.LeftPortrait.color.a, Is.GreaterThan(.99f));
            yield return null;
        }
        [UnityTest]
        public IEnumerator EntranceAndCharacterReplacementFinishBeforeTextStarts()
        {
            DialogueScript sample = PresentationEffectsPreview.Create();
            var replacement = new DialogueCharacter("npc_third", "新访客", sample.LeftCharacter.Portrait);
            panel.Play(new DialogueScript(new[]
            {
                new DialogueLine(DialogueSpeaker.Left, "第一句"),
                new DialogueLine(DialogueSpeaker.Right, "第二句", replaceRight: true, rightCharacter: replacement)
            }, sample.LeftCharacter, sample.RightCharacter));
            Assert.That(panel.IsReady, Is.False);
            foreach (TMPro.TMP_Text text in panel.GetComponentsInChildren<TMPro.TMP_Text>())
                if (text.name == "Dialogue") Assert.That(text.maxVisibleCharacters, Is.Zero);
            yield return Ready();
            panel.Advance(); yield return null; panel.Advance();
            Assert.That(panel.IsReady, Is.False);
            yield return new WaitForSecondsRealtime(.85f);
            Assert.That(panel.RightCharacter, Is.SameAs(replacement));
            Assert.That(panel.RightPortrait.sprite, Is.SameAs(replacement.Portrait));
            Assert.That(panel.LineIndex, Is.EqualTo(1));
            Assert.That(panel.IsReady, Is.True);
        }
        [UnityTest]
        public IEnumerator LineAccentUsesNcsTargetKeyAndDoesNotDependOnSpeakerSlot()
        {
            var sample = PresentationEffectsPreview.Create();
            panel.Play(new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Left, "由左侧说话，右侧受惊",
                accentFxKey: PresentationEffectIds.AccentShakeStartle, accentTargetKey: "npc_guest") },
                sample.LeftCharacter, sample.RightCharacter));
            yield return Ready();
            RectTransform right = panel.RightPortrait.transform.parent as RectTransform;
            RectTransform left = panel.LeftPortrait.transform.parent as RectTransform;
            Vector2 rightBase = right.anchoredPosition;
            Vector2 leftBase = left.anchoredPosition;
            panel.Effects.Tick(.015f);
            Assert.That(Vector2.Distance(right.anchoredPosition, rightBase), Is.GreaterThan(.1f));
            Assert.That(Vector2.Distance(left.anchoredPosition, leftBase), Is.LessThan(.01f));
            Assert.Throws<ArgumentException>(() => panel.PlayEffect(PresentationEffectIds.AccentShakeStartle, "npc_not_here"));
        }

        [UnityTest]
        public IEnumerator AllEightEffectIdsFinishAndUnknownIdDoesNotCancelCurrentCue()
        {
            string[] ids = { PresentationEffectIds.EnterShadowWalk, PresentationEffectIds.AccentShakeStartle,
                PresentationEffectIds.ShakeLight, PresentationEffectIds.ShakeHeavy, PresentationEffectIds.FlashWhite,
                PresentationEffectIds.TransitionBlack, PresentationEffectIds.PortraitFadeOut, PresentationEffectIds.PortraitFadeIn };
            foreach (string id in ids)
            {
                var handle = panel.PlayEffect(id, "right");
                panel.Effects.Tick(1f);
                Assert.That(handle.IsCompleted, Is.True, id);
            }
            var active = panel.PlayEffect(PresentationEffectIds.ShakeHeavy);
            Assert.Throws<ArgumentException>(() => panel.PlayEffect("not_a_real_effect"));
            Assert.That(active.Status, Is.EqualTo(PresentationEffectStatus.Playing));
            yield return null;
        }
        [UnityTest]
        public IEnumerator AccentTargetsOtherCharacterAndComposesWithScreenFlashAndShake()
        {
            RectTransform left = panel.LeftPortrait.transform.parent as RectTransform;
            RectTransform right = panel.RightPortrait.transform.parent as RectTransform;
            Vector2 leftBase = left.anchoredPosition, rightBase = right.anchoredPosition;
            var accent = panel.PlayEffect(PresentationEffectIds.AccentShakeStartle, "npc_guest");
            var shake = panel.PlayEffect(PresentationEffectIds.ShakeHeavy);
            var flash = panel.PlayEffect(PresentationEffectIds.FlashWhite);
            panel.Effects.Tick(.025f);
            Assert.That(left.anchoredPosition, Is.EqualTo(leftBase));
            Assert.That(Vector2.Distance(right.anchoredPosition, rightBase), Is.GreaterThan(.1f));
            Assert.That(panel.EffectStage.anchoredPosition.magnitude, Is.GreaterThan(.1f));
            Assert.That(panel.Effects.OverlayAlpha, Is.GreaterThan(.1f));
            Assert.That(accent.IsDone || shake.IsDone || flash.IsDone, Is.False);
            panel.Effects.Tick(1f);
            Assert.That(accent.IsCompleted && shake.IsCompleted && flash.IsCompleted, Is.True);
            Assert.That(right.anchoredPosition, Is.EqualTo(rightBase));
            Assert.That(panel.EffectStage.anchoredPosition, Is.EqualTo(Vector2.zero));
            yield return null;
        }
        [UnityTest]
        public IEnumerator HeavyShakeRejectsLightAndSamePriorityReplacesWithoutOldCallback()
        {
            int oldCallbacks = 0;
            var heavy = panel.PlayEffect(PresentationEffectIds.ShakeHeavy, completed: () => oldCallbacks++);
            var light = panel.PlayEffect(PresentationEffectIds.ShakeLight);
            Assert.That(light.Status, Is.EqualTo(PresentationEffectStatus.Cancelled));
            Assert.That(heavy.IsDone, Is.False);
            var replacement = panel.PlayEffect(PresentationEffectIds.ShakeHeavy);
            Assert.That(heavy.Status, Is.EqualTo(PresentationEffectStatus.Cancelled));
            panel.Effects.Tick(1f);
            Assert.That(replacement.IsCompleted, Is.True);
            Assert.That(oldCallbacks, Is.Zero);
            yield return null;
        }
        [UnityTest]
        public IEnumerator BlackTransitionSwitchesAtFullyBlackAndBlocksInput()
        {
            int switches = 0;
            var fade = panel.PlayFade("transition", .6f, () =>
            { Assert.That(panel.Effects.OverlayAlpha, Is.EqualTo(1f)); switches++; });
            Assert.That(panel.IsEffectBlocking, Is.True);
            panel.Advance();
            Assert.That(panel.LineIndex, Is.Zero);
            panel.Effects.Tick(.35f);
            Assert.That(switches, Is.EqualTo(1));
            panel.Effects.Tick(.35f);
            Assert.That(fade.IsCompleted, Is.True);
            Assert.That(panel.Effects.OverlayAlpha, Is.Zero);
            Assert.That(panel.IsEffectBlocking, Is.False);
            yield return null;
        }
        [UnityTest]
        public IEnumerator CameraZoomFocusAndShakeAreVisibleInOverlayAndReset()
        {
            RectTransform blocker = panel.transform.Find("InputBlocker") as RectTransform;
            Vector3 blockerScale = blocker.localScale;
            var cue = panel.PlayCameraCue(1.08f, 4f, .3f, new Vector2(30f, 8f));
            panel.Effects.Tick(.15f);
            Assert.That(panel.EffectStage.localScale.x, Is.GreaterThan(1f));
            Assert.That(panel.EffectStage.anchoredPosition.magnitude, Is.GreaterThan(1f));
            Assert.That(blocker.localScale, Is.EqualTo(blockerScale));
            panel.Effects.Tick(.3f);
            Assert.That(cue.IsCompleted, Is.True);
            Assert.That(panel.Effects.CameraZoom, Is.EqualTo(1.08f).Within(.001f));
            panel.PlayCameraCue(); panel.Effects.Tick(1f);
            Assert.That(panel.EffectStage.localScale, Is.EqualTo(Vector3.one));
            yield return null;
        }
        [UnityTest]
        public IEnumerator SwapFadesMirrorsAndPreservesTextThenNewCharacterReplacesOldOne()
        {
            DialogueCharacter a = panel.LeftCharacter, b = panel.RightCharacter;
            int done = 0;
            panel.SwapPortraits(() => done++);
            Assert.That(panel.IsEffectBlocking, Is.True);
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(panel.LeftCharacter, Is.SameAs(b));
            Assert.That(panel.RightCharacter, Is.SameAs(a));
            Assert.That(panel.RightPortrait.transform.localScale.x, Is.LessThan(0f));
            Assert.That(done, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator StopCancelsBlockingAndBlackCallbacksAndRestoresStageAtTimeScaleZero()
        {
            Time.timeScale = 0f;
            int callbacks = 0;
            var fade = panel.PlayFade("transition", .6f, () => callbacks++);
            var shake = panel.PlayEffect(PresentationEffectIds.ShakeHeavy);
            panel.PlayCameraCue(1.1f, duration: 1f);
            yield return new WaitForSecondsRealtime(.06f);
            Assert.That(panel.Effects.OverlayAlpha, Is.GreaterThan(0f));
            panel.Stop();
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(fade.Status, Is.EqualTo(PresentationEffectStatus.Cancelled));
            Assert.That(shake.Status, Is.EqualTo(PresentationEffectStatus.Cancelled));
            Assert.That(callbacks, Is.Zero);
            Assert.That(panel.IsEffectBlocking, Is.False);
            Assert.That(panel.Effects.OverlayAlpha, Is.Zero);
            Assert.That(panel.EffectStage.localScale, Is.EqualTo(Vector3.one));
            Assert.That(panel.EffectStage.anchoredPosition, Is.EqualTo(Vector2.zero));
        }
        [UnityTest]
        public IEnumerator IndependentDebugButtonsLaunchEffectsWithoutNcs()
        {
            controller.PreviewStartle(); yield return null; yield return null;
            controller.PreviewShakeHeavy(); yield return null; yield return null;
            controller.PreviewFlash(); yield return null; yield return null;
            Assert.That(panel.Effects.ScreenShakePriority, Is.EqualTo(2));
            Assert.That(panel.Effects.OverlayAlpha, Is.GreaterThan(0));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<DayFlowController>(), Is.Null);
        }
        [UnityTest]
        public IEnumerator CaptureEffectsOnTheSharedScene()
        {
            controller.ToggleHelp();
            panel.Advance();
            yield return new WaitForSecondsRealtime(.25f);
            yield return new WaitForEndOfFrame();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath("output"));
            CaptureScreenshot("output/presentation-effects.png");
            yield return null;
        }
        private static void CaptureScreenshot(string path)
        {
            // Save synchronously to avoid async screenshot writers racing repeated PlayMode runs.
            Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
            if (capture == null) throw new InvalidOperationException("Game view capture unavailable.");
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));
                System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(path), capture.EncodeToPNG());
            }
            finally { UnityEngine.Object.Destroy(capture); }
        }
    }
}
#endif
