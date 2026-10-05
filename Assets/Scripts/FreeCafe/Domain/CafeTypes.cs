using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    [Flags] public enum Capability { None=0, Movable=1, Source=2, Portion=4, Container=8, Processor=16, Customer=32, Delivery=64, Discard=128 }
    public enum AreaId { Cafe, Counter, TopFloor, Basement }
    // Preserve the old numeric values for v1 migration; Loaded waits for manual input.
    public enum MachinePhase { Idle=0, Running=1, Ready=2, Loaded=3 }
    public enum OperationControl { Timer, Clicks, Qte }
    public enum BusinessPhase { Preparation, Open, Night }
    [Serializable] public sealed class EntitySpec
    {
        public string id, label, artId, sourceItemId;
        public Capability capabilities;
        public int capacity, initialStock, refillCost, unitCost;
        public float width=1, height=1;
        public bool Has(Capability value) => (capabilities & value) == value;
    }
    [Serializable] public sealed class OperationSpec
    {
        public string id, label, machineSpecId, inputSpecId, outputSpecId;
        public float seconds, clickInterval=.5f;
        public int clicks=3;
        public OperationControl control;
        public bool acceptsContainer=true;
    }
    [Serializable] public sealed class Portion { public string specId; public int units, quality; }
    [Serializable] public sealed class ProductSpec
    {
        public string id, label, cupSpecId, recipeId;
        public int price, unlockReputation, qualityPriceStep=50;
        public bool requiresLatteArt;
        public Portion[] ingredients;
    }
    [Serializable] public sealed class RecipeStepSpec
    {
        public string id, kind, label, inputSpecId, outputSpecId, targetSpecId, operationId;
        public int units=1;
        public float seconds;
    }
    [Serializable] public sealed class RecipeSpec
    {
        public string id, productId, label;
        public RecipeStepSpec[] steps;
    }
    [Serializable] public sealed class InitialEntity { public string id, specId; public AreaId area; public float x, y; }
    [Serializable] public sealed class AreaSpec { public AreaId id; public string label, artId; public float halfWidth=9, halfHeight=5; }
    [Serializable] public sealed class NpcSpec { public string id, name, dialogue, topic; public int unlockReputation, minimumBeanQuality; }
    [Serializable] public sealed class EconomySpec
    {
        public int baseCustomers=12, customerGrowthEvery=30, maxCustomers=24, initialSeats=3;
        public int wastePenalty=10, wrongOrderPenalty=10, hireCost=200, expansionCost=200, extraMachineCost=200, upgradeCost=100;
        public int seatsPerExpansion=2, maxStaff=4, maxMachineCopies=3;
        public float autoSeconds=20, autoChance=.2f, staffSpeedPerHire=.1f, timerUpgradeMultiplier=.5f, priceUpgradeMultiplier=1.5f;
        public int[] beanUnitPrices={5,10,15};
    }
    [Serializable] public sealed class CafeCatalog
    {
        public int startingMoney=500;
        public float daySeconds=300, customerPatience=180, customerInterval=18;
        public EntitySpec[] entities; public OperationSpec[] operations; public ProductSpec[] products;
        public RecipeSpec[] recipes;
        public InitialEntity[] layout; public AreaSpec[] areas;
        public NpcSpec[] npcs;
        public EconomySpec economy=new EconomySpec();
    }
    [Serializable] public sealed class ProcessingTask
    {
        public string inputId, operationId;
        public float remaining, cooldown;
        public int clicksDone;
        public MachinePhase phase;
    }
    [Serializable] public sealed class WorldEntity
    {
        public string id, specId, ownerId, npcId;
        public AreaId area;
        public float x, y, rotation;
        public int units=1, stock, quality;
        public bool latteArt;
        public List<Portion> contents=new List<Portion>();
        public ProcessingTask task;
    }
    [Serializable] public sealed class CustomerRequest { public string id, customerId, productId; public float patience, automaticRemaining=-1; }
    [Serializable] public sealed class CafeSnapshot
    {
        public int version=2, nextId=1, money, reputation, day=1, served, requestSequence;
        public int todayServed, todayFailed, todayRevenue, todayArrivals, todayCustomerBudget, skillPoints, rewardedSkillMilestones, positiveReputationEarned;
        public int seats=3, staff, beanQuality, staffWagesPaidDay;
        public uint randomState=1234567;
        public bool open, automaticOrders, automaticMachines, automaticLatteArt, automaticCocoa, automaticDelivery;
        public BusinessPhase businessPhase;
        public float timeRemaining, arrivalRemaining;
        public AreaId currentArea=AreaId.Cafe;
        public string focusedRequestId, lastSummary;
        public List<WorldEntity> entities=new List<WorldEntity>();
        public List<CustomerRequest> requests=new List<CustomerRequest>();
        public List<string> unlockedProducts=new List<string>(), unlockedNpcs=new List<string>(), topics=new List<string>(), spokenCustomers=new List<string>();
        public List<string> fasterMachines=new List<string>(), betterProducts=new List<string>(), skillMachines=new List<string>();
    }
    public readonly struct ActionResult
    {
        public readonly bool success; public readonly string message;
        public ActionResult(bool success, string message) { this.success=success; this.message=message; }
        public static ActionResult Yes(string message) => new ActionResult(true,message);
        public static ActionResult No(string message) => new ActionResult(false,message);
    }
}
