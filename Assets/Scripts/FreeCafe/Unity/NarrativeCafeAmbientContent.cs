using System;
using System.Linq;
using Spotlight.Narrative;

namespace Spotlight.FreeCafe
{
    /// <summary>Source-authored ambient snapshots on the existing hint UI, never a modal narrative event.</summary>
    public static class NarrativeCafeAmbientContent
    {
        public static bool TryOrder(CafeWorld world, WorldEntity customer, out string text)
        {
            text = null;
            var runtime = NarrativeRuntime.Instance;
            var configuration = runtime != null && runtime.IsReady ? runtime.Configuration : null;
            if (world == null || customer == null || configuration == null) return false;
            var request = world.State.requests.FirstOrDefault(r => r.customerId == customer.id);
            if (request == null) return false;
            var templates = configuration.openingPresentations.Where(p => p.presentationId.StartsWith("cafe.ambient.order.", StringComparison.Ordinal))
                .OrderBy(p => p.presentationId, StringComparer.Ordinal).ToArray();
            if (templates.Length == 0) return false;
            uint hash = 2166136261;
            foreach (char c in customer.id ?? request.id) hash = unchecked((hash ^ c) * 16777619);
            string key = templates[hash % (uint)templates.Length].presentationId;
            string body = string.Join("\n", configuration.openingLines.Where(l => l.presentationId == key).OrderBy(l => l.seq)
                .Select(l => runtime.ResolveText(l.textKey)));
            string drink = world.Catalog.products.FirstOrDefault(p => p.id == request.productId)?.label;
            if (string.IsNullOrWhiteSpace(drink) || string.IsNullOrWhiteSpace(body)) return false;
            text = NarrativeTextSubstitution.DrinkName(body, drink)
                .Replace("{饮品名}", drink).Replace("{桌号}", customer.id)
                .Replace("{customerName}", customer.person?.displayName ?? "顾客", StringComparison.OrdinalIgnoreCase);
            return true;
        }
    }
}
