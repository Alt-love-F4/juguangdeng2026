# 窗口 C · 革命联合后续工程交接

日期：2026-10-16。**Spec交付完毕；未开始游戏实现。** 三个有正文事件有局部阻塞、均可先做独立内容；0个当前可不经依赖处理直接正式发布。另1份空稿不施工。只产出本目录 `spec.md`、`source-map.md`、`handoff.md`。

## 1. 给工程的顺序与放行门

|顺序|先做的独立工作|阻塞内容隔离|正式放行门|
|---|---|---|---|
|1 / C13|68段源映射、夜景/阿黛尔、三选一互斥回复与共同结尾、根结果+1结构；沙盒三路径|密信段的未定属性/选项条件；A正式续接/恢复；正式好感key|C-B01、02、05；C-G01/04必要能力；V13全部通过|
|2 / C28|157段全文、公共旧账簿/身世谈话、三选一和尾声、C13完成事实闪回；沙盒直接注入历史拥有真假做标记预览|历史学专精真实来源、好感key；眼部遮罩效果可先占位|C-B05、06；C-G01/06/07；V28全部通过；不要求C13必已完成|
|3 / C14前半|清晨入口、c1、信件P017↔P018精确续接、索雷尔匿名→姓氏、c2与wisdom4、c4与perception6、c5|正式数值目标未定，先只看源增量账单；不写正式状态|本段可独立预览，不因此允许整根完成|
|4 / C14后半|wisdom5独立正文、复合成功独立正文、公共尾声、情报/联系事实设计|P191嵌套关系与倾向读取时点不选边；仅明确标注的隔离夹具可预览两候选|C-B03、04、05；C-G01/02/03/05；B的真实前置映射确认；V14全部通过|
|不施工 / C29|只登记空正文与故事D80大纲|任何对白、检定、暗道/合作结果、完成事实|先有实际原稿；不可将D80概述当可施工文案|

不要为赶进度将局部阻塞翻译成“失败恒false”“默认成功”“不加奖励”或“暂时所有选项一样”。上表允许文本预览与支线隔离施工，不允许丢掉原条件后正式出包。不要求等待其他窗所有事件完成；只等本根确实用到的上游/公共依赖。

## 2. 当前能力核对（静态事实，不是验收）

|能力/数据|读到的当前事实与路径|结论|
|---|---|---|
|日程入口|`Assets/Scripts/Narrative/NarrativeController.Entries.cs` 的ProcessEntry/EntryTimeSlot；MorningBeforeMap→Morning、AfterBusiness→Noon、BeforeNight→Night；`DayFlowController.cs`在经营后与结束当天接入|已核对，可复用；不能因为C13背景夜景把AfterBusiness误当BeforeNight|
|安全续接|`NarrativeController.cs` ApplyResult QueueEvent / PreflightResults：QUEUE_EVENT仅允许目标声明Immediate；条件在EventCanStart重查|已核对，C13声明Immediate与此一致；A成功后安全续接，不用root代码硬播|
|一次性根提交|同文件CompleteEvent汇总根Result与selectedChoices的resultOwner，完成后标happened；Inline snapshots另有独立字典|已核对已有延迟Choice结算；**尚非**本批多页分支/条件奖励完整实现|
|同根多选择分支|SelectChoice按组保留选中ID；PageCondition在`NarrativeRuntime.cs`绑定`controller.State.GetCondition`；Presenter.PageVisible只读bool，pageBarConds非空直接抛异常|缺口：未见同会话选择/检定对下一页的局部门控；不得为了门控提前SET_FLAG到正式状态|
|自动属性段|`NarrativeController.InlineChecks.cs`缓存每个checkId一次；Inline禁止success/failure事件与结果；Presenter PassiveCheck失败整页跳过|已核对，复用纯属性段；不能直接表达“属性AND派系数值+条件奖励”|
|玩家属性真值|`PlayerSessionCoordinator.cs` Bind/ReadAttribute连到`PlayerRuntime.TryReadAttribute`；`Player.xlsx` PlayerAttribute B5:C8|已核对：language/courage/wisdom/perception，不读Details面板占位数值|
|关系/倾向数据|Narrative.xlsx Bar B5:S22：样例npc关系+world/calendar/money/AP+四属性；没有阿黛尔好感、四倾向或革命信任|缺失于所核对正式表；不能声称项目任意角落绝无相关设计。C-B05要求整合权威定义|
|历史学|PlayerRuntime.TryReadSpecialization接受专精ConditionKey；当前专精仅letter.literature/politics/religion/science，Text“历史与官房学”绑定politics|读取能力存在，所需身份映射未确认；不能偷换。C-B06|
|信件|ConversationPage有LetterReveal/LetterRead+readingKey；`NarrativePresenter.LetterSteps.cs`与`FirstDayLetterPanel.Generic.cs`、`LetterEventPlayback.cs`已经在工作树|已核对代码/表存在；最终复核时工程报告S2主链及通用信件断言通过、收尾仍在进行；这是他窗报告，不是C14或本窗口实测|
|角色/名牌|Object当前有adele/player，没有sorel；ConversationLine只有speakerKey，没有逐行speakerNameOverride；Presenter用CharacterCatalog统一解析名称|缺少已核对的局部匿名/揭示名牌字段，C-G05；不能用两个NPC代演|
|画面资源|`Assets/Resources/Dialogue/NarrativeStageVisuals.asset`已有oct07.cafe.day/night；阿黛尔资源目录当前仅neutral.png；Characters目录未见sorel|已核对限定路径；可复用/占位，不声称全工程无图|
|效果|`PresentationEffectIds.cs`已列背景平移、立绘淡入淡出/点头、轻重震屏；`IPresentationEffectPlayer`有镜头/淡幕接口|复用现有通道；眼部遮框、仅对话框震动、原位名牌覆盖未证实支持，集中C-G07|

**报告状态（最终复核更新）**：`Docs/superpowers/reports/2026-10-15-october07-acceptance.md`在本窗口工作期间更新，最后读到文件修改时间2026-10-16 00:47:02：包二S1已报告验收；包三S2正式链路已报告通过，知觉1/2、信件精确续接、最终两事实及day3正式日结均有工程实跑记录，最终中断/预览回归收尾中；动态视口重新fit和chapter自然结束pause lease释放仍有待补回归。`2026-10-15-october07-content-map.md`第6–7节的“待parent”是当时配表交接状态，运行进展以较新的acceptance为准。该报告仍注明UTF runner禁用，不是完整UTF通过。本窗口没有复跑，不能将此回报等同本批C13/C14/C28已验收。

## 3. 跨窗口输入与输出

### 3.1 输入依赖

|稳定事实 / 依赖|生产方|读方与位置|值与用途|来源/整合规则|
|---|---|---|---|---|
|`story.oct07.s2.completed`|已在工程接入的oct07.s2|C13/C14/C28根连续性守卫|true：确已完整收首信|C13-P025–027；C14-P006–008/P051；C28-P138；沿已有事实，不重做10月7日|
|`story.oct09.meeting.known`|既有oct07.s2|仅解释与B的前情关系|true只表示知道约定，不是赴约/得到帮助|不得替代下面的rose完成|
|`story.rose.1009.s1.completed`（拟约定）|B的MA10.09-S1|C14根|true：与老爷实际交谈过|B原稿P065–070、C14-P212；只读，不由C写入；B若已用别名由总控统一实际映射|
|`story.neutral.1013.s2.completed`（候选待A确认）|A的中立同名重新开业篇|C13 root及后续恢复|true并且day8：活动已经真实结束|中立P138–153与C13-P002；旧10.13-S1别名不由C消歧；此事实不等于革命C13完成|
|`story.revolution.1013.s2.completed`|本窗C13|C28-P091|true才播放P092–098闪回|正文引用C13-P036，消歧证据明确；只控制片段，不控制整个C28|
|历史学专精权威ConditionKey（未定）|玩家知识/专精设计，不由C授予|C28-P035|真实拥有bool|现有politics名“历史与官房学”仅相近，C-B06|
|阿黛尔好感、四倾向、革命信任正式Bar定义（未定）|统一数值设计/工程整合|三根最终结果及C14-P191|源文增量/阈值确定，物理键/初值/范围未确认|不使用样例npc关系/布尔revolution，也不由各窗各造一份数据|

### 3.2 输出事实（拟新增，不是已配表）

下表所有bool初始false且onlySetTrue=true；只在根最终成功提交，取消/预览不写。各组排他性由根选择与结果验证保证，不能在取消后残留旧分支true。

|事实|生产节点|读取方/语义|禁止误用|
|---|---|---|---|
|`story.revolution.1013.s2.completed`|C13全篇完成|C28闪回|不是neutral同名完成，不是已合作|
|`story.revolution.1014.s1.completed`|C14-P219读完|总控/后续查询已会谈|不等于复合成功或加入组织|
|`story.revolution.1014.s1.c4.1.selected`|C14选择c4.1且全篇完成|未来内容知道中立表达|不自动当“反对革命”|
|`story.revolution.1014.s1.c4.2.selected`|同组c4.2|改良表达证据|不自动加入新玫瑰|
|`story.revolution.1014.s1.c4.3.selected`|同组c4.3|支持人民决定统治方式的原话证据|不自动成为革命成员/与玛丽合作|
|`story.revolution.1014.s1.c4.4.selected`|同组c4.4|秩序表达证据|不自动报告教会|
|`story.revolution.1014.s1.c5.1.selected`|C14选择c5.1且全篇完成|承诺不主动出卖顾客|不等于已保护某名具体客人|
|`story.revolution.1014.s1.c5.2.selected`|同组c5.2|保留周旋立场|不当作同意出卖或自动失败|
|`story.revolution.1014.s1.c5.3.selected`|同组c5.3|合法调查配合立场|不生成实际举报事件|
|`story.revolution.1014.s1.intel.sorel_dissident.known`|复合成功P199且全根完成|知道索雷尔属于反对现有政治秩序的某组织|不公开医生身份/正式组织名|
|`story.revolution.1014.s1.intel.cafe_network.known`|复合成功P200且全根完成|知道咖啡馆和神秘组织的关系|不解锁暗道|
|`story.revolution.1014.s1.contact_drop.known`|公共P209且全根完成|知道后门灯架投信方法|不创建寄信行动/AP成本/实体信件|
|`story.revolution.1028.s1.completed`|C28全篇完成|已听身世谈话|不是11月7日要求的“同意合作”|

C29故事D80的“此前明确站在革命联合/普罗大众一侧”可将上述c4.3/c5.*作为候选证据，但当前没有正文定义组合规则；不给C29写OR/AND、不消费这些事实生成剧情。C13和C28的安慰选择没有原文后续数值差异，只留本根历史，不另造“父女关系良好”等全局条件。

### 3.3 数值语义登记（等待正式绑定，不给猜测key）

|语义标识（文档内，不是新Bar ID）|读取/写入|确定量|欠缺的生产定义|
|---|---|---|---|
|C-N01 阿黛尔好感|C13-P067；C28选中P108/P118/P126|每根最终+1|正式Bar/关系key、计量方向、上下限、初始值、溢出/封顶规则|
|C-N02 中立倾向|C14-P101|+1|统一派系倾向key与范围/初始值|
|C-N03 新玫瑰会倾向|C14-P111|+1|同上|
|C-N04 革命联合倾向|C14-P123/P157/P164/P172及P191读取|+1/+2/+1/−1；门槛≥2|key、范围/初值；是否能负值、封顶策略及本会话读取时点|
|C-N05 王庭—教会倾向|C14-P131|+1|key、范围/初始值|
|C-N06 革命联合信任|C14-P201|复合成功+1|信任方向、关系/Bar key、范围/初值；不是C-N04|

这些是阻塞不是让工程填默认0。正式表缺定义时用“未就绪/未知”拒绝正式根启动或结果预检，不能默默丢弃+1/-1。沙盒可以显式给测试输入，例如T0=1，仅是测试夹具，不传播为新游戏初始值。

## 4. 必要公共能力需求（同一工程入口统一补）

不重造调度器、脚本语言、存档层或新的剧情系统；以下只列当前NCS边界缺口和验收契约，不在本轮改schema/代码。

|编号|需求与触发来源|现有复用点/最小接口契约|验证与不允许的捷径|
|---|---|---|---|
|C-G01|同一根里多次选择、选后互斥正文、OR共享页和选择后续接；C13 c1、C14 c2/c4/c5、C28 c1|复用Controller.selectedChoices、Choice.groupKey/resultOwner、ConversationPage与Presenter；给当前根暴露只读“本组所选Choice”和命名Inline结果的页条件查询；只在抵达页时求值，配置可定位分支guard；C28选择必须在闪回之后或跳闪回之后只出现一次，必要时在既有序列支持无重复正文的选择续接步骤|c2.1/.2共同页只存一份；其他回复不串播；选中前不会判掉所有后续；不借正式SET_FLAG门控、不建子根、不在Panel塞章节硬编码|
|C-G02|C14-P191语言AND倾向，以及P199–201条件结果|复用真实AbilityReader、已确认Bar读数、现有检定快照和CompleteEvent batch；支持同根复合快照、基于快照的最终结果owner收集；Inline本身仍不得填现行禁止的successResultOwnerId，工程明确适配位置|成功只记一次情报/信任；失败不记；取消不写；缺数据报依赖错误；pageBarConds目前会抛异常，不能写个字符串便宣称支持|
|C-G03|若C-B04批准“含本会话增量”，P191需读投影值|复用已选Choice结果形成只读待提交增量视图；读取=真实基值+批准范围内已选Δ；根结束才应用实际Result；需明确上下限与两笔增量顺序|中途外部订阅/存档不观察到临时值；退出不需要倒扣；不使用先发奖励再撤销方案；若批准仅T0则不开发这项投影，只固定读取时点|
|C-G04|C13重开业完成后直接续接、取消后不丢章|复用QUEUE_EVENT、安全候选、根条件/一次性和既有快照；A在唯一真实完成点提交候选；A已完成/C13未完成的同日恢复需能重新构造待续接；挡住提前结束当天但不重结算A|A取消不触发；A成功一次C13一次；C13取消后重试；day9不补播；预览/入队不置完成。缺少该恢复契约不得只写一次QueueEvent就结束|
|C-G05|C14-P026–045匿名，P047改姓氏；缺索雷尔图|同一Object/portrait owner，行或页级可配置显示名覆盖，来源按Text；保留原“？？”/“？？？”差异；复用CharacterCatalog/NarrativePresentationDataAdapter|改名不重进场、不把别的正式角色当索雷尔、不提前显示医生/全名；数据层仍同一身份|
|C-G06|C28历史专精片段与成功提示|真实专精ConditionKey经PlayerRuntime/同根快照读取；复用pageCondKeys bool门控和现有提示样式，key由C-B06提供|未拥有跳P044；未知key报错不视为false；不映射智慧或自动授予专精|
|C-G07|C14-P088眼部强调/P135背景平移/P187仅对话框震动；C28-P092遮眼闪回、P149淡弱BGM|优先复用PresentationEffectLibrary、PlayCameraCue/PlayFade/背景通道；在现有适配边界把需补的局部目标/遮框参数数据化，退出复位；P088“方法”只取明确眼部强调意图|不把对话框震动退化为全画面重震，不凭空加白闪/CG；未完成效果可明示预览占位；精确验收不因占位而通过|

C-G01与G02是**正式逻辑门**；G07属于**演出能力/资源门**，可先静态占位进行逻辑预览，但“精确原文演出验收”仍未通过。现有Inline缓存与CompleteEvent延迟结果已经能复用，不以这些需求为由重写整个NCS。

## 5. 真正阻塞与未决项

|编号|阻塞与来源|影响范围|需要的最小结论；其余继续做|
|---|---|---|---|
|C-B01|C13-P002的重开业实际生产者；A源中立P138–153，旧10.10-S2-P071又指S1别名|C13正式入口/恢复|A/总控确认实际根与真实完成回调；首选`neutral.1013.s2`已给源证据，不接受裸MA70.10.13-S2查找。正文/选项先施工|
|C-B02|故事D64说密信段根据属性鉴定/选项触发，C13-P023–036没有门槛或开关|C13正式密信段|确认以DOCX无额外条件为最终稿，或补准确条件及覆盖范围；不能凭空指定知觉2/6，不能拿后出现的c1倒控前文。其他正文先施工|
|C-B03|C14-P174的智慧5到P191之间无收束，唯一P202收束|C14复合情报分支|确认P191是否被智慧5包裹。低智慧+语言/倾向达标是决定性反例；不影响各段原文入库|
|C-B04|C14先标派系增量后检定≥2，但产品要求根末提交|C14复合检定及读数提示|确认读T0还是T0+当前已选Δ；时点/上下限一致。不能在正式执行时随便选择一种|
|C-B05|当前正式Bar未定义阿黛尔好感、四派系倾向、革命信任；原稿多处明确奖励|三根正式结算、C14倾向门|总控统一数值key/关系方向/范围/初值/负值和封顶策略；保留所有原增量，不做0填空/缩放/混用样例|
|C-B06|C28-P035历史学专精与现有“历史与官房学”只名称相近|C28历史条件段|确认等价可复用`specialization.player.letter.politics`，或提供确切独立知识ID/获取源；本窗不决定，不加新获取方式|
|C-B07|C29原稿0非空段；故事D80仅大纲|全部C29施工|需正文、选择/检定/结果后另写Spec；当前不生成任何完成态|
|C-B08|故事D89写“10.28达成合作”，C28正文无合作选项|11月7日等下游条件；不阻塞C28独立内容|总控核对真实合作事件/选择ID，不能拿C28完成填空；C29也还是空稿，不能预设它输出合作|

额外非阻塞校对：C14-P088“立绘方法”、P136“提供这些顾客”残句，C13-P018“打交到”/P065“承载者”，C28-P036“阿戴尔”/P138“哪些”都保持原样，可让作者后续校对；本轮不擅改正文。故事D79与C28稿没有完全对应的质问段，按已写正文交付，不补纲要中的台词。C14收到钱只作叙事，金额缺失不构成需要制作一个奖励的理由。

## 6. 占位与资源替换清单

所有占位只用于验证源意图与流程；未查到不等于全项目没有。既有舞台别名只复用，不改原图片、不做新美术。资源替换必须定位回本根page/line/source段。

|编号|用途 / 源段|已核对候选或预览默认|替换入口 / 验收|
|---|---|---|---|
|C-R01|夜咖啡馆：C13-P003；C28-P002|已存在`oct07.cafe.night`（同源咖啡馆图+蓝灰tint）|两根首page.bgOverride；`Assets/Resources/Dialogue/NarrativeStageVisuals.asset`中的正式替换映射，不写不存在资源路径|
|C-R02|清晨咖啡馆：C14-P002|已存在`oct07.cafe.day`先预览；不假称是独立黎明图|C14 opening.bgOverride；以后美术提供清晨变体再换|
|C-R03|阿黛尔全部表情：C13-P007等、C28-P034/P084/P125等|已见`Assets/Resources/Characters/adele/neutral.png`；开心/平静/思索/迷惑/尴尬/悲伤保留语义，缺图暂同角色基准回退|源行emote→CharacterCatalog/现有表情映射；不每句回neutral；多表情段a/b/c原位换|
|C-R04|索雷尔工人装与表情：C14-P023、P026–214|当前Resources/Characters未见sorel；正式身份先登记，预览仅名牌/正文、空肖像并缺资源诊断，不拿其他人物替代|Object/角色资源目录由工程核对后绑定；服装与平静/思索/开心/迷惑/兴奋/尴尬差分替换，不泄露身份|
|C-R05|门铃/脚步：C14-P023；拍打声P187；夜间咖啡馆环境音C28-P003|未在本轮定点核对到准确音频key；静音占位并日志列缺口，不编路径|相应行sfxKey与现有音频通道；门铃+脚步需要顺序组合时在既有效果配置处理，无额外对白点击；P149只淡出现有BGM|
|C-R06|C14-P088强调索雷尔眼睛；P135背景放大左至右平移；P187对话框震动|背景平移已有fx_background_pan可复用；具体眼部/对话框目标未核实，先原位静态占位|原文后一显示行P089/P136/P188的效果边界及PresentationEffectLibrary；P088不反复缩放，P135不摇立绘/UI，P187不震整个背景；C-G07|
|C-R07|C28-P092放大阿黛尔+框挡眼睛，P093–098闪回|只复用阿黛尔图；无现成遮眼预设证据，静态占位并明确未实现精确效果|flashback页enter/exit；P099选择出现前恢复正常stage，取消同样清理；不能用黑色全屏盖住全文|
|C-R08|C14-P007信图示与P017读信纸|复用FirstDayLetterPanel现有Prefab bindings/通用LetterEventPlayback；不编新信封路径|LetterReveal/LetterRead读取`revolution.1014.s1.letter`；无新游戏业务，P017仅一次阅读，返回P018|
|C-R09|C13-P062退场/P066暗；C14-P215渐隐；C28-P149退场/P156暗|已有PortraitFadeOut与Fade接口；时长沿现有效果预设，缺适配时仍列演出未验收|对应源段之间效果步骤，不复制前后台词作为空载体；根结果等末尾渐暗完成才提交|

原稿正文“动作描写”如喝茶、拿账簿、路过一家人只显示文字，不额外做人群立绘/道具/背景；本批没有显式新增CG任务。

## 7. 配置映射与放行核对

|配置部位|本批要求|拒收情形|
|---|---|---|
|Object|复用player/adele；sorel先查重；匿名显示保持同一身份|用其他NPC立绘代演；在正文开头提前显示医生/全名|
|Event / SetupParams|每根一个主Conversation演出；root onceOnly+blocksSave；入口见spec|按分支拆多次根结算、把C29建假完成事件|
|EventDate / Condition|C14 day9 morning，C28 day23 night；C13无日期自动行而是受day8守卫的安全续接|因为S1就都排上午；给C13另开BeforeNight/AfterBusiness双触发；日期绕过真实前置|
|ConversationPage / Line|source-map逐段映射，选择绑定有正文页末；root内局部条件/分支由G01实现|空选项页失效；反向提前判定；选项全部回应都播；pageBarConds未支持却写入|
|AbilityCheck / Condition|Inline检查wisdom4/perception6/wisdom5；c2勇气是解锁门；复合门用批准的真实属性+Bar|把c4.3因知觉不足锁掉；Inline配置结果owner；把派系数值当玩家第五属性|
|ReadingPage / ReadingLine / Text|P017仅信纸，逐字保留，无外加称呼签名；文本key见source-map|信件正文又复制到对话，读完误回P019/解密前，打开即完成根|
|Choice / Result|c4/c5互斥owner，最终一次提交；C13公共+1与C28仅选中支+1区分|C28根再多加+1，取消后已有倾向/信任/情报，复合失败仍给信任|
|ConditionKey|本窗拟输出13个bool见§3.2；初始false/onlySetTrue；C13与neutral不撞名|完成态冒充合作/触发态，选项两支同时true，缺知识key默认为false|
|Bar / 专精|绑定统一已确认定义及真实来源；原文+1/+2/−1和门槛原值不变|新增无依据初始值/范围，把缺值硬填0或用样例/布尔替代|
|演出/调试来源|正式配置key能追溯到source-map，GM/共享场景加载真实配置；从段落预览需重建依赖舞台|在C#复制一份硬编码稿、另建临时Scene/Canvas、以截图通过代替逻辑验收|

目前三个新root未在Narrative.xlsx中发现同名占用；正式写表前再查，使用现有官方Excel/schema/生成链路，禁止新建分片时绕过合并白名单。此条是工程交接约束，本窗口没有执行配表/生成。

## 8. 分支与最短验收矩阵（全部未运行）

### V0 · 通用边界

|用例|输入/操作|预期|
|---|---|---|
|V0-01|事件前一天/当天/后一天，已完成true/false，锁true/false|只匹配当天、正确入口和前置且未完成未锁时启动，不延期补播|
|V0-02|正文/信件/选择/最终渐暗中取消或卸载|无结果、无完成事实、无日程推进；自有锁/特效/音频释放；在正确安全入口可重试|
|V0-03|根正常结束、重复完成回调、读回根后安全快照|只提交一次；不多收AP/多加好感；选项事实排他；原日程回调一次|
|V0-04|沙盒选全部分支、读信、注入测试属性/知识|正式状态/AP/存档/已看标记完全不变；缺正式依赖仍显示未就绪|
|V0-05|共享PresentationDebugScene启动真实key预览；Space/Enter、Esc、F9 timeScale=0|正文正确续接；按键不穿透信件；取消清场；不另建测试布局|
|V0-06|读数未知/未就绪与真实低于阈值对比|前者明确依赖错误，后者按原稿跳过；没有默认0/自动成功|

### V13 · C13

|用例|输入/路径|预期|
|---|---|---|
|V13-01|day8，A活动未完成/被取消；即便普通经营结束|不播C13、不发+1、不强行创建完成|
|V13-02|day8 A真实活动完成+首信完成，安全续接|恰一次C13；不抢A当前对白，不要求再经营一轮/再结束当天|
|V13-03|三次隔离会话分别c1.1/.2/.3|只播P043–045 / P047–049 / P051–053之一；全到P055–065；最终+1而非0/+2/+3|
|V13-04|neutral.1013.s2完成但C13未完，中途取消C13后同日恢复|A不重结算；C13可再进；不可提前结束当天越过必然续接；转day9不补播|
|V13-05|C-B02裁定后，密信条件上下边界或不同选择前置|仅批准范围内显示P023–036；不得改到后置c1评价分支；未裁定前标待验|

### V14 · C14

|用例|输入/路径|预期|
|---|---|---|
|V14-01|day9 MorningBeforeMap，首信与B赴约完成；仅meeting.known的反例|真实前置全满足才播；知道约定不等于赴约；同一天重复入口不重播|
|V14-02|c1两项各跑；P007、P015、信纸末页、P018边界逐点推进|c1无数值结果；P007不可拆；P015后读完整P017；返回P018且不重读/漏段|
|V14-03|courage5与6；c2三项；wisdom3与4|③不足灰显、达标可选；①②同一正文；智慧段独立门，成功P078–080，失败直P082|
|V14-04|c4全部四支；c4.3分别perception5/6|所有选项可选；只有知觉6多P116，c4.3两种都革命倾向+1；其余三支不检查知觉|
|V14-05|c4四支×c5三支=12组完整根|仅选中的两条结果；革命增量矩阵为c4.3行(+3,+2,0)、其余行(+2,+1,−1)；非革命c4另加对应倾向+1；不错误给同根+2信任|
|V14-06|wisdom4/5|P175–190只在5出现；P187效果仅此支；若未决复合作用域尚未裁定，禁止宣称整段已验收|
|V14-07|language2/3 × 倾向1/2 × wisdom4/5|复合门两项AND，包含阈值；智慧4而其余达标用来验证C-B03批准的作用域|
|V14-08|T0=1，Δ=+2；以及T0=2，Δ=−1|若批准T0口径：前者失败后者成功；若批准投影：前者成功后者失败；正式只接受批准的一种，两个口径不要混入同一快照|
|V14-09|复合成功/失败后均读完P219；另在成功显示情报后取消|成功最终两情报+信任1，失败均不写；取消也不写；两完整分支都知道contact_drop且root完成|
|V14-10|各页/信件/匿名切名/多表情行连续推进|索雷尔只一次出场，P045后改名，其他台词/旁白不清场；P215才退场；缺图不由阿黛尔代演|
|V14-11|P204“几枚硬币”和P031/P034制作红茶|不更改钱/物品/品质/AP，不创建制作关卡，不填假金额|

### V28 · C28

|用例|输入/路径|预期|
|---|---|---|
|V28-01|day23 BeforeNight，C13完成false/true各跑|两种都能进根；仅true播放P092–098；完成neutral同名不能替代|
|V28-02|批准的历史学专精有/无；另给智慧高但无专精|只有知识拥有者多P036–043；高智慧不能替代；两支汇P044|
|V28-03|知识2态×革命C13完成2态×c1三项=12条路径|所有路径到P155；只选中回复，最终+1；不会因历史/闪回/三个+1标记叠加|
|V28-04|闪回放大遮眼后继续到选择，另在闪回中取消|恢复正常画面，遮框不遗留；原阿黛尔连续立绘，取消不回写好感|
|V28-05|完成根并查询全局事实/下游|只root和好感；没有“同意合作”、暗道、C29完成、下一日自动完成|

### V29 · 空稿与下游防误接

0正文→0正式Event/Date/完成态；故事D89要求合作时，无合法合作事实应诊断依赖未定义，不用C28完成替代。此检查不阻止已完成C28自己的正常结尾。

## 9. 本窗口实际完成的静态检查

- 原始DOCX只读提取，三篇非空源段为68、219、157，共444；第四篇0段。每份源文SHA256与源映射写在source-map。
- 按原文顺序逐段登记，正文/选项/标题/条件/演出/结果明确区分；多表情按a/b/c拆分约定，只有原条件成功正文才进入对应页。
- 读取故事.xlsx相关日期与分支、人物.xlsx姓名/身份、现有Narrative/Player/Text表和schema/关键代码；新root无当前占用；两个10.13-S2按命名空间隔离。
- 已列明同根选择门控、复合结果、原子最终结算、旧存档/取消恢复的能力差异；未把待开发接口写成已经通过的事实。
- 已执行只读Python文档校验：444个源表行按段号连续且逐字等于DOCX，四份源SHA256一致；所有C-B/C-G/C-R引用均有定义，目录仅三个指定Markdown文件，无未填充占位词或草稿状态残留。
- 文档复核：未填未知金额/初值；未改原文；C13共用+1与C28每支+1互不重复；C14政治选择无隐含禁用；C28没有合作输出；C29不生成完成态。
- **Unity/PlayMode/配置生成/实际分支/存档验收均未运行。** 本轮仅文档静态校验，不用旧工程报告或静态阅读冒充本窗实测。

交给总控：先协调C-B01/02/05以释放C13；并行确认C-B03/04/06，C-B08是后续引用修订而非要求C窗口补剧情。完成后停在规格，不自动提交Git、不启动工程实现。

## 10. Construction Prompt (copy into the engineering Agent window)

```text
You are implementing Window C: the Revolution Union follow-up package in the existing NCS. Do not invent or rewrite story content. First read the complete package:

1. `Docs/superpowers/specs/2026-10-16-content-batches/C-revolution/spec.md` (sole entry point; read it in full)
2. `Docs/superpowers/specs/2026-10-16-content-batches/C-revolution/source-map.md`
3. `Docs/superpowers/specs/2026-10-16-content-batches/C-revolution/handoff.md`
4. Latest `Docs/superpowers/reports/2026-10-15-october07-acceptance.md`
5. Latest `Docs/superpowers/reports/2026-10-15-october07-content-map.md`
6. Project `AGENTS.md` and the current worktree diff.

Scope:
- `revolution.1013.s2` from `D:\桌面\文案\革命联合相关事件\MA70.10.13-S2.docx`, game date 670.10.13 / day 8.
- `revolution.1014.s1` from `D:\桌面\文案\革命联合相关事件\MA70.10.14-S1.docx`, game date 670.10.14 / day 9.
- `revolution.1028.s1` from `D:\桌面\文案\革命联合相关事件\MA70.10.28-S1.docx`, game date 670.10.28 / day 23.
- `MA70.10.29-S1.docx` is empty: record the blocker only; do not create story, completion, cooperation, or ending content.

Work in this order: C13, then the independent C28 body/flashback, then C14. For each event, complete: configure -> official generate -> compile/import -> preview in `Assets/Scenes/PresentationDebugScene.unity` -> test branches/cancel/once/save/day-flow -> fix -> update the latest acceptance record and the C handoff continuation log.

Before configuring, verify cross-batch prerequisites: C13 needs the real completed fact from the neutral October 13 reopening event; C14 needs first-letter completion and the B-window October 9 meeting completion, and `story.oct09.meeting.known` is not a substitute; C28 uses only `story.revolution.1013.s2.completed` for the flashback, never the same-name neutral completion.

Reuse the existing NCS, Narrative/Player/Text schemas, entry scheduler, AbilityReader, Inline checks, generic letter flow, presentation effects, and the shared debug scene. Do not rebuild the scheduler, narrative system, letter system, scene, Canvas, or parallel story fields. Do not overwrite other Agents' changes. Do not hardcode the source paragraphs in C#.

When content, numeric definitions, resources, or dependent gameplay are genuinely missing, record the blocker in the event acceptance record and handoff, then continue independent work. Never fill unknowns with zero, automatic success, fake completion facts, or the wrong relationship/bool key. Treat the following as blockers exactly as documented in handoff: C14 P191 scope and composite-check timing, official affection/faction/trust keys, and the C28 history-specialization mapping.

After each event, re-read the key configuration, generated output, and runtime results. Separate existing engineering evidence from new evidence for this batch, unverified items, and blockers. Do not call the Spec complete until the event itself has been configured and tested; do not perform unrelated refactors, cleanup, rollback, or overwrite.
```

## 11. Continuation Log

- Delivered by specification window: `spec.md` as sole entry point, linked `source-map.md`, and this handoff with construction order, blockers, evidence rules, acceptance matrix, and copyable prompt.
- Construction checkpoint 2026-10-16: C13, C28, and C14 were appended to the existing `Narrative.xlsx`/`Text.xlsx` through `Tools/Narrative/import_revolution.py`. Source SHA/count checks passed: C13 68 paragraphs / 52 dialogue lines / 3 choices; C28 157 / 144 / 3; C14 219 / 171 / 12. C13 and C28/C14 manifests are under `Library/Locus/tmp/C-revolution/`.
- Construction checkpoint details: all three roots are explicitly `onceOnly` and `blocksSave`; all three remain `productionReleased=false` with no numeric results or completion result emitted. C28 history is guarded by unresolved `C-B06`; its flashback reads only `story.revolution.1013.s2.completed`. C14 has the shared Reading structure, anonymous Sorel name keys, and Inline attribute rows for wisdom4/perception6/wisdom5; P191 is still blocked and has no composite result.
- Official generation blocker: `Assets/ConfigTables/GenerateAll.bat` reached Player semantic validation but exited 1 because the pre-existing `rose.1009.s1` row has `eventKind=narrative`, while the current schema permits only `actor,world`. This row was not changed by the C construction and was not corrected by this window. Luban outputs written during the failed run are not acceptance evidence; C13/C28/C14 have not been officially generated as a complete set.
- Unity/runtime status: the connected Editor is editing and bridge-ready; no C-package import, shared `Assets/Scenes/PresentationDebugScene.unity` preview, PlayMode branch/cancel/once/save/day-flow test, or formal acceptance run was claimed. Existing October 7 evidence remains evidence for that earlier batch only.
- Next owner: resolve the unrelated existing Luban validation blocker without overwriting concurrent work, then regenerate and import the complete tables; follow with C13 -> C28 -> C14 preview and V13/V28/V14 matrix. Resolve C-B01/C-B02/C-B05 before formal C13 release; confirm C-B03/C-B04/C-B06 in parallel; keep C-B07/C-B08 blocked without inventing the empty draft or cooperation result.
