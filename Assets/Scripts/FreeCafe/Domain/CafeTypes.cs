using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    /// <summary>实体能力标记；用位运算描述一个规格「能做哪些事」。</summary>
    [Flags]
    public enum Capability
    {
        None = 0,        // 无能力
        Movable = 1,     // 可被玩家搬动
        Source = 2,      // 物品来源（豆仓、牛奶柜等），可从中取出物品
        Portion = 4,     // 单份原料，可直接倒进容器
        Container = 8,   // 容器，可以容纳多份原料
        Processor = 16,  // 加工设备，可以执行工序
        Customer = 32,   // 顾客，可以接收餐品
        Delivery = 64,   // 出餐口
        Discard = 128    // 丢弃区
    }

    /// <summary>区域编号。</summary>
    public enum AreaId
    {
        Cafe,     // 一楼营业区
        Counter,  // 备料台/吧台
        TopFloor, // 顶层
        Basement  // 地下室
    }

    // 数值保留 v1 存档的旧含义；Loaded 表示已装料、等待玩家手动操作。
    /// <summary>设备加工阶段。</summary>
    public enum MachinePhase
    {
        Idle = 0,    // 空闲
        Running = 1, // 自动走时间中
        Ready = 2,   // 产物已就绪，等待接取
        Loaded = 3   // 已装料，等待点击/QTE 等手动操作
    }

    /// <summary>工序的操作方式。</summary>
    public enum OperationControl
    {
        Timer,  // 定时自动完成
        Clicks, // 需要连续点击若干次
        Qte     // 需要节奏点击（失败可重试）
    }

    /// <summary>经营阶段。</summary>
    public enum BusinessPhase
    {
        Preparation, // 营业前的准备阶段
        Open,        // 营业中
        Night        // 夜间（打烊结算后，可升级招募）
    }

    /// <summary>实体规格：设备、原料来源、杯子、顾客等所有世界物体的静态配置。</summary>
    [Serializable]
    public sealed class EntitySpec
    {
        public string id;           // 规格 id
        public string label;        // 显示名称
        public string artId;        // 美术资源 id
        public string sourceItemId; // 来源可取出的物品规格 id（仅 Source 使用）
        public Capability capabilities; // 能力标记
        public float capacityMl;    // 制作模式的液体容量；份数容量保留给旧模式
        public int capacity;        // 容器容量（份数）
        public int initialStock;    // 来源的初始/满库存
        public int refillCost;      // 一键补货的固定费用
        public int unitCost;        // 按单位购买时的单价（0 表示用固定补货费）
        public bool dragOutput;     // 产物是否允许「拖动设备」直接取出（仅加工设备可开启）
        public float width = 1;     // 摆放宽度（用于边界判定）
        public float height = 1;    // 摆放高度（用于边界判定）

        /// <summary>是否同时具备给定的全部能力。</summary>
        public bool Has(Capability value) => (capabilities & value) == value;
    }

    /// <summary>工序规格：某台设备处理某种输入时的一条加工规则。</summary>
    [Serializable]
    public sealed class OperationSpec
    {
        public string id;            // 工序 id
        public string label;         // 工序显示名（如「研磨」）
        public string machineSpecId; // 所属设备规格
        public string inputSpecId;   // 输入原料规格
        public string outputSpecId;  // 输出产物规格
        public float seconds = 0;    // 基础加工耗时（秒）
        public float clickInterval = .5f; // 点击类工序的最小点击间隔
        public int clicks = 3;       // 点击类工序需要的点击数
        public OperationControl control; // 操作方式
        public bool acceptsContainer = true; // 是否接受从容器内容物中取一份投入
    }

    /// <summary>容器里的一份原料（同规格同品质会合并份数）。</summary>
    [Serializable]
    public sealed class Portion
    {
        public string specId; // 原料规格 id
        public int units;     // 份数
        public int quality;   // 品质（0~2，仅咖啡豆等有品质的物品使用）
        public float milliliters;
        public LiquidTemperature temperature;
        public float productionScore = 100;
        public int addedSequence;
    }

    /// <summary>餐品规格：配方、价格与解锁条件。</summary>
    [Serializable]
    public sealed class ProductSpec
    {
        public string id;      // 餐品 id
        public string label;   // 餐品显示名
        public string cupSpecId; // 指定杯型
        public string recipeId;  // 显式指定的制作流程 id（为空则按餐品 id 查找）
        public string description;
        public string tutorial;
        public float idealCoffeeRatio = .5f;
        public float idealSweetness;
        public LiquidTemperature requiredTemperature = LiquidTemperature.Boiling;
        public float minimumScore = 60;
        public int maxRemakes = 1;
        public int price;        // 基础售价
        public int unlockReputation; // 解锁所需声望
        public int qualityPriceStep = 50; // 咖啡品质每高一级的加价
        public bool requiresLatteArt;     // 是否必须完成拉花
        public Portion[] ingredients;     // 配方原料（份数需与杯子容量匹配）
    }

    /// <summary>制作流程中的单个步骤（用于 UI 展示指引）。</summary>
    [Serializable]
    public sealed class RecipeStepSpec
    {
        public string id;            // 步骤 id
        public string kind;          // 步骤类型
        public string label;         // 步骤文案
        public string inputSpecId;   // 输入规格（可选）
        public string outputSpecId;  // 输出规格（可选）
        public string targetSpecId;  // 目标规格（可选）
        public string operationId;   // 关联工序 id（可选）
        public int units = 1;        // 涉及份数
        public float seconds;        // 参考耗时
    }

    /// <summary>制作流程：一组步骤，绑定到某个餐品。</summary>
    [Serializable]
    public sealed class RecipeSpec
    {
        public string id;        // 流程 id
        public string productId; // 绑定餐品
        public string label;     // 流程显示名
        public RecipeStepSpec[] steps; // 步骤列表
    }

    /// <summary>初始布局条目：世界开局时摆在某个位置的一个实体。</summary>
    [Serializable]
    public sealed class InitialEntity
    {
        public string id;     // 实体 id
        public string specId; // 规格 id
        public AreaId area;   // 所在区域
        public float x;       // 坐标 x
        public float y;       // 坐标 y
    }

    /// <summary>区域规格：可用摆放范围与背景资源。</summary>
    [Serializable]
    public sealed class AreaSpec
    {
        public AreaId id;       // 区域编号
        public string label;    // 显示名
        public string artId;    // 背景美术 id
        public float halfWidth = 9;  // 半宽（摆放边界）
        public float halfHeight = 5; // 半高（摆放边界）
    }

    /// <summary>NPC 规格：满足条件后会在顾客中出现。</summary>
    [Serializable]
    public sealed class NpcSpec
    {
        public string id;       // NPC id
        public string name;     // 名字
        public string dialogue; // 对话内容
        public string topic;    // 首次对话给玩家的热点词条
        public int unlockReputation;   // 解锁声望门槛
        public int minimumBeanQuality; // 出场所需的最低咖啡豆品质
    }

    /// <summary>经济参数：客流、罚款、招募/扩建/升级价格与各类倍率。</summary>
    [Serializable]
    public sealed class EconomySpec
    {
        public int baseCustomers = 12;       // 每日基础客流
        public int customerGrowthEvery = 30; // 每累计服务多少位顾客，客流 +1
        public int maxCustomers = 24;        // 每日客流上限
        public int initialSeats = 3;         // 初始卡座数
        public int wastePenalty = 10;        // 丢弃原料的扣款
        public int wrongOrderPenalty = 10;   // 交付错误订单的扣款
        public int hireCost = 200;           // 招募店员费用
        public int expansionCost = 200;      // 扩建卡座费用
        public int extraMachineCost = 200;   // 新增设备费用
        public int upgradeCost = 100;        // 通用升级费用
        public int seatsPerExpansion = 2;    // 每次扩建增加的卡座数
        public int maxStaff = 4;             // 店员上限
        public int maxMachineCopies = 3;     // 同类设备数量上限
        public float autoSeconds = 20;       // 自动点单的等待秒数
        public float autoChance = .2f;       // 自动点单的出现概率
        public float staffSpeedPerHire = .1f;      // 每名店员提供的设备加速
        public float timerUpgradeMultiplier = .5f; // 金钱升级后的耗时倍率
        public float priceUpgradeMultiplier = 1.5f; // 售价升级后的价格倍率
        public int[] beanUnitPrices = { 5, 10, 15 }; // 三档咖啡豆单价
    }

    /// <summary>总目录：所有静态配置的集合，由 JSON 反序列化而来。</summary>
    [Serializable]
    public sealed class CafeCatalog
    {
        public int startingMoney = 500;      // 启动资金
        public float daySeconds = 300;       // 每天营业时长（秒）
        public float customerPatience = 180; // 顾客耐心（秒）
        public float customerInterval = 18;  // 顾客到店间隔（秒）
        public EntitySpec[] entities;        // 全部实体规格
        public OperationSpec[] operations;   // 全部工序规格
        public ProductSpec[] products;       // 全部餐品规格
        public RecipeSpec[] recipes;         // 制作流程（可选）
        public InitialEntity[] layout;       // 初始布局
        public AreaSpec[] areas;             // 区域配置
        public NpcSpec[] npcs;               // NPC 配置（可选）
        public ProductionSettings production = new ProductionSettings();
        public EconomySpec economy = new EconomySpec(); // 经济参数
    }

    /// <summary>设备当前加工任务。</summary>
    [Serializable]
    public sealed class ProcessingTask
    {
        public string inputId;     // 正在加工的材料实体 id
        public string operationId; // 当前工序 id
        public float remaining;    // 剩余加工时间
        public float cooldown;     // 点击间隔冷却
        public int clicksDone;     // 已完成的点击数
        public MachinePhase phase; // 加工阶段
    }

    /// <summary>世界实体：场景中的每一个物品、设备或顾客。</summary>
    [Serializable]
    public sealed class WorldEntity
    {
        public string id;     // 实体 id（free_ / order_ 前缀由分配器生成）
        public string specId; // 规格 id
        public string ownerId; // 若材料被锁在设备内，这里是设备 id
        public string npcId;  // 顾客对应的 NPC id（普通顾客为空）
        public AreaId area;   // 所在区域
        public float x;       // 坐标 x
        public float y;       // 坐标 y
        public float rotation; // 朝向角度
        public int units = 1;  // 作为单份原料时的份数
        public int stock;      // 作为来源时的剩余库存
        public int quality;    // 品质（0~2）
        public bool latteArt;  // 是否已完成拉花
        public List<Portion> contents = new List<Portion>(); // 容器内容物
        public ProductionEntityState production = new ProductionEntityState();
        public ProcessingTask task; // 设备加工任务（非设备为空）
    }

    /// <summary>顾客订单。</summary>
    [Serializable]
    public sealed class CustomerRequest
    {
        public string id;         // 订单 id
        public string customerId; // 下单顾客的实体 id
        public string productId;  // 想要的餐品
        public int remakes;
        public float lastProductionScore;
        public float patience;    // 剩余耐心（秒）
        public float automaticRemaining = -1; // 自动点单剩余时间，负数表示非自动点单
    }

    /// <summary>世界快照：存档 / 读档与视图刷新所依赖的完整运行时状态。</summary>
    [Serializable]
    public sealed class CafeSnapshot
    {
        public ProductionSession production = new ProductionSession();
        public int version = 2; // 存档版本（v1 在 Restore 时显式迁移）
        public int nextId = 1;  // 自增 id 分配器
        public int money;       // 当前金币
        public int reputation;  // 当前声望
        public int day = 1;     // 第几天
        public int served;      // 累计完成订单数
        public int requestSequence; // 订单序号（用于轮换餐品与 NPC）
        public int todayServed;  // 今日成功单数
        public int todayFailed;  // 今日失败/未完成单数
        public int todayRevenue; // 今日收入
        public int todayArrivals; // 今日已到店顾客数
        public int todayCustomerBudget; // 今日客流上限
        public int skillPoints;  // 可用技能点
        public int rewardedSkillMilestones; // 已发放的技能点里程碑，避免重复发
        public int positiveReputationEarned; // 累计获得的正向声望
        public int seats = 3;    // 当前卡座数
        public int staff;        // 已招募店员数
        public int beanQuality;  // 当前咖啡豆品质（0~2）
        public int staffWagesPaidDay; // 工资已结算到的天数
        public uint randomState = 1234567; // 随机数状态（随存档走，保证可复现）
        public bool open;               // 是否营业中
        public bool automaticOrders;    // 店员能力：自动点单
        public bool automaticMachines;  // 店员能力：设备自动加工
        public bool automaticLatteArt;  // 店员能力：自动拉花
        public bool automaticCocoa;     // 店员能力：自动处理可可 QTE
        public bool automaticDelivery;  // 店员能力：自动交付
        public BusinessPhase businessPhase; // 当前经营阶段
        public float timeRemaining;    // 今日剩余营业时间
        public float arrivalRemaining; // 下一位顾客到店的倒计时
        public AreaId currentArea = AreaId.Cafe; // 当前所在区域
        public string focusedRequestId; // 当前聚焦的订单 id
        public string lastSummary;      // 最近一次打烊结算文案
        public List<WorldEntity> entities = new List<WorldEntity>(); // 全部世界实体
        public List<CustomerRequest> requests = new List<CustomerRequest>(); // 当前订单
        public List<string> unlockedProducts = new List<string>(); // 已解锁餐品
        public List<string> unlockedNpcs = new List<string>();     // 已解锁 NPC
        public List<string> topics = new List<string>();           // 已获得的热点词条
        public List<string> spokenCustomers = new List<string>();  // 已对话过的顾客，避免重复给词条
        public List<string> fasterMachines = new List<string>();  // 已用金币升级效率的设备
        public List<string> betterProducts = new List<string>();  // 已升级售价的餐品
        public List<string> skillMachines = new List<string>();   // 已用技能点升级效率的设备
    }

    /// <summary>一次操作的结果：是否成功 + 给玩家看的提示文案。</summary>
    public readonly struct ActionResult
    {
        public readonly bool success;
        public readonly string message;

        public ActionResult(bool success, string message)
        {
            this.success = success;
            this.message = message;
        }

        /// <summary>成功结果。</summary>
        public static ActionResult Yes(string message) => new ActionResult(true, message);

        /// <summary>失败结果。</summary>
        public static ActionResult No(string message) => new ActionResult(false, message);
    }
}