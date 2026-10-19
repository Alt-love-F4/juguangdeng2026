using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Spotlight.FreeCafe;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// Host adapter for CafeWorld's owner-scoped narrative service. Completion comes exclusively
    /// from the authoritative delivery receipt, never inferred from counters, hints or tray state.
    /// </summary>
    public sealed class NarrativeCafeGameplayBridge : IDisposable
    {
        // Tunable implementation presets, NOT source-author definitions of localized quality grades.
        public const int ProvisionalNormalScore = 60;
        public const int ProvisionalExcellentScore = 90;

        private readonly CafeWorld world;
        private readonly CafeSnapshot snapshot;
        private readonly int calendarDay;
        private readonly bool requiresPassing;
        private readonly HashSet<string> products;
        private readonly bool acceptsAnyProduct;
        private readonly HashSet<string> deliveredCups = new HashSet<string>(StringComparer.Ordinal);
        private Action<NarrativeGameplayResult> completed;
        private IDisposable serviceLease;
        private bool disposed, observedBusinessOpen, allTastesPassed = true;

        public string OwnerKey { get; }
        /// <summary>Nonmodal instruction; tokens remain intact in the domain service spec.</summary>
        public string GameplayHint { get; }
        public bool IsFinished => disposed;
        public string Diagnostic { get; private set; }

        private NarrativeCafeGameplayBridge(CafeWorld world, string ownerKey,
            IReadOnlyList<CafeNarrativeOrderSpec> orders, bool requiresPassing, Action<NarrativeGameplayResult> completed)
        {
            this.world = world;
            snapshot = world.State;
            calendarDay = snapshot.day;
            OwnerKey = ownerKey;
            this.requiresPassing = requiresPassing;
            this.completed = completed;
            products = new HashSet<string>(StringComparer.Ordinal);
            var hint = new StringBuilder();
            foreach (var order in orders)
            {
                if (IsAnyProductToken(order.productId)) acceptsAnyProduct = true;
                else products.Add(CatalogProductId(order.productId));
                if (hint.Length > 0) hint.Append("；");
                hint.Append("需要").Append(order.count == 1 ? "一杯" : order.count.ToString(CultureInfo.InvariantCulture) + "杯")
                    .Append(ProductInstruction(world, order.productId));
                if (order.minScore > 0) hint.Append("（目标 ").Append(order.minScore.ToString("0.##", CultureInfo.InvariantCulture)).Append(" 分）");
            }
            hint.Append(requiresPassing ? "。请实际制作并放上托盘按铃交付，未达标需重新制作。" : "。请实际制作并放上托盘按铃交付，按真实品鉴结果继续剧情。");
            GameplayHint = hint.ToString();
            observedBusinessOpen = snapshot.open;
        }

        public static bool TryStart(CafeWorld world, string gameplayKey,
            Action<NarrativeGameplayResult> completed, out NarrativeCafeGameplayBridge bridge, out string error)
            => TryStart(world, "narrative-cafe:" + Guid.NewGuid().ToString("N"), gameplayKey, completed, out bridge, out error);

        public static bool TryStart(CafeWorld world, string ownerKey, string gameplayKey,
            Action<NarrativeGameplayResult> completed, out NarrativeCafeGameplayBridge bridge, out string error)
        {
            bridge = null;
            if (completed == null) { error = "Gameplay completion callback is missing."; return false; }
            if (string.IsNullOrWhiteSpace(ownerKey)) { error = "Narrative cafe service owner is missing."; return false; }
            if (!TryParse(world, gameplayKey, out var orders, out bool requiresPassing, out error)) return false;
            if (!world.ProductionEnabled) { error = "The real cafe production world is not ready."; return false; }
            if (world.HasTutorialCustomer) { error = "Narrative service cannot consume the first-day tutorial order."; return false; }
            var pending = new NarrativeCafeGameplayBridge(world, ownerKey, orders, requiresPassing, completed);
            if (!world.TryBeginNarrativeService(ownerKey, orders, pending.OnDelivered, out var lease, out error))
            {
                pending.Dispose();
                return false;
            }
            if (lease == null)
            {
                pending.Dispose();
                error = "CafeWorld accepted service without an owner lease.";
                return false;
            }
            // An owner may have been cancelled reentrantly while the domain published its orders.
            if (pending.disposed) lease.Dispose();
            else
            {
                pending.serviceLease = lease;
                world.Changed += pending.OnWorldChanged;
            }
            bridge = pending;
            return true;
        }

        /// <summary>Catalog validation only; never creates orders or acquires a world owner.</summary>
        public static bool ValidateKey(CafeWorld world, string gameplayKey, out string error)
            => TryParse(world, gameplayKey, out _, out _, out error);

        private static bool TryParse(CafeWorld world, string key, out List<CafeNarrativeOrderSpec> orders,
            out bool requiresPassing, out string error)
        {
            orders = null;
            requiresPassing = false;
            error = null;
            if (world == null) { error = "CafeWorld is unavailable."; return false; }
            bool serve = key != null && key.StartsWith("serve:", StringComparison.Ordinal);
            bool taste = key != null && key.StartsWith("taste:", StringComparison.Ordinal);
            if (!serve && !taste)
            { error = "Unsupported gameplay key; expected serve: or taste:<productId>:<minimumScore>:<count>[+product:score:count]."; return false; }
            var result = new List<CafeNarrativeOrderSpec>();
            foreach (string entry in key.Substring(6).Split('+'))
            {
                string[] fields = entry.Split(':');
                if (fields.Length != 3 || string.IsNullOrWhiteSpace(fields[0]) || fields[0] != fields[0].Trim()
                    || !float.TryParse(fields[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out float score)
                    || float.IsNaN(score) || float.IsInfinity(score) || score < 0 || score > 100
                    || !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count <= 0)
                { error = "Malformed cafe service requirement: " + entry; return false; }
                // Only the explicitly agreed tokens are flexible. Keep the original token in
                // the spec so the domain, not the bridge, verifies temperature/sugar constraints.
                if (!IsAnyProductToken(fields[0]) && world.Product(CatalogProductId(fields[0])) == null)
                { error = "Unknown catalog product: " + fields[0]; return false; }
                result.Add(new CafeNarrativeOrderSpec { productId = fields[0], minScore = score, count = count, requirePass = serve });
            }
            orders = result;
            requiresPassing = serve;
            return true;
        }

        private static bool IsAnyProductToken(string token) => token == "any" || token == "any.hot";

        private static string CatalogProductId(string token)
            => token == "blackcoffee.sweet" || token == "blackcoffee.unsweetened" ? "blackcoffee" : token;

        private static string ProductInstruction(CafeWorld world, string token)
        {
            switch (token)
            {
                case "any": return "任意饮品";
                case "any.hot": return "热饮";
                case "blackcoffee.sweet": return "加糖黑咖啡";
                case "blackcoffee.unsweetened": return "无糖黑咖啡";
                default: return world.Product(token)?.label ?? token;
            }
        }

        private void OnDelivered(CafeNarrativeDeliveryReceipt receipt)
        {
            if (disposed || receipt == null || receipt.OwnerKey != OwnerKey
                || string.IsNullOrEmpty(receipt.RequestId) || string.IsNullOrEmpty(receipt.CupId)
                || world.Product(receipt.ProductId) == null
                || !acceptsAnyProduct && !products.Contains(receipt.ProductId)
                || float.IsNaN(receipt.Score) || float.IsInfinity(receipt.Score) || receipt.Score < 0 || receipt.Score > 100
                || !deliveredCups.Add(receipt.CupId)) return;
            // Domain receipts certify actual consumption and scoring. A service retry that failed
            // its threshold cannot poison later passing quotas. Taste attempts intentionally can.
            if (!requiresPassing) allTastesPassed &= receipt.Passed;
            if (!receipt.Completed) return;
            if (requiresPassing && !receipt.Passed)
            { Cancel("Cafe service published an inconsistent completed passing quota."); return; }
            Finish(requiresPassing || allTastesPassed, false);
        }

        private void OnWorldChanged()
        {
            if (disposed) return;
            // Lifecycle cancellation only. Never infer successful delivery from Changed/state diffs.
            if (!ReferenceEquals(world.State, snapshot) || snapshot.day != calendarDay)
            { Cancel("Cafe snapshot/calendar changed while service was pending."); return; }
            // Domain completion releases silently, calls the immutable receipt callback, THEN
            // notifies Changed; a valid Completed receipt therefore disposes us before this path.
            // An absent lease on a still-pending owner is cancellation, including closed service.
            if (!world.HasNarrativeService)
            { Cancel("Authoritative narrative service was cancelled before a completion receipt."); return; }
            if (observedBusinessOpen && !snapshot.open && snapshot.businessPhase == BusinessPhase.Night)
            { Cancel("Ordinary business closed while narrative service was pending."); return; }
            observedBusinessOpen = snapshot.open;
        }

        public void Cancel(string reason)
        {
            if (disposed) return;
            Diagnostic = reason;
            Finish(false, true);
        }

        private void Finish(bool passed, bool cancelled)
        {
            var callback = completed;
            Dispose(); // Detach/latch before releasing a lease or invoking a reentrant runtime.
            callback?.Invoke(new NarrativeGameplayResult { passed = passed, cancelled = cancelled });
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            completed = null;
            world.Changed -= OnWorldChanged;
            var lease = serviceLease;
            serviceLease = null;
            lease?.Dispose(); // Domain removes ONLY this owner's pending orders and restores focus.
        }
    }
}
