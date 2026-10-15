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
        private readonly struct MachineObservation
        {
            public readonly ProductionEntityState state;
            public readonly ProductionPhase phase;
            public MachineObservation(ProductionEntityState value) { state = value; phase = value.phase; }
        }
        private readonly Dictionary<string, MachineObservation> phases = new Dictionary<string, MachineObservation>();
        private readonly HashSet<string> slots = new HashSet<string>();
        private readonly HashSet<string> live = new HashSet<string>();
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
            phases.Clear(); slots.Clear(); live.Clear(); visits.Clear(); area = null;
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
            bool audible = Time.timeScale > 0f && !runtime.BusinessSimulationPaused;
            if (area.HasValue && area.Value != world.State.currentArea) GameAudio.Play(AudioKeys.Area);
            area = world.State.currentArea;
            live.Clear();
            // Standalone scene previews have no day-flow owner to supply ambience.
            // Formal sessions keep their phase-controlled loop as the sole owner.
            if (dayFlow == null && gameFlow == null)
                Loop("standalone-ambience", AudioKeys.CafeAmbience, audible);
            foreach (WorldEntity entity in world.State.entities)
            {
                ProductionEntityState data = entity.production;
                if (data == null) continue;
                bool local = entity.area == world.State.currentArea;
                string key = entity.specId == "machine.grinder" ? AudioKeys.Grinder : entity.specId == "machine.mixer" ? AudioKeys.Mixer : AudioKeys.Heating;
                bool processing = data.phase == ProductionPhase.Processing && entity.specId != "machine.stove";
                Loop("machine:" + entity.id, key, audible && local && processing);
                // Emptying/restoring replaces the state object; it is not a finished processing batch.
                if (phases.TryGetValue(entity.id, out MachineObservation prior) && ReferenceEquals(prior.state, data) && prior.phase == ProductionPhase.Processing &&
                    data.phase != ProductionPhase.Processing && data.remaining <= .0001f && (data.phase == ProductionPhase.Ready || data.phase == ProductionPhase.Room))
                    GameAudio.Play(AudioKeys.Ready, this);
                phases[entity.id] = new MachineObservation(data);
                WorldEntity filter = world.IsPrecisionExtractor(entity) ? world.ExtractionFilter(entity) : null;
                bool draining = filter != null && filter.production.filterDrainHold <= 0f && world.LiquidVolume(filter) > .001f &&
                    world.LiquidVolume(entity) < world.LiquidCapacity(entity) - .001f;
                Loop("drip:" + entity.id, AudioKeys.Extraction, audible && local && draining);
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
        }

        private void Loop(string slot, string key, bool active)
        {
            if (!active) return;
            live.Add(slot); slots.Add(slot);
            GameAudio.SetLoop(this, slot, key, true);
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
