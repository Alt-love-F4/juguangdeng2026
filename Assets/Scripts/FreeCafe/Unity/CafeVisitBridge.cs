using System;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;
using Spotlight.GameFlow;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>Scene-owned translation boundary. Never starts NCS and never spawns scene objects.</summary>
    public sealed class CafeVisitBridge : IDisposable
    {
        private readonly FreeCafeRuntime owner;
        private NarrativeRuntime runtime;
        private readonly HashSet<string> warnings = new HashSet<string>();
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private bool pumping;
        private long appliedVersion = -1;
        private CafeSnapshot appliedState;
        public bool Ready { get; private set; }
        public bool HasStageHandoff => owner.World.State.cafeVisitFeedback.Any(f => !f.isClick);
        public CafeVisitBridge(FreeCafeRuntime owner) { this.owner = owner; }
        public void Dispose()
        {
            if (runtime != null) runtime.CafeVisitsCommitted -= OnCommitted;
            runtime = null; Ready = false; appliedVersion = -1; appliedState = null;
        }
        private void Warn(string key, string text)
        { if (warnings.Add(key)) Debug.LogWarning("[CafeVisit/configuration_error] " + text, owner); }
        private void OnCommitted(NarrativeCafeVisitSnapshot snapshot) { Ready = false; appliedVersion = -1; }
        public bool Pump()
        {
            if (pumping) return Ready;
            pumping = true;
            try
            {
                var next = NarrativeRuntime.Instance;
                if (next != runtime)
                {
                    Dispose(); runtime = next;
                    if (runtime != null) runtime.CafeVisitsCommitted += OnCommitted;
                }
                if (runtime == null || !runtime.IsReady) return Ready = false;
                // Both restore orders converge here, after the synchronous GameFlow restore transaction.
                if (GameSaveParticipantRegistry.PendingRestore != null && GameSaveParticipantRegistry.PendingRestore.Any(p => p != null && p.Id == FreeCafeRuntime.SaveId)) return Ready = false;
                var snapshot = runtime.GetCommittedCafeVisits();
                if (snapshot == null || !snapshot.IsReady) return Ready = false;
                if (snapshot.Version != appliedVersion || appliedState != owner.World.State)
                {
                var intents = new List<CafeVisitIntent>();
                foreach (var entry in snapshot.Visits)
                {
                    var character = owner.Character(entry.CharacterId);
                    bool invalid = character == null || character.worldSprite == null;
                    if (invalid && !entry.IsEnded) Warn(entry.VisitInstanceId + ":identity", entry.CharacterId + " 无有效世界资源，拒绝来访 configuration_invalid。");
                    intents.Add(new CafeVisitIntent
                    {
                        visitInstanceId = entry.VisitInstanceId, characterId = entry.CharacterId, visitKey = entry.VisitKey,
                        arrivalAuthorized = entry.ArrivalAuthorized, isEnded = entry.IsEnded,
                        orderProductId = entry.OrderProductId, requireRealProduction = entry.RequireRealProduction,
                        endReason = entry.EndReason, preferredSeatId = entry.PreferredSeatId,
                        seatedVisualPath = entry.SeatedVisualPath, arrivalDelaySeconds = entry.ArrivalDelaySeconds,
                        hasPatienceOverride = entry.PatienceSeconds.HasValue, patienceSeconds = entry.PatienceSeconds ?? 0,
                        leaveAfterServed = entry.LeaveAfterServed, usesDefaultConfiguration = entry.UsesDefaultConfiguration,
                        identity = invalid ? null : new CafePersonIdentity { personId = entry.CharacterId, characterId = entry.CharacterId,
                            narrativeCharacterId = entry.CharacterId, displayName = character.displayName, role = CafePersonRole.Customer, visualId = "customer" }
                    });
                }
                var authorized = new HashSet<string>(snapshot.Visits.Select(v => v.VisitInstanceId));
                foreach (var old in owner.World.State.cafeVisits.ToArray())
                    if (!authorized.Contains(old.visitInstanceId) && old.status != CafeVisitStatus.Ended)
                    {
                        Warn(old.visitInstanceId + ":orphan", "已完成恢复后 NCS 不含此来访，清理旧咖啡馆实例：" + old.visitInstanceId);
                        owner.World.EndCafeVisit(old.visitInstanceId, "reservation_cancelled");
                    }
                owner.World.SyncCafeVisits(intents);
                appliedVersion = snapshot.Version;
                appliedState = owner.World.State;
                }
                Ready = true;
                foreach (var visit in owner.World.State.cafeVisits)
                    foreach (string diagnostic in visit.diagnostics)
                        Warn(visit.visitInstanceId + ":" + diagnostic, visit.visitInstanceId + " " + diagnostic);
                foreach (var fact in owner.World.State.cafeVisitFeedback.ToArray())
                {
                    var dto = new NarrativeCafeVisitFeedback { visitInstanceId = fact.visitInstanceId, characterId = fact.characterId,
                        stage = fact.stage, reason = fact.reason, sequence = fact.sequence, isClick = fact.isClick,
                        orderId = fact.orderId, productId = fact.productId,
                        serviceReceipt = fact.serviceReceipt == null ? null : new NarrativeCafeServiceReceipt
                        {
                            visitInstanceId = fact.serviceReceipt.visitInstanceId, characterId = fact.serviceReceipt.characterId,
                            orderId = fact.serviceReceipt.orderId, productId = fact.serviceReceipt.productId, cupId = fact.serviceReceipt.cupId,
                            classifiedProductId = fact.serviceReceipt.classifiedProductId, realProduction = fact.serviceReceipt.realProduction,
                            satisfied = fact.serviceReceipt.satisfied, correctType = fact.serviceReceipt.correctType,
                            latteArtSatisfied = fact.serviceReceipt.latteArtSatisfied, score = fact.serviceReceipt.score, minimumScore = fact.serviceReceipt.minimumScore
                        } };
                    bool known = snapshot.Visits.Any(v => v.VisitInstanceId == fact.visitInstanceId && v.CharacterId == fact.characterId);
                    if (!known) { owner.World.AcknowledgeCafeVisitFeedback(fact.sequence); continue; }
                    runtime.SubmitCafeVisitFeedback(dto);
                    if (runtime.IsCafeVisitFeedbackComplete(dto)) owner.World.AcknowledgeCafeVisitFeedback(fact.sequence);
                }
                return true;
            }
            finally { pumping = false; }
        }
        public bool Click(string customerId)
        {
            var visit = owner.World.State.cafeVisits.Find(v => v.customerId == customerId && v.status != CafeVisitStatus.Ended);
            if (visit == null) return false;
            owner.World.RequestCafeVisitClick(visit.visitInstanceId); Pump(); return true;
        }
        public Sprite SeatedSprite(string customerId)
        {
            var visit = owner.World.State.cafeVisits.Find(v => v.customerId == customerId && v.status != CafeVisitStatus.Ended);
            if (visit == null || string.IsNullOrEmpty(visit.seatedVisualPath) || string.IsNullOrEmpty(visit.preferredSeatId) || visit.seatId != visit.preferredSeatId) return null;
            if (!sprites.TryGetValue(visit.seatedVisualPath, out var sprite))
            {
                sprite = Resources.Load<Sprite>(visit.seatedVisualPath);
                sprites[visit.seatedVisualPath] = sprite;
                if (sprite == null) Warn(visit.visitInstanceId + ":visual", visit.seatedVisualPath + " 专属素材无效，使用普通室内资源；专属素材未验证。");
            }
            return sprite;
        }
    }
}
