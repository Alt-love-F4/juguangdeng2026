# 全量叙事接入交接入口

更新时间：2026-10-19，Asia/Shanghai。范围：当前仓库文案接入既有 NCS／DayFlow／CafeWorld。

## 先看结论与状态边界

这轮已把四线当前源稿、270 组日常和六期报纸统一编译进源 Excel，经官方 Luban 生成；补上真实咖啡玩法等待、同根图路由／终点、条件结果、成长和日终门禁。**这不是“全部分支已自然通关”的验收声明**。少数原稿判定／金额未明的根或选项继续明确关闭。不能批量打开 construction／integration／blocked 条件充当完成。

原有咖啡馆拖拽、制作、托盘按铃、订单与经济模拟保持原所有者。新的剧情服务只添加 owner-scoped 真实订单，不复制造咖啡算法、库存、存档或全局剧情系统。

本入口是后续 Agent 的接手顺序；详细源锚点、各分支待定问题与专项测试见下列文件。

## 当前配置量级

此前全量集成快照（地图面板修正前；当前数目见 `Docs/NarrativeMapDialoguePanelFix.md`）：346 Event，其中62个正文根、240个地图日常/首访/新闻反应根、15个报纸/公开消息根、29个开场子链/历史测试演示事件；5506 ConversationPage、8156 ConversationLine、300 Choice、36 AbilityCheck、234 ReadingPage/1049 ReadingLine、10788 Text、17 Ending（15个正式条件结局+2个禁用历史测试结局）。三处根级作者阻塞仍关闭；节点数不是完成率，也不代表全部自然可达。

新编译器覆盖四线61份现稿，保留原oct07两根，并把10/25邀请从10/26稿拆成独立日期事件，所以正文根数为62。日常270组由240个NCS根与30个咖啡馆非模态快照承载。普通成长、初访与新闻替换复用同一Action/Controller。

## 文件分工与入口

- `E:\myproject\unity_p_taptap2026\Tools\Narrative\Content\INTEGRATION_CONTRACT.md`：跨 Agent 的 ID／数据行／玩法接口契约。
- `E:\myproject\unity_p_taptap2026\Docs\NarrativeRuntimeIntegrationHandoff.md`：End／Gameplay／图遍历、guard、条件结果、训练及取消生命周期。
- `E:\myproject\unity_p_taptap2026\Docs\NarrativeCafeIntegrationHandoff.md`：真实 CafeWorld 交付桥、DayFlow、拘押／日终及测试。
- `E:\myproject\unity_p_taptap2026\Docs\NarrativeNeutralContentHandoff.md`
- `E:\myproject\unity_p_taptap2026\Docs\NarrativeRoseContentHandoff.md`
- `E:\myproject\unity_p_taptap2026\Docs\NarrativeChurchContentHandoff.md`
- `E:\myproject\unity_p_taptap2026\Docs\NarrativeRevolutionContentHandoff.md`
- `E:\myproject\unity_p_taptap2026\Docs\NarrativeContentAudit-2026-10-19.md`：实施前审计基线，不是实施后的完成清单。

## 唯一数据生产链

1. 修改 `Tools/Narrative/Content/<faction>.py` 或日常模块中的源映射。先核对源 DOCX／XLSX 当前哈希；不要悄悄删掉 source-drift 校验。
2. 使用 workspace bundled Python 运行 `Tools/Narrative/integrate_content.py`。它只读当前源 Excel／源稿，生成 `output/narrative-integration/merge-plan.json`，检查行字段和跨模块 QueueEvent；不会直接写表。
3. 使用 workspace bundled Node 运行 `Tools/Narrative/merge_workbooks.mjs`。它用 artifact-tool 块写两个源工作簿，修改前核对哈希并留首份备份；不要并行运行多个合并器。
4. 使用 `Assets/ConfigTables/GenerateAll.bat` 官方 strict 生成 C#／JSON。可通过 `PYTHON_EXE` 指定 bundled Python；设置文件保留官方 Luban 5.1.0 路径。
5. Unity 刷新、编译，运行 `LubanNarrativeLoader.Load()` 和现有静态扫描；再跑测试与自然流程。Resources 文本可能在编辑器内缓存旧资源，最终检查要刷新或重启 Play，不能把旧缓存报错误判为新 JSON 仍错误。

`merge_workbooks.mjs` 默认从用户 workspace bundle 导入 artifact-tool，可用 `OAI_WORKSPACE_NODE_MODULES` 指向 dependency loader 返回目录；不需要项目 npm install。不得提交 node_modules 或把本地审计快照变成运行时依赖。

新增页字段顺序：现有 `checkId` 后为 `gameplayKey`、`nextPageId`。Schema／源表／Generated 必须保持同步。

## 核心事实与所有者

- 游戏 day1 = 670-10-06；不要混用现实日期。
- 根完成：`story.<root>.completed`。临时 choice／check／gameplay 证据仅根内使用；根尾一次结算。
- 路线：`route.neutral/rose/church/revolution`，由共通 11/11 实际分流独占设置，不在四个分支入口各自伪造。
- 可反转 host 状态：`church.detention.active`、`story.adele.away`、`story.sylvain.unavailable`。这些绝不能被旧声明合并成 onlySetTrue。
- 关系：角色 `.affection` 统一关系 Bar，targetId=player；阵营 `faction.*`。当前初值0、范围-20..20是可调实现预设，不宣称文案已给出全部平衡数值。
- 属性：`player.language/courage/wisdom/perception` 仍为整数0..20。训练余数 `training.*` 0..9。日常低于10给2个十分点，否则1；继承旧日常增长草案，仍需全流程平衡验收。
- 钱由原 cafe provider／MoneyAccount 持有；日期由 DayFlow provider 持有；AP 由原 Actions 持有。MONEY_ADD／REMOVE 不改成 BAR_ADD 外部 cafe.money。
- `known.*` 应来自实际相识根尾。没有前置生产者的日常会安全不可用；不要为让地图亮起而全设已相识。

## 咖啡馆真实交互

### 核心实现

- `Assets/Scripts/FreeCafe/Domain/CafeWorld.NarrativeService.cs`
- `Assets/Scripts/FreeCafe/Domain/CafeWorld.ProductionQuality.cs` 的实际 ServeProduction 接口分派。
- `Assets/Scripts/GameFlow/UI/NarrativeCafeGameplayBridge.cs` 与 `.Session.cs`。
- `Assets/Scripts/Narrative/NarrativePresenter.Gameplay.cs`。

`TryBeginNarrativeService` 给现有 CafeWorld 加入 owner-scoped 真实顾客／订单。它不打开营业、不增加经营收益或声望、不消耗其他人的订单；玩家仍使用原出品流程。创建订单、端杯、入场都不是完成。不可重用杯子回执，不可用 GM 加钱或统计增量模拟交付。

- `serve:<actualProduct>:score:count`：低品质保持请求，真实重做。
- `taste:<actualProduct>:score:count`：真实交付一次，低分返回 passed=false，由原稿低分页接回；不是取消。
- 多杯用 `+` 串联，每杯实体唯一。
- `any`：接受实际分类成功的餐品，不要求误选固定黑咖啡。
- `any.hot`：实际饮品温度至少40°C的实现预设，质量仍走原 evaluator。
- `blackcoffee.sweet`／`.unsweetened`：真实糖份有／无，两个配额必须分别交杯。全局 classifier 拒绝额外糖时，仅在该明确变体的只读分类投影中剥糖判断咖啡基底；真实杯子、糖、出品评分仍不替换。
- `host:end_business`：现有 endpoint 关闭真实营业；已关闭为合法幂等成功，不推进日期。

普通点单用现有 Hint／Toast 非模态显示，绑定实时订单饮品；不打开 DialoguePanel，也不暂停顾客耐心。闲谈30份快照中的20组已入表，但非模态环境播放仍需按源条件谨慎接续，不能无条件让未相识角色讲话。

### 生命周期

Gameplay 等待仅让出当前 Presenter 的暂停／舞台；Runtime.IsBusy、存档和其他剧情仍锁定。宿主真正就绪前不得放开输入。取消仅移除本服务订单并恢复原 focus，不返还已经实际消耗的饮品。EndDay／Restore 释放服务；迟到回执不结算。真实品鉴返回后才恢复原剧情的下一页。

独立 Reading 事件现在带可选 PresentationCancelled 回调。正式读报取消必须走取消事务（旧日常Reading已于2026-10-19迁为Conversation），而不是 Complete → 发成长；旧外部单回调调用仍保持原约定。

## 日常池与地图

`daily.py` 将270组编译到现有 Event／Conversation／Action／Text；没有第二套调度器。**2026-10-19地图面板修正：240个地图日常/首访/新闻反应根统一Conversation；原始总览B22的“普通段用阅读”呈现约定被本次用户反馈替代，原始文案文件不回写。真正报纸仍Reading，公开公告仍Notice。** 详见 `Docs/NarrativeMapDialoguePanelFix.md`。

- 九地每地一个 Repeat 日常行动。先首访，再一个符合条件的内容；当天新闻优先并替换普通文本。
- 使用已有 Snapshot.counters 的 daily.playcount.* 做确定性最少播放轮换，成功后才推进历史；不宣称实现了旧稿的12个随机Loops。
- 行动取消退本次日常AP，不提交普通内容成长；已完整读完的首访可保留其真实发生事实，首访本身不发成长。
- 成长在根尾通过条件 TRAIN_ATTRIBUTE 提交，源中20组选项保留回应／选择事实，不写到主线核心判定里。
- normal10/21日常仅圣徒街；拘押不给日常；人物离开／西尔万10/16—18不可用；11/23及终局停止。
- 已有地图按钮别名：旧城区复用原“大堂区”锚点；大教堂复用原temporary锚点；library显示灰墙学院，market显示大堂区。没有运行时生成新地图美术。后续应由美术确认位置／图标，不要擅自复用经营底图。
- 部分日常需要 known.pascal／known.eugene／known.lefebvre 或天气公开状态。若暂无真实生产者会保持不可用，接手时追溯原稿，不可把条件删掉直接随机。

## 源稿未决事项的处理原则

当前仍有未放行根或局部选项，详细看四线交接及 `output/narrative-integration/content-audit.json`。典型包括：

- 新玫瑰11/15坦诚度初值、区间缺口和知觉失败去向。
- 中立11/29商业营收门槛／统计周期／权威provider，不能拿钱包余额代替营收。
- 王庭11/23多处重复引用RY1115旧选项，不能擅自决定两结局分流。
- 中立11/18仅能放行已写明的王女庇护路径；未知贿赂和缺失 end if4 不静默替代。
- 教会10/23对白索价3000但正式扣款仍待定，只保留无金额歧义的实际说服／救援路径；未知贿赂选项不可选。
- 教会11/4 xx金额、记者广告价、促销智慧>=2/>=3冲突、革命个别缺失回应／阈值、特殊热可可与热牛奶身份等未决节点，不统一设成功。

缺图可用已有明确登记的占位并审计，不应再因未画专属画面关闭整个长根；缺少玩法规则则不可当作美术占位解决。

## 测试与审计

本轮测试数字与最新结果单独写在 `Docs/NarrativeIntegrationVerification.md`，以免旧专项记录被误认为当前完整通关结果。历史失败、修复后复测和未运行项必须分开记录。

复现内容测试：bundled Python `-m unittest discover -s Tools/Narrative/Content -p test_*.py -v`。

Unity建议顺序：

1. NCS loader + editor validator + 当前静态报告。
2. Narrative core/editor、FreeCafe.EditMode、GameFlow.Narrative 专项。
3. `Spotlight.Narrative.Gameplay.Tests` 在既有 PresentationDebugScene 的暂停／取消／End／Reading回归。
4. 原 FreeCafe.PlayMode 的拖拽、杯具、交付、教程等回归。
5. 正式新游戏逐日推进，记录每段真实入口、选择、真实出品、完成事实、存档与后续解锁。不得只用GM假完成代替这项验收。

## 继续工作的最短路径

1. 先读本入口和 Verification，再定位目标路线交接中的源锚点。
2. 先修复与当前改动直接相关的测试，不为变绿而删除判定。
3. 对安全可达主链做新游戏真实回归；已编译但有作者阻塞的分支另外列验收，不伪造通关。
4. 完成真实相识／天气生产者和20组咖啡馆闲谈的非阻塞调用；保持已有点单和咖啡交互。
5. 再集中确认金额／收入／结局歧义，将闸门替换成真实规则及测试，然后源表重生成。

## 本次交付最终验证摘要

- 官方Luban strict和Player语义校验通过；Unity实际全量Load成功，全局静态校验无错误。
- 120项内容构建测试通过。
- 7项PlayMode叙事/实际生成Rose1011×真实CafeWorld交付回归通过。
- 1144项EditMode执行完成，记录1项浮点精度断言失败（100 vs 99.9999924）；不是全绿。
- 原咖啡馆40项PlayMode执行，34通过、6失败（旧器皿引用/拉花Prefab/教程文案），详见Verification；未为了本轮叙事改写其业务或删测试。
- 全新游戏到所有结局的人工自然通关尚未完成；3个作者规则未明的根仍锁定，20组闲谈调用及部分相识生产者仍待接续。
- 修正过实际日期可达性：教会1009在Rose1009地图行动之后的同日夜晚触发，不能放到更早已结束的AfterBusiness阶段。

不要将本文数据规模、自动测试通过或源稿映射覆盖改写成“全部剧情/全部交互已上线验收”。


## 只读关系图入口

新增 `Assets/Scenes/NarrativeRelationshipScene.unity`，以及无需进入Play的菜单 `Tools > Narrative > Story Relationship Window`。从现有配置读取故事／事实／续接／结局关系，点击查看正文与条件证据；不启动NCS、不结算、不改存档。完整使用及验证见 `Docs/NarrativeRelationshipGraph.md`。新增关系图不是替代既有演出调试场景，Panel回归仍走PresentationDebugScene。


## 结局与本地需求图增补（最新口径）

详细交接及逐项限制见 `Docs/NarrativeEndingAndArtDelivery.md`。本次沿用原NCS Ending和ResultPanel，补上开场确认离开的IF0，当前为**16条启用Ending＋2条禁用历史测试**。末尾根可触发、上游自然可达、作者判定未明须继续分层报告；不把理想世界／RY1／RY2的原稿闸门设为true。

已核对93项旧美术标记，并从当前原稿补6组明确标记，**共37张本地素色中文需求卡**，没有调用任何图片API。已有背景和角色不覆盖；角色差分卡仅供需求预览。CG03／CG04／CG05和当前新增CG07／CG09有已核实的演出挂点及结束恢复，其他暂定／道具卡另列需求，不自动塞满全屏。

复核菜单：`Tools > Narrative > Ending Panels and Trigger Review`、`Tools > Narrative > Art Requirement Placeholders`。面板预览仍使用原PresentationDebugScene；游戏结局由真实根尾事实触发，不由预览领取。

## GM剧情图谱与试播交接（2026-10-19）

关系图已通过全局GM挂接，入口、状态语义、独立内存试播与正式Gameplay边界见 `Docs/NarrativeGMAtlas.md`。不需要切独立场景，不创建新的正式NCS会话，咖啡馆现有交互代码不改。共享GraphView同时更新EditorWindow/独立关系场景风格。

## 地图日常执行次数与AP（2026-10-19补审）

市政厅24组同名段落是独立/日期解锁的造访，不是一条任务的24个付费步骤；首访+本次正文自动连续执行只扣一次AP。可选Repeat日常现在在待办标本次完成，按钮明确再次行动/另计消耗，不阻止休息；真实必做/交付/进度门禁保留。审计证据、九地单次AP与取消/续接回归见 `Docs/NarrativeDailyActionCosts.md`。不凭历史猜测补点，不将全部未来日期内容拼接成一次行动。
