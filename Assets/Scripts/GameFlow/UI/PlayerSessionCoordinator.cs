using System;
using System.Collections.Generic;
using Spotlight.FreeCafe;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    /// <summary>Session-owned wiring. It never stores a second copy of gameplay values.</summary>
    public sealed class PlayerSessionCoordinator : IDisposable
    {
        private NarrativeRuntime runtime;
        private PlayerItemCatalog itemCatalog;
        private CafeWorld world;
        private DayFlowController dayFlow;
        private CafeNarrativeValueProvider cafe;
        private DelegateNarrativeValueProvider actions, calendar;
        private Action<int, int> pointChanged;
        public bool IsBound => runtime != null && world != null;

        public void Bind(NarrativeRuntime target, CafeWorld owner, DayFlowController days)
        {
            if (target == null || target.Controller == null || owner == null || days == null)
                throw new InvalidOperationException("Player owners are not ready.");
            Dispose();
            runtime = target; world = owner; dayFlow = days;
            itemCatalog = new PlayerItemCatalog(runtime.Configuration);
            var controller = runtime.Controller;
            controller.BeginValueRestore();
            controller.UnbindValueProviders();
            cafe = new CafeNarrativeValueProvider(world);
            actions = new DelegateNarrativeValueProvider("actions", key => key == "current" ? runtime.Actions.ActionPoints.Current : key == "max" ? runtime.Actions.ActionPoints.Max : (int?)null);
            calendar = new DelegateNarrativeValueProvider("dayflow", key => key == "day" ? dayFlow.CurrentDay : (int?)null);
            controller.BindValueProvider(cafe);
            controller.BindValueProvider(actions);
            controller.BindValueProvider(calendar);
            controller.MoneyAccount = cafe;
            pointChanged = (_, __) => actions.NotifyChanged();
            runtime.Actions.ActionPoints.Changed += pointChanged;
            runtime.BarChanged += OnBarChanged;
            controller.State.ConditionsChanged += NotifyPlayer;
            runtime.MapInventory.Changed += NotifyPlayer;
            PlayerRuntime.Current.BindQueries(ReadBar, ReadCondition, ReadItem);
            target.AbilityReader = ReadAttribute;
            controller.AbilityNameResolver = ResolveAttributeName;
        }

        public void NotifyDayChanged() => calendar?.NotifyChanged();
        private void OnBarChanged(string key, int before, int after) => NotifyPlayer();
        private void NotifyPlayer() => PlayerRuntime.Current?.NotifyOwnersChanged();
        private string ResolveAttributeName(string attributeId)
        {
            PlayerRuntime player = PlayerRuntime.Current;
            var attribute = player?.Catalog.Attributes.Find(item => item.AttributeId == attributeId);
            return attribute == null ? attributeId : player.Catalog.Text(attribute.NameText);
        }
        private (bool ready, int value) ReadAttribute(string attributeId)
        {
            PlayerRuntime player = PlayerRuntime.Current;
            if (player == null) return (false, 0);
            PlayerReadStatus status = player.TryReadAttribute(attributeId, out int value);
            return (status == PlayerReadStatus.Success, value);
        }
        private PlayerReadStatus ReadBar(string key, out int value)
        {
            value = 0;
            if (runtime == null) return PlayerReadStatus.NotReady;
            if (runtime.Controller.State.GetDefinition(key) == null) return PlayerReadStatus.UnknownId;
            return runtime.Controller.State.TryGetBar(key, out value) ? PlayerReadStatus.Success : PlayerReadStatus.NotReady;
        }
        private PlayerReadStatus ReadCondition(string key, out bool value)
        {
            value = false;
            if (runtime == null) return PlayerReadStatus.NotReady;
            return runtime.Controller.State.TryGetCondition(key, out value) ? PlayerReadStatus.Success : PlayerReadStatus.UnknownId;
        }
        private PlayerReadStatus ReadItem(string key, out int value)
        {
            value = 0;
            if (runtime == null) return PlayerReadStatus.NotReady;
            if (!itemCatalog.IsKnownItem(key, runtime.MapInventory)) return PlayerReadStatus.UnknownId;
            value = runtime.MapInventory.GetCount(key);
            return PlayerReadStatus.Success;
        }

        public bool Submit(FirstDayLetterDraft draft)
        {
            var player = PlayerRuntime.Current;
            if (player == null || !IsBound) throw new InvalidOperationException("Player initialization is waiting for its owners.");
            if (player.CreationSubmitted) return true; // replay only; never reapply rewards
            var plan = draft == null ? player.PrepareDefaultCreation() : player.PrepareCreation(draft);
            var state = runtime.Controller.State;
            var next = new Dictionary<string, int>();
            foreach (var attribute in player.Catalog.Attributes)
            {
                var definition = state.GetDefinition(attribute.BarKey);
                if (definition == null || !state.IsWritableBar(attribute.BarKey) || !state.TryGetBar(attribute.BarKey, out int current))
                    throw new InvalidOperationException("Creation attribute unavailable: " + attribute.BarKey);
                int delta = plan.AttributeBonuses.TryGetValue(attribute.AttributeId, out int bonus) ? bonus : 0;
                int finalValue = checked(current + delta);
                if (finalValue < definition.min || finalValue > definition.max)
                    throw new InvalidOperationException("Creation bonus exceeds Bar range: " + attribute.BarKey);
                next.Add(attribute.BarKey, finalValue);
            }
            if (!string.IsNullOrEmpty(plan.SpecializationConditionKey)) state.ValidateConditionWrite(plan.SpecializationConditionKey, true);
            if (plan.StartingMoney < 0) throw new InvalidOperationException("Invalid starting money.");
            runtime.Controller.BeginValueRestore();
            var backup = state.Capture();
            int balance = world.State.money;
            int max = runtime.Actions.ActionPoints.Max, points = runtime.Actions.ActionPoints.Current;
            try
            {
                return player.CommitCreation(plan, result =>
                {
                    try
                    {
                        foreach (var pair in next) runtime.SetBar(pair.Key, pair.Value);
                        if (!string.IsNullOrEmpty(result.SpecializationConditionKey)) state.SetCondition(result.SpecializationConditionKey, true);
                        if (!world.TryInitializeMoney(result.StartingMoney, out string error)) throw new InvalidOperationException(error);
                        runtime.Actions.ActionPoints.ResetForDay(player.Catalog.ActionPointMax);
                    }
                    catch
                    {
                        state.Restore(backup);
                        world.TryInitializeMoney(balance, out _);
                        runtime.Actions.ActionPoints.ResetForDay(max);
                        runtime.Actions.ActionPoints.Set(points);
                        throw;
                    }
                });
            }
            finally { if (player.CreationSubmitted) NotifyPlayer(); }
        }

        public void Dispose()
        {
            if (runtime != null)
            {
                runtime.Actions.ActionPoints.Changed -= pointChanged;
                runtime.BarChanged -= OnBarChanged;
                runtime.Controller.State.ConditionsChanged -= NotifyPlayer;
                runtime.MapInventory.Changed -= NotifyPlayer;
                runtime.Controller.UnbindValueProviders();
                runtime.AbilityReader = null;
                runtime.Controller.AbilityReader = null;
                runtime.Controller.AbilityNameResolver = null;
                runtime.Controller.MoneyAccount = null;
            }
            cafe?.Dispose();
            PlayerRuntime.Current?.UnbindQueries();
            runtime = null; world = null; dayFlow = null; cafe = null;
        }
    }
}
