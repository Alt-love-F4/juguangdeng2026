# 行动详情、地图入口与物品交易验收报告

日期：2026-10-16。依据：[统一 Spec](../specs/2026-10-16-action-details-inventory-integration-spec.md)。

状态：公共运行时、配置化导航、交易/交付恢复与卡片投影已交付；**不是 Spec 全量验收通过，也不代表正式经营原料商品可发布**。真实 Map/CoffeeShop、选定演出与 DayFlow 检查点回归已通过；扩展回归发现的四项失败已聚焦修复并通过复验。六项长文本实景检查发现模板禁用换行，已修复并加入通过的防回归断言。

按提速要求停止扩大回归。剩余为一项功能合同阻塞（经营原料适配）与四组验收缺口（完整日结入口、磁盘存档、更多分辨率/动效、GM 手动交互），详见文末。以下区分实际运行、源码覆盖和未验范围；本次没有写玩家存档。

## 实际运行证据

| 编号 | 运行或操作 | 实际结果与边界 |
| --- | --- | --- |
| E1 | 官方 `Assets/ConfigTables/GenerateAll.bat`，2026-10-16 23:34:04 最终生成 | exit 0；Unity 重新编译和 domain reload 通过。已显式导入 Item、Text、Action、MapPosition；真实 `LubanNarrativeLoader.Load()` 成功，actions=7、items=3，cafe 挂配置 Navigate。当前生成文件另可核对地点数为 9 |
| E2 | EditMode `9efcce9b37e947dbaf4cd296d2c77111`；GameFlow.Narrative + Narrative.Core | **502/502 通过**。这是该运行版本的通过基线；其后的 condition 通知延迟、缺图通用占位及 Play 夹具修正需最终重跑，不能把 502 当作最新完整工作树认证 |
| E3 | 真实 `CafeWorld` + `CafeNarrativeValueProvider` 内存 smoke | 初始金币 500；隔离交易 `Money(1)` + `ActionPoint(1)` → `ItemAdd(夹具物品,2)` 成功，余额 499。world.Changed 观察到物品、AP、回执均已提交，只通知 1 次；同 operationId 重试不再付款。CafeSnapshot 内存往返恢复余额 499。**未做磁盘保存或经营原料入库** |
| E4 | PlayMode `fd4478f5cf5946bbb43cea76354a317f` | 33 项，30 通过、3 失败；失败均来自 MapInitialization 的重复 provider 夹具，已修正。该次选定 PresentationDebugSceneTests、October07Handoff、S1Presentation、S2Sequence 均通过；本次历史运行本身不是全绿 |
| E5 | PlayMode `ba4848e952974cc4a2b3ec35948056f3` | 6 项，5 通过、1 失败。真实 Map→CoffeeShop 的 0AP/零资金导航，以及 S1→pendingFocus→玩家显式导航通过。失败为加载失败用例预期了 Exception，实际 Unity 原生日志为 Error；当次恢复断言尚未执行到。预期类型已修正，不能据该次失败用例宣称恢复通过 |
| E6 | 最终 PlayMode `4f7549f0606346698db2638f0c9079fc` | **36/36 通过**，主窗口已确认最终结果。包含真实 authored Map/CoffeeShop 导航、S1 聚焦交接、宿主加载失败连续两次恢复/重试，以及所选共享 PresentationDebug 与 Oct07 S1/S2 回归。夹具使用真实场景及正式控件，但 presenter/value owner 仍为受控内存注入，不等同整天真人游玩 |
| E7 | 扩展 EditMode `b1dc18af2cab4157816eb37325bf46a9` | **1120 项：1116 通过、4 失败**。包含最新 condition 通知延迟及 FreeCafe/Narrative.Editor；四项为角色目录旧数量/默认角色假设、分页可选 opts 被要求必填、旧 warning 总数断言。修复不改正文、角色标签或 blockedBy。此运行不是全绿，后续只复验受影响范围 |
| E8 | 修复后 EditMode `48fec5cb119d420b8047b8808d384c95` | **71/71 通过**：MapUiRepairTests、RuntimeCharacterTests、NarrativeConfigurationValidatorTests。包含正式 generated 数据严格空错误校验、可选槽可省略/必填仍拒绝、真实选定 NPC 身份与恢复，以及新增六格长文本强制换行/高度/执行按钮位置断言；不是重新宣称 1120 全量全绿 |
| E9 | PlayMode `3bbf18c199924802b22781f85c11f1b5` | **4/4 通过**：DayFlowCheckpointTests，覆盖真实 CafeWorld 开店→夜晚一次确认、恢复回执拒绝过早日结、取消后的下午恢复重试且不重开经营、早晨取消可恢复。不是地图/事项/咖啡馆每个 UI 入口都手动走完 |
| E10 | 1920×1080 实景六项长文本内存夹具 | 复用 authored Map、LocationActionPopup 和共享 PresentationDebugScene；三种长名物品 + Money + AP + OR 资格共六格。发现 NoWrap 导致溢出，临时切换 Normal 后检查描述与成本换行、滚动到底部所得及按钮。源码已固定 Normal 并隐藏固定位置的预览“需要”字幕，E8 防回归通过。临时夹具已清理，无源表/场景保存。截图是运行时修复预览，未补多分辨率最终画面；路径见下 |

E10 运行时预览截图（本地 Library 证据，不作为可发布美术）：顶部 `Library/Locus/Screenshots/locus_game_20261016_161021_334.png`，滚动到底 `Library/Locus/Screenshots/locus_game_20261016_161102_645.png`。截图仍含后来已隐藏的静态“需要”字幕；没有据此声称最终全部视觉验收完成。最终已返回 Edit Mode，恢复 `Assets/Scenes/NarrativeTest.unity`，Console 无 error/warn；`git diff --check` 通过（仅换行风格提示）。

## 实现与测试源码定位

下表每格首个路径相对仓库根目录，后续省略前缀的文件名沿用同格首路径的目录（S2 的 Editor 子路径亦如此）；矩阵中的 S/T 编号对应这里，避免将实现位置误当运行证据。

| 编号 | 源码位置 | 可核对职责 |
| --- | --- | --- |
| S1 | `Assets/ConfigTables/Data/Narrative/Narrative.xlsx`；`Assets/ConfigTables/Data/Shared/Text.xlsx`；`Assets/ConfigTables/Defines/schema.xml` | Action/MapPosition/Condition/Result/ActionMilestone/Item 与 Text 真源；官方生成入口为 GenerateAll.bat，拆分源表合并规则见 README |
| S2 | `Assets/Scripts/Narrative/NarrativeActionDefinitions.cs`；`LubanNarrativeLoader.cs`；`NarrativeItemCatalog.cs`；`Editor/NarrativeConfigurationValidator.cs` | 五种 executionKind、六类频次/修饰、成本 parser、引用/组合/展示上限/重复扣款/结果 owner/物品归属校验 |
| S3 | `Assets/Scripts/Narrative/NarrativeActionController.cs` | 挂载/稳定排序、出现与执行条件、状态投影、operationId/回执、交付、分阶段链、到期待办和快照恢复 |
| S4 | `Assets/Scripts/Narrative/NarrativeController.ActionTransactions.cs`；`NarrativeController.cs`；`NarrativeController.ActionExpiry.cs`；`NarrativeState.cs`；`NarrativeMapInventory.cs` | 独立交易批次、根成功提交边界、库存/资金/AP 预检与通知延迟、条件通知延迟、声明入口到期队列 |
| S5 | `Assets/Scripts/GameFlow/UI/MapActionDetailsView.cs`；`MapActionInventoryView.cs`；`MapActionNewContentDot.cs`；`MapTaskPanel.cs`；`MapSceneController.cs` | 原有卡片模板扩展、分项资格/成本/所得、动画/锁定/已读、库存元数据与缺图占位、事项和地点投影 |
| S6 | `Assets/Scripts/GameFlow/UI/DayFlowController.cs`；`GameFlowBootstrap.cs`；`CafeNarrativeValueProvider.cs`；`PlayerCafeOwner.cs`；`PlayerSessionCoordinator.cs`；`NarrativeSaveBridge.cs` | 正式导航宿主、加载失败恢复、必做检查、唯一经营账户及既有存档 owner 接口 |
| S7 | `Assets/Scripts/FreeCafe/Domain/CafeWorld.Money.cs`；`CafeWorld.cs`；`CafeWorld.Business.cs`；`CafeWorld.Saving.cs` | 真实资金 compare-and-commit/通知延迟；source.stock、Take、BuySource、CafeSnapshot 校验与恢复 |
| S8 | `Assets/Scripts/FreeCafe/Unity/NarrativeDebugGM.cs`；`Assets/Scripts/Narrative/NarrativeDebugSession.cs` | 既有 GM 内的独立行动检查会话、日期/余额/物品准备、失败模拟、执行/待办恢复及回执诊断 |
| T1 | `Assets/Scripts/Narrative/Tests/NarrativeActionTests.cs`；`NarrativeActionTransactionBoundaryTests.cs` | 频次/成本/取消/多根恢复、买卖换、拒绝/溢出/通知观察、交付待办/最终奖励重试、回执和迁移 |
| T2 | `Assets/Scripts/Narrative/Tests/LubanNarrativeLoaderTests.cs`；`NarrativeMapContentTests.cs`；`NarrativeDebugSessionTests.cs` | 配置门禁、六格展示计数/OR、Item 目录及 CafeSupply 拒绝、日期/可见性/已读、隔离调试 |
| T3 | `Assets/Scripts/GameFlow/UI/Tests/MapUiRepairTests.cs`；`MapActionPointsViewTests.cs`；`NarrativeSaveBridgeTests.cs`；`PlayerCafeOwnerTests.cs` | 配置导航/零成本/失效卡拒绝、替换/事项/布局、AP、存档桥及 owner 生命周期 |
| T4 | `Assets/Scripts/GameFlow/UI/Presentation/Tests/ActionNavigationSceneAcceptanceTests.cs`；`MapInitializationPlayModeTests.cs` | 加载 `Assets/Scenes/Map.unity`、真实 CoffeeShop 与共享调试场景；零成本防连点、宿主加载失败重试、S1 配置与 pendingFocus |
| T5 | `Assets/Scripts/GameFlow/UI/Presentation/Tests/PresentationDebugSceneTests.cs`；`October07HandoffTests.cs`；`October07S1PresentationTests.cs`；`October07S2SequenceTests.cs` | 共享 `Assets/Scenes/PresentationDebugScene.unity` 与选定 Oct07 演出/交接回归 |

生成物定位：`Assets/Scripts/Config/Generated/NcsActionEntry.cs`、`NcsItemEntry.cs` 与 `Assets/Settings/Resources/Luban/tbncsaction.json`、`tbncsitem.json`、`tbncsmapposition.json`、`tbtext.json`。生成物不是手工配置入口。完整填写合同见 [MapActionsREADME](../../../Assets/Scripts/Narrative/MapActionsREADME.md)。

## Spec 验收矩阵

“已通过”只覆盖列出的运行范围；“源码覆盖/待验”表示仍缺真实日流程或特定视觉证据。E7 已覆盖最新通知边界；E8 复验四项失败修复及新增长文本修复。按提速要求没有重复全量测试。

| Spec 场景 | 当前实现与对应测试 | 实际证据 / 验收状态 |
| --- | --- | --- |
| 配置接入与迁移校验 | S1/S2；T2 覆盖类型互斥、Navigate/EndDay 约束、结果 owner、必做可达、非法成本/溢出、循环、过期 MapAction 拒绝、目录引用及重复扣款 | **E1 已通过**官方生成→导入/编译→真实 loader；T2 有 E2 基线。不是全部正式内容已放行 |
| 进入咖啡馆 | `cafe.enter` 正式配置挂到权威 `cafe`，Navigate 白名单委托既有宿主；0AP/零资金可用、防连点、不写行动回执/剧情结果。失效实例/条件变化重新检查；market“大堂区”不映射 cafe。S3/S5/S6，T3/T4 | **E6 已通过**真实 Map→CoffeeShop、零成本且不开店、宿主失败两次恢复重试；T3 的新手/host 门禁属 E2 基线。完整新手日流程待验 |
| oct07 S1/S2 | S1 的 replacesLocationActions 仅遮同地点其他卡及导航；完成后 pendingFocus 回地图展开 cafe，由玩家点击进店，不自动开店。S3/S5/S6，T4/T5 | **E6 已通过**真实 S1 配置/受控 presenter 完成后的聚焦及显式进店，并通过选定 S1/S2/交接回归；不把受控 presenter 算作真人完整对白游玩 |
| 日期剧情 | 出现条件继续用 action；Event 根成功前取消零消耗，不写完成；成功链结束才算 Once。S2/S3/S4，T1/T2 | **E2 核心基线通过**；隔离配置测试可证能力。正式正文日期/前置与完整入口仍由内容 owner 逐条验收，不批量解除门禁 |
| 显示、资格、成本分离 | action 控制隐藏，actionExecute 失败仍展示；同组 AND/跨组 OR，资格一格准确解释，重复成本合并；动画后重验，缺 provider 显示暂不可用。S2/S3/S5，T1/T2/T3 | **E2 逻辑基线通过**；当前源码具六格投影。condition 通知延迟已由 E7 覆盖；六项长中文与滚动实景检查见 E10，换行/按钮布局修复由 E8 复验。缺图交互与全部动效仍未完整目视验收 |
| 六类与组合/到期 | Repeat/Daily/Once + Timed/Progress，Future 替换；到期与成功事件分离，day3+d2 在 day5 到期；due/cursor 可恢复，按事件声明阶段排普通队列并尊重前置/blockedBy。S2/S3/S4，T1/T2 | **E2 核心基线通过**，含 `CombinedDailyProgressTriggersOnlyAtTarget`（实际同时填 durationDays）、跳日/重复刷新/快照等源码覆盖。Future 没有独立 operationId 回执，不能写成 Spec 全部回执保证已完成；正式跨阶段恢复待验 |
| 交易买/卖/换 | Exchange 独立结果组，不造假 Event；成本先预检，所得不垫付输入；库存/资金/AP/次数/回执同提交，支持同 operationId 幂等。S3/S4/S6/S7，T1 的 ExchangeCommitsFixedCostsAndOutputsAndReceiptSurvivesRestore、溢出/拒绝/补偿用例 | **E2 核心基线 + E3 真实账户内存 smoke 通过**；真实 world 观察不到半笔交易，重复 op 不再扣。未发布正式商品，任意外部 provider 故障与磁盘恢复不能据此宣称通过 |
| 经营原料采购→经营取用→恢复 | 当前 Item 仅支持 MapInventory；CafeSupply 明确拒绝。FreeCafe BuySource 是按实际 source 缺量补至 initialStock，涉及质量/价格和实体 owner；地图库存不是该库存。S2/S7，T2 | **合同阻塞、未交付**。没有获批 itemKey→source/单位/质量/容量/交接策略，没有可发布经营原料；E3 只证明账户，不证明补货 |
| 累计/连续交付 | 每日一次、漏日重置连续进度不退旧成本；里程碑只记一次。提交后排待办，取消/拒绝/恢复只续事件或独立奖励，不再次收费或计进度；最终结果成功前不算整项完成。S3/S4，T1 | **E2 核心基线通过**；T1 包含末根独立 owner、空 pendingEvents 的失败奖励恢复、通知观察/保存门禁。真实 DayFlow 多日交付未验 |
| 顺序事件链 | 成本并入首个成功根；首根前取消零消耗；前根成功后保留游标，只重试剩余根；successResultOwnerId 在最终真实根提交。S3/S4，T1 | **E2 核心基线通过**；不是全链原子事务。最新 condition 通知延迟已由 E7 覆盖；真实跨场景/自动存档恢复待验 |
| 必做与所有日结入口 | HasBlockingActions 与稳定投影供日结和地图聚焦；零 AP 必做不进事项，带 AP 非导航行动进入事项；替换不封其他地点。S3/S5/S6，T1/T2/T3/T5 | **E2 逻辑、E6 选定 Oct07、E9 DayFlow 检查点通过**；地图/事项/咖啡馆全部 UI 日结入口逐一阻止→聚焦→完成放行仍待验 |
| 红点/物品栏/卡片表现 | 只在 ConfirmActionDisplayed 且实例有效/实际可见时已读；余额变化不制造新卡；名称描述查 Item 目录，正数量显示；金币投影唯一账户。分项资格/成本/所得、滚动/动画、缺图“物”字占位。S3/S5，T2/T3 | **E2 状态/布局基线、E6 共享演出、E8 卡片 UI 通过**；E10 已观察长文本两列/滚动所得及按钮，并修复 NoWrap。更多分辨率、缺图详情交互、滚动区域飞入/免费区域收起和所得动效仍未完整目视验收 |
| 存档迁移/幂等恢复 | 保存挂载/版本、进度/每日、期限、交付里程碑、已读/聚焦、剩余根/到期游标、operationSequence/回执；未知旧 key 局部过滤；提交临界区禁止快照。S3/S4/S6，T1/T3 | **E2 快照逻辑基线 + E3 CafeSnapshot 内存往返通过**；未做专用磁盘槽保存/加载及自动存档。旧 token 清理/逐项诊断仍有限制，不能称所有历史记录无损迁移 |
| GM 隔离行动检查 | S8 列挂载/原因/资格/成本/结果/进度/游标/operationState/回执；独立日期/余额/物品、重置、下一次资金拒绝、续办。不会默认改正式玩家存档。T2 有隔离调试源码 | **源码已实现，完整 GM 交互待验**。Navigate/EndDay 在 GM 仅检查；直接预览和手动确认不等于正式入口验收，失败模拟不覆盖任意库存/provider/持久化故障 |

## 结算、恢复与发布边界

- 同步交易只承诺当前受控地图库存和支持通知延迟的真实资金 provider 合同：共同预检后由资金 compare-and-commit、sealed 地图库存批量写入、AP/计数/回执提交，通知最后发出；拒绝/溢出与 AP 在资金提交时变化的补偿有核心测试。E3 已补真实 CafeWorld 通知观察。不得外推为任意第三方 provider 或任意回调故障均已验证。
- Event 是按根提交：首根成功才付当次成本，前根成功结果保留，后根失败从游标续办。Delivery/非最终 Progress 的 `submissionOnly=true` 回执表示本次物资/进度已落账，不代表整项演出或最终结果已完成；待办完成后转完整回执。提交后的关闭只关展示。
- Future 采用退役/后续挂载及 dueActions/expiryAction/currentExpiryEvent/pendingExpiryEvents 恢复，没有额外 Future operationId 回执。RunEvents 使用独立 onExpireEvents；空策略是 Remove。旧 Timer 若曾依赖 resultEvents 到期播放，需要 owner 显式迁移，不能声称已自动保留旧播放行为。
- 旧 `Once`/`once` 是 Daily；精确大写 `ONCE` 才是一次性。旧 endDay 映射 EndDay。旧交付 counter/daily 迁入有效进度/提交日，未知历史日期不猜、不补发奖励；旧 executing 快照按已付成本继续。现有快照测试不等同完整存档槽/崩溃恢复验收。
- 本次正式配置增加的是免费 cafe.enter 导航。Item 中 medicine、food、delivery_receipt 共 3 条是既有隔离交付夹具的元数据，对应 Text 标记 **TEST_ONLY**、归属 MapInventory，不是正式商品、价格或经营原料。没有借验收发布虚构交易、奖励或新正文，没有批量移除 blockedBy；本报告不把公共能力完成当作所有剧情已开放。

## 经营补给待定决策与责任

经营补给需要**内容 owner 与 FreeCafe 领域/存档 owner 共同确认合同**，工程据此实现 provider/适配器并做验收：稳定 itemKey 对应哪个真实 source 实体/规格、每份单位、质量及混合策略、容量/满库存行为、获批价格；地图无经营会话时由哪个既有存档 owner 持有待入库记录、何时应用、如何保存与仅应用一次。

现有 `CafeWorld.Business.BuySource` 按缺量补满实体库存，`CafeWorld.Take` 消耗 source.stock，`CafeWorld.Saving.Restore` 校验实际实体；这些不是固定包地图交易的已接入适配。合同未确认前不创建 CafeSupply 元数据或正式原料交易。确认后仍须证明地图购买→真实 source 增量→进店可取用→专用槽保存/恢复→再次进店不重复。

## 给文案窗口的简短接口

1. 在 Narrative.xlsx 配 Action，选 executionKind=Event/Navigate/Exchange/Delivery/EndDay；用 MapPosition.defaultAction/extendedActions 挂 enabled 地点，同 actionKey 跨地点共享状态。日期/前置放 `Condition.ownerType=action`，持有不消耗的资格放 `actionExecute`；同组 AND、跨组 OR，不把资金不足写成隐藏条件。
2. 成本仅用正整数 `Item(key,n)`、`ActionPoint(n)`、`Money(n)`，Money 对应唯一经营账户“金币”。剧情链填真实 resultEvents；Exchange 留空 resultEvents，successResultOwnerId 指 Result.eventId 列中的独立结果组，不造假 Event，也不在 Result 重扣同一成本。可固定买/卖/换；正式数值须内容 owner 确认。
3. Delivery 同时设置 executionKind=Delivery、非 None deliveryMode、正目标与成本，非最终节点填 ActionMilestone；到期用 onExpirePolicy/onExpireEvents，遵守事件声明入口，不能自动播放 MapAction。常驻免费 cafe.enter 已接正式配置，禁止另加地点中文名特判导航。
4. 物品用独立 Item 页，填 nameText/iconPath/descText/inventoryOwner；当前只能 MapInventory。3 条 TEST_ONLY 不是可发布货品；经营原料等待上述 owner 决策。展示口径为合并成本 + 资格整体一格，最多 6 格。正文与 blockedBy 仍归内容 owner，不硬编码故事 ID、不用假价格/奖励填通路。
5. 更新真源后走官方 GenerateAll.bat→Unity 导入/编译→真实 loader→对应入口验收；填写细节与迁移见 [MapActionsREADME](../../../Assets/Scripts/Narrative/MapActionsREADME.md)。

## 剩余工作与快速验收

按提速要求本轮不继续扩大全量测试，当前剩余明确分为一项功能和四组验收，不用猜测百分比：

1. **功能阻塞：经营原料适配。** 需确认上述映射/单位/品质/容量/价格与持久化 owner，之后才能实现并验证采购→取用→恢复。当前拒绝 CafeSupply，不能发布原料交易。
2. **日结 UI 入口验收。** 在正式 Map/DayFlow 准备一项可见必做，分别从地图、事项和咖啡馆尝试日结；应阻止并聚焦；完成后再次尝试应放行。E9 已证明领域检查点，不再重复同一组核心恢复参数组合。
3. **磁盘存档验收。** 用专用测试槽记录已提交交易/交付和未完成后续根，保存→加载→重试，确认不再收费/计进度/发已发奖励；同时检查自动存档入口。未覆盖用户存档。
4. **视觉/动效验收。** E10 已做 1080p 六格长文本和滚动预览，E8 固定换行行为；尚需较窄分辨率、缺图描述点击、成本飞入/实际所得反馈及免费区域收起目视检查。
5. **GM 手动交互。** 核对隔离准备→失败模拟→续办→回执展示与重置，确认正式玩家会话未被修改。已有源码及隔离会话测试，不等于整套面板手动验收。

兼容边界：Future 没有额外 operationId 回执；旧 seen/focus token 的清理与逐项诊断仍有限。上述边界单列保留，不能表述为任意历史数据或任意外部 provider 故障均已完全覆盖。
