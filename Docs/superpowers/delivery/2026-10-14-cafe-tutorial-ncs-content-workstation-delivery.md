# 咖啡馆教程 NCS 内容与操作台入口交付记录

依据：`Docs/superpowers/specs/2026-10-14-cafe-tutorial-ncs-content-workstation-spec.md`。本次未纳入首日开局 Spec；未实现操作台打开后的咖啡制作玩法、蒙版或步骤引导。

## 修改内容

- 源配置：`Assets/ConfigTables/Data/Narrative/Narrative.xlsx`、`Assets/ConfigTables/Data/Shared/Text.xlsx`，新增 25 行 `CafeTutorialStep` 步骤定义；重配 `OpeningPresentation` / `OpeningLine`，使用既有 `first_day_cafe` 作为唯一正式入口；新增原稿段落文本和原稿归档 key。
- Schema / 生成：`Assets/ConfigTables/Defines/schema.xml` 增加 `CafeTutorialStep`、OpeningPresentation 的可选中心角色名 key，并允许 OpeningLine 的 `center` speaker。通过 `Assets/ConfigTables/GenerateAll.bat` 从 Excel 生成 `Assets/Scripts/Config/Generated/` 和 `Assets/Settings/Resources/Luban/`；没有直接编辑生成 JSON。
- 可重建源工具：`Assets/ConfigTables/configure_cafe_tutorial.py`，从 docx 按段落索引导入文本、生成演出和步骤行；保留完整原稿段落归档。重跑后已重新生成并验证。
- Runtime：`Assets/Scripts/Narrative/CafeTutorialStepDefinition.cs`、`NarrativeConfiguration.cs`、`LubanNarrativeLoader.cs` 加载并查询教程步骤；`NarrativePresentationDataAdapter.cs` 和 `NarrativeDefinitions.cs` 支持第三角色显示名及 center speaker。
- 流程/门禁：`Assets/Scripts/GameFlow/UI/DayFlowController.cs` 在正式开场后播放顾客到店、沿用已有 NCS `first_day_perception`（属性 `perception` >= 3）分支并播放汇合文本。咖啡馆经营入口不启动普通自由经营按钮；`Assets/Scripts/FreeCafe/Unity/FreeCafeRuntime.cs` 仅在待操作台阶段接受 `AreaId.Counter`，拒绝吧台实体点击、其他楼层/区域切换和操作。打开 Counter 区域视图成功后回报稳定信号。
- 测试：`Assets/Scripts/Narrative/Tests/CafeTutorialContentTests.cs`，检查生成内容、分支/评价段落存在，以及当前工作台信号只接受一次。

## 稳定 ID 与内容映射

- 教程 ID：`first_cafe_tutorial`。
- 正式开场 ID：`first_day_cafe`；文本 key `opening.line.01` 起（当前 8 句演出行；底层文本也以 `cafe.tutorial.source.pNNN` 保存完整段落原稿）。角色为 player / adele。
- 到店/分支/汇合/入口：`customer_arrives`、`perception_success`、`perception_failure`、`order_confirmed`、`workstation_open`，对应演出 ID `cafe_tutorial.<stepId>`。
- 后续定义：`make_coffee_base`、`pour_into_cup`、`adjust_strength`、`adjust_sweetness`、`demo_grind`、`demo_boil`、`demo_brew`、`demo_cup`、`demo_base`、`demo_strength`、`demo_sweetness`、`demo_serve`、`serve_customer`；可选演示条目标记 `optionalDemo=true`。
- 五档结果：`result_excellent`、`result_good`、`result_average`、`result_bad`、`result_refund`；结尾 `ending`。退款结果的 nextStepId 指回 `make_coffee_base`，仅为剧情内容元数据，不实现重做玩法。
- 每段正文 key 形如 `cafe.tutorial.<stepId>.<两位序号>`，标题 key 形如 `cafe.tutorial.<stepId>.title`；原稿逐段完整归档 key 为 `cafe.tutorial.source.pNNN`。
- 目前实际发出的唯一玩法完成信号：tutorialId `first_cafe_tutorial` + stepId/signal `workstation_opened`。定义表中对应内容步骤为 `workstation_open`，完成信号 ID 为 `workstation_opened`，焦点 key `cafe.workstation`。

## 接入方式与限制位置

- 区域限制在 `FreeCafeRuntime.Enter(AreaId)` 和 `FreeCafeRuntime.Click(FreeEntityView)`；待操作台期间只允许区域入口将视图切到 `AreaId.Counter`，不进入 `World.EnterProduction()`，不启动制作判定。
- 打开成功后 `FreeCafeRuntime.NotifyTutorialStepCompleted("first_cafe_tutorial", "workstation_opened")` 发信号；监听 `FreeCafeRuntime.CafeTutorialStepCompleted`，事件参数依次是 tutorialId、stepId。调用方可经 `TryGetCafeTutorialStep(tutorialId, stepId, out CafeTutorialStepDefinition)` 读取定义，字段含 tutorialId、stepId、presentationId、focusTargetKey、completionSignalId、nextStepId、optionalDemo。
- Session 忽略不匹配、过期、重复完成通知。后续步骤定义仅供读取，不代表已启用或自动推进。
- 现场目录没有独立 `workstation` WorldObject；实际可操作入口是现有 `FreeCafePortal` 到 `AreaId.Counter` 的区域切换。因此信号在 Counter 区域焦点切换成功后发送；不是基于设备动画/制作状态。

## 原稿覆盖及问题记录

- 已整理开场介绍、老太太到店与对白、成功/失败支线、汇合、顾客订单/第一杯说明、阿黛尔制作讲解、研磨/烧水/冲泡/装杯/基底/浓度/甜度/出餐演示名、五档评价及对应奖励文字、顾客离场、阿黛尔收尾、玩家独白和黑场前文本。完整原稿每个非空段落都保留在归档 key，舞台说明不因整理对白而丢失。
- 原稿 B 顾客完整订单配料说明在文档文本中重复；原文保留，未静默删重。
- 原稿“平时会更长修剪下树的枝蔓”等表述疑似错字/语病；未改写，需文案负责人确认。
- 五档评价仅提供评价与钱数，没有评分判定规则、具体数值阈值；本次只配置文字，不配置评分逻辑。
- 原稿提到顾客到店、点单及演出，但本次没有启用实体顾客生成、实际订单演出；这些内容仅作为 NCS 文案准备。
- 原稿提到制作各步骤“操作动画说明”，却未给出每项的详细逐句讲解。演示段以原稿列出的研磨等名称存入可选 NCS 段，不杜撰操作教学台词。
- 首段当前 8 行，是原稿第一段按说话人/引号切分的结果；旧 13 行临时测试台词已被替换。

## 验证结果

- `Assets/ConfigTables/GenerateAll.bat`：成功，Luban strict 校验通过，Player semantic validation passed。
- Unity 刷新资源后执行 `LubanNarrativeLoader.Load()`：成功，返回 8 条 `first_day_cafe` opening lines 和 25 个教程步骤定义；已有 `first_day_perception` / perception >= 3 条件可读。
- EditMode `Spotlight.Narrative.Tests.CafeTutorialContentTests`：2/2 通过。
- Unity 编译及 domain reload：成功。
- 未执行 CoffeeShop 场景 PlayMode 点击回归：场景未处于已加载状态；当前测试只覆盖 NCS 配置和信号幂等。需在 CoffeeShop 正式运行流程验证 Counter 入口碰撞/点击，尤其确认场景使用的生产模式和区域 portal 设置。

## 故障复盘与本次修复（2026-10-14）

- 复现后的运行态：`DayFlowState.FirstDayIntroduction`，NCS `busy=false`、`ValuesReady=false`、`valueRestore=true`，safe callback 队列里留有一次待执行回调，DayFlow 等待 perception branch。检定 event 从未入队，故没有结果说明 UI。
- 首个原因：新游戏调用 `TrySubmitPlayerCreation(null)` 时咖啡场景 owner 尚未创建，提交请求被保存为 `pendingDefaultCreation`；`FreeCafeRuntime.Awake` 创建 `CafeWorld` 后此前未调用 `PlayerCafeOwner.Bind(World)`。因此默认角色创建没有随后重试，NCS 没有 player 属性 provider，`CompleteNarrativeRestore` 早退，safe boundary 永远无法成立。修复是在 FreeCafe runtime 构建 World 后立即绑定 `PlayerCafeOwner`，使场景加载后待提交玩家能绑定并完成恢复。
- 另一个直接表现原因：`GameFlowBootstrap.Update` 原来用 `CanUseGameplayControls()` 决定 NCS Presenter suspended 状态；该条件在 `FirstDayIntroduction` 必为 false，故即使值恢复后检定能排队，检定说明 UI 也不可见/不可点击。修复新增 `CanUseNarrativePresentation()`：仅在咖啡教程仍活跃、DayFlow 正在 `FirstDayIntroduction`、没有暂停/切图/其它 DialoguePanel 时允许 NCS 演出；Gameplay controls 判定和咖啡制作门禁未放开。
- 曾对当前 Play Mode 运行态作临时诊断补救：绑定当时已存在的 CafeWorld 并补提交默认玩家后，观测到 `ValuesReady=true`、检定事件进入 active presentation、`first_day_perception` 快照 passed=true。随后为编译 Bootstrap 的冷更改切到 Edit Mode；回到 Play 后 Unity 回到了 `启动场景`，故没有声称亲眼验证修复后的检定 UI交互或操作台点击。
- 编译：Unity incremental status `up_to_date`，运行时反射可见 `CanUseNarrativePresentation` 新方法；专项 CafeTutorialContentTests 在本故障修复之前通过 2/2。需在正式新游戏流程做一次完整 PlayMode 回归，以验证属性提交 -> 可见检定说明/结果 -> 成功/失败支线 -> 汇合对白 -> Counter 操作台打开。

## 后续交接

后续玩法负责人可读取 `CafeTutorialStepDefinition` 来申请段落及焦点目标；完成真实玩法后调用 `NotifyTutorialStepCompleted`，但本次 Session 只接受 `workstation_opened`。需另行实现订单顾客表现、配方/步骤领域状态、评分、奖励、重做、制作完成通知及完整蒙版/引导；不可把本次预置内容误认为这些玩法已接通。首日信件、日期动画、地图流程和其他 DayFlow 行为未纳入变更。
