# 地图行动配置与诊断

本文按当前源码说明配置合同与实现边界，对应规格 `Docs/superpowers/specs/2026-10-16-action-details-inventory-integration-spec.md`。本文更新仅做文件核对，未启动 Unity Editor、未运行测试或正式场景；测试源码不等于测试通过。实际运行结果由主验收报告 `Docs/superpowers/reports/2026-10-16-action-details-acceptance.md` 记录。

## 配置真源与生成路径

- 行动、地点、条件、结果、里程碑和物品：`Assets/ConfigTables/Data/Narrative/Narrative.xlsx` 的 `Action`、`MapPosition`、`Condition`、`Result`、`ActionMilestone`、`Item` 页；物品使用独立 Item 目录，不再从 Object 推断。
- 文本：`Assets/ConfigTables/Data/Shared/Text.xlsx`。结构：`Assets/ConfigTables/Defines/schema.xml`。
- 官方生成入口：`Assets/ConfigTables/GenerateAll.bat`。存在拆分的 `Narrative_*.xlsx` / `Text_*.xlsx` 时会先执行合并；修改源表时需确认拆分表不会覆盖修改。
- 生成 C#：`Assets/Scripts/Config/Generated/`，包括 `NcsActionEntry.cs`；生成 JSON：`Assets/Settings/Resources/Luban/`，包括 `tbncsaction.json`、`tbncsmapposition.json`、`tbncscondition.json`、`tbncsresult.json`、`tbncsitem.json`、`tbtext.json`。不要直接编辑生成物。
- 配置变更的接入步骤是官方生成、Unity 导入/编译、真实 `LubanNarrativeLoader.Load()`、对应场景验收。单独生成成功不能证明运行可用；本次文档更新没有执行这些步骤。

## Action 字段

| 字段 | 填写含义与边界 |
| --- | --- |
| actionKey | 全局唯一索引；同 key 在多个地点共享进度、每日次数、完成和期限。独立实例用不同 key |
| type | 频次/期限/进度规则，建议使用下节的大写标准值；组合用 `\|` 或 `+` |
| executionKind | 精确填写 Event、Navigate、Exchange、Delivery、EndDay；行为类别与频次分开 |
| navigationKey | 仅 Navigate 使用；当前白名单只有 `cafe.enter`，禁止场景路径或任意命令 |
| executeTextKey | 可选按钮 Text key；空值由卡片选择默认文字 |
| sortOrder | 地点内升序；同值保持挂载源顺序 |
| mustExecute | 未完成且仍显示的必做行动阻止日结；成本不足不会自动隐藏。禁止与 Timed/Future 组合，须挂到 enabled 地点 |
| nameText / descText | 名称和描述 Text key |
| requirements | 只表达正整数成本：Item、ActionPoint、Money；多项用分号或换行分隔 |
| resultEvents | 玩家主动执行的有序 NCS 事件链；普通进度到目标才启动最终链，Delivery 另有里程碑链。Exchange 不允许填写 |
| successResultOwnerId | 可选独立 Result 组 owner；Exchange 必填；Event/Progress/Delivery 在最终提交的最后一个真实根结算，无真实根时走独立物品/资金批次，见下文提交边界 |
| durationDays | Timed/Future 正整数天数；无期限填 0；非 Future 的正值也会启用 Timed 修饰 |
| progressTarget | Progress/Delivery 正整数目标；无进度填 0；非 Future 的正值也会启用 Progress 修饰 |
| nextActionKey | Future 到期后在原挂载地点挂载的后续行动；须存在，不能自指或形成链循环 |
| deliveryMode | None、Cumulative、Consecutive；非 None 才启用交付状态、每日一次和连续断日规则 |
| onExpireEvents | 独立的到期事件链，不再隐式复用成功 `resultEvents` |
| onExpirePolicy | Remove 或 RunEvents；空值加载为 Remove；RunEvents 必须有 onExpireEvents |
| notifyNew | 是否参与新内容提示；正式常驻导航设 false，Navigate 即使为 true 也被排除出新卡提示 |
| replacesLocationActions | 未完成且显示时，只保留同地点具有该标记的未完成行动；同时遮住该地点导航，不影响其他地点 |
| focusLocationOnComplete | 成功后记录待聚焦地点，由地图宿主消费 |

`ActionMilestone` 使用 `milestoneId`、`actionKey`、`atProgress`、`eventId`、`seq`。仅用于非 None 的交付；要求 `1 <= atProgress < progressTarget`，按 seq、milestoneId 稳定排序。达到最终目标的事件写在 Action.resultEvents。

## 频次、组合与旧类型

| 新配置标准值 | 语义 | 兼容旧写法 |
| --- | --- | --- |
| REPEAT | 可重复提交，每次重新检查成本 | Repeat、Repeatble、Repeatable |
| DAILY | 每个正式游戏日最多成功一次 | Daily；**旧 Once / once 也表示每天** |
| ONCE | 整个存档成功一次；精确大写才是此含义 | OneTime |
| TIMED | 挂载日 + durationDays 到期；可修饰基础类型 | Timed、Timer |
| PROGRESS | 每次有效提交 +1，达到目标才进入最终事件链 | Progress |
| FUTURE | 倒计时不可执行，到期替换为 nextActionKey | Future、Upcoming |

REPEAT/DAILY/ONCE 互斥；TIMED 与 PROGRESS 可叠加，如 `DAILY|TIMED|PROGRESS`，并填写正数 durationDays/progressTarget。FUTURE 与其他类型互斥。没有基础类型的 TIMED/PROGRESS 沿用可重复提交语义；普通 Progress 可同日多次，Delivery 固定每日一次。普通 ONCE 的有序事件链全部成功后才算完成。

不要把旧 `Once` 批量改大小写：`Once|Timer|Progress` 的基础频次是 Daily，`ONCE|TIMED|PROGRESS` 才是一次性进度行动。类型 parser 还接受逗号/分号分隔，但新配置统一用 `|` 或 `+`。

旧 `EndDay` 类型加载成 Daily 并设置 endDay 标记；executionKind 填 EndDay，成本留空，不带期限/进度/Future，也不能自身 mustExecute。宿主调用现有 DayFlow 日结检查，不通过修改日历实现。直接构造的旧 `endDay=true`、Event 定义也有类别兼容映射。

executionKind 留空时：deliveryMode 非 None 映射 Delivery，否则旧 EndDay 映射 EndDay，其余 Event。新的交付必须同时声明 `executionKind=Delivery` 和非 None 的 deliveryMode；仅写类别不会启用交付规则。

## 出现、执行资格与成本

`Condition.ownerType=action, ownerId=actionKey` 控制卡片出现；日期、前置事件、解锁等放这里，不满足不显示，也不产生新卡提示。`ownerType=actionExecute` 使用同一条件解释器，控制已显示行动能否执行；属性、持有但不消耗的物品等放这里，失败保留卡片并说明原因。没有对应 Condition 行表示无该类限制。

两个 scope 都是**同 groupIndex 内 AND，不同 groupIndex 间 OR**。例如 groupIndex=0 有条件 A、B，groupIndex=1 有条件 C，整体是 `(A 且 B) 或 C`。详情页把所有资格分支放进一个资格区域，标明“任一组满足/全部满足”及各项状态，不能把 OR 各组当成全部必须满足。GM 的逐行失败也不代表整体资格失败，应同时看 aggregate。

requirements 的合法格式是 `Item(已确认itemKey[,正整数数量])`、`ActionPoint(正整数)`、`Money(正整数)`；Item 缺省数量为 1。重复同 key 物品、AP、资金会合并核对和展示；不同成本永远全部支付，不能用 OR 条件选择替代成本。Bar、Cond、NotCond 会被 parser 拒绝，须迁到合适的 Condition scope。成本不足不得写进出现条件；资格物品检查不扣物品，交付物资必须写成 Item 成本。

Money 固定读取现有 `INarrativeMoneyAccount` 对应的唯一经营账户，卡片/物品栏称“金币”；不存在独立 `item.gold` 钱包。数值/provider 缺失应呈现“暂不可用”，具体 key 与诊断留在 GM/日志。动画只做表现，动画后仍由核心重新检查资格、库存、余额与 AP。

展示上限为“合并后的成本项数 + 资格表达式一格”不超过 6；没有 actionExecute 条件时资格占 0 格，有任意数量的 OR-of-AND 分支时整体占 1 格。parser 按合并后的成本种类计数，重复同 key 物品/AP/资金不会按原始行数额外占格，loader 与详情页采用同一展示口径；合并数量仍须在 Int32 正整数范围内，不截断第七项。

## Result owner 与执行提交边界

`successResultOwnerId` 指向 Result 页 `eventId` 列中的一组行；列名沿用历史名称，owner **可以是行动引用的独立结果组**，不要求存在同名 Event。loader 认可被 Action.successResultOwnerId 引用且具有 Result 行的 owner，也继续认可 Event/Choice owner。不要创建永不播放的假 Event 来容纳交易奖励。独立结果组应使用独立 ID，避免与真实事件/选项共用组导致多入口重复结算。各 Result 仍以 `ownerType=result, ownerId=resultId` 配条件。

- **Exchange**：必填 successResultOwnerId，resultEvents 留空。仅支持 ItemAdd、ItemRemove、MoneyAdd、MoneyRemove（源表常用 `ITEM_ADD`、`ITEM_REMOVE`、`MONEY_ADD`、`MONEY_REMOVE`）。按当前 Result 条件选择结果，先预检成本再输出，不能用本次将获得的物品垫付输入。当前实现要求同一 `NarrativeMapInventory` 和支持通知延迟的资金 provider；共同预检库存/整数范围、余额和 AP，在同步提交中更新结果、次数及回执。拒绝返回 false；同一个 operationId 的成功回执可直接返回成功，不能把另一次新生成的 operationId 当作重复回调。不要在 requirements 与 Result 中重复扣同一笔成本。
- **Event / 普通 Progress**：有 resultEvents 的普通行动把当次成本并入首个成功根的结果预检；该根成功前取消不收费。普通 Progress 未到目标时每次直接提交整组成本和进度，到目标才进入最终链。前根已成功、后根失败时保留已提交标记和剩余事件游标，继续时只重试剩余根，不重付成本或重发前根结果；整条链不是一次原子事务。successResultOwnerId 只在最终提交的最后一个真实根结算，不要求为了奖励再造事件。无事件链的普通 Event/Progress 使用同一地图库存批次提交成本与最终结果；独立批次仅支持物品/资金结果，其他世界效果须放在真实事件根。
- **Delivery**：每次成功交付提交整组成本并记录进度/提交日，随后排入当次里程碑与最终事件。每日一份；Cumulative 保留累计进度，Consecutive 在距离上次提交超过一天且未完成时清零进度，不退还历史物资，也不清除已触发里程碑。事件取消或结果失败保留 `executingAction/currentEvent/pendingEvents` 游标；对同 key 再次 Execute 或恢复待办只续未完成链，不再扣物资、不再加进度。达到最终目标但最终链或独立结果组未成功时 `IsCompleted` 仍为 false。successResultOwnerId 在最终提交的最后一个真实根并入结算；没有真实根时，在已提交交付之后执行独立物品/资金批次。该结果批次失败时，即使 pendingEvents 为空仍保留待办，可重试结果，不再收取交付成本。里程碑/最终事件自身的 Result 继续有效，不要与独立组重复配置奖励。

Exchange、Event、Progress、Delivery 使用 operationId 与回执。即时成本/进度提交路径（含 Delivery 和非最终 Progress）先写 `submissionOnly=true` 回执，表示当次提交已落账、演出或最终结果可能待办；当次链与应结算结果全部成功后转为完整回执。Exchange 直接写完整回执；分阶段 Event 还用 rootCostCommitted 记录首根已付，并在整次执行成功时写完整回执。非最终进度提交结束后的完整回执也只代表这次提交，不代表整个目标完成。同一 operationId 的 submissionOnly 回执在仍有待办时会续办，不能把它提前当作全部完成。重复点击的新 operationId 仍是新操作，须受每日次数/完成/执行中等状态约束。

loader 会拒绝 requirements 与行动 resultEvents/successResultOwnerId 中重复的扣款：ItemRemove 去掉历史 `item:` 前缀后与 Item 成本 key 比较，已有 Money 成本时再有 MoneyRemove 也会拒绝。不要依靠条件互斥绕过此静态检查。代码的批量预检、通知延迟或补偿路径不等于真实账户与场景失败注入已验收；不得把任意自定义库存/provider 都描述为可靠原子事务。

## 到期事件与声明阶段

挂载日 day3、durationDays=2 时，day3/4 可执行，正式 BeginDay 到 day5 时退役。重复同一天 BeginDay 不重置 AP 或重复到期；Future 在原地点挂载后续 key。成功结果与到期结果必须独立配置：Remove 只移除，RunEvents 使用 onExpireEvents，绝不自动拿 resultEvents 当过期奖励。

到期事件通过普通 NCS 候选队列，使用 Event **声明的 entry、priority 和安全边界**。Morning/BeforeNight 等阶段事件等待对应 ProcessEntry；不是一到期就强行从地图入口启动。显式到期资格仍受事件前置条件、blockedBy、once 等门禁限制。`entry=MapAction` 的事件只允许玩家操作，loader 拒绝把它用于到期；需要经营服务上下文的事件也不会由该到期接口启动。

到期任务保留 dueActions、expiryAction、currentExpiryEvent、pendingExpiryEvents；排队或事件失败时保留当前未完成项，恢复只续剩余事件。Future 后续挂载与这些状态随快照保存；当前没有另外的 Future operationId 回执，不应把挂载幂等与队列恢复描述成已验证的跨场景回执保证。

旧 Timer 若曾借 resultEvents 做过期处理，须由配置 owner 显式迁移为 RunEvents + onExpireEvents，并确认事件声明入口可自动调度；旧 MapAction 事件不能直接复制。缺省 Remove **不会自动恢复旧到期播放行为**。

## 地点、咖啡馆与地图投影

MapPosition.defaultAction/extendedActions 指定初始挂载；正式入口只挂载 enabled 地点。动态挂载可用 `NarrativeRuntime.Instance.Actions.Mount(positionKey, actionKey[, instanceVersion])`；底层 Mount 不代替宿主检查地点可达性。详情、红点与必做引导共用 GetDisplayableActions 的出现条件、排序和替换投影。

权威咖啡馆地点只有 `positionKey=cafe`。当前生成配置已把 `actionKey=cafe.enter` 挂到 cafe，executionKind=Navigate、navigationKey=cafe.enter，成本/成功结果/到期事件均空，notifyNew=false。`market` 及其“大堂区”等显示名不能视为咖啡馆；中文别名只走现有地图兼容映射，不能扩展导航白名单。

点地点聚焦并打开配置卡，再点“进入咖啡馆”才委托 MapSceneController/既有 DayFlow 或场景切换宿主；核心 Actions.Execute 不执行 Navigate。导航提交会检查当前卡片 owner、地点与实例 token，并再次检查可用性；不靠 AP、物品或资金付费，不生成剧情完成/奖励/耗日记录。新手锁定、加载防重入与失败恢复仍依赖宿主，需正式场景验收。

`oct07.cafe.s1` 的 replacesLocationActions/focusLocationOnComplete 继续承担：S1 未完成时 cafe 仅显示剧情卡；成功后请求返回地图聚焦 cafe，再由玩家选择免费导航。该替换不隐藏其他地点支线，也不意味着自动开店。所有日结入口应走正式必做检查；mustExecute 不能靠改日期绕过。事项面板从已显示、有 AP 成本的非导航行动投影，零 AP 必做仍可阻止日结。

notifyNew 控制新内容候选；只有实际展示的卡片通过 ConfirmActionDisplayed 确认实例 token 才记已读，打开地点不等于翻完所有卡。Daily/Delivery 的实例 token 带日期，余额变化本身不制造新内容；已读与完成是不同状态。

## 填写示例与内容边界

以下两行引用现有配置，可在源表定位；共同的普通默认字段为 durationDays=0、progressTarget=0、nextActionKey 空、deliveryMode=None、successResultOwnerId/onExpireEvents 空、onExpirePolicy=Remove：

| 用途 | Action 填法 | 地点与条件 |
| --- | --- | --- |
| 现有必做剧情 | actionKey=oct07.cafe.s1，type=ONCE，executionKind=Event，requirements 空，resultEvents=oct07.s1，mustExecute=true，replacesLocationActions=true，focusLocationOnComplete=true，notifyNew=true | cafe.extendedActions 挂载；日期与前置继续由现有 action Condition 管理，保留事件门禁 |
| 现有免费导航 | actionKey=cafe.enter，type=REPEAT，executionKind=Navigate，navigationKey=cafe.enter，requirements/resultEvents 空，mustExecute=false，notifyNew=false；文本 key 为 action.cafe.enter.name/desc/button | 仅挂 cafe；不新增付费条件；受地点替换与宿主新手门禁约束 |

下表是**待内容方填写的合同模板，含占位文字，不能直接导入**。不指定虚构商品、价格或正式补货映射；交易/交付测试数据只放隔离夹具。

| 用途 | Action 字段合同 | 实际结果配置 |
| --- | --- | --- |
| 普通剧情支线 | executionKind=Event；type 选 REPEAT/DAILY/ONCE；nameText/descText 引用已审文本；requirements 仅填获批成本；resultEvents 填已存在的有序 MapAction 事件 | 使用真实事件/选项结果；需要固定末尾结果才填独立 successResultOwnerId |
| 固定采购 | executionKind=Exchange，type=REPEAT，deliveryMode=None，requirements=`Money(获批正整数价格)`，resultEvents 空，successResultOwnerId=独立结果组ID | Result.eventId=该组ID，resultType=ITEM_ADD，target=已确认地图库存itemKey，value=获批正整数数量 |
| 固定出售 | executionKind=Exchange，requirements=`Item(已确认输入key,获批数量)`，resultEvents 空，successResultOwnerId=独立结果组ID | MONEY_ADD，target=cafe，value=获批金额；不另造金币物品 |
| 固定换物 | executionKind=Exchange，requirements=`Item(已确认输入key,获批数量)`，resultEvents 空，successResultOwnerId=独立结果组ID | ITEM_ADD，target=已确认输出key，value=获批数量 |
| 每日交付 | executionKind=Delivery，type=PROGRESS，deliveryMode=Cumulative 或 Consecutive，progressTarget=获批目标，requirements=`Item(已确认物资key,获批数量)` 加可选获批 AP 成本 | ActionMilestone 配非最终节点事件；resultEvents 可配真实最终 MapAction 事件；固定最终奖励可填独立 successResultOwnerId，无事件链时该组限物品/资金结果 |

每个新增行动还须填写 actionKey、文本、sortOrder、mustExecute、notifyNew 和适用的期限/聚焦策略，再挂 enabled 地点；出现条件与执行条件分别放对应 scope，不用 UI/DayFlow 硬编码故事 ID。

## 存档兼容策略

动作快照保存 AP、普通进度、每日记录、期限、挂载/版本、退役、已读、待聚焦地点、交付状态、事件/到期游标、operationSequence 和已有回执。物品数量由 NarrativeMapInventory 快照保存，真实经营资金由原有账户/存档 owner 保存；仅保存动作 JSON 不等于保存全部经营状态。同步提交期间禁止快照；跨演出仍遵守 NCS 与行动保存门禁，不能假定任何帧均可保存。

- deliveryVersion=1 恢复交付的 progress、lastSubmissionDay、completed、triggeredMilestones；有效日期恢复后 RefreshForDay 处理连续断日，不补满 AP。
- 旧 deliveryVersion=0 保留已知行动的普通计数、每日日期与退役状态；显式交付从旧 counter 转入 progress（钳制到目标），从旧 daily 转入 lastSubmissionDay，并据目标恢复 completed，不补发物资或奖励。存在进度但没有提交日时输出兼容警告，不猜历史日期、不清空进度；Consecutive 从下一次有日期提交后才开始断日重置。旧格式没有已触发里程碑列表，不能据无日期计数重建全部历史交付凭据。
- 旧正在执行快照没有 activeOperationId 时按“旧路径已扣成本”恢复，避免继续链时再扣。当前快照另存 rootCostCommitted、submissionRecorded、successResultsCommitted、剩余根与回执的 submissionOnly；分别区分成本已付、进度已记和最终结果已结算，失败暂停后可在允许的保存边界保存，再显式重试，包括空事件队列的结果待办。
- 未知行动的 counter/daily/expiry 记录会局部忽略并输出 `Ignored unknown saved action key` 警告；mounts、receipts、dueActions、retired 也过滤未知行动。保留其他有效状态，不整份清空存档。seenActionInstances 与 pendingFocusPositions 仍按历史 token/地点字符串恢复，并非所有过滤分支都逐项警告；完整历史 token 清理与诊断仍有限制。

## 物品归属与经营原料限制

地图库存是可存档的 `NarrativeMapInventory`，物品栏显示其正数量项；金币显示唯一经营账户余额。Item 页的唯一合同为 itemKey、nameText、iconPath、descText、inventoryOwner，名称/描述须引用已有 Text key，数量仍由库存保存。`NarrativeItemCatalog` 严格校验空/重复 key，以及 Action Item 成本、ITEM_ADD/ITEM_REMOVE 结果、ITEM_GTE 条件的目录引用；结果/条件中的历史 `item:` target 前缀在引用校验时归一。当前只支持 inventoryOwner=MapInventory；CafeSupply 或未知归属明确拒绝加载。

物品栏直接按 Item.itemKey 查元数据；缺名称回退“未命名物品”，缺图保留原槽位并显示“物”字通用占位，名称/描述仍可读取。现有 medicine、food、delivery_receipt 是既有交付隔离夹具的 MapInventory 目录项，对应中文 Text 注释标记 TEST_ONLY，未挂正式地图，不是已批准商品或经营原料。

FreeCafe 经营原料由真正的 source 实体库存及其存档 owner 管理，地图库存加同名物品不会进入经营。当前没有获确认并接入的 itemKey→source、计量单位、质量/容量、跨场景保存/恢复及仅应用一次的交接合同。**不得发布经营原料商品或声称地图采购可用于经营**；需先由内容/领域 owner 确定映射，再实现适配并验证地图购买→进店取用→保存恢复。本文不提供伪造商品、正式价格或生产补货映射。

## GM 隔离诊断用法

现有 GM 左栏进入“地图行动检查”，或在“叙事调试”点“地图行动检查（独立会话）”。它读取生成配置，建立独立 NarrativeDebugSession，不创建/重置正式 NarrativeRuntime，也不写玩家存档。

1. 用“准备行动日期 day”“准备独立金币余额”和物品 key/最终数量准备起点；资格在“测试条件/资格设置”中准备。准备日期只修改起点，不处理正式到期。
2. 点“挂载配置并建立起点”；它挂载 enabled 地点，挂载日就是准备日。后续改测试条件后重新建立当前起点；“重置样例到测试起点”恢复该独立基线。
3. 搜索 actionKey/标题/地点，选行动与地点。复制检查内容：配置/有效挂载、实际显示与原因、action/actionExecute aggregate 和逐行诊断、成本/结果表达式、频次/进度/期限、事件门禁、当前游标、operationId 与已有回执。结果表达式不是已发放回执；检查文本列 operationState、失败原因、回执 ID/key/day/submissionOnly 与 rootCostCommitted/submissionRecorded/successResultsCommitted，不能仅凭回执数判断整项完成；核心禁止快照时部分字段暂不可读。
4. “提交/重试所选行动”走 Actions.Execute；“恢复已提交行动的待办”续剩余链/到期任务。推进每日与到期用“推进行动日期（到期/每日刷新）”，不能用“准备日期”代替；当前事件声明阶段与门禁仍适用。
5. “模拟下一次资金提交失败”只拒绝下一次独立资金 TryCommit，无资金提交不会触发；重置起点会清除。它不模拟任意库存写入失败、正式账户持久化故障或场景加载失败。查看“状态与诊断”区分接受请求、成功提交与事件失败。
6. “手动确认测试演出完成（仅核心）”仅用于核心诊断；事件直接预览、隔离提交均不代表正式地图入口验收。Navigate/EndDay 在此仅检查，不调用正式宿主。结束后“退出测试会话”。

正式导航、新手/oct07、地图交互、资金跨场景、存读档和失败恢复仍需对应正式 Map/DayFlow 场景观察；演出 UI 按仓库约定使用 `Assets/Scenes/PresentationDebugScene.unity`。本文没有运行这些验证，主验收报告负责记录实际命令、通过/失败/未运行与证据。
