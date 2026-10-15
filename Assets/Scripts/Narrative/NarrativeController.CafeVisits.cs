using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spotlight.Narrative
{
    public sealed partial class NarrativeController
    {
        private readonly List<NarrativeCafeVisitSave> cafeVisits = new List<NarrativeCafeVisitSave>();
        private readonly List<NarrativeCafeFeedbackSave> cafeFeedback = new List<NarrativeCafeFeedbackSave>();
        private readonly Dictionary<string, string> cafeLocationOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Action>> cafeWaiters = new Dictionary<string, List<Action>>(StringComparer.Ordinal);
        private long cafeVisitVersion, cafeVisitSequence;
        private bool cafeDirty, pumpingCafe;
        private NarrativeCafeVisitSnapshot committedCafeVisits = new NarrativeCafeVisitSnapshot(0, true, new List<NarrativeCafeVisit>());
        public event Action<NarrativeCafeVisitSnapshot> CafeVisitsCommitted;
        public NarrativeCafeVisitSnapshot GetCommittedCafeVisits() => committedCafeVisits;

        private static string CafeFactKey(NarrativeCafeVisitFeedback f) => f.isClick ? "click:" + f.sequence : "auto:" + f.stage;
        private static string CafeWaiterKey(NarrativeCafeVisitFeedback f) => f.visitInstanceId + "/" + CafeFactKey(f);
        public bool IsCafeVisitFeedbackComplete(NarrativeCafeVisitFeedback fact)
        {
            if (fact == null) return false;
            var visit = cafeVisits.Find(v => v.id == fact.visitInstanceId && v.characterId == fact.characterId);
            return visit != null && visit.completedFacts.Contains(CafeFactKey(fact));
        }

        /// <summary>Copies and durably queues a fact. Completion means every eligible hook has settled or been skipped.</summary>
        public bool SubmitCafeVisitFeedback(NarrativeCafeVisitFeedback fact, Action completed = null)
        {
            if (fact == null || fact.sequence < 0 || !ValidCafeStage(fact.stage) ||
                fact.isClick && fact.stage != "before_served" && fact.stage != "served") return false;
            var visit = cafeVisits.Find(v => v.id == fact.visitInstanceId && v.characterId == fact.characterId);
            if (visit == null) return false;
            var copy = fact.Copy();
            string key = CafeWaiterKey(copy);
            if (completed != null)
            {
                if (!cafeWaiters.TryGetValue(key, out var waiters)) cafeWaiters.Add(key, waiters = new List<Action>());
                waiters.Add(completed);
            }
            if (!visit.completedFacts.Contains(CafeFactKey(copy)) && !cafeFeedback.Any(w => CafeWaiterKey(w.fact) == key))
                cafeFeedback.Add(new NarrativeCafeFeedbackSave { fact = copy });
            CheckEndingsAtSafeBoundary();
            return true;
        }

        private static int CafeStageOrder(string stage)
        {
            switch (stage)
            {
                case "reserved": return 0;
                case "waiting": return 1;
                case "arrived": return 2;
                case "before_served": return 3;
                case "served": return 4;
                case "leaving": return 5;
                case "ended": return 6;
                default: return -1;
            }
        }

        private static bool ValidCafeStage(string stage) => stage == "reserved" || stage == "waiting" || stage == "arrived" ||
            stage == "before_served" || stage == "served" || stage == "leaving" || stage == "ended";

        private void ReserveCafeVisit(string character, string key)
        {
            if (cafeVisits.Any(v => !v.ended && v.characterId == character))
            { Debug.LogWarning("[NCS][Cafe][merged] Duplicate reservation: " + character); return; }
            var definition = configuration.cafeVisits.Find(v => v != null && v.visitKey == key && v.characterId == character);
            if (definition == null) { Debug.LogWarning("[NCS][Cafe][configuration_invalid] Unknown reservation: " + key); return; }
            CreateCafeVisit(character, definition, false);
        }

        private NarrativeCafeVisitSave CreateCafeVisit(string character, NarrativeCafeVisitDefinition definition, bool arrival)
        {
            var visit = new NarrativeCafeVisitSave
            {
                id = Guid.NewGuid().ToString("N"), order = ++cafeVisitSequence, characterId = character,
                visitKey = definition?.visitKey ?? string.Empty, preferredSeatId = definition?.preferredSeatId,
                seatedVisualPath = definition?.seatedVisualPath, arrivalAuthorized = arrival,
                stage = arrival ? "waiting" : "reserved", defaults = definition == null,
                leaveAfterServed = definition?.leaveAfterServed ?? true
            };
            if (definition != null)
            {
                float delay = definition.arrivalDelaySeconds;
                if (float.IsNaN(delay) || float.IsInfinity(delay) || delay < 0)
                    Debug.LogWarning("[NCS][Cafe][default] Invalid delay: " + definition.visitKey);
                else visit.arrivalDelaySeconds = delay;
                if (definition.patienceSeconds.HasValue)
                {
                    float patience = definition.patienceSeconds.Value;
                    if (float.IsNaN(patience) || float.IsInfinity(patience) || patience <= 0)
                        Debug.LogWarning("[NCS][Cafe][default] Invalid patience: " + definition.visitKey);
                    else { visit.hasPatience = true; visit.patienceSeconds = patience; }
                }
            }
            else Debug.LogWarning("[NCS][Cafe][default] No visit preset: " + character);
            cafeVisits.Add(visit);
            cafeDirty = true;
            if (!configuration.objects.Any(o => o != null && o.objId == character && o.objType == NarrativeObjectType.Npc))
            {
                EndCafeVisit(visit, "configuration_invalid");
                Debug.LogWarning("[NCS][Cafe][configuration_invalid] Unknown NPC: " + character);
            }
            return visit;
        }

        private void MoveCafeNpc(string character, string location)
        {
            var visit = cafeVisits.Find(v => !v.ended && v.characterId == character);
            if (location == "cafe")
            {
                if (visit == null)
                {
                    var presets = configuration.cafeVisits.Where(v => v != null && v.characterId == character).ToList();
                    if (presets.Count > 1)
                    { Debug.LogWarning("[NCS][Cafe][configuration_invalid] Ambiguous visit preset: " + character); return; }
                    visit = CreateCafeVisit(character, presets.FirstOrDefault(), true);
                }
                if (visit.ended) return;
                if (!visit.arrivalAuthorized) { visit.arrivalAuthorized = true; visit.stage = "waiting"; cafeDirty = true; }
                cafeLocationOwners[character] = visit.id;
                state.SetNpcLocation(character, location);
            }
            else
            {
                if (visit != null) EndCafeVisit(visit, string.IsNullOrEmpty(location)
                    ? (visit.arrivalAuthorized ? "ncs_despawn" : "reservation_cancelled") : "location_changed");
                cafeLocationOwners.Remove(character);
                state.SetNpcLocation(character, location);
            }
        }

        private void EndCafeVisit(NarrativeCafeVisitSave visit, string reason)
        {
            if (visit.ended) return;
            visit.ended = true; visit.stage = "ended"; visit.endReason = reason; cafeDirty = true;
            if (cafeLocationOwners.TryGetValue(visit.characterId, out string owner) && owner == visit.id)
            {
                cafeLocationOwners.Remove(visit.characterId);
                if (state.GetNpcLocation(visit.characterId) == "cafe") state.SetNpcLocation(visit.characterId, string.Empty);
            }
        }

        private void PublishCafeVisits()
        {
            if (!cafeDirty) return;
            cafeDirty = false;
            committedCafeVisits = new NarrativeCafeVisitSnapshot(++cafeVisitVersion, true, cafeVisits.Select(v => new NarrativeCafeVisit(v)).ToList());
            var listeners = CafeVisitsCommitted;
            if (listeners == null) return;
            foreach (Action<NarrativeCafeVisitSnapshot> listener in listeners.GetInvocationList())
                try { listener(committedCafeVisits); } catch (Exception e) { Debug.LogException(e); }
        }

        private void PumpCafeBoundary()
        {
            if (pumpingCafe || !IsSafeBoundary) return;
            pumpingCafe = true;
            try
            {
                PublishCafeVisits();
                while (IsSafeBoundary && cafeFeedback.Count > 0)
                {
                    var work = cafeFeedback[0];
                    var fact = work.fact;
                    var visit = cafeVisits.Find(v => v.id == fact.visitInstanceId && v.characterId == fact.characterId);
                    if (visit == null) { cafeFeedback.RemoveAt(0); continue; }
                    if (!work.accepted)
                    {
                        work.accepted = true;
                        string key = CafeFactKey(fact);
                        bool eligible = !visit.acceptedFacts.Contains(key) &&
                            (!visit.ended || fact.stage == "ended" || fact.stage == "leaving") &&
                            (fact.isClick || fact.stage == "reserved" || visit.ended && fact.stage == "leaving" || CafeStageOrder(fact.stage) >= CafeStageOrder(visit.stage)) &&
                            (visit.arrivalAuthorized || fact.stage == "reserved" || fact.stage == "leaving" || fact.stage == "ended") &&
                            (!fact.isClick || (fact.stage == "served"
                                ? !visit.leaveAfterServed && visit.stage == "served"
                                : visit.stage == "arrived" || visit.stage == "before_served"));
                        if (!visit.acceptedFacts.Contains(key)) visit.acceptedFacts.Add(key);
                        if (eligible)
                        {
                            if (fact.stage == "ended") EndCafeVisit(visit, fact.reason ?? "business_closed");
                            else if (!fact.isClick && !visit.ended && CafeStageOrder(fact.stage) >= CafeStageOrder(visit.stage))
                            { visit.stage = fact.stage; cafeDirty = true; }
                            work.hooks.AddRange(configuration.cafeVisitHooks.Where(h => h != null && h.visitKey == visit.visitKey && h.stage == fact.stage && h.trigger == (fact.isClick ? "click" : "auto"))
                                .OrderBy(h => h.seq).Select(h => h.eventId));
                            if (work.hooks.Count == 0) Debug.Log("[NCS][Cafe][unconfigured] stage=" + fact.stage + " visit=" + visit.id);
                        }
                        PublishCafeVisits(); // Ended is visible before any follow-up hook runs.
                    }
                    if (!string.IsNullOrEmpty(work.waitingEvent)) break;
                    if (work.nextHook < work.hooks.Count)
                    {
                        string eventId = work.hooks[work.nextHook++];
                        if (fact.isClick && visit.ended) continue;
                        work.waitingEvent = eventId;
                        if (!QueueEvent(eventId))
                        {
                            work.waitingEvent = null;
                            Debug.Log("[NCS][Cafe][rejected] event=" + eventId + " reason=" + CafeEventRejection(eventId));
                        }
                        if (!string.IsNullOrEmpty(work.waitingEvent)) break;
                        continue;
                    }
                    if (!visit.completedFacts.Contains(CafeFactKey(fact))) visit.completedFacts.Add(CafeFactKey(fact));
                    cafeFeedback.RemoveAt(0);
                }
                foreach (string key in cafeWaiters.Keys.ToArray())
                {
                    if (!cafeVisits.Any(v => v.completedFacts.Any(f => v.id + "/" + f == key))) continue;
                    var callbacks = cafeWaiters[key]; cafeWaiters.Remove(key);
                    foreach (var callback in callbacks) try { callback(); } catch (Exception e) { Debug.LogException(e); }
                }
                PublishCafeVisits();
            }
            finally { pumpingCafe = false; }
        }

        private string CafeEventRejection(string id)
        {
            if (string.IsNullOrEmpty(id) || !events.TryGetValue(id, out var definition)) return "missing_event";
            if (IsEnded) return "session_ended";
            if (state.IsEventLocked(id)) return "locked";
            if (state.IsFlagSet("event.once." + id) || definition.onceOnly && state.HasHappenedEvent(id)) return "once_only";
            return "conditions_or_queue";
        }

        private void CompleteCafeHook(string eventId)
        {
            if (cafeFeedback.Count > 0 && cafeFeedback[0].waitingEvent == eventId) cafeFeedback[0].waitingEvent = null;
        }

        private void SkipCafeFeedbackBatch()
        {
            foreach (var work in cafeFeedback)
            {
                // Settle both a discarded queued hook and hooks not yet queued; never play a lost batch.
                work.accepted = true;
                work.waitingEvent = null;
                work.nextHook = work.hooks.Count;
                var visit = cafeVisits.Find(v => v.id == work.fact.visitInstanceId && v.characterId == work.fact.characterId);
                if (visit != null && !visit.acceptedFacts.Contains(CafeFactKey(work.fact)))
                    visit.acceptedFacts.Add(CafeFactKey(work.fact));
            }
        }

        private void CaptureCafeVisits(NarrativeSnapshot snapshot)
        {
            snapshot.cafeVisitVersion = cafeVisitVersion; snapshot.cafeVisitSequence = cafeVisitSequence;
            snapshot.cafeVisits.AddRange(cafeVisits.Select(v => v.Copy()));
            snapshot.cafeFeedback.AddRange(cafeFeedback.Select(w => w.Copy()));
            foreach (var pair in cafeLocationOwners) snapshot.cafeLocationOwners.Add(new NarrativeStringValue { key = pair.Key, value = pair.Value });
        }

        private void RestoreCafeVisits(NarrativeSnapshot snapshot)
        {
            cafeVisits.Clear(); cafeFeedback.Clear(); cafeLocationOwners.Clear(); cafeWaiters.Clear();
            cafeVisitVersion = snapshot.cafeVisitVersion; cafeVisitSequence = snapshot.cafeVisitSequence;
            if (snapshot.cafeVisits != null) cafeVisits.AddRange(snapshot.cafeVisits.Where(v => v != null).Select(v => v.Copy()));
            if (snapshot.cafeFeedback != null) cafeFeedback.AddRange(snapshot.cafeFeedback.Where(w => w?.fact != null).Select(w => w.Copy()));
            if (snapshot.cafeLocationOwners != null) foreach (var pair in snapshot.cafeLocationOwners)
                if (pair?.key != null) cafeLocationOwners[pair.key] = pair.value;
            cafeDirty = true;
            committedCafeVisits = new NarrativeCafeVisitSnapshot(cafeVisitVersion, false, cafeVisits.Select(v => new NarrativeCafeVisit(v)).ToList());
        }
    }
}
