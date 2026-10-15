using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    public sealed class October07HandoffTests
    {
        private GameObject root;
        private PlayerPresentationTestSession session;
        private DayFlowController flow;

        [SetUp]
        public void SetUp()
        {
            session = new PlayerPresentationTestSession();
            root = new GameObject("October07 handoff test");
            flow = root.AddComponent<DayFlowController>();
            flow.Initialize(null);
            session.Bind(flow);
            Assert.That(session.Submit(null), Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            flow.HideForMainMenu();
            Object.Destroy(root);
            yield return null;
            session.Dispose();
        }

        [UnityTest]
        public IEnumerator DevelopmentHandoffWorksWithoutBusinessModuleAndPreservesPlayer()
        {
            var player = PlayerRuntime.Current;
            var controller = session.Narrative.Controller;
            flow.RestoreJson("{\"day\":1,\"state\":12,\"firstDayIntroductionCompleted\":true,\"firstDayCafeTutorialStarted\":true}");
            flow.ResumeSavedPhase();
            Assert.That(flow.CurrentPresentation, Is.TypeOf<PreBusinessPanel>());
            Assert.That(flow.CanSkipTutorialToOctober07, Is.True);
            var button = flow.GetComponentInChildren<UnityEngine.UI.Button>(true);
            Assert.That(flow.TrySkipTutorialToOctober07(), Is.True);
            Assert.That(flow.TrySkipTutorialToOctober07(), Is.False);
            Assert.That(flow.CurrentDay, Is.EqualTo(2));
            Assert.That(flow.CurrentDate, Is.EqualTo("王国纪元 670年10月7日"));
            Assert.That(PlayerRuntime.Current, Is.SameAs(player));
            Assert.That(session.Narrative.Controller, Is.SameAs(controller));
            Assert.That(flow.IsFirstSessionGuideActive, Is.False);
            Assert.That(session.Narrative.Actions.CurrentDay, Is.EqualTo(2));
            Assert.That(session.Narrative.Actions.ActionPoints.Current, Is.EqualTo(session.Narrative.Actions.ActionPoints.Max));
            Assert.That(controller.State.TryGetBar("world.calendar", out int day), Is.True);
            Assert.That(day, Is.EqualTo(2));
            float timeout = Time.realtimeSinceStartup + 8f;
            while (flow.State != DayFlowState.CityMapLeisure && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(flow.State, Is.EqualTo(DayFlowState.CityMapLeisure));
            Assert.That(flow.CurrentDialogue, Is.Null);
            Assert.That(flow.CanSave, Is.True);
        }

        [Test]
        public void JumpIsUnavailableBeforeSafeHandoffAndAfterDayOne()
        {
            Assert.That(flow.TrySkipTutorialToOctober07(), Is.False);
            flow.RestoreJson("{\"day\":2,\"state\":5,\"firstDayCafeTutorialStarted\":true}");
            flow.ResumeSavedPhase();
            Assert.That(flow.TrySkipTutorialToOctober07(), Is.False);
            Assert.That(flow.CurrentDay, Is.EqualTo(2));
        }
    }
}
