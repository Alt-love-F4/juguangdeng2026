# 新玫瑰当前源稿编译交接（2026-10-19）

> 本文说明内容编译交付，不是“已全部可玩”的声明。未运行 Unity、未改源 Excel、未生成 JSON/C#、未改 Runtime/Schema。只在本人三个指定文件内工作，其他并行 Agent 的文件保持原样。

## 1009最终恢复：严格保留已核实既有基线

- 本轮修复恢复 `Tools/Narrative/configure_rose_1009.py` 已核实行为，不新增内容裁定：`rose.1009.s1.perception`、Inline、attributeId=perception、threshold=5。
- 原有9页、67行布局恢复；`portrait_cg` 是PassiveCheck。成功播P073并显示注册的临时CG；失败播相同P073、不显示CG；两支无条件共用P074–P076，之后自然完成根。
- 保留 `rose.1009.s1.p073.cg` / `.p073.no_cg` 及 `rose.1009.s1.p074/p075/p076` 稳定lineId，原reception页承载选项；没有额外菜单页、失败End或第10页。
- 原稿P072没有数值；门槛5来自已存在的实现基线，不宣称由原稿新增授权。此前门槛2及失败End修改是本工作包的误读，已撤销，不是主集成批准的策略。现已移除相应虚假授权审计，不要求主集成改掉原有正确Unity断言。
- 临时CG仍是 `rose.club.knight_guidance` 已注册placeholder=1，未制作或替换美术。25个聚焦测试通过，已覆盖门槛5及两种结果的共享尾部；未运行Unity。其他根不因本补丁改变。

## 1. 文件、接口与真实产出

- 编译器：`E:/myproject/unity_p_taptap2026/Tools/Narrative/Content/rose.py`
- 测试：`E:/myproject/unity_p_taptap2026/Tools/Narrative/Content/test_rose.py`
- 本文：`E:/myproject/unity_p_taptap2026/Docs/NarrativeRoseContentHandoff.md`

导出 `build(project_root: pathlib.Path) -> dict`，返回现有 Narrative sheet 的行字典列表和 `Text`；附 `_audit`、`_replace_roots`。生产编译只用 Python 标准库 DOCX XML、当前 CharacterCatalog、当前 FreeCafe catalog，不读旧 D 盘文稿或导出快照，不调用网络。

本次读取 **17 份 DOCX / 1504 个非空段落**。所有段落有 `_audit.sources[].coverage`；1263 个锚点进入正文、选择、玩法、阅读或分片，剩余为标题/控制/演出说明，没有无条件把分支指令当台词。当前生成 **18 根 / 200 ConversationPage / 1363 ConversationLine / 61 Choice / 67 Result / 5 Ending**。数量是编译行数，不是正式可玩率。

17 个源根状态：11 implemented（本地路径编译）、5 partial（有明确节点缺口）、1 construction-blocked。另有1个来源明确的10/25邀请分片。`implemented` 仍依赖主集成运行时与上游；不能等同实机验收。

`_replace_roots = ["rose.1009.s1", "rose.1011.s1", "rose.1016.s1", "rose.1103.s1"]`，仅替换这四个已存在于当前源表的根及其子 ID；后两项在此次解除整根门禁时加入，防止主合并保留旧 Condition。不得使用 faction-wide 删除前缀。保留已存在的 `rose.1009.s1.action` 与 MapPosition 挂载关系。

## 2. 当前17份原稿逐份覆盖

原稿目录：`E:/myproject/unity_p_taptap2026/策划文件/文案/a-新玫瑰（共通+支线）`。P编号一律指当前文档按非空段落序号；hash 为本次真实文件 SHA256。

| 当前文件 | 段数 | 根 ID | 本地状态 |
|---|---:|---|---|
| MA70.10.09-S1.docx | 76 | `rose.1009.s1` | implemented |
| MA70.10.11-S1.docx | 140 | `rose.1011.s1` | implemented |
| MA70.10.12-S1.docx | 86 | `rose.1012.s1` | implemented |
| MA70.10.16-S1.docx | 96 | `rose.1016.s1` | partial |
| MA70.10.19 - S1.docx | 55 | `rose.1019.s1` | implemented |
| MA70.10.24-S1.docx | 128 | `rose.1024.s1` | implemented |
| MA70.10.24-S2.docx | 153 | `rose.1024.s2` | partial |
| MA70.10.26-S2.docx | 64 | `rose.1026.s2` | partial |
| MA70.10.26-Short Ending 1 信任的界限.docx | 57 | `rose.ending.trust_limit` | implemented |
| MA70.11.01-S1.docx | 60 | `rose.1101.s1` | partial |
| MA70.11.01—结局分支 Short Ending2 工作需求要看清.docx | 31 | `rose.ending.job_requirements` | implemented |
| MA70.11.03—S1.docx | 150 | `rose.1103.s1` | partial |
| NR70.11.11-S2.docx | 23 | `rose.route.1111.s2` | implemented |
| NR70.11.15-S1 结局分支-Normal Ending 理想新世界.docx | 183 | `rose.route.1115.s1` | construction-blocked |
| NR70.11.22-S1.docx | 65 | `rose.route.1122.s1` | implemented |
| NR结局-结局分支—BAD Ending：无法原谅.docx | 22 | `rose.route.ending.unforgiven` | implemented |
| NR结局-True Ending：另一种绽放.docx | 115 | `rose.route.ending.bloom` | implemented |

### `rose.1009.s1`

两种来由回答P045/P046分别进入P048–P052 / P054–P063，合流P065–P071。严格恢复既有9页/67行与Inline检查 `rose.1009.s1.perception`，知觉≥5时PassiveCheck播P073+临时CG；失败播相同P073无CG；两支共用P074–P076并自然完成根，没有失败End。保留原MapAction、Action、页及尾文lineId。

SHA256：`fbbf2128352a45ec6b83c3ca673b034ff9eafea81fdc1e5ec30fc6ad5bc579a6`。

### `rose.1011.s1`

麻雀饮品三选、新闻二选；真实 taste:blackcoffee:90:1：低品 P050 后也继续。nature P060/P061 → 短尾 P068–P073 → End；P062 后 reason P079/P081 同短尾 → End；P080 且智慧<3 同短尾 → End；P080 且智慧≥3 → P085–P140 长线，含历史专精/知觉分支、名片 Reading、根尾赏识。

SHA256：`a38b775b8245a58a5fdfbf909e47d0cef0cbbd4c4b5250c96a20056e331391f9`。

### `rose.1012.s1`

真实 blackcoffee60×1 + milkcoffee60×1；失败 P016 回制作。既有赏识 false → P039–P054 短尾 End；true → P056–P070，按帽子帮助事实分 P072–P075 / P077–P083，合流 P085。

SHA256：`34d940e4ae1f3e2d5bdfa5ea867c617e0c9785716fb239170b4c30b120860895`。

### `rose.1016.s1`

当前96段全部有锚点。知觉/智慧心理分支；使用真实甜/无糖黑咖啡各一杯的 normal60 selector，要求不同 Cup ID；失败 P019–P020 重做。拒绝信任 → P049–P053 End；信任 → 三话题任意顺序各一次（64→65→66、64→66→65、65→64→66、65→66→64、66→64→65、66→65→64）→ 倾向三选。第三倾向未裁定故锁定；甜/无糖由真实糖料正量/零量证明，整根已开放。“微苦”没有作者数值阈值，只保留节点级未定说明，不据此再阻断整根。

SHA256：`50a60576a409903268e7453c7ea2bff8a0d36841434d4b6dbbfdbfb51775e620`。

### `rose.1019.s1`

知觉>7 才出现 P019–P021；真实奶油咖啡+牛奶咖啡 normal60 各一杯；指路/拒绝二选，仅指路给西尔万、阿黛尔各+1；均合流 P051–P054。

SHA256：`5d6ba7f6c3f004936c347478f04b0b9e89b6d78579265e7b40e23fcb78a64dbc`。

### `rose.1024.s1`

仅教会搜查脱险者；真实 blackcoffee90×1，失败重做；四种身份猜测同一回复；挂旗 → allegiance；不挂 → short1 pending。贝特朗仍用 canonical actor，显示名“男孩”。

SHA256：`772c98aa35a40a1533d90ce3ca30b047e7e1c2c508bf475845a0cb9523c2b0dc`。

### `rose.1024.s2`

仅已真实释放且 bribery/rescue 二者恰有一个；两套内心独白和威胁回应互斥。真实 blackcoffee90×1。五问题中仅“目的”正确；知觉>9 可提前效忠并跳过重复效忠菜单；最后效忠 → allegiance；正确提问后拒绝 → safe_refusal；错误提问后拒绝 → short1 pending。

SHA256：`4ee8f403d61752b88e0180764f35328d13f8c7af99aa141b663efce52baad216`。

### `rose.1026.s2`

同份原稿 P004–P014 拆成 rose.1025.s2：day20/10月25日 BeforeNight 读邀请；主根 day21/10月26日 BeforeNight 到会客室。三种角色回答互斥，第三项智慧≥7；知觉>8且智慧>9 才出现 P044；根尾提交 prisoner_list_received。俱乐部地图解锁效果尚待主集成。

SHA256：`1003a611856acc4da80632344e77485b8dc190978d43b36f24402e155143e6a0`。

### `rose.ending.trust_limit`

只在10月26日且 short1 pending=true 启动。街道恢复 → B太太数日来访 → 大堂区遇刺 P056；完整尾部提交 terminal 和 completed，Ending 才有资格。源码明确“不用制作环节”，没有额外插入制作。

SHA256：`067b334bbc77f02167a245dce1f90633f30a8e732e9bf59d5e6f9eedd9a77db6`。

### `rose.1101.s1`

合作=true（两种原始经历）→ P043–P058 → End；拒绝=true且确实完成1024-S1 → 根尾 short2 pending + QUEUE_EVENT；拘押经历拒绝合作无源去向，门禁不允许该组合自动当成功或死亡。

SHA256：`7cbfeb2a9703fefea337871fca562d3674069871497637f586dd061c39c3ccf2`。

### `rose.ending.job_requirements`

只由1101失败队列进入，另检查 short2 pending；P004–P030 完整尾声之后才提交 terminal/Ending。

SHA256：`4c07bf3d507598906bac6cf220228d4079256bd6a6884985a8d52441202565c5`。

### `rose.1103.s1`

先真实等待 `serve:any.hot:60:1`（交付实际产品，实际温度≥40℃且评级≥60），才进入庇护选择。拒绝庇护 → P053–P066 End 仍有编译，但因当天全部营业额尚不能事务支付，此选项锁定；同意庇护 →身份公开，智慧>8心理；安慰 → P111–P114 End，不能串播质疑；质疑 → P117–P148 并好感+1。庇护/拒绝事实根尾提交。整根已开放；特殊 cocoa+hotmilk 台词仅有默认 false、无生产者的独立分支标记，any.hot 成功绝不自动触发它。拒绝分支仍待营业额支付，普通接受/安慰/质疑路径不因此阻断。

SHA256：`30ca5fcf5f0e18b9bd03d7b974c67148137c8b45a6b200e868591ac1f5f52528`。

### `rose.route.1111.s2`

只接 church.1111.s1 正式四路选择、route.rose 已提交，其他三路=false；播放本文件全部正文。不在本根伪造选路事实。

SHA256：`8604a70a3f860aec7cd3c2a4ad201861151604f87b5a41586b8e805de0388d96`。

### `rose.route.1115.s1`

两动机互斥；源 low candor 尾 P105–P113 后 End+理想新世界 terminal；high candor 才继续 P116；知觉>9 发起追问后才允许其回答及后续计划。坦诚值和知觉未通过尾部未给，整根硬门禁；不发明坦诚初值、不把+2当成>2。

SHA256：`8154c6620157dceaa6264de17c94ecbd786abafa525bb0d83df8c6592d299717`。

### `rose.route.1122.s1`

必须前一根真实获得 plan_known；阿黛尔被扣留、读信、夜间麻雀到店；毒药 / 加酒二选，各自 pending + QUEUE_EVENT，只进一个结局。普通文本不把毒药选择混成两杯连续咖啡。

SHA256：`6cfbc4de3e40d908c79b8fdf55cab2f3c1a22ec549c3dff99522a341ee89ae1c`。

### `rose.route.ending.unforgiven`

仅 POISON pending 与玫瑰路线正确时可入；完整遗书 P002–P021 后 terminal；文件名 BAD 与正文 Normal 冲突保留审计，以语义 unforgiven 命名，不裁定评级。

SHA256：`96c725b0d2cb1fe2e53ddbd5cf5700331e3b1d69cc4807b896ce1573351bc2d5`。

### `rose.route.ending.bloom`

仅 BLOOM pending 与玫瑰路线正确时可入；加酒、重逢、诸人后日谈 P003–P114 完整叙述，根尾 terminal。P029 次日与结算是终局叙事蒙太奇，不在此改世界日期或重启营业。

SHA256：`ad97a93ac35905b360c6817bea60ac8cbfc440172a6677e18f484d773bf25a6b`。

## 3. 所有剩余阻塞节点（不能删门禁假装完成）

当前仅 `rose.route.1115.s1` 保留 `scope=root` 硬门禁：每个事件 OR 组都要求默认 false、无生产者的 `integration.<root>.ready=true`。1016和1103不再输出整根ready条件。`scope=node` 表示局部未定或尚锁定分支，不阻断已经准确编译的共通路径。

| 根/节点 | 范围 | blockedBy | 准确缺口 |
|---|---|---|---|
| `rose.1016.s1` / P015 | node | `light_bitter_threshold_unspecified` | Source 微苦加糖 / 无糖 is represented by actual positive/zero sugar on two distinct blackcoffee cups. Source gives no quantitative 微苦 threshold; none is invented. Exact bitterness tuning remains a content node, not a root gate. |
| `rose.1016.s1` / P093 | node | `ambiguous_faction_recipient` | Source 中立/王庭教会倾向+1 does not identify one or both recipients. Third stance remains locked until content decision. |
| `rose.1024.s2` / P006 | node | `street_tip_unpriced` | Source hands over a purse to street guards but gives no amount; no invented MONEY_REMOVE. Economic effect remains pending even though dialogue is mapped. |
| `rose.1026.s2` / P063 | node | `club_map_action_pending` | Source unlocks garden-district club access. Main map integration must register reusable club action, not a parallel location. |
| `rose.1101.s1` / P041 | node | `detained_refusal_unspecified` | Only non-detained 10/24-S1 plus refusal is authored as failure; other refusal origin excluded by event groups. |
| `rose.1103.s1` / P031 | node | `special_drink_identity_pending` | Common path waits for one actual hot product >=40C and score>=60. Special cocoa+hotmilk identity/combination data is unavailable in the root-local passed-only receipt; P032-P037 remains disabled behind a false no-producer marker, never inferred from any.hot success. |
| `rose.1103.s1` / P058 | node | `today_revenue_transfer_missing` | Refusal hands over ALL today revenue, not current money or guessed number. Choice P050 is locked behind integration.rose.1103.revenue_transfer.ready=false until actual transactional transfer is connected; common acceptance paths remain open. |
| `rose.route.1115.s1` / P099 | root | `candor_baseline_and_gap` | P099 +2 candor, P104 <1, P115 >2. No initial value, prior producer, contract numeric target or 1..2 outcome. Never map second choice automatically to >2. Draft low/high flags have no producer. |
| `rose.route.1115.s1` / P138 | root | `perception_failure_tail_unspecified` | P138 opens the dialogue through P182 with no alternate tail for perception<=9. Never play the answer when the initiating question was skipped; content must specify that return path. |

此次改为 partial 的1016可正常生产 completed/known.sade/离场事实，其后的1019不再因玫瑰本包的整根门禁阻断。1103完成真实热饮后可走普通接受庇护、安慰或质疑路径；不能通过未付营业额的拒绝选项。1115之后的1122及其两个终局仍不能拿独立预览当自然通关。教会拘押/释放、革命1029及主业务API各有独立集成验收，本文不替其他模块宣称实机通过。

### 2026-10-19 主集成追加契约

- `serve:any.hot:60:1`：通配的是实际 Product，不是忽略物品；业务必须确认该真实杯内容的温度≥40℃，不能用“所选配方通常是热的”代替实际温度。
- `serve:blackcoffee.sweet:60:1+blackcoffee.unsweetened:60:1`：两个授权 selector 分别检查真实糖料份额>0与=0，底层产品仍是catalog里的blackcoffee，且必须是两只不同的真实杯。
- 40℃为主集成明确授权的工程判据，原稿“微苦”没有量化标准；不发明苦味/糖克数阈值。
- 以上为用户明确新增的跨业务API约定。本工作包只更新内容和契约测试；没有自行修改域模型/桥，也没有把尚在并行接入的API冒称为实机测试通过。
## 4. 跨工作包的事实/ID契约

- 真实角色目录：`adele`, `sparrow`, `sylvain`, `alienor`, `sade`, `bertrand`, `rose.envoy`, `rose.butake`, `guest_b`。不新增替代的西尔万/萨德。未知的源名配角埃尔玛/贾科尼/凯用 scoped `rose.elma`, `rose.giaconi`, `rose.kai` Object，无立绘，有美术审计；绝不挪用熟人的身份。
- 1024男孩使用 `bertrand` canonical actor + `speakerNameKey=男孩`；1011正式自我介绍前的西尔万使用 `sylvain` canonical actor + `speakerNameKey=客人`，不提前通过UI泄露正式姓名。
- 外部输入：`story.church.1009.s2.hat_helped`、`story.church.1020.s1.cleared`、`church.release.bribery`、`church.release.rescue`、`story.revolution.1029.s1.cooperated`、`story.revolution.1029.s1.refused`。按实际并行模块命名对齐；默认 false，不制造“缺省就是救出”。
- 输出 `story.rose.1024.allegiance` / `.safe_refusal` 供革命工作包。`safe_refusal` 只在1024-S2正确问题后拒绝效忠时置真；不是所有拒绝都安全。
- 输出庇护：`story.rose.1103.s1.sheltered`、`church.alienor.sheltered` / `church.alienor.refused_shelter`，供教会1104/1105。身份已知单独为 `story.rose.1103.s1.identity_known`，不是“是否做过质疑选项”。
- 中央入线：教会 `church.1111.s1` 根尾提交四个互斥 `route.*` 并队列 `rose.route.1111.s2`。玫瑰入口额外检查中央 completed、rose=true、其余=false，不在子根复制选路系统。
- 数字守卫的 bar key 必须是实际注册的 `player.wisdom`、`player.perception`；AbilityCheck attribute ID 若需要新增才用裸 `wisdom/perception`。本编译器没有新增玩家属性或复制金钱/日历。好感、阵营用契约 `adele.affection`、`sylvain.affection`、`alienor.affection`、`faction.*`。

### 认识事实生产者

全部 known.* 声明初始 false / onlySetTrue=true，Result 在根尾事务提交，制作等待、取消、预览或仅文件名不生产。1016及1103只在真实制作结束且实际完整尾部后提交；等待中仍不能提前认识人物或提交庇护。

| 事实 | 生产根 | 源证据/守卫 |
|---|---|---|
| `known.sparrow` | `rose.1011.s1` | P003-P038; every actually completed short/long tail；`root completed` |
| `known.sylvain` | `rose.1011.s1` | P125 explicit introduction, P128-P130 card, P136-P140 long tail；`choice:rose.1011.s1.choice.p080 AND bar:player.wisdom:ge:3` |
| `known.alienor` | `rose.1012.s1` | P032-P034 actual encounter; P039 or P072/P077 branch-specific introduction；`root completed` |
| `known.sade` | `rose.1016.s1` | P010 actual introduction; P049-P053 refusal or P083-P096 long tail；`root completed` |
| `known.alienor` | `rose.1016.s1` | P009-P010 actual encounter, not filename inference；`root completed` |
| `known.alienor` | `rose.1103.s1` | P020 actual arrival and both completed tails；`root completed` |

### 日常可用性离场事实

`story.adele.away` / `story.sylvain.unavailable` 声明初始 false、**onlySetTrue=false**；离场后真实归来必须允许清除。只记录源中请假、外地讲课、被扣押或真实返店；一般喝完离开不等于永久不可用。

| 根 | 状态 | 值 | 证据 |
|---|---|---|
| `rose.1011.s1` | `story.adele.away` | false | P003 actual cafe presence |
| `rose.1012.s1` | `story.adele.away` | false | P004-P031 actual presence |
| `rose.1016.s1` | `story.adele.away` | true | P005 leave of absence |
| `rose.1016.s1` | `story.sylvain.unavailable` | true | P036 out teaching/avoiding danger |
| `rose.1019.s1` | `story.adele.away` | false | P007 actual presence |
| `rose.1019.s1` | `story.sylvain.unavailable` | false | P024-P025 actual return |
| `rose.1024.s1` | `story.adele.away` | false | P022-P124 actual presence |
| `rose.1024.s2` | `story.adele.away` | false | P015-P019 actual reunion |
| `rose.1101.s1` | `story.adele.away` | false | P058 actually returns；守卫 `story.revolution.1029.s1.cooperated` |
| `rose.1101.s1` | `story.adele.away` | true | P015 leaves; terminal refusal has no return；守卫 `story.revolution.1029.s1.refused` |
| `rose.1103.s1` | `story.adele.away` | false | P003 actual presence |
| `rose.route.1115.s1` | `story.adele.away` | false | P011-P020 actual presence |
| `rose.route.1122.s1` | `story.adele.away` | true | P003-P027 missing/held by New Rose |
| `rose.route.ending.bloom` | `story.adele.away` | false | P032-P044 actual reunion |

1016现已解除整根内容门禁；真实制作与本根完整结束后才提交请假/讲课事实，不能在等待中提前写状态。主日常管线继续使用既定10/16–18排期安全策略，不能因为事件尚未走完而误称人物已经回城。人物在本根中段离开但结尾真实归来（1101合作成功）只结算最终 false，避免存档留在中段状态。

## 5. 真实咖啡交互、预设与运行时假设

- 1011：`taste:blackcoffee:90:1`。一个真实交付尝试；低品返回 passed=false，P050播放后继续，不取消、不自动判成功。订单 `{drinkname}` 原稿未指名，本轮按主集成授权实际产品预设显示“黑咖啡”，不是声称作者原定饮品。
- 1012：`serve:blackcoffee:60:1+milkcoffee:60:1`，至少两杯不同真实产品，不能一杯抵两杯。
- 1016：`serve:blackcoffee.sweet:60:1+blackcoffee.unsweetened:60:1`，是当前96段稿的**普通**门槛，不是旧稿优秀。两个selector检查真实糖料份额正量/零量及不同杯ID；“微苦”精确阈值仍未定，但按主集成指令不阻断共通根。
- 1019：`serve:creamcoffee:60:1+milkcoffee:60:1`。creamcoffee 已在真实 FreeCafe products 列表验证，不是从旧 Recipe.xlsx 猜的名字。
- 1024双根：`serve:blackcoffee:90:1`。serve负责真实必须达标/重做；已有源失败页可接受 failed 回传后显式 nextPageId 重做，不把取消当低品。
- 1103：`serve:any.hot:60:1`，实际交付产品温度≥40℃/评级≥60。hotmilk仍是entity而非Product，不伪造`serve:hotmilk`；特殊 cocoa+hotmilk 身份数据仍未返回，因此只有该段 P032–P037 是 pending，默认不播。
- normal60 / excellent90 是可调工程预设，不是作者写出的数值；1011智慧≥3来自仓库旧施工脚本明确记载的 user-confirmed 决策；1009知觉≥5严格恢复既有configure_rose_1009.py基线与 `rose.1009.s1.perception` Inline/PassiveCheck API；成功/失败P073差异仅在CG，两支共享P074–P076。
- 主运行时须支持晨间/营业关闭时针对剧情的真实订单API，不能为了满足制作悄悄开店、跨日或重启经营。未在本工作包编辑业务系统。
- Gameplay结束仅发布 `gameplay:<pageId>:passed` 根内结果，继续由既有NCS拥有者恢复；choice/result全部在根尾结算。过期回调、重复交付、取消/重播/存读档由主运行时测试覆盖。
- 名片仍为同根 LetterRead + ReadingPage，**不是信封 reveal**，不调用取名/开场信。主适配器需保持可自然读完、关闭不当完成、无信封的阅读语义。
- 外景只用注册 key：`club.exterior`, `club.reception`, `rose.club.blindfold`, `rose.club.knight_guidance`, `cafe.exterior.day`, `street.day`, `market.day`；普通店内恢复 `scene.cafe`。未动一/二/三楼经营原图。地下会议室暂沿同俱乐部会客图，已审计，不伪造美术。

## 6. Ending 安全规则

5行 Ending：信任的界限、工作需求要看清、理想新世界、无法原谅、另一种绽放。全部 `enabled=true` 但 `Condition ownerType=ending` 同时检查根尾 terminal 与 completed；没有任何 Ending 仅按日期或某个未读完的选择 flag 触发。选支只提交 pending 并通过 QUEUE_EVENT进入独立尾声。理想新世界是1115真正终止的原文路径，短尾有 End，不能接着播高坦诚长线。优先级统一20，可由主整合统一协调，不用极大值抢占。

## 7. 实际运行的测试

使用本机已配置的 bundled Python，无网络：

```powershell
& 'C:/Users/AltF4/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' -B -m unittest Tools.Narrative.Content.test_rose -v
```

**结果：25 tests passed。** 覆盖：当前17源/真实hash/逐段锚点；纯构建无文件写；主键和引用；指令不入台词；1009原回答、Inline/PassiveCheck ID、知觉0/2/4/5/6边界、成功/失败均且只播一次P073–P076、成功独有临时CG、9页67行及原尾文lineId；1011所有短End、智慧门槛、低品继续、长线认识/赏识/名片、未制作不提交；1012四种赏识/帽子组合和重做；1016六种话题顺序、拒信End；1019仅指路给奖励；1024两种释放×五问题、知觉提前效忠；1101合作/拒绝；1103安慰早End；1115低尾/知觉分支；结束条件不靠日期；1122互斥队列；离场状态可清除；1025/1026真实日期分片；锚点漂移硬门禁；实际 player.* 数字 key；新增1016糖量selector/真实重做等待、1103热饮等待后两条接受路径、特殊对白不误触发、未付营业额拒绝锁定、授权selector正反查验。

**没有运行**：主Excel合并、官方Luban生成、Unity编译/PlayMode、实际咖啡馆操作、存读档、PresentationDebugScene回归。单元测试里的Walk是小型声明式图解释器，验证编译输出，不冒充Unity实测。

## 8. 后续接手顺序

1. 主整合加载 build 并合并源表；共享global rows去重、保留可反转离场flags。仅源Excel修改后走官方Luban生成。
2. 验证主运行时 taste/serve、targeted real narrative order、End、nextPageId、根尾条件结果；优先实测1011短→End与低品→继续，避免影响既有咖啡交互。
3. 主域/桥实测any.hot实际温度≥40℃、甜/无糖实际糖份及独立杯ID；内容侧继续补1016微苦精度/第三倾向、1103特殊饮品身份/营业额支付。只有1115仍需解决坦诚度/知觉失败尾后再移除root gate，不能手工勾ready跳过缺口。
4. 补1024街头打点经济语义、1026俱乐部地图入口、1024非拘押+革命1029未给路径等跨源问题。
5. 用现有 `Assets/Scenes/PresentationDebugScene.unity` 验证图→实景→图、连续翻页/取消/重播；不新建场景/Canvas/平行剧情系统。正式日期/夜晚/中央分线另走主流程验收。

## 附：完整生成页ID索引（审计路径，不是新增生产依赖）

以下路径直接对应当前build输出；每页源段和守卫见 `_audit.sources[].coverage` 及对应sheet行。

### `rose.1009.s1`

`rose.1009.s1.door`, `rose.1009.s1.blindfold`, `rose.1009.s1.reception`, `rose.1009.s1.origin_letter`, `rose.1009.s1.origin_uncle`, `rose.1009.s1.request`, `rose.1009.s1.portrait_cg`, `rose.1009.s1.portrait_no_cg`, `rose.1009.s1.portrait_tail`

### `rose.1011.s1`

`rose.1011.s1.arrival`, `rose.1011.s1.sparrow.insight`, `rose.1011.s1.offer`, `rose.1011.s1.menu.sparrow_drink`, `rose.1011.s1.sparrow_drink.21`, `rose.1011.s1.sparrow_drink.23`, `rose.1011.s1.sparrow_drink.25`, `rose.1011.s1.news`, `rose.1011.s1.menu.heard`, `rose.1011.s1.heard.yes`, `rose.1011.s1.heard.no`, `rose.1011.s1.sylvain.arrives`, `rose.1011.s1.brew_gate`, `rose.1011.s1.brew_gate.failed`, `rose.1011.s1.debate`, `rose.1011.s1.menu.nature`, `rose.1011.s1.ordinary`, `rose.1011.s1.short.nature`, `rose.1011.s1.end.short.nature`, `rose.1011.s1.not_human`, `rose.1011.s1.menu.reason`, `rose.1011.s1.short.reason`, `rose.1011.s1.end.short.reason`, `rose.1011.s1.short.wisdom`, `rose.1011.s1.end.short.wisdom`, `rose.1011.s1.long.answer`, `rose.1011.s1.long.history_known`, `rose.1011.s1.long.history_unknown`, `rose.1011.s1.long.history`, `rose.1011.s1.long.recognition`, `rose.1011.s1.long.farewell`, `rose.1011.s1.long.card`, `rose.1011.s1.long.card_memory`, `rose.1011.s1.long.tail`, `rose.1011.s1.end`

### `rose.1012.s1`

`rose.1012.s1.trial_offer`, `rose.1012.s1.brew_gate`, `rose.1012.s1.brew_gate.failed`, `rose.1012.s1.trial_success`, `rose.1012.s1.outside`, `rose.1012.s1.hat.memory`, `rose.1012.s1.no_appreciation`, `rose.1012.s1.end.short`, `rose.1012.s1.appreciation.outside`, `rose.1012.s1.appreciation`, `rose.1012.s1.hat.helped`, `rose.1012.s1.hat.not_helped`, `rose.1012.s1.tail`, `rose.1012.s1.end`

### `rose.1016.s1`

`rose.1016.s1.arrival`, `rose.1016.s1.sade.insight`, `rose.1016.s1.order`, `rose.1016.s1.brew_gate`, `rose.1016.s1.brew_gate.failed`, `rose.1016.s1.seated`, `rose.1016.s1.sade.caution`, `rose.1016.s1.trust.offer`, `rose.1016.s1.menu.trust`, `rose.1016.s1.trust.refused`, `rose.1016.s1.end.refused`, `rose.1016.s1.trust.accepted`, `rose.1016.s1.menu.topics.start`, `rose.1016.s1.topics.start.answer.64`, `rose.1016.s1.menu.topics.64`, `rose.1016.s1.topics.64.answer.65`, `rose.1016.s1.menu.topics.64_65`, `rose.1016.s1.topics.64_65.answer.66`, `rose.1016.s1.topics.64.answer.66`, `rose.1016.s1.menu.topics.64_66`, `rose.1016.s1.topics.64_66.answer.65`, `rose.1016.s1.topics.start.answer.65`, `rose.1016.s1.menu.topics.65`, `rose.1016.s1.topics.65.answer.64`, `rose.1016.s1.menu.topics.65_64`, `rose.1016.s1.topics.65_64.answer.66`, `rose.1016.s1.topics.65.answer.66`, `rose.1016.s1.menu.topics.65_66`, `rose.1016.s1.topics.65_66.answer.64`, `rose.1016.s1.topics.start.answer.66`, `rose.1016.s1.menu.topics.66`, `rose.1016.s1.topics.66.answer.64`, `rose.1016.s1.menu.topics.66_64`, `rose.1016.s1.topics.66_64.answer.65`, `rose.1016.s1.topics.66.answer.65`, `rose.1016.s1.menu.topics.66_65`, `rose.1016.s1.topics.66_65.answer.64`, `rose.1016.s1.topics.tail`, `rose.1016.s1.menu.stance`, `rose.1016.s1.tail`, `rose.1016.s1.end`

### `rose.1019.s1`

`rose.1019.s1.arrival`, `rose.1019.s1.insight`, `rose.1019.s1.sylvain`, `rose.1019.s1.brew_gate`, `rose.1019.s1.request`, `rose.1019.s1.menu.guide`, `rose.1019.s1.guide.yes`, `rose.1019.s1.guide.no`, `rose.1019.s1.tail`, `rose.1019.s1.end`

### `rose.1024.s1`

`rose.1024.s1.arrival`, `rose.1024.s1.brew_gate`, `rose.1024.s1.brew_gate.failed`, `rose.1024.s1.talk`, `rose.1024.s1.menu.identity_guess`, `rose.1024.s1.flag.offer`, `rose.1024.s1.menu.flag`, `rose.1024.s1.end`

### `rose.1024.s2`

`rose.1024.s2.homecoming.street`, `rose.1024.s2.homecoming`, `rose.1024.s2.brew_gate`, `rose.1024.s2.brew_gate.failed`, `rose.1024.s2.talk`, `rose.1024.s2.mind.bribed`, `rose.1024.s2.mind.rescued`, `rose.1024.s2.threat`, `rose.1024.s2.bribed`, `rose.1024.s2.rescued`, `rose.1024.s2.menu.question`, `rose.1024.s2.question.other`, `rose.1024.s2.question.purpose`, `rose.1024.s2.rose.offer`, `rose.1024.s2.signal`, `rose.1024.s2.menu.signal`, `rose.1024.s2.rose.normal_offer`, `rose.1024.s2.menu.allegiance`, `rose.1024.s2.tail`, `rose.1024.s2.end`

### `rose.1026.s2`

`rose.1026.s2.meeting`, `rose.1026.s2.menu.role`, `rose.1026.s2.role.wait`, `rose.1026.s2.role.rooms`, `rose.1026.s2.role.bridge`, `rose.1026.s2.alliance`, `rose.1026.s2.doubt`, `rose.1026.s2.mission`, `rose.1026.s2.end`

### `rose.ending.trust_limit`

`rose.ending.trust_limit.recovery.street`, `rose.ending.trust_limit.story`, `rose.ending.trust_limit.betrayal`, `rose.ending.trust_limit.end`

### `rose.1101.s1`

`rose.1101.s1.arrival`, `rose.1101.s1.success`, `rose.1101.s1.end.success`, `rose.1101.s1.end`

### `rose.ending.job_requirements`

`rose.ending.job_requirements.story`, `rose.ending.job_requirements.end`

### `rose.1103.s1`

`rose.1103.s1.rain`, `rose.1103.s1.brew_gate`, `rose.1103.s1.drink.special`, `rose.1103.s1.shelter.request`, `rose.1103.s1.menu.shelter`, `rose.1103.s1.shelter.refused`, `rose.1103.s1.end.refused`, `rose.1103.s1.shelter.accepted`, `rose.1103.s1.insight`, `rose.1103.s1.identity`, `rose.1103.s1.menu.response`, `rose.1103.s1.comfort`, `rose.1103.s1.end.comfort`, `rose.1103.s1.question`, `rose.1103.s1.end`

### `rose.route.1111.s2`

`rose.route.1111.s2.choice_of_order`, `rose.route.1111.s2.end`

### `rose.route.1115.s1`

`rose.route.1115.s1.letter`, `rose.route.1115.s1.letter_and_meeting`, `rose.route.1115.s1.insight`, `rose.route.1115.s1.politics`, `rose.route.1115.s1.menu.motive`, `rose.route.1115.s1.cause`, `rose.route.1115.s1.family`, `rose.route.1115.s1.question_of_cost`, `rose.route.1115.s1.normal_ending`, `rose.route.1115.s1.end.normal_ending`, `rose.route.1115.s1.candor.high`, `rose.route.1115.s1.interjection`, `rose.route.1115.s1.plan`, `rose.route.1115.s1.known_identity`, `rose.route.1115.s1.long.tail`, `rose.route.1115.s1.end`

### `rose.route.1122.s1`

`rose.route.1122.s1.missing_adele`, `rose.route.1122.s1.letter`, `rose.route.1122.s1.night`, `rose.route.1122.s1.menu.coffee`, `rose.route.1122.s1.end`

### `rose.route.ending.unforgiven`

`rose.route.ending.unforgiven.story`, `rose.route.ending.unforgiven.end`

### `rose.route.ending.bloom`

`rose.route.ending.bloom.story`, `rose.route.ending.bloom.end`

### `rose.1025.s2`

`rose.1025.s2.invitation`, `rose.1025.s2.end`
