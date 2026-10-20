# 现场跟随待办与快速剧情测试

更新时间：2026-10-20（Asia/Shanghai）。

## 游戏内入口

从正常启动场景进入主菜单，按反引号打开 GM → **剧情自动测试**：

- **快速现场测试（跳开场/教程，直达选项）**：默认角色直接进入第一天地图；不播放序章、建角信件、arrival 开场、咖啡馆开场介绍或新手教程。沿用正式玩家、地图、CafeWorld 和 DayFlow；没有自动授予开场剧情完成事实。
- **完整开场现场测试（保留教程）**：走正常新游戏开场。对话仍可以整段推进；角色创建及操作教学可以手动接管。
- **现场目标：某结局**：同一真实流程，对话选择倾向指定结局。IF0 必须经过开场拒绝和离开，所以该目标自动保留开场。
- **现场：暂停 / 继续、只执行下一步、停止跟随**：暂停的是自动操作，游戏现场仍可由玩家接管；停止后保留当前会话。测试时阻止正式存档及自动保存，结束后释放门禁。
- **打开现场测试记录**：查看逐步 JSON。旧后台批跑入口继续使用隔离会话，输出目录独立。

Editor 中等价入口为 `Tools > Narrative > Walkthrough > Follow Current Todos`、`Follow Full Opening`、`Follow IF0 Target`。这些入口均要求 Play Mode。

## 场景与操作记录

右侧现场记录显示日期、实际场景、咖啡馆区域/剧情地点、当前待办、导航/地图行动、对话选项、数值变化、制作状态和真实交付。可以收起或隐藏。只显示最近150条，完整记录写入：

`Application.persistentDataPath/NarrativeWalkthroughs/<UTC开始时间>.json`

报告区分 `skip-opening-and-tutorials`、`full-opening`、`current-session`、`current-session-skip-tutorials`；跳过教程的轮次明确标为辅助测试。指定目标与实际结局可能不同；没有从报告反向授予剧情事实。现场测试与后台批跑互斥。

## 对话直接跳到选项

现场测试默认开启 **直接跳到选项并立即选择**：

1. NarrativePresenter 遍历原剧情页、条件和检定，略过无选项的普通文本页；选项页在原适配层中调用 `FastForwardToChoiceOrComplete`，直接显示选项，不调用旧 Skip 的逐页快切。
2. 有选项时在真实选项面板上选择可用项，同一推进步立即提交。锁定项不参与自动选择；角色检定仍由原控制器执行。
3. 段尾、后续页、分支与根尾仍经原 NarrativePresenter / NarrativeController。属性、好感、标记等按原作者配置的结算时机提交；不是点击时直接改值。
4. 无选项的段落正常完成；Gameplay 页仍等待真实玩法回执，Letter/Reading/Notice 仍用原面板，取消和结局预览不写 completed。

关闭“自动选择对话选项”可在选项处人工接管；关闭快速对话可恢复逐步阅读。普通玩家原有 Skip 按钮行为保持原样。

## 玩家待办改进

玩家与现场测试共用 GameplayTodoProjection 的推荐规则：优先可执行的必做事项，完成后推荐下一项；可重复日常、咖啡谱和区域浏览作为自选内容，不盖住休息/剧情指引。显示“下一步”，直接展开操作说明与导航入口。

锁定事项说明行动点、物品或剧情条件不足，并保留原始条件校验。地图事项提供地点、行动和进度；营业订单显示实际订单杯型、现有配方流程，以及进入制作台、取杯、小游戏、交付盘/按铃的提示。完成状态仍读取原行动计数及真实订单，待办不拥有第二套任务状态。

## 验证与边界

- Unity 6000.3.5f2 实际编译通过；无新增 C# 编译错误。
- Play Mode 快速开局实际从主菜单进入 Map，再到 CoffeeShop，正常开始营业并进入制作台，没有开场/教程阻挡。
- 完整开场 IF0 已实际经真实选项“强硬拒绝”和“就此别离”到达 `arrival.ending.if0`；报告确认真实 GameOver，测试租约释放。
- 既有 `PresentationDebugScene.unity` 内运行真实 Presenter + Controller + DialoguePanel 的功能核对：90句台词直接推进，只物化1个选项页；锁定选项拒绝、有效项只选一次；选择时值仍为3，根尾真实结算后变为5，无取消错误。证据为 `Library/NarrativeWalkthrough/instant-settlement.json`。
- 测试期间清零自动保存计时，停止后重新按正常间隔计时，避免测试耗时导致停止后立刻自动保存。
- 现场暂停后恢复原对话显示；停止后 `IsWalkthroughActive=false`、`SkipDialogueText=false`，保存门禁恢复；已实际核对。
- 复用已有离线 NUnit 宿主：待办排序8例与原后台/宿主34例合计42/42通过。离线验证不等同于 Unity PlayMode 测试套件；新增共享场景回归用例已编译，尚未执行 Unity Test Runner。
- 现场测试遇到真实制作、阅读/教学人工输入或未满足内容门禁时暂停并说明原因。没有自动完成手动咖啡制作，没有解除作者 construction/integration/blocked 闸门；不宣称四线已自然通关。

本轮验证修正自动保存计时前，停止现场测试后产生过一次0号自动存档（2026-10-20 18:41，Asia/Shanghai）；手动存档未改。没有删除或伪造该自动存档，也不将本轮记成“整个验证期间所有存档均不变”。

本次可复查证据另保存在 `output/narrative-walkthrough/`：`instant-settlement.json`、`quick-start-state.json`、`stopped-state.json`、`quick-start-steps.json`、`todo-regression.log` 和 `scene-and-operations.png`。

手动面板核对只使用既有 `PresentationDebugScene`，未新增场景、Canvas 或输入系统。Editor 功能核对菜单：`Tools > Narrative > Walkthrough > Verify Instant Dialogue Settlement`；需在该共享场景 Play Mode 中执行。`Write Diagnostic Snapshot` 与 `Capture Rendered Game Frame` 输出至 `Library/NarrativeWalkthrough/`。

## 代码入口

- `Assets/Scripts/FreeCafe/Unity/NarrativeWalkthroughRunner.cs`：可见操作驱动、目标选项策略、现场记录、暂停与释放。
- `Assets/Scripts/FreeCafe/Unity/NarrativeAutoGM.cs`：游戏内入口。
- `Assets/Scripts/GameFlow/UI/GameFlowBootstrap.cs` / `DayFlowController.cs`：显式快速新游戏入口与教学跳过。
- `Assets/Scripts/GameFlow/UI/Presentation/DialoguePanel.cs`：直接到选项/段尾，不执行剧情结果。
- `Assets/Scripts/GameFlow/UI/GameplayTodoProjection.cs` / `MapTaskPanel.cs` / `FreeCafeHud.cs`：只读待办推荐与步骤说明。
