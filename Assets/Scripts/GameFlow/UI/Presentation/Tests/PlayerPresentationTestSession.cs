using System;
using System.Reflection;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow.Tests
{
    /// <summary>Isolated owners for presentation tests, not a substitute for Bootstrap scene-flow verification.</summary>
    internal sealed class PlayerPresentationTestSession : IDisposable
    {
        private readonly PlayerRuntime previousPlayer = PlayerRuntime.Current;
        private readonly NarrativeRuntime previousNarrative = NarrativeRuntime.Instance;
        private readonly PropertyInfo singleton = typeof(NarrativeRuntime).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
        private readonly GameObject root;
        private PlayerSessionCoordinator coordinator;
        internal NarrativeRuntime Narrative { get; }
        internal CafeWorld World { get; }

        internal PlayerPresentationTestSession(bool includeDayFlowArrival = false)
        {
            var config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", ownerId = "world", min = 1, max = 9999,
                initValue = 1, advanceMode = NarrativeAdvanceMode.EventDriven });
            foreach (string id in FirstDayLetterDraft.AttributeKeys)
                config.bars.Add(new NarrativeBarDefinition { barKey = "player." + id, ownerId = "player", min = 0, max = 20,
                    initValue = 0, advanceMode = NarrativeAdvanceMode.EventDriven });
            foreach (string id in FirstDayLetterDraft.SpecializationIds)
                config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "specialization.player." + id });
            if (includeDayFlowArrival)
            {
                // This fixture tests day-flow completion boundaries, not first-arrival content.
                config.events.Add(new NarrativeEventDefinition { eventId = "arrival.start" });
                config.events.Add(new NarrativeEventDefinition { eventId = "arrival.complete" });
                config.results.Add(new NarrativeResultDefinition { resultId = "fixture.arrival.complete", eventId = "arrival.start",
                    resultType = NarrativeResultType.QueueEvent, target = "event:arrival.complete" });
            }
            root = new GameObject("Isolated player presentation session");
            root.SetActive(false);
            Narrative = root.AddComponent<NarrativeRuntime>();
            Narrative.Initialize(config, new ImmediateNarrativePresenter());
            singleton.GetSetMethod(true).Invoke(null, new object[] { Narrative });
            PlayerRuntime.Current = new PlayerRuntime(PlayerCreationCatalog.Load());
            World = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
        }

        internal void Bind(DayFlowController flow)
        {
            coordinator = new PlayerSessionCoordinator();
            coordinator.Bind(Narrative, World, flow);
        }

        internal bool Submit(FirstDayLetterDraft draft)
        {
            bool success = coordinator.Submit(draft);
            if (success) Narrative.Controller.CompleteValueRestore(false);
            return success;
        }

        internal void BindLetter(FirstDayLetterPanel panel)
        {
            panel.SubmitDraft = Submit;
            panel.SubmitDefault = () => Submit(null);
        }

        public void Dispose()
        {
            coordinator?.Dispose();
            UnityEngine.Object.DestroyImmediate(root);
            singleton.GetSetMethod(true).Invoke(null, new object[] { previousNarrative });
            PlayerRuntime.Current = previousPlayer;
        }
    }
}
