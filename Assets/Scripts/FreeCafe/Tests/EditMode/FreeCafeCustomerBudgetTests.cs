using System;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeCustomerBudgetTests
    {
        private CafeCatalog catalog;
        private CafeWorld world;

        [SetUp]
        public void Setup()
        {
            catalog = JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text);
            world = new CafeWorld(catalog);
        }

        private void Open()
        {
            Assert.That(world.StartDay().success, Is.True);
        }

        private CafeSnapshot Copy() => JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));

        private CafeVisitIntent Visit(string id, bool authorized = false, float delay = 0, string seat = "customer_0")
        {
            return new CafeVisitIntent
            {
                visitInstanceId = id, characterId = "character_" + id,
                arrivalAuthorized = authorized, arrivalDelaySeconds = delay, preferredSeatId = seat,
                identity = new CafePersonIdentity
                {
                    personId = "person_" + id, displayName = id, role = CafePersonRole.Customer
                }
            };
        }

        // Use the real successful-order path without tying economic tests to drink production or UI.
        private void CompleteCustomers(int count)
        {
            for (int iteration = 0; iteration < 20 && world.State.todayServed < count; iteration++)
            {
                if (world.State.requests.Count == 0)
                {
                    world.Tick(catalog.customerInterval);
                }
                else
                {
                    foreach (var request in world.State.requests) request.automaticRemaining = 0;
                    world.Tick(.01f);
                }
            }
            Assert.That(world.State.todayServed, Is.EqualTo(count));
            Assert.That(world.State.requests, Is.Empty);
        }

        [Test]
        public void DefaultsAndFormalCatalogSpecifyThreeToFiveWithFiveReputationStep()
        {
            var defaults = new EconomySpec();
            Assert.That(defaults.baseCustomers, Is.EqualTo(3));
            Assert.That(defaults.maxCustomers, Is.EqualTo(5));
            Assert.That(defaults.customersReputationStep, Is.EqualTo(5));
            Assert.That(catalog.economy.baseCustomers, Is.EqualTo(3));
            Assert.That(catalog.economy.maxCustomers, Is.EqualTo(5));
            Assert.That(catalog.economy.customersReputationStep, Is.EqualTo(5));
        }

        [TestCase(-1, 3)]
        [TestCase(0, 3)]
        [TestCase(4, 3)]
        [TestCase(5, 4)]
        [TestCase(9, 4)]
        [TestCase(10, 5)]
        [TestCase(1000000, 5)]
        [TestCase(int.MaxValue, 5)]
        public void OpeningReputationDeterminesOrdinaryBudget(int reputation, int expected)
        {
            world.State.reputation = reputation;
            Open();
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(expected));
        }

        [TestCase(0u, 0)]
        [TestCase(1u, 30)]
        [TestCase(3u, 999)]
        [TestCase(uint.MaxValue, int.MaxValue)]
        public void ServedHistoryAndRandomSeedDoNotChangeBudget(uint seed, int served)
        {
            world.State.randomState = seed;
            world.State.served = served;
            world.State.reputation = 5;
            catalog.economy.customerGrowthEvery = 1;
            Open();
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(4));
        }

        [Test]
        public void BudgetIsFrozenUntilNextOpening()
        {
            Open();
            world.State.reputation = 10;
            world.State.served = 10000;
            world.Tick(.1f);
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(3));
            Assert.That(world.EndDay().success, Is.True);
            world.State.reputation = 10;
            Open();
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(5));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(10)]
        [TestCase(int.MaxValue)]
        public void CustomOneCustomerCatalogStaysAtOne(int reputation)
        {
            catalog.economy.baseCustomers = catalog.economy.maxCustomers = 1;
            world = new CafeWorld(catalog);
            world.State.reputation = reputation;
            Open();
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(1));
            Assert.That(world.State.todayArrivals, Is.EqualTo(1));
        }

        [Test]
        public void CustomReputationStepAndRangeAreHonoredWithoutOverflow()
        {
            catalog.economy.baseCustomers = 2;
            catalog.economy.maxCustomers = 7;
            catalog.economy.customersReputationStep = 3;
            world = new CafeWorld(catalog);
            world.State.reputation = 9;
            Open();
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(5));

            catalog.economy.baseCustomers = catalog.economy.maxCustomers = int.MaxValue;
            world = new CafeWorld(catalog);
            world.State.reputation = int.MaxValue;
            Open();
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(int.MaxValue));
        }

        [TestCase(0, 5, 5)]
        [TestCase(5, 3, 5)]
        [TestCase(3, 5, 0)]
        [TestCase(3, 5, -1)]
        public void InvalidCustomerConfigurationIsRejected(int basis, int maximum, int step)
        {
            catalog.economy.baseCustomers = basis;
            catalog.economy.maxCustomers = maximum;
            catalog.economy.customersReputationStep = step;
            Assert.Throws<ArgumentException>(() => new CafeWorld(catalog));
        }

        [TestCase(0, 3)]
        [TestCase(5, 4)]
        [TestCase(10, 5)]
        public void CompletingThreeToFiveCustomersAwardsPositiveReputationAndSkillMilestone(int reputation, int count)
        {
            world.State.reputation = reputation;
            world.State.positiveReputationEarned = 9;
            Open();
            CompleteCustomers(count);
            int before = world.State.reputation;
            Assert.That(world.EndDay().success, Is.True);
            Assert.That(world.State.reputation, Is.EqualTo(before + count));
            Assert.That(world.State.todayFailed, Is.Zero);
            Assert.That(world.State.positiveReputationEarned, Is.EqualTo(9 + count));
            Assert.That(world.State.skillPoints, Is.EqualTo(1));
            Assert.That(world.State.rewardedSkillMilestones, Is.EqualTo(1));
            Assert.That(world.EndDay().success, Is.False);
            Assert.That(world.State.skillPoints, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnarrivedSpecialReservationDoesNotIncreaseSettlementTarget(bool authorized)
        {
            Assert.That(world.SyncCafeVisits(new[] { Visit("pending", authorized, 1000) }).success, Is.True);
            Open();
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(4));
            CompleteCustomers(3);
            Assert.That(world.State.todayArrivals, Is.EqualTo(3));
            Assert.That(world.CafeVisit("pending").customerId, Is.Null.Or.Empty);
            int before = world.State.reputation;
            Assert.That(world.EndDay().success, Is.True);
            Assert.That(world.State.reputation, Is.EqualTo(before + 3));
        }

        [Test]
        public void ServedSpecialCustomerCanStillBeInStoreWhenEndingDay()
        {
            Assert.That(world.SyncCafeVisits(new[] { Visit("staying", true) }).success, Is.True);
            Open();
            CompleteCustomers(4);
            Assert.That(world.CafeVisit("staying").status, Is.EqualTo(CafeVisitStatus.Served));
            Assert.That(world.Find(world.CafeVisit("staying").customerId), Is.Not.Null);
            int before = world.State.reputation;
            Assert.That(world.EndDay().success, Is.True);
            Assert.That(world.State.reputation, Is.EqualTo(before + 4));
            Assert.That(world.State.todayFailed, Is.Zero);
            Assert.That(world.CafeVisit("staying").status, Is.EqualTo(CafeVisitStatus.Leaving));
        }

        [TestCase(0, -3)]
        [TestCase(1, -2)]
        public void EarlyCloseCountsUnfinishedArrivedCustomersNotUnarrivedBudget(int completed, int expectedChange)
        {
            world.State.reputation = 10;
            Open();
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(5));
            Assert.That(world.State.todayArrivals, Is.EqualTo(3));
            if (completed == 1)
            {
                world.State.requests[0].automaticRemaining = 0;
                world.Tick(.01f);
            }
            Assert.That(world.State.requests.Count, Is.EqualTo(3 - completed));
            int before = world.State.reputation;
            Assert.That(world.EndDay().success, Is.True);
            Assert.That(world.State.reputation, Is.EqualTo(before + expectedChange));
            Assert.That(world.State.todayFailed, Is.EqualTo(3 - completed));
            Assert.That(world.State.requests, Is.Empty);
            Assert.That(world.State.open, Is.False);
        }

        [Test]
        public void NoArrivalsStillHasMinimumOneSettlementTarget()
        {
            Assert.That(world.SyncCafeVisits(new[]
            {
                Visit("v0", seat: "customer_0"), Visit("v1", seat: "customer_1"), Visit("v2", seat: "customer_2")
            }).success, Is.True);
            Open();
            Assert.That(world.State.todayArrivals, Is.Zero);
            Assert.That(world.EndDay().success, Is.True);
            Assert.That(world.State.reputation, Is.EqualTo(-1));
            Assert.That(world.State.todayFailed, Is.Zero);
        }

        [TestCase(2, 12)]
        [TestCase(2, 24)]
        [TestCase(3, 24)]
        public void LegacyBudgetRestoresUnchangedAndUsesNewBudgetOnlyNextDay(int version, int budget)
        {
            Open();
            var saved = Copy();
            saved.version = version;
            saved.todayCustomerBudget = budget;
            saved.todayArrivals = budget;
            Assert.That(world.Restore(saved).success, Is.True);
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(budget));
            Assert.That(world.State.todayArrivals, Is.EqualTo(budget));
            Assert.That(world.State.requests.Count, Is.EqualTo(3));
            Assert.That(world.EndDay().success, Is.True);
            Assert.That(world.State.reputation, Is.EqualTo(-10), "Legacy days retain the ten-order target cap");
            Open();
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(3));
            Assert.That(world.State.todayArrivals, Is.EqualTo(3));
        }

        [Test]
        public void LegacyTwentyFourPlusSpecialReservationBudgetRestores()
        {
            Assert.That(world.SyncCafeVisits(new[] { Visit("legacy") }).success, Is.True);
            Open();
            var saved = Copy();
            saved.todayCustomerBudget = 25;
            saved.todayArrivals = 24;
            Assert.That(world.Restore(saved).success, Is.True);
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(25));
            Assert.That(world.State.todayArrivals, Is.EqualTo(24));
            Assert.That(world.CafeVisit("legacy").budgetReserved, Is.True);
        }

        [Test]
        public void BudgetBeyondLegacyLimitWithoutSpecialVisitsIsRejectedAtomically()
        {
            Open();
            var live = world.State;
            var saved = Copy();
            saved.todayCustomerBudget = 25;
            Assert.That(world.Restore(saved).success, Is.False);
            Assert.That(world.State, Is.SameAs(live));
        }

        [Test]
        public void ConfiguredMaximumAboveLegacyLimitStillRestores()
        {
            catalog.economy.maxCustomers = 30;
            world = new CafeWorld(catalog);
            Open();
            var saved = Copy();
            saved.todayCustomerBudget = 30;
            Assert.That(world.Restore(saved).success, Is.True);
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(30));
        }
    }
}
