#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.GameFlow.Tests
{
    /// <summary>Generated formal date route + actual CafeWorld delivery. UI acknowledgements alone are controlled.
    /// Fixture explicitly seeds prior encounters/abilities; this is NOT a full natural new-game playthrough.</summary>
    public sealed class NarrativeGeneratedCafePlaybackTests
    {
        private Scene previous, shared;
        private NarrativeController controller;
        private NarrativePresenter presenter;
        private CafeWorld world;
        private readonly List<string> seen = new List<string>();
        private NarrativeConversationPresentationData current;
        private Action next;
        private bool entryDone;
        private string failure;
        private NarrativeCafeGameplayBridge service;
        [UnitySetUp] public IEnumerator Setup()
        {
            entryDone = false; failure = null; current = null; next = null; service = null; seen.Clear();
            previous = SceneManager.GetActiveScene();
            shared = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PresentationDebugScene.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null; SceneManager.SetActiveScene(shared);
            var c = LubanNarrativeLoader.Load();
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction(); world.EnterProduction(); world.State.day = 6;
            presenter = new NarrativePresenter(c, key => c.texts.Find(t => t.key == key)?.content ?? key);
            controller = new NarrativeController(c, presenter);
            controller.SetEndingChecksEnabled(false);
            controller.BeginValueRestore();
            controller.BindValueProvider(new DelegateNarrativeValueProvider("dayflow", key => key == "day" ? 6 : (int?)null));
            controller.BindValueProvider(new DelegateNarrativeValueProvider("cafe", key => key == "money" ? world.State.money : (int?)null));
            controller.BindValueProvider(new DelegateNarrativeValueProvider("actions", key => key == "current" || key == "max" ? 5 : (int?)null));
            foreach (string attribute in new[] { "language", "courage", "wisdom", "perception" }) controller.SetBar("player." + attribute, 10);
            foreach (string fact in new[] { "story.rose.1009.s1.completed", "story.neutral.1010.s1.completed" }) controller.State.SetCondition(fact, true);
            controller.CompleteValueRestore(false);
            controller.AbilityReader = id => controller.State.TryGetBar("player." + id, out int value) ? (true, value) : (false, 0);
            presenter.PageCondition = controller.ReadPresentationCondition;
            presenter.ChoiceUnlocked = (id, context) => controller.EvaluateConditions("choice", id, context);
            presenter.InlineAbilityCheck = controller.EvaluateInlineAbilityCheckForPresentation;
            presenter.ChoiceSelected += controller.SelectChoice;
            presenter.ConversationPlayback = (request, data, done) => { current = data; next = done; seen.Add(data.lines.FirstOrDefault()?.pageId ?? "choices"); return true; };
            presenter.NoticePlayback = (request, data, done) => { done(); return true; };
            presenter.LetterPlayback = (request, data, done) => { seen.Add(data.pageId); done(); return true; };
            presenter.GameplayPlayback = (request, data, done) =>
            {
                Assert.That(NarrativeCafeGameplayBridge.TryStart(world, request.RequestId + data.pageId, data.gameplayKey, done, out service, out string error), Is.True, error);
                return service;
            };
            controller.EventFailed += (id, error) => failure = id + ":" + error;
            controller.SetOfficialDay(6);
            Assert.That(controller.ProcessOfficialDay(6), Is.True);
            Assert.That(controller.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap, success => entryDone = success), Is.True);
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            presenter?.Dispose(); service?.Dispose(); controller?.UnbindValueProviders();
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (shared.IsValid() && shared.isLoaded) yield return SceneManager.UnloadSceneAsync(shared);
        }
        private void DeliverActualCoffee()
        {
            var request = world.Request(world.State.focusedRequestId); Assert.That(request, Is.Not.Null);
            var product = world.Product(request.productId);
            Assert.That(world.SelectServingCup(world.RequiredCupSpecId(request) == "cup.small" ? "s.cups.small" : "s.cups.large").success, Is.True);
            var cup = world.ServingCup;
            foreach (var need in product.evaluation.amounts)
            {
                var portion = new Portion { specId = need.ingredientId, milliliters = need.idealMl * world.OrderPortionScale(request), grams = need.idealGrams,
                    productionScore = 100, coffeeKind = need.coffeeKind, temperature = LiquidTemperature.Warm };
                if (need.ingredientId == "coffee") { world.AddConstituent(portion, "water", 0, portion.milliliters); world.AddConstituent(portion, "grounds", portion.milliliters * product.evaluation.idealCoffeeStrength, 0); }
                cup.contents.Add(portion);
            }
            world.FinishPour(cup.id);
            var result = world.DeliverProductionCup(); Assert.That(result.success, Is.True, result.message);
        }
        private IEnumerator Run(bool longRoute)
        {
            bool delivered = false;
            for (int frame = 0; frame < 600 && !entryDone && failure == null; frame++)
            {
                if (presenter.IsAwaitingGameplay && world.HasNarrativeService && !delivered)
                {
                    Assert.That(controller.State.TryGetCondition("story.rose.1011.s1.completed", out bool before) && before, Is.False);
                    DeliverActualCoffee(); delivered = true;
                }
                if (next != null)
                {
                    var data = current; var done = next; next = null;
                    if (data.options.Count > 0)
                    {
                        string suffix = data.options.Any(o => o.id.EndsWith("p062")) ? longRoute ? "p062" : "p060"
                            : data.options.Any(o => o.id.EndsWith("p080")) ? "p080" : null;
                        var option = suffix == null ? data.options.First(o => o.enabled) : data.options.Single(o => o.id.EndsWith(suffix));
                        Assert.That(data.selectChoice(option.id), Is.True, option.id);
                    }
                    done();
                }
                yield return null;
            }
            Assert.That(failure, Is.Null); Assert.That(delivered, Is.True); Assert.That(entryDone, Is.True);
            Assert.That(controller.State.TryGetCondition("story.rose.1011.s1.completed", out bool complete) && complete, Is.True);
            Assert.That(controller.State.TryGetCondition("story.rose.1011.s1.sylvain_appreciation", out bool appreciated) && appreciated, Is.EqualTo(longRoute));
            Assert.That(seen.Any(p => p.Contains("long.") || p.Contains("card")), Is.EqualTo(longRoute));
            Assert.That(world.State.todayRevenue, Is.Zero); Assert.That(world.State.open, Is.False);
        }
        [UnityTest] public IEnumerator GeneratedMorningRouteActualCoffeeShortEndDoesNotReadLongTail() { yield return Run(false); }
        [UnityTest] public IEnumerator GeneratedMorningRouteActualCoffeeLongTailCommitsAppreciation() { yield return Run(true); }
    }
}
#endif
