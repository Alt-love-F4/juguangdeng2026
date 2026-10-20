# 游戏内剧情后台自动测试

## 使用

1. 从正常启动场景进入游戏或主菜单，按反引号键（`~` 所在键）打开全局 GM。
2. 选择 **剧情自动测试**。
3. 选择全结局后台测试（每结局3种属性策略）、快速回归（1种策略），或单个目标结局。
4. 默认 **结局后停下人工验收**：真实结局判定完成后保存日志与截图，显示真实 ResultPanel；在实时演算窗点击通过、标记问题或暂不验收后才开始下一轮。
5. 想连续批跑时，将结局后行为切换为 **截图后自动下一轮**。所有轮次仍逐个记录，遇到阻塞不会强行授予结局。

后台测试使用隔离的真实 NarrativeController + CafeWorld，不替换 NarrativeRuntime.Instance、PlayerRuntime.Current 或 PlayerCafeOwner.Current。测试期间暂停正式日程并阻止正式保存；停止或完成批跑后恢复此前 timeScale。不要在正式剧情演出或其他结局预览正在播放时启动。推荐在主菜单启动。

## 默认覆盖与辅助

默认63个预设：16个启用结局 × 3个开局数值策略 = 48轮，再追加15个明示历史起点场景。快速回归31轮。

- 后台执行真实日程入口（早晨／营业后／夜间）、必须地图行动与有界日常行动。
- 对话、阅读、信件、通知不等待玩家点击；选项和检定通过真实控制器提交；每帧有推进预算。
- 每天开启真实领域营业，解锁目录配方／设备、免费补料、后台生成饮品，并通过真实评价、交付、收入和剧情回执路径结算。
- 显式数值辅助包括初始资金10000、6AP/日、属性与关系／倾向种子；报告记录其性质。这不是合法角色创建／数值平衡／手动制作通关认证。
- 遇到不可满足咖啡品质要求、未解锁选项、日期／前置／作者施工门禁，记录原因，不伪造成功。
- 开局策略没有注入历史完成或结局事实。历史场景明确注入测试前置，单独标记为 `assisted-historical-scenario`，不能算完整开局可达。

## 报告

输出到 `Application.persistentDataPath/NarrativeAutoTests/<UTC批次时间>/`。GM与实时窗有“打开结果目录”。每批包含：

- `manifest.json`：批次、配置内容SHA256、每轮结果。
- `summary.csv`：目标、实际结局、状态、证据类型、日期、交付数、人工验收、截图和原因。
- 每轮 `.json` 与 `.log`：事件／页／选择／检定／数值变化、最终值和阻塞清单。
- 每次实际结局 `*-ending.png`：真实结局面板的屏幕截图，隐藏工具自身实时窗；失败时记录 `screenshotError`，不制造替代图。

截图需要实际渲染 Game 画面；Editor 中会打开 Game 窗口，Player使用正常渲染画面。截图尺寸跟随当前Game分辨率。占位插画仍按真实资源显示，截图不表示最终美术已验收。

`ending`表示实际接受了结局，需核对 `actualEndingId` 是否等于目标。`blocked`／`content-blocked`／`host-blocked`表示本轮明确停止原因；`no-ending`／`budget-exhausted`不等于数学上证明不可达。

## 目前已知内容限制

- `route.eligible.church/rose/revolution`默认false，没有配置结果生产者，因此11/11相关选路仍被关闭。
- `rose.route.1115.s1`：坦诚度／知觉失败尾声作者门禁。
- `church.route.1123.s1`：旧选项引用作者门禁。
- `neutral.route.1129.s1`：营收量纲／周期／阈值未定。
- 1014与1107部分隐蔽检定路径存在未求值check guard；自动计划选择当前配置中能合法进入检定的明确选项，并保留所有正式校验。

## 实现入口

- `Assets/Scripts/FreeCafe/Unity/NarrativeAutoGM.cs`：游戏内GM分类。
- `Assets/Scripts/GameFlow/UI/Automation/NarrativeAutoRunner.cs`：批跑、人工验收、文件记录及截图。
- `Assets/Scripts/GameFlow/UI/Automation/NarrativeAutoSession.cs`：预设、日程、领域咖啡与来访桥接。
- `Assets/Scripts/Narrative/Automation/`：纯后台演出器和可解释目标策略。
- `Assets/Scripts/FreeCafe/Domain/CafeWorld.Automation.cs`：后台饮品辅助与真实领域交付。

咖啡馆日常音乐另已随机使用现有“阳光的音乐”与“日常推进”两首；每次进入日常／新营业日选择一次，剧情owner override优先级不变。
