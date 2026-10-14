using System;
using Spotlight.FreeCafe;

namespace Spotlight.Narrative
{
    public sealed class CafeNarrativeValueProvider : INarrativeValueProvider, INarrativeMoneyAccount, IDisposable
    {
        private CafeWorld world;
        public string ProviderId => "cafe";
        public bool IsReady => world != null;
        public event Action Changed;
        public CafeNarrativeValueProvider(CafeWorld world)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            world.Changed += OnChanged;
        }
        private void OnChanged() => Changed?.Invoke();
        public bool TryRead(string sourceKey, out int value)
        {
            value = 0;
            if (world == null || sourceKey != "money") return false;
            value = world.State.money;
            return true;
        }
        public bool TryRead(out int balance) => TryRead("money", out balance);
        public bool TryCommit(int expectedBalance, int finalBalance, out string error)
        {
            if (world == null) { error = "Cafe provider disposed."; return false; }
            return world.TryCommitMoney(expectedBalance, finalBalance, out error);
        }
        public void Dispose()
        {
            if (world != null) world.Changed -= OnChanged;
            world = null;
        }
    }
}
