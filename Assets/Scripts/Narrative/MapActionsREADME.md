# 地图行动配置与验证

Map 的地点选择打开可滚动的行动详情卡：名称、描述、类型、倒计时/阶段进度、最多六项需求（两列三行）和执行按钮。需求不足时按钮变灰但仍可点击，弹出“要求不满足”并震动未满足项；每天行动当天用过、未来行动等待中等类型限制会震动按钮。

配置来源仍为 `Assets/ConfigTables/Data/Narrative/Narrative.xlsx` 的 Action 和 MapPosition 页。执行 `Assets/ConfigTables/GenerateAll.bat` 后重新进入 Play Mode。不要编辑生成 JSON 或 generated C#。

## Action 页现有字段

| 字段 | 含义 |
| --- | --- |
| actionKey | 全局唯一行动索引，同一索引在多个地点共享完成状态/计时 |
| type | 基础类型与修饰类型，以 `|` 或 `+` 组合 |
| mustExecute | 策划的 isCrucial；已挂载、未完成的今日必做行动阻止结束当天，即使需求不足 |
| nameText / descText | 名称/描述文本 key |
| requirements | 需求列表，用分号分隔；空字符串为免费行动，最多六项 |
| resultEvents | 事件 key 列表，按配置顺序等待各事件完成后执行下一项 |
| durationDays | Timer 的 remainingDays 或 Upcoming 的 requiredDays，正整数；其他类型填 0 表示无时限 |
| progressTarget | Progress 的 costStage，正整数；其他类型填 0 表示无阶段进度 |
| nextActionKey | Upcoming 到期后在原地点挂载的已配置新行动，必填且不能引用自身 |

基础类型 `Repeatble`（也支持 Repeatable/REPEAT）、`Once`（每天）、`OneTime`（一次性）互斥；`Timer`、`Progress` 可与基础类型或彼此组合；`Upcoming` 与所有类型互斥。`EndDay` 是每天行动的特殊类型，执行成功后进入现有结束一天流程。EndDay 不可标记 mustExecute。

兼容原有表值：`REPEAT`=重复，`DAILY`=每天，**旧值全大写 `ONCE`=一次性**，`TIMED`=时限，`PROGRESS`=进度，`FUTURE`=未来。新增配置建议使用 `DAILY` 或 `OneTime`，避免大小写混淆。

例：`type=Once|Timer|Progress, durationDays=3, progressTarget=2` 表示每天一次、三天后过期、执行两次完成的行动。直接设置正数 durationDays/progressTarget 也会启用对应修饰；填写 Timer/Progress 类型但缺少正数目标会报配置错误。

计时从挂载当天开始，以挂载日 + durationDays 为到期日。新一天开始才更新计数；例如第 1 天挂载 durationDays=2，第 3 天到期。Timer 到期移除并执行 resultEvents（按本次策划配置段的“等于 remainingDays 时触发行动”实现）；已完成的一次性/进度行动不会再次触发到期事件。Upcoming 不可手动执行，到期移除、挂载 nextActionKey 并顺序执行事件。Progress 每次执行都消耗需求并增加一阶段，仅达到目标的那次执行事件，达到目标后移除。

MapPosition 的 defaultAction 和 extendedActions 指定初始挂载；只有 enabled 地点参与挂载和必做检查。新增/动态挂载可调用 `NarrativeRuntime.Instance.Actions.Mount(positionKey, actionKey)`。所有行动进度、每天完成记录、挂载、截止日期、未来替换结果以及地图物品库存随 NCS 快照保存；事件执行中保存也保留后续事件链。

## 需求写法

| 写法 | 检查 / 消耗 |
| --- | --- |
| Item(itemKey) 或 Item(itemKey,数量) | 检查地图库存；成功执行消耗数量，默认 1 |
| Bar(barKey,>=,数值) | 检查 NCS 存档进度条；支持 >、>=、<、<=、=，不消耗 |
| Cond(condKey) 或 Cond(condKey,true) | 必须拥有该存档条件 |
| NotCond(condKey) 或 Cond(condKey,false) | 必须没有该条件 |
| ActionPoint(正整数) | 检查并消耗行动点 |

数值按 32 位整数解析，非法/溢出数值和非正消耗会拒绝加载。进度条自身继续使用已有 min/max 钳制。重复物品 key 或多项行动点需求按总量检查，执行前再次检测，不足时不扣除；消耗过程发生失败时退回已经扣除的物品。

## 地图物品栏和动效

原地图固定显示的物品示例已替换为真实、可存档的地图库存。物品获取入口为 `NarrativeRuntime.Instance.MapInventory.TryAdd(itemKey, count)`，扣除为 TryConsume。该入口供后续奖励/物品系统接入；当前 NCS Result 表尚无物品奖励类型，也不将咖啡馆桌面实体自动视为背包物品。

显示名称可在 Text 表配置 `item.<itemKey>.name`；缺失时显示 key。进度条显示名称可配置 `bar.<barKey>.name`；条件名称使用条件 key 的文本。当前没有地图物品图标配置，消耗动效使用带名称与数量的物品卡，从水平物品栏飞入需求栏并闪亮填充。隐藏在水平滚动区外的物品会先自动滚入视野；行动点从顶部行动点栏飞入需求栏。动效期间阻止重复点击和切换行动，动效结束后再复核需求并提交消耗。

## 手工回归

1. 在 Map 点击市场/临时地点，确认已有剧情行动的描述、每天类型及行动点状态。
2. 配置六项混合需求，逐一改变库存、进度条和条件，检查颜色/满足文字和灰色按钮；点击缺失项时应弹窗并震动，不扣资源。
3. 向 MapInventory 添加超过五种物品，需求引用末尾物品；执行后检查自动滚动、飞行动效、需求填充和正确扣除。
4. 标记某已挂载行动 mustExecute，检查“有需要进行的行动”和结束当天拦截；当天完成后解除拦截。
5. 配置每天+进度、时限、未来行动，推进日期并保存/读取，确认限制、计数和替换不重复触发。

自动回归位于 `Assets/Scripts/Narrative/Tests/NarrativeActionTests.cs`，使用 Unity EditMode Test Runner。
