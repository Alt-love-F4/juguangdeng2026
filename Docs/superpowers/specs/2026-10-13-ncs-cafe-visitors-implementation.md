# 特殊 NPC 来访实现与验证记录

本次按 `2026-10-13-ncs-cafe-visitors-design.md` 实施。用户确认的歧义处理：`RESERVE_CAFE_VISIT` 只预留；必须后续 `SPAWN_NPC` 提交且经营延迟已到，才申请入座。

## 当前工程核对

原工程已有 NCS 安全结算边界、正式事件队列、DayFlow 日历、Object/Text 身份目录、普通咖啡馆订单与存档。缺少独立来访实例、预留队列、订单后耐心、阶段反馈和正式演出经营暂停。本轮复用这些已有系统；领域不依赖 NCS/config/UI，接入层翻译 DTO。

工作区原有大量未提交改动，本轮按现有文件追加，未执行 checkout/reset/clean、未提交 Git、未重建共享演出场景。

## 实现入口

- `Assets/Scripts/Narrative/NarrativeCafeVisits.cs` 与 `NarrativeController.CafeVisits.cs`：不可变提交快照、持久化实例与终态、位置归属、事实去重与串行阶段 hook。
- `Assets/Scripts/FreeCafe/Domain/CafeWorld.Visits.cs`、`CafeWorld.VisitSaving.cs`、`CafeVisitTypes.cs`：预留、座位队列、客流预算、来访状态、独立耐心、待反馈与恢复校验。
- `Assets/Scripts/FreeCafe/Unity/CafeVisitBridge.cs`：按双方恢复完成状态同步，反馈重投/确认、人物资源检查、座位专属视觉。
- `FreeCafeRuntime`：统一开店同步、经营 Tick 暂停、特殊人物点击、正式保存入口。
- `Assets/Scripts/GameFlow/UI/Presentation/Pause/PresentationPauseRegistry.cs`：可叠加暂停 owner，正式 Panel/Presenter 生命周期接入，不修改全局 timeScale。
- `Assets/Scripts/FreeCafe/Unity/FreeCafeGMPanel.cs`：已有 GM 面板中增加“特殊来访”分类与实例诊断。

## Excel 示例入口

源表为 `Assets/ConfigTables/Data/Narrative/Narrative.xlsx`，文本为 `Assets/ConfigTables/Data/Shared/Text.xlsx`。新增 CafeVisit 与 CafeVisitHook。时间/耐心/离店字段使用 string 列保留空值和非法值供 adapter 明确诊断与默认处理；运行时 DTO 为 typed 值。

- `demo_cafe_visit`：角色 `adele`，实际普通座位 ID `customer_0`，arrivalDelaySeconds 留空，patienceSeconds=180，leaveAfterServed=false。
- `demo_cafe_delayed_visit`：同角色/座位，延迟 30 经营秒，送餐后离店。它是独立测试预设，不能与前例同时预约同一角色。
- 普通室内资源：`Characters/adele/neutral`，复用 Object 已有资源，没有覆盖人物身份。专属 seatedVisualPath 留空；不将其计作专属姿态素材验证。
- 先请求 `EV_DEMO_CAFE_RESERVE`（或延迟的 `EV_DEMO_CAFE_DELAYED_RESERVE`），再请求 `EV_DEMO_CAFE_ARRIVE`，最后开店。
- `EV_DEMO_CAFE_CANCEL` 用于 NCS 取消/离场。
- 订单前点击：`EV_DEMO_CAFE_BEFORE` / `DEMO_CAFE_BEFORE` Conversation。
- 上餐自动：`EV_DEMO_CAFE_SERVED` / `DEMO_CAFE_SERVED` Conversation。
- 上餐后点击：`EV_DEMO_CAFE_CHAT` / `DEMO_CAFE_CHAT` Conversation。
- `EV_DEMO_CAFE_LEAVING` 与 `EV_DEMO_CAFE_ENDED` 是纯结果事件，写入 `demo.cafe.leaving` / `demo.cafe.ended` ConditionKey，便于检查反馈。不弹空窗口。

示例没有挂 EventDate，不自动进入正式剧情。启动正式会话、进入 CoffeeShop 后通过现有 GM 面板“特殊来访”请求事件；也可用 NCS Controller.QueueEvent 同名事件。两套预设属于同一角色，因此不先预约而直接 SPAWN 会得到“多预设歧义”提示，这是刻意拒绝猜测预设。

## 默认与诊断

无来访预设的有效 NPC 使用普通座位、普通耐心、延迟 0、送餐后离店。空 Hook 不阻塞且标记无内容。坏座位回退普通座位/视觉；坏专属图片回退 Object 室内资源；坏角色/无世界资源终止 configuration_invalid。缺失/条件拒绝/锁定/onceOnly 阶段事件跳过并记录原因。日志按实例/诊断键去重，GM 展示状态、座位、预算、延迟、耐心、原因与反馈。

`python Assets/ConfigTables/create_cafe_visit_fixtures.py` 在 `Library/Locus/tmp/cafe-visit-fixtures/` 生成全空表和错误配置的完整 Excel 副本，不覆盖正式数据。

## 验证（持续更新）

- 官方 `GenerateAll.bat` Luban strict 已通过，生成 typed C# / JSON；未手改生成文件。
- 空表与错误配置隔离 fixture 官方 strict 均退出 0，输出位于上述临时目录各自的 JSON/generated；错误可选字段留给运行时默认处理。
- 首轮针对性 EditMode：NCS 来访 9 + 咖啡馆领域 16，共 25/25 通过。
- Unity 编译已通过；后续取消边界和集成测试补充后需再次收敛并复测。
- PlayMode/UI 与实际 CoffeeShop 集成结果待最终测试填写。没有剧情或没有专属素材不算对应演出/素材验证通过。
