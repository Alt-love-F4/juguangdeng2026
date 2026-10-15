# 10 月 7 日正式内容表与来源映射

日期：2026-10-15。责任：oct07-config（仅配置源、官方生成、事实交付文档）。

本报告是已实施配置的定位清单，不新增设计或第二套剧本标准。S1 正式配表后，parent 报告包二 40 行正式实跑通过并明确授权包三；现 S2 原文、七步组合、Inline 检定、信纸与最终两事实均已进入正式源表并由官方 bat 生成通过。配置交付不等于 S2 运行体验验收。本 Agent 未切换 Editor/PlayMode、未触发编译或运行时测试，未做 git reset/commit。

## 1. 来源与完整性

- 规格：`Docs/superpowers/specs/2026-10-15-october07-narrative-integration-spec.md`，附录 A/B。
- S1 原件：`D:/桌面/文案/中立/MA70.10.07-S1.docx`，已只读解包核对 48 个非空段落，附录 A 一致（仅换行归一）。其中 40 段显示正文，8 段文档结构/演出说明；未丢弃其来源或意图。
- S2 原件：`D:/桌面/文案/革命联合相关事件/MA70.10.07-S2.docx`，已核对 47 个非空段落，与附录 B 一致（仅换行归一）。
- 总纲：`D:/桌面/文案/故事.xlsx` → `时间轴-共通线路`，已读取确切 `670.10.07` 行，S1 客流/商会与 S2 密信/10 月 9 日约定顺序吻合。总纲未修改。
- S1 正文只剥离角色名前缀及括号表情标记，其余汉字、数字、引号、标点和顺序逐字核对。玩家姓名来自 `Object=player`，不把 `{playername}` 作为固定台词重复显示；正文中的其他内容不润色。40 条 Text 和 ConversationLine 均有来源段号，单元格批注保留对应完整原文。

## 2. 唯一正式源与结构

| 文件 | 现状与编辑边界 |
|---|---|
| `Assets/ConfigTables/Data/Narrative/Narrative.xlsx` | 29 张现有工作表；增量追加 S1/S2 行、Action 两列、ConversationPage 三列、AbilityCheck 一列及 cafe 挂载值；不新建表 |
| `Assets/ConfigTables/Data/Shared/Text.xlsx` | `Text` 单表；B=key、C=正文、D=comment；S1 增量 511:555，S2 增量 556:599 |
| `Assets/ConfigTables/Defines/schema.xml` | 仅协商后的最小字段约束变化，见下 |

现有链路复用：`Event → PresentationIndex → ConversationPage → ConversationLine → Text`，`Action / Condition / ConditionKey / Result / MapPosition / Object` 沿既有 NCS。没有新建内容工作簿、分片或平行剧本。

### schema 精确增量

1. `NcsActionEntry.replacesLocationActions: bool`，旧行明确 false；S1 true。
2. `NcsActionEntry.focusLocationOnComplete: bool`，旧行明确 false；S1 true。
3. `NcsConversationLineEntry.side` 从 `left,right` 扩为 `left,right,none`；`none` 用于旁白/玩家/路人，不替换已有舞台立绘。
4. `NcsConversationLineEntry.accentTargetKey` 从 Object 可选引用扩为普通 string，以容纳 `background` 等已有演出语义目标；运行时 validator 由 parent 负责接受 Object 或 reserved target。
5. Event 不新增入口字段：S1 复用 `eventProperty=entry=MapAction;blocksSave=true`，S2 复用 `entry=BeforeNight;blocksSave=true`；旧行缺省 Immediate。
6. `NcsConversationPageEntry` 追加 `stepType`（set 含空、Dialogue、PassiveCheck、LetterReveal、LetterRead）、`readingKey`（可选 PresentationIndex 引用）、`checkId`（可选 AbilityCheck 引用）。旧行三列空，loader 将空 stepType 视为 Dialogue。Letter 两步仅填 readingKey，PassiveCheck 仅填 Inline checkId。
7. `NcsAbilityCheckEntry` 追加 `mode`（set 含空、Branch、Inline），successEventId/failureEventId 改为可选 Event 引用。runtime/validator 仍强制 Branch 双分支；空 mode=Branch；Inline 两 eventId 与两 resultOwnerId 全空，禁止 Choice/Setup 引用，仅由 PassiveCheck page 使用，不削弱旧分支约束。
8. 官方 `StringDataSet` 使用 `Args.Split(',')` 保留空成员；最终 schema 为 `string#set=,Dialogue,PassiveCheck,LetterReveal,LetterRead` 与 `string#set=,Branch,Inline`，显式容纳约定的旧空默认值，不回填修改旧剧情行。

无新增 Letter 表/文件；信件复用 ReadingPage/ReadingLine/PresentationIndex。默认信封/纸图来自原 Prefab bindings，经 DTO 消费；S2 page.imageKey 与 eventProperty envelope 覆写均未填，避免捏造资源路径。fixedWords 不新增 Excel 列，继续用现有 `Resources/Dialogue/PassiveCheckPresentation.asset`。

## 3. S1 正式身份与行为行

| 配置项 | 真实标识 / 行 |
|---|---|
| 根事件 | `oct07.s1`，`Event!B54:O54`，onceOnly=true，MapAction，blocksSave=true |
| 主演出 | `oct07.s1.conversation`，`PresentationIndex!B20:D20`，Conversation |
| 演出参数 | `SetupParams!B40:G40`，复用 `Tpl_ArrivalConversation`，无选项、无自动推进 |
| 咖啡馆昼 | `oct07.s1.cafe`，`ConversationPage!B21:I21`，seq=1，21 句，bg=`oct07.cafe.day` |
| 圣徒街昼 | `oct07.s1.street`，`ConversationPage!B22:I22`，seq=2，19 句，bg=`oct07.street.day` |
| 地图行动 | `oct07.cafe.s1`，`Action!B11:N11`，ONCE、mustExecute=true、requirements 空（0 AP）、resultEvents=`oct07.s1`，两新 bool=true |
| 地图挂载 | `MapPosition!H5`，cafe.extendedActions=`oct07.cafe.s1`，保留其他地点/原导航 |
| 完成事实 | `story.oct07.s1.completed`，`ConditionKey!B26:E26`，初始 false、onlySetTrue=true |
| 地图阶段投影 | `world.map.active`，`ConditionKey!B27:E27`，初始 false、onlySetTrue=false；由 parent DayFlow 写入 |
| 行动条件 | `Condition!B26:H27` 与 `B30:H30`：同组 day=2（BAR_BETWEEN world.calendar，value=`2,2`）+ completed=false + map.active=true |
| 事件条件 | `Condition!B28:H29`：day=2 + completed=false；不重复地图 flag，避免行动开始后的上下文冲突 |
| 最终结果 | `oct07.s1.complete`，`Result!B72:H72`，仅根事件全部完成才 SET_FLAG；无奖励、道具、日期推进或额外选项 |
| 无立绘路人 | `oct07.passerby.a`，`Object!B13:J13`；nameText=`oct07.passerby.a.name`，所有立绘/图标路径空 |
| 标题/行动名称 | `Text!B551:D552`，“开店前的交谈” |

## 4. S1 全部 48 段来源映射

本表共用 `eventId=oct07.s1`、`presentationKey=oct07.s1.conversation`。下表的 cafe/street 分别指 `oct07.s1.cafe` / `oct07.s1.street`；正文行 ID、Text key 均为 `oct07.s1.pNNN`。每个正文恰好对应一条正式行，无额外结构文字点击。来源原件/附录保留全文，本表仅提供定位。

| 来源段 | 正式 page / lineId / seq | Narrative 行；Text 行 | 来源处理 / 演出意图 |
|---|---|---|---|
| S1-P001 | — | — | 文档标题，不显示；仍留在权威来源 |
| S1-P002 | — | — | 章节“一”，不显示 |
| S1-P003 | cafe / p004 / 1 | ConversationPage 21；Line 68 | 时间、咖啡馆昼背景与阿黛尔角色意图；初始空舞台，人物不提前入场 |
| S1-P004 | cafe / oct07.s1.p004 / 1 | Line 68；Text 511 | 旁白，原文完整 |
| S1-P005 | cafe / oct07.s1.p005 / 2 | Line 69；Text 512 | 旁白 |
| S1-P006 | cafe / oct07.s1.p006 / 3 | Line 70；Text 513 | 旁白 |
| S1-P007 | cafe / oct07.s1.p008 / 4 | Line 71 | 立绘入场意图并入后续第一句阿黛尔台词，不显示标记文字 |
| S1-P008 | cafe / oct07.s1.p008 / 4 | Line 71；Text 514 | adele/right，首次登场 |
| S1-P009 | cafe / oct07.s1.p009 / 5 | Line 72；Text 515 | player/none；动态姓名，无固定立绘 |
| S1-P010 | cafe / oct07.s1.p010 / 6 | Line 73；Text 516 | adele/right；emote=cautious，缺差分回退 |
| S1-P011 | cafe / oct07.s1.p011 / 7 | Line 74；Text 517 | player/none |
| S1-P012 | cafe / oct07.s1.p012 / 8 | Line 75；Text 518 | adele/right；fx_portrait_nod→adele，不重置表情 |
| S1-P013 | cafe / oct07.s1.p013 / 9 | Line 76；Text 519 | player/none |
| S1-P014 | cafe / oct07.s1.p015 / 10 | Line 77 | 背景缓移意图并入后句；fx_background_pan→background，不额外正文 |
| S1-P015 | cafe / oct07.s1.p015 / 10 | Line 77；Text 520 | 旁白，同时触发背景层效果 |
| S1-P016 | cafe / oct07.s1.p016 / 11 | Line 78；Text 521 | 旁白，不清阿黛尔 |
| S1-P017 | cafe / oct07.s1.p017 / 12 | Line 79；Text 522 | adele/right，保持表情 |
| S1-P018 | cafe / oct07.s1.p018 / 13 | Line 80；Text 523 | player/none |
| S1-P019 | cafe / oct07.s1.p019 / 14 | Line 81；Text 524 | adele/right |
| S1-P020 | cafe / oct07.s1.p020 / 15 | Line 82；Text 525 | adele/right |
| S1-P021 | cafe / oct07.s1.p021 / 16 | Line 83；Text 526 | player/none |
| S1-P022 | cafe / oct07.s1.p022 / 17 | Line 84；Text 527 | adele/right |
| S1-P023 | cafe / oct07.s1.p023 / 18 | Line 85；Text 528 | player/none |
| S1-P024 | cafe / oct07.s1.p024 / 19 | Line 86；Text 529 | adele/right；emote=dejected，缺差分回退 |
| S1-P025 | cafe / oct07.s1.p025 / 20 | Line 87；Text 530 | player/none |
| S1-P026 | cafe / oct07.s1.p026 / 21 | Line 88；Text 531 | adele/right；保持表情 |
| S1-P027 | — | — | 章节“二”，不显示 |
| S1-P028 | street / oct07.s1.p029 / 1 | ConversationPage 22；Line 89 | 切圣徒街昼，bgOverride=oct07.street.day；阿黛尔保持，不做二次入场 |
| S1-P029 | street / oct07.s1.p029 / 1 | Line 89；Text 532 | 旁白 |
| S1-P030 | street / oct07.s1.p030 / 2 | Line 90；Text 533 | 旁白 |
| S1-P031 | street / oct07.s1.p031 / 3 | Line 91；Text 534 | 旁白 |
| S1-P032 | street / oct07.s1.p032 / 4 | Line 92；Text 535 | 旁白 |
| S1-P033 | street / oct07.s1.p033 / 5 | Line 93；Text 536 | 旁白 |
| S1-P034 | street / oct07.s1.p034 / 6 | Line 94；Text 537 | player/none |
| S1-P035 | street / oct07.s1.p036 / 7 | Object 13；Line 95 | 路人 A 无立绘意图：独立姓名身份，图路径全空、side=none |
| S1-P036 | street / oct07.s1.p036 / 7 | Line 95；Text 538 | oct07.passerby.a/none |
| S1-P037 | street / oct07.s1.p037 / 8 | Line 96；Text 539 | 旁白 |
| S1-P038 | street / oct07.s1.p038 / 9 | Line 97；Text 540 | oct07.passerby.a/none |
| S1-P039 | street / oct07.s1.p039 / 10 | Line 98；Text 541 | 旁白 |
| S1-P040 | street / oct07.s1.p040 / 11 | Line 99；Text 542 | player/none |
| S1-P041 | street / oct07.s1.p041 / 12 | Line 100；Text 543 | oct07.passerby.a/none |
| S1-P042 | street / oct07.s1.p042 / 13 | Line 101；Text 544 | 旁白 |
| S1-P043 | street / oct07.s1.p043 / 14 | Line 102；Text 545 | 旁白 |
| S1-P044 | street / oct07.s1.p044 / 15 | Line 103；Text 546 | adele/right，沿连续舞台状态 |
| S1-P045 | street / oct07.s1.p045 / 16 | Line 104；Text 547 | player/none |
| S1-P046 | street / oct07.s1.p046 / 17 | Line 105；Text 548 | 旁白，不新增招牌 CG |
| S1-P047 | street / oct07.s1.p047 / 18 | Line 106；Text 549 | 正文完整保留「沸点咖啡馆」 |
| S1-P048 | street / oct07.s1.p048 / 19 | Line 107；Text 550 | 最后一句；结束后根事件才结算完成 |

正文单元格为 `Text!C511:C550`，key 在 B 列，来源摘要在 D 列；Narrative 正文行位置指 `ConversationLine!B68:L107`。旁白 speakerKey 为空，不额外显示名字。未显式写 emote 的阿黛尔句子为空意图，运行时应保持上一表情而非重置。

## 5. 占位与能力接线清单

下表列的是当前表内真实定位及联调责任，不将未运行的效果写成已验证。所有 S1 演出行的 eventId=`oct07.s1`、presentationKey=`oct07.s1.conversation`；预览应读这套正式生成配置，不启动真实行动。

| 编号 | 来源与真实定位 | 当前引用 / 意图 | 状态与替换方式 |
|---|---|---|---|
| OCT07-R01 | P003/P004；page=`oct07.s1.cafe`；line=`oct07.s1.p004`，seq=1 | bgOverride=`oct07.cafe.day`；原图 `Assets/Art/Background/架空咖啡馆背景_01.png` | 原图存在，旧开场已有引用；parent 实现 VisualLibrary 稳定入口，不复制/挪动原图；本 Agent 未作视觉验收 |
| OCT07-R02 | P028/P029；page=`oct07.s1.street`；line=`oct07.s1.p029`，seq=1 | bgOverride=`oct07.street.day`；街昼，淡切约 0.3 秒 | 本次查找未确认真实圣徒街图；parent 采用中性纯色缺图占位，禁止拿室内图冒充；以后替换该稳定 key 的资源绑定 |
| OCT07-R03 | S2-P002/P003；event=oct07.s2；presentation=oct07.s2.conversation；page=oct07.s2.opening；line=oct07.s2.p003，seq=1，Line108 | bgOverride=`oct07.cafe.night`（所有七步骤同 key）；咖啡馆原图+独立蓝灰夜色意图 | 正式已配，builder已执行落地并在正式S2实跑；替换入口 `Assets/Resources/Dialogue/NarrativeStageVisuals.asset` 的 oct07.cafe.night，当前同咖啡馆texture、独立tint=(.42,.48,.62,1)。配置Agent未自行作视觉验收，整体验证见acceptance报告 |
| OCT07-R04 | P010；page=`oct07.s1.cafe`；line=`oct07.s1.p010`，seq=6，Line 行73 | emote=`cautious`；基准 `Characters/adele/neutral` | 缺小心翼翼差分，保留 key，parent 负责 fallback/诊断；以后仅替换对应差分资源 |
| OCT07-R05 | P012；page=`oct07.s1.cafe`；line=`oct07.s1.p012`，seq=8，Line 行75 | fx=`fx_portrait_nod`，target=`adele`；约0.25秒轻点头、不循环 | 表已配置，现有同一效果库扩展由 parent 负责；不额外切表情、不逐句重播；以后替换效果 preset |
| OCT07-R06 | P024；page=`oct07.s1.cafe`；line=`oct07.s1.p024`，seq=19，Line 行86 | emote=`dejected`；基准 `Characters/adele/neutral` | 缺沮丧差分，保留 key，parent 负责 fallback；未写表情的后续行保持现状 |
| OCT07-R07 | 撤销项 | 无额外 CG | 无需处理；正常背景切换已覆盖，不生成 CG 配置 |
| OCT07-R08 | S2-P011；event=oct07.s2；presentation=oct07.s2.conversation；page=oct07.s2.opening；line=oct07.s2.p011，seq=9，Line116 | 猫叫意图；sfxKey空；本行批注保留缺音效说明 | 无已核实匹配音频，不造路径、不用无关声音。以后填该行sfxKey并官方生成；GM需明确缺音效诊断 |

另：S1-P014 背景缓移合并到 P015 的 line=`oct07.s1.p015`，page=`oct07.s1.cafe`，seq=10，Line 行77，accentFxKey=`fx_background_pan`，target=`background`。已只读确认 `Assets/Resources/Dialogue/PresentationEffectLibrary.asset` 的 parent 预设：pan duration=1.6、strength=0.02、zoom=1.05；nod duration=0.25、strength=8、zoom=1。背景源与占位替换入口为 `Assets/Resources/Dialogue/NarrativeStageVisuals.asset`：day 绑定原咖啡馆图，street 为中性纯色 placeholder、fadeSeconds=0.3；不复制原图，不把参数写进台词。

## 6. S2 正式身份与完整来源映射

parent 在包二实跑通过后明确授权包三；本节为授权后已写入并生成的数据，不是后续设计建议。`Library/Locus/tmp/oct07-config/s2-source-only.json` 仅为早期来源摘录，loader不读，正式唯一源仍为两工作簿/schema。S2 全文42个显示段落：34条对话/旁白 + 1称呼 + 7行密文；其余5段结构/演出/条件说明均有来源映射，不作为额外正文。

### 正式身份、阶段与最终结果

| 项目 | 真实ID与源行 |
|---|---|
| 单根事件 | `oct07.s2`，Event!B55:O55，onceOnly=true，property=`entry=BeforeNight;blocksSave=true` |
| 日期阶段 | EventDate!B7:F7，day=2，timeSlot=night，repeatRule=once，priority=0（单事件默认序） |
| 主演出 | `oct07.s2.conversation`，PresentationIndex!B21:D21，Conversation |
| 信纸索引 | `oct07.s2.letter`，PresentationIndex!B22:D22，Reading |
| 演出参数 | `oct07.s2`，SetupParams!B41:G41，复用Tpl_ArrivalConversation，无选项/自动推进/checkId |
| 根前置 | Condition!B31:H33，同组day=2 + story.oct07.s2.completed=false + story.oct07.s1.completed=true |
| 真实知觉节点 | `oct07.s2.perception`，AbilityCheck!B7:H7，mode=Inline，四个分支/结果字段全空 |
| 属性条件 | `oct07.s2.perception.1`，AbilityCheckCondition!B7:G7，attributeId=perception，threshold=2，group=1，seq=1 |
| 完成事实 | `story.oct07.s2.completed`，ConditionKey!B28:E28，初始false、onlySetTrue=true |
| 已知约定事实 | `story.oct09.meeting.known`，ConditionKey!B29:E29，初始false、onlySetTrue=true |
| 最终结算 | Result!B73:H74：seq1 `oct07.s2.complete`与seq2 `oct07.s2.meeting_known`，仅SET_FLAG上述两事实；根event全部结束后才提交 |

### 同根七步（无子event队列）

| 步骤seq | ConversationPage.pageId / 源行 | stepType / 引用 | 正文与边界 |
|---|---|---|---|
| 1 | `oct07.s2.opening` / row23 | Dialogue | P003:P012，10句；bgOverride=oct07.cafe.night，独立事件空舞台，不继承阿黛尔 |
| 2 | `oct07.s2.envelope` / row24 | LetterReveal，readingKey=oct07.s2.letter | P013，显示不可拆信物；没有正文行/额外点击 |
| 3 | `oct07.s2.watch` / row25 | Dialogue | P014:P018，5句；同owner信物保留、仍不可拆 |
| 4 | `oct07.s2.perception` / row26 | PassiveCheck，checkId=oct07.s2.perception | P019读取一次真实属性，成功仅P020（1句），失败跳过本page直P021；不入Choice/Setup分支 |
| 5 | `oct07.s2.before_letter` / row27 | Dialogue | P021，1句；完成该句才进入读信 |
| 6 | `oct07.s2.read_letter` / row28 | LetterRead，readingKey=oct07.s2.letter | P022启用同owner已展示信物；ReadingPage.titleKey=P023，ReadingLine P024:P030；读完整才能关闭 |
| 7 | `oct07.s2.after_letter` / row29 | Dialogue | 关信后P031:P047，17句；只末尾交回根event结算与日程 |

所有7个page bgOverride均为 `oct07.cafe.night`，无bgm覆写；Dialogue/PassiveCheck行均side=none，玩家speakerKey=player、旁白speakerKey空，无任何登场人物。失败不造替代台词/提示。成功小字通过现有PassiveCheckPresentationConfig.Hint同快照，固定词由parent在原资源为checkId=oct07.s2.perception配置“已察觉”，不新增Excel字段/重复条件算法。

### 全部47段逐行映射

共用eventId=`oct07.s2`。下表opening/watch/perception/before_letter/after_letter为pageId的`oct07.s2.`后缀，主presentationKey均为`oct07.s2.conversation`。信纸共用readingKey=`oct07.s2.letter`、ReadingPage=`oct07.s2.letter.page`（ReadingPage row7），称呼只在title显示一次。完整原文仍在Spec附录B与Text单元格批注；本表不重复复制密文。

| 来源段 | 正式page / lineId / seq | Narrative行；Text行 | 显示或演出定位 |
|---|---|---|---|
| S2-P001 | — | — | 文档标题不显示，仍保留在权威来源 |
| S2-P002 | opening / oct07.s2.p003 / 1 | ConversationPage23；Line108 | 夜景空舞台初始化，R03 |
| S2-P003 | opening / oct07.s2.p003 / 1 | Line108；Text556 | 旁白 |
| S2-P004 | opening / oct07.s2.p004 / 2 | Line109；Text557 | 旁白 |
| S2-P005 | opening / oct07.s2.p005 / 3 | Line110；Text558 | 旁白提到阿黛尔，不表示入场 |
| S2-P006 | opening / oct07.s2.p006 / 4 | Line111；Text559 | 旁白 |
| S2-P007 | opening / oct07.s2.p007 / 5 | Line112；Text560 | 旁白 |
| S2-P008 | opening / oct07.s2.p008 / 6 | Line113；Text561 | 旁白 |
| S2-P009 | opening / oct07.s2.p009 / 7 | Line114；Text562 | 旁白提到阿黛尔，不表示入场 |
| S2-P010 | opening / oct07.s2.p010 / 8 | Line115；Text563 | 旁白 |
| S2-P011 | opening / oct07.s2.p011 / 9 | Line116；Text564 | 猫叫意图R08；sfxKey空，缺音效不捏造 |
| S2-P012 | opening / oct07.s2.p012 / 10 | Line117；Text565 | 发现信，后接Reveal |
| S2-P013 | oct07.s2.envelope / 无line / step2 | ConversationPage24 | LetterReveal，信图示出现但不能拆；引用oct07.s2.letter |
| S2-P014 | watch / oct07.s2.p014 / 1 | Line118；Text566 | player/none，保留⌈⌋及英文六点号 |
| S2-P015 | watch / oct07.s2.p015 / 2 | Line119；Text567 | 旁白 |
| S2-P016 | watch / oct07.s2.p016 / 3 | Line120；Text568 | 旁白，拿扫帚不凭空加演出资源 |
| S2-P017 | watch / oct07.s2.p017 / 4 | Line121；Text569 | 旁白 |
| S2-P018 | watch / oct07.s2.p018 / 5 | Line122；Text570 | 检查窗边后进入真实知觉节点 |
| S2-P019 | oct07.s2.perception / 无line / step4 | ConversationPage26；AbilityCheck7；AbilityCheckCondition7 | Inline perception≥2，单次快照；成功只下一句，失败直接P021 |
| S2-P020 | perception / oct07.s2.p020 / 1 | Line123；Text571 | player/none，仅知觉成功显示；首正文成功小字同快照 |
| S2-P021 | before_letter / oct07.s2.p021 / 1 | Line124；Text572 | 完成本句后才启用拆信 |
| S2-P022 | oct07.s2.read_letter / 无line / step6 | ConversationPage28 | LetterRead，同readingKey同owner，不另排子event |
| S2-P023 | oct07.s2.letter.page / titleKey=oct07.s2.p023 / page1 | ReadingPage7；Text573 | “致新任店长：”仅称呼，不再重复成ReadingLine |
| S2-P024 | oct07.s2.letter.page / oct07.s2.p024 / 1 | ReadingLine7；Text574 | 七行密文第1行，33个两位token |
| S2-P025 | oct07.s2.letter.page / oct07.s2.p025 / 2 | ReadingLine8；Text575 | 密文第2行，33个两位token |
| S2-P026 | oct07.s2.letter.page / oct07.s2.p026 / 3 | ReadingLine9；Text576 | 密文第3行，33个两位token |
| S2-P027 | oct07.s2.letter.page / oct07.s2.p027 / 4 | ReadingLine10；Text577 | 密文第4行，33个两位token |
| S2-P028 | oct07.s2.letter.page / oct07.s2.p028 / 5 | ReadingLine11；Text578 | 密文第5行，33个两位token |
| S2-P029 | oct07.s2.letter.page / oct07.s2.p029 / 6 | ReadingLine12；Text579 | 密文第6行，33个两位token |
| S2-P030 | oct07.s2.letter.page / oct07.s2.p030 / 7 | ReadingLine13；Text580 | 密文第7行；总计231个token，不拆两位数字 |
| S2-P031 | after_letter / oct07.s2.p031 / 1 | Line125；Text581 | 关闭信纸精确续接首句 |
| S2-P032 | after_letter / oct07.s2.p032 / 2 | Line126；Text582 | 旁白 |
| S2-P033 | after_letter / oct07.s2.p033 / 3 | Line127；Text583 | 旁白 |
| S2-P034 | after_letter / oct07.s2.p034 / 4 | Line128；Text584 | 旁白，原解密说明 |
| S2-P035 | after_letter / oct07.s2.p035 / 5 | Line129；Text585 | 旁白 |
| S2-P036 | after_letter / oct07.s2.p036 / 6 | Line130；Text586 | 原连续字母串逐字保留，不改写推导 |
| S2-P037 | after_letter / oct07.s2.p037 / 7 | Line131；Text587 | 旁白 |
| S2-P038 | after_letter / oct07.s2.p038 / 8 | Line132；Text588 | 原英文大小写、分词、单引号与句点均保留 |
| S2-P039 | after_letter / oct07.s2.p039 / 9 | Line133；Text589 | 中文译文含⌈⌋原样 |
| S2-P040 | after_letter / oct07.s2.p040 / 10 | Line134；Text590 | player/none，心理活动引号原样 |
| S2-P041 | after_letter / oct07.s2.p041 / 11 | Line135；Text591 | 旁白 |
| S2-P042 | after_letter / oct07.s2.p042 / 12 | Line136；Text592 | 旁白 |
| S2-P043 | after_letter / oct07.s2.p043 / 13 | Line137；Text593 | 旁白 |
| S2-P044 | after_letter / oct07.s2.p044 / 14 | Line138；Text594 | player/none |
| S2-P045 | after_letter / oct07.s2.p045 / 15 | Line139；Text595 | 收好信，不新增物品/奖励 |
| S2-P046 | after_letter / oct07.s2.p046 / 16 | Line140；Text596 | 关门离开，不新增地图预约行动 |
| S2-P047 | after_letter / oct07.s2.p047 / 17 | Line141；Text597 | 末句后才最终两事实、交回日程 |

Text!C556:C597为42段原文，Text!B598:D599为两事实调试名。已对原DOCX/Spec逐字核验；玩家行只去`{playername}:`前缀，⌈⌋、ASCII点号、长串、原数字/标点一律保留。ReadingLine.mode=narration，voiceKey空；不捏造读信配音/环境音。

## 7. 生成链路、备份与验证

### 施工前已读的生成/分片脚本

- `Assets/ConfigTables/GenerateAll.bat`：无分片时直接使用保存的两正式工作簿；有 `Narrative_*.xlsx` 或 `Text_*.xlsx` 才执行 merge，再做 Player 语义验证与官方 Luban `--strict`。
- `Assets/ConfigTables/merge_narrative.py`：Narrative 白名单只有 Object/Bar/Trigger/Event/EventDate/Result/Condition/Template/SetupParams/Choice/OpeningPresentation/OpeningLine，**不覆盖** Action、MapPosition、Conversation、Reading 等新增表。当前无分片，因此本次安全直用源工作簿；不可随意新增分片，否则会重建工作簿并丢掉白名单外表。
- `Assets/ConfigTables/generate_tables.py`：旧自建生成路径，已读，不运行；本次只走官方 bat。
- `Assets/ConfigTables/configure_first_arrival.py`：已读参考现有表头与演出模式，不运行、不重写旧内容。

### 备份与并发保护

所有备份均在 `Library/Locus/tmp/oct07-config/backups/`，不放在 ConfigTables/Data，避免被分片 glob 吸入。每次二进制增量均先备份、读取当前 SHA256、存临时工作簿、写回前重核 SHA256，再原位替换；避免读旧版本覆盖他人更新。

| 增量 | 目录 | 内容 |
|---|---|---|
| 01 | `20261015-220933-text01` | Text 修改前工作簿 + manifest；40 正文、标题/行动名/路人名 |
| 02 | `20261015-221417-s1pages02` | Narrative/schema 修改前备份 + manifest；40 正文映射、两 page、根事件、演出索引与参数；另留重建的 schema 原始基线副本 |
| 03 | `20261015-221549-s1action03` | Narrative/Text 修改前工作簿 + manifest；Action 两列、条件键/条件/结果及 cafe 挂载 |
| 04 | `20261015-230709-s2text04` | Text 修改前工作簿 + manifest；S2 42条完整显示正文 + 两事实调试名 |
| 05 | `20261015-230932-s2wire05` | Narrative/schema 修改前备份 + manifest；S2七步骤/34对话/7密文/称呼/Inline/日期/两结果与schema字段 |
| 06 | `20261015-231359-s2blank06` | schema 修改前备份 + manifest；set显式空成员兼容旧空值，仅修schema不动旧表行 |

未删除、重排或重建现有工作表。原有单元格值已逐格与修改前备份对照，唯一预期的既有值修改是 `MapPosition!H5`，空挂载变成 S1；旧 Action 的新增两列均 false。其他 Agent 的运行时代码/UI 变更未触碰。

### 已执行命令 / 结果

- 官方命令：在项目根执行 `cmd.exe /d /c Assets\ConfigTables\GenerateAll.bat`。本机 Git Bash 实际使用 `MSYS_NO_PATHCONV=1 cmd.exe /d /c 'Assets\ConfigTables\GenerateAll.bat'`。首次路径引号错误未启动 bat，修正后官方执行成功。
- 2026-10-15 **22:16:22**：exit code=0；`Player semantic validation passed`；官方 Luban load/validation/code/data 全部结束，`SUCCESS`。
- 自动生成更新：`Assets/Scripts/Config/Generated/NcsActionEntry.cs`、`NcsConversationLineEntry.cs`；`Assets/Settings/Resources/Luban/` 下 tbtext、tbncssetupparams/object/conditionkey/event/presentationindex/action/condition/conversationpage/conversationline/result/mapposition 共12项 JSON。这些全由 bat 产生，未手改。
- 只读核验：S1 40 段 Text 和附录/原 DOCX 逐字一致（只执行授权的前缀/表情剥离）；两页 21+19 句，seq 连续，来源顺序无漏行/重复。
- 只读核验：以上11张 Narrative 源表及 Text 与生成 JSON 全量逐字段比对一致；空 string/bool/int 沿 Luban 缺省映射，不以空值误判。
- 只读核验：无本次新引入的重复主键。Bar 原有按 owner 作用域区分的同名键原样保留，不以第二列字符串重复误判全局错误。
- S1 配表阶段曾保持 S2 正式行为空；parent 明确包三授权后追加 S2，现已正式生成。原 `s2-source-only.json` 仅历史来源摘录，不由 loader 读取、不作为配置源。S2 当前交付见第6节与后续生成记录。

### S2 官方生成与自动核验

- 2026-10-15 **23:10:35** 首轮bat exit1：官方set校验不接受旧行空stepType/mode。该轮官方工具仍自动输出C#/JSON，已立刻通知parent暂勿使用；未手改这些输出。
- 已读本机官方Luban实现 `D:/Luban/src/Luban.DataValidator.Builtin/Set/StringDataSet.cs`，Args.Split逗号保留空成员；只修schema允许set空成员，旧行空值/语义不改。
- **23:14:12** 同一官方GenerateAll.bat最终 **exit0**，Player语义通过，Luban --strict validation结束，零ERROR，SUCCESS。成功后已提醒parent导入JSON/统一编译，本Agent不操作Editor。
- 本次官方生成类新增/更新 `NcsConversationPageEntry.cs` 与 `NcsAbilityCheckEntry.cs`；JSON增加S2 Event/EventDate/SetupParams/PresentationIndex/ConditionKey/Condition/AbilityCheck/AbilityCheckCondition/ConversationPage/ConversationLine/ReadingPage/ReadingLine/Result/Text内容（共14张已全量核对），没有手改生成JSON/C#。
- 已对S2原DOCX47非空段落逐段比对Spec；42显示Text逐字一致，仅玩家前缀剥离。34对话+1称呼+7密文各自唯一映射，七个page seq连续1:7、行数`10,0,5,1,1,0,17`；称呼不重复显示。
- 七行密文各33个两位token，总231个，与原文逐token/空格保持；长字母串、英文、中文译文与括号/句点全保留，不擅自修正文案推导。
- Inline真实输入perception/threshold2/group1/seq1；四分支字段全空，未被Choice/Setup引用；只有唯一S2根event，无子event/假分支/测试事件。最终Result严格仅两条SET_FLAG，无任务/物品/奖励/ADVANCE_DAY。
- 与S2施工前备份比对：**Narrative6238个、Text2220个既有cell的值、数据类型、字体/填充/边框/对齐/数字格式/保护样式全保持**；没有新重复键。旧Action/S1内容不变，旧ConversationPage/AbilityCheck新增列留空兼容。
- 13张Narrative表+Text与最终生成JSON全量逐字段一致；AbilityCheck既有comment为策划列不生成，合法类型缺省按Luban归一，不误判空bool/string。检查摘要为 `Library/Locus/tmp/oct07-config/s2-verification.json`，不是配置源。
- 最终源SHA256：Narrative=`c5097bda93274a3782eaf2a5a5669155effffc4aab6491a8881eecd9dabc9dce`；Text=`a18ccdc3e93562066ae2f3cb6b77db3b24225642f8c9594b11d3bce6c2dd9970`；schema=`2931469d32b3422c7530dfb28138d3982b22f262a66f0a83d05a1ae33f563e73`。

### 联调责任与验收交叉引用

已将 Event 路由属性、Action 两 bool、world.map.active、side=none、stable visual keys、表情/效果 key 同步给 parent、ncs-routing 与 oct07-map；包三page/check字段与routing逐项一致。Schema/Excel施工期间由本Agent独占，生成成功后已冻结。parent已统一编译并执行EditMode builder落地night alias与fixedWord资源，正式S1/S2、日结与最终中断/存档回归均完成。以上parent运行证据详见acceptance报告，不作为本配置Agent自行运行的测试。

本配置Agent未执行PlayMode/手工UI/存读档测试。parent最终已实跑：S1正式40行、S2 BeforeNight七步骤及真实perception1/2分支、信封先不可拆、读完整信精确接P031、末尾两事实、原日程至day3；GM/共享场景五入口、独立预览隔离、暂停释放、开场复用、三个隔离安全快照恢复及正式Stop→重试/Reveal与Read退出菜单均通过。最终直接NUnit/TestCase/协程59/59通过；专用UTF runner被工作区禁用，不称完整UTF通过。详细证据与资源占位见acceptance。此为parent协作验收回报，不是配置Agent自测，也不只凭表存在宣称完成。

最短正式复现路径：①首日交接“测试：跳到 10 月 7 日”→ day=2 正式地图→咖啡馆“开店前的交谈”→S1 完整阅读→正常经营详情（不自动开店）；②S1 完成→结束当天→BeforeNight 的 oct07.s2→七步骤完整阅读→原日程次日。parent已实跑两知觉分支、信纸返回P031、两事实末尾结算、原PreNight及日结至day3地图且CanSave=true；详细证据、最终中断回归及工具限制以 `Docs/superpowers/reports/2026-10-15-october07-acceptance.md` 为准。

### 当前可用预览与替换入口（parent集成补充）

- 实际共享场景为 `Assets/Scenes/PresentationDebugScene.unity`（不是额外子目录Scene）。F1=S1完整；F2/F3=S2知觉1/2完整；F4=oct07.s2.perception；F5=oct07.s2.read_letter。Esc取消，Backspace重播；均读取正式生成配置，由独立DebugSession计算，不结算正式状态。
- 游戏内 `~` → 叙事调试 → 播放控制，同五个按钮。已实跑全部五入口、S1全40条与S2两完整分支，正式Participants/历史隔离已对比通过。节点预览重建夜景空舞台，不继承S1阿黛尔。
- `Assets/Resources/Dialogue/NarrativeStageVisuals.asset`：R01昼、R02街道placeholder、R03夜；替换texture/tint/fadeSeconds，不改正文表。`Assets/Resources/Dialogue/PresentationEffectLibrary.asset`：nod .25s/8，background pan 1.6s/.02/zoom1.05，保留独立背景层。
- `Assets/Resources/Dialogue/PassiveCheckPresentation.asset`：oct07.s2.perception固定词“已察觉”；中文能力名从既有属性配置标签解析，不另写剧情条件算法。
- 信封/拆信/纸图复用 `Assets/Resources/Art/UIPrefab/FirstDayLetterPanel.prefab` 原sprite引用；generic布局不改开场序列和Prefab。原纸图英文装饰属图内嵌，不作正文。
- R04/R06仍缺指定差分，当前回退adele基准图并诊断；R08猫叫sfx仍明确缺失。未用无关美术/声音冒充，R07仍无需处理。
