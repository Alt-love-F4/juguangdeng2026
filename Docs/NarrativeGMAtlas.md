# GM剧情图谱、只读状态与演出试播

日期：2026-10-19。此文档是新增GM入口的交接，不替代NCS配置或存档协议。

## 使用入口

进入游戏按 `~` / 反引号打开GM。全局调试第一项“剧情图谱 · 分支 / 状态 / 试播”，或侧栏“剧情图谱”，均可进入共享图谱。无需切到独立场景、重新启动NCS或离开咖啡馆。

- 卡片与目录：查看根事件、正文逐页、选项、入口/结果、检定和连线证据；支持搜索、阵营/类型筛选、缩放、平移、相邻聚焦。
- 状态：真实会话中的当前播放/排队/已发生、一次性/可重复、条件未满足、建设阻塞和无会话。详细条件以组内AND/组间OR展示。展开“条件 / 状态诊断”或切换“入口 / 结果”查看详情。
- 结局推演：在详情的“关系证据”页切换“结局推演”，按正式结局优先级列出启用与禁用项；展开可查看实时条件参数、阈值与组别，并跳转关系证据或复制诊断。预测遇到未知的高优先级条件会停止，不猜测较低优先级结局；全程不触发结局、不写正式状态。
- 本页预览：只播放指定配置页，忽略该页显示条件；选项仅展示，不结算、不写入已读。直接跳到互斥分支页是内容检查，不证明自然到达。
- 从头分支试播：在现有NCS核心的独立内存副本中执行页条件、选项和检定；复用正式对白、阅读、公告、信件和结局面板。选择不能改变正式状态，结束后返回图谱。
- 试播时自动收起GM；重新按 `~` 可回图谱，继续观看或停止本工具试播。没有抢占/强制关闭正式剧情的入口。
- 真实玩法步骤：明确停在pageId/gameplayKey并提示，**不创建正式咖啡订单、不伪造制作成功**。咖啡交付仍应在正式流程或原独立测试入口验收。
- 已有正式剧情、读报、信件或结算面板占用舞台时禁止开始新试播，显示确切原因。建设阻塞/禁用节点依旧不能假放行。

## 状态语义

当前存档没有逐页/逐句已读账本；根事件已发生不代表每个互斥分支都看过。Opening无独立历史，不能拿同名Event完成记录代替。页条件是当前值预判，不是播放历史；过期的局部choice/check、不可用provider保持未知。GM若有公开的当前DialoguePanel来源页，只会额外显示当前舞台游标，不伪造历史。

`NarrativeGraphRuntimeStatus` 仅接收已存在的controller，每0.5秒刷新懒缓存。GM隐藏且没有活动试播或待办请求时，Update立即返回，不刷新会话或重绘图谱；只有打开GM图谱时才进行节流状态刷新。状态读取不会查找/创建runtime、调用正式CaptureSnapshot、推进日期、Flush、抽随机数或提交结局。结局推演只调用条件诊断接口，不调用正式结局检查。图谱刷新沿用宿主当前Configuration；只有无会话时才读取生成表，不会将运行中的旧快照悄悄换成磁盘新表。

无正式会话时可查看配置、使用独立默认测试数值试播；不能把默认数值解释为玩家现状。正式会话存在时试播复制State的纯数据、可读外部bar和玩家姓名；不借用正式Controller、Actions、Inventory、CafeWorld或存档写入器。

## 复用与隔离实现

- `Assets/Scripts/Narrative/Graph/NarrativeRelationshipGraphView.cs`：场景/EditorWindow/GM共享暖纸/咖啡/黄铜UI，注入状态/试播/刷新hooks；回调延迟到OnGUI结束之后。没有新图模型或调度系统。
- `Assets/Scripts/Narrative/Graph/NarrativeGraphRuntimeStatus.cs`：只读controller状态和纯诊断。
- `Assets/Scripts/FreeCafe/Unity/NarrativeAtlasGM.cs`：注册GM分类，管理overlay、浏览暂停、0.5秒状态刷新、试播/返回和场景退出清理。
- `Assets/Scripts/GameFlow/UI/Presentation/NarrativeAtlasPreview.cs`：源配置深拷贝 + 现有NarrativeDebugSession/Presenter + 正式播放bridge。副本中清除Result/Trigger/EventDate/Ending，将指定root入口改为独立手动请求；不会修改源配置或正式会话。
- `Assets/Scripts/FreeCafe/Unity/NarrativeDebugGM.cs`：只新增挂载AtlasGM，原叙事调试分类和分支测试保留。
- `Assets/Scripts/FreeCafe/Unity/FreeCafeGMPanel.cs`：统一游戏画风、滚动目录/操作/回执、纸色按钮；新增SetExternalOverlay。overlay时根仍active且Canvas/Raycaster仍开启，以透明全屏UGUI屏障防图谱点击穿透；正式输入门禁仍看到GM可见。
- `ReadingPanel.cs` / `NoticePanel.cs`：复用DialoguePanel已有外部输入门禁，GM打开时不响应Space/Enter/Esc或公开确认方法。不是另设一份全局GM依赖。

UI没有外部生图API调用、没有修改字体资源或正式面板Prefab；复用项目中文字体，只有自建fallback字体、皮肤/纹理由各自owner释放。没有新增测试Scene/Canvas/InputSystem，测试统一Additive加载已有PresentationDebugScene。

## 验证记录

- Unity已完成编译（11:31后实际程序集包含字段式配置复制修复）；新的连线裁剪修正在统一测试前再次导入。正式配置深拷贝实际检查通过：346事件、5727页、11地图位置、4个能力名称映射字典均保留，行对象引用独立。
- EditMode最终：**41/41通过**（状态33、关系图/构建器8），job `a3d77cab5351467386070b952cde845c`；`editmode-final.json` 和XML已保存。最终Console Error=0。
- PlayMode第一轮在并发脚本重新导入期间被Unity标记Cancelled by user，不能计作通过；测试已按服务与GM分开重跑。
- 在已有CoffeeShop Play会话中执行真实GM就地验证：打开分类/图谱 → 单页 `daily.st.01.opening` 的真实DialoguePanel → 重新打开GM保持选择 → 从头试播 `daily.old.08`，点击真实选项A，仅播放opening和response.a，不播放response.b。完整源配置、runtime引用及逐字段咖啡订单均不变，恢复原GM可见/分类、timeScale与暂停owner。日志 `live-ui-verification.txt`。该会话没有正式NCS，故不能将“无会话”当作存档状态验证。
- 已按实际渲染帧截取 `gm-atlas-mainline.png` 并目视检查纸张风格、中文、按钮和正文。首次异步CaptureScreenshot遇到active RenderTarget 256×144与Screen 1920×1080不一致，异步命名live的几张图无效；不作为验收截图。改用现有测试中的EndOfFrame截图帮助方法，未新建任何测试布局。发现嵌套GUI.matrix旋转连线越过侧栏边界，已改为保留原矩阵的短轴对齐线段绘制，再次回归。
- 最终完整新增PlayMode：**9/9通过**，job `08a6f00a64e74765bbac44e1aff80651`，逐条fullName已核对，含1个GM集成与8个试播隔离用例，NUnit XML也确认total9/passed9/failed0。覆盖正式状态/外部值复制、左右互斥选择、单页只读、真实daily面板、Reading/Notice/Opening/Ending路由、GM输入门禁、正式忙时拒绝、Gameplay明确取消、不释放无关暂停及幂等Dispose。
- 原始结果 `playmode-final.json` / `playmode-final.xml`；最终目视验收截图为 **`gm-shell.png`、`gm-atlas.png`**（11:47生成），中文清晰、预览按钮可见、详情可滚动，修正后的连线不越过侧栏。`gm-atlas-mainline.png` 是裁剪修正前的过程图，不作为最终截图。
- 旧job遇到跨线程共享测试管理器时可能被其他GameplayHud测试覆盖，必须核对返回的test full name，不能把非本功能的测试结果算通过。

## 接手注意

1. 不新增平行剧情字段/存档已读体系，不把当前页条件满足写成已读。若需要真实逐页历史，另做正式需求与存档兼容设计。
2. 不将GM独立预览的root入口简化写回正式表，也不把清空Result的副本误当作正式Configuration。
3. 不把Gameplay试播伪造成已完成咖啡交付；面板可播与真实玩法自然可达是不同验收层级。
4. 修改演出继续用共享PresentationDebugScene回归；关系图独立场景也继续复用GraphView。
5. 本项目存在并行工作。不要覆盖其他Agent正在修改的MapSceneController/GameFlowBootstrap或用户字体/PSD。Unity测试期间不要写C#触发重编译；被取消的job不计通过。

## 2026-10-19 文案工作台更新

默认改为「文案阅览」，并保留「关系图」切换；支持独立页目录、配置页码跳转、正文/选项搜索、显式 nextPageId 定位、返回历史、字号和问题定位复制。使用步骤与严格的试播语义见 `Docs/NarrativeGMWriterWorkbench.md`。本节不把此前测试通过记录当作新版本验收。

本次验证：文案工作台首轮 EditMode 100/100、PlayMode 13/13（已核对真实叶用例与 NUnit XML），实际阅览/关系截图已检查。看图后追加的居中/相邻快捷键微调编译通过；最后重跑因启动场景未保存被拦截，未自动保存或丢弃。完整边界与证据见 `Docs/NarrativeGMWriterWorkbench.md`。
