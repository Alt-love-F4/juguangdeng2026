using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafePeopleTests
    {
        private CafeCatalog catalog;
        private CafeWorld world;
        [SetUp] public void Setup()
        {
            catalog=JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text);
            world=new CafeWorld(catalog);
        }
        [Test] public void LobbyEntityStartsBusinessOnlyOnceAndCannotBeMoved()
        {
            Assert.That(world.State.open,Is.False);Assert.That(world.State.requests,Is.Empty);
            Assert.That(world.ActivateBusinessControl("s.beans").success,Is.False);
            Assert.That(world.ActivateBusinessControl("business.start").success,Is.True);
            int arrivals=world.State.todayArrivals,count=world.State.requests.Count;
            Assert.That(world.State.businessPhase,Is.EqualTo(BusinessPhase.Open));Assert.That(count,Is.GreaterThan(0));
            Assert.That(world.ActivateBusinessControl("business.start").success,Is.False);
            Assert.That(world.State.todayArrivals,Is.EqualTo(arrivals));Assert.That(world.State.requests.Count,Is.EqualTo(count));
            Assert.That(world.Move("business.start",AreaId.Counter,0,0).success,Is.False);
        }
        [Test] public void OpeningEntityRequiresTheLobbyAndCanStartANewDayAfterClosing()
        {
            world.State.currentArea=AreaId.Counter;Assert.That(world.ActivateBusinessControl("business.start").success,Is.False);
            world.State.currentArea=AreaId.Cafe;world.ActivateBusinessControl("business.start");world.EndDay();
            Assert.That(world.ActivateBusinessControl("business.start").success,Is.True);Assert.That(world.State.day,Is.EqualTo(2));
        }
        [Test] public void TutorialCustomerHasAClosedShopBlackCoffeeOrder()
        {
            Assert.That(world.State.open, Is.False);
            Assert.That(world.StartTutorialCustomer().success, Is.True);
            Assert.That(world.State.open, Is.False);
            Assert.That(world.State.focusedRequestId, Is.EqualTo("tutorial_order"));
            Assert.That(world.State.requests.Count, Is.EqualTo(1));
            Assert.That(world.State.requests[0].productId, Is.EqualTo("blackcoffee"));
            Assert.That(world.Find("customer_0"), Is.Not.Null);
            world.EndTutorialCustomer();
            Assert.That(world.Find("customer_0"), Is.Null);
            Assert.That(world.State.requests, Is.Empty);
        }
        [Test] public void TutorialCustomerWaitsWithoutBusinessTimersAndCannotBeDuplicated()
        {
            world.StartTutorialCustomer();
            var request = world.Request(CafeWorld.TutorialOrderId);
            float patience = request.patience, remaining = world.State.timeRemaining;
            Assert.That(world.StartTutorialCustomer().success, Is.True);
            Assert.That(world.State.requests.Count, Is.EqualTo(1));
            Assert.That(world.StartDay().success, Is.False);
            world.Tick(1000);
            Assert.That(request.patience, Is.EqualTo(patience));
            Assert.That(world.State.timeRemaining, Is.EqualTo(remaining));
            Assert.That(world.State.todayArrivals, Is.Zero);
            world.EndTutorialCustomer();
            world.EndTutorialCustomer();
            Assert.That(world.State.requests, Is.Empty);
            Assert.That(world.StartDay().success, Is.True);
        }
        [Test] public void ClosedShopSnapshotOnlyAllowsTheOwnedTutorialOrder()
        {
            world.StartTutorialCustomer();
            var snapshot = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            Assert.That(world.Restore(snapshot).success, Is.True);
            var live = world.State;
            snapshot = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(live));
            snapshot.requests[0].id = "ordinary_order";
            snapshot.focusedRequestId = "ordinary_order";
            Assert.That(world.Restore(snapshot).success, Is.False);
            Assert.That(world.State, Is.SameAs(live));
        }
        [Test] public void OrdinaryVisitorsHaveIndependentIdentitiesAndOnlyPlaceOrders()
        {
            world.StartDay();var ids=new HashSet<string>();
            foreach(var request in world.State.requests)
            {
                var customer=world.Find(request.customerId);Assert.That(customer.person,Is.Not.Null);
                Assert.That(ids.Add(customer.person.personId),Is.True);Assert.That(customer.person.personId,Is.Not.EqualTo(customer.id));
                Assert.That(customer.person.role,Is.EqualTo(CafePersonRole.Customer));Assert.That(customer.person.displayName,Is.Not.Empty);
                Assert.That(customer.person.narrativeCharacterId,Is.Null.Or.Empty);Assert.That(customer.npcId,Is.Null.Or.Empty);
                Assert.That(world.Product(request.productId),Is.Not.Null);
            }
            var old=world.Find("customer_0").person.personId;world.EndDay();world.StartDay();
            Assert.That(world.Find("customer_0").person.personId,Is.Not.EqualTo(old),"座位复用不能成为人物身份复用");
            Assert.That(world.State.topics,Is.Empty);
        }
        [Test] public void LegacySaveGainsOnlyMissingBusinessEntranceAndVisitorIdentity()
        {
            world.StartDay();world.Take("s.milk",AreaId.Counter,1,-2,out var milk);int stock=world.Find("s.milk").stock;
            var copy=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            copy.entities.RemoveAll(e=>e.id=="business.start");foreach(var e in copy.entities) e.person=null;
            int requests=copy.requests.Count;
            Assert.That(world.Restore(copy).success,Is.True);Assert.That(world.Find("business.start"),Is.Not.Null);
            Assert.That(world.Find(milk.id).person,Is.Null);
            Assert.That(world.Find(milk.id).x,Is.EqualTo(1));Assert.That(world.Find("s.milk").stock,Is.EqualTo(stock));
            Assert.That(world.State.requests.Count,Is.EqualTo(requests));Assert.That(world.Find("customer_0").person,Is.Not.Null);
            Assert.That(world.Restore(JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State))).success,Is.True);
            Assert.That(world.State.entities.FindAll(e=>e.id=="business.start").Count,Is.EqualTo(1));
        }
        [Test] public void MalformedPersonSaveIsRejectedWithoutReplacingLiveWorld()
        {
            world.StartDay();var live=world.State;var copy=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(live));
            copy.entities.Find(e=>e.id=="customer_0").person.displayName="";
            Assert.That(world.Restore(copy).success,Is.False);Assert.That(world.State,Is.SameAs(live));
        }
        private sealed class IdentitySource : ICafeCustomerIdentitySource
        {
            public int calls;
            public CafePersonIdentity CreateIdentity(string visitId,int seatIndex)
            {
                calls++;return new CafePersonIdentity{personId="person_"+seatIndex,displayName="测试客人",narrativeCharacterId="existing-character-key",visualId="customer"};
            }
        }
        [Test] public void ExplicitIdentityAdapterIsUsedAndRestorationDoesNotCallItAgain()
        {
            var source=new IdentitySource();world=new CafeWorld(catalog,source);world.StartDay();
            Assert.That(source.calls,Is.EqualTo(world.State.requests.Count));Assert.That(world.Find("customer_0").person.narrativeCharacterId,Is.EqualTo("existing-character-key"));
            int calls=source.calls;var copy=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            Assert.That(world.Restore(copy).success,Is.True);Assert.That(source.calls,Is.EqualTo(calls));
        }
    }
}
