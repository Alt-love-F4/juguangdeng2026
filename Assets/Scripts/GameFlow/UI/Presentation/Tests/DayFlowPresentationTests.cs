using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    public sealed class DayFlowPresentationTests
    {
        private const string TestTag = "day-flow-presentation-tests";
        private GameObject controllerObject;
        private GameObject endpointObject;
        private float oldTimeScale;
        private PlayerPresentationTestSession session;

        [SetUp]
        public void SetUp()
        {
            oldTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            GameSaveParticipantRegistry.PendingRestore = null;
            session = new PlayerPresentationTestSession(includeDayFlowArrival: true);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (controllerObject != null) Object.Destroy(controllerObject);
            if (endpointObject != null) Object.Destroy(endpointObject);
            UIKit.ClosePanelsByTag(TestTag);
            GameSaveParticipantRegistry.PendingRestore = null;
            Time.timeScale = oldTimeScale;
            yield return null;
            session.Dispose();
            session = null;
        }

        [TestCase(typeof(DateChangePanel))]
        [TestCase(typeof(PreBusinessPanel))]
        [TestCase(typeof(CafeOpeningPanel))]
        [TestCase(typeof(BusinessEndPanel))]
        [TestCase(typeof(CityMapPanel))]
        [TestCase(typeof(PreNightPanel))]
        [TestCase(typeof(HomeNightPanel))]
        [TestCase(typeof(DayEndPanel))]
        public void PrefabHasOwnClassAndCommonInterface(Type panelType)
        {
            GameObject prefab = Resources.Load<GameObject>("Art/UIPrefab/" + panelType.Name);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent(panelType), Is.InstanceOf<IDayFlowPresentation>());
            Assert.That(prefab.GetComponent<CanvasGroup>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(600));
            Assert.That(prefab.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<RawImage>(true).texture, Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<Button>(true), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator AutomaticDateCompletesOnceEvenWhenTimeIsPaused()
        {
            DateChangePanel panel = Open<DateChangePanel>();
            int completions = 0;
            panel.SetDates("旧日期", "新日期");
            Time.timeScale = 0f;
            ((IDayFlowPresentation)panel).Play(() => completions++);
            yield return WaitFor(() => !panel.IsPlaying);
            panel.Continue();
            yield return null;
            Assert.That(completions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DateChangeTearsOffYesterdayAndShowsOnlyCalendarDate()
        {
            DateChangePanel panel = Open<DateChangePanel>();
            panel.SetDates("王国纪元 670年12月31日", "王国纪元 671年1月1日");
            TMP_Text Text(string path) => panel.transform.Find("Content/Calendar/" + path).GetComponent<TMP_Text>();
            Assert.That(Text("OldPage/Day").text, Is.EqualTo("31"));
            Assert.That(Text("OldPage/Month").text, Is.EqualTo("十二月"));
            Assert.That(Text("NewPage/Day").text, Is.EqualTo("1"));
            Assert.That(Text("NewPage/Month").text, Is.EqualTo("一月"));
            Assert.That(Text("NewPage/MonthLatin").text, Is.EqualTo("JANVIER"));

            panel.Play(null);
            yield return null;
            // The year plate starts on yesterday's year and flips mid-transition.
            Assert.That(Text("Header").text, Is.EqualTo("王国纪元 670年"));
            yield return WaitFor(() => !panel.IsPlaying);
            Assert.That(Text("Header").text, Is.EqualTo("王国纪元 671年"));
            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
                Assert.That(text.text, Does.Not.Contain(":"), "Date transition must not show clock time: " + text.name);
        }

        [UnityTest]
        public IEnumerator StopNeverInvokesCompletion()
        {
            DateChangePanel panel = Open<DateChangePanel>();
            int completions = 0;
            panel.Play(() => completions++);
            panel.Stop();
            yield return new WaitForSecondsRealtime(2.3f);
            Assert.That(panel.IsPlaying, Is.False);
            Assert.That(completions, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ReplayingReplacesOldCompletion()
        {
            DateChangePanel panel = Open<DateChangePanel>();
            int oldCompletions = 0, newCompletions = 0;
            panel.Play(() => oldCompletions++);
            panel.Stop();
            panel.Play(() => newCompletions++);
            yield return WaitFor(() => !panel.IsPlaying);
            Assert.That(oldCompletions, Is.Zero);
            Assert.That(newCompletions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ConfirmationWaitsForEntranceAndIgnoresDoubleSubmit()
        {
            PreBusinessPanel panel = Open<PreBusinessPanel>();
            int completions = 0;
            panel.Play(() => completions++);
            panel.Continue();
            Assert.That(completions, Is.Zero);
            Assert.That(panel.IsPlaying, Is.True);
            yield return Submit(panel);
            panel.Continue();
            yield return WaitFor(() => !panel.IsPlaying);
            Assert.That(completions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ClosingPanelCancelsItsPendingCallback()
        {
            DateChangePanel panel = Open<DateChangePanel>();
            int completions = 0;
            panel.Play(() => completions++);
            UIKit.ClosePanel(panel);
            yield return new WaitForSecondsRealtime(2.3f);
            Assert.That(completions, Is.Zero);
        }

        [UnityTest]
        public IEnumerator FormalDayStartsOnMapAndPresentationsAdvanceOnlyOnCompletion()
        {
            controllerObject = new GameObject("DayFlowTest");
            var flow = controllerObject.AddComponent<DayFlowController>();
            flow.Initialize(null);
            session.Bind(flow);
            endpointObject = new GameObject("DayFlowEndpointTest");
            var endpoint = endpointObject.AddComponent<DayFlowTestEndpoint>();
            // An already-open endpoint must not bypass the date presentation.
            endpoint.active = true;
            Assert.That(session.Submit(null), Is.True);
            flow.BeginNewSession(playLetter: false, playIntroduction: false);
            Assert.That(flow.State, Is.EqualTo(DayFlowState.DateTransition));
            Assert.That(flow.CurrentPresentation, Is.TypeOf<DateChangePanel>());
            Assert.That(flow.BlocksGameplayInput, Is.True);
            Assert.That(PlayerRuntime.Current.HasSelectedAvatar, Is.True);
            Assert.That(PlayerRuntime.Current.Avatar, Is.EqualTo(AvatarKind.Male));
            Assert.That(flow.CurrentAvatarSelection, Is.Null);
            yield return null;
            Assert.That(flow.State, Is.EqualTo(DayFlowState.DateTransition), "An active endpoint cannot skip the date presentation.");
            yield return WaitFor(() => flow.State == DayFlowState.CityMapLeisure);
            Assert.That(flow.IsFirstDayMapGuideActive, Is.True);
            Assert.That(flow.CurrentPresentation, Is.Null, "The formal map replaces the old CityMap/PreBusiness presentation.");
            Assert.That(flow.BlocksGameplayInput, Is.False);
            Assert.That(endpoint.starts, Is.Zero, "Reaching the map must not automatically start business.");
            flow.StartCafeFromMap();
            Assert.That(flow.State, Is.EqualTo(DayFlowState.BusinessTransition));
            Assert.That(flow.CurrentPresentation, Is.TypeOf<CafeOpeningPanel>());
            Assert.That(endpoint.starts, Is.Zero);
            yield return Submit((DayFlowPresentationPanel)flow.CurrentPresentation);
            yield return WaitFor(() => flow.State == DayFlowState.FreeCafeBusiness);
            Assert.That(flow.BlocksGameplayInput, Is.False);
            Assert.That(endpoint.starts, Is.EqualTo(1));
            flow.FinishBusiness();
            Assert.That(endpoint.ends, Is.EqualTo(1));
            Assert.That(flow.CurrentPresentation, Is.TypeOf<BusinessEndPanel>());
            yield return Submit((DayFlowPresentationPanel)flow.CurrentPresentation);
            yield return WaitFor(() => flow.State == DayFlowState.CityMapLeisure);
            Assert.That(flow.CurrentPresentation, Is.Null);
            Assert.That(flow.BlocksGameplayInput, Is.False);
            flow.EndDayFromMap();
            Assert.That(flow.State, Is.EqualTo(DayFlowState.DateTransition));
            Assert.That(flow.CurrentPresentation, Is.TypeOf<DateChangePanel>());
            StringAssert.Contains("10月7日", flow.CurrentDate);
            yield return new WaitForSecondsRealtime(4.5f);
            yield return WaitFor(() => flow.State == DayFlowState.CityMapLeisure);
            flow.HideForMainMenu();
            Assert.That(flow.CurrentPresentation, Is.Null);
            Assert.That(flow.BlocksGameplayInput, Is.False);
        }

        [UnityTest]
        public IEnumerator RestoringPhaseDoesNotReplayNewGameDateAndNewSessionResetsDay()
        {
            controllerObject = new GameObject("DayFlowRestoreTest");
            var flow = controllerObject.AddComponent<DayFlowController>();
            flow.Initialize(null);
            session.Bind(flow);
            GameSaveParticipantRegistry.PendingRestore = new List<GameSaveParticipantData>
            {
                new GameSaveParticipantData { Id = DayFlowController.SaveId,
                    Json = "{\"day\":5,\"previousDate\":\"昨日\",\"state\":6}" }
            };
            flow.RestoreSavedDayFlow();
            Assert.That(flow.State, Is.EqualTo(DayFlowState.DateTransition));
            Assert.That(flow.CurrentPresentation, Is.TypeOf<DateChangePanel>());
            StringAssert.Contains("10月11日", flow.CurrentDate);
            Assert.That(GameSaveParticipantRegistry.TakePending(DayFlowController.SaveId), Is.Null);
            Assert.That(session.Submit(null), Is.True);
            flow.BeginNewSession(playLetter: false, playIntroduction: false);
            Assert.That(flow.State, Is.EqualTo(DayFlowState.DateTransition));
            Assert.That(flow.CurrentPresentation, Is.TypeOf<DateChangePanel>());
            Assert.That(flow.BlocksGameplayInput, Is.True);
            Assert.That(PlayerRuntime.Current.HasSelectedAvatar, Is.True);
            Assert.That(PlayerRuntime.Current.Avatar, Is.EqualTo(AvatarKind.Male));
            Assert.That(flow.CurrentAvatarSelection, Is.Null);
            Assert.That(flow.State, Is.EqualTo(DayFlowState.DateTransition));
            StringAssert.Contains("10月6日", flow.CurrentDate);
            flow.HideForMainMenu();
            yield return new WaitForSecondsRealtime(2.3f);
            Assert.That(flow.State, Is.EqualTo(DayFlowState.DateTransition));
            Assert.That(flow.CurrentPresentation, Is.Null);
        }

        private static T Open<T>() where T : DayFlowPresentationPanel
        {
            T panel = UIKit.OpenPanel<T>(UILevel.Pop, tag: TestTag, cachePolicy: PanelCachePolicy.Transient);
            Assert.That(panel.GetComponent<Canvas>().overrideSorting, Is.True);
            Assert.That(panel.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(600));
            return panel;
        }

        private static IEnumerator Submit(DayFlowPresentationPanel panel)
        {
            Button button = panel.GetComponentInChildren<Button>(true);
            yield return WaitFor(() => button.interactable);
            button.onClick.Invoke();
        }

        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Presentation timed out.");
                yield return null;
            }
        }
    }
}
