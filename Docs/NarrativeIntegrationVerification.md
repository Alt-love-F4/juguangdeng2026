# 本轮叙事集成验证记录

日期：2026-10-19，Asia/Shanghai。此文档区分源编译、实际 Unity 测试与完整自然通关；不可用某个通过数字替代其余层级。

## 数据和编译

- 四线当前61份DOCX全部纳入编译器审计，含两个保留的oct07根；逐文件哈希和非空段定位见各路线handoff及output/narrative-integration/content-audit.json。
- 新日常270组、六期报纸均有配置。2026-10-19 地图面板修正后，240个地图日常/首访/新闻反应根统一使用Conversation（原普通段落Reading策略已撤销）；真正报纸仍用Reading、公开消息仍用Notice；点单10组为非模态快照。闲谈20组已入表但并未宣称全部调用接线。
- 官方GenerateAll.bat执行Player semantic validation和Luban strict；成功输出正式Excel对应的C#/JSON。没有JSON-only修改。
- 当前Unity实际LubanNarrativeLoader.Load成功，不是只用Python数行。
- Editor静态报告的GlobalErrors=0；剩余Errors为显式建设阻塞，不能当作已验收。原扫描器不计直接Reading事件的问题已经修复，有回归覆盖。
- UnityEditor脚本编译状态无失败；不代表目标平台Player Build已执行。

## 已运行的测试类别

### 源内容编译测试

由主集成运行：bundled Python unittest，覆盖源漂移、61稿锚点、互斥分支、话题排列、门槛、结果所有权、270组及六期报纸。最后数字以本文末尾的最终快照为准。

这些测试使用纯构建输出与真值表，不冒充实际CafeWorld或UnityUI自然播放。

### Unity NCS Gameplay PlayMode

第二次按完整测试类过滤运行，job 1c8da64639a94b52945cff12b441f5b4：**5/5通过**。

覆盖：只让出本owner暂停、真实宿主等待契约、End隔离后继页、取消/迟到回调、同步重复回调、Standalone Reading取消不结算成长/新闻、Runtime输入让出但Busy/Save仍锁定。加载既有PresentationDebugScene，没有新增临时Scene/Canvas。

第一次仅assembly过滤的job没有在初始化时间内启动测试；记为未运行，不计失败用例，也不伪称通过。

### Unity EditMode首次宽回归

job 0c4dbecfee32442ca6c19e9db9f82b59：1133项执行完，记录10项失败。该历史运行并非当前最终结果。

已定位并处理的类型：

- 关系Bar漏targetId=player：修源合并器并重新官方生成。
- 旧表快照断言只允许2个禁用Ending、固定页数、C13仅日期入口、Rose1011无促销前置：改为当前真实条件/续接/玩法边界断言，不删语义检查。
- Rose1009旧检定/CG语义必须保留：从现有configure_rose_1009.py核实>=5、失败同P073无CG、共同P074-P076；不采用无来源新门槛。
- 显式注入Presenter在ResetSession被清空导致EditMode创建UI：保留调用者注入所有权，不能把测试问题当作需要创建第二套UI。
- 一个既有小数倾倒测试要求精确100，但返回99.9999924；尚需精度测试/业务误差预算调查，不为本次剧情任务擅改评分公式。

### Unity真实CafeWorld服务专项

实际测试使用随项目发布的catalog、真实杯内容、订单与托盘交付，不以假成功事件替代。取消只撤销服务订单，杯子和已消耗原料不伪回滚；真实交付不伪造营业收入。

已通过的专项覆盖serve/taste、多杯独立性、真实冷热/糖变体、业务前后关闭/取消、存档和重复回执，以及DayFlow日终/拘押门禁。最终集中运行数字见文末，不把子Agent便携harness的结果冒充Unity运行结果。

### 咖啡馆原有PlayMode回归

job fd21b2b6d52542b89f53a5b1982933d7：选择制作、输入所有权、器皿交付、水和教程四个既有测试类。执行结果见文末最终快照。

已观察的未通过项包括旧m.brewer/视图相关空引用、miniGamePanel未赋引用、拉花背景断言及两项教程文案断言。这些失败需要逐项归因；本轮未改上述Prefab/拉花Panel/教程文案，不能凭这一点就宣称它们一定全部与新改动无关。保留失败，不删测试，不把原有咖啡交互“保证不变”写成全绿证明。

## 本轮没有完成的验收

- 未从正式新游戏手动逐日走到全部结局。
- 未把缺失规则的根/选项临时设为ready=true来通关。
- 未跑目标平台Player Build。
- 未完成全套视觉/音效/CG人工验收。
- 尚未证明全部高门槛的属性培养在可用日常和AP预算下自然可达，尤其缺少真实相识生产者的地点。
- 未为旧存档全部路线历史补造新事实；兼容规则和迁移后的自然回归仍需验证。

## 继续验证的优先顺序

1. 先看最新静态报告中的具体blockedBy，不把节点数量当完成率。
2. 正式流程从10/6跑到10/14：首客、地图必做、俱乐部和仪式、真实制作、短/长线、促销、晚谈、索雷尔/蒂博。
3. 在各Gameplay节点取消、重播、读档；检查订单、钱、AP、已完成事实及后续解锁。
4. 正常搜查/被捕两路径，真实日程15→16→17→18；拘押不营业/不进地图，释放后恢复正确phase。
5. 补完作者未决问题后再跑11/11分流和各结局，保留各终局自然可达存档。

## 最终快照

- 数据：346事件、5506对话页、8156对话行、300选项、36检定、234阅读页、1049阅读行、10788文本；17条Ending中15正式条件结局、2禁用测试结局。
- 源编译单元测试：**120/120通过**。
- 新Gameplay/Reading生命周期和实际生成剧情×真实咖啡组合：最终PlayMode job `59b09403ca8a4067a1e17ecba677768d` **7/7通过**。其中两个新增组合用例走真实day6 MorningBeforeMap入口，真实CafeWorld制作/按铃回执分别验证短线不进名片/长线、长线提交赏识。测试显式准备历史前置和属性，并控制UI确认，不能当作手动全程新游戏通关。
- 原咖啡馆四类PlayMode：**40项执行，34通过、6失败**。具体失败保留在 `output/narrative-integration/playmode-cafe.json`，未为了变绿修改无关Prefab、拉花或旧教程文案。
- 静态报告仍保留3个根的4条作者阻塞原因；不是编译错误，也没有被统一设ready绕过。
- 最后宽范围EditMode：job `e0ae868266f64211abd333ab84863a39` **1144项执行完成，失败列表仅1项**：`CafeSaveMigrationTests.RepeatedFractionalPoursKeepPerfectScoreWithinStrictSaveRange`，严格期望100.0，实际99.9999924。其余本轮叙事、条件、生成配置、真实CafeWorld服务、日终／拘押、注入生命周期相关失败在本次复测未再报告。原始结果见 `output/narrative-integration/editmode-final.json`；不将这个失败写成全绿。
- 最终Unity实际加载：346 Event／5506 ConversationPage／8156 ConversationLine；Editor扫描 **GlobalErrors=0、Warnings=0、4条明确construction阻塞原因**，归属于3个根。`Docs/NcsIntegrationReport.md` 和 `Assets/ConfigTables/Data/Narrative/NcsIntegrationQueue.csv` 已同步。
- 官方Luban最后strict成功，Player semantic validation通过；修改范围git diff --check通过。未列入通过项的测试不代表已通过。


## 结局与需求卡增补验证入口

本文件上方1144/40等数字是整轮叙事接入当时的历史验证，不是新结局面板／需求卡的测试结果。最新专项数字见 `Docs/NarrativeEndingAndArtDelivery.md` 最终验证：启用结局已补开场IF0为16项；原3个终局作者闸门不更改。占位图只用本地排字，原93项＋6项当前源标记共37张，无图片API调用。

## 地图对白面板修正补验（2026-10-19）

- 240地图根统一Conversation，220根由Reading迁移；报纸Reading/公告Notice/咖啡馆非模态快照保留。源Excel经官方Luban重新生成，无JSON-only改动。
- Python129/129；EditMode57/57（DailyContent+Loader+ReadingReport）；旧Reading PlayMode6/6；新增真实地图行动用例1/1，在共享场景逐个验证九地DialoguePanel及取消退款/无成长。
- Unity实际新快照：5727 ConversationPage/9031 ConversationLine，14 ReadingPage/180 ReadingLine；GlobalErrors0、Warnings0，3根既有阻塞的4条原因不变。测试准备了day26/人物/主线/首访历史，不是完整自然通关。
- 详见 `Docs/NarrativeMapDialoguePanelFix.md` 与 `output/narrative-map-dialogue-fix/`。

## GM剧情图谱与演出试播补验（2026-10-19）

- GM增加剧情图谱入口，GraphView与GM shell统一暖纸/咖啡/黄铜风格；根状态读取正式controller，不新增逐页已读账本；单页/分支试播复用现有Presenter和面板，只在内存副本运行，不创建真实咖啡订单。
- EditMode状态/关系图/构建器41/41通过；最终新增PlayMode9/9通过（1 GM集成、8试播隔离，job 08a6f00a64e74765bbac44e1aff80651，JSON/fullNames/XML均已核对）。
- 真实Cafe会话额外验证daily.st.01单页与daily.old.08选项A仅到response.a，源配置/runtime/逐字段订单不变并恢复暂停owner。该会话无正式NCS，不能冒充正式存档状态验收；有正式状态的复制/隔离由独立runtime fixture回归覆盖。
- 详见 `Docs/NarrativeGMAtlas.md` 和 `output/narrative-gm-atlas/`。早期取消/其他HUD测试覆盖job不计入通过数字；最终截图gm-shell.png/gm-atlas.png已目视核对。

## 日常分段/重复扣点补审（2026-10-19）

- 市政厅源稿24组为独立/不同日期造访，同名不等于一个24步任务。九地源配置每地1个Repeat池；首访+正文单次Execute/AP，选项回应和真实QUEUE_EVENT不另收费，未复现一次执行多次扣点。
- 修复地图/待办误导：做完一次标本次完成、按钮再次行动且明确另计费用；非必做Repeat日常不阻止休息。保留日期、条件、剧情、成本、存档和咖啡交互，没有改Excel/JSON。
- EditMode101/101（DailyContent/Action/BatchContinuity/MapUiRepair）；最终PlayMode4/4（九地首访+正文、九地取消、CIV08选择回应、营业地图行动只执行一次）；详情见 `Docs/NarrativeDailyActionCosts.md`，证据 output/narrative-daily-action-audit/。
