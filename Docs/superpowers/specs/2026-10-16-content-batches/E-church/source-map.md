# 窗口 E：完整原稿与来源映射

> 本文件是完整施工包的来源附件。请先从唯一入口 [spec.md](./spec.md) 读取全部结论，并完整阅读 [handoff.md](./handoff.md) 的依赖与验收；不要单独按本映射开始施工。

日期：2026-10-16。1 篇长稿 + 2 篇仅标题稿。本表是拟定配置定位，不是已配表记录。结论见 `spec.md`；依赖见 `handoff.md`。

## 1. 编号与文本转换

- E04-PNNN 是 11 月 4 日 DOCX 的非空段落序号（含标题、指令、选项）；同时保留 XML 段号。该稿无表格、手动换行或制表节点；文本节点按正文顺序拼接。原文列完整保留；Markdown 转义只为展示。
- 根 ID `church.1104.s1`；统一 presentationKey=`church.1104.s1.conversation`；页 ID 为根 + 本节 page 后缀；普通正文的 lineId=textKey=`church.1104.s1.pNNN`。页内 seq 只计正文，不计指令和选项。
- `source`、`boundary.*` 是文档/交接节点，不直接生成空 Dialogue 页。各页都在一个根事务内；路由是施工语义，不是现有 schema 已支持的语法。先解决 E-C01/E-C02 再正式配表。
- 对话仅剥离说话人前缀、表情 `（#…）`、奖励说明和明确作者备注；台词内容、引号、标点、数字原样。P034 同一原段的两组引号仍为一行，不丢第二句。旁白原样。
- 确定性技术纠错：P019 阿黛尔反引号异写映射 adele；P166/P266 的 speaker `{playernmae}` 映射 player；P256 正文 `{playernam}`→`{playername}`；P420 空表情只剥离，不赋新表情。其他语病和重复照原文，不润色。
- 身份：埃莉诺→沿既有美术标识 `alienor` 统一补正式 Object（当前表未注册），显示“埃莉诺”须 E-C03 别名支持；阿黛尔各异写→`adele`。旁白/玩家无立绘。无立绘配角拟 ID 根下 npc.mother（女人）、npc.vendor、npc.civilian_a/b、npc.elder_lady、npc.buyer（男子）、npc.tenant（男人）、npc.landlord（房东）。首句“房东？”保留显示的不确定性；买主与租客不合并。
- 旁白沿现有 ConversationLine 的空 speakerKey + side=none，不新增 narrator Object，也不把“留空”字样写入单元格。
- 路线相似段不去重、不串播。市场尾句 P118/P402、郊区尾句 P159–160/P360–361、黄昏 P244/P404 不同；旧城 P161–219/P261–319 相同但各归本路线。

## 2. 工作簿及前置来源

| 只读来源 | 证据与边界 |
|---|---|
| 故事.xlsx / 时间轴-共通线路!C85:D85、F85 | 670.11.03；10.24-S1 后，王女因店外新玫瑰徽记求庇护；庇护才借住三楼。正文目录文件名扫描未找到 11.03 原稿，E-B01；不能用 10.24 完成替代庇护事实。 |
| 同表 C86:D86 | 670.11.04；前一日庇护才游城；已倾向革命联合才辩论。day=30。 |
| 同表 F86 | 蒂博认出乔装王女并报告王庭；没有对应台词。只拟定根完成后隐藏事实，不新增遭遇演出。 |
| 同表 C87:D87、F87 | day=31 的公爵来访两支及管制梗概；两 DOCX 只有标题，不补写剧情或管制系统。 |
| 故事.xlsx / 王庭教会时间轴!C3:C22 | 此专线从 11.11 起，没有本事件的详细定义；依共通线，不从空白格补剧情。 |
| 人物.xlsx / 人物!A7、D7、H7、J7 | 阿莉耶诺尔.康多尔 / 埃莉诺【乔装假名】为同一人；需平民装、无圣痕形象。已有资源未做视觉验收。 |
| 人物!A18、D18；A21、D21、E21、K21 | 阿黛尔为店员；蒂博为残疾老兵/情报商。回忆及背景报告不表示蒂博登场。 |
| D 源 MA70.10.14-S2 / P041、P046–048 | 跛脚工人谈救济，对应 E04-P149–150/P350–351；是否所有可达存档均经历需 E-B04。 |
| D 源 MA70.10.23-S1 / P002、P084；10.23-S2 / P003、P032–034 | 三条脱困方式都失败才 end if2；终局状态阻断本事件。不能将 D 的所有事件完成作为本事件 AND 条件。 |

## 3. 所有选项的显示、可选及选后路由

三组没有源文属性/资金/身份可选条件，均在本组节点显示且可选；不把贵价或急促态度改为灰色。选择先记本根局部快照。金额待定是正式发布阻塞，不是玩家贫穷门槛；不能删/禁第三选项冒充完整事件。

| 原段 | 旧别名 | 拟 Choice ID（根后缀） / Text | 显示 / 可选 | 选择后判定与目标 |
|---|---|---|---|---|
| E04-P041 | MA70.11.03-S1-1 | `fee.free` / `church.1104.s1.p041` | 对应组出现即显示/可选；无门槛 | P045–047 → P069 |
| E04-P042 | MA70.11.03-S1-1 | `fee.one` / `church.1104.s1.p042` | 对应组出现即显示/可选；无门槛 | P049–058 → P069；根尾 MONEY_ADD 1 |
| E04-P043 | MA70.11.03-S1-1 | `fee.quoted` / `church.1104.s1.p043` | 对应组出现即显示/可选；无门槛 | P060–068 → P069；金额未定 |
| E04-P075 | MA70.11.03-S1-2 | `route.far` / `church.1104.s1.p075` | 对应组出现即显示/可选；无门槛 | P078–251 → P412 |
| E04-P076 | MA70.11.03-S1-2 | `route.near` / `church.1104.s1.p076` | 对应组出现即显示/可选；无门槛 | P253–411 → P412 |
| E04-P429 | MA70.11.03-S1-3 | `night.wait` / `church.1104.s1.p429` | 对应组出现即显示/可选；无门槛 | P432；无新判定/奖励 |
| E04-P430 | MA70.11.03-S1-3 | `night.ask` / `church.1104.s1.p430` | 对应组出现即显示/可选；无门槛 | P432；无新判定/奖励 |
| E04-P431 | MA70.11.03-S1-3 | `night.hurry` / `church.1104.s1.p431` | 对应组出现即显示/可选；无门槛 | P432；无新判定/奖励 |

晚间三选项均到 P432，无各自额外回应/关系变化。报酬①无结果行而不是写 MONEY_ADD 0；②在根尾 +1；③待数值决策，隔离预览可看占位但不能写钱。

## 4. 11 月 4 日逐段完整正文

源：`D:\桌面\文案\王庭教会\MA70.11.04-S1.docx`；SHA-256 `85060b970849e5e76c616504ba6621867603b678e1f919a917e8fd8cb0f6b955`；473 非空段。

### 身份与入口｜P001–P002

拟 page/步骤：`church.1104.s1.source`；门：不显示；庇护前置。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P001（1） | 标题；—（不建正文行） | MA70.11.04-S1 | 标题；日期按 11.04，不按旧选项号 |
| E04-P002（2） | 控制；—（不建正文行） | （若MA70.11.03-S1选择庇护则触发） | 入口庇护前置 E-B01 |

### 房间清晨｜P003–P011

拟 page/步骤：`church.1104.s1.morning.wake`；门：根入口通过。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P003（3） | 演出；—（并入舞台/音效步骤） | [背景：主角房间，清晨] | E-R001；P005 前房间清晨、空舞台 |
| E04-P004（4） | 演出；—（并入舞台/音效步骤） | [音效：鸟叫声] | E-R010；P005 鸟叫一次 |
| E04-P005（5） | 正文；seq=1；line/Text `church.1104.s1.p005`；speakerKey留空（旁白）；side=none | 我在窗外叽叽喳喳的鸟叫声中逐渐苏醒 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P006（6） | 正文；seq=2；line/Text `church.1104.s1.p006`；speakerKey留空（旁白）；side=none | 阳光透过床帘照进房间里 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P007（7） | 正文；seq=3；line/Text `church.1104.s1.p007`；speakerKey留空（旁白）；side=none | 感觉今天又是一个好天气呢 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P008（8） | 正文；seq=4；line/Text `church.1104.s1.p008`；speakerKey留空（旁白）；side=none | 昨天下午无比惊险刺激的经历，让我晚上回来之后没多久就陷入了沉睡 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P009（9） | 正文；seq=5；line/Text `church.1104.s1.p009`；speakerKey留空（旁白）；side=none | 啊，差点忘记埃莉诺还借住在咖啡馆的三楼！真没想到她竟然是阿莉耶诺尔王女…… | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P010（10） | 正文；seq=6；line/Text `church.1104.s1.p010`；speakerKey留空（旁白）；side=none | 不知道昨天晚上她睡得怎么样？ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P011（11） | 正文；seq=7；line/Text `church.1104.s1.p011`；speakerKey留空（旁白）；side=none | 等会去问问，现在先准备开店吧！ | 按本页门显示；正文结束后沿 spec 路由 |

### 咖啡馆相见｜P012–P020

拟 page/步骤：`church.1104.s1.morning.cafe`；门：公共。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P012（13） | 演出；—（并入舞台/音效步骤） | [场景转换，咖啡馆内部] | E-R002；P013 前咖啡馆昼 |
| E04-P013（14） | 正文；seq=1；line/Text `church.1104.s1.p013`；speakerKey留空（旁白）；side=none | 等我到达咖啡馆时，没想到埃莉诺和阿黛尔已经在大堂中了 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P014（15） | 演出；—（并入舞台/音效步骤） | [出现王女、阿黛尔的立绘] | E-R003/004；P015 前两人一起入场 |
| E04-P015（16） | 正文；seq=2；line/Text `church.1104.s1.p015`；speaker=player；side=none | {playername}：「早上好，两位女士。」 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P016（17） | 正文；seq=3；line/Text `church.1104.s1.p016`；speaker=alienor；side=left | 埃莉诺：「早上好，{playername}店长。」 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P017（18） | 正文；seq=4；line/Text `church.1104.s1.p017`；speaker=adele；side=right | 阿黛尔·米尼耶：「早上好，{playername}店长！没想到我们会来得这么早吧！告诉你一件大事——」 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P018（19） | 正文；seq=5；line/Text `church.1104.s1.p018`；speaker=player；side=none | {playername}：「什么大事？」 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P019（20） | 正文；seq=6；line/Text `church.1104.s1.p019`；speaker=adele；side=right | 阿黛尔`米尼耶：「阿莉，啊不，埃莉诺说她希望我们能带她去圣诺特的各个区去转一转！」 | 按本页门显示；正文结束后沿 spec 路由；异写映射 |
| E04-P020（21） | 正文；seq=7；line/Text `church.1104.s1.p020`；speakerKey留空（旁白）；side=none | 我将目光转向一旁的埃莉诺 | 按本页门显示；正文结束后沿 spec 路由 |

### 知觉成功｜P021–P028

拟 page/步骤：`church.1104.s1.morning.perception.pass`；门：morning.perception=true。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P021（23） | 控制；—（不建正文行） | （知觉≥3触发） | 只取 morning.perception 一次快照；true→P022，false→P030 |
| E04-P022（24） | 正文；seq=1；line/Text `church.1104.s1.p022`；speakerKey留空（旁白）；side=none | 或许昨晚睡得不太好，她的面色看起来有一些憔悴 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P023（25） | 正文；seq=2；line/Text `church.1104.s1.p023`；speakerKey留空（旁白）；side=none | 眼神里却满是活力和期待 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P024（26） | 正文；seq=3；line/Text `church.1104.s1.p024`；speakerKey留空（旁白）；side=none | 特别像幼时我出门郊游前一夜的状态 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P025（27） | 正文；seq=4；line/Text `church.1104.s1.p025`；speakerKey留空（旁白）；side=none | 我了然地笑笑，问起我关心的问题 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P026（28） | 正文；seq=5；line/Text `church.1104.s1.p026`；speaker=player；side=none | {playername}：「埃莉诺小姐，您昨晚睡得不好吗？」 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P027（29） | 正文；seq=6；line/Text `church.1104.s1.p027`；speaker=alienor；side=left | 埃莉诺：「是，啊，不，不……{playername}店长，昨晚是我第一次在王宫外过夜，所以我感到有些不习惯，也有一些惊奇。」 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P028（30） | 正文；seq=7；line/Text `church.1104.s1.p028`；speaker=player；side=none | {playername}：「可以理解。那么，埃莉诺小姐，你想出去转一转，具体是去哪里呢？」 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |

### 知觉失败｜P029–P032

拟 page/步骤：`church.1104.s1.morning.perception.fail`；门：同一 morning.perception=false。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P029（32） | 控制；—（不建正文行） | （知觉＜3触发） | 沿同一快照失败支，不再检定 |
| E04-P030（33） | 正文；seq=1；line/Text `church.1104.s1.p030`；speakerKey留空（旁白）；side=none | 她用着期待的眼神看着我 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P031（34） | 正文；seq=2；line/Text `church.1104.s1.p031`；speakerKey留空（旁白）；side=none | 我无法抵挡这般热切的眼神，不得不问道 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P032（35） | 正文；seq=3；line/Text `church.1104.s1.p032`；speaker=player；side=none | {playername}：「埃莉诺小姐，你想出去转一转，具体是去哪里呢？」 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |

### 邀请与报酬选择｜P033–P043

拟 page/步骤：`church.1104.s1.morning.invite`；门：公共；末尾 fee 三选一。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P033（37） | 控制；—（不建正文行） | （收束） | 两支汇合 P034 |
| E04-P034（38） | 正文；seq=1；line/Text `church.1104.s1.p034`；speaker=alienor；side=left | 埃莉诺：「说实话，我并没有一个具体的想法。只是这次机会难得，我得以脱离王宫，成为「埃莉诺」而不是「阿莉耶诺尔」，我想去看看我的老师西瓦尔口中常提到的「外面的世界」的真实的样子……」「不知道你们是否愿意做我的向导？如果你们需要报酬的话，我也可以支付的！」 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P035（39） | 正文；seq=2；line/Text `church.1104.s1.p035`；speakerKey留空（旁白）；side=none | 听完她的话，阿黛尔转过来也充满期待地看着我 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P036（40） | 正文；seq=3；line/Text `church.1104.s1.p036`；speakerKey留空（旁白）；side=none | 不知道她是对向导这个身份还是阿莉耶诺尔的报酬感到期待？ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P037（41） | 正文；seq=4；line/Text `church.1104.s1.p037`；speakerKey留空（旁白）；side=none | 我一边想着这个有些好笑的问题，一边回应着她 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P038（42） | 正文；seq=5；line/Text `church.1104.s1.p038`；speaker=player；side=none | {playername}：「当然。既然你想去看看“外面的世界”，那我们就去北岸的区域逛一逛，但是出于对您的安全考虑，我们不深入那些不安全的地点，可以吗？」 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P039（43） | 正文；seq=6；line/Text `church.1104.s1.p039`；speaker=alienor；side=left | 埃莉诺：「当然，{playername}店长。那报酬呢……您有想法吗？」 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P040（45） | 控制；—（不建正文行） | 选项MA70.11.03-S1-1 | P039 后呈现 fee 组选项 |
| E04-P041（46） | 选项；Choice `church.1104.s1.fee.free`；挂 `morning.invite`；Text `church.1104.s1.p041` | ①「报酬就不需要了。带你闲逛就当我们给自己放个假，我想阿黛尔也是这么想的吧。」 | P045–047 → P069 |
| E04-P042（47） | 选项；Choice `church.1104.s1.fee.one`；挂 `morning.invite`；Text `church.1104.s1.p042` | ②「一个国王币怎么样？我觉得很好，就这么定了。」 | P049–058 → P069；根尾 MONEY_ADD 1 |
| E04-P043（48） | 选项；Choice `church.1104.s1.fee.quoted`；挂 `morning.invite`；Text `church.1104.s1.p043` | ③「xx个国王币怎么样？一个友情价。」【数值未定】 | 作者备注不显示；xx 待定 E-B02；P060–068 → P069；金额未定；E-B02：Q报酬未定 |

### 不收报酬｜P044–P047

拟 page/步骤：`church.1104.s1.fee.free`；门：fee.free。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P044（50） | 控制；—（不建正文行） | 选项MA70.11.03-S1-1①→ | fee.free→P045；结束跳 P069 |
| E04-P045（51） | 正文；seq=1；line/Text `church.1104.s1.p045`；speakerKey留空（旁白）；side=none | 埃莉诺看起来有些惊讶，她转头看向阿黛尔 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P046（52） | 正文；seq=2；line/Text `church.1104.s1.p046`；speaker=adele；side=right；表情=激动 | 阿黛尔·米尼耶：（#激动）「当然啦，我可是很乐意带你看看北岸人民的生活的。没有报酬也完全没问题的呢！」 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P047（53） | 正文；seq=3；line/Text `church.1104.s1.p047`；speaker=alienor；side=left；表情=开心 | 埃莉诺：（#开心）「好的，那等你们空闲下来，我们便出发吧。」 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |

### 一个国王币｜P048–P058

拟 page/步骤：`church.1104.s1.fee.one`；门：fee.one。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P048（55） | 控制；—（不建正文行） | 选项MA70.11.03-S1-1②→ | fee.one→P049；结束跳 P069 |
| E04-P049（56） | 正文；seq=1；line/Text `church.1104.s1.p049`；speaker=alienor；side=left；表情=惊讶 | 埃莉诺：（#惊讶）「只要一个国王币吗，{playername}店长？」 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P050（57） | 正文；seq=2；line/Text `church.1104.s1.p050`；speaker=player；side=none | {playername}：「是的，小姐。一个国王币足矣。」 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P051（58） | 正文；seq=3；line/Text `church.1104.s1.p051`；speaker=adele；side=right；表情=开心 | 阿黛尔·米尼耶：（#开心）「嘿嘿，{playername}店长一定是担心埃莉诺小姐你不好意思才特意想出的办法！一个国王币不多不少，刚刚好！」 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P052（59） | 正文；seq=4；line/Text `church.1104.s1.p052`；speakerKey留空（旁白）；side=none | 埃莉诺的神情看起来有些动容 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P053（60） | 正文；seq=5；line/Text `church.1104.s1.p053`；speaker=alienor；side=left | 埃莉诺：「那就麻烦你们了。」 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P054（61） | 正文；seq=6；line/Text `church.1104.s1.p054`；speakerKey留空（旁白）；side=none | 她拿出一枚国王币交到我的手中【金钱+1】 | 剥离【金钱+1】；记待提交 1，不即时改钱 |
| E04-P055（62） | 正文；seq=7；line/Text `church.1104.s1.p055`；speakerKey留空（旁白）；side=none | 没想到有一天我和奥尔狄亚王国的王女有一天会用一枚小小的货币而联系在一起，真是有意思 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P056（63） | 正文；seq=8；line/Text `church.1104.s1.p056`；speaker=player；side=none | {playername}：「好的。接下来等咖啡馆空闲下来，我们便出发吧。」 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P057（64） | 正文；seq=9；line/Text `church.1104.s1.p057`；speaker=alienor；side=left；表情=开心 | 埃莉诺：（#开心）「好的。」 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P058（65） | 正文；seq=10；line/Text `church.1104.s1.p058`；speaker=adele；side=right；表情=开心 | 阿黛尔·米尼耶：（#开心）「好耶！」 | 按本页门显示；正文结束后沿 spec 路由 |

### 友情价｜P059–P068

拟 page/步骤：`church.1104.s1.fee.quoted`；门：fee.quoted；E-B02。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P059（67） | 控制；—（不建正文行） | 选项MA70.11.03-S1-1③→ | fee.quoted→P060；结束跳 P069 |
| E04-P060（68） | 正文；seq=1；line/Text `church.1104.s1.p060`；speaker=alienor；side=left | 埃莉诺：「没问题，{playername}店长，这个价格并不高。」 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P061（69） | 正文；seq=2；line/Text `church.1104.s1.p061`；speakerKey留空（旁白）；side=none | 她从随身携带的小包中掏出xx个国王币交给我 | 按本页门显示；正文结束后沿 spec 路由；E-B02：Q报酬未定 |
| E04-P062（70） | 演出；—（并入舞台/音效步骤） | [画面中出现十几个国王币] | E-R005；P063 前仅此分支货币图；不是金额依据 |
| E04-P063（71） | 正文；seq=3；line/Text `church.1104.s1.p063`；speaker=alienor；side=left；表情=平静 | 埃莉诺：（#平静）「{playername}店长，你确认一下数目吧。」 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P064（72） | 正文；seq=4；line/Text `church.1104.s1.p064`；speakerKey留空（旁白）；side=none | 我接过这些货币，但没有多看，径直放入包中【金钱+xx】 | 剥离【金钱+xx】；待定不生成正式结果；E-B02：Q报酬未定 |
| E04-P065（73） | 正文；seq=5；line/Text `church.1104.s1.p065`；speakerKey留空（旁白）；side=none | 面对埃莉诺疑惑的眼神，我笑着说道 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P066（74） | 正文；seq=6；line/Text `church.1104.s1.p066`；speaker=player；side=none；表情=开心 | {playername}：（#开心）「埃莉诺小姐，我想我们之间还是有着基本的信任。那么，稍等咖啡馆空闲下来，我们便出发吧。」 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P067（75） | 正文；seq=7；line/Text `church.1104.s1.p067`；speaker=alienor；side=left；表情=开心 | 埃莉诺：（#开心）「好的。」 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P068（76） | 正文；seq=8；line/Text `church.1104.s1.p068`；speaker=adele；side=right；表情=开心 | 阿黛尔·米尼耶：（#开心）「好耶！」 | 按本页门显示；正文结束后沿 spec 路由 |

### 实际经营交接｜P069–P070

拟 page/步骤：`church.1104.s1.boundary.business`；门：报酬任一分支结束；E-C01。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P069（78） | 控制；—（不建正文行） | （选项收束，进入咖啡馆经营） | 交实际经营，保留根待提交结果；不标完成 |
| E04-P070（79） | 控制；—（不建正文行） | （经营结束之后） | 只在实际经营结束 AfterBusiness 续 P071 |

### 经营后选择路线｜P071–P077

拟 page/步骤：`church.1104.s1.tour.depart`；门：实际经营完成；route 二选一。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P071（80） | 正文；seq=1；line/Text `church.1104.s1.p071`；speaker=adele；side=right | 阿黛尔·米尼耶：「呼，终于忙完啦！接下来挂上一个“暂停营业”的牌子我们便出发吧！我们先去哪里呢，{playername}店长？」 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P072（81） | 正文；seq=2；line/Text `church.1104.s1.p072`；speaker=player；side=none | {playername}：「埃莉诺小姐，你有想先去的地方吗？」 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P073（82） | 正文；seq=3；line/Text `church.1104.s1.p073`；speaker=alienor；side=left | 埃莉诺：「没有，由你们决定吧。」 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P074（84） | 控制；—（不建正文行） | 选项MA70.11.03-S1-2 | P073 后呈现 route 二选一 |
| E04-P075（85） | 选项；Choice `church.1104.s1.route.far`；挂 `tour.depart`；Text `church.1104.s1.p075` | ①「为了节省时间，我们采取由远及近的方式游览吧！即从大堂区到圣西尔万郊区再到旧城区，最后回到圣徒街。」 | P078–251 → P412 |
| E04-P076（86） | 选项；Choice `church.1104.s1.route.near`；挂 `tour.depart`；Text `church.1104.s1.p076` | ②「为了节省时间，我们采取由近及远的方式游览吧！即从圣徒街到旧城区再到圣西尔万郊区，然后去到大堂区，最后回到咖啡馆。」 | P253–411 → P412 |
| E04-P077（88） | 控制；—（不建正文行） | 选项MA70.11.03-S1-2①→ | route.far→P078；P251 后跳 P412 |

### 远到近：市场｜P078–P118

拟 page/步骤：`church.1104.s1.far.market`；门：route.far。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P078（89） | 演出；—（并入舞台/音效步骤） | [场景转换：大堂区的露天大市场画面] | E-R006；P080 前市场 |
| E04-P079（90） | 演出；—（并入舞台/音效步骤） | [音效：市场里人声嘈杂] | E-R011；P080 市场人声 |
| E04-P080（91） | 正文；seq=1；line/Text `church.1104.s1.p080`；speakerKey留空（旁白）；side=none | 大堂区的露天市场比别处热闹得多，哪怕时间已经到了下午，依旧有不少人聚集在这里 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P081（92） | 正文；seq=2；line/Text `church.1104.s1.p081`；speakerKey留空（旁白）；side=none | 蔬菜、旧衣、二手家具和廉价香料堆放在摊位上，车夫的吆喝、摊贩的叫卖和顾客的讨价还价声交杂在一起 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P082（94） | 正文；seq=3；line/Text `church.1104.s1.p082`；speaker=adele；side=right | 阿黛尔·米尼耶：大堂区主要是普通百姓居住的地方，但这边有一个很大的市场，所以超级热闹！不过要早点来，晚了只剩蔫的。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P083（95） | 正文；seq=4；line/Text `church.1104.s1.p083`；speakerKey留空（旁白）；side=none | 阿黛尔已经熟练地凑到附近的摊位上看起来了 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P084（96） | 正文；seq=5；line/Text `church.1104.s1.p084`；speakerKey留空（旁白）；side=none | 埃莉诺缓步则走向一处旧衣摊位前查看起来 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P085（97） | 正文；seq=6；line/Text `church.1104.s1.p085`；speakerKey留空（旁白）；side=none | 我凑上前去看了一眼，摊上的衣服没什么特别的，就是普通二手旧衣 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P086（98） | 正文；seq=7；line/Text `church.1104.s1.p086`；speakerKey留空（旁白）；side=none | 随处可见的样式，随处可见的材质 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P087（99） | 正文；seq=8；line/Text `church.1104.s1.p087`；speakerKey留空（旁白）；side=none | 不过这种“随处可见”对王女来说可能也是少见的呢…… | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P088（101） | 正文；seq=9；line/Text `church.1104.s1.p088`；speakerKey留空（旁白）；side=none | 我又仔细看了看才发现埃莉诺的目光并非停留在摊位的衣服上 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P089（102） | 正文；seq=10；line/Text `church.1104.s1.p089`；speakerKey留空（旁白）；side=none | 她关注着摊位旁一对母女 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P090（103） | 正文；seq=11；line/Text `church.1104.s1.p090`；speakerKey留空（旁白）；side=none | 那对母女穿着相当朴素，女儿身上衣服还算得整洁，母亲身上的衣服却满是补丁的痕迹，颜色灰扑扑的，一看就是穿了很多年的衣服了 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P091（104） | 正文；seq=12；line/Text `church.1104.s1.p091`；speakerKey留空（旁白）；side=none | 看起来家庭普通却对女儿很好呢 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P092（105） | 正文；seq=13；line/Text `church.1104.s1.p092`；speakerKey留空（旁白）；side=none | 她们在摊位旁看了很久，最终仿佛终于鼓起勇气一般向摊主发问 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P093（106） | 正文；seq=14；line/Text `church.1104.s1.p093`；speaker=church.1104.s1.npc.mother；side=none | 女人：请问那件外套多少钱？ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P094（107） | 正文；seq=15；line/Text `church.1104.s1.p094`；speakerKey留空（旁白）；side=none | 我们顺着她的手指看过去，那是一件深灰色旧羊毛外套。样式倒是好看，只是袖口磨亮，衣襟有明显缝补痕迹，肩线歪了，扣子大小并不相同，内衬也用不同颜色的布重新缝过。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P095（108） | 正文；seq=16；line/Text `church.1104.s1.p095`；speaker=church.1104.s1.npc.vendor；side=none | 摊主：嘿，夫人，你可真有眼光。这是我最近刚拿到手的一件好货，最初是旧城区的旧贵族们穿的，后来几经转手到了我手上啦！你可别看它外表不太好看，但是里面羊毛含量非常高，穿起来特别保暖，保你一整个冬天都不会感到寒冷！不过也因此嘛，它的售价不会太便宜，xx国王币。 | 按本页门显示；正文结束后沿 spec 路由；E-B02：C外套报价未定 |
| E04-P096（109） | 正文；seq=17；line/Text `church.1104.s1.p096`；speaker=church.1104.s1.npc.mother；side=none | 女人：什么？！我也在你的摊位上买过很多衣服了，第一次遇见这个价格的！ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P097（110） | 正文；seq=18；line/Text `church.1104.s1.p097`；speaker=church.1104.s1.npc.vendor；side=none | 摊主：夫人，你摸摸看呀，这个衣服和别的不一样的！ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P098（112） | 正文；seq=19；line/Text `church.1104.s1.p098`；speakerKey留空（旁白）；side=none | 埃莉诺转头悄悄地和我们讨论起这件羊毛外套 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P099（113） | 正文；seq=20；line/Text `church.1104.s1.p099`；speaker=alienor；side=left | 埃莉诺：虽然这件衣服已然被改得不如从前，但我看一眼就知道这确实是上好的羊毛制成的，原料费、人工费算上的话，xx国王币对于这件衣服而言甚至算得上便宜啦。【目前不太清楚这个国家的消费水平来着】 | 作者消费水平备注不显示；金额 E-B02；显示别名埃莉诺；E-C03；E-B02：C外套成本/售价讨论未定 |
| E04-P100（114） | 正文；seq=21；line/Text `church.1104.s1.p100`；speaker=adele；side=right | 阿黛尔·米尼耶：天呢，埃莉诺小姐！那可是xx国王币！这只是一件外套而已。 | 按本页门显示；正文结束后沿 spec 路由；E-B02：C外套报价未定 |
| E04-P101（115） | 正文；seq=22；line/Text `church.1104.s1.p101`；speaker=alienor；side=left | 埃莉诺：……可那确实是由上等的羊毛制成的，摊主没有说错，冬天穿上就再也不会感到寒冷，而且可以一直传下去呢。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P102（116） | 正文；seq=23；line/Text `church.1104.s1.p102`；speaker=adele；side=right | 阿黛尔·米尼耶：小姐，你知道xx国王币够一个普通家庭活多少天了吗？ | 按本页门显示；正文结束后沿 spec 路由；E-B02：C所对应生活天数比较；不新增天数常量 |
| E04-P103（117） | 正文；seq=24；line/Text `church.1104.s1.p103`；speakerKey留空（旁白）；side=none | 埃莉诺沉默下来了 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P104（119） | 正文；seq=25；line/Text `church.1104.s1.p104`；speakerKey留空（旁白）；side=none | 摊位前的争论也接近尾声 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P105（120） | 正文；seq=26；line/Text `church.1104.s1.p105`；speakerKey留空（旁白）；side=none | 那个母亲带着满脸喜爱和惋惜最后摸了摸外套，然后带着孩子离开了 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P106（121） | 正文；seq=27；line/Text `church.1104.s1.p106`；speakerKey留空（旁白）；side=none | 埃莉诺望着她们远去的背影没有说话 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P107（123） | 正文；seq=28；line/Text `church.1104.s1.p107`；speaker=adele；side=right | 阿黛尔·米尼耶：埃莉诺小姐，您请跟我来。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P108（124） | 正文；seq=29；line/Text `church.1104.s1.p108`；speakerKey留空（旁白）；side=none | 阿黛尔带着埃莉诺来到一个贩卖蔬菜水果的摊位前 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P109（125） | 正文；seq=30；line/Text `church.1104.s1.p109`；speaker=adele；side=right | 阿黛尔·米尼耶：小姐，你觉得xx国王币在这个摊位可以买到多少东西呢？ | 按本页门显示；正文结束后沿 spec 路由；E-B02：F蔬果购买力；是否等于C待确认 |
| E04-P110（126） | 正文；seq=31；line/Text `church.1104.s1.p110`；speaker=alienor；side=left | 埃莉诺：……几根胡萝卜加几个洋葱？ | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P111（127） | 正文；seq=32；line/Text `church.1104.s1.p111`；speaker=adele；side=right | 阿黛尔·米尼耶：还能再买好几个土豆和苹果呢，这已经足够一个三口家庭吃好几天啦。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P112（128） | 正文；seq=33；line/Text `church.1104.s1.p112`；speaker=alienor；side=left | 埃莉诺：我在……的时候学习过税制、贸易和经济体制，我的老师曾经让我接触过一些账单，我以为我了解这些，但显然我错了。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P113（129） | 正文；seq=34；line/Text `church.1104.s1.p113`；speaker=adele；side=right | 阿黛尔·米尼耶：那也很厉害啦。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P114（130） | 正文；seq=35；line/Text `church.1104.s1.p114`；speaker=alienor；side=left | 埃莉诺：……但是，我学的知识里不会告诉我体制下xx国王币够一家三口生活几天。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03；E-B02：家庭生活费用引用；与C/F关系待确认 |
| E04-P115（132） | 正文；seq=36；line/Text `church.1104.s1.p115`；speakerKey留空（旁白）；side=none | 埃莉诺没有再说话，她只是默默地沿着道路向前走着 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P116（133） | 正文；seq=37；line/Text `church.1104.s1.p116`；speakerKey留空（旁白）；side=none | 她时不时停留在摊位旁，询问摊主价格，然后认真地记录下来 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P117（134） | 正文；seq=38；line/Text `church.1104.s1.p117`；speakerKey留空（旁白）；side=none | 我和阿黛尔只是无言地跟在她身后 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P118（135） | 正文；seq=39；line/Text `church.1104.s1.p118`；speakerKey留空（旁白）；side=none | 直到埃莉诺大致浏览完附近的摊位，回过头告诉我们可以出发去下一个地点了 | 按本页门显示；正文结束后沿 spec 路由 |

### 远到近：郊区｜P119–P160

拟 page/步骤：`church.1104.s1.far.suburb`；门：route.far。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P119（138） | 演出；—（并入舞台/音效步骤） | [场景转换：圣西尔万郊区] | E-R007；P121 前郊区 |
| E04-P120（139） | 演出；—（并入舞台/音效步骤） | [音效：人声鼎沸] | E-R012；P121 人声；不改 P123 人少的原文 |
| E04-P121（140） | 正文；seq=1；line/Text `church.1104.s1.p121`；speaker=player；side=none | {playername}：这里就是圣西尔万郊区了。这里聚集了大量工匠、工人和外省移民的穷人，可以说这里是底层人民最集中的片区。我们就顺着向前逛逛吧，埃莉诺小姐，这边人多且杂乱，你一定要跟紧我们。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P122（141） | 正文；seq=2；line/Text `church.1104.s1.p122`；speaker=alienor；side=left | 埃莉诺：好的。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P123（143） | 正文；seq=3；line/Text `church.1104.s1.p123`；speakerKey留空（旁白）；side=none | 或许由于是下午时分，大多数人还在上班，街道上的人并不多 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P124（144） | 正文；seq=4；line/Text `church.1104.s1.p124`；speakerKey留空（旁白）；side=none | 但也聚集了一些无业和失业的流民，男人们穿着洗得发白、肘部磨破的上衣的上衣坐在路边，有人把烟卷夹在指间，有人叼在嘴角，时不时深吸一口，但都舍不得抽得太快。街道上飘散着一股烟草气混着煤灰、渣土和酒精的味道 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P125（145） | 正文；seq=5；line/Text `church.1104.s1.p125`；speakerKey留空（旁白）；side=none | 偶尔有招工的人从街口经过，他们便抬头望一眼，若那人没有停下，又很快低下头去，猛吸一口香烟。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P126（146） | 正文；seq=6；line/Text `church.1104.s1.p126`；speakerKey留空（旁白）；side=none | 女人们则少聚集在街边，多在民居门口。有人带着孩子，有人在门口纺织，还有一些女人穿着暴露，在房屋门口招揽着来往的男人。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P127（148） | 正文；seq=7；line/Text `church.1104.s1.p127`；speaker=player；side=none | {playername}：埃莉诺小姐，你会怎么评价这些人呢？ | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P128（149） | 正文；seq=8；line/Text `church.1104.s1.p128`；speaker=alienor；side=left | 埃莉诺：……我不知道。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P129（150） | 正文；seq=9；line/Text `church.1104.s1.p129`；speaker=player；side=none | {playername}：好吃懒做？不务正事？颓废阴暗？ | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P130（151） | 正文；seq=10；line/Text `church.1104.s1.p130`；speaker=alienor；side=left | 埃莉诺：……抱歉，但我想不完全是。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P131（152） | 正文；seq=11；line/Text `church.1104.s1.p131`；speakerKey留空（旁白）；side=none | 我没有再询问，我们继续往前走着 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P132（154） | 正文；seq=12；line/Text `church.1104.s1.p132`；speakerKey留空（旁白）；side=none | 突然我们远远看见一处棚子，上面有王室和教会的标志 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P133（155） | 正文；seq=13；line/Text `church.1104.s1.p133`；speaker=adele；side=right | 阿黛尔·米尼耶：那是什么？ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P134（156） | 正文；seq=14；line/Text `church.1104.s1.p134`；speaker=player；side=none | {playername}：不知道，你们在此等候，我上前去看看吧。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P135（158） | 正文；seq=15；line/Text `church.1104.s1.p135`；speakerKey留空（旁白）；side=none | 棚子下并没有看见有王庭或者教会打扮的人，只有几个平民坐在棚下休息。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P136（159） | 正文；seq=16；line/Text `church.1104.s1.p136`；speaker=player；side=none | {playername}：你好，请问这个棚子是用来干什么的？我看见上面有王庭和教会的标识。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P137（160） | 正文；seq=17；line/Text `church.1104.s1.p137`；speaker=church.1104.s1.npc.civilian_a；side=none | 平民A：嗨呀，这个棚子就是之前埃利昂公爵带人来发放粥和面包的时候搭建的，说之后会经常来施粥呢！ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P138（161） | 正文；seq=18；line/Text `church.1104.s1.p138`；speaker=church.1104.s1.npc.civilian_b；side=none | 平民B：哼，每次只拿那么一点东西来，我看就是作秀罢了。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P139（162） | 正文；seq=19；line/Text `church.1104.s1.p139`；speaker=church.1104.s1.npc.civilian_a；side=none | 平民A：你怎么这么说呢？现在国家经济也不好，埃利昂公爵愿意带人来我们这种地方施粥已经很好了。上次发现斗殴事件之后，公爵大人不也说要整治吗？ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P140（163） | 正文；seq=20；line/Text `church.1104.s1.p140`；speaker=player；side=none | {playername}：……斗殴事件？ | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P141（164） | 正文；seq=21；line/Text `church.1104.s1.p141`；speaker=church.1104.s1.npc.civilian_a；side=none | 平民A：是的，哎呀，就是上次埃利昂公爵来施粥，但是数量有限，有的人为了争夺就打起来啦。我看也不是埃利昂公爵的错。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P142（165） | 正文；seq=22；line/Text `church.1104.s1.p142`；speaker=church.1104.s1.npc.civilian_b；side=none | 平民B：怎么不是他的错了？有本事多带点食物来，他又不是不知道我们国家这么多人都吃不饱，要我说就是他们王庭和教会的错！ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P143（166） | 正文；seq=23；line/Text `church.1104.s1.p143`；speaker=church.1104.s1.npc.civilian_a；side=none | 平民A：哎哟，大哥，你可小心着说话吧！ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P144（167） | 正文；seq=24；line/Text `church.1104.s1.p144`；speaker=church.1104.s1.npc.civilian_b；side=none | 平民B：怎么？有本事立马派人把我抓进去，我还怕他们不成？ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P145（169） | 正文；seq=25；line/Text `church.1104.s1.p145`；speakerKey留空（旁白）；side=none | 眼见两位大哥的话题逐渐转向其他地方，我赶紧撤离，把刚才打听到的消息转告给她们。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P146（170） | 正文；seq=26；line/Text `church.1104.s1.p146`；speaker=alienor；side=left | 埃莉诺：埃利昂叔叔确实是一个很好的人呢……我之前也有听闻过他在民间的名声很望。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P147（171） | 正文；seq=27；line/Text `church.1104.s1.p147`；speaker=adele；side=right | 阿黛尔·米尼耶：我好像也有听说过前段时间埃利昂公爵在北岸施粥的事呢！ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P148（172） | 正文；seq=28；line/Text `church.1104.s1.p148`；speaker=player；side=none | {playername}：是的。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P149（173） | 正文；seq=29；line/Text `church.1104.s1.p149`；speakerKey留空（旁白）；side=none | 我突然回想起，前几日似乎有一个跛脚的工人也在我面前提过埃利昂公爵施粥的事 | D/1014 P041、P046–048 前情；E-B04 |
| E04-P150（174） | 正文；seq=30；line/Text `church.1104.s1.p150`；speakerKey留空（旁白）；side=none | 不过他没有提及背后还有斗殴事件，还真是复杂啊 | 同 E-B04 |
| E04-P151（176） | 正文；seq=31；line/Text `church.1104.s1.p151`；speakerKey留空（旁白）；side=none | 我们没有就这个话题深入聊下来，继续向前走着 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P152（178） | 正文；seq=32；line/Text `church.1104.s1.p152`；speakerKey留空（旁白）；side=none | 混乱、无序、颓废 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P153（179） | 正文；seq=33；line/Text `church.1104.s1.p153`；speakerKey留空（旁白）；side=none | 初次来到这里的人一定会有这样的印象 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P154（180） | 正文；seq=34；line/Text `church.1104.s1.p154`；speakerKey留空（旁白）；side=none | 但我们也在这里看见有人积极地生活着，邻里互相帮助着，人们团结着，相互依靠着 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P155（181） | 正文；seq=35；line/Text `church.1104.s1.p155`；speakerKey留空（旁白）；side=none | 在生活的压迫下寻找着一条可以前行的道路 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P156（183） | 正文；seq=36；line/Text `church.1104.s1.p156`；speakerKey留空（旁白）；side=none | 埃莉诺偶尔询问着我们关于人们生活的问题，我们一一回答 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P157（184） | 正文；seq=37；line/Text `church.1104.s1.p157`；speakerKey留空（旁白）；side=none | 她偶尔也会长久地凝望着那些在路边抽烟的颓废的男人和来往的牵着孩子的行色匆匆的妇人 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P158（185） | 正文；seq=38；line/Text `church.1104.s1.p158`；speakerKey留空（旁白）；side=none | 她偶尔也会为路边穿着破烂但是聚集在一起玩得很开心的孩子们，在阳光下悠闲散步的小猫而停留 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P159（187） | 正文；seq=39；line/Text `church.1104.s1.p159`；speakerKey留空（旁白）；side=none | 我们沿着圣西尔万郊区一直走到旧城区附近 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P160（188） | 正文；seq=40；line/Text `church.1104.s1.p160`；speaker=alienor；side=left | 埃莉诺：谢谢你们。虽然有些突然，但如果不是你们，我或许不能够如此顺利地了解外面的世界，接下来，让我们去旧城区看看吧。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |

### 远到近：旧城入口｜P161–P167

拟 page/步骤：`church.1104.s1.far.old.intro`；门：route.far。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P161（190） | 演出；—（并入舞台/音效步骤） | [场景转换：旧城区画面] | E-R008；P163 前旧城 |
| E04-P162（191） | 演出；—（并入舞台/音效步骤） | [人声嘈杂声，但不是很大声] | E-R013；P163 较低人声 |
| E04-P163（192） | 正文；seq=1；line/Text `church.1104.s1.p163`；speaker=player；side=none | {playername}：这就是北岸的旧城区。你有什么初步印象吗，埃莉诺小姐？ | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P164（193） | 正文；seq=2；line/Text `church.1104.s1.p164`；speaker=alienor；side=left；表情=思索 | 埃莉诺：（#思索）嗯……我觉得，这里的房子看起来挺好的，但是略微有些破败。以及这里的街道好狭窄，感觉勉强只能够一个人过，与王宫和圣徒街都不一样呢。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P165（194） | 正文；seq=3；line/Text `church.1104.s1.p165`；speaker=adele；side=right | 阿黛尔·米尼耶：是的，小姐。旧城区最初是贵族们居住的地方，所以这里的房子多独栋宅邸，但是后来这些贵族权力式微，渐渐被新发展的权贵取代，他们开始被称为“旧贵族”。后来，有部分旧贵族连这样的生活都难以负担，便将房子分割为无数的小房间，出租给底层人民，以获取一些收入。甚至有些旧贵族抛下房子离开，房子陷入无主的状态，很快就被附近的人们占领，再分割成不同的房间…… | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P166（195） | 正文；seq=4；line/Text `church.1104.s1.p166`；speaker=player；side=none | {playernmae}：由此形成了如今旧城区旧贵族与底层人民，独栋宅邸与群租民房同时出现的景象。我们继续往里走一点吧，埃莉诺小姐。 | 错误 speaker {playernmae} 映射 player；speaker技术纠错 |
| E04-P167（196） | 正文；seq=5；line/Text `church.1104.s1.p167`；speaker=alienor；side=left | 埃莉诺：好、好的。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |

### 远到近：知觉插段｜P168–P171

拟 page/步骤：`church.1104.s1.far.old.perception`；门：route.far 且 old.perception=true。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P168（198） | 控制；—（不建正文行） | （知觉≥3开启） | old.perception=true→P169–170→P172；false→P172 |
| E04-P169（199） | 正文；seq=1；line/Text `church.1104.s1.p169`；speakerKey留空（旁白）；side=none | 从说话的语气看来，埃莉诺受到了些许震撼呢 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P170（200） | 正文；seq=2；line/Text `church.1104.s1.p170`；speakerKey留空（旁白）；side=none | 是哪一部分呢？贵族式微，还是贫民进入贵族的地盘？ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P171（201） | 控制；—（不建正文行） | （知觉＜3无事发生） | 失败不显示提示/空行，直接 P172 |

### 远到近：宅邸介绍｜P172–P172

拟 page/步骤：`church.1104.s1.far.old.house`；门：route.far。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P172（203） | 正文；seq=1；line/Text `church.1104.s1.p172`；speaker=player；side=none | {playername}：这是旧城区最大的宅邸，归属于旧贵族吕西尼昂家族。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |

### 远到近：宅邸远景｜P173–P180

拟 page/步骤：`church.1104.s1.far.old.estate`；门：route.far。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P173（204） | 演出；—（并入舞台/音效步骤） | [美术：狭窄的街道尽头，一座旧式宅邸沉默地立在那里。铁门锈迹斑斑，庭院杂草丛生，褪色的家徽（随便画个圆形或者方形然后上面写Lusignan就行，反正很小也看不清）却仍端端正正地嵌在门楣上。] | E-R014；P174 前宅邸画面 |
| E04-P174（205） | 正文；seq=1；line/Text `church.1104.s1.p174`；speaker=alienor；side=left | 埃莉诺：我曾经听闻过这个家族的名字，似乎是奥尔狄亚王国早期的一批贵族之一。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P175（206） | 正文；seq=2；line/Text `church.1104.s1.p175`；speaker=adele；side=right | 阿黛尔·米尼耶：是的，听说他们家族鼎盛时期有好几个庄园呢！ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P176（207） | 正文；seq=3；line/Text `church.1104.s1.p176`；speaker=alienor；side=left | 埃莉诺：现在看起来……虽然荒芜，却也不小呢。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P177（208） | 正文；seq=4；line/Text `church.1104.s1.p177`；speaker=adele；side=right | 阿黛尔：外面看起来确实大，但是里面早就空啦。地卖了，马车卖了，听说连好些房间的家具也卖掉了。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P178（209） | 正文；seq=5；line/Text `church.1104.s1.p178`；speaker=alienor；side=left | 埃莉诺：…… | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P179（210） | 正文；seq=6；line/Text `church.1104.s1.p179`；speakerKey留空（旁白）；side=none | 谁也没有说话，我们站在吕西尼昂宅邸门口不远处观赏着过去的时代留下的庞然大物 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P180（211） | 正文；seq=7；line/Text `church.1104.s1.p180`；speakerKey留空（旁白）；side=none | 突然宅邸的大门打开，一位老妇人和一个穿着富贵的男人从宅邸中出来 | 按本页门显示；正文结束后沿 spec 路由 |

### 远到近：传家物｜P181–P191

拟 page/步骤：`church.1104.s1.far.old.heirloom`；门：route.far。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P181（212） | 演出；—（并入舞台/音效步骤） | [美术：在大门模糊画两个人影，一高一矮] | E-R015；P182 前远处二人影 |
| E04-P182（213） | 正文；seq=1；line/Text `church.1104.s1.p182`；speakerKey留空（旁白）；side=none | 他们的声音顺着风隐隐地传进我们的耳朵里 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P183（214） | 正文；seq=2；line/Text `church.1104.s1.p183`；speaker=church.1104.s1.npc.elder_lady；side=none | 老妇人：先生，请你回去之后好好对待这只茶杯，它是个老物件啦，已经在我们家传了很多代了，如果不是现在条件实在不好，我是舍不得卖掉它的…… | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P184（215） | 正文；seq=3；line/Text `church.1104.s1.p184`；speaker=church.1104.s1.npc.buyer；side=none | 男子：我明白的，夫人。您止步，我就先走了。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P185（216） | 正文；seq=4；line/Text `church.1104.s1.p185`；speakerKey留空（旁白）；side=none | 男子道别之后便转身离开了 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P186（217） | 正文；seq=5；line/Text `church.1104.s1.p186`；speakerKey留空（旁白）；side=none | 那位老妇人仍站在宅邸门口目送着男子的离开 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P187（218） | 正文；seq=6；line/Text `church.1104.s1.p187`；speakerKey留空（旁白）；side=none | 她看起来已有六七十岁，衣裙看起来是由贵族才会用的高级布料制成，但是已经发暗，袖口还有一些缝补的痕迹。显然，过去的生活已经消失在时代的洪流中，如今不复往昔。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P188（219） | 正文；seq=7；line/Text `church.1104.s1.p188`；speaker=alienor；side=left | 埃莉诺：已经传了很多代，想必已经是传家宝级别了吧。这样珍贵的东西，也要卖掉吗？ | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P189（220） | 正文；seq=8；line/Text `church.1104.s1.p189`；speakerKey留空（旁白）；side=none | 我和阿黛尔没有立刻回答，许久我才听见阿黛尔淡淡地回答道 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P190（221） | 正文；seq=9；line/Text `church.1104.s1.p190`；speaker=adele；side=right | 阿黛尔·米尼耶：或许，实在是没有办法了吧。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P191（222） | 正文；seq=10；line/Text `church.1104.s1.p191`；speakerKey留空（旁白）；side=none | 我们正准备退出这条街道时，突然旁边的宅邸中传来激烈的争执声。 | 按本页门显示；正文结束后沿 spec 路由 |

### 远到近：驱逐｜P192–P199

拟 page/步骤：`church.1104.s1.far.old.eviction`；门：route.far。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P192（223） | 演出；—（并入舞台/音效步骤） | [音效：激烈的争吵声] | E-R016；P194 前争吵音 |
| E04-P193（224） | 演出；—（并入舞台/音效步骤） | [美术：一个男人，一个抱孩子的女人，一个老人，剪影即可] | E-R017；P194 前一家三人剪影 |
| E04-P194（225） | 正文；seq=1；line/Text `church.1104.s1.p194`；speakerKey留空（旁白）；side=none | 一个男人扛着一口大锅走出来，身后跟着一个抱着孩子的女人和一个步履蹒跚的老人 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P195（226） | 正文；seq=2；line/Text `church.1104.s1.p195`；speakerKey留空（旁白）；side=none | 另一个疑似房东的男人双手抱胸站在门口，轻蔑地看着他们 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P196（227） | 正文；seq=3；line/Text `church.1104.s1.p196`；speaker=church.1104.s1.npc.landlord；side=none | 房东？：我已经给过你们很多机会了，但是你们一拖再拖，就别怪我不客气了。 | 按本页门显示；正文结束后沿 spec 路由；此句显示房东？；E-C03 |
| E04-P197（228） | 正文；seq=4；line/Text `church.1104.s1.p197`；speaker=church.1104.s1.npc.tenant；side=none | 男人：大人，长官，阁下，先生，我求求您了，再宽限两天吧……您看孩子还病着……她还那么小，不能病着跟我们奔波呀…… | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P198（229） | 正文；seq=5；line/Text `church.1104.s1.p198`；speaker=church.1104.s1.npc.landlord；side=none | 房东：我就是看在孩子的份上才给了你们几次机会，哼，谁让你们一直拖着不肯交租？我也不是做慈善的，没有钱交租就得给我滚出去。 | 按本页门显示；正文结束后沿 spec 路由；此句显示房东 |
| E04-P199（230） | 正文；seq=6；line/Text `church.1104.s1.p199`；speakerKey留空（旁白）；side=none | 说完房东便头也不回地离开，将大门狠狠地关上 | 按本页门显示；正文结束后沿 spec 路由 |

### 远到近：居住讨论｜P200–P219

拟 page/步骤：`church.1104.s1.far.old.aftermath`；门：route.far。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P200（231） | 演出；—（并入舞台/音效步骤） | [很重的关门声] | E-R018；P201 前关门声一次 |
| E04-P201（232） | 正文；seq=1；line/Text `church.1104.s1.p201`；speaker=church.1104.s1.npc.tenant；side=none | 男人：阁下……阁下…… | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P202（233） | 正文；seq=2；line/Text `church.1104.s1.p202`；speakerKey留空（旁白）；side=none | 男人又叫唤了两声，没有任何应答 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P203（234） | 正文；seq=3；line/Text `church.1104.s1.p203`；speakerKey留空（旁白）；side=none | 他失望地垂下头，只把锅扛得更稳些，招呼着后面的女人和老人向外走 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P204（235） | 正文；seq=4；line/Text `church.1104.s1.p204`；speaker=alienor；side=left | 埃莉诺：他们这是……要去哪里？他们能找到地方住吗？这个房东也太坏了，孩子生病，为什么不可以多宽限几天呢？ | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P205（236） | 正文；seq=5；line/Text `church.1104.s1.p205`；speaker=adele；side=right | 阿黛尔：……不知道，先找个能过夜的地方吧。旧城区总有人家愿意挤一挤，也可能去郊区寻找流浪儿聚集的地方。而且，房东也不一定就是一个纯粹的坏人。在旧城区的人，多少都是在生活上有困难的人。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P206（237） | 正文；seq=6；line/Text `church.1104.s1.p206`；speaker=player；side=none | {playername}：…… | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P207（238） | 正文；seq=7；line/Text `church.1104.s1.p207`；speakerKey留空（旁白）；side=none | 我没有说话，只是倾听着两位女士的探讨 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P208（239） | 正文；seq=8；line/Text `church.1104.s1.p208`；speakerKey留空（旁白）；side=none | 在内心默默做出自己的判断 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P209（241） | 正文；seq=9；line/Text `church.1104.s1.p209`；speaker=alienor；side=left | 埃莉诺：刚才那位老夫人也没钱了，她看上去……和他们一样困难，她可以把家传的茶杯卖掉，这家人为什么不能也卖点东西来抵扣房租呢？ | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P210（242） | 正文；seq=10；line/Text `church.1104.s1.p210`；speaker=adele；side=right | 阿黛尔·米尼耶：不太一样。吕西尼昂夫人家里除了茶杯，还有别的东西，但你看，这家人哪怕被赶出来也只带着一口锅，如果把锅卖了，日后可真是什么都没有啦。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P211（243） | 正文；seq=11；line/Text `church.1104.s1.p211`；speaker=player；side=none | {playername}：吕西尼昂夫人卖的是过去的生活，他们丢的是今天的生活。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P212（244） | 正文；seq=12；line/Text `church.1104.s1.p212`；speakerKey留空（旁白）；side=none | 埃莉诺望了望宅邸，又望向那一家人离开的背影。这次，她沉默了很久。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P213（245） | 正文；seq=13；line/Text `church.1104.s1.p213`；speaker=alienor；side=left | 埃莉诺：那……王庭或者教会能不能给他们安排一个住处？哪怕只是暂时的。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P214（246） | 正文；seq=14；line/Text `church.1104.s1.p214`；speaker=player；side=none | {playername}：能安排一次，就要安排第二次、第三次。谁能拿到房子，谁又要继续住在街上——这不是一句命令能决定的。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P215（247） | 正文；seq=15；line/Text `church.1104.s1.p215`；speaker=alienor；side=left | 埃莉诺：…… | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P216（248） | 正文；seq=16；line/Text `church.1104.s1.p216`；speaker=adele；side=right | 阿黛尔：不过，要是连想办法的人都没有，他们就真的只能一直搬下去啦。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P217（249） | 正文；seq=17；line/Text `church.1104.s1.p217`；speakerKey留空（旁白）；side=none | 埃莉诺轻轻攥住衣角。她没有再说话，只在这道狭窄的巷道里驻足。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P218（250） | 正文；seq=18；line/Text `church.1104.s1.p218`；speakerKey留空（旁白）；side=none | 过了许久她才看向我们，轻声开口道 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P219（251） | 正文；seq=19；line/Text `church.1104.s1.p219`；speaker=alienor；side=left | 埃莉诺：我们……去下一个地方吧。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |

### 远到近：圣徒街｜P220–P224

拟 page/步骤：`church.1104.s1.far.street.intro`；门：route.far。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P220（253） | 演出；—（并入舞台/音效步骤） | [场景转换：圣徒街] | E-R009；P221 前圣徒街 |
| E04-P221（254） | 正文；seq=1；line/Text `church.1104.s1.p221`；speakerKey留空（旁白）；side=none | 或许是因为在其他区域受到了太大的冲击，重新回到圣徒街的埃莉诺显得恹恹的，并没有对周围的繁华景象发表太多的看法 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P222（255） | 正文；seq=2；line/Text `church.1104.s1.p222`；speakerKey留空（旁白）；side=none | 阿黛尔和我对视一眼 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P223（256） | 正文；seq=3；line/Text `church.1104.s1.p223`；speakerKey留空（旁白）；side=none | 她指指埃莉诺的身影，悄悄对我做着口型 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P224（257） | 正文；seq=4；line/Text `church.1104.s1.p224`；speakerKey留空（旁白）；side=none | 我试图去理解她的意思 | 按本页门显示；正文结束后沿 spec 路由 |

### 远到近：智慧成功｜P225–P232

拟 page/步骤：`church.1104.s1.far.street.wisdom.pass`；门：route.far 且 street.wisdom=true。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P225（259） | 控制；—（不建正文行） | （开启智慧检定，智慧≥4触发） | street.wisdom=true→P226；false→P234；只读一次 |
| E04-P226（260） | 正文；seq=1；line/Text `church.1104.s1.p226`；speakerKey留空（旁白）；side=none | 去安慰她？ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P227（261） | 正文；seq=2；line/Text `church.1104.s1.p227`；speakerKey留空（旁白）；side=none | 我指了指自己，一脸茫然地看着阿黛尔 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P228（262） | 正文；seq=3；line/Text `church.1104.s1.p228`；speakerKey留空（旁白）；side=none | 阿黛尔点点头，做出加油的手势，再指指埃莉诺的身影，示意让我赶紧行动 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P229（263） | 正文；seq=4；line/Text `church.1104.s1.p229`；speakerKey留空（旁白）；side=none | 我只能无奈地走上前去 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P230（264） | 正文；seq=5；line/Text `church.1104.s1.p230`；speaker=player；side=none | {playername}：埃莉诺小姐，你今天看见了「外面的世界」的一隅，想必受到了不小的冲击……但也不要过于沮丧，你看，圣徒街依旧如此繁华，圣诺特中还是有不少人过着富足而快乐的生活。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P231（265） | 正文；seq=6；line/Text `church.1104.s1.p231`；speaker=alienor；side=left | 埃莉诺：唉，我…… | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P232（266） | 正文；seq=7；line/Text `church.1104.s1.p232`；speakerKey留空（旁白）；side=none | 她多次试图张口，却都失败了 | 按本页门显示；正文结束后沿 spec 路由 |

### 远到近：智慧失败｜P233–P240

拟 page/步骤：`church.1104.s1.far.street.wisdom.fail`；门：route.far 且同一 street.wisdom=false。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P233（268） | 控制；—（不建正文行） | （开启智慧检定，智慧＜4触发） | 同一 street.wisdom 失败快照，不重新判 |
| E04-P234（269） | 正文；seq=1；line/Text `church.1104.s1.p234`；speakerKey留空（旁白）；side=none | 阿黛尔是什么意思？ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P235（270） | 正文；seq=2；line/Text `church.1104.s1.p235`；speakerKey留空（旁白）；side=none | 去看看她？去问问她？去帮帮她？看不懂啊 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P236（271） | 正文；seq=3；line/Text `church.1104.s1.p236`；speakerKey留空（旁白）；side=none | 我摊出两只手，耸耸肩，对阿黛尔做出无计可施的手势 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P237（272） | 正文；seq=4；line/Text `church.1104.s1.p237`；speakerKey留空（旁白）；side=none | 阿黛尔轻轻哼了一声，然后上前走到埃莉诺身旁 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P238（273） | 正文；seq=5；line/Text `church.1104.s1.p238`；speaker=adele；side=right；表情=思索 | 阿黛尔·米尼耶：（#思索）埃莉诺小姐……我知道你现在心里可能有很多想法……但是你也不要太沮丧，你看，圣徒街是多么繁华呀！我们沸点咖啡馆就坐落在这么繁华的街道上嘿嘿嘿，我每天看见各个阶层的人们来到咖啡馆中享用一杯咖啡并露出满意的笑容就觉得很幸福呢~ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P239（274） | 正文；seq=6；line/Text `church.1104.s1.p239`；speaker=alienor；side=left | 埃莉诺：……是吗？……谢谢你的安慰，我明白你的意思，但是…… | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P240（275） | 正文；seq=7；line/Text `church.1104.s1.p240`；speakerKey留空（旁白）；side=none | 她多次试图张口，却都失败了 | 按本页门显示；正文结束后沿 spec 路由 |

### 远到近：安慰汇合｜P241–P242

拟 page/步骤：`church.1104.s1.far.street.merge`；门：route.far。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P241（277） | 控制；—（不建正文行） | （收束） | 智慧两支汇合 P242 |
| E04-P242（278） | 正文；seq=1；line/Text `church.1104.s1.p242`；speakerKey留空（旁白）；side=none | 不过她好歹终于抬起头看向了圣徒街 | 按本页门显示；正文结束后沿 spec 路由 |

### 远到近：黄昏｜P243–P251

拟 page/步骤：`church.1104.s1.far.street.dusk`；门：route.far。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P243（279） | 演出；—（并入舞台/音效步骤） | [美术：夜晚变亮的的圣徒街] | E-R019；P244 前圣徒街傍晚灯光 |
| E04-P244（280） | 正文；seq=1；line/Text `church.1104.s1.p244`；speakerKey留空（旁白）；side=none | 此时已经接近傍晚，圣徒街上的灯光亮起 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P245（281） | 正文；seq=2；line/Text `church.1104.s1.p245`；speakerKey留空（旁白）；side=none | 街边的叫卖声，人们的讨论声，不绝的脚步声，将我们包裹着 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P246（282） | 正文；seq=3；line/Text `church.1104.s1.p246`；speakerKey留空（旁白）；side=none | 一派欣欣向荣的热闹景象 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P247（283） | 正文；seq=4；line/Text `church.1104.s1.p247`；speakerKey留空（旁白）；side=none | ——这就是圣诺特最繁华的商业街 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P248（284） | 正文；seq=5；line/Text `church.1104.s1.p248`；speakerKey留空（旁白）；side=none | 当我们沉浸于圣诺特的繁华景象时，我听见旁边的埃莉诺无意识地发出喃喃呓语 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P249（285） | 正文；seq=6；line/Text `church.1104.s1.p249`；speaker=alienor；side=left | 埃莉诺：但是这些人是真的幸福……还是忽略他人的苦难而幸福着呢？这种幸福是真的幸福吗？…… | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P250（286） | 正文；seq=7；line/Text `church.1104.s1.p250`；speakerKey留空（旁白）；side=none | 我心下有些惊讶，但是并没有回应 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P251（287） | 正文；seq=8；line/Text `church.1104.s1.p251`；speakerKey留空（旁白）；side=none | 这是属于埃莉诺的问题，也期待着阿莉耶诺尔的回答。 | 按本页门显示；正文结束后沿 spec 路由 |

### 近到远：圣徒街｜P252–P260

拟 page/步骤：`church.1104.s1.near.street`；门：route.near。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P252（289） | 控制；—（不建正文行） | 选项MA70.11.03-S1-2②→ | route.near→P253；P411 后跳 P412 |
| E04-P253（290） | 演出；—（并入舞台/音效步骤） | [场景转换：圣徒街景象] | E-R009；P255 前圣徒街日景 |
| E04-P254（291） | 演出；—（并入舞台/音效步骤） | [音效：人声鼎沸] | E-R011；P255 人声 |
| E04-P255（292） | 正文；seq=1；line/Text `church.1104.s1.p255`；speaker=player；side=none | {playername}：埃莉诺小姐，圣徒街你应该不是很陌生吧？毕竟你也来过很多次咖啡馆了。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P256（293） | 正文；seq=2；line/Text `church.1104.s1.p256`；speaker=alienor；side=left | 埃莉诺：是的，{playernam}店长。 | 正文 {playernam}→{playername}；原稿不改；显示别名埃莉诺；E-C03 |
| E04-P257（294） | 正文；seq=3；line/Text `church.1104.s1.p257`；speaker=adele；side=right | 阿黛尔·米尼耶：圣徒街是圣诺特最繁华的商业街哦~不管你想买什么东西，在这里几乎都可以买到~ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P258（295） | 正文；seq=4；line/Text `church.1104.s1.p258`；speaker=player；side=none | {playername}：是的，这里平时人流量也很大。我想着也是杜瓦尔叔叔选择将咖啡馆建在这里的原因之一吧。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P259（296） | 正文；seq=5；line/Text `church.1104.s1.p259`；speaker=adele；side=right | 阿黛尔·米尼耶：哈哈，有道理呢！ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P260（297） | 正文；seq=6；line/Text `church.1104.s1.p260`；speakerKey留空（旁白）；side=none | 或许是由于对于圣徒街并不陌生，埃莉诺看起来对圣徒街的兴趣不是很大，我们只是简单逛了一下街上的商铺情况，便前往了下一个地点。 | 按本页门显示；正文结束后沿 spec 路由 |

### 近到远：旧城入口｜P261–P267

拟 page/步骤：`church.1104.s1.near.old.intro`；门：route.near。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P261（299） | 演出；—（并入舞台/音效步骤） | [场景转换：旧城区画面] | E-R008；P263 前旧城 |
| E04-P262（300） | 演出；—（并入舞台/音效步骤） | [人声嘈杂声，但不是很大声] | E-R013；P263 较低人声 |
| E04-P263（301） | 正文；seq=1；line/Text `church.1104.s1.p263`；speaker=player；side=none | {playername}：这就是北岸的旧城区。你有什么初步印象吗，埃莉诺小姐？ | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P264（302） | 正文；seq=2；line/Text `church.1104.s1.p264`；speaker=alienor；side=left；表情=思索 | 埃莉诺：（#思索）嗯……我觉得，这里的房子看起来挺好的，但是略微有些破败。以及这里的街道好狭窄，感觉勉强只能够一个人过，与王宫和圣徒街都不一样呢。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P265（303） | 正文；seq=3；line/Text `church.1104.s1.p265`；speaker=adele；side=right | 阿黛尔·米尼耶：是的，小姐。旧城区最初是贵族们居住的地方，所以这里的房子多独栋宅邸，但是后来这些贵族权力式微，渐渐被新发展的权贵取代，他们开始被称为“旧贵族”。后来，有部分旧贵族连这样的生活都难以负担，便将房子分割为无数的小房间，出租给底层人民，以获取一些收入。甚至有些旧贵族抛下房子离开，房子陷入无主的状态，很快就被附近的人们占领，再分割成不同的房间…… | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P266（304） | 正文；seq=4；line/Text `church.1104.s1.p266`；speaker=player；side=none | {playernmae}：由此形成了如今旧城区旧贵族与底层人民，独栋宅邸与群租民房同时出现的景象。我们继续往里走一点吧，埃莉诺小姐。 | 错误 speaker {playernmae} 映射 player；speaker技术纠错 |
| E04-P267（305） | 正文；seq=5；line/Text `church.1104.s1.p267`；speaker=alienor；side=left | 埃莉诺：好、好的。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |

### 近到远：知觉插段｜P268–P271

拟 page/步骤：`church.1104.s1.near.old.perception`；门：route.near 且 old.perception=true。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P268（307） | 控制；—（不建正文行） | （知觉≥3开启） | old.perception=true→P269–270→P272；false→P272 |
| E04-P269（308） | 正文；seq=1；line/Text `church.1104.s1.p269`；speakerKey留空（旁白）；side=none | 从说话的语气看来，埃莉诺受到了些许震撼呢 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P270（309） | 正文；seq=2；line/Text `church.1104.s1.p270`；speakerKey留空（旁白）；side=none | 是哪一部分呢？贵族式微，还是贫民进入贵族的地盘？ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P271（310） | 控制；—（不建正文行） | （知觉＜3无事发生） | 失败不显示提示/空行，直接 P272 |

### 近到远：宅邸介绍｜P272–P272

拟 page/步骤：`church.1104.s1.near.old.house`；门：route.near。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P272（312） | 正文；seq=1；line/Text `church.1104.s1.p272`；speaker=player；side=none | {playername}：这是旧城区最大的宅邸，归属于旧贵族吕西尼昂家族。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |

### 近到远：宅邸远景｜P273–P280

拟 page/步骤：`church.1104.s1.near.old.estate`；门：route.near。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P273（313） | 演出；—（并入舞台/音效步骤） | [美术：狭窄的街道尽头，一座旧式宅邸沉默地立在那里。铁门锈迹斑斑，庭院杂草丛生，褪色的家徽（随便画个圆形或者方形然后上面写Lusignan就行，反正很小也看不清）却仍端端正正地嵌在门楣上。] | E-R014；P274 前宅邸 |
| E04-P274（314） | 正文；seq=1；line/Text `church.1104.s1.p274`；speaker=alienor；side=left | 埃莉诺：我曾经听闻过这个家族的名字，似乎是奥尔狄亚王国早期的一批贵族之一。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P275（315） | 正文；seq=2；line/Text `church.1104.s1.p275`；speaker=adele；side=right | 阿黛尔·米尼耶：是的，听说他们家族鼎盛时期有好几个庄园呢！ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P276（316） | 正文；seq=3；line/Text `church.1104.s1.p276`；speaker=alienor；side=left | 埃莉诺：现在看起来……虽然荒芜，却也不小呢。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P277（317） | 正文；seq=4；line/Text `church.1104.s1.p277`；speaker=adele；side=right | 阿黛尔：外面看起来确实大，但是里面早就空啦。地卖了，马车卖了，听说连好些房间的家具也卖掉了。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P278（318） | 正文；seq=5；line/Text `church.1104.s1.p278`；speaker=alienor；side=left | 埃莉诺：…… | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P279（319） | 正文；seq=6；line/Text `church.1104.s1.p279`；speakerKey留空（旁白）；side=none | 谁也没有说话，我们站在吕西尼昂宅邸门口不远处观赏着过去的时代留下的庞然大物 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P280（320） | 正文；seq=7；line/Text `church.1104.s1.p280`；speakerKey留空（旁白）；side=none | 突然宅邸的大门打开，一位老妇人和一个穿着富贵的男人从宅邸中出来 | 按本页门显示；正文结束后沿 spec 路由 |

### 近到远：传家物｜P281–P291

拟 page/步骤：`church.1104.s1.near.old.heirloom`；门：route.near。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P281（321） | 演出；—（并入舞台/音效步骤） | [美术：在大门模糊画两个人影，一高一矮] | E-R015；P282 前远处二人影 |
| E04-P282（322） | 正文；seq=1；line/Text `church.1104.s1.p282`；speakerKey留空（旁白）；side=none | 他们的声音顺着风隐隐地传进我们的耳朵里 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P283（323） | 正文；seq=2；line/Text `church.1104.s1.p283`；speaker=church.1104.s1.npc.elder_lady；side=none | 老妇人：先生，请你回去之后好好对待这只茶杯，它是个老物件啦，已经在我们家传了很多代了，如果不是现在条件实在不好，我是舍不得卖掉它的…… | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P284（324） | 正文；seq=3；line/Text `church.1104.s1.p284`；speaker=church.1104.s1.npc.buyer；side=none | 男子：我明白的，夫人。您止步，我就先走了。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P285（325） | 正文；seq=4；line/Text `church.1104.s1.p285`；speakerKey留空（旁白）；side=none | 男子道别之后便转身离开了 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P286（326） | 正文；seq=5；line/Text `church.1104.s1.p286`；speakerKey留空（旁白）；side=none | 那位老妇人仍站在宅邸门口目送着男子的离开 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P287（327） | 正文；seq=6；line/Text `church.1104.s1.p287`；speakerKey留空（旁白）；side=none | 她看起来已有六七十岁，衣裙看起来是由贵族才会用的高级布料制成，但是已经发暗，袖口还有一些缝补的痕迹。显然，过去的生活已经消失在时代的洪流中，如今不复往昔。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P288（328） | 正文；seq=7；line/Text `church.1104.s1.p288`；speaker=alienor；side=left | 埃莉诺：已经传了很多代，想必已经是传家宝级别了吧。这样珍贵的东西，也要卖掉吗？ | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P289（329） | 正文；seq=8；line/Text `church.1104.s1.p289`；speakerKey留空（旁白）；side=none | 我和阿黛尔没有立刻回答，许久我才听见阿黛尔淡淡地回答道 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P290（330） | 正文；seq=9；line/Text `church.1104.s1.p290`；speaker=adele；side=right | 阿黛尔·米尼耶：或许，实在是没有办法了吧。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P291（331） | 正文；seq=10；line/Text `church.1104.s1.p291`；speakerKey留空（旁白）；side=none | 我们正准备退出这条街道时，突然旁边的宅邸中传来激烈的争执声。 | 按本页门显示；正文结束后沿 spec 路由 |

### 近到远：驱逐｜P292–P299

拟 page/步骤：`church.1104.s1.near.old.eviction`；门：route.near。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P292（332） | 演出；—（并入舞台/音效步骤） | [音效：激烈的争吵声] | E-R016；P294 前争吵音 |
| E04-P293（333） | 演出；—（并入舞台/音效步骤） | [美术：一个男人，一个抱孩子的女人，一个老人，剪影即可] | E-R017；P294 前一家三人剪影 |
| E04-P294（334） | 正文；seq=1；line/Text `church.1104.s1.p294`；speakerKey留空（旁白）；side=none | 一个男人扛着一口大锅走出来，身后跟着一个抱着孩子的女人和一个步履蹒跚的老人 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P295（335） | 正文；seq=2；line/Text `church.1104.s1.p295`；speakerKey留空（旁白）；side=none | 另一个疑似房东的男人双手抱胸站在门口，轻蔑地看着他们 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P296（336） | 正文；seq=3；line/Text `church.1104.s1.p296`；speaker=church.1104.s1.npc.landlord；side=none | 房东？：我已经给过你们很多机会了，但是你们一拖再拖，就别怪我不客气了。 | 按本页门显示；正文结束后沿 spec 路由；此句显示房东？；E-C03 |
| E04-P297（337） | 正文；seq=4；line/Text `church.1104.s1.p297`；speaker=church.1104.s1.npc.tenant；side=none | 男人：大人，长官，阁下，先生，我求求您了，再宽限两天吧……您看孩子还病着……她还那么小，不能病着跟我们奔波呀…… | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P298（338） | 正文；seq=5；line/Text `church.1104.s1.p298`；speaker=church.1104.s1.npc.landlord；side=none | 房东：我就是看在孩子的份上才给了你们几次机会，哼，谁让你们一直拖着不肯交租？我也不是做慈善的，没有钱交租就得给我滚出去。 | 按本页门显示；正文结束后沿 spec 路由；此句显示房东 |
| E04-P299（339） | 正文；seq=6；line/Text `church.1104.s1.p299`；speakerKey留空（旁白）；side=none | 说完房东便头也不回地离开，将大门狠狠地关上 | 按本页门显示；正文结束后沿 spec 路由 |

### 近到远：居住讨论｜P300–P319

拟 page/步骤：`church.1104.s1.near.old.aftermath`；门：route.near。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P300（340） | 演出；—（并入舞台/音效步骤） | [很重的关门声] | E-R018；P301 前关门声一次 |
| E04-P301（341） | 正文；seq=1；line/Text `church.1104.s1.p301`；speaker=church.1104.s1.npc.tenant；side=none | 男人：阁下……阁下…… | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P302（342） | 正文；seq=2；line/Text `church.1104.s1.p302`；speakerKey留空（旁白）；side=none | 男人又叫唤了两声，没有任何应答 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P303（343） | 正文；seq=3；line/Text `church.1104.s1.p303`；speakerKey留空（旁白）；side=none | 他失望地垂下头，只把锅扛得更稳些，招呼着后面的女人和老人向外走 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P304（344） | 正文；seq=4；line/Text `church.1104.s1.p304`；speaker=alienor；side=left | 埃莉诺：他们这是……要去哪里？他们能找到地方住吗？这个房东也太坏了，孩子生病，为什么不可以多宽限几天呢？ | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P305（345） | 正文；seq=5；line/Text `church.1104.s1.p305`；speaker=adele；side=right | 阿黛尔：……不知道，先找个能过夜的地方吧。旧城区总有人家愿意挤一挤，也可能去郊区寻找流浪儿聚集的地方。而且，房东也不一定就是一个纯粹的坏人。在旧城区的人，多少都是在生活上有困难的人。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P306（346） | 正文；seq=6；line/Text `church.1104.s1.p306`；speaker=player；side=none | {playername}：…… | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P307（347） | 正文；seq=7；line/Text `church.1104.s1.p307`；speakerKey留空（旁白）；side=none | 我没有说话，只是倾听着两位女士的探讨 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P308（348） | 正文；seq=8；line/Text `church.1104.s1.p308`；speakerKey留空（旁白）；side=none | 在内心默默做出自己的判断 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P309（350） | 正文；seq=9；line/Text `church.1104.s1.p309`；speaker=alienor；side=left | 埃莉诺：刚才那位老夫人也没钱了，她看上去……和他们一样困难，她可以把家传的茶杯卖掉，这家人为什么不能也卖点东西来抵扣房租呢？ | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P310（351） | 正文；seq=10；line/Text `church.1104.s1.p310`；speaker=adele；side=right | 阿黛尔·米尼耶：不太一样。吕西尼昂夫人家里除了茶杯，还有别的东西，但你看，这家人哪怕被赶出来也只带着一口锅，如果把锅卖了，日后可真是什么都没有啦。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P311（352） | 正文；seq=11；line/Text `church.1104.s1.p311`；speaker=player；side=none | {playername}：吕西尼昂夫人卖的是过去的生活，他们丢的是今天的生活。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P312（353） | 正文；seq=12；line/Text `church.1104.s1.p312`；speakerKey留空（旁白）；side=none | 埃莉诺望了望宅邸，又望向那一家人离开的背影。这次，她沉默了很久。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P313（354） | 正文；seq=13；line/Text `church.1104.s1.p313`；speaker=alienor；side=left | 埃莉诺：那……王庭或者教会能不能给他们安排一个住处？哪怕只是暂时的。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P314（355） | 正文；seq=14；line/Text `church.1104.s1.p314`；speaker=player；side=none | {playername}：能安排一次，就要安排第二次、第三次。谁能拿到房子，谁又要继续住在街上——这不是一句命令能决定的。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P315（356） | 正文；seq=15；line/Text `church.1104.s1.p315`；speaker=alienor；side=left | 埃莉诺：…… | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P316（357） | 正文；seq=16；line/Text `church.1104.s1.p316`；speaker=adele；side=right | 阿黛尔：不过，要是连想办法的人都没有，他们就真的只能一直搬下去啦。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P317（358） | 正文；seq=17；line/Text `church.1104.s1.p317`；speakerKey留空（旁白）；side=none | 埃莉诺轻轻攥住衣角。她没有再说话，只在这道狭窄的巷道里驻足。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P318（359） | 正文；seq=18；line/Text `church.1104.s1.p318`；speakerKey留空（旁白）；side=none | 过了许久她才看向我们，轻声开口道 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P319（360） | 正文；seq=19；line/Text `church.1104.s1.p319`；speaker=alienor；side=left | 埃莉诺：我们……去下一个地方吧。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |

### 近到远：郊区｜P320–P361

拟 page/步骤：`church.1104.s1.near.suburb`；门：route.near。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P320（362） | 演出；—（并入舞台/音效步骤） | [场景转换：圣西尔万郊区] | E-R007；P322 前郊区 |
| E04-P321（363） | 演出；—（并入舞台/音效步骤） | [音效：人声鼎沸] | E-R012；P322 人声 |
| E04-P322（364） | 正文；seq=1；line/Text `church.1104.s1.p322`；speaker=player；side=none | {playername}：这里就是圣西尔万郊区了。这里聚集了大量工匠、工人和外省移民的穷人，可以说这里是底层人民最集中的片区。我们就顺着向前逛逛吧，埃莉诺小姐，这边人多且杂乱，你一定要跟紧我们。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P323（365） | 正文；seq=2；line/Text `church.1104.s1.p323`；speaker=alienor；side=left | 埃莉诺：好的。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P324（367） | 正文；seq=3；line/Text `church.1104.s1.p324`；speakerKey留空（旁白）；side=none | 或许由于是下午时分，大多数人还在上班，街道上的人并不多 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P325（368） | 正文；seq=4；line/Text `church.1104.s1.p325`；speakerKey留空（旁白）；side=none | 但也聚集了一些无业和失业的流民，男人们穿着洗得发白、肘部磨破的上衣的上衣坐在路边，有人把烟卷夹在指间，有人叼在嘴角，时不时深吸一口，但都舍不得抽得太快。街道上飘散着一股烟草气混着煤灰、渣土和酒精的味道 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P326（369） | 正文；seq=5；line/Text `church.1104.s1.p326`；speakerKey留空（旁白）；side=none | 偶尔有招工的人从街口经过，他们便抬头望一眼，若那人没有停下，又很快低下头去，猛吸一口香烟。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P327（370） | 正文；seq=6；line/Text `church.1104.s1.p327`；speakerKey留空（旁白）；side=none | 女人们则少聚集在街边，多在民居门口。有人带着孩子，有人在门口纺织，还有一些女人穿着暴露，在房屋门口招揽着来往的男人。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P328（372） | 正文；seq=7；line/Text `church.1104.s1.p328`；speaker=player；side=none | {playername}：埃莉诺小姐，你会怎么评价这些人呢？ | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P329（373） | 正文；seq=8；line/Text `church.1104.s1.p329`；speaker=alienor；side=left | 埃莉诺：……我不知道。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P330（374） | 正文；seq=9；line/Text `church.1104.s1.p330`；speaker=player；side=none | {playername}：好吃懒做？不务正事？颓废阴暗？ | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P331（375） | 正文；seq=10；line/Text `church.1104.s1.p331`；speaker=alienor；side=left | 埃莉诺：……抱歉，但我想不完全是。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P332（376） | 正文；seq=11；line/Text `church.1104.s1.p332`；speakerKey留空（旁白）；side=none | 我没有再询问，我们继续往前走着 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P333（378） | 正文；seq=12；line/Text `church.1104.s1.p333`；speakerKey留空（旁白）；side=none | 突然我们远远看见一处棚子，上面有王室和教会的标志 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P334（379） | 正文；seq=13；line/Text `church.1104.s1.p334`；speaker=adele；side=right | 阿黛尔·米尼耶：那是什么？ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P335（380） | 正文；seq=14；line/Text `church.1104.s1.p335`；speaker=player；side=none | {playername}：不知道，你们在此等候，我上前去看看吧。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P336（382） | 正文；seq=15；line/Text `church.1104.s1.p336`；speakerKey留空（旁白）；side=none | 棚子下并没有看见有王庭或者教会打扮的人，只有几个平民坐在棚下休息。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P337（383） | 正文；seq=16；line/Text `church.1104.s1.p337`；speaker=player；side=none | {playername}：你好，请问这个棚子是用来干什么的？我看见上面有王庭和教会的标识。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P338（384） | 正文；seq=17；line/Text `church.1104.s1.p338`；speaker=church.1104.s1.npc.civilian_a；side=none | 平民A：嗨呀，这个棚子就是之前埃利昂公爵带人来发放粥和面包的时候搭建的，说之后会经常来施粥呢！ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P339（385） | 正文；seq=18；line/Text `church.1104.s1.p339`；speaker=church.1104.s1.npc.civilian_b；side=none | 平民B：哼，每次只拿那么一点东西来，我看就是作秀罢了。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P340（386） | 正文；seq=19；line/Text `church.1104.s1.p340`；speaker=church.1104.s1.npc.civilian_a；side=none | 平民A：你怎么这么说呢？现在国家经济也不好，埃利昂公爵愿意带人来我们这种地方施粥已经很好了。上次发现斗殴事件之后，公爵大人不也说要整治吗？ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P341（387） | 正文；seq=20；line/Text `church.1104.s1.p341`；speaker=player；side=none | {playername}：……斗殴事件？ | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P342（388） | 正文；seq=21；line/Text `church.1104.s1.p342`；speaker=church.1104.s1.npc.civilian_a；side=none | 平民A：是的，哎呀，就是上次埃利昂公爵来施粥，但是数量有限，有的人为了争夺就打起来啦。我看也不是埃利昂公爵的错。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P343（389） | 正文；seq=22；line/Text `church.1104.s1.p343`；speaker=church.1104.s1.npc.civilian_b；side=none | 平民B：怎么不是他的错了？有本事多带点食物来，他又不是不知道我们国家这么多人都吃不饱，要我说就是他们王庭和教会的错！ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P344（390） | 正文；seq=23；line/Text `church.1104.s1.p344`；speaker=church.1104.s1.npc.civilian_a；side=none | 平民A：哎哟，大哥，你可小心着说话吧！ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P345（391） | 正文；seq=24；line/Text `church.1104.s1.p345`；speaker=church.1104.s1.npc.civilian_b；side=none | 平民B：怎么？有本事立马派人把我抓进去，我还怕他们不成？ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P346（393） | 正文；seq=25；line/Text `church.1104.s1.p346`；speakerKey留空（旁白）；side=none | 眼见两位大哥的话题逐渐转向其他地方，我赶紧撤离，把刚才打听到的消息转告给她们。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P347（394） | 正文；seq=26；line/Text `church.1104.s1.p347`；speaker=alienor；side=left | 埃莉诺：埃利昂叔叔确实是一个很好的人呢……我之前也有听闻过他在民间的名声很望。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P348（395） | 正文；seq=27；line/Text `church.1104.s1.p348`；speaker=adele；side=right | 阿黛尔·米尼耶：我好像也有听说过前段时间埃利昂公爵在北岸施粥的事呢！ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P349（396） | 正文；seq=28；line/Text `church.1104.s1.p349`；speaker=player；side=none | {playername}：是的。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P350（397） | 正文；seq=29；line/Text `church.1104.s1.p350`；speakerKey留空（旁白）；side=none | 我突然回想起，前几日似乎有一个跛脚的工人也在我面前提过埃利昂公爵施粥的事 | D/1014 前情；E-B04 |
| E04-P351（398） | 正文；seq=30；line/Text `church.1104.s1.p351`；speakerKey留空（旁白）；side=none | 不过他没有提及背后还有斗殴事件，还真是复杂啊 | 同 E-B04 |
| E04-P352（400） | 正文；seq=31；line/Text `church.1104.s1.p352`；speakerKey留空（旁白）；side=none | 我们没有就这个话题深入聊下来，继续向前走着 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P353（402） | 正文；seq=32；line/Text `church.1104.s1.p353`；speakerKey留空（旁白）；side=none | 混乱、无序、颓废 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P354（403） | 正文；seq=33；line/Text `church.1104.s1.p354`；speakerKey留空（旁白）；side=none | 初次来到这里的人一定会有这样的印象 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P355（404） | 正文；seq=34；line/Text `church.1104.s1.p355`；speakerKey留空（旁白）；side=none | 但我们也在这里看见有人积极地生活着，邻里互相帮助着，人们团结着，相互依靠着 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P356（405） | 正文；seq=35；line/Text `church.1104.s1.p356`；speakerKey留空（旁白）；side=none | 在生活的压迫下寻找着一条可以前行的道路 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P357（407） | 正文；seq=36；line/Text `church.1104.s1.p357`；speakerKey留空（旁白）；side=none | 埃莉诺偶尔询问着我们关于人们生活的问题，我们一一回答 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P358（408） | 正文；seq=37；line/Text `church.1104.s1.p358`；speakerKey留空（旁白）；side=none | 她偶尔也会长久地凝望着那些在路边抽烟的颓废的男人和来往的牵着孩子的行色匆匆的妇人 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P359（409） | 正文；seq=38；line/Text `church.1104.s1.p359`；speakerKey留空（旁白）；side=none | 她偶尔也会为路边穿着破烂但是聚集在一起玩得很开心的孩子们，在阳光下悠闲散步的小猫而停留 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P360（411） | 正文；seq=39；line/Text `church.1104.s1.p360`；speakerKey留空（旁白）；side=none | 我们沿着圣西尔万郊区一直走到大堂区附近 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P361（412） | 正文；seq=40；line/Text `church.1104.s1.p361`；speaker=alienor；side=left | 埃莉诺：谢谢你们。虽然有些突然，但如果不是你们，我或许不能够如此顺利地了解外面的世界，接下来，让我们去大堂区看看吧。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |

### 近到远：市场｜P362–P402

拟 page/步骤：`church.1104.s1.near.market`；门：route.near。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P362（414） | 演出；—（并入舞台/音效步骤） | [场景转换：大堂区的露天大市场画面] | E-R006；P364 前市场 |
| E04-P363（415） | 演出；—（并入舞台/音效步骤） | [音效：市场里人声嘈杂] | E-R011；P364 人声 |
| E04-P364（417） | 正文；seq=1；line/Text `church.1104.s1.p364`；speakerKey留空（旁白）；side=none | 大堂区的露天市场比别处热闹得多，哪怕时间已经到了下午，依旧有不少人聚集在这里 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P365（418） | 正文；seq=2；line/Text `church.1104.s1.p365`；speakerKey留空（旁白）；side=none | 蔬菜、旧衣、二手家具和廉价香料堆放在摊位上，车夫的吆喝、摊贩的叫卖和顾客的讨价还价声交杂在一起 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P366（420） | 正文；seq=3；line/Text `church.1104.s1.p366`；speaker=adele；side=right | 阿黛尔·米尼耶：大堂区主要是普通百姓居住的地方，但这边有一个很大的市场，所以超级热闹！不过要早点来，晚了只剩蔫的。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P367（421） | 正文；seq=4；line/Text `church.1104.s1.p367`；speakerKey留空（旁白）；side=none | 阿黛尔已经熟练地凑到附近的摊位上看起来了 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P368（422） | 正文；seq=5；line/Text `church.1104.s1.p368`；speakerKey留空（旁白）；side=none | 埃莉诺缓步则走向一处旧衣摊位前查看起来 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P369（423） | 正文；seq=6；line/Text `church.1104.s1.p369`；speakerKey留空（旁白）；side=none | 我凑上前去看了一眼，摊上的衣服没什么特别的，就是普通二手旧衣 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P370（424） | 正文；seq=7；line/Text `church.1104.s1.p370`；speakerKey留空（旁白）；side=none | 随处可见的样式，随处可见的材质 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P371（425） | 正文；seq=8；line/Text `church.1104.s1.p371`；speakerKey留空（旁白）；side=none | 不过这种“随处可见”对王女来说可能也是少见的呢…… | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P372（427） | 正文；seq=9；line/Text `church.1104.s1.p372`；speakerKey留空（旁白）；side=none | 我又仔细看了看才发现埃莉诺的目光并非停留在摊位的衣服上 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P373（428） | 正文；seq=10；line/Text `church.1104.s1.p373`；speakerKey留空（旁白）；side=none | 她关注着摊位旁一对母女 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P374（429） | 正文；seq=11；line/Text `church.1104.s1.p374`；speakerKey留空（旁白）；side=none | 那对母女穿着相当朴素，女儿身上衣服还算得整洁，母亲身上的衣服却满是补丁的痕迹，颜色灰扑扑的，一看就是穿了很多年的衣服了 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P375（430） | 正文；seq=12；line/Text `church.1104.s1.p375`；speakerKey留空（旁白）；side=none | 看起来家庭普通却对女儿很好呢 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P376（431） | 正文；seq=13；line/Text `church.1104.s1.p376`；speakerKey留空（旁白）；side=none | 她们在摊位旁看了很久，最终仿佛终于鼓起勇气一般向摊主发问 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P377（432） | 正文；seq=14；line/Text `church.1104.s1.p377`；speaker=church.1104.s1.npc.mother；side=none | 女人：请问那件外套多少钱？ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P378（433） | 正文；seq=15；line/Text `church.1104.s1.p378`；speakerKey留空（旁白）；side=none | 我们顺着她的手指看过去，那是一件深灰色旧羊毛外套。样式倒是好看，只是袖口磨亮，衣襟有明显缝补痕迹，肩线歪了，扣子大小并不相同，内衬也用不同颜色的布重新缝过。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P379（434） | 正文；seq=16；line/Text `church.1104.s1.p379`；speaker=church.1104.s1.npc.vendor；side=none | 摊主：嘿，夫人，你可真有眼光。这是我最近刚拿到手的一件好货，最初是旧城区的旧贵族们穿的，后来几经转手到了我手上啦！你可别看它外表不太好看，但是里面羊毛含量非常高，穿起来特别保暖，保你一整个冬天都不会感到寒冷！不过也因此嘛，它的售价不会太便宜，xx国王币。 | 按本页门显示；正文结束后沿 spec 路由；E-B02：C外套报价未定 |
| E04-P380（435） | 正文；seq=17；line/Text `church.1104.s1.p380`；speaker=church.1104.s1.npc.mother；side=none | 女人：什么？！我也在你的摊位上买过很多衣服了，第一次遇见这个价格的！ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P381（436） | 正文；seq=18；line/Text `church.1104.s1.p381`；speaker=church.1104.s1.npc.vendor；side=none | 摊主：夫人，你摸摸看呀，这个衣服和别的不一样的！ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P382（438） | 正文；seq=19；line/Text `church.1104.s1.p382`；speakerKey留空（旁白）；side=none | 埃莉诺转头悄悄地和我们讨论起这件羊毛外套 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P383（439） | 正文；seq=20；line/Text `church.1104.s1.p383`；speaker=alienor；side=left | 埃莉诺：虽然这件衣服已然被改得不如从前，但我看一眼就知道这确实是上好的羊毛制成的，原料费、人工费算上的话，xx国王币对于这件衣服而言甚至算得上便宜啦。【目前不太清楚这个国家的消费水平来着】 | 作者消费水平备注不显示；金额 E-B02；显示别名埃莉诺；E-C03；E-B02：C外套成本/售价讨论未定 |
| E04-P384（440） | 正文；seq=21；line/Text `church.1104.s1.p384`；speaker=adele；side=right | 阿黛尔·米尼耶：天呢，埃莉诺小姐！那可是xx国王币！这只是一件外套而已。 | 按本页门显示；正文结束后沿 spec 路由；E-B02：C外套报价未定 |
| E04-P385（441） | 正文；seq=22；line/Text `church.1104.s1.p385`；speaker=alienor；side=left | 埃莉诺：……可那确实是由上等的羊毛制成的，摊主没有说错，冬天穿上就再也不会感到寒冷，而且可以一直传下去呢。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P386（442） | 正文；seq=23；line/Text `church.1104.s1.p386`；speaker=adele；side=right | 阿黛尔·米尼耶：小姐，你知道xx国王币够一个普通家庭活多少天了吗？ | 按本页门显示；正文结束后沿 spec 路由；E-B02：C所对应生活天数比较；不新增天数常量 |
| E04-P387（443） | 正文；seq=24；line/Text `church.1104.s1.p387`；speakerKey留空（旁白）；side=none | 埃莉诺沉默下来了 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P388（445） | 正文；seq=25；line/Text `church.1104.s1.p388`；speakerKey留空（旁白）；side=none | 摊位前的争论也接近尾声 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P389（446） | 正文；seq=26；line/Text `church.1104.s1.p389`；speakerKey留空（旁白）；side=none | 那个母亲带着满脸喜爱和惋惜最后摸了摸外套，然后带着孩子离开了 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P390（447） | 正文；seq=27；line/Text `church.1104.s1.p390`；speakerKey留空（旁白）；side=none | 埃莉诺望着她们远去的背影没有说话 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P391（449） | 正文；seq=28；line/Text `church.1104.s1.p391`；speaker=adele；side=right | 阿黛尔·米尼耶：埃莉诺小姐，您请跟我来。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P392（450） | 正文；seq=29；line/Text `church.1104.s1.p392`；speakerKey留空（旁白）；side=none | 阿黛尔带着埃莉诺来到一个贩卖蔬菜水果的摊位前 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P393（451） | 正文；seq=30；line/Text `church.1104.s1.p393`；speaker=adele；side=right | 阿黛尔·米尼耶：小姐，你觉得xx国王币在这个摊位可以买到多少东西呢？ | 按本页门显示；正文结束后沿 spec 路由；E-B02：F蔬果购买力；是否等于C待确认 |
| E04-P394（452） | 正文；seq=31；line/Text `church.1104.s1.p394`；speaker=alienor；side=left | 埃莉诺：……几根胡萝卜加几个洋葱？ | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P395（453） | 正文；seq=32；line/Text `church.1104.s1.p395`；speaker=adele；side=right | 阿黛尔·米尼耶：还能再买好几个土豆和苹果呢，这已经足够一个三口家庭吃好几天啦。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P396（454） | 正文；seq=33；line/Text `church.1104.s1.p396`；speaker=alienor；side=left | 埃莉诺：我在……的时候学习过税制、贸易和经济体制，我的老师曾经让我接触过一些账单，我以为我了解这些，但显然我错了。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P397（455） | 正文；seq=34；line/Text `church.1104.s1.p397`；speaker=adele；side=right | 阿黛尔·米尼耶：那也很厉害啦。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P398（456） | 正文；seq=35；line/Text `church.1104.s1.p398`；speaker=alienor；side=left | 埃莉诺：……但是，我学的知识里不会告诉我体制下xx国王币够一家三口生活几天。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03；E-B02：家庭生活费用引用；与C/F关系待确认 |
| E04-P399（458） | 正文；seq=36；line/Text `church.1104.s1.p399`；speakerKey留空（旁白）；side=none | 埃莉诺没有再说话，她只是默默地沿着道路向前走着 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P400（459） | 正文；seq=37；line/Text `church.1104.s1.p400`；speakerKey留空（旁白）；side=none | 她时不时停留在摊位旁，询问摊主价格，然后认真地记录下来 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P401（460） | 正文；seq=38；line/Text `church.1104.s1.p401`；speakerKey留空（旁白）；side=none | 我和阿黛尔只是无言地跟在她身后 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P402（461） | 正文；seq=39；line/Text `church.1104.s1.p402`；speakerKey留空（旁白）；side=none | 直到埃莉诺大致浏览完附近的摊位，回过头告诉我们可以回去了 | 按本页门显示；正文结束后沿 spec 路由 |

### 近到远：返回圣徒街｜P403–P411

拟 page/步骤：`church.1104.s1.near.street.dusk`；门：route.near。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P403（463） | 演出；—（并入舞台/音效步骤） | [美术：夜晚变亮的的圣徒街] | E-R019；P404 前傍晚灯光 |
| E04-P404（464） | 正文；seq=1；line/Text `church.1104.s1.p404`；speakerKey留空（旁白）；side=none | 再次回到圣徒街，已经接近傍晚，圣徒街上的灯光亮起 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P405（465） | 正文；seq=2；line/Text `church.1104.s1.p405`；speakerKey留空（旁白）；side=none | 街边的叫卖声，人们的讨论声，不绝的脚步声，将我们包裹着 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P406（466） | 正文；seq=3；line/Text `church.1104.s1.p406`；speakerKey留空（旁白）；side=none | 一派欣欣向荣的热闹景象 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P407（467） | 正文；seq=4；line/Text `church.1104.s1.p407`；speakerKey留空（旁白）；side=none | ——这就是圣诺特最繁华的商业街 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P408（468） | 正文；seq=5；line/Text `church.1104.s1.p408`；speakerKey留空（旁白）；side=none | 当我们沉浸于圣诺特的繁华景象时，我听见旁边的埃莉诺无意识地发出喃喃呓语 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P409（469） | 正文；seq=6；line/Text `church.1104.s1.p409`；speaker=alienor；side=left | 埃莉诺：但是这些人是真的幸福……还是忽略他人的苦难而幸福着呢？这种幸福是真的幸福吗？…… | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P410（470） | 正文；seq=7；line/Text `church.1104.s1.p410`；speakerKey留空（旁白）；side=none | 我心下有些惊讶，但是并没有回应 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P411（471） | 正文；seq=8；line/Text `church.1104.s1.p411`；speakerKey留空（旁白）；side=none | 这是属于埃莉诺的问题，也期待着阿莉耶诺尔的回答。 | 按本页门显示；正文结束后沿 spec 路由 |

### 路线汇合与夜间入口｜P412–P412

拟 page/步骤：`church.1104.s1.boundary.night`；门：互斥路线结束；等待 BeforeNight；E-C01。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P412（473） | 控制；—（不建正文行） | （收束） | 游览汇合；等同根 BeforeNight 续 P413；不结算 |

### 夜间回咖啡馆｜P413–P424

拟 page/步骤：`church.1104.s1.night.return`；门：同根 BeforeNight 续接。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P413（474） | 正文；seq=1；line/Text `church.1104.s1.p413`；speakerKey留空（旁白）；side=none | 夜色越来越暗，我们顺着圣徒街回到咖啡馆中 | 正文返回咖啡馆；工程默认夜背景 E-R020 |
| E04-P414（475） | 正文；seq=2；line/Text `church.1104.s1.p414`；speaker=adele；side=right | 阿黛尔·米尼耶：今天真是充实的一天呀，我现在都感觉到累了！ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P415（476） | 正文；seq=3；line/Text `church.1104.s1.p415`；speaker=player；side=none | {playername}：大家今晚早点收拾睡觉吧。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P416（477） | 正文；seq=4；line/Text `church.1104.s1.p416`；speaker=adele；side=right；表情=开心 | 阿黛尔·米尼耶：（#开心）当然呀，明天又是忙碌的一天呢！今天歇业了半天，不知道明天上午顾客会不会暴涨呢嘿嘿嘿。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P417（478） | 正文；seq=5；line/Text `church.1104.s1.p417`；speakerKey留空（旁白）；side=none | 无论发生了什么，阿黛尔永远这么有活力呢 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P418（479） | 正文；seq=6；line/Text `church.1104.s1.p418`；speakerKey留空（旁白）；side=none | 我转向一旁沉默的埃莉诺 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P419（480） | 正文；seq=7；line/Text `church.1104.s1.p419`；speaker=player；side=none | {playername}：埃莉诺小姐，早点歇息吧。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P420（481） | 正文；seq=8；line/Text `church.1104.s1.p420`；speaker=alienor；side=left；表情=空→保持 | 埃莉诺：（#）……{playername}店长，我有些事情想要和你商讨一下。 | 空表情（#）剥离并保持当前表情；显示别名埃莉诺；E-C03 |
| E04-P421（482） | 正文；seq=9；line/Text `church.1104.s1.p421`；speaker=player；side=none | {playername}：啊，好的。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P422（483） | 正文；seq=10；line/Text `church.1104.s1.p422`；speaker=adele；side=right | 阿黛尔·米尼耶：那你们先聊吧，我收拾收拾去睡觉啦！晚安埃莉诺小姐，晚安{playername}店长！ | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P423（484） | 正文；seq=11；line/Text `church.1104.s1.p423`；speaker=player；side=none | {playername}：晚安，阿黛尔。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P424（485） | 正文；seq=12；line/Text `church.1104.s1.p424`；speaker=alienor；side=left | 埃莉诺：晚安，阿黛尔小姐。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |

### 离场与回应选择｜P425–P431

拟 page/步骤：`church.1104.s1.night.private`；门：公共；末尾 night 三选一。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P425（487） | 演出；—（并入舞台/音效步骤） | [阿黛尔立绘消失，只留下埃莉诺] | E-R021；P426 前仅 adele 退场 |
| E04-P426（488） | 正文；seq=1；line/Text `church.1104.s1.p426`；speakerKey留空（旁白）；side=none | 阿黛尔离开之后，我和埃莉诺在咖啡馆中随机找了个座位坐下 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P427（489） | 正文；seq=2；line/Text `church.1104.s1.p427`；speakerKey留空（旁白）；side=none | 埃莉诺一时没有说话，似乎在组织言语 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P428（491） | 控制；—（不建正文行） | 选择MA70.11.03-S1-3 | P427 后呈现 night 三选一；均到 P432 |
| E04-P429（492） | 选项；Choice `church.1104.s1.night.wait`；挂 `night.private`；Text `church.1104.s1.p429` | ①……（耐心等待） | 保留“……（耐心等待）”选项语义；P432；无新判定/奖励 |
| E04-P430（493） | 选项；Choice `church.1104.s1.night.ask`；挂 `night.private`；Text `church.1104.s1.p430` | ②埃莉诺小姐，请问您找我要商讨什么事呢？ | P432；无新判定/奖励 |
| E04-P431（494） | 选项；Choice `church.1104.s1.night.hurry`；挂 `night.private`；Text `church.1104.s1.p431` | ③埃莉诺小姐，有话快讲。 | P432；无新判定/奖励 |

### 王女的疑问｜P432–P439

拟 page/步骤：`church.1104.s1.night.question`；门：夜间三选项共同后续。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P432（496） | 正文；seq=1；line/Text `church.1104.s1.p432`；speaker=alienor；side=left | 埃莉诺：……{playername}店长，我非常感谢今天你和阿黛尔愿意花时间带我感受外面真实的世界……此时我才发现，我之前对外面的了解可谓是一无所知……我原本以为，只要亲眼看见他们的生活，就能明白他们需要什么，下次上课的时候我就可以回应西瓦尔老师关于“外面的世界”的问题，但是，不是的…… | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P433（497） | 正文；seq=2；line/Text `church.1104.s1.p433`；speakerKey留空（旁白）；side=none | 她沉默了一会儿继续说道 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P434（498） | 正文；seq=3；line/Text `church.1104.s1.p434`；speaker=alienor；side=left | 埃莉诺：我今天去了好几个区域，见到了不同的人……我只是看见了更多自己不知道的事情。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P435（499） | 正文；seq=4；line/Text `church.1104.s1.p435`；speaker=player；side=none | {playername}：这是一个过程。至少你现在走进了一个真实的世界。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P436（500） | 正文；seq=5；line/Text `church.1104.s1.p436`；speaker=alienor；side=left | 埃莉诺：可如果只是看见，却什么也改变不了，那又有什么意义？ | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P437（501） | 正文；seq=6；line/Text `church.1104.s1.p437`；speaker=player；side=none | {playername}：……你想改变吗？目前，至少你不能再假装他们过得很好。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P438（502） | 正文；seq=7；line/Text `church.1104.s1.p438`；speakerKey留空（旁白）；side=none | 埃莉诺低头看着自己的手 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P439（503） | 正文；seq=8；line/Text `church.1104.s1.p439`；speaker=alienor；side=left | 埃莉诺：我不知道……我可以吗？我今天才发现，对王庭来说只是一个决定，一项政策，一句话，对民众来说，却可能是明天的面包、煤炭，或者能不能继续住在房子里……总之是和生活息息相关的。我幼时被当做继承人培养，我学过治理国家的专业知识，但是在广大的现实中，它们只是知识罢了，我还是不知道……知道这些以后，我究竟能做什么。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |

### 未倾向革命联合｜P440–P452

拟 page/步骤：`church.1104.s1.night.nonrevolution`；门：同一真实阵营分类快照=false；E-B03。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P440（504） | 控制；—（不建正文行） | （如果主角此时未倾向革命联合，则触发） | 取此时阵营分类快照；false→P441；未知不能当 false |
| E04-P441（505） | 正文；seq=1；line/Text `church.1104.s1.p441`；speaker=player；side=none | {playername}：那就先别急着一下子解决所有事情。至少下次再看见他们的时候，别把他们当成一个决定，一项政策或是一句话。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P442（506） | 正文；seq=2；line/Text `church.1104.s1.p442`；speakerKey留空（旁白）；side=none | 埃莉诺轻轻点头，却没有露出轻松的表情。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P443（507） | 正文；seq=3；line/Text `church.1104.s1.p443`；speakerKey留空（旁白）；side=none | 我想她还需要一段时间来消化今天所接触到的 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P444（508） | 正文；seq=4；line/Text `church.1104.s1.p444`；speaker=player；side=none | {playername}：埃莉诺小姐，今天时间不早了。您早点休息，这些东西……不是一时就能消化的。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P445（509） | 正文；seq=5；line/Text `church.1104.s1.p445`；speaker=alienor；side=left | 埃莉诺：好的，谢谢…… | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P446（511） | 正文；seq=6；line/Text `church.1104.s1.p446`；speakerKey留空（旁白）；side=none | 我朝她点点头，然后率先离开 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P447（512） | 正文；seq=7；line/Text `church.1104.s1.p447`；speakerKey留空（旁白）；side=none | 走远几步后我回头看见她仍坐在原位 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P448（513） | 正文；seq=8；line/Text `church.1104.s1.p448`；speakerKey留空（旁白）；side=none | 她思索的身影在昏暗的灯光下若隐若现 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P449（514） | 正文；seq=9；line/Text `church.1104.s1.p449`；speakerKey留空（旁白）；side=none | 你会如何做呢，阿莉耶诺尔·康多尔。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P450（516） | 正文；seq=10；line/Text `church.1104.s1.p450`；speakerKey留空（旁白）；side=none | 由于忙碌了一天，回到房间后，我很快就陷入了沉睡 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P451（517） | 正文；seq=11；line/Text `church.1104.s1.p451`；speakerKey留空（旁白）；side=none | 明天不知道会迎接什么呢…… | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P452（518） | 控制；—（不建正文行） | （黑幕） | P451 后黑幕→唯一 FINAL；不可继续 P453；E-R022；黑幕完成才最终提交 |

### 已倾向革命联合｜P453–P473

拟 page/步骤：`church.1104.s1.night.revolution`；门：同一真实阵营分类快照=true；E-B03。

| 来源（XML 段） | 类型 / seq / 配置定位 | 原文（完整） | 处理与路由 / 演出锚点 |
|---|---|---|---|
| E04-P453（520） | 控制；—（不建正文行） | （如果主角此时已倾向革命联合，则触发） | 沿同一阵营分类 true→P454；不是 P452 的后续 |
| E04-P454（521） | 正文；seq=1；line/Text `church.1104.s1.p454`；speaker=player；side=none | {playername}：所以你还觉得，他们需要王庭和教会吗？ | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P455（522） | 正文；seq=2；line/Text `church.1104.s1.p455`；speaker=alienor；side=left | 埃莉诺：什么？！这，这是当然的吧，不，不，我不知道……天呐……我没有想过这个问题。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P456（523） | 正文；seq=3；line/Text `church.1104.s1.p456`；speaker=player；side=none | {playername}：王庭和教会让他们贫穷，再偶尔施舍一点东西，然后要求他们感恩。这样的统治有什么必要？ | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P457（524） | 正文；seq=4；line/Text `church.1104.s1.p457`；speaker=alienor；side=left | 埃莉诺：可我知道王庭和教会还是会做一些事的，如果没有王庭，谁来修路、储粮、维持秩序？如果只是把一切推倒，受苦的还是他们。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P458（525） | 正文；seq=5；line/Text `church.1104.s1.p458`；speaker=player；side=none | {playername}：至少他们可以自己决定该怎么生活，而不是等着某个上层的人替他们决定。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P459（526） | 正文；seq=6；line/Text `church.1104.s1.p459`；speaker=alienor；side=left | 埃莉诺：你说得好像只要让他们自己选择，他们就一定能得到更好的生活。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P460（527） | 正文；seq=7；line/Text `church.1104.s1.p460`；speaker=player；side=none | {playername}：至少那会是他们自己的选择。 | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P461（528） | 正文；seq=8；line/Text `church.1104.s1.p461`；speakerKey留空（旁白）；side=none | 埃莉诺沉默片刻，声音低了下来。 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P462（529） | 正文；seq=9；line/Text `church.1104.s1.p462`；speaker=alienor；side=left | 埃莉诺：也许你说得对。可我今天第一次真正看见他们之后，反而更害怕有人把他们带去承受另一场混乱。 | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P463（530） | 正文；seq=10；line/Text `church.1104.s1.p463`；speaker=player；side=none | {playername}：害怕他们受苦，还是害怕他们不再需要你们？ | 按本页门显示；正文结束后沿 spec 路由；动态姓名 |
| E04-P464（531） | 正文；seq=11；line/Text `church.1104.s1.p464`；speakerKey留空（旁白）；side=none | 埃莉诺怔住 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P465（532） | 正文；seq=12；line/Text `church.1104.s1.p465`；speakerKey留空（旁白）；side=none | 片刻后，她避开了我的目光 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P466（533） | 正文；seq=13；line/Text `church.1104.s1.p466`；speaker=alienor；side=left | 埃莉诺：……我不知道。或许两者都有。我从来没有想过这个问题，我，我想我会想一想的…… | 按本页门显示；正文结束后沿 spec 路由；显示别名埃莉诺；E-C03 |
| E04-P467（535） | 正文；seq=14；line/Text `church.1104.s1.p467`；speakerKey留空（旁白）；side=none | 她不再看向我，而是陷入了更深的沉默 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P468（536） | 正文；seq=15；line/Text `church.1104.s1.p468`；speakerKey留空（旁白）；side=none | 我看了看她，然后率先离开 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P469（537） | 正文；seq=16；line/Text `church.1104.s1.p469`；speakerKey留空（旁白）；side=none | 走远几步后我回头看见她仍坐在原位 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P470（538） | 正文；seq=17；line/Text `church.1104.s1.p470`；speakerKey留空（旁白）；side=none | 她思索的身影在昏暗的灯光下若隐若现 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P471（540） | 正文；seq=18；line/Text `church.1104.s1.p471`；speakerKey留空（旁白）；side=none | 由于忙碌了一天，回到房间后，我很快就陷入了沉睡 | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P472（541） | 正文；seq=19；line/Text `church.1104.s1.p472`；speakerKey留空（旁白）；side=none | 明天不知道会迎接什么呢…… | 按本页门显示；正文结束后沿 spec 路由 |
| E04-P473（542） | 控制；—（不建正文行） | （黑幕） | P472 后黑幕→同一 FINAL；E-R022；黑幕完成才最终提交 |

## E05A：【施工中】MA70.11.05-S1.docx

源：`D:\桌面\文案\王庭教会\【施工中】MA70.11.05-S1.docx`；SHA-256 `e4b24b8a56befdce94e316a91f5bc04dcc109bcf7fa285fd6d44741025f1cb29`。1 非空标题，0 正文。逻辑根 `church.1105.s1` 仅登记，禁止创建空 Event/Date/Result/Text 或完成事实。两份文件的 S1/S2 不擅自对应庇护两支或日夜。

| 来源（XML 段） | 类型 | 完整原文 | 处理 |
|---|---|---|---|
| E05A-P001（1） | 标题 | MA70.11.05-S1 | 未完成源稿，不施工；E-B05 |

## E05B：【施工中】MA70.11.05-S2.docx

源：`D:\桌面\文案\王庭教会\【施工中】MA70.11.05-S2.docx`；SHA-256 `5f6025cac385e3cad5a9c97c30871efbb09f8d3d74a10f7fbfc0f9e31e27032b`。1 非空标题，0 正文。逻辑根 `church.1105.s2` 仅登记，禁止创建空 Event/Date/Result/Text 或完成事实。两份文件的 S1/S2 不擅自对应庇护两支或日夜。

| 来源（XML 段） | 类型 | 完整原文 | 处理 |
|---|---|---|---|
| E05B-P001（1） | 标题 | MA70.11.05-S2 | 未完成源稿，不施工；E-B05 |

## 静态覆盖记录

- 11 月 4 日：标题1、控制26、演出33、正文405、选项8；总计 473。正文及 8 条选择文本有唯一拟定 key。
- 加上两份 11 月 5 日标题，共 475 个非空源段。无实际 Excel 行号，因为本轮未配表。
- 这是文档静态核对，不代表生成、编译、运行或 UI 验收通过。

文档静态审计（2026-10-16）：重新只读解包三份 DOCX，475 源段及三份 SHA-256 一致；405 正文 line/Text key 和 8 个 Choice ID 唯一。按 spec 的三组选择、知觉/智慧和倾向门枚举 216 条逻辑路径（近路线不枚举不存在的智慧检定），所有405正文至少可达一次，每条路径无重复源段。该结果只验证文档路由模型，不验证NCS配置或运行时。
