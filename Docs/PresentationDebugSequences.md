# 调试场景：连续演出预览

## 使用方式

使用共享场景 `Assets/Scenes/PresentationDebugScene.unity`，进入 Play Mode 后按 **E / C**，或点击右侧对应入口，打开二级菜单。

- 菜单从当前已生成的 Luban 数据读取事件，不维护场景内的演出 ID 清单。
- **入口候选**：没有被 QueueEvent 或检定分支引用的事件，优先列出。这个标记只是便于选择，不是新的剧情入口标准。
- **中途试播**：其他事件，也可以从此处开始测试；如果依赖前置状态，可能不满足初始条件，界面会明确提示。
- `sequence:<eventId>` 从该事件开始，跟随正式的选项、检定、QueueEvent 和结果触发的 PlaySetup，直到事件队列和特效队列全部结束。
- 连续演出入口优先列出；选择 `sequence:<eventId>` 才会跟随完整事件链。菜单同时兼容原有 Opening、Conversation / Reading / Notice、Event 和 Setup 单片段入口，供制作检查。它们不是连续链路，可通过类型筛选区分；不想逐个检查片段时请选择连续演出类型。
- 按 **Esc** 停止整段，**Backspace** 从所选入口重新开始，**H** 切换说明。
- 遇到选项需要选择；跳过文字不会替你选择分支，也不会把互斥分支一起播放。

## 与正式剧情的关系

连续播放复用 `NarrativeController`、`NarrativePresenter` 和正式 Panel bridge，不以 ID 命名约定猜测后续剧情，不另建一套事件/选项调度器。

每次播放建立独立配置副本和临时状态：

- 不创建或替换 `NarrativeRuntime.Instance`，不访问正式存档或玩家后端。
- 原本来自正式 DayFlow、经营和行动系统的外部值转为临时本地初始值。
- 选项与检定结果在临时控制器内结算，允许条件与后续事件正确接续；不会提交给正式游戏。
- 临时背包、临时金钱及标记不外传。只把 PlaySetup / BarHint 表现结果交给预览播放器；NPC/世界等效果不转发给正式场景。
- 场景退出、Esc、切换或重播会释放 presenter、回调、暂停 owner 和调试标签下的 UI。

Inspector 的 `previewAttributeValue` 用于检定；默认遵循页面与选项条件。可开启 `previewAllPages` / `previewUnlockChoices` 做美术内容检查（这不再等价于正式条件下的分支播放）。

## 后续新增配表

1. 按现有 NCS 规范添加 Event、Presentation、页面、选项、检定和结果关联。
2. 重新生成 Luban JSON，让更新后的数据进入游戏。
3. 关闭后重新打开二级菜单，会重新读取生成数据，自动显示新增事件。无需新增场景按钮、修改 ID 数组或更新调试脚本。

自动发现覆盖现有 NCS 数据结构及正式播放器支持的演出类型。若新增表结构、演出类型或原子，仍需先接入正式 loader / presenter；调试菜单不会凭空实现新的播放器。

自动识别不意味着替配置补齐缺失的关联：一个事件播完之后，如果正式配置没有 QueueEvent / 检定分支，而是等待下一天、玩家行动或其他外部触发，预览会在当前队列结束处停止，不会擅自推进正式游戏。循环/重试分支按玩家选择继续，直到退出循环或手动停止。

## 共用播放器修正

- 明确配置的 Setup 模板优先；PresentationClass 只补充未指定的默认模板。
- Conversation / Notice / Reading 在模板的内容原子处播放，而不是绕过整个模板。
- Conversation、Reading 按 `seq` 完整播放全部可见页面，保留页面内容、角色、语音及图片信息。
- 页面中已经选择过的选项组，不会在模板 A_CHOICE 中重复弹出。
- 完成最后一个页面后仍继续模板尾部原子；连续预览还会等待结果驱动的 PlaySetup drain 完成。

## 回归

- `PresentationDebugSequenceTests`：真实首次到店连续剧情、选项重试、被动检定分支、模板前后原子、整段停止。
- `PresentationDebugCatalogTests`：新增事件/内容自动发现、不重复暴露已绑定片段、不修改生成配置。
- `PresentationDebugSceneTests`：共享场景按钮、搜索、选择、页面交接与所选入口重播。
