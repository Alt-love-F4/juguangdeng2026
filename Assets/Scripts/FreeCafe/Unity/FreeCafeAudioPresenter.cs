using System.Collections.Generic;
using Spotlight.Audio;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    [DisallowMultipleComponent]
    public sealed class FreeCafeAudioPresenter : MonoBehaviour
    {
        private FreeCafeRuntime runtime;
        private CafeWorld world;
        private sealed class MachineObservation
        {
            public ProductionEntityState state;
            public ProductionPhase phase;
            public readonly string machineSlot, dripSlot;
            public MachineObservation(string id) { machineSlot = "machine:" + id; dripSlot = "drip:" + id; }
        }
        private readonly Dictionary<string, MachineObservation> phases = new Dictionary<string, MachineObservation>();
        private readonly HashSet<string> slots = new HashSet<string>();
        private readonly HashSet<string> live = new HashSet<string>();
        private readonly HashSet<string> observedEntities = new HashSet<string>();
        private readonly Dictionary<string, CafeVisitStatus> visits = new Dictionary<string, CafeVisitStatus>();
        private readonly List<string> removed = new List<string>();
        private AreaId? area;
        private Spotlight.GameFlow.DayFlowController dayFlow;
        private Spotlight.GameFlow.GameFlowBootstrap gameFlow;

        public void Bind(FreeCafeRuntime value)
        {
            if (world != null) world.Changed -= Observe;
            runtime = value; world = runtime.World;
            dayFlow = FindFirstObjectByType<Spotlight.GameFlow.DayFlowController>();
            gameFlow = FindFirstObjectByType<Spotlight.GameFlow.GameFlowBootstrap>();
            ResetObservation();
            if (world != null) world.Changed += Observe;
            Observe();
        }

        public void ResetObservation()
        {
            GameAudio.StopOwner(this);
            phases.Clear(); slots.Clear(); live.Clear(); observedEntities.Clear(); visits.Clear(); area = null;
        }

        private void OnEnable() { if (world != null) { world.Changed -= Observe; world.Changed += Observe; Observe(); } }
        private void OnDisable() { if (world != null) world.Changed -= Observe; ResetObservation(); }
        private void Update() => Observe();

        private void Observe()
        {
            if (runtime == null || !runtime.isActiveAndEnabled || world == null)
            {
                if (slots.Count > 0) ResetObservation();
                return;
            }
            bool audible = Time.timeScale > 0f && !runtime.BusinessSimulationPaused && !runtime.GameplayBlocked;
            if (area.HasValue && area.Value != world.State.currentArea) GameAudio.Play(AudioKeys.Area);
            area = world.State.currentArea;
            live.Clear(); observedEntities.Clear();
            // Standalone scene previews have no day-flow owner to supply ambience.
            // Formal sessions keep their phase-controlled loop as the sole owner.
            if (dayFlow == null && gameFlow == null)
                Loop("standalone-ambience", AudioKeys.CafeAmbience, audible);
            foreach (WorldEntity entity in world.State.entities)
            {
                ProductionEntityState data = entity.production;
                if (data == null) continue;
                observedEntities.Add(entity.id);
                bool local = entity.area == world.State.currentArea;
                if (!phases.TryGetValue(entity.id, out MachineObservation observation))
                {
                    observation = new MachineObservation(entity.id);
                    phases.Add(entity.id, observation);
                }
                float volume = 1f, pitch = 1f;
                bool processing = data.phase == ProductionPhase.Processing;
                string key = null;
                WorldEntity heated = null;
                if (entity.specId == "machine.grinder") key = AudioKeys.Grinder;
                else if (entity.specId == "machine.mixer") key = AudioKeys.Mixer;
                else if (entity.specId == "machine.stove")
                {
                    key = AudioKeys.Stove;
                    heated = FreeCafeSoundDynamics.StoveVessel(world, entity);
                    processing = heated != null;
                }
                else if (entity.specId == "machine.heater" && data.selfHeating)
                {
                    key = AudioKeys.Heating;
                    heated = entity;
                }
                // Externally heated cups/jugs are voiced by their stove, never twice.
                if (heated != null)
                {
                    var settings = world.Catalog.production;
                    float temperature = Mathf.InverseLerp(settings.ambientTemperatureCelsius, settings.heatingTargetCelsius, world.TemperatureCelsius(heated));
                    FreeCafeSoundDynamics.Heating(world.MachineProgress(heated).Fraction, temperature, out volume, out pitch);
                }
                Loop(observation.machineSlot, key, audible && local && processing && key != null, volume, pitch);
                // Emptying/restoring replaces the state object; it is not a finished batch.
                if (audible && local && FreeCafeSoundDynamics.Finished(observation.state, observation.phase, data))
                    GameAudio.Play(AudioKeys.Ready, this);
                observation.state = data; observation.phase = data.phase;
                WorldEntity filter = world.IsPrecisionExtractor(entity) ? world.ExtractionFilter(entity) : null;
                float filterVolume = world.LiquidVolume(filter);
                float space = Mathf.Max(0, world.LiquidCapacity(entity) - world.LiquidVolume(entity));
                bool draining = filter != null && filter.production.filterDrainHold <= 0f && filterVolume > .001f && space > .001f;
                if (draining)
                {
                    float baseFlow = Mathf.Max(.001f, world.Catalog.production.filterDrainMlPerSecond);
                    float dt = Mathf.Max(.0001f, Time.deltaTime);
                    float flow = Mathf.Min(baseFlow, Mathf.Min(filterVolume, space) / dt) / baseFlow;
                    FreeCafeSoundDynamics.Pour(flow, FreeCafeSoundDynamics.Fill(world, entity), out volume, out pitch);
                }
                Loop(observation.dripSlot, AudioKeys.Extraction, audible && local && draining, volume, pitch);
            }
            foreach (CafeVisitInstance visit in world.State.cafeVisits)
            {
                if (visits.TryGetValue(visit.visitInstanceId, out CafeVisitStatus prior))
                {
                    if (prior != visit.status && visit.status == CafeVisitStatus.SeatedBeforeServe) GameAudio.Play(AudioKeys.Arrive, this);
                    if (prior != visit.status && visit.status == CafeVisitStatus.Leaving) GameAudio.Play(AudioKeys.Leave, this);
                }
                visits[visit.visitInstanceId] = visit.status;
            }
            removed.Clear();
            foreach (string slot in slots) if (!live.Contains(slot)) removed.Add(slot);
            foreach (string slot in removed) { GameAudio.SetLoop(this, slot, null, false); slots.Remove(slot); }
            removed.Clear();
            foreach (var pair in phases) if (!observedEntities.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (string id in removed) phases.Remove(id);
        }

        private void Loop(string slot, string key, bool active, float volume = 1f, float pitch = 1f)
        {
            if (!active) return;
            live.Add(slot); slots.Add(slot);
            GameAudio.SetLoop(this, slot, key, true, volume, pitch);
        }

        public static string FeedbackKey(CafeFeedbackKind kind)
        {
            switch (kind)
            {
                case CafeFeedbackKind.DeliverySatisfied: return AudioKeys.OrderSuccess;
                case CafeFeedbackKind.DeliveryRemake: return AudioKeys.OrderRemake;
                case CafeFeedbackKind.DeliveryFailed: return AudioKeys.OrderFailed;
                case CafeFeedbackKind.CupPickup: return AudioKeys.CupPickup;
                case CafeFeedbackKind.Froth: return AudioKeys.Froth;
                case CafeFeedbackKind.Discard: return AudioKeys.Discard;
                case CafeFeedbackKind.Empty: return AudioKeys.Empty;
                case CafeFeedbackKind.GrinderTurn: return AudioKeys.GrinderTurn;
                case CafeFeedbackKind.LatteComplete: return AudioKeys.LatteComplete;
                default: return null;
            }
        }
    }
}
