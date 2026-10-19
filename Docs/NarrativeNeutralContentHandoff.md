# 中立共通 / 中立分线内容编译交接（2026-10-19）

## 所有权与真实完成边界

本轮只写：`Tools/Narrative/Content/neutral.py`、`Tools/Narrative/Content/test_neutral.py`、本文。**未修改 Excel、Luban 产物、运行时、Schema、经营系统或教程；未执行生成或 Unity 编译/PlayMode。** Python 图模型测试通过不等于整条中立线正式可通关。目前仅11/29营收根保留整根施工阻塞；促销/记者正常路径已开放；11/18皇女庇护路径也已开放，事件与选项均要求alienor.affection≥3，拒绝end_if4和未知金额贿赂分别禁选。满足庇护条件后可继续11/22并选择B，或留下且未入会进入C；营收依赖的D仍不可声称正常可达。

依据 `Tools/Narrative/Content/INTEGRATION_CONTRACT.md` 及仓库当前 `策划文件/文案/a-中立（共通+支线）` 全部 **16 DOCX / 2305 非空段**。没有用旧 D 盘映射。每文件有当前 SHA-256 与段落数基线；漂移会 fail closed，必须重新 diff 分支段后更新基线，不能只更新 hash。

## 导出 API 与合并

```python
from pathlib import Path
from Tools.Narrative.Content.neutral import build
rows = build(Path('E:/myproject/unity_p_taptap2026'))
```

返回既有 Narrative sheet 名→row dict 列表，另含 Text、`_audit`、`_replace_roots`。**使用源 Excel 名**：Text=`key/text/comment`；ConversationLine=`speakerKey/side/emote`；Event=`eventProperty`；数值 Condition=`BAR_GTE/BAR_BETWEEN`。不使用 value/speakerId/speakerSlot/emotion/BAR_GE 等不正确的替代字段。Gameplay 使用主 Agent 按契约扩展的 `ConversationPage.gameplayKey`，本模块不改 Schema。

`_audit` 是16项列表，每项有 source/source_sha256/source_paragraphs/root/supported_events/status/blockedBy/pending/anchors；每个 PNNN 含原文 raw 与 line/choice/check/result/gameplay/direction/blocked-directive 等用途。

`_replace_roots` 有15个重建根，**不含 oct07.s1**。主合并器应替换这些根的旧演出/条件/结果/文本，不能只追加新key而留下旧页造成双剧情。保留其他模块与全局行。neutral.1008.s1.action 也在替换前缀中，因此模块重出同名真实 Action，现有 market MapPosition 引用继续有效；不写 MapPosition。oct07.s1/s2、教程不在替换范围。

相识/依赖 ConditionKey 默认 false、onlySetTrue=true，nameText 有配套 Text。主集成拥有四个互斥 route flag、关系/阵营 Bar、属性/provider。本模块不输出任何 route flag 或 Bar 行。跨模块同名依赖 ConditionKey 按主定义去重，不能误改全局 route flag。

预期输出：15 Event、11 EventDate、164 ConversationPage、1655 ConversationLine、33 Choice、12 Inline AbilityCheck、53 Result、4 Ending、5 generic Object、1 Action；Text/flag名/诊断数量不是剧情完成率。

## 跨模块事实与规范 ID

### 共通

- oct07.s1 不输出任何替换 Event/Conversation/Result/Text；当前48段仍全部审计，保留已工作首日链。
- neutral.1008.s1 保持 MapAction、第3天、world.map.active。A/B/C及语言≥2成败互斥；根尾 `known.colette=true`，取消不写相识。
- neutral.1010.s1 前置 `story.rose.1009.s1.completed`。先筹备，根尾 QUEUE_EVENT neutral.1010.s2，避免同日先筛选S2导致漏事件。
- 10/10-S2原稿显式依赖筹备，但S1已有客人、S2又称营业前。队列尊重明确前置，不实际倒拨经营时钟；此正文时序需编辑裁定。
- 10/10-S2 只有接待 A/B 根尾写 `known.antoine=true`。拒绝C不进讨论、不获得相识/好感/赏识。
- `neutral.antoine.appreciation`：10/10-S2经营讨论A+语言≥3成功根尾产出。
- **当前10/13-S1仍编到兼容根 neutral.1013.s2，不生成 neutral.1013.s1。**
- `neutral.antoine.observation`：促销实际完成且此前赏识为真、播过附加观察后根尾产出；11/12邀请依赖它。
- C13继续读 `story.neutral.1013.s2.completed`；完成结果同时有 first_service 与 activity 两次真实 Gameplay passed 条件，不会只读完开头就解锁晚谈。
- **实际根尾队列已接入**：促销根的 `neutral.1013.s2.result.c13_continuation` 是 eventId=neutral.1013.s2 的 QUEUE_EVENT，target=revolution.1013.s2；使用与完成结果完全相同的两项Gameplay守卫。不是点击选项即排队，也不是仅设置完成flag等待不存在的日期入口。取消/证据不完整均不排队。
- 已读取当前革命编译器确认 C13 为 Immediate、无EventDate，且检查促销completed；本模块不复制其 Event/日期/台词。
- neutral.1027.s1 前置 `story.rose.1016.s1.completed`。智慧≥4/语言≥3分支根尾结算 alienor.affection +2/+1/-1。
- neutral.1030.s1 原稿入场 adele.affection≥3；选择/知觉≥4对应+1/+2/0/-1。

### 分流及分线

中央 `church.1111.s1` 应在自己的根尾完成选路后 QUEUE_EVENT：

1. 供出情报/中立A → `neutral.route.1111.s1`（平凡之日）。
2. 语言≥4、不供情报、最终不参与政治 → `neutral.route.1111.s2`（继续中立线）。

两根均 Immediate、无 EventDate；不复制中央选路检定。**两种入口均要求 route.neutral=true，其余三旗=false；A不是教会入线。**

后续根统一 `neutral.route.1112.s1 / 1118.s1 / 1122.s1 / 1129.s1 / 1130.s1 / 1130.s2`。

- `neutral.association.member`：11/12接受选项根尾产出。
- `neutral.investigation.shelter`：11/18选B且 alienor.affection≥3，真实完成庇护支线后根尾产出。事件入口本身也有BAR_GTE alienor.affection 3；好感不足/未提供时不进入全禁选菜单。没有accepted choice不会置shelter或completed，取消不结算。
- `neutral.investigation.bribed`：**只声明不生产**；金额/扣款/失败终局未授权。
- `neutral.stayed`：11/22留下支线根尾产出；其 End 立即结束本根，不继续B结局内容。
- `neutral.association.committee`：11/29根尾；目前在营收阻塞后面，不能视为正常可获得。
- `neutral.negotiation.success / neutral.negotiation.failed`：11/30语言≥5的真实 Inline PassiveCheck 成败。
- 谈判失败 End 后根尾 QUEUE_EVENT `neutral.route.1130.s2`，不会继续D epilogue。
- 四 Ending=`neutral.ending.a/b/c/d`，均要求 root.completed + root专用终局flag + route.neutral，**没有日期自动弹结局**。A平凡之日/B远方的咖啡馆/C沸点依旧/D商业大亨。

## 逐文件覆盖：当前稿，而非旧施工包

| 当前DOCX | 段数 | 根 | 已编译/保留 | 阻塞/未完成 |
|---|---:|---|---|---|
| MA70.10.07-S1.docx | 48 | oct07.s1 | 所有原配置保留；源审计 | 本模块不重写首日链 |
| MA70.10.08-S1.docx | 86 | neutral.1008.s1 | 采购、A/B/C、语言成败、相识与+2/+1根尾 | 采购日常未定交daily |
| MA70.10.10-S1.docx | 77 | neutral.1010.s1 | 智慧/语言成败、筹备事实、+1/+2、排队S2 | 成本降低值未定；两天后与10/13矛盾 |
| MA70.10.10-S2.docx | 114 | neutral.1010.s2 | 接待/拒绝、讨论成败、赏识、真实相识 | 无额外虚构黑咖啡任务；时间按依赖排队 |
| MA70.10.13-S1.docx | 152 | neutral.1013.s2 | 促销互斥图、两次服务等待、条件附加、C13完成门禁 | 正常路径开放；仅附加B智慧≥3/≥2冲突禁选；收益数值未定不猜 |
| MA70.10.18-S1.docx | 97 | neutral.1018.s1 | 实际成绩接口、推荐/广告互斥、拒绝广告、推荐事实 | 正常高低品质路径开放；仅未知广告费的付费选项禁选 |
| MA70.10.27-S1.docx | 185 | neutral.1027.s1 | 全文/棋局互斥/好感/专属话题事实 | 专属话题库由daily消费 |
| MA70.10.30-S1.docx | 107 | neutral.1030.s1 | 全文/好感入场/花束选项/梦想及话题锁定 | 实际主题池筛选归daily |
| 入线MA70.11.11-S1(中立结局A）.docx | 285 | neutral.route.1111.s1 | 全文、Queue-only、根尾条件A | 公爵宅/新住处专用图pending；中央入口由教会 |
| 入线MA70.11.11-S2.docx | 142 | neutral.route.1111.s2 | 全文、Queue-only、继续线完成 | 中央入口/选路由教会 |
| 入线MA70.11.12-S1.docx | 77 | neutral.route.1112.s1 | 观察前置、入会/拒绝互斥、入会事实 | 金钱/营收/客流加成未知，不猜 |
| 入线MA70.11.18-S1.docx | 175 | neutral.route.1118.s1 | 正常庇护B开放；事件及选项好感≥3；其余图隔离 | 仅拒绝/贿赂分别禁选；低好感不进入，无整根施工false门禁 |
| 入线MA70.11.22-S1包含中立结局B.docx | 220 | neutral.route.1122.s1 | 条件段隔离、留下End、离开B | 真庇护完成后正常可入；贿赂分支仍关闭；信件为叙述非Letter动画 |
| 入线MA70.11.29-S1.docx | 181 | neutral.route.1129.s1 | 全文、委员事实/前置 | 整根blockedBy；营收标准/周期/provider未定 |
| 入线MA70.11.30-S1中立结局D商业大亨.docx | 226 | neutral.route.1130.s1 | 全文、语言≥5、失败End→C、成功D | 上游1129阻塞；协会/市政厅专用图pending |
| 入线MA70.11.30-S2中立结局C.docx | 133 | neutral.route.1130.s2 | 全文、未入会/真谈判失败OR、互斥回顾、C | 已入会但营收不足暂不开放，不把未完成1129当失败 |

“全文”指现行 prose 有图内行或明确结构/终局标题用途，**不是承诺所有特效指令、专用原画、经济玩法均已实现**。全部raw锚点可供下一Agent逐段继续。

## 阻塞解除步骤：不得删 note 就算完成

### A. 促销真实咖啡交互：正常路径已开放

2026-10-19 主集成明确通知正在实现 domain+bridge 的**真实 any 产品支持**，并授权内容解除旧通用品类阻塞。现输出 `serve:any:60:1` 两个真实 Gameplay 步骤，Event无blockedBy、无false施工前置。any是主集成负责的真实选择协议，不是虚构catalog产品，不强制blackcoffee。

1. first_service之前不显示端上/不错的结果台词。
2. activity在分工选择后等待实际服务。
3. 两次实际服务证据均满足，才在根尾结算root.completed、对应Choice奖励，以及 **QUEUE_EVENT revolution.1013.s2**。
4. 排队Result和完成Result使用相同两个ownerType=result/BOOL_EQ gameplay guard，缺少任何证据不排队；取消也不结算。
5. 普通A、智慧B成败、语言C成败均可完成；有赏识时附加A成败或附加C也可完成。

count=1两次是**有边界的实际服务段预设**，非原稿规定杯数，也非整场经营活动已回接。Normal=60/Excellent=90是允许的**实现评分预设，非作者数字**；此模块用60。主集成负责真实any协议/实体交付/评分/owner清理的运行时验证；本worker本轮只执行纯编译测试，没有声称已验证新domain实现。

附加B仍因P101智慧≥3/P112智慧≥2而禁选；其余路径不被拖成根级阻塞。该项在_audit.blockedBy明确 `scope=choice`、choiceId、conditionKey、sourceAnchors，状态为partial-branches-blocked。裁定后补真实InlineCheck及成功+2，不能把blocked布尔当检定。经营收益提升的未知数值仅审计，不阻塞可表达的剧情，不伪造钱或成长。

### B. 记者真实品质：正常路径已开放

现输出 `taste:any:60:1`；Event无blockedBy、无false施工前置。主集成授权的真实any服务完成后按实际品质返回true/false；高品质仅走推荐，低品质走广告咨询并可拒绝，两者均正常根尾完成。未发生真实交付则仍停留在Gameplay，不自动获得推荐/完成标记。

`NarrativeCafeGameplayBridge` 已采用owner-scoped `CafeWorld.TryBeginNarrativeService`，实际物理receipt才完成；taste低分是false而非取消，支持闭店/营业前服务且不重开店/跳日/挤走普通订单。新增any能力由主集成提供并验证。原稿下午仍按EventDate.noon映射AfterBusiness，不偷偷改日历/经营。

P65-P68无广告价格，**只禁用advert.a付费选项**，advert.b拒绝可正常完成；不产生MONEY_REMOVE、免费广告发布或未知宣传加成。推荐事实交下一期新闻消费。_audit该未知收费阻塞明确标为scope=choice，而非整个事件阻塞。原画/音效等pending不算此两根施工阻塞。

### C. 11/18：正常皇女庇护已开放，拒绝/贿赂仅选项级关闭

按主集成明确指示，`neutral.route.1118.s1` 不再输出eventProperty.blockedBy或false施工前置。事件正常条件为：中立线继续根1111.s2已完成、route.neutral且其他路线旗标为false、当前根未完成，另加 **BAR_GTE alienor.affection 3**。B选项也有相同数值门槛。

这是将原稿P066/P081的**可用庇护选项条件**提升为当前有边界实现的入口保护，防止低好感时进入所有可选项都禁用的事件；在_audit.pending明确属于主集成授权的安全范围裁定，不假称原稿本来限制整根事件。好感不足或数值provider不可用时fail closed，不自动庇护/不伪造失败结局。

正常路径：实际选择 `neutral.route.1118.s1.choice.investigation.b` → 真实播放P082-P106庇护及共同尾声 → **根尾**置 `neutral.investigation.shelter=true` 和 `story.neutral.route.1118.s1.completed=true`。两项Result均有choice:B与bar:alienor.affection:ge:3守卫，shelter属于B的resultOwner；仅有好感但没有接受选择不会成功，取消也不结算。

- A拒绝：仍因end_if4缺对应稿/终局单独禁选，保留 `blocked.neutral.missing_end_if4`。
- C贿赂：金额/实际扣款/失败终局未定，仍单独禁选，保留 `blocked.neutral.investigation_bribe`；不扣钱、不产bribed。
- _audit.status=partial-branches-blocked，两个blockedBy项均明确scope=choice；新增 `resolvedNormalPath`，含实际choiceId、fact、原稿P066/P081/P107、入口数值条件及低好感关闭原因。不是根级construction-blocked，也不把美术pending当施工门禁。

11/22继续要求真实1118.completed与shelter。已用纯图模型串联验证 **1118庇护→1122离开→结局B**，以及 **1118庇护→1122留下→未入会→1130.s2结局C**；没有绕过实际前置、填贿赂事实或使用钱包推导营收。未来补真实bribed时需增加完整OR组，而不是简单删除shelter gate。

### D. 11/29营收与C/D

需明确营收量纲/周期/阈值/真实provider；钱包余额不是营收，not:1129.completed不是营收不足。因此11/29 blocked，不会凭空成委员；D完整图依赖真实委员，正式暂不可达。C仅未入会或真谈判失败可入。`neutral.revenue.below_standard`只声明不生产，规则确认后补真实来源及C第三OR组。

## 结算、经营与演出边界

- Choice.resultOwnerId对应根尾Result；成功检定奖励还有ownerType=result/BOOL_EQ guard，不是点击即提交。
- 所有guard在结算前冻结的行为依赖主分支既有NCS；不另造状态/存档。
- 不输出TRAIN_ATTRIBUTE/MONEY_ADD/MONEY_REMOVE，不改玩家属性，不复制money/calendar/AP，不猜成本/收益/协会奖励。
- 所有编译根onceOnly=true、blocksSave=true。施工根除blockedBy还加默认false条件，旧入口不可误播。
- 局部分支仅choice/check/gameplay guard，不伪造持久选项事实。
- 店内普通页显式scene.cafe，没有经营楼层底图。市场/圣徒街用登记key。公爵宅/协会/市政厅/新住处/小镇缺专用图，显式已登记**纯黑幕** `church.execution.black` staging，并在锚点记录art pending；不以俱乐部或旧咖啡馆冒充。该key当前是纯色非处刑CG，后续主集成可提供中性共享black key。
- 五个generic Object仅未知声线/记者/监察官员/官方代表/送货伙计，空portrait，保留真实角色名，不冒用具名角色肖像。
- 音效/BGM/镜头/立绘进退/时间字幕只留source anchors，未声称均实现；11/22信件目前为叙述行，尚非LetterReveal/Read。时段/场景切换不在本模块偷偷推进日历。

## 实际测试与主集成下一步

2026-10-19实际执行：

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
$env:PYTHONIOENCODING='utf-8'
python -m unittest discover -s Tools/Narrative/Content -p test_neutral.py -v
```

**27 tests passed**。覆盖当前16文件/2305锚点、源漂移fail closed、确定性/无写操作、实际Excel头、唯一ID/引用、全部瞬态guard引用、指令不播成台词、oct07保留、1008互斥及相识、1010赏识与拒绝、促销双Gameplay证据/正常角色及附加路径、真实根尾C13队列与当前革命Immediate无日期入口、仅选项级阻塞、记者高低品质互斥/付费禁用、1027/1030奖励、Queue-only分流、Ending完成守门、1118事件/选项好感入口、无接受选择/取消不结算、庇护→1122→B或未入会C整链、1122提前End、1130失败End→C、C入口OR与非伪造营收、已登记背景/空肖像。

GraphModel只为测试，不是运行时；促销/记者测试不再使用inspect_blocked_plan，正常图直接测试等待/成败/取消/根尾队列。11/18庇护测试也不再使用inspect_blocked_plan；真实入口条件有单独好感缺失/不足/达标及前置/互斥旗标回归。测试证据不是向runtime自动填写成功。C13目标通过只读调用当前革命编译器核对，不造占位Event。

后续顺序：

1. 对齐中央11/11队列和全部跨模块事实；按已授权的serve:any/taste:any完成真实domain+bridge支持与实测。本内容已开放，不再保留旧通用品类false gate。
2. 主Agent合并源Excel，保护非本模块根与market MapPosition，去重全局ConditionKey/Bar；剩余阻塞保留，不冒充全量可玩。
3. 官方Luban strict及实际NCS验证；核查stepType/gameplayKey/源字段。
4. Unity共享PresentationDebugScene互斥/翻页/取消/重播；正式咖啡馆验证可交互、仅释放自身pause、一次性根尾结算。
5. 验证地图/晨午夜入口、C13、宿舍日终不可绕过、取消/存读档不误完成，特别覆盖11/22留下不进B、11/30失败不进D。
6. 仅11/29整根继续等待营收规则裁定；智慧冲突/付费广告/拒绝end_if4/贿赂仍为各自选项级待办。主Agent本次最终合并后运行实际Unity/经营/存档测试，不凭27项纯测试声称全线实机验收。
