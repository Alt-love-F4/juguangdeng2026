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
        [Test] public void MidBusinessVisitAddsOnlyItsOwnBudgetAndReplacesOrdinaryCustomer()
        {
            world.StartDay(); world.State.todayCustomerBudget = world.State.todayArrivals;
            int budget = world.State.todayCustomerBudget, arrivals = world.State.todayArrivals;
            Sync(Intent());
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.SeatedBeforeServe));
            Assert.That(world.CafeVisit("v1").budgetReserved, Is.False);
            Assert.That(world.CafeVisit("v1").diagnostics, Contains.Item("ordinary_customer_replaced"));
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(budget + 1));
            Assert.That(world.State.todayArrivals, Is.EqualTo(arrivals + 1), "Historical ordinary arrivals must not roll back");
            var order = Order(); var customer = world.Find("customer_0");
            long sequence = world.State.cafeVisitFeedbackSequence;
            Sync(Intent()); Sync(Intent());
            Assert.That(Order(), Is.SameAs(order));
            Assert.That(world.Find("customer_0"), Is.SameAs(customer));
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(budget + 1), "Repeated snapshots must not add another slot");
            Assert.That(world.State.todayArrivals, Is.EqualTo(arrivals + 1));
            Assert.That(world.State.cafeVisitFeedbackSequence, Is.EqualTo(sequence));
            Assert.That(world.CafeVisit("v1").diagnostics.Count(d => d == "ordinary_customer_replaced"), Is.EqualTo(1));
            Assert.That(world.Restore(Copy()).success, Is.True);
        }
        [Test] public void ReplacedOrdinaryCustomerDoesNotPenalizeDailyReputationAndCounterPersists()
        {
            world.StartDay();
            Sync(Intent());
            Assert.That(world.State.todayReplacedCustomers, Is.EqualTo(1));
            Assert.That(world.Restore(Copy()).success, Is.True);
            Assert.That(world.State.todayReplacedCustomers, Is.EqualTo(1));
            foreach (var request in world.State.requests.ToArray())
            {
                request.automaticRemaining = 0;
            }
            world.Tick(.01f);
            int served = world.State.todayServed;
            Assert.That(served, Is.EqualTo(world.State.todayArrivals - 1));
            int reputation = world.State.reputation;
            world.EndDay();
            Assert.That(world.State.reputation, Is.EqualTo(reputation + served));
            world.StartDay();
            Assert.That(world.State.todayReplacedCustomers, Is.Zero);
        }

        [TestCase(-1)] [TestCase(100)]
        public void InvalidReplacementCounterRejectsSave(int count)
        {
            world.StartDay();
            var snapshot = Copy();
            snapshot.todayReplacedCustomers = count;
            Assert.That(world.Restore(snapshot).success, Is.False);
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
        [Test] public void ReplacementCancelsUnfinishedOrderAndClearsSpokenAndFocusWithoutSettlement()
        {
            world.StartDay(); var original = world.Find("customer_0");
            var request = world.State.requests.Find(r => r.customerId == original.id);
            world.Focus(request.id); world.State.spokenCustomers.Add(original.id);
            var cup = CorrectCup(request);
            int money = world.State.money, reputation = world.State.reputation, failed = world.State.todayFailed;
            int served = world.State.served, successful = world.State.successfulOrders, revenue = world.State.todayRevenue;
            Sync(Intent());
            Assert.That(world.Find(original.id), Is.Not.SameAs(original));
            Assert.That(world.Request(request.id), Is.Null);
            Assert.That(world.State.requests.Count(r => r.customerId == original.id), Is.EqualTo(1));
            Assert.That(world.State.spokenCustomers, Does.Not.Contain(original.id));
            Assert.That(world.State.focusedRequestId, Is.Not.EqualTo(request.id));
            Assert.That(world.Request(world.State.focusedRequestId), Is.Not.Null);
            Assert.That(world.Find(cup.id), Is.SameAs(cup), "Player-made drinks must not be discarded or refunded");
            Assert.That(world.State.money, Is.EqualTo(money));
            Assert.That(world.State.reputation, Is.EqualTo(reputation));
            Assert.That(world.State.todayFailed, Is.EqualTo(failed));
            Assert.That(world.State.served, Is.EqualTo(served));
            Assert.That(world.State.successfulOrders, Is.EqualTo(successful));
            Assert.That(world.State.todayRevenue, Is.EqualTo(revenue));
            Assert.That(world.Find("customer_0").person.characterId, Is.EqualTo("character_v1"));
        }
        [Test] public void ThreeCustomerBudgetDoesNotBlockStoryOrCreateOrdinaryRefillQuota()
        {
            catalog.economy.baseCustomers = catalog.economy.maxCustomers = 3;
            world = new CafeWorld(catalog); world.StartDay();
            Assert.That(world.State.todayArrivals, Is.EqualTo(3));
            Sync(Intent());
            Assert.That(Order(), Is.Not.Null);
            Assert.That(world.State.todayArrivals, Is.EqualTo(4));
            Assert.That(world.State.todayCustomerBudget, Is.EqualTo(4));
            foreach (var r in world.State.requests) r.automaticRemaining = 0;
            world.Tick(.01f); world.Tick(catalog.customerInterval);
            Assert.That(world.State.requests, Is.Empty);
            Assert.That(world.State.todayArrivals, Is.EqualTo(4));
        }
        private void UseTables(int count = 3)
        {
            catalog.customerTableCount = count;
            world = new CafeWorld(catalog);
            world.State.seats = count * 2;
        }
        private void MoveCustomer(int from, int to)
        {
            string id = "customer_" + from;
            var customer = world.Find(id);
            var request = world.State.requests.Find(r => r.customerId == id);
            customer.id = "customer_" + to;
            request.customerId = customer.id;
        }
        [TestCase(false, 0f, CafeVisitStatus.Reserved)]
        [TestCase(true, 10f, CafeVisitStatus.PendingArrival)]
        public void UnauthorizedOrDelayedVisitDoesNotDisplaceAnyone(bool authorized, float delay, CafeVisitStatus status)
        {
            world.StartDay();
            var occupants = world.State.entities.Where(e => e.specId == "customer").ToArray();
            var requests = world.State.requests.ToArray();
            var intent = Intent(authorized: authorized); intent.arrivalDelaySeconds = delay;
            Sync(intent); Sync(intent);
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(status));
            Assert.That(world.State.entities.Where(e => e.specId == "customer"), Is.EqualTo(occupants));
            Assert.That(world.State.requests, Is.EqualTo(requests));
            Assert.That(world.CafeVisit("v1").diagnostics, Does.Not.Contain("ordinary_customer_replaced"));
            intent.arrivalAuthorized = true;
            if (delay > 0) world.State.businessElapsed = delay;
            Sync(intent);
            Assert.That(Order(), Is.Not.Null);
            Assert.That(world.CafeVisit("v1").diagnostics, Contains.Item("ordinary_customer_replaced"));
        }
        [Test] public void NoEligibleProductDoesNotDisplaceOrdinaryCustomer()
        {
            world.StartDay(); var original = world.Find("customer_0");
            var requests = world.State.requests.ToArray();
            world.State.unlockedProducts.Clear(); Sync(Intent());
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.WaitingForSeat));
            Assert.That(world.Find("customer_0"), Is.SameAs(original));
            Assert.That(world.State.requests, Is.EqualTo(requests));
            world.State.unlockedProducts.Add(catalog.products[0].id); Sync(Intent());
            Assert.That(Order(), Is.Not.Null);
        }
        [Test] public void DuplicateIdentityDoesNotDisplaceAnyCustomer()
        {
            world.StartDay(); var requests = world.State.requests.ToArray();
            world.Find("customer_1").person.personId = "person_v1";
            var original = world.Find("customer_0"); Sync(Intent()); Sync(Intent());
            Assert.That(world.Find("customer_0"), Is.SameAs(original));
            Assert.That(world.State.requests, Is.EqualTo(requests));
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.WaitingForSeat));
            Assert.That(world.CafeVisit("v1").diagnostics, Contains.Item("character_already_present"));
        }
        [TestCase(0, 0)]
        [TestCase(0, 2)]
        [TestCase(2, 0)]
        [TestCase(2, 2)]
        public void DedicatedTableReplacesOrdinaryOccupantOnEitherSide(int occupied, int requested)
        {
            UseTables(2); world.StartDay();
            if (occupied != 0) MoveCustomer(0, occupied);
            var original = world.Find("customer_" + occupied);
            var untouched = world.Find("customer_1");
            Sync(Intent(seat: "customer_" + requested));
            Assert.That(world.CafeVisit("v1").seatId, Is.EqualTo("customer_" + requested));
            Assert.That(world.Find(original.id), Is.Not.SameAs(original));
            Assert.That(world.Find("customer_1"), Is.SameAs(untouched));
            Assert.That(world.State.requests.Count, Is.EqualTo(2));
            Assert.That(world.State.requests.Count(r => r.customerId == "customer_" + requested), Is.EqualTo(1));
            Assert.That(world.Restore(Copy()).success, Is.True);
        }
        [Test] public void EmptyLegalSeatWinsBeforeAnyOrdinaryReplacement()
        {
            world.State.seats = 4; world.StartDay();
            var originals = world.State.entities.Where(e => e.specId == "customer").ToArray();
            Sync(Intent(seat: null));
            Assert.That(world.CafeVisit("v1").seatId, Is.EqualTo("customer_3"));
            foreach (var original in originals) Assert.That(world.State.entities, Does.Contain(original));
            Assert.That(world.CafeVisit("v1").diagnostics, Does.Not.Contain("ordinary_customer_replaced"));
        }
        [Test] public void GenericReplacementRespectsEarlierGenericReservation()
        {
            UseTables(); world.StartDay();
            var protectedCustomer = world.Find("customer_0");
            Sync(Intent("earlier", authorized: false, seat: null), Intent(seat: null));
            Assert.That(world.CafeVisit("v1").seatId, Is.EqualTo("customer_1"));
            Assert.That(world.Find("customer_0"), Is.SameAs(protectedCustomer));
            Assert.That(world.CafeVisit("earlier").status, Is.EqualTo(CafeVisitStatus.Reserved));
        }
        [Test] public void GenericReplacementRespectsLaterDedicatedWholeTableReservation()
        {
            UseTables(); world.StartDay();
            var protectedCustomer = world.Find("customer_0");
            Sync(Intent(seat: null), Intent("later", authorized: false, seat: "customer_3"));
            Assert.That(world.CafeVisit("v1").seatId, Is.EqualTo("customer_1"));
            Assert.That(world.Find("customer_0"), Is.SameAs(protectedCustomer));
            Assert.That(world.Find("customer_3"), Is.Null);
        }
        [Test] public void EarlierDedicatedReservationBlocksReplacementOnOppositeSide()
        {
            UseTables(); world.StartDay();
            var original = world.Find("customer_0");
            Sync(Intent("earlier", authorized: false), Intent(seat: "customer_3"));
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.WaitingForSeat));
            Assert.That(world.Find("customer_0"), Is.SameAs(original));
            Assert.That(Order(), Is.Null);
        }
        [Test] public void FullySpecialTablesKeepLateVisitorWaitingEvenAfterService()
        {
            UseTables();
            Sync(Intent("v1"), Intent("v2", seat: "customer_1"), Intent("v3", seat: "customer_2"));
            world.StartDay(); AutoServe("v1");
            var occupants = world.State.entities.Where(e => e.specId == "customer").ToArray();
            var requests = world.State.requests.ToArray(); int arrivals = world.State.todayArrivals;
            Sync(Intent("late", seat: null)); Sync(Intent("late", seat: null));
            Assert.That(world.CafeVisit("late").status, Is.EqualTo(CafeVisitStatus.WaitingForSeat));
            Assert.That(world.State.entities.Where(e => e.specId == "customer"), Is.EqualTo(occupants));
            Assert.That(world.State.requests, Is.EqualTo(requests));
            Assert.That(world.State.todayArrivals, Is.EqualTo(arrivals));
            Assert.That(world.CafeVisit("late").diagnostics, Does.Not.Contain("ordinary_customer_replaced"));
        }
        [Test] public void OppositeSideStoryVisitCannotBeDisplaced()
        {
            UseTables(); Sync(Intent("first", seat: "customer_3")); world.StartDay();
            var original = world.Find("customer_3");
            Sync(Intent());
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.WaitingForSeat));
            Assert.That(world.Find("customer_3"), Is.SameAs(original));
            Assert.That(world.CafeVisit("first").status, Is.EqualTo(CafeVisitStatus.SeatedBeforeServe));
        }
        [Test] public void NamedNarrativeIdentityWithoutVisitOwnershipIsNotOrdinary()
        {
            world.StartDay(); var original = world.Find("customer_0");
            original.person.narrativeCharacterId = "other_story_character";
            Sync(Intent());
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.WaitingForSeat));
            Assert.That(world.Find("customer_0"), Is.SameAs(original));
        }
        [TestCase(0)]
        [TestCase(2)]
        public void NarrativeServiceOwnedTableIsProtected(int requested)
        {
            UseTables(2); world.StartDay();
            world.State.requests.Find(r => r.customerId == "customer_0").automaticRemaining = 0;
            world.Tick(.01f); world.EnableProduction();
            Assert.That(world.TryBeginNarrativeService("protected-service", new[] { new CafeNarrativeOrderSpec { productId = "blackcoffee" } },
                _ => Assert.Fail("Replacement cannot complete narrative service"), out var lease, out var error), Is.True, error);
            try
            {
                var customer = world.Find("customer_0");
                var request = world.State.requests.Find(r => r.customerId == customer.id);
                Sync(Intent(seat: "customer_" + requested));
                Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.WaitingForSeat));
                Assert.That(world.Find("customer_0"), Is.SameAs(customer));
                Assert.That(world.Request(request.id), Is.SameAs(request));
                Assert.That(world.State.focusedRequestId, Is.EqualTo(request.id));
                Assert.That(world.HasNarrativeService, Is.True);
            }
            finally { lease.Dispose(); }
        }
        [Test] public void TutorialCustomerIsNeverAnOrdinaryReplacementCandidate()
        {
            Assert.That(world.StartTutorialCustomer().success, Is.True);
            var customer = world.Find(CafeWorld.TutorialCustomerId);
            // Defensive coverage for a mixed tutorial/business state; StartDay normally refuses it.
            world.State.open = true; world.State.businessPhase = BusinessPhase.Open;
            Sync(Intent());
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.WaitingForSeat));
            Assert.That(world.Find(CafeWorld.TutorialCustomerId), Is.SameAs(customer));
            Assert.That(world.Request(CafeWorld.TutorialOrderId), Is.Not.Null);
        }
        [Test] public void ReplacementClearsBothOrdinaryOccupantsOfLegacyConflictedTable()
        {
            UseTables(2); world.StartDay(); MoveCustomer(1, 2);
            var first = world.Find("customer_0"); var opposite = world.Find("customer_2");
            var requests = world.State.requests.ToArray();
            Sync(Intent());
            Assert.That(Order(), Is.Not.Null);
            Assert.That(world.Find(first.id), Is.Not.SameAs(first));
            Assert.That(world.Find(opposite.id), Is.Not.SameAs(opposite));
            foreach (var r in requests) Assert.That(world.Request(r.id), Is.Null);
            Assert.That(world.State.requests.Count, Is.EqualTo(1));
            Assert.That(world.Find("customer_2"), Is.Null);
            Assert.That(world.State.todayArrivals, Is.EqualTo(3));
            Assert.That(world.State.todayFailed, Is.Zero);
        }
        [Test] public void MixedOrdinaryAndSpecialTableConflictCannotCausePartialEviction()
        {
            UseTables(2); Sync(Intent("first")); world.StartDay(); MoveCustomer(1, 2);
            var special = world.Find("customer_0"); var ordinary = world.Find("customer_2");
            var requests = world.State.requests.ToArray();
            Sync(Intent(seat: "customer_2"));
            Assert.That(world.CafeVisit("v1").status, Is.EqualTo(CafeVisitStatus.WaitingForSeat));
            Assert.That(world.Find("customer_0"), Is.SameAs(special));
            Assert.That(world.Find("customer_2"), Is.SameAs(ordinary));
            Assert.That(world.State.requests, Is.EqualTo(requests));
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
