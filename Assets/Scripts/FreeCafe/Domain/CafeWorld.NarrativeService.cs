using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    /// <summary>One source-authored quota, evaluated by the existing real product evaluator.</summary>
    public sealed class CafeNarrativeOrderSpec
    {
        public string productId;
        public float minScore;
        public int count = 1;
        public bool requirePass = true;
    }

    /// <summary>Immutable receipt emitted only by the authoritative physical-cup delivery transaction.</summary>
    public sealed class CafeNarrativeDeliveryReceipt
    {
        public string OwnerKey { get; }
        public string RequestId { get; }
        public string CupId { get; }
        public string ProductId { get; }
        public float Score { get; }
        public bool Passed { get; }
        public bool Completed { get; }
        internal CafeNarrativeDeliveryReceipt(string owner, string request, string cup, string product, float score, bool passed, bool completed)
        { OwnerKey = owner; RequestId = request; CupId = cup; ProductId = product; Score = score; Passed = passed; Completed = completed; }
    }

    public sealed partial class CafeWorld
    {
        private static bool IsNarrativeWildcard(string product) => product == "any" || product == "any.hot";
        private static string NarrativeBaseProduct(string product)
            => product == "blackcoffee.sweet" || product == "blackcoffee.unsweetened" ? "blackcoffee" : product;
        private NarrativeService narrativeService;
        public bool HasNarrativeService => narrativeService != null && !narrativeService.Disposed;

        /// <summary>
        /// Authorize real, owner-scoped orders even before opening. Does not open business, create
        /// sales, mutate a normal order, grant a recipe, or advance the day. Only actual delivery
        /// through DeliverProductionCup can satisfy a quota. Caller must dispose the returned lease.
        /// </summary>
        public bool TryBeginNarrativeService(string ownerKey, IReadOnlyList<CafeNarrativeOrderSpec> orders,
            Action<CafeNarrativeDeliveryReceipt> onDelivery, out IDisposable lease, out string error)
        {
            lease = null; error = null;
            if (string.IsNullOrWhiteSpace(ownerKey) || onDelivery == null || orders == null || orders.Count == 0)
            { error = "剧情点单缺少 owner、配方或回调。"; return false; }
            if (!ProductionEnabled || HasTutorialCustomer || HasNarrativeService)
            { error = "制作系统未就绪、首客教学尚未结束，或已有剧情点单。"; return false; }
            var quota = new List<CafeNarrativeOrderSpec>(); int total = 0;
            foreach (var row in orders)
            {
                if (row == null || !IsNarrativeWildcard(row.productId) && Product(NarrativeBaseProduct(row.productId)) == null || !Finite(row.minScore) || row.minScore < 0 || row.minScore > 100 || row.count <= 0 || row.count > 32 || total > 32 - row.count)
                { error = "剧情点单的餐品、评级或数量无效。"; return false; }
                total += row.count;
                quota.Add(new CafeNarrativeOrderSpec { productId = row.productId, minScore = row.minScore, count = row.count, requirePass = row.requirePass });
            }
            int slot = AvailableSeat();
            if (slot < 0) { error = "当前没有空座，剧情不能覆盖已有顾客或订单。"; return false; }
            var session = new NarrativeService(this, ownerKey, quota, slot, onDelivery);
            narrativeService = session;
            try { session.Create(); lease = session; Notify(); return true; }
            catch { session.Dispose(); throw; }
        }

        private bool TryServeNarrativeOrder(WorldEntity cup, CustomerRequest request, out ActionResult result)
        {
            result = default;
            var session = narrativeService;
            if (session == null || session.Disposed || !session.Owns(request?.id)) return false;
            result = session.Deliver(cup, request); return true;
        }

        private sealed class NarrativeService : IDisposable
        {
            private readonly CafeWorld world;
            private readonly CafeSnapshot snapshot;
            private readonly string owner, customerId, previousFocus;
            private readonly List<CafeNarrativeOrderSpec> orders;
            private Action<CafeNarrativeDeliveryReceipt> completed;
            private string requestId;
            private WorldEntity customer;
            private int index, fulfilled;
            private bool allPassed = true;
            public bool Disposed { get; private set; }
            public NarrativeService(CafeWorld world, string owner, List<CafeNarrativeOrderSpec> orders, int slot, Action<CafeNarrativeDeliveryReceipt> completed)
            {
                this.world = world; snapshot = world.State; this.owner = owner; this.orders = orders; this.completed = completed;
                customerId = "customer_" + slot; previousFocus = snapshot.focusedRequestId;
            }
            public bool Owns(string id) => !Disposed && id == requestId && ReferenceEquals(world.State, snapshot);
            public void Create()
            {
                customer = new WorldEntity { id = customerId, specId = "customer", area = AreaId.Cafe,
                    person = world.CreateCustomerIdentity("story." + owner + "." + snapshot.requestSequence, 0) };
                snapshot.entities.Add(customer); CreateOrder();
            }
            private void CreateOrder()
            {
                var row = orders[index]; var product = IsNarrativeWildcard(row.productId) ? Array.Find(world.Catalog.products, p => world.State.unlockedProducts.Contains(p.id) && world.CanOfferProduct(p)) : world.Product(NarrativeBaseProduct(row.productId));
                if (product == null) throw new InvalidOperationException("没有可制作的餐品。 ");
                requestId = "narrative_order_" + (++snapshot.requestSequence);
                snapshot.requests.Add(new CustomerRequest { id = requestId, customerId = customerId,
                    productId = product.id, cupSpecId = product.cupSpecId, patience = float.MaxValue });
                snapshot.focusedRequestId = requestId;
            }
            public ActionResult Deliver(WorldEntity cup, CustomerRequest request)
            {
                if (!Owns(request?.id) || cup == null) return ActionResult.No("剧情订单已失效。");
                var row = orders[index];
                // The source explicitly requests a sugar variant. The global classifier intentionally
                // rejects extras; project only its coffee base for kind, without altering the real cup.
                WorldEntity classified = cup;
                if (row.productId == "blackcoffee.sweet" || row.productId == "blackcoffee.unsweetened")
                    classified = new WorldEntity { id = cup.id, specId = cup.specId, latteArt = cup.latteArt,
                        contents = cup.contents.FindAll(p => p.specId != "sugar") };
                var classification = world.ClassifyCoffee(classified);
                var product = IsNarrativeWildcard(row.productId) && classification.known ? world.Product(classification.productId) : world.Product(NarrativeBaseProduct(row.productId));
                if (product == null) return ActionResult.No("请完成一杯真实饮品后交付；当前杯子保留。 ");
                string requiredCup = IsNarrativeWildcard(row.productId) ? product.cupSpecId : world.RequiredCupSpecId(request);
                if (!classification.known || classification.productId != product.id || !world.AcceptsServingCup(cup, requiredCup))
                    return ActionResult.No("请按当前剧情订单制作" + product.label + "，并使用正确杯型；杯子和订单均已保留。");
                bool hasSugar = cup.contents.Exists(p => p.specId == "sugar" && (p.units > 0 || p.grams > 0 || p.milliliters > 0));
                if (row.productId.EndsWith(".sweet", StringComparison.Ordinal) && !hasSugar
                    || row.productId.EndsWith(".unsweetened", StringComparison.Ordinal) && hasSugar)
                    return ActionResult.No("请按剧情订单区分加糖与无糖；杯子和订单均已保留。 ");
                if (row.productId == "any.hot" && world.TemperatureCelsius(cup) < 40f)
                    return ActionResult.No("这份剧情订单需要热饮（至少40°C）；杯子和订单均已保留。 ");
                var score = world.EvaluateProduction(cup, product, requiredCup);
                bool passed = score.total >= row.minScore;
                string usedRequest = requestId, cupId = cup.id;
                request.lastProductionScore = score.total;
                world.ConsumeDeliveredContainer(cup);
                bool advances = passed || !row.requirePass;
                bool done = false;
                if (advances)
                {
                    allPassed &= passed;
                    snapshot.requests.Remove(request);
                    fulfilled++;
                    if (fulfilled >= row.count) { fulfilled = 0; index++; }
                    done = index >= orders.Count;
                    if (!done) CreateOrder();
                }
                else request.remakes++;
                snapshot.production.lastDelivery = done ? "剧情饮品已交付 · " + score.total.ToString("0.0") + "分"
                    : advances ? "剧情饮品已交付，请继续完成剩余订单。" : "这杯未达到剧情品质要求，请重新制作 · " + score.total.ToString("0.0") + "分";
                var receipt = new CafeNarrativeDeliveryReceipt(owner, usedRequest, cupId, product.id, score.total,
                    done ? allPassed : passed, done);
                var callback = completed;
                if (done) Release(false);
                callback?.Invoke(receipt);
                world.Notify();
                return ActionResult.Yes(snapshot.production.lastDelivery, advances ? CafeFeedbackKind.DeliverySatisfied : CafeFeedbackKind.DeliveryRemake);
            }
            public void Dispose() => Release(true);
            private void Release(bool notify)
            {
                if (Disposed) return;
                Disposed = true; completed = null;
                if (ReferenceEquals(world.narrativeService, this)) world.narrativeService = null;
                if (!ReferenceEquals(world.State, snapshot)) return;
                bool ownsFocus = snapshot.focusedRequestId == requestId;
                snapshot.requests.RemoveAll(r => r.id == requestId && r.customerId == customerId);
                snapshot.entities.Remove(customer);
                if (ownsFocus)
                    snapshot.focusedRequestId = snapshot.requests.Exists(r => r.id == previousFocus) ? previousFocus
                        : snapshot.requests.Count > 0 ? snapshot.requests[0].id : null;
                if (notify) world.Notify();
            }
        }
    }
}

