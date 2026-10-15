# Presentation Debug Scene

项目统一入口：`Assets/Scenes/PresentationDebugScene.unity`。使用实际演出 Prefab、正式 `NarrativePresenter` 与正式面板桥接，不另建测试版渲染器。

## 配表工作流

1. 修改 `Assets/ConfigTables/Data/Narrative/Narrative.xlsx` 和 `Assets/ConfigTables/Data/Shared/Text.xlsx`。
2. 运行 `Spotlight > Narrative > Generate Luban` 或 `Assets/ConfigTables/GenerateAll.bat`，等待成功生成与导入。
3. 在本场景进入 Play Mode，按 E 或 C 打开演出目录，按类型或 ID/标题搜索。重新打开目录会重新加载生成表，不需新增场景按钮。
4. `sequence:<eventId>` 从事件开始连续播放，使用隔离的正式 controller 处理选项、检定、后续事件及 PLAY_SETUP。状态与结果只存在于临时会话，不访问正式 Runtime/存档；停止或重播即丢弃。
5. `opening:`、`conversation:`、`reading:`、`notice:` 为单项内容，`event:` 保留该事件的 setup/属性上下文，`setup:` 独立检查参数化模板。单项模式不提交剧情结果、不跟随结果跳转。

自动目录遍历 OpeningPresentation、PresentationIndex、具有演出内容的 Event 与所有 SetupParams；新条目不需要修改枚举、按钮或硬编码白名单。Template 只有定义、没有具体参数时不是完整内容；请用 SetupParams 配好资源与参数再预览。

## 预览上下文与一致性边界

Controller Inspector 的 `previewAllPages` / `previewUnlockChoices` 控制是否强制展示条件页、解锁选项；关闭时按隔离会话的初始/演进状态判定。`previewAttributeValue` 为预览检定属性值，`previewPlayerName` 替换玩家姓名。它们不代表正式存档的状态。

- 正式 dispatcher 和面板共用背景、BGM、旁白、立绘、表情、强调特效、音效、模板原子与分页代码；不是仅仅在调试器里模仿效果。
- 正式 Reading 已补齐顺序多页与页条件。开场已有的空文本行按分隔行跳过，缺失 textKey 仍报错；正式与预览采用相同规则。
- 非空 `pageBarConds` 仍是未实现的表达式，明确报错，不伪装通过。非法配表、缺资源和不支持的新原子不保证播放成功，须按诊断修正或先扩展正式播放器。
- `Tpl_CutScene` 是表内模板原子演出，不等同于视频面板；N 是视频面板占位样例，不代表配表已支持视频内容。
- Reading/Notice 面板自身取消沿用正式桥接的单页结束语义；Esc 被场景捕获时停止整段，清理所有调试面板。

## UI 与快捷键

右侧为四组可滚动入口，底部固定状态、当前条目、停止、重播与操作提示。说明面板内部 Canvas 排序为 60，低于正式演出；配表播放默认收起说明。

- E / C：全部配表演出目录（含连续入口与单项内容）。
- F / G：指定开场 / 特效试验入口；1~0：黑影、受惊、震屏、白闪、黑场、换位、镜头与退场。
- Q / W / R / T / Y / U / I / O / P：原有日期、来信和日程 UI。
- V / B / N：硬编码阅读、通知、视频面板样例，**不是配表内容目录**。
- Z：独立黑屏序章；M：详情；L / K：长短加载。
- Esc：停止；Backspace：从入口重播；H：说明开关；F12：恢复说明；F9：timeScale 暂停测试。

保留场景中用户其他物体、原 33 个按钮实例、持久事件及 GUID。已加载且干净的场景可执行 `Tools > Game Flow > Upgrade Presentation Debug Layout` 仅升级工作台 UI；升级标记 dirty，不自动保存。不要用 Rebuild 覆盖用户自定义对象。

## 代码与验证

- 目录：`PresentationDebugCatalog.cs`。
- 隔离播放：`PresentationDebugSession.cs`。
- 场景入口：`PresentationDebugSceneController.cs`。
- UI：`PresentationDebugDialogueMenu.cs` 与 `Assets/Editor/GameFlow/PresentationDebugSceneBuilder.cs`。
- 测试：`PresentationDebugCatalogTests`、`PresentationDebugSessionTests`、`NarrativePresenterReadingTests`、`NcsOpeningDataTests` 与共享场景 `PresentationDebugSceneTests`。

新增表内内容应首先生成并通过实际数据/资源校验，再在此场景检查正常播放、选择分支、取消、重播及暂停。新增播放器类型或原子时先扩展正式链路，再补测试；自动发现不能替代内容质量验证。
