using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeVisitTests
    {
        private CafeCatalog catalog;
        private CafeWorld world;
        [SetUp] public void Setup()
        {
            catalog = JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text);
            world = new CafeWorld(catalog);
        }
        private CafeVisitIntent Intent(string id = "v1", bool authorized = true, string seat = "customer_0", bool leave = false)
        {
            return new CafeVisitIntent { visitInstanceId = id, characterId = "character_" + id,
                arrivalAuthorized = authorized, preferredSeatId = seat, leaveAfterServed = leave,
                identity = new CafePersonIdentity { personId = "person_" + id, displayName = id, role = CafePersonRole.Customer } };
        }
        private void Sync(params CafeVisitIntent[] intents) => Assert.That(world.SyncCafeVisits(intents).success, Is.True);
        private CafeSnapshot Copy() => JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
        private void Ack(string stage, string id = "v1")
        {
            var f = world.State.cafeVisitFeedback.First(x => x.visitInstanceId == id && x.stage == stage && !x.isClick);
            Assert.That(world.AcknowledgeCafeVisitFeedback(f.sequence).success, Is.True);
        }
        private CustomerRequest Order(string id = "v1") => world.State.requests.Find(r => r.customerId == world.CafeVisit(id).customerId);
        private void AutoServe(string id = "v1")
        {
            Order(id).automaticRemaining = 0;
            world.Tick(.01f);
        }
        private WorldEntity CorrectCup(CustomerRequest order)
        {
            var product = world.Product(order.productId);
            var cup = new WorldEntity { id = "free_999", specId = world.RequiredCupSpecId(order), area = AreaId.Counter, latteArt = true };
            foreach (var ingredient in product.ingredients) cup.contents.Add(new Portion { specId = ingredient.specId, units = ingredient.units });
            world.State.entities.Add(cup);
            return cup;
        }
        [Test] public void ReserveRequiresSpawnAndElapsedTimeAndCopiesInput()
        {
            var intent = Intent(authorized: false); intent.arrivalDelaySeconds = 5;
            Sync(intent); intent.identity.displayName = "changed";
            world.StartDay(); world.Tick(6);
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.Reserved));
            Assert.That(world.Find("customer_0"), Is.Null);
            Assert.That(world.CafeVisit("v1").identity.displayName, Is.EqualTo("v1"));
            intent.arrivalAuthorized = true; Sync(intent);
            Assert.That(Order(), Is.Not.Null);
            Assert.That(world.Find("customer_0").x, Is.EqualTo(-6));
            int arrivals = world.State.todayArrivals; Sync(intent); Sync(intent);
            Assert.That(world.State.todayArrivals, Is.EqualTo(arrivals));
            Assert.That(world.State.cafeVisitFeedback.Count(f => f.stage == "arrived"), Is.EqualTo(1));
        }
        [Test] public void AuthorizedDelayDoesNotStartPatienceUntilSeated()
        {
            var intent = Intent(); intent.arrivalDelaySeconds = 10; Sync(intent); world.StartDay();
            float patience = world.CafeVisit("v1").patienceRemaining;
            world.Tick(9);
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.PendingArrival));
            Assert.That(world.CafeVisit("v1").patienceRemaining, Is.EqualTo(patience));
            world.Tick(1); Assert.That(Order(), Is.Not.Null);
            Assert.That(Order().patience, Is.EqualTo(patience));
        }
        [Test] public void OpeningReservationsRaiseMinimumAndRestoreAboveOrdinaryMaximum()
        {
            catalog.economy.maxCustomers = 1; catalog.economy.baseCustomers = 1;
            world = new CafeWorld(catalog);
            Sync(Intent("v1"), Intent("v2", seat: "customer_1")); world.StartDay();
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(3));
            Assert.That(world.State.todayArrivals, Is.EqualTo(3));
            Assert.That(world.CafeVisit("v1").diagnostics, Contains.Item("customer_budget_minimum_adjusted"));
            Assert.That(world.Restore(Copy()).success, Is.True);
        }
        [Test] public void MidBusinessVisitAddsBudgetAndWaitsOnlyForSeat()
        {
            world.StartDay(); world.State.todayCustomerBudget = world.State.todayArrivals;
            int budget = world.State.todayCustomerBudget; Sync(Intent());
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.WaitingForSeat));
            Assert.That(world.CafeVisit("v1").budgetReserved, Is.True);
            Assert.That(world.CafeVisit("v1").diagnostics, Does.Not.Contain("budget_unavailable"));
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(budget + 1));
            Sync(Intent());
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(budget + 1), "Repeated snapshots must not add another slot");
            var ordinary = world.State.requests.Find(r => r.customerId == "customer_0");
            ordinary.automaticRemaining = 0;
            world.Tick(.01f);
            Assert.That(Order(), Is.Not.Null, "A late story visitor must arrive even after normal traffic has exhausted its budget");
            Assert.That(world.Restore(Copy()).success, Is.True);
        }
        [Test] public void DedicatedSeatConflictQueuesAndReleasesToNextVisit()
        {
            Sync(Intent("v1"), Intent("v2")); world.StartDay();
            Assert.That(Order("v1"), Is.Not.Null);
            Assert.That(world.CafeVisit("v2").status, Is.EqualTo(CafeVisitStatus.WaitingForSeat));
            Assert.That(world.CafeVisit("v2").diagnostics, Contains.Item("seat_conflict"));
            world.EndCafeVisit("v1", "ncs_despawn");
            Assert.That(Order("v2"), Is.Not.Null);
            Assert.That(world.CafeVisit("v2").seatId, Is.EqualTo("customer_0"));
        }
        [Test] public void ExistingOrdinaryCustomerIsNotEvictedAndSpecialWinsReleasedSeat()
        {
            world.StartDay(); var original = world.Find("customer_0"); Sync(Intent());
            Assert.That(world.Find("customer_0"), Is.SameAs(original));
            world.State.requests.Find(r => r.customerId == original.id).automaticRemaining = 0;
            world.Tick(.01f);
            Assert.That(Order(), Is.Not.Null);
            Assert.That(world.Find("customer_0").person.characterId, Is.EqualTo("character_v1"));
        }
        [Test] public void StoryReservationsDoNotReduceOrdinaryDailyTraffic()
        {
            world.State.randomState = 3;
            var baseline = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            baseline.State.randomState = 3;
            baseline.StartDay();
            Sync(Intent()); world.StartDay();
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(baseline.State.todayCustomerBudget + 1));
            Assert.That(world.State.requests.Any(r => world.Find(r.customerId).person.narrativeCharacterId == null), Is.True);
            Assert.That(Order(), Is.Not.Null);
        }
        [Test] public void OrdinaryArtworkDoesNotBlockAnNcsActorWithTheSameArtKey()
        {
            world.StartDay();
            var ordinary = world.Find("customer_0");
            ordinary.person.characterId = "character_v1";
            var intent = Intent(seat: "customer_1");
            var order = world.State.requests.Find(r => r.customerId == "customer_1");
            order.automaticRemaining = 0;
            world.Tick(.01f);
            Sync(intent);
            Assert.That(Order(), Is.Not.Null);
            Assert.That(world.CafeVisit("v1").diagnostics, Does.Not.Contain("character_already_present"));
        }
        [Test] public void GenericReservationProtectsSeatBeforeAuthorization()
        {
            Sync(Intent(authorized: false, seat: null)); world.StartDay();
            Assert.That(world.State.requests.Count, Is.EqualTo(world.State.seats - 1));
            var intent = Intent(seat: null); Sync(intent);
            Assert.That(Order(), Is.Not.Null);
        }
        [Test] public void ManualServiceSettlesOnceAndKeepsServedCustomerWithoutOrder()
        {
            Sync(Intent()); world.StartDay(); var order = Order(); var cup = CorrectCup(order);
            cup.area = AreaId.Cafe;
            world.Focus(order.id); int money = world.State.money;
            Assert.That(world.Interact(cup.id, order.customerId).success, Is.True);
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.Served));
            Assert.That(Order(), Is.Null); Assert.That(world.State.money, Is.GreaterThan(money));
            int revenue = world.State.todayRevenue;
            Assert.That(world.Interact(cup.id, order.customerId).success, Is.False);
            Assert.That(world.State.todayRevenue, Is.EqualTo(revenue));
            Assert.That(world.Restore(Copy()).success, Is.True);
        }
        [Test] public void AutomaticServiceDefaultLeaveWaitsForServedThenLeavingAcknowledgment()
        {
            Sync(Intent(leave: true)); world.StartDay(); AutoServe();
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.Served));
            Assert.That(world.Find("customer_0"), Is.Not.Null);
            long seq = world.State.cafeVisitFeedback.First(f => f.stage == "served").sequence;
            Ack("served"); Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.Leaving));
            world.AcknowledgeCafeVisitFeedback(seq);
            Assert.That(world.State.cafeVisitFeedback.Count(f => f.stage == "leaving"), Is.EqualTo(1));
            Ack("leaving"); Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.Ended));
        }
        [Test] public void ServedPatienceAndRepeatedClicksSurviveRestoreWithoutDuplicateReward()
        {
            var intent = Intent(); intent.hasPatienceOverride = true; intent.patienceSeconds = 5;
            Sync(intent); world.StartDay(); AutoServe(); Ack("served");
            int served = world.State.todayServed; int failed = world.State.todayFailed;
            world.RequestCafeVisitClick("v1"); world.RequestCafeVisitClick("v1");
            var clicks = world.State.cafeVisitFeedback.Where(f => f.isClick).ToArray();
            Assert.That(clicks.Length, Is.EqualTo(2)); Assert.That(clicks[1].sequence, Is.GreaterThan(clicks[0].sequence));
            Assert.That(world.Restore(Copy()).success, Is.True); Sync(intent); world.Tick(6);
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.Leaving));
            Assert.That(world.CafeVisit("v1").endReason, Is.EqualTo("patience_expired"));
            Assert.That(world.State.todayServed, Is.EqualTo(served)); Assert.That(world.State.todayFailed, Is.EqualTo(failed));
            Ack("leaving"); Sync(intent); Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.Ended));
        }
        [Test] public void TimeoutCountsOneFailureAndCanRestoreLeaving()
        {
            Sync(Intent()); world.StartDay(); Order().patience = .1f;
            int failures = world.State.todayFailed; world.Tick(.2f); world.Tick(.2f);
            Assert.That(world.State.todayFailed, Is.EqualTo(failures + 1));
            Assert.That(world.State.cafeVisitFeedback.Count(f => f.stage == "leaving"), Is.EqualTo(1));
            Assert.That(world.Restore(Copy()).success, Is.True);
        }
        [Test] public void ClosedServedVisitHasNoOutstandingOrderPenalty()
        {
            Sync(Intent()); world.StartDay(); AutoServe();
            int failures = world.State.todayFailed, ordinary = world.State.requests.Count;
            world.EndDay(); Assert.That(world.State.todayFailed, Is.EqualTo(failures + ordinary));
            Assert.That(world.Restore(Copy()).success, Is.True);
            Ack("leaving"); Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.Ended));
        }
        [Test] public void PendingAndOutboxRestoreAndEndedInputCannotResurrect()
        {
            var intent = Intent(authorized: false); Sync(intent); world.StartDay();
            long sequence = world.State.cafeVisitFeedbackSequence;
            Assert.That(world.Restore(Copy()).success, Is.True); Sync(intent);
            Assert.That(world.State.cafeVisitFeedbackSequence, Is.EqualTo(sequence));
            intent.isEnded = true; intent.endReason = "reservation_cancelled"; Sync(intent);
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.Ended));
            intent.isEnded = false; intent.arrivalAuthorized = true; Sync(intent);
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.Ended));
        }
        [Test] public void DuplicateFeedbackSnapshotIsRejectedAtomically()
        {
            Sync(Intent()); world.StartDay(); var live = world.State; var copy = Copy();
            copy.cafeVisitFeedback.Add(copy.cafeVisitFeedback[0]);
            Assert.That(world.Restore(copy).success, Is.False); Assert.That(world.State, Is.SameAs(live));
        }
        [Test] public void ProductionRemakeDoesNotEmitServedButFinalSuccessDoes()
        {
            Sync(Intent()); world.StartDay(); world.EnableProduction(); world.EnterProduction();
            var order = Order(); world.Focus(order.id); var product = world.Product(order.productId);
            product.minimumScore = 101; product.maxRemakes = 1;
            world.SelectServingCup(world.RequiredCupSpecId(order) == "cup.small" ? "s.cups.small" : "s.cups.large");
            Assert.That(world.DeliverProductionCup().success, Is.True);
            Assert.That(Order(), Is.SameAs(order)); Assert.That(order.remakes, Is.EqualTo(1));
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.SeatedBeforeServe));
            Assert.That(world.State.cafeVisitFeedback.Any(f => f.stage == "served"), Is.False);
            product.minimumScore = 0; product.requiresLatteArt = false;
            world.SelectServingCup(world.RequiredCupSpecId(order) == "cup.small" ? "s.cups.small" : "s.cups.large");
            var cup = world.ServingCup;
            foreach (var amount in product.evaluation.amounts)
                cup.contents.Add(new Portion { specId = amount.ingredientId, coffeeKind = amount.coffeeKind, units = 1,
                    milliliters = amount.idealMl * world.OrderPortionScale(order), grams = amount.idealGrams * world.OrderPortionScale(order),
                    temperatureCelsius = amount.coffeeKind == "cold_brew" ? 22 : 95, productionScore = 100 });
            Assert.That(world.DeliverProductionCup().feedback, Is.EqualTo(CafeFeedbackKind.DeliverySatisfied));
            Assert.That(Order(), Is.Null); Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.Served));
        }
        [Test] public void AutomaticDeliveryUsesSameServedHandoff()
        {
            Sync(Intent()); world.StartDay(); var order = Order(); world.Focus(order.id); CorrectCup(order);
            world.State.automaticDelivery = true; world.Tick(.01f);
            Assert.That(Order(), Is.Null); Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.Served));
            Assert.That(world.State.cafeVisitFeedback.Count(f => f.stage == "served"), Is.EqualTo(1));
        }
    }
}
