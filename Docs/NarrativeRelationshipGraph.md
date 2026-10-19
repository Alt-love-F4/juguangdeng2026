> 2026-10-19 GM扩展：游戏内按 `~` → 全局调试首项或侧栏“剧情图谱”。共享GraphView已统一暖纸/咖啡/黄铜画风，GM可读取当前会话状态并使用现有正式面板进行独立试播。无逐页历史不伪造已读；真正咖啡Gameplay不会被自动模拟。独立场景/EditorWindow无宿主时仍只读。当前入口、隔离边界与验证详见 `Docs/NarrativeGMAtlas.md`。以下初版场景记录保留为历史说明。

﻿# Unity 故事关系图

## 用途与边界

在 Unity 中浏览当前正式 NCS 配置的故事关系：节点代表事件，连线提示配置中声明的依赖／排队等关系；点击节点查看正文与条件等信息。它是**只读配置浏览器**，不是第二套剧情配置源，也不是剧情调度器。

- 不创建 `NarrativeRuntime`、`DayFlowBootstrap`、咖啡馆世界或订单；不执行结果，不推进日期，不写入存档。
- 不修改 Excel、生成 JSON 或正式 Build Settings。
- 不播放演出，不重建工作台，不抢占咖啡馆的交互输入。
- 演出 Panel、咖啡馆图片／实景切换仍使用共享 `Assets/Scenes/PresentationDebugScene.unity` 回归。关系图场景不是另一个 Panel 测试场景。
- 图中“已配表／有正文”“静态有依赖”“已测试／可自然通关”不是同一个状态。施工闸门必须保留，不能为了连通图形而默认设为 true。

## 推荐入口：无需 Play Mode 的编辑器窗口

Unity 菜单：

```text
Tools > Narrative > Story Relationship Window
```

独立 EditorWindow 与场景复用 `NarrativeRelationshipGraphView`。不进入 Play Mode 就可浏览，适合已有正式游戏场景或未保存工作的情况。关闭窗口会释放其独立 view；不会停止或启动游戏剧情。

## 独立网页前端

额外的浏览器只读入口位于 `Tools/Narrative/RelationshipFrontend/`。从仓库根目录启动 `python -m http.server 8765 --bind 127.0.0.1`，再打开 `http://127.0.0.1:8765/Tools/Narrative/RelationshipFrontend/`。页面直接读取 `Assets/Settings/Resources/Luban/` 当前生成 JSON；源工作簿变更后先运行官方 Luban 生成管线，再用页面“重读”。

网页端不依赖 Unity Runtime，也不执行条件、结果或存档写入。它在浏览器侧从生成表建立展示关系；字段与关系语义应与 `NarrativeRelationshipGraph.cs` 保持一致。日期只用于排布，缺少表内生产者不等于确认缺少宿主实现。网页前端与 Unity EditorWindow 是独立入口，Panel 回归仍使用 `PresentationDebugScene.unity`。

## 场景入口

目标场景：

```text
Assets/Scenes/NarrativeRelationshipScene.unity
```

菜单：

```text
Tools > Narrative > Create Missing Story Relationship Scene
Tools > Narrative > Open Story Relationship Scene
Tools > Narrative > Rebuild Story Relationship Scene
```

- **Create Missing**：仅在目标场景文件不存在时创建，不自动覆盖，不在脚本导入时偷偷运行。
- **Open**：必要时先创建，再以 Additive 模式打开并设为活动场景。保留其他已打开场景及未保存修改；不会弹出“保存所有场景”并擅自关掉它们。
- **Rebuild**：先确认覆盖；若目标关系图场景有未保存修改，拒绝重建并提示先手动处理。其他场景即使有未保存修改也不会被保存、关闭或覆盖。
- 所有创建／打开／重建操作均要求退出 Play Mode。
- 重建先在新的 Additive 场景中完成构建，再替换干净的目标场景；保留原目标 `.meta`／GUID，不调用全局 `SaveAssets`。
- 创建完恢复原活动场景；原本已加载的目标在重建后仍保持加载。创建动作不会自动把它加入 Build Settings。

场景仅含背景 Camera 与挂载 `NarrativeRelationshipSceneController` 的根节点。没有 Canvas、EventSystem、正式游戏启动器或另一份演出 Prefab。控制器使用 `[ExecuteAlways]`／IMGUI，实际图形交互由共享 view 负责。

> Additive 打开是为了保护编辑现场，不是隔离运行正式游戏。若其他已加载场景有启动器，按 Play 时 Unity 仍会运行那些场景。只想查看配置时优先使用上述 EditorWindow，无需启动任何场景。

## 浏览操作

- 左键点击节点：选中并查看详情。
- 右键／中键拖动：平移画布。
- 鼠标滚轮：缩放关系图。
- 可用按钮、搜索／筛选及详细说明以共享 view 的实际界面为准；场景与窗口使用同一套交互，不在 builder 中各写一份。

不要把日期相邻理解为前置依赖：**日期只用于组织／定位，不应凭时间顺序虚构一条剧情连线**。条件依赖、结果事实生产、`QUEUE_EVENT` 等关系需要区分；同一条件组内的 AND／OR 以及条件否定必须查看详情，不能仅凭一条边推断运行时满足条件。关联多个生产者的 OR 关系也不表示玩家必须经历全部生产者。

静态图不替代运行时可达性求解：外部 CafeWorld 回执、人物状态、未定作者规则、动态队列和一次性结算仍须按正式流程测试。出现未决条件、无生产者事实或施工阻塞，应回到源表／编译器修正，禁止由浏览器写事实绕过。

## 代码入口（给后续 Agent）

| 职责 | 文件 |
|---|---|
| Editor 菜单、Additive 安全构建、EditorWindow 包装 | `Assets/Scripts/Narrative/Editor/NarrativeRelationshipSceneBuilder.cs` |
| 场景控制器、只读图模型、共享 IMGUI view | `Assets/Scripts/Narrative/Graph/` |
| 场景结构与脏目标拒绝的 EditMode 测试 | `Assets/Scripts/Narrative/Editor/Tests/NarrativeRelationshipSceneBuilderTests.cs` |
| 全量叙事接入总交接 | `Docs/NarrativeIntegrationHandoff.md` |

固定共享接口：

```csharp
NarrativeRelationshipSceneController.ScenePath
new NarrativeRelationshipGraphView()
view.Draw(UnityEngine.Rect bounds)
view.Reload()
view.Dispose()
```

View 自行读取既有配置；窗口只有生命周期与边界 Rect，不添加剧情状态或另一份数据。后续增加按钮或交互应改共享 view，而不是分别改场景与 EditorWindow。若修改源表，继续走唯一生产链 `integrate_content.py → merge_workbooks.mjs → GenerateAll.bat` 后再刷新图。

## 验证与交接记录

新增三个 EditMode 测试：固定路径契约、内存构建只含 Camera／图控制器且 Build Settings 不变、脏目标拒绝且修改仍保留。结构测试只使用未保存的内存场景，**不会创建或覆盖正式 `.unity` 文件**。

最初 builder 交付阶段尚未生成场景；以下是后续复核清单，实际已执行结果以本文末尾最终记录为准：

1. `Create Missing Story Relationship Scene`，确认实际文件和根节点结构。
2. 打开编辑器窗口和场景，核对两处一致、正文可读、平移缩放与点击有效。
3. 在另一个存在未保存修改的场景打开关系图，验证修改与加载状态保留。
4. 给目标关系图场景增加未保存修改后点击 Rebuild，确认拒绝而不是丢弃。
5. 干净目标重建，确认 GUID 与 Build Settings 不变。
6. 运行 `Spotlight.Narrative.Editor.Tests.NarrativeRelationshipSceneBuilderTests` 和图模型专项测试。
7. 在最终交接更新实际测试数、失败和手动验收范围，不把编译成功写为已完成自然通关。

## 当前共享界面功能（已实现）

- 默认主线视图显示62个正文根和15个启用结局节点；全部视图还能浏览开场、教程、日常、报纸、公开消息、演示与表外条件。统计从当前生成数据计算，不写死为验收进度。
- 搜索匹配事件ID、标题和正文；阵营过滤与“仅阻塞”视图可以单独定位待定内容。
- 左键节点进入右侧详情：正文／选项、入口／结果、关系证据三个页签。
- 正文按配置page浏览，保留pageId、lineId、显示条件、显式nextPageId、真实Gameplay key、检定条件与选项；不会把所有互斥页当成一次自然游玩。
- 上一页／下一页、页滑条与查页可以快速跳到关键词；支持复制ID、当前页、配置与关系证据。
- 右键／中键或Alt+左键拖动画布，滚轮以鼠标位置缩放；F适配当前筛选全图、Home返回起点；右下角总览点击跳转。文字输入框聚焦时不会抢F/Home输入。
- 选择节点后强调相邻连线；“仅选中节点及相邻”可收起无关内容。未显示的阵营／表外条件仍受筛选控制。
- 关系证据保留Condition的组号与组内AND／组间OR；条件结果和选项结果标记分支来源。实线／虚线和颜色区分续接、前置、排斥、检定、结局与未找到表内生产者。
- 日期由EventDate／日历条件／ID／上游排布提示组织。ID推断或继承日期明确标为排布提示，不宣称正式日期入口。
- 没找到事实的表内生产者只显示“可能由宿主提供”，不会直接断言项目缺实现，也不会为了画线生产虚假的事件。

## 交接文件补充

- 图模型：`Assets/Scripts/Narrative/Graph/NarrativeRelationshipGraph.cs`。构建内容搜索索引、结果owner映射和跨事件关系；不触发运行时。
- 共享UI：`Assets/Scripts/Narrative/Graph/NarrativeRelationshipGraphView.cs`。Window与场景统一Draw／Reload／Dispose；纹理和字体明确释放。
- `NarrativeRelationshipGraphTests`：覆盖当前生成配置、分支结果来源、未找到生产者、日期不虚构因果、正文与选项可浏览且不改配置。
- `NarrativeRelationshipSceneTests`：PlayMode加载实际关系图场景，检查GUI创建、节点选择、没有额外启动NarrativeRuntime。

本次没有改正式剧情源表、JSON、Prefab或咖啡馆交互代码。构建器通过Additive创建，创建后核对原启动场景仍保持dirty状态。随后官方Unity Test Runner进行过场景备份／重载，最终恢复启动场景为clean；本实现没有手动保存或改写启动场景、没有丢弃／重置其Git内容。

## 本轮实际验证最终记录

- 实际已保存：`Assets/Scenes/NarrativeRelationshipScene.unity`，仅两个根：StoryRelationshipCamera与StoryRelationshipGraph；没有加入Build Settings。
- Unity脚本编译通过。现有生成配置实际建立429个节点、3708条原始证据关系；默认主线视图77个节点（62正文根＋15启用结局）。同端点同类型关系在画布合并显示，证据仍逐条保留。
- EditMode **8/8通过**：图模型5项＋构建器3项。初版测试使用普通未命名加法场景受Test Runner未保存场景约束失败，已改Editor独立PreviewScene进行结构／dirty验证；没有为测试保存用户场景或新建Panel布局。
- PlayMode **1/1通过**：加载实际关系图场景、Unity实际OnGUI绘制、选择Rose1011，确认没有额外创建NarrativeRuntime，离开后引用不变。
- 实际向EditorWindow发送左键MouseDown／MouseUp，选中`event:oct07.s1`并显示正文；截取窗口图像并目视检查了中文、节点、连线和右侧正文。
- 关系聚焦截图：`output/narrative-relationship-graph/editor-graph-related.png`；完整初始视图截图：同目录editor-graph.png。
- 查看与筛选不消费AP／不创建订单／不产生剧情结果；未修改本轮之前的剧情源表或咖啡交互实现。
- 测试与模型加载原始结果：`output/narrative-relationship-graph/tests.json`、playmode.json、model-load.json。截图和静态图并不代表全部剧情自然通关。

推荐直接用菜单窗口浏览，建议窗口宽度至少1200、高度至少800；全图适配仅用于总览，查看正文与关系时使用搜索、相邻过滤、居中和滚轮放大。打开独立关系图场景进入Play也可浏览；不要把它Additive到正式启动场景后误以为其他场景不会运行。
