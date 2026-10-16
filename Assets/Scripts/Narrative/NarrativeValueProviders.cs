using System;

namespace Spotlight.Narrative
{
    /// <summary>Changed is raised after an owner operation commits, on the session thread.</summary>
    public interface INarrativeValueProvider
    {
        string ProviderId { get; }
        bool IsReady { get; }
        bool TryRead(string sourceKey, out int value);
        event Action Changed;
    }

    public sealed class DelegateNarrativeValueProvider : INarrativeValueProvider
    {
        private readonly Func<string, int?> reader;
        private readonly Func<bool> ready;
        public string ProviderId { get; }
        public bool IsReady => ready();
        public event Action Changed;
        public DelegateNarrativeValueProvider(string providerId, Func<string, int?> reader, Func<bool> ready = null)
        {
            ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            this.ready = ready ?? (() => true);
        }
        public bool TryRead(string sourceKey, out int value)
        {
            int? result = IsReady ? reader(sourceKey) : null;
            value = result.GetValueOrDefault();
            return result.HasValue;
        }
        public void NotifyChanged() => Changed?.Invoke();
    }

    /// <summary>Optional notification batching for a money account; state changes still apply immediately.</summary>
    public interface INarrativeMoneyNotificationScope
    {
        /// <summary>Coalesces change notifications until the last nested scope is disposed; does not roll back changes.</summary>
        IDisposable DeferNotifications();
    }

    /// <summary>Atomic strict balance commit, verified against the preflight balance.</summary>
    public interface INarrativeMoneyAccount
    {
        bool TryRead(out int balance);
        bool TryCommit(int expectedBalance, int finalBalance, out string error);
    }
}
