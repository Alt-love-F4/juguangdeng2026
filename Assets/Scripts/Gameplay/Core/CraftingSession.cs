using System;
using System.Collections.Generic;
using Spotlight.Interaction;
using UnityEngine;

namespace Spotlight.Gameplay
{
    public enum CraftingStepResult { None, Success, Fail }
    [Serializable] public sealed class CraftingSnapshot { public int stepIndex, clicks, cocoaMistakes; public float quality = 1f; public bool ingredientAdded; }
    public sealed class CraftingSession
    {
        private readonly PlayerProfile profile;
        private readonly List<RecipeStep> steps;
        private CraftingSnapshot state = new();
        public CustomerOrder Order { get; }
        public RecipeStep CurrentStep => state.stepIndex < steps.Count ? steps[state.stepIndex] : null;
        public int CurrentStepIndex => state.stepIndex;
        public int StepCount => steps.Count;
        public int Clicks => state.clicks;
        public float QualityMultiplier => state.quality;
        public int CocoaMistakes => state.cocoaMistakes;
        public int MaxCocoaMistakes => GameplayConfigService.GetParamInt("cocoa_game_max_mistakes", 2);
        public event Action<RecipeStep> OnStepChanged;
        public event Action<int> OnGrindProgress;
        public event Action<CraftingStepResult> OnSessionFinished;
        public event Action<int> OnCocoaMistake;
        public CraftingSession(CustomerOrder order, PlayerProfile profile) { Order = order; this.profile = profile; steps = RecipeStepParser.ForRecipe(order.Recipe); }
        public CraftingSnapshot Capture() => state;
        public void Restore(CraftingSnapshot value)
        {
            if (value == null || value.stepIndex < 0 || value.stepIndex >= steps.Count) throw new ArgumentException("无效制作进度");
            state = value;
        }
        public void Start() => OnStepChanged?.Invoke(CurrentStep);
        public InteractionResult CanSelectCup(string size) => CurrentStep?.Kind != RecipeStepKind.SelectCup ? InteractionResult.Reject("当前不需要选杯") :
            size != Order.Recipe.cup_size ? InteractionResult.Reject("订单需要" + (Order.Recipe.cup_size == "small" ? "小杯" : "大杯")) : InteractionResult.Accept();
        public void SelectCup() { if (CurrentStep?.Kind == RecipeStepKind.SelectCup) Next(); }
        public InteractionResult CanAddIngredient(string id, bool prepaid = false)
        {
            var step = CurrentStep;
            if (step == null || step.Kind != RecipeStepKind.AddIngredient && step.Kind != RecipeStepKind.AddMilk && step.Kind != RecipeStepKind.AddHoney) return InteractionResult.Reject("当前步骤不需要加原料");
            if (step.IngredientId != id) return InteractionResult.Reject("需要的是：" + GameplayConfigService.GetIngredient(step.IngredientId)?.comment);
            if (state.ingredientAdded) return InteractionResult.Reject("原料已加入，等待加工");
            var definition = GameplayConfigService.GetIngredient(id);
            if (definition == null || profile.Reputation < definition.unlock_rep) return InteractionResult.Reject("原料尚未解锁");
            if (!prepaid && profile.GetStock(id) < step.Amount) return InteractionResult.Reject("库存不足，请先补货");
            return InteractionResult.Accept();
        }
        public InteractionResult AddIngredient(string id, bool prepaid = false)
        {
            var result = CanAddIngredient(id, prepaid);
            if (!result.Success) return result;
            if (!prepaid) profile.Consume(id, CurrentStep.Amount);
            if (CurrentStep.Kind == RecipeStepKind.AddMilk) state.ingredientAdded = true; else Next();
            return result;
        }
        public void GrindClick()
        {
            if (CurrentStep?.Kind != RecipeStepKind.GrindClick) return;
            state.clicks++; OnGrindProgress?.Invoke(state.clicks);
            if (state.clicks >= CurrentStep.ClickCount) { state.clicks = 0; Next(); }
        }
        public void CompleteTimerStep()
        {
            if (CurrentStep?.Kind is RecipeStepKind.Process or RecipeStepKind.MokaPot or RecipeStepKind.MatchaGrind or RecipeStepKind.AddMilk or RecipeStepKind.CocoaMinigame) Next();
        }
        public void CompleteBeanSelection(bool success, int score)
        {
            if (CurrentStep?.Kind != RecipeStepKind.BeanSelection) return;
            state.quality *= success ? 1.05f : .8f; Next();
        }
        public void CocoaMistake()
        {
            state.cocoaMistakes++; OnCocoaMistake?.Invoke(state.cocoaMistakes);
            if (state.cocoaMistakes >= MaxCocoaMistakes) { profile.Spend(GameplayConfigService.GetParamInt("cocoa_game_fail_penalty", 10)); OnSessionFinished?.Invoke(CraftingStepResult.Fail); }
        }
        public void CompleteCocoaMinigame() { if (CurrentStep?.Kind == RecipeStepKind.CocoaMinigame) Next(); }
        public void SetLatteArtQuality(float score)
        {
            if (CurrentStep?.Kind != RecipeStepKind.LatteArt) return;
            state.quality *= .7f + .3f * Mathf.Clamp01(score / GameplayConfigService.GetParamFloat("latte_art_max_score", 100f)); Next();
        }
        public void Serve() { if (CurrentStep?.Kind == RecipeStepKind.Serve) Next(); }
        private void Next()
        {
            state.stepIndex++; state.ingredientAdded = false;
            if (state.stepIndex >= steps.Count) OnSessionFinished?.Invoke(CraftingStepResult.Success); else OnStepChanged?.Invoke(CurrentStep);
        }
    }
}
