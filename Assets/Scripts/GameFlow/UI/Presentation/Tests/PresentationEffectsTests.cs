#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
        public IEnumerator PrefabHistoryButtonsPauseRevealAndEscapeClosesHistoryBeforeCancellingDialogue()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("PresentationEffectsHistoryKeyboard");
            try
            {
                panel.Play(new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Narrator, new string('字', 300)) }));
                yield return Ready();
                Button historyButton = panel.transform.Find("EffectStage/Artwork/DialogueBox/history")?.GetComponent<Button>();
                var dialogue = panel.transform.Find("EffectStage/Artwork/DialogueBox/Dialogue")?.GetComponent<TMPro.TMP_Text>();
                Button closeButton = panel.transform.Find("DialogueHistory/Close")?.GetComponent<Button>();
                var historyText = panel.transform.Find("DialogueHistory/Viewport/Text")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(historyButton, Is.Not.Null, "Use the history Button on the actual dialogue prefab.");
                Assert.That(closeButton, Is.Not.Null, "History must provide its generated Close Button.");
                Assert.That(dialogue, Is.Not.Null);
                Assert.That(historyText, Is.Not.Null);
                Assert.That(historyButton.IsInteractable(), Is.True);
                yield return new WaitForSecondsRealtime(.1f);
                Assert.That(panel.IsTyping, Is.True, "Open history while the controlled long line is still revealing text.");

                historyButton.onClick.Invoke();
                Assert.That(panel.IsHistoryOpen, Is.True);
                Assert.That(panel.ReadingPaused, Is.True);
                int revealedCharacters = dialogue.maxVisibleCharacters;
                Assert.That(revealedCharacters, Is.GreaterThan(0));
                string frozenHistory = historyText.text;
                int entryIndex = DialogueHistoryService.Formal.Entries.Count - 1;
                Assert.That(entryIndex, Is.GreaterThanOrEqualTo(0));
                string frozenEntry = DialogueHistoryService.Formal.Entries[entryIndex].Text;
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(panel.ReadingPaused, Is.True);
                Assert.That(dialogue.maxVisibleCharacters, Is.EqualTo(revealedCharacters), "History pauses the actual typewriter.");
                Assert.That(historyText.text, Is.EqualTo(frozenHistory));
                Assert.That(DialogueHistoryService.Formal.Entries[entryIndex].Text, Is.EqualTo(frozenEntry));

                closeButton.onClick.Invoke();
                Assert.That(panel.IsHistoryOpen, Is.False);
                Assert.That(panel.ReadingPaused, Is.False);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(dialogue.maxVisibleCharacters, Is.GreaterThan(revealedCharacters), "The Close Button resumes revealing text.");
                Assert.That(panel.IsPlaying, Is.True);

                historyButton.onClick.Invoke();
                Assert.That(panel.IsHistoryOpen, Is.True);
                Assert.That(panel.ReadingPaused, Is.True);
                yield return null; // Do not send Escape on history's opening/input-blocked frame.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                Assert.That(panel.IsHistoryOpen, Is.False, "First Escape closes only history.");
                Assert.That(panel.ReadingPaused, Is.False);
                Assert.That(panel.IsPlaying, Is.True, "The debug controller must not also cancel dialogue on that Escape.");
                Assert.That(controller.CurrentPanel, Is.SameAs(panel));

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                Assert.That(DialoguePanel.IsAnyPlaying, Is.False, "Second Escape cancels dialogue after history is closed.");
                Assert.That(controller.CurrentPanel, Is.Null);
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        public IEnumerator HistoryPausesPartialRevealAndNestedPauseSurvivesClose()
        {
            DialogueHistoryService.ClearFormal();
            string text = new string('字', 300);
            panel.Play(new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Narrator, text) }));
            yield return Ready();
            yield return new WaitForSecondsRealtime(.1f);
            panel.OpenHistory();
            Assert.That(panel.IsHistoryOpen, Is.True);
            Assert.That(panel.ReadingPaused, Is.True);
            Assert.That(DialogueHistoryService.Formal.Entries.Count, Is.EqualTo(1));
            string partial = DialogueHistoryService.Formal.Entries[0].Text;
            Assert.That(partial.Length, Is.LessThan(text.Length));
            var other = panel.AcquireReadingPause(this);
            panel.Advance();
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(DialogueHistoryService.Formal.Entries[0].Text, Is.EqualTo(partial));
            panel.CloseHistory();
            Assert.That(panel.ReadingPaused, Is.True);
            other.Dispose();
            Assert.That(panel.ReadingPaused, Is.False);
            Assert.That(panel.LineIndex, Is.Zero);
        }

        [UnityTest]
        public IEnumerator HistoryBlocksChoiceUntilClosedWithoutRebuildingOptions()
        {
            DialogueHistoryService.ClearFormal();
            panel.Play(new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Narrator, "选择") },
                options: new[] { new DialogueOption("yes", "继续") }));
            yield return Ready();
            panel.Advance(); yield return null; panel.Advance(); yield return null;
            Assert.That(panel.IsChoosing, Is.True);
            panel.OpenHistory();
            Assert.That(panel.SelectOption("yes"), Is.False);
            Assert.That(panel.SelectedOptionId, Is.Empty);
            panel.CloseHistory(); yield return null;
            Assert.That(panel.IsChoosing, Is.True);
            Assert.That(panel.SelectOption("yes"), Is.True);
            Assert.That(DialogueHistoryService.Formal.Entries.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator SameCharacterExpressionUpdateDoesNotStartReplacement()
        {
            var character = NarrativeCharacterResolver.CreateDialogueCharacter("adele");
            panel.Play(new DialogueScript(new[]
            {
                new DialogueLine(DialogueSpeaker.Right, "first"),
                new DialogueLine(DialogueSpeaker.Right, "second", replaceRight: true,
                    rightCharacter: new DialogueCharacter(character.CharacterKey, character.DisplayName, character.Portrait))
            }, rightCharacter: character));
            yield return Ready();
            panel.Advance(); yield return null;
            panel.Advance();
            Assert.That(panel.LineIndex, Is.EqualTo(1));
            Assert.That(panel.Effects.IsPortraitTransitioning, Is.False);
            Assert.That(panel.IsEffectBlocking, Is.False);
        }

        [UnityTest]
        public IEnumerator PausedEffectDoesNotAdvanceUnscaledTimerOrCallback()
        {
            bool completed = false;
            var handle = panel.PlayFade("black", .2f, completed: () => completed = true);
            panel.Effects.SetPaused(true);
            float alpha = panel.Effects.OverlayAlpha;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(panel.Effects.OverlayAlpha, Is.EqualTo(alpha));
            Assert.That(handle.IsDone, Is.False);
            Assert.That(completed, Is.False);
            panel.Effects.SetPaused(false);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(completed, Is.True);
        }

        [UnityTest]
        public IEnumerator DefaultEntranceKeepsEveryPortraitUnmirroredAndNamesReadable()
        {
            Assert.That(panel.RightPortrait.transform.localScale.x, Is.GreaterThan(0));
            Assert.That(panel.LeftPortrait.transform.localScale.x, Is.GreaterThan(0));
            Assert.That(panel.EffectStage.localScale, Is.EqualTo(Vector3.one));
            foreach (TMPro.TMP_Text label in panel.GetComponentsInChildren<TMPro.TMP_Text>())
                Assert.That(label.transform.localScale.x, Is.GreaterThan(0));
            // The non-speaking portrait intentionally uses inactiveTint (including alpha .85).
            // Entrance must be visible, not force that independent focus channel to opaque white.
            Assert.That(panel.LeftPortrait.color.a, Is.GreaterThan(.8f));
            yield return null;
        }
        [UnityTest]
        public IEnumerator EntranceAndCharacterReplacementFinishBeforeTextStarts()
        {
            DialogueScript sample = PresentationEffectsPreview.Create();
            var replacement = NarrativeCharacterResolver.CreatePreviewCharacter("sylvain", Resources.Load<DialogueVisualsAsset>("Dialogue/DialoguePreview").CenterPortrait);
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
            Assert.That(panel.CenterCharacter, Is.SameAs(replacement));
            Assert.That(panel.CenterPortrait.sprite, Is.SameAs(replacement.Portrait));
            Assert.That(panel.LineIndex, Is.EqualTo(1));
            Assert.That(panel.IsReady, Is.True);
        }

        [UnityTest]
        public IEnumerator NewSpeakersFillEmptyPortraitSlotsAndKeepTheirAssignedPosition()
        {
            var visuals = Resources.Load<DialogueVisualsAsset>("Dialogue/DialoguePreview");
            var player = NarrativeCharacterResolver.CreatePreviewCharacter("player", visuals.LeftPortrait);
            var firstVisitor = NarrativeCharacterResolver.CreatePreviewCharacter("adele", visuals.RightPortrait);
            var secondVisitor = NarrativeCharacterResolver.CreatePreviewCharacter("sylvain", visuals.CenterPortrait);
            var fourthVisitor = NarrativeCharacterResolver.CreatePreviewCharacter("colette", visuals.CenterPortrait);
            panel.Play(new DialogueScript(new[]
            {
                new DialogueLine(DialogueSpeaker.Left, "我在这里。", replaceLeft: true, leftCharacter: player),
                new DialogueLine(DialogueSpeaker.Right, "第一位来客。", replaceRight: true, rightCharacter: firstVisitor),
                new DialogueLine(DialogueSpeaker.Right, "第二位来客。", replaceRight: true, rightCharacter: secondVisitor),
                new DialogueLine(DialogueSpeaker.Right, "第二位来客继续说话。", replaceRight: true,
                    rightCharacter: new DialogueCharacter(secondVisitor.CharacterKey, secondVisitor.DisplayName, null, false)),
                new DialogueLine(DialogueSpeaker.Right, "三人都在场后按原侧替换。", replaceRight: true, rightCharacter: fourthVisitor)
            }, new DialogueCharacter("", null), new DialogueCharacter("", null), isPreview: true));
            panel.SetPreviewReading(0f, typewriter: false);
            yield return Ready();
            Assert.That(panel.LeftCharacter, Is.SameAs(player));
            Assert.That(panel.LeftPortrait.enabled, Is.True);

            yield return AdvanceToLine(1);
            Assert.That(panel.RightCharacter, Is.SameAs(firstVisitor));
            Assert.That(panel.RightPortrait.enabled, Is.True);

            yield return AdvanceToLine(2);
            Assert.That(panel.CenterCharacter, Is.SameAs(secondVisitor));
            Assert.That(panel.RightCharacter, Is.SameAs(firstVisitor));
            Assert.That(panel.CenterPortrait.enabled, Is.True);
            Assert.That(panel.CenterPortrait.sprite, Is.SameAs(secondVisitor.Portrait));

            yield return AdvanceToLine(3);
            Assert.That(panel.CenterCharacter.CharacterKey, Is.EqualTo(secondVisitor.CharacterKey));
            Assert.That(panel.RightCharacter, Is.SameAs(firstVisitor));

            yield return AdvanceToLine(4);
            Assert.That(panel.RightCharacter, Is.SameAs(fourthVisitor));
            Assert.That(panel.CenterCharacter.CharacterKey, Is.EqualTo(secondVisitor.CharacterKey));
            Assert.That(panel.LeftCharacter, Is.SameAs(player));
        }

        [UnityTest]
        public IEnumerator DuplicateInitialCharactersKeepTheSpeakingSlotOnly()
        {
            var visuals = Resources.Load<DialogueVisualsAsset>("Dialogue/DialoguePreview");
            var leftCopy = NarrativeCharacterResolver.CreatePreviewCharacter("adele", visuals.LeftPortrait);
            var rightCopy = NarrativeCharacterResolver.CreatePreviewCharacter("adele", visuals.RightPortrait);
            panel.Play(new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Right, "阿黛尔发言。") },
                leftCopy, rightCopy, isPreview: true));
            panel.SetPreviewReading(0f, typewriter: false);
            yield return Ready();

            Assert.That(panel.LeftPortrait.enabled, Is.False);
            Assert.That(panel.RightPortrait.enabled, Is.True);
            Assert.That(panel.RightPortrait.sprite, Is.SameAs(rightCopy.Portrait));
            Assert.That(panel.LeftCharacter, Is.Null);
            Assert.That(panel.RightCharacter.CharacterKey, Is.EqualTo("adele"));
        }

        [UnityTest]
        public IEnumerator DuplicateGuestInThreePortraitStageLeavesPlayerAndOneGuest()
        {
            var visuals = Resources.Load<DialogueVisualsAsset>("Dialogue/DialoguePreview");
            var player = NarrativeCharacterResolver.CreatePreviewCharacter("player", visuals.LeftPortrait);
            var rightGuest = NarrativeCharacterResolver.CreatePreviewCharacter("adele", visuals.RightPortrait);
            var centerGuest = NarrativeCharacterResolver.CreatePreviewCharacter("adele", visuals.CenterPortrait);
            panel.Play(new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Narrator, "三人在场。") },
                player, rightGuest, centerCharacter: centerGuest, isPreview: true));
            panel.SetPreviewReading(0f, typewriter: false);
            yield return Ready();

            Assert.That(panel.LeftCharacter, Is.SameAs(player));
            Assert.That(panel.RightCharacter.CharacterKey, Is.EqualTo("adele"));
            Assert.That(panel.CenterCharacter, Is.Null);
            Assert.That(panel.LeftPortrait.enabled, Is.True);
            Assert.That(panel.RightPortrait.enabled, Is.True);
            Assert.That(panel.CenterPortrait.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator DuplicateCharactersReplacedOnTwoSlotsKeepTheSpeakingSlot()
        {
            var visuals = Resources.Load<DialogueVisualsAsset>("Dialogue/DialoguePreview");
            var player = NarrativeCharacterResolver.CreatePreviewCharacter("player", visuals.LeftPortrait);
            var rightGuest = NarrativeCharacterResolver.CreatePreviewCharacter("adele", visuals.RightPortrait);
            var centerGuest = NarrativeCharacterResolver.CreatePreviewCharacter("adele", visuals.CenterPortrait);
            panel.Play(new DialogueScript(new[]
            {
                new DialogueLine(DialogueSpeaker.Center, "阿黛尔来到中间。", replaceRight: true,
                    rightCharacter: rightGuest, replaceCenter: true, centerCharacter: centerGuest)
            }, player, new DialogueCharacter("", null), isPreview: true));
            panel.SetPreviewReading(0f, typewriter: false);
            yield return Ready();

            Assert.That(panel.LeftPortrait.enabled, Is.True);
            Assert.That(panel.RightPortrait.enabled, Is.False);
            Assert.That(panel.CenterPortrait.enabled, Is.True);
            Assert.That(panel.CenterCharacter.CharacterKey, Is.EqualTo("adele"));
            Assert.That(panel.CenterPortrait.sprite, Is.SameAs(centerGuest.Portrait));
        }

        [UnityTest]
        public IEnumerator DifferentCharacterKeysWithSameDisplayNameRemainSeparate()
        {
            var visuals = Resources.Load<DialogueVisualsAsset>("Dialogue/DialoguePreview");
            var first = new DialogueCharacter("adele", "阿黛尔", visuals.LeftPortrait);
            var second = new DialogueCharacter("guest_alias", "阿黛尔", visuals.RightPortrait);
            panel.Play(new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Narrator, "同名角色。") },
                first, second, isPreview: true));
            panel.SetPreviewReading(0f, typewriter: false);
            yield return Ready();

            Assert.That(panel.LeftPortrait.enabled, Is.True);
            Assert.That(panel.RightPortrait.enabled, Is.True);
            Assert.That(panel.LeftCharacter.CharacterKey, Is.EqualTo("adele"));
            Assert.That(panel.RightCharacter.CharacterKey, Is.EqualTo("guest_alias"));
        }

        private IEnumerator AdvanceToLine(int targetLine)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (panel.LineIndex < targetLine)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                if (panel.IsReady) panel.Advance();
                yield return null;
            }
            yield return Ready();
        }

        [UnityTest]
        public IEnumerator LineAccentUsesNcsTargetKeyAndDoesNotDependOnSpeakerSlot()
        {
            var sample = PresentationEffectsPreview.Create();
            panel.Play(new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Left, "由左侧说话，右侧受惊",
                accentFxKey: PresentationEffectIds.AccentShakeStartle, accentTargetKey: "adele") },
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
            panel.Effects.Tick(1f); // Settle entrance/focus before capturing the resting baseline.
            RectTransform left = panel.LeftPortrait.transform.parent as RectTransform;
            RectTransform right = panel.RightPortrait.transform.parent as RectTransform;
            Vector2 leftBase = left.anchoredPosition, rightBase = right.anchoredPosition;
            float leftBottom = leftBase.y + left.rect.yMin * left.localScale.y;
            float rightBottom = rightBase.y + right.rect.yMin * right.localScale.y;
            var accent = panel.PlayEffect(PresentationEffectIds.AccentShakeStartle, "adele");
            var shake = panel.PlayEffect(PresentationEffectIds.ShakeHeavy);
            var flash = panel.PlayEffect(PresentationEffectIds.FlashWhite);
            panel.Effects.Tick(.025f);
            Assert.That(left.anchoredPosition.x, Is.EqualTo(leftBase.x).Within(.001f));
            Assert.That(left.anchoredPosition.y + left.rect.yMin * left.localScale.y,
                Is.EqualTo(leftBottom).Within(.001f));
            Assert.That(Mathf.Abs(right.anchoredPosition.x - rightBase.x), Is.GreaterThan(.1f));
            Assert.That(right.anchoredPosition.y + right.rect.yMin * right.localScale.y,
                Is.EqualTo(rightBottom).Within(.001f));
            Assert.That(panel.EffectStage.anchoredPosition.magnitude, Is.GreaterThan(.1f));
            Assert.That(panel.Effects.OverlayAlpha, Is.GreaterThan(.1f));
            Assert.That(accent.IsDone || shake.IsDone || flash.IsDone, Is.False);
            panel.Effects.Tick(1f);
            Assert.That(accent.IsCompleted && shake.IsCompleted && flash.IsCompleted, Is.True);
            Assert.That(right.anchoredPosition.x, Is.EqualTo(rightBase.x).Within(.001f));
            Assert.That(right.anchoredPosition.y + right.rect.yMin * right.localScale.y,
                Is.EqualTo(rightBottom).Within(.001f));
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
        public IEnumerator SwapFadesWithoutMirroringAndPreservesCharacterIdentity()
        {
            DialogueCharacter a = panel.LeftCharacter, b = panel.RightCharacter;
            int done = 0;
            panel.SwapPortraits(() => done++);
            Assert.That(panel.IsEffectBlocking, Is.True);
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(panel.LeftCharacter, Is.SameAs(b));
            Assert.That(panel.RightCharacter, Is.SameAs(a));
            Assert.That(panel.RightPortrait.transform.localScale.x, Is.GreaterThan(0f));
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
