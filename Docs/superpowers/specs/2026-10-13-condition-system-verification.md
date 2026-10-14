# 条件系统最小实现与验证记录

实现依据：`2026-10-13-condition-system-design.md`。日期：2026-10-13。Unity 6000.3.5f2。

## 已实现

- 配置定义的 bool 条件池，初始值、只允许置 true、未知 key 拒绝；专精唯一来源。旧 flags 仅保留内部 once/lock/achievement 职责。
- 同组 AND、跨组 OR；BOOL_EQ、ITEM_GTE、ACTION_PROGRESS_GTE、ACTION_COMPLETED，以及仍在使用的 Bar/关系和事件记录查询。旧 Action Cond/NotCond/Bar 解析不再接受。
- 事件及选项 bool/物品结果按执行顺序预检，整批提交；失败保留可定位错误及待恢复状态。
- 每日固定成本交付：累计、连续、合并重复成本、一天一次、中途节点一次、完成锁定、保存 pending 演出。UI 显示模式/进度/今日提交状态。
- 正式 DayFlow 日序同步，配置日期调度每正式日最多处理一次并保存处理日序，不靠交付 EndTurn 推进日期。
- YokiFrame FSM Menu/Loading/Playing/GameOver；安全边界选择稳定结局，停止日程、行动、经营计时/输入及排队自动档；结束面板只能回主菜单。
- Narrative envelope v2，显式支持 v1；未知未来版本拒绝。保留其它参与者，并处理同场景读档和未加载场景的经营数据。
- 新增 ConditionKey/ActionMilestone/Ending 源表和生成产物；官方 GenerateAll.bat 全量成功。补入正式开场已引用但 Object 表缺失的 adele，原六条 Object 数据未改。
- 修复相关回归暴露的关系方向写入问题、物品栏模板被销毁后引用的问题，并更新与当前正式布局/流程不一致的旧测试断言。

没有开发 spec 的暂缓能力。FreeCafe 仅加会话状态输入/计时门禁，没有修改物品/杯子实体模型或合并咖啡库存。

## 原存档保护

原目录：`C:/Users/hp/AppData/LocalLow/DefaultCompany/聚光灯2026/Spotlight/Saves/slots/`。

备份：

- `Library/Locus/tmp/condition-system-save-backup/save_0.original.yoki`
- `Library/Locus/tmp/condition-system-save-backup/save_1.original.yoki`

自动档 SHA-256：`ff9e450df9a2e2aa83ab9cb658755f548b01745a9d19eba184ad23f64fd5310d`。验证期间再次核对原自动档与备份一致。所有试验写档使用 `load-copy` 或 `newgame-copy` 子目录，未覆盖真实自动档。

实际旧自动档只包含人物详情和 DayFlow 两个参与者，场景 Map、第 1 天，没有叙事参与者。通过正式 ConfirmLoad 确认入口恢复为 Playing/CityMapLeisure，正确初始化新叙事会话。此结果不代表任意历史存档都兼容。

## 自动测试

- Unity 编译成功，完整域重载完成。
- Luban 官方生成成功；源表/生成内容核对一致；最终配置校验 0 错误，19 bool keys、6 actions、2 endings，两个测试结局均 disabled。
- EditMode：188/188 通过。Narrative Core、Narrative Editor、GameFlow Narrative、Saving。
- PlayMode：58/58 通过。DayFlowPresentation、DetailsPanel、FirstDayLetterDraft、FirstDayLetter、NcsOpeningData、MapInitialization。
- 最终 Console 检查无 error。
- `git -c core.whitespace=-blank-at-eol diff --check` 通过。普通 diff --check 仅报告 Unity 新序列化空字段自带的行尾空格。

结果文件（本机 Library，非版本控制）：

- `Library/Locus/tmp/condition-system-verification/editmode.json`
- `Library/Locus/tmp/condition-system-verification/playmode.json`
- `Library/Locus/tmp/condition-system-verification/queued-autosave.txt`

## 实际正式流程

1. 从主菜单确认读取原自动档备份副本，进入正式 Map/Playing/CityMapLeisure，玩法输入可用。
2. 在第 1 天启用测试条件并给予地图物品：累计和连续各提交一次，进度 1/5、1/5，药品从30变26，行动点从5变3；同日再次提交拒绝。
3. 写入副本 v2 自动档并重新从正式读档入口加载，bool/物品/进度一致，同日仍不能重复提交。
4. 从正式地图进入 CoffeeShop，进入 FreeCafeBusiness；结束经营返回地图，经过夜晚及日结进入第 2 天。
5. 第 2 天累计及连续各提交一次，达到 2/5，药品22、行动点3。节点演出中保存，再加载恢复待播放节点，药品/进度未重复扣增，当日重交仍拒绝。
6. 连续漏日后有效进度归零；累计继续经过第3/4/5天达到完成，奖励 delivery_receipt 只有1份，进入 GameOver。
7. 最终干净 Play 会话重新从主菜单 StartGame，跳过开场信（经正式按钮行为提交默认建角），进入地图；选择咖啡馆，逐句完成阿黛尔13句引导，进入 FreeCafeBusiness，GameplayControlsAvailable=true。
8. GameOver 后调用 Esc、Resume、OpenPause、BeginNewDayFlow 和执行已完成行动均不能继续游戏；自动档请求拒绝。
9. 结束快照写入测试副本手动槽，从菜单正式读取恢复 GameOver/timeScale=0；返回主菜单后再 StartGame，Playing/CityMapLeisure、无结束标记、无旧交付奖励。
10. 专门排队自动档后在同帧触发完成结局（仅内存测试配置移除演出以控制时机），EOF 后副本自动档文件字节不变。
11. 共享 PresentationDebugScene 的 GameOver 入口显示 ResultPanel，timeScale=0 时按钮关闭及过渡正常。

正式流程验证通过调用当前 UI 按钮所绑定的公开入口/完成方法推进；不是逐像素鼠标自动化。用于终局和存档时序的测试开关/库存/进度均为 Play Mode 内存试验，未写入正式配置。

截图：

- 正式累计结局：`Library/Locus/Screenshots/locus_game_20261013_121159_989.png`
- 共享调试场景结束面板：`Library/Locus/Screenshots/locus_game_20261013_123845_277.png`

验证结束已返回 Edit Mode，活动场景 `Assets/Scenes/启动场景.unity`，场景无未保存修改。TMP 动态字体测试缓存已清理并恢复基线内容，既有 `.workbuddy/` 保留。

## 测试内容使用与限制

`act_test_delivery_cumulative` / `act_test_delivery_consecutive` 每次 medicine2 + food1 + AP1，目标5，节点2；需要 `test.delivery.enabled=true` 且测试入口挂载，正式地图默认不挂载。结局默认 disabled。可在共享调试场景侧栏打开新增“游戏结束 / 返回主菜单”预览。

未执行独立 Player 构建、全项目所有测试、长时间经营压力测试或所有历史版本存档迁移。未逐一手动覆盖全部 NPC/事件支线；相应核心逻辑由本次自动回归覆盖。已有调试长对话仍引用 Object 表未定义的 eugene，本次未扩展这项无关内容。测试期间一次后台源变更触发了 Play 域重载，随后冻结源码并重跑了干净会话的结束保护、新游戏与恢复验证，最终结果不使用中断会话的失败状态。
