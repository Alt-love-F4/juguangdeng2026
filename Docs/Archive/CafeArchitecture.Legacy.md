# 咖啡店经营与后续印刷玩法的架构

## 本轮的范围

实现的是单人、单个制作中订单的完整经营闭环：营业 → NPC交流/接单 → 取杯 → 拿取原料 → 放置/加工 → 真实小游戏 → 上餐 → 收入 → 补货/升级 → 日结 → 下一天。
本轮没有实现夜间报纸编辑、热点词条收集、NPC关系系统、员工AI或多人并行订单。它们不是假装已完成的空面板。

## 怎么运行

打开 Assets/Scenes/CoffeeShop.unity，进入 Play Mode。先点开始营业，再点击右侧顾客。
从左侧小杯/大杯堆拖一个杯子到工作台；订单会提示杯型。将上方原料拖入当前订单杯子。
根据步骤把杯子移到对应设备。研磨需要点击，定时加工期间杯子不能拿走。筛豆、可可搅拌、拉花均有实际小游戏。
最后把杯子拖到右侧出餐口。下方辅助按钮与世界交互走相同规则，不是第二套玩法。
库存面板可以补货，设备面板可以升级。可以提前打烊或等5分钟营业结束；下一天保留资金、库存和升级。
Esc/暂停按钮暂停。正常从主菜单进入时仍使用原 GameFlow；直接运行咖啡店时有沙盒暂停和快捷存档。

## 一张图理解职责

输入适配器（世界鼠标/未来UI拖拽）
    → IInteractionRules（数据化操作目标，不依赖 Sprite 或 Canvas）
    → 玩法规则提供者（CoffeeWorldRuntime / 未来 PrintingRules）
    → 配置定义 + 运行时状态
    → 结果与画面更新

## 1. 通用实体层：Assets/Scripts/Interaction

InteractiveEntity 是小的交互基类，统一对象标识、悬停、点击与拖拽入口。
ItemSourceEntity 是杯堆、原料堆：它留在原地，拖出临时物品。
CarryableEntity 是独立杯子/原料份：拥有唯一实例ID。
PlacementEntity 是工作台、设备或其他接收点。
NpcEntity 是可以交流的实体；不是所有实体都应该能放进背包。

定义ID描述“哪一种物品”，实例ID描述“具体哪一个物品”。源对象不是物品实例。
PlacementLedger 是与场景、鼠标、经济规则无关的纯状态模型。它记录物品、所在位置、占用、内容、关联ID与可选业务载荷。
InteractionTarget 只有定义和实例状态。未来报纸是 Canvas 时，用UI输入适配器构造同样的数据目标即可；不需要给UI挂 Collider2D。
InteractionWorld 只负责当前世界空间拖拽。它不是所有未来输入系统的万能父类。

## 2. 咖啡店规则层：CoffeeWorldRuntime + CoffeeGameplayController

CoffeeWorldRuntime 将来源、杯子、设备和放置点组合成咖啡店规则。它判断能否取物、目标是否接收、库存/杯型/配方/容量是否允许。
CoffeeGameplayController 组织营业、订单、加工时间、结算、存档，并暴露 NPC交互及日结事件。
CraftingSession 解释 RecipeStep 表，不认识场景物体名，也不负责鼠标动画。
显示层只表现结果，不自行扣库存或发金币。

成功放置才确认取出。取消临时拿取不扣库存。原料先放到工作台会扣一次库存，随后移入杯子不会重复扣除。闲置原料收回时返还库存。
搬动已有物品保留实例ID；非法目标/占用目标不会改变原归属。加工中的当前杯子不能搬动。
当前制作中的杯子绑定订单；其他空杯可以预先放置，但不能代替当前杯子完成制作。

## 3. 配表的唯一来源

Assets/ConfigTables/Data/WorldObject.xlsx：实体角色、来源物品、标签、允许接收标签、行为ID、美术路径、初始布局和容量。
Recipe.xlsx：售价、杯型、声望解锁等配方元数据。steps 字段已清空，保留只为兼容旧数据。
RecipeStep.xlsx：配方ID、顺序、操作、原料或目标、设备与数量。当前7种饮品共54个步骤。
Ingredient.xlsx：库存和补货成本。
Device.xlsx：耗时与升级。
GameParam.xlsx：营业时间、客流、初始资金等。
Npc.xlsx：稳定NPC ID、名字、社交ID、外观、解锁与初始台词。

本项目沿用等效Luban Python生成器，XML schema同步声明上述表；目前并非Luban生成的强类型C#代码。
先运行生成器的 --validate；默认生成模式先校验全部表，再输出JSON，避免错误表部分发布。
未知步骤、重复ID、无效来源/原料/设备引用、缺失图片、非连续步骤会报错，不会静默忽略。
JSON是生成结果；不要同时手改Excel和JSON。金额和耗时不在第二份 ScriptableObject 中重复维护。

生成：python Assets/ConfigTables/generate_tables.py
检查：python Assets/ConfigTables/generate_tables.py --validate
装配：Unity菜单 Tools/Spotlight/Validate Cafe Tables，再 Tools/Spotlight/Build Cafe Workspace。
装配仅在编辑模式、CoffeeShop场景执行；生成预制体及场景实例。原来的世界对象和旧HUD保留为禁用迁移参考，背景保留。

## 4. 预制体与存档

Assets/Resources/Interaction/Items：独立物品预制体。
Assets/Resources/Interaction/Entities：来源、放置点、设备、NPC预制体。
CoffeeWorkspace.prefab：整套物理交互场景。
CafeHUD.prefab：经营界面、补货/升级行、三个小游戏。

gameplay 存档参与者保存金币、库存、升级、日序、营业剩余时间、订单、制作步骤、品质和设备加工进度。
world.placements 参与者保存物品身份、放置点、杯内内容和订单关联。
读档保留制作中的实际杯子及机器位置。小游戏的画布/筛豆布局不是档案数据，读档后从该步骤重新打开小游戏。
旧存档缺失世界参与者时仍加载档案；旧存档没有保存制作过程，不能恢复此前未保存的杯子。

## 5. 后续如何接入白天热点与夜间印刷

### NPC交流与热点

Npc.xlsx 的稳定ID是起点。后续新增 NpcRuntimeState（好感、已见台词、事件进度），不要把状态写回NPC定义。
订阅 CoffeeGameplayController.NpcInteracted，按NPC、当天事件、对话选择判断是否获得词条。
新增 Topic.xlsx 定义标题、标签、事实来源、解锁条件；TopicInstance 保存本次取得的来源NPC、日期、可信度和是否已使用。
“拿到词条”不等于删除NPC，也不等于复制一份咖啡原料库存。它是叙事系统生成新的内容物品。

### 报纸编辑

报纸版面定义接收 topic 标签的放置点；词条是可携带实例。
用 PrintingRules 实现 IInteractionRules：检查是否已使用、栏位是否被占用、版面规则、重复词条、稿件长度等。
Canvas报纸用新的UI拖拽适配器，复用 PlacementLedger 和数据目标；世界空间报纸可以直接复用现有 InteractionWorld 输入适配器。
不要让 CoffeeWorldRuntime 知道报纸逻辑，也不要在 InteractiveEntity 里增加“如果是报纸”的分支。
印刷按钮根据已经确认的版面状态生成 IssueSnapshot，印刷完成后再触发声望、NPC关系与第二天热点变化。
词条移动、报纸内容保存与撤回可以复用实例ID和占用模型；印刷消耗与叙事后果是新的业务规则。

### 昼夜流程

当前 DayEnded 是营业结束边界。后续由新的日程协调器接管“咖啡营业 → 夜间编辑 → 印刷结算 → 下一天”。
它调用现有的营业服务，不把夜间编辑塞进咖啡控制器。NPC关系与报纸发行作为各自存档参与者注册。

## 不需要改代码的扩展 / 需要改代码的扩展

普通新原料、同类来源、同类放置点、使用已有操作的新配方：改表、美术/预制体、重新装配。
全新机制（发酵、报纸排版规则、NPC对话选择、印刷后果）：实现新的业务规则，然后将其参数开放给配表。
不要用字符串反射任意执行代码；有限操作类型和明确提供者就是边界。

## 验证

EditMode：放置事务、错误目标、无效存档、实例身份、库存一次性扣除、错误原料、配方定义、NPC身份、技能点阈值。
PlayMode：7种饮品、真正的来源拖出/取消、3个小游戏、加工中读档、补货、升级、日切换、暂停。
详细测试输出保留在 Temp/CafeVerification；这不是美术和发行版本质量保证。
