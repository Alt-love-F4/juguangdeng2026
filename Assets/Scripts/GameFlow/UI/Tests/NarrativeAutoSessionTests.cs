using System;
using System.Linq;
using NUnit.Framework;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow.Tests
{
    public sealed class NarrativeAutoSessionTests
    {
        private static CafeCatalog Catalog() => JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text);

        [Test]
        public void PresetsCoverEveryEnabledEndingAndDoNotInventTerminalFacts()
        {
            var config = LubanNarrativeLoader.Load();
            var presets = NarrativeAutoPresets.Create(config);
            Assert.That(presets.Count, Is.EqualTo(63));
            Assert.That(presets.Select(p => p.targetEndingId).Distinct(), Is.EquivalentTo(config.endings.Where(e => e.enabled).Select(e => e.endingId)));
            foreach (var p in presets)
            {
                if (!p.historicalScenario) Assert.That(p.history, Is.Empty, p.id);
                foreach (string id in p.preferredChoices) Assert.That(config.choices.Any(c => c.choiceId == id), Is.True, p.id + "/" + id);
                foreach (string key in p.history)
                {
                    Assert.That(config.conditionKeys.Any(c => c.key == key), Is.True, key);
                    Assert.That(key.StartsWith("construction.") || key.StartsWith("blocked.") || key.StartsWith("integration."), Is.False, key);
                    Assert.That(config.conditions.Any(c => c.ownerType == "ending" && c.ownerId == p.targetEndingId && c.target == key && !key.StartsWith("route.")), Is.False, "Cannot seed the target's terminal evidence: " + key);
                }
            }
        }

        [Test]
        public void OpeningCampaignReallySelectsRefusalAndStopsWithoutTouchingFormalOwners()
        {
            var runtime = NarrativeRuntime.Instance;
            var player = PlayerRuntime.Current;
            var cafe = PlayerCafeOwner.Current;
            using (var session = new NarrativeAutoSession(LubanNarrativeLoader.Load(), Catalog(), NarrativeAutoPresets.Make("arrival.ending.if0", 0, false)))
            {
                for (int guard = 0; !session.Finished && guard < 100; guard++) session.Tick(32);
                Assert.That(session.Finished, Is.True);
                Assert.That(session.Result.actualEndingId, Is.EqualTo("arrival.ending.if0"), session.Result.reason);
                Assert.That(session.Result.events, Does.Contain("arrival.leave"));
                int steps = session.Result.steps;
                session.Tick(256);
                Assert.That(session.Result.steps, Is.EqualTo(steps));
                Assert.That(NarrativeRuntime.Instance, Is.SameAs(runtime));
                Assert.That(PlayerRuntime.Current, Is.SameAs(player));
                Assert.That(PlayerCafeOwner.Current, Is.SameAs(cafe));
            }
        }

        [TestCase("rose.route.1115.s1.ending.ideal_world")]
        [TestCase("church.ending.ry1")]
        [TestCase("church.ending.ry2")]
        public void HistoricalScenariosCannotBypassAuthorBlockedRoots(string id)
        {
            using (var s = new NarrativeAutoSession(LubanNarrativeLoader.Load(), Catalog(), NarrativeAutoPresets.Make(id, 0, true)))
            {
                s.Tick(32);
                Assert.That(s.Result.status, Is.EqualTo("content-blocked"));
                Assert.That(s.Result.actualEndingId, Is.Null.Or.Empty);
                Assert.That(s.Result.blockers, Is.Not.Empty);
                Assert.That(s.Controller.State.Conditions.Where(p => p.Key.StartsWith("construction.") || p.Key.StartsWith("blocked.") || p.Key.StartsWith("integration.")).All(p => !p.Value), Is.True);
            }
        }

        [TestCase("neutral.ending.a")]
        [TestCase("neutral.ending.b")]
        [TestCase("neutral.ending.c")]
        [TestCase("neutral.ending.d")]
        [TestCase("rose.ending.trust_limit.ending")]
        [TestCase("rose.ending.job_requirements.ending")]
        [TestCase("rose.route.ending.unforgiven.ending")]
        [TestCase("rose.route.ending.bloom.ending")]
        [TestCase("church.ending.if2")]
        [TestCase("church.ending.if4")]
        [TestCase("revolution.end01")]
        [TestCase("revolution.end02")]
        public void HistoricalScenarioActuallySettlesItsRootAndProducesExpectedEnding(string id)
        {
            using (var s = new NarrativeAutoSession(LubanNarrativeLoader.Load(), Catalog(), NarrativeAutoPresets.Make(id, 0, true)))
            {
                for (int guard = 0; !s.Finished && guard < 1000; guard++) s.Tick(32);
                Assert.That(s.Finished, Is.True);
                Assert.That(s.Result.status, Is.EqualTo("ending"), s.Result.reason);
                Assert.That(s.Result.actualEndingId, Is.EqualTo(id), s.Result.reason);
                Assert.That(s.Result.evidence, Is.EqualTo("assisted-historical-scenario"));
                Assert.That(s.Result.events, Does.Contain(s.Preset.startEvent));
            }
        }
    }
}
