#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    /// <summary>Real panel/bridge regression in the shared scene, not a natural story playthrough.
    /// Formal values are explicitly seeded. Only the private preview core may branch.</summary>
    public sealed class NarrativeAtlasPreviewTests
    {
        private const string Root = "atlas.fixture.root", Presentation = "atlas.fixture.presentation";
        private const string ChoicePage = "atlas.fixture.choice", LeftPage = "atlas.fixture.left", RightPage = "atlas.fixture.right";
        private const string LeftChoice = "atlas.fixture.choose.left", RightChoice = "atlas.fixture.choose.right", LockedChoice = "atlas.fixture.choose.locked";
        private Scene previous, shared;
        private NarrativeAtlasPreview preview;
        private NarrativeRuntime formal, originalRuntime;
        private GameObject formalHost;
        private PresentationDebugSceneController debug;
        private IDisposable foreignPause;
        private Func<bool> previousDialogueGate, previousPresenterGate;
        private float oldTimeScale;
        private int pauseOwners;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldTimeScale = Time.timeScale;
            pauseOwners = PresentationPauseRegistry.OwnerCount;
            originalRuntime = NarrativeRuntime.Instance;
            previousDialogueGate = DialoguePanel.ExternalInputBlocked;
            previousPresenterGate = NarrativePresenter.ExternalInputBlocked;
            // GM normally owns these gates. Temporarily permit the real public panel inputs.
            DialoguePanel.ExternalInputBlocked = () => false;
            NarrativePresenter.ExternalInputBlocked = () => false;
            previous = SceneManager.GetActiveScene();
            shared = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            Assert.That(shared.IsValid() && shared.isLoaded, Is.True);
            SceneManager.SetActiveScene(shared);
            debug = shared.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PresentationDebugSceneController>(true)).Single();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            preview?.Dispose(); preview = null;
            debug?.StopCurrent();
            formal?.Presenter?.DebugStopPresentation();
            formal?.Controller?.UnbindValueProviders();
            if (formalHost != null) UnityEngine.Object.Destroy(formalHost);
            formal = null; formalHost = null;
            foreignPause?.Dispose(); foreignPause = null;
            UIKit.ClosePanelsByTag(NarrativeAtlasPreview.PanelTag);
            DialoguePanel.ExternalInputBlocked = previousDialogueGate;
            NarrativePresenter.ExternalInputBlocked = previousPresenterGate;
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (shared.IsValid() && shared.isLoaded) yield return SceneManager.UnloadSceneAsync(shared);
            Time.timeScale = oldTimeScale;
            yield return null;
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners), "Release only this fixture's pause owners.");
            Assert.That(NarrativeRuntime.Instance, Is.SameAs(originalRuntime), "Do not replace the formal singleton.");
        }

        [UnityTest]
        public IEnumerator RootLeftChoiceUsesCopiedFormalStateAndNeverPlaysRightBranch()
        { yield return CheckBranch(LeftChoice, LeftPage, RightPage); }

        [UnityTest]
        public IEnumerator RootRightChoiceUsesCopiedExternalValuesAndNeverPlaysLeftBranch()
        { yield return CheckBranch(RightChoice, RightPage, LeftPage); }

        private IEnumerator CheckBranch(string choiceId, string expectedPage, string excludedPage)
        {
            var config = ChoiceConfiguration();
            CreateFormal(config);
            var controller = formal.Controller;
            var presenter = formal.Presenter;
            var actions = formal.Actions;
            string beforeState = Json(controller.State.Capture()), beforeActions = actions.CaptureJson(), beforeConfig = Json(config);
            var rootRow = config.events.Single(e => e.eventId == Root);
            var pageRow = config.presentations.conversationPages.Single(p => p.pageId == ChoicePage);
            var resultRow = config.results[0];
            var barRow = config.bars.Single(b => b.barKey == "player.language");
            int effects = 0, completed = 0;
            controller.EffectRequested += _ => effects++;
            controller.EventCompleted += _ => completed++;
            var scenes = LoadedScenes();
            preview = new NarrativeAtlasPreview(config, key => key);
            preview.Play(Node(config, Root), runtime: formal);
            yield return WaitForChoice();
            var isolated = preview.PreviewController;
            Assert.That(isolated, Is.Not.Null.And.Not.SameAs(controller));
            Assert.That(isolated.State.GetCondition("atlas.unlocked"), Is.True);
            Assert.That(isolated.State.TryGetBar("player.language", out int language) && language == 5, Is.True);
            Assert.That(isolated.State.TryGetBar("cafe.money", out int money) && money == 500, Is.True);
            Assert.That(DialoguePanel.Active.SelectOption(LockedChoice), Is.False, "Do not force-enable unavailable choices.");
            Assert.That(DialoguePanel.Active.SelectOption(choiceId), Is.True, "Use the actual panel -> NCS selection chain.");
            // Tick owns teardown: inspect the selected branch before Tick releases the core.
            CollectionAssert.AreEqual(new[] { choiceId }, isolated.SelectedChoices);
            Assert.That(isolated.ReadPresentationCondition("choice:" + choiceId), Is.True);
            var seen = new HashSet<string> { ChoicePage };
            yield return DrainDialogue(seen);
            CollectionAssert.AreEquivalent(new[] { ChoicePage, expectedPage }, seen);
            Assert.That(seen, Does.Not.Contain(excludedPage));
            Assert.That(seen, Does.Not.Contain("atlas.fixture.hidden"));
            Assert.That(isolated.State.GetCondition("atlas.reward"), Is.False, "Neither root nor choice effects settle in preview.");
            Assert.That(isolated.State.HasHappenedEvent("atlas.fixture.trap"), Is.False);
            Assert.That(preview.PreviewController, Is.Null, "Tick cleans up the completed core.");
            Assert.That(preview.Status, Is.Not.Null.And.Not.Empty);
            Assert.That(effects, Is.Zero); Assert.That(completed, Is.Zero);
            Assert.That(formal.Controller, Is.SameAs(controller));
            Assert.That(formal.Presenter, Is.SameAs(presenter));
            Assert.That(formal.Actions, Is.SameAs(actions));
            Assert.That(formal.Configuration, Is.SameAs(config));
            Assert.That(config.events.Single(e => e.eventId == Root), Is.SameAs(rootRow));
            Assert.That(config.presentations.conversationPages.Single(p => p.pageId == ChoicePage), Is.SameAs(pageRow));
            Assert.That(config.results[0], Is.SameAs(resultRow));
            Assert.That(config.bars.Single(b => b.barKey == "player.language"), Is.SameAs(barRow));
            Assert.That(Json(controller.State.Capture()), Is.EqualTo(beforeState));
            Assert.That(actions.CaptureJson(), Is.EqualTo(beforeActions), "No formal AP spending.");
            Assert.That(Json(config), Is.EqualTo(beforeConfig), "Clear gates/results/triggers only from a deep copy.");
            CollectionAssert.AreEqual(scenes, LoadedScenes(), "scene.cafe must not load CoffeeShop in preview.");
            yield return AssertPreviewReleased();
        }

        [UnityTest]
        public IEnumerator SinglePageIgnoresOnlySelectedPageGuardsAndCannotCommitChoices()
        {
            var config = ChoiceConfiguration();
            string before = Json(config);
            Assert.That(config.presentations.conversationPages.Single(p => p.pageId == RightPage).pageCondKeys,
                Is.EqualTo("choice:" + RightChoice));
            preview = new NarrativeAtlasPreview(config, key => key);
            preview.Play(Node(config, Root), RightPage);
            var seen = new HashSet<string>();
            yield return WaitFor(() => DialoguePanel.Active?.CurrentSource?.PageId == RightPage, "Direct single-page jump");
            Assert.That(preview.CurrentPageId, Is.EqualTo(RightPage));
            Assert.That(preview.PreviewController.SelectedChoices, Is.Empty);
            yield return DrainDialogue(seen);
            CollectionAssert.AreEquivalent(new[] { RightPage }, seen);
            Assert.That(Json(config), Is.EqualTo(before));
            yield return AssertPreviewReleased();
            preview.Dispose();
            preview = new NarrativeAtlasPreview(config, key => key);
            preview.Play(Node(config, Root), ChoicePage);
            yield return WaitForChoice();
            Assert.That(DialoguePanel.Active.SelectOption(LeftChoice), Is.False, "Single-page choices are display-only.");
            Assert.That(DialoguePanel.Active.SelectOption(RightChoice), Is.False);
            Assert.That(preview.PreviewController.SelectedChoices, Is.Empty);
            preview.Dispose();
            yield return AssertPreviewReleased();
        }

        [UnityTest]
        public IEnumerator GeneratedOrdinaryDailyPageUsesDialoguePanelWithoutStartingFormalRuntime()
        {
            var config = LubanNarrativeLoader.Load();
            var root = config.events.First(e => e.eventProperties.Any(p => p.property == "dailyKind" && p.value == "ordinary"));
            var page = config.presentations.conversationPages.Where(p => p.presentationKey == root.presentationKey).OrderBy(p => p.seq).First();
            Assert.That(root.presentationClass, Is.EqualTo("Conversation").IgnoreCase);
            string before = Json(config);
            preview = new NarrativeAtlasPreview(config);
            preview.Play(Node(config, root.eventId), page.pageId);
            yield return WaitFor(() => DialoguePanel.Active?.CurrentSource?.PageId == page.pageId, root.eventId);
            Assert.That(DialoguePanel.Active.Tag, Is.EqualTo(NarrativeAtlasPreview.PanelTag));
            Assert.That(UIKit.GetPanel<ReadingPanel>()?.IsPlaying == true, Is.False);
            Assert.That(UIKit.GetPanel<NoticePanel>()?.IsPlaying == true, Is.False);
            yield return DrainDialogue(new HashSet<string>());
            Assert.That(Json(config), Is.EqualTo(before));
            Assert.That(NarrativeRuntime.Instance, Is.SameAs(originalRuntime));
            yield return AssertPreviewReleased();
        }

        [UnityTest]
        public IEnumerator GeneratedReadingNoticeOpeningAndEndingUseExistingPanels()
        {
            var config = LubanNarrativeLoader.Load();
            string before = Json(config);
            var scenes = LoadedScenes();
            var newspaper = config.events.First(e => e.eventId.StartsWith("newspaper.", StringComparison.Ordinal));
            Assert.That(newspaper.presentationClass, Is.EqualTo("Reading").IgnoreCase);
            preview = new NarrativeAtlasPreview(config);
            preview.Play(Node(config, newspaper.eventId));
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False);
            yield return WaitFor(() => DialoguePanel.Active?.IsPlaying == true, "Newspaper DialoguePanel");
            var reading = DialoguePanel.Active;
            Assert.That(reading.Tag, Is.EqualTo(NarrativeAtlasPreview.PanelTag));
            DialoguePanel.ExternalInputBlocked = () => true;
            int line = reading.LineIndex;
            reading.Advance(); reading.Cancel();
            yield return null;
            Assert.That(reading.IsPlaying, Is.True);
            Assert.That(reading.LineIndex, Is.EqualTo(line));
            DialoguePanel.ExternalInputBlocked = () => false;
            reading.Cancel();
            yield return FinishAndRelease();
            preview.Dispose();

            var bulletin = config.events.First(e => e.eventId.StartsWith("news.bulletin.", StringComparison.Ordinal));
            Assert.That(bulletin.presentationClass, Is.EqualTo("Notice").IgnoreCase);
            preview = new NarrativeAtlasPreview(config);
            preview.Play(Node(config, bulletin.eventId));
            yield return WaitFor(() => UIKit.GetPanel<NoticePanel>()?.IsPlaying == true, "Bulletin NoticePanel");
            var notice = UIKit.GetPanel<NoticePanel>();
            Assert.That(notice.Tag, Is.EqualTo(NarrativeAtlasPreview.PanelTag));
            DialoguePanel.ExternalInputBlocked = () => true;
            notice.Confirm(); notice.Cancel();
            yield return null;
            Assert.That(notice.IsPlaying, Is.True);
            DialoguePanel.ExternalInputBlocked = () => false;
            notice.Cancel();
            yield return FinishAndRelease();
            preview.Dispose();

            Time.timeScale = 0;
            var graph = NarrativeRelationshipGraph.Build(config);
            preview = new NarrativeAtlasPreview(config);
            preview.Play(graph.Find("opening:first_day_cafe"));
            yield return WaitFor(() => DialoguePanel.Active?.IsPlaying == true, "OpeningPresentation DialoguePanel");
            Assert.That(DialoguePanel.Active.Tag, Is.EqualTo(NarrativeAtlasPreview.PanelTag));
            yield return DrainDialogue(new HashSet<string>());
            yield return AssertPreviewReleased();
            preview.Dispose();
            var ending = config.endings.First(e => e.enabled);
            preview = new NarrativeAtlasPreview(config);
            preview.Play(graph.Find("ending:" + ending.endingId));
            yield return WaitFor(() => UIKit.GetPanel<ResultPanel>()?.State == PanelState.Open, "Ending ResultPanel");
            var result = UIKit.GetPanel<ResultPanel>();
            Assert.That(result.Tag, Is.EqualTo(NarrativeAtlasPreview.PanelTag));
            Assert.That(result.IsEndingPresentation, Is.True);
            Assert.That(result.CurrentEndingId, Is.EqualTo(ending.endingId));
            Assert.That(result.DisplayedTitle, Is.Not.Null.And.Not.Empty);
            Assert.That(result.DisplayedMessage, Is.Not.Null.And.Not.Empty);
            result.ConfirmResult(); result.ConfirmResult();
            yield return FinishAndRelease();
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Json(config), Is.EqualTo(before));
            CollectionAssert.AreEqual(scenes, LoadedScenes(), "Cafe opening preview must not load CoffeeShop.");
        }

        [UnityTest]
        public IEnumerator FormalBusyAndExistingPresentationRejectWithoutStealingTheirOwners()
        {
            var config = ChoiceConfiguration();
            CreateFormal(config);
            Action held = null;
            formal.ConversationPlayback = (request, data, done) => { held = done; return true; };
            Assert.That(formal.QueueEvent("atlas.fixture.busy"), Is.True);
            yield return WaitFor(() => held != null && formal.Controller.IsBusy, "Formal held presentation");
            var controller = formal.Controller;
            var presenter = formal.Presenter;
            string before = Json(controller.State.Capture());
            int owners = PresentationPauseRegistry.OwnerCount;
            var node = Node(config, Root);
            Assert.That(NarrativeAtlasPreview.BlockReason(node, formal), Is.Not.Null.And.Not.Empty);
            preview = new NarrativeAtlasPreview(config, key => key);
            Assert.Throws<InvalidOperationException>(() => preview.Play(node, runtime: formal));
            Assert.That(preview.IsPlaying, Is.False);
            Assert.That(preview.PreviewController, Is.Null);
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(owners));
            Assert.That(formal.Controller, Is.SameAs(controller));
            Assert.That(formal.Presenter, Is.SameAs(presenter));
            Assert.That(Json(controller.State.Capture()), Is.EqualTo(before));
            formal.Presenter.DebugStopPresentation();
            debug.PlayReading();
            yield return WaitFor(() => debug.CurrentPanel is DialoguePanel panel && panel.IsPlaying, "Existing presentation");
            var existing = debug.CurrentPanel;
            owners = PresentationPauseRegistry.OwnerCount;
            Assert.That(NarrativeAtlasPreview.BlockReason(node), Is.Not.Null.And.Not.Empty);
            Assert.Throws<InvalidOperationException>(() => preview.Play(node));
            Assert.That(debug.CurrentPanel, Is.SameAs(existing));
            Assert.That(((DialoguePanel)existing).IsPlaying, Is.True);
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(owners));
        }

        [UnityTest]
        public IEnumerator GameplayExplicitlyCancelsWithoutFormalHostOrderOrFakeSuccess()
        {
            var config = ChoiceConfiguration();
            config.presentations.conversationPages.Clear();
            config.presentations.conversationLines.Clear();
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
            {
                pageId = "atlas.fixture.brew", presentationKey = Presentation, seq = 1,
                stepType = NarrativeConversationStepType.Gameplay, gameplayKey = "serve:black_coffee:60:1"
            });
            CreateFormal(config);
            int hostRequests = 0;
            formal.GameplayPlayback = (request, data, done) => { hostRequests++; throw new AssertionException("Preview called formal coffee host."); };
            string beforeState = Json(formal.Controller.State.Capture()), beforeActions = formal.Actions.CaptureJson(), beforeConfig = Json(config);
            var scenes = LoadedScenes();
            preview = new NarrativeAtlasPreview(config, key => key);
            preview.Play(Node(config, Root), runtime: formal);
            var isolated = preview.PreviewController;
            Assert.That(isolated, Is.Not.Null);
            bool completed = false;
            isolated.EventCompleted += _ => completed = true;
            yield return WaitFor(() => preview.CurrentPageId == "atlas.fixture.brew", "Gameplay cancellation");
            // Deliver cancellation to NCS, but inspect before Tick releases PreviewController.
            for (int i = 0; i < 8; i++) yield return null;
            Assert.That(completed, Is.False);
            Assert.That(isolated.State.HasHappenedEvent(Root), Is.False);
            Assert.That(isolated.State.GetCondition("atlas.reward"), Is.False);
            Assert.That(preview.Status, Does.Contain("atlas.fixture.brew"));
            Assert.That(preview.Status, Does.Contain("serve:black_coffee:60:1"));
            yield return FinishAndRelease();
            Assert.That(hostRequests, Is.Zero);
            Assert.That(Json(formal.Controller.State.Capture()), Is.EqualTo(beforeState));
            Assert.That(formal.Actions.CaptureJson(), Is.EqualTo(beforeActions));
            Assert.That(Json(config), Is.EqualTo(beforeConfig));
            CollectionAssert.AreEqual(scenes, LoadedScenes());
        }

        [UnityTest]
        public IEnumerator DisposeIsIdempotentReleasesTransientPanelAndKeepsUnrelatedPause()
        {
            var config = ChoiceConfiguration();
            foreignPause = PresentationPauseRegistry.Acquire("atlas-test-unrelated-owner");
            int owners = PresentationPauseRegistry.OwnerCount;
            preview = new NarrativeAtlasPreview(config, key => key);
            preview.Play(Node(config, Root), ChoicePage);
            yield return WaitForChoice();
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.GreaterThan(owners));
            var panel = DialoguePanel.Active;
            preview.Dispose(); preview.Dispose(); preview.Tick();
            Assert.That(preview.IsPlaying, Is.False);
            Assert.That(preview.PreviewController, Is.Null);
            yield return AssertPreviewReleased(owners);
            Assert.That(panel == null || !panel.IsPlaying, Is.True);
            Assert.Throws<ObjectDisposedException>(() => preview.Play(Node(config, Root)));
        }

        private void CreateFormal(NarrativeConfiguration config)
        {
            // Existing runtime-test pattern: inactive host, explicit Initialize, no Awake or singleton replacement.
            formalHost = new GameObject("NarrativeAtlasPreviewTests.FormalHost");
            formalHost.SetActive(false);
            formal = formalHost.AddComponent<NarrativeRuntime>();
            formal.Initialize(config, key => key);
            formal.Controller.SetEndingChecksEnabled(false);
            formal.Controller.BeginValueRestore();
            formal.Controller.BindValueProvider(new DelegateNarrativeValueProvider("cafe", key => key == "money" ? 500 : (int?)null));
            formal.Controller.SetBar("player.language", 5);
            formal.Controller.State.SetCondition("atlas.unlocked", true);
            formal.Controller.CompleteValueRestore(false);
        }

        private IEnumerator WaitForChoice()
        {
            yield return WaitFor(() => DialoguePanel.Active != null && DialoguePanel.Active.IsPlaying
                && DialoguePanel.Active.Tag == NarrativeAtlasPreview.PanelTag && DialoguePanel.Active.IsReady, "Preview ready");
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!DialoguePanel.Active.IsChoosing)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Preview choices did not become visible.");
                if (DialoguePanel.Active.IsReady) DialoguePanel.Active.Advance();
                yield return null;
            }
        }

        private IEnumerator DrainDialogue(HashSet<string> seen)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while (preview.IsPlaying)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), preview.Status + " / " + preview.CurrentPageId);
                var panel = DialoguePanel.Active;
                if (panel != null && panel.IsPlaying && panel.Tag == NarrativeAtlasPreview.PanelTag)
                {
                    string page = panel.CurrentSource?.PageId;
                    if (!string.IsNullOrEmpty(page)) seen.Add(page);
                    Assert.That(panel.IsChoosing, Is.False, "Driver must not silently select another choice.");
                    if (panel.IsReady) panel.Advance();
                }
                yield return null;
                preview.Tick();
            }
        }

        private IEnumerator FinishAndRelease()
        {
            yield return WaitFor(() => { preview.Tick(); return !preview.IsPlaying; }, "Tick completion cleanup");
            Assert.That(preview.PreviewController, Is.Null);
            yield return AssertPreviewReleased();
        }

        private IEnumerator AssertPreviewReleased(int expectedOwners = -1)
        {
            yield return WaitFor(() => !UnityEngine.Object.FindObjectsByType<UIPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(panel => panel.Tag == NarrativeAtlasPreview.PanelTag), "Transient preview panels must not be cached.");
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(expectedOwners < 0 ? pauseOwners : expectedOwners));
        }

        private static IEnumerator WaitFor(Func<bool> ready, string message)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ready(), Is.True, message);
        }
        private static SceneHandle[] LoadedScenes() => Enumerable.Range(0, SceneManager.sceneCount)
            .Select(i => SceneManager.GetSceneAt(i).handle).ToArray();
        private static string Json(object value) => JsonConvert.SerializeObject(value, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        private static StoryGraphNode Node(NarrativeConfiguration config, string id)
        {
            var node = NarrativeRelationshipGraph.Build(config).Find(NarrativeRelationshipGraph.EventId(id));
            Assert.That(node, Is.Not.Null, id);
            return node;
        }

        private static NarrativeConfiguration ChoiceConfiguration()
        {
            var config = new NarrativeConfiguration();
            config.objects.Add(new NarrativeObjectDefinition { objId = "player", objType = NarrativeObjectType.Player });
            config.objects.Add(new NarrativeObjectDefinition { objId = "world", objType = NarrativeObjectType.World });
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", ownerId = "world", min = 1, max = 999, initValue = 1, advanceMode = NarrativeAdvanceMode.Monotonic });
            foreach (string attribute in new[] { "language", "courage", "wisdom", "perception" })
                config.bars.Add(new NarrativeBarDefinition { barKey = "player." + attribute, ownerId = "player", min = 0, max = 10 });
            config.bars.Add(new NarrativeBarDefinition { barKey = "cafe.money", ownerId = "world", min = 0, max = 9999,
                sourceMode = NarrativeSourceMode.External, providerId = "cafe", sourceKey = "money" });
            foreach (string key in new[] { "atlas.unlocked", "atlas.denied", "atlas.rootGate", "atlas.reward" })
                config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = key });
            config.templates.Add(new NarrativeTemplateDefinition { tplName = "Tpl_TwoPersonTalk", atomSeq = "A_DIALOG" });
            config.templates.Add(new NarrativeTemplateDefinition { tplName = "Tpl_Narration", atomSeq = "A_NARRATE" });
            config.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = Presentation, presentationClass = "Conversation" });
            config.events.Add(new NarrativeEventDefinition { eventId = Root, actorId = "player", targetId = "world", presentationKey = Presentation,
                presentationClass = "Conversation", entry = NarrativeEntryPoint.MapAction, onceOnly = true, blocksSave = true });
            config.events.Add(new NarrativeEventDefinition { eventId = "atlas.fixture.busy", presentationKey = Presentation, presentationClass = "Conversation" });
            config.events.Add(new NarrativeEventDefinition { eventId = "atlas.fixture.trap" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "root-gate", ownerType = "event", ownerId = Root,
                condType = "BOOL_EQ", target = "atlas.rootGate", value = "true" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "left-gate", ownerType = "choice", ownerId = LeftChoice,
                condType = "BOOL_EQ", target = "atlas.unlocked", value = "true" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "right-gate", ownerType = "choice", ownerId = RightChoice,
                condType = "BAR_GTE", target = "cafe.money", value = "400" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "locked-gate", ownerType = "choice", ownerId = LockedChoice,
                condType = "BOOL_EQ", target = "atlas.denied", value = "true" });
            int seq = 0;
            foreach (string choice in new[] { LeftChoice, RightChoice, LockedChoice })
                config.choices.Add(new NarrativeChoiceDefinition { choiceId = choice, groupKey = "atlas.fixture.options", textKey = choice,
                    resultOwnerId = "atlas.fixture.choice-result", seq = ++seq });
            AddPage(config, ChoicePage, 1, null, "atlas.fixture.options");
            AddPage(config, LeftPage, 2, "choice:" + LeftChoice);
            AddPage(config, RightPage, 3, "choice:" + RightChoice);
            AddPage(config, "atlas.fixture.hidden", 4, "atlas.denied");
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = "atlas.fixture.end", presentationKey = Presentation,
                seq = 5, stepType = NarrativeConversationStepType.End });
            config.results.Add(new NarrativeResultDefinition { resultId = "root.reward", eventId = Root,
                resultType = NarrativeResultType.SetFlag, target = "atlas.reward", value = "true" });
            config.results.Add(new NarrativeResultDefinition { resultId = "choice.reward", eventId = "atlas.fixture.choice-result",
                resultType = NarrativeResultType.SetFlag, target = "atlas.reward", value = "true" });
            config.results.Add(new NarrativeResultDefinition { resultId = "root.queue", eventId = Root, seq = 1,
                resultType = NarrativeResultType.QueueEvent, target = "event:atlas.fixture.trap" });
            config.triggers.Add(new NarrativeTriggerDefinition { triggerId = "external-trigger", barKey = "cafe.money", threshold = 700,
                direction = NarrativeTriggerDirection.Up, crossType = NarrativeCrossType.Cross, eventId = "atlas.fixture.trap" });
            config.eventDates.Add(new NarrativeEventDateDefinition { eventId = Root, day = 99, timeSlot = NarrativeTimeSlot.Morning });
            return config;
        }

        private static void AddPage(NarrativeConfiguration config, string id, int seq, string condition = null, string options = null)
        {
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = id, presentationKey = Presentation,
                seq = seq, pageCondKeys = condition, optionGroupKey = options, bgOverride = "scene.cafe" });
            config.presentations.conversationLines.Add(new NarrativeConversationLineDefinition { lineId = id + ".line", pageId = id, textKey = id, seq = 1 });
        }
    }
}
#endif
