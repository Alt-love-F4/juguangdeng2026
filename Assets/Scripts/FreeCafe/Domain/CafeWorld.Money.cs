namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        public bool TryCommitMoney(int expectedBalance, int finalBalance, out string error)
        {
            error = null;
            if (finalBalance < 0 || State.money != expectedBalance)
            {
                error = "Invalid balance or money changed since preflight.";
                return false;
            }
            if (State.money == finalBalance) return true;
            State.money = finalBalance;
            Notify();
            return true;
        }

        public bool TryAddMoney(int amount, out string error)
        {
            if (amount <= 0 || (long)State.money + amount > int.MaxValue) { error = "Invalid amount or money overflow."; return false; }
            return TryCommitMoney(State.money, State.money + amount, out error);
        }

        public bool TryRemoveMoney(int amount, out string error)
        {
            if (amount <= 0 || State.money < amount) { error = "Invalid amount or insufficient money."; return false; }
            return TryCommitMoney(State.money, State.money - amount, out error);
        }

        /// <summary>Session initialization calls this while input and narrative checks are blocked.</summary>
        public bool TryInitializeMoney(int finalBalance, out string error) => TryCommitMoney(State.money, finalBalance, out error);
    }
}
