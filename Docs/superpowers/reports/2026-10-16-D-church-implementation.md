# D 王庭教会前中段施工交付记录

日期：2026-10-16。当前版本：续做中，首轮 02:43–02:44，续做 02:51 起（+08:00）。以本文件顶部最新交付状态为准；下方前次与首轮记录保留为历史。范围仅 D 批次 `church.1009.s2`、`church.1014.s2`、`church.1020.s1`、`church.1023.s1`、`church.1023.s2`。本记录不把规格、配置存在或静态文本核对写成游戏验收通过。

## 最新续做交付（2026-10-16 17:55 +08:00）

本轮完成 `church.1014.s2` P028闭合信封显示与收起。现在为12步骤（原9页内容拆为10个正文步骤，加LetterReveal/LetterHide），仍51句、6选择，Text和source-map原文不改。P027读完才显示，P029–P035及第一组选项期间保持闭合，第一组选择完成后、P041新话题前正常收起。正式AfterBusiness、C上午信前置、倾向结算仍未接通，blockedBy保留。

实际改动文件与公共能力：

- `Assets/ConfigTables/Defines/schema.xml`：在既有步骤set末尾追加LetterHide；`Assets/Scripts/Narrative/NarrativePresentationDefinitions.cs` 的enum同样末尾追加，旧枚举数值不变；Letter数据新增owner专用dismissStage回调。
- `Assets/Scripts/Narrative/LubanNarrativeLoader.ConversationSteps.cs`：LetterReveal可无readingKey/正文，不构造假信；指定readingKey时继续严格校验，LetterRead仍必须完整Reading引用。LetterHide不能带正文/选项/check/reading。
- `Assets/Scripts/Narrative/NarrativePresenter.LetterSteps.cs`、`NarrativePresenter.cs`：构造0正文的闭合道具快照，保存正常收起回调；遇LetterHide一次调用并失效旧Reveal取消回调，外部停止仍走取消，不早完成根。
- `Assets/Scripts/GameFlow/UI/Presentation/FirstDayLetterPanel.Generic.cs`、`LetterEventPlayback.cs`：增加同owner、仅Reveal的正常收起；先脱离旧完成/取消回调再Stop。不同owner、Read及重复收起不关闭；ReleaseStage仍保留中断取消语义。使用既有prefab直接绑定的闭合图，无新Scene/Canvas/图片资源。
- `Tools/Narrative/configure_church_1014.py`：精确旧值迁移；P027移入letter_intro，原letter_question从P029继续，插入envelope和envelope_hide，后页顺延，候选回读/CAS/备份及幂等检查；同时修为后续不保存没有实际变更的Text工作簿。首次本轮apply仍重序列化了Text字节，但9860个原有cell签名逐项保持；不将字节变化描述成新增正文。
- `Assets/ConfigTables/Data/Narrative/Narrative.xlsx`、官方生成 `Assets/Settings/Resources/Luban/tbncsconversationpage.json` / `tbncsconversationline.json`：新增3个步骤、6个seq迁移及P027页归属调整，无新Reading数据/Date/Result/ConditionKey/Action。
- `Assets/Scripts/GameFlow/UI/Presentation/NarrativeChapterPreview.ChurchGuest.cs`、`NarrativeChapterPreview.cs`：新增PlayChurchGuestConstruction(perception)，只在独立内存配置day9/Immediate播放，正式根不解除门禁。
- `Assets/Scripts/GameFlow/UI/Presentation/Tests/ClosedEnvelopeStepTests.cs`：15个用例；新脚本与Unity自动生成meta配对保留。

实际验证：

|检查|结果与范围|
|---|---|
|官方GenerateAll.bat|17:44:22 exit0，semantic与strict通过；未手改生成物|
|Unity编译/加载|unity_recompile实际compiled、compilation completed、domain_reload completed、bridge ready；最终收敛后Console error0。真实loader读取12步骤成功|
|步骤断言|15/15在Play Mode直接执行NUnit断言通过：0正文Reveal、步骤确认不完成根、一次Hide、迟到回调、取消/Dispose、Loader严格性；不是Unity TestRunner套件|
|全分支Presenter|18/18（知觉1/2×两组三选一）通过；P027→无正文Reveal→P029、Hide→P041、P075顺序正确，show/hide各一次。此项道具adapter为替身|
|共享场景实际UI|知觉1=43行、知觉2=44行，均到P075；每次7次闭合道具状态检查，非modal、无raycast、不显示纸张/取名/签名；调用OpenEnvelope/SkipLetter/ConfirmSignature不能拆信；P041前已收起；P031.a/b顺序保持|
|真实取消|P029时停止保留信封，preview取消，没有隔离happened或完成回调，道具/舞台owner释放|
|真实adapter回归|不同owner拒绝收起；正常重复dismiss/release无额外回调；同owner后续Read不受旧dismiss影响；Release仍取消一次|
|暂停|以上UI/取消/adapter运行前已有baseline=1，结束恢复1，未新增泄漏；不写成pause0|
|静态/幂等|源计划与生成557字段一致；apply后plan为added={}、migrations=[]，原Text9860cell签名保持；无D1014 Reading/Result/Date/Action|

失败/未覆盖：初次步骤断言在Edit Mode触发现有PresentationUI的DontDestroyOnLoad限制，清理了该次唯一未初始化对象后改Play Mode通过；首次UI因别窗占用拒绝未执行，空闲后两次通过。补截图时Editor被并发工作切回editing，截图未取得；不以截图作为本轮证据。正式AfterBusiness/阵营九组合结算/正式存读档仍未验证，客人立绘/放杯与出入场/声音等仍pending。

本轮证据：`Library/Locus/tmp/church-content/20261016-174403-719003-1014-config/manifest.json`、`Library/Locus/tmp/church-content/1014-envelope-verification.json`；运行工具t20=UI两条、t21=Presenter18、t22=取消、t23=真实adapter隔离与Read回归。前次15:20段“1014信封未施工”已由本轮取代。

最新依赖纠正：最终JSON与 `Docs/superpowers/reports/2026-10-16-A01-A04-formal-source-tables.md` 已有 `neutral.1013.s2.result.completed → story.neutral.1013.s2.completed=true`，活动根不再blocked；这解除“没有A活动生产者”的旧缺口，但本窗口未独立跑A自然链。B密见/C上午信仍无完成Result生产者；21条Bar只有NPC/三人物关系、日历/资金/AP/四属性，无正式教会/革命倾向源。因此D1014的整体活动/信件与倾向门禁不能直接删除。

下一施工入口：1014现可用PlayChurchGuestConstruction完整查看闭合信封；剩余独立演出可补客人正式资源与退场、P031放杯。1023继续准备同根局部释放/后备路由，金额/阵营provider/拘押日程未决必须继续报错；具体Ending仅可在真实失败与完整P033后接入。1020完整内容预览与下述已验证成果保持。

## 前次续做交付（2026-10-16 15:20 +08:00，历史）

本轮实际推进 `church.1020.s1`：从只能分段预览，推进为完整根内两尾互斥预览。19页正文及117句/7选择/5Inline保持，新增一个公共终端FadeOut，总计20步骤。正式MapAction、拘押跳日、地图限制、永久结果仍未放行。

实际修改：

- `Assets/Scripts/Narrative/NarrativeController.SelectedChecks.cs` 与 `NarrativeController.Entries.cs`：新增通用 `selectedCheck:choiceId=checkId;choiceId=checkId` 页面门；必须活跃根、映射Choice属于当前根页面组、所有检定已定义且为无分支/无结果Inline、恰好一条映射Choice已选；无选择/多选/坏引用均报错。只读取所选检定并复用Inline快照，`not:`不会把unknown改成失败。没有按church事件ID写剧情特判、没有提前写持久flag。
- `Assets/Scripts/Narrative/Tests/SelectedCheckGuardTests.cs`：新增36个参数用例，覆盖四解释5/6、相反未选值、未选provider不可用、正反/嵌套门共享一次读取、未选/多选/外根选择/未知映射/非Inline错误。文件与Unity生成meta配对保留。
- `Tools/Narrative/configure_church_1020.py`：三页旧dependency迁移为selectedCheck及其反门；加入唯一末尾FadeOut；只从已知旧值迁移，保留其他单元格并执行备份/CAS。默认plan在最终表上返回 `added={}`、`migrations=[]`。
- `Assets/ConfigTables/Data/Narrative/Narrative.xlsx` 与官方生成的 `Assets/Settings/Resources/Luban/tbncsconversationpage.json`：完整源配置21条曾作为中间候选失败，已修正为20条有效步骤；没有改Text、Result、Action、EventDate或schema。
- `Assets/Scripts/GameFlow/UI/Presentation/NarrativeChapterPreview.ChurchRaid.cs` 与 `NarrativeChapterPreview.cs`：新增 `PlayChurchRaidConstruction(language,courage,wisdom,perception,completed)`，沿现有DebugSession使用day15和显式四属性，只有内存配置转换Immediate/去blockedBy。支持完整正文与真实舞台淡出，仍不产出拘押事实、跳日或放行正式入口。

实际验证：

|验证|结果/边界|
|---|---|
|官方生成|15:07:13与修正后15:13:05均exit0，semantic/strict通过；未手改生成物|
|Unity收敛与加载|`unity_recompile`两次返回up_to_date/not_needed/bridge ready，说明自动编译已应用；实际类型/新方法存在。ForceUpdate导入Page JSON后loader成功，1020=20步骤；Console error=0。不声称工具本轮执行了一次新的完整编译|
|NUnit直接断言|逐case SetUp/test/TearDown实际执行36/36通过；不是Unity TestRunner套件|
|完整Presenter路径|24/24通过：合作P020/P021/反抗P022 × 四解释 × 属性5/6，未选属性置相反值；每次只取所选解释快照，P144/P152两尾互斥，恰好一次淡出。此项淡出用回调测试替身|
|共享场景真实UI|新正式预览入口：courage3+language6完整53行到P144；courage4+language5完整37行到P152；实际舞台淡出、completed回调到达、pause均0。追加源锚断言P030有/P027无及P124有/P147无通过，证明第一次失败不强制拘押|
|终端取消|读完P152，在真实overlay淡出期间DialoguePlayback.Stop；没有隔离happened/完成回调，preview退出且pause0，舞台owner释放|
|正式门禁|新载入的正式根day15 QueueMapActionEvent拒绝；仍含D-C04/D-B04，无永久拘押/完成结果|
|正式快照隔离|共享场景NarrativeRuntime.IsReady=false，未执行正式CaptureSnapshot前后比较；不计为已通过。所有运行均为独立DebugSession，未验证真实AP/钱/存档/DayFlow|

失败与修正：第一版两尾各放FadeOut虽通过Luban，但真实loader明确报 `FadeOut must be the final step`；已收敛为互斥正文后唯一公共FadeOut，未修改公共验证器放宽约束。首次UI脚本引用不存在的panel.Options，工具编译失败未启动演出，修为按已接受选择计数推进后复测通过。

证据：`Library/Locus/tmp/church-content/20261016-150650-680102-1020-config/manifest.json`（初次迁移），`20261016-151237-437526-fade-repair/manifest.json`（共用末尾修正）。实际运行工具任务t13=Presenter24、t15=UI双尾、t16=终端取消、t17=源锚/正式拒绝；本轮证据摘要 `Library/Locus/tmp/church-content/1020-full-content-verification.json` 另核对1057个源计划/官方JSON运行字段相等（空单元格按生成规则转空串）。前次runtime摘要的“1020仅片段、尾段未播”已经被本轮完整内容验证取代。

最新上游核查：B1009无正式completed生产者；A活动无真实completed生产者；C1014虽声明completed键但无Result生产者，三根均blocked；两阵营真实bar/provider仍无。B帽子历史与D只交completed合同仍冲突，未擅改。1023金额、资金/倾向/局部释放方式、拘押规则和具体Ending仍是后续门禁。本轮先解除1020内容汇合，不新增正式地图必做，不清任何blockedBy。

续做入口：1020内容现在可由 `NarrativeChapterPreview.PlayChurchRaidConstruction` 完整预览；正式联调需先统一密信来源、拘押/圣徒街范围/日结规则及1023闭环，再配置终态互斥结果与地图Action。1023可继续准备独立的说服/营救内容及局部路由，但未知金额/provider必须显式阻塞；1014闭合信封仅允许显示，不可借旧信正文作占位。

1014 P028后续定点核查（只读、未施工）：现有LetterReveal虽是非modal/非raycast闭合道具，loader/Presenter仍要求Reading引用和至少一行内容；D没有信内正文，不能借oct07阅读数据填充。现有Reveal生命周期只在整个根finally释放，中途ReleaseStage会通知取消。下轮可最小扩展“无正文Reveal”和“同owner正常收起道具”，再按P027→P028→P029拆页及话题切换收起；保持source-map原始归属和原文不变，工程页拆分在配置器记录映射即可。闭合图已由现有FirstDayLetterPanel prefab直接引用，无需搬资源或新Scene/Canvas。

## 前次交付状态（2026-10-16 12:12 +08:00，历史）

用户已明确选择“继续准备禁用内容”，未提供金额/倾向/拘押玩法定案。本次将五个根全部准备为 blockedBy 保护的源配置，正式投放数仍为0；不把禁用内容当完整游戏验收。

|事件|已生成配置|本次实际运行证据|正式/内容门禁|
|---|---|---|---|
|church.1009.s2|4页36句2选择、P050尾完成结果|共享场景真实UI30/28行；Controller/Presenter双支、取消无完成、once、正式门禁拒绝均通过|B真实密见完成生产者；安全Immediate续接未接，资源占位|
|church.1014.s2|9页51句6选择；Inline知觉≥2；无倾向或完成结果|Presenter18/18组合（知觉1/2×三×三）通过；共享UI知觉1=43句、2=44句，P031.a/b和P075正确，未打开读信UI，pause0|A活动/C上午信真实完成、倾向bar；P028闭合信封物件/动作/立绘待补，真实AfterBusiness未测|
|church.1020.s1|19页117句7选择、5Inline检查；无Action/Date/Result/完成事实|所选解释四属性5/6共8出口Presenter分段断言通过，未选属性置相反值不影响；公共尾未播|selected_explanation_pass/fail依赖未实现；拘押/地图限制/信源资格未接，不可完整预览|
|church.1023.s1|14页113句5选择（含2后备复用）、language7及智慧/知觉OR6|正式根拒绝、资金未知依赖抛错；OR四组(5,5)/(6,5)/(5,6)/(6,6)实测正确|金额/账户、倾向、后备与局部releaseMethod、拘押日程依赖门；无MoneyRemove/释放/失败完成结果，不可完整预览|
|church.1023.s2|2页25句，无Date/Ending/Result/ConditionKey|共享UI25句到P032，IsEnded=false、pause0，正式根拒绝|真实release.failed生产者、P033红帧/淡出/音效、Ending和终止回调；不是结局验收|

官方命令统一 `MSYS_NO_PATHCONV=1 cmd.exe /d /c 'Assets\\ConfigTables\\GenerateAll.bat'`：1014 12:05:19、1020 12:06:08、1023.s1 12:07:41，exit0，Player semantic/Luban strict通过。最后完整 `unity_recompile` 返回 compiled、compilation completed、domain_reload completed、bridge ready、11文件无语义warning；之后执行上述断言与UI回归。此前两次共享检查点接口缺失编译失败已在最终收敛后解除，未由D改共享C#。

实际文件增量：

- `Tools/Narrative/import_church_text.py`；`configure_church_1009.py`；`configure_church_1014.py`；`configure_church_1020.py`；`configure_church_1023_release.py`；`configure_church_1023_end.py`。
- `Assets/ConfigTables/Data/Narrative/Narrative.xlsx`、`Assets/ConfigTables/Data/Shared/Text.xlsx`：增量行，候选回读保持既有单元格，并写前CAS；本窗口子任务串行写工作簿。跨窗口无全局互斥，整合仍应遵循最新总控审查记录唯一owner，CAS不冒称跨文件原子事务。
- 官方生成 `Assets/Settings/Resources/Luban/` 对应Event/Presentation/Setup/Page/Line/Choice/AbilityCheck/ConditionKey/Condition/Result/Object/Text JSON；没有手改生成物。共享schema/C#扩展来自其他窗口，D未修改。
- `Assets/Resources/Dialogue/NarrativeStageVisuals.asset`：只追加D教堂/拘押室/黑幕/傍晚咖啡馆的显式中性色或黑色占位，不移动/复制资源。背景、音效、CG、帽图、闭合信封、人物姿态/出场动作与P033红帧没有声称齐备。

新增manifest：`Library/Locus/tmp/church-content/20261016-120435-450385-1014-config/manifest.json`、`20261016-120545-249448-1020-config/manifest.json`、`20261016-120730-626108-1023-release-config/manifest.json`。全批静态证据 `Library/Locus/tmp/church-content/final-config-verification.json`：5根均blockedBy、正文计数36/51/117/113/25，Text38/57/124/116/25，共465原段/360显示Text；无任何D EventDate，除1009完成外无D结果。

最新工程版本同时已读 `Docs/superpowers/reports/2026-10-16-content-batches-integration-audit.md`（总控只读中途审查）及E工程记录。帽子历史合同仍与B不一致；沿D原规格只提交completed，没有擅增hat_helped。C14虽有配置/局部验收，仍有blockedBy和缺正式完成结果，不能当真实上午信前置已成立。

可继续入口：先以唯一整合owner收敛公共源和生成，再给出金额B、两倾向权威数值及拘押日程/location决议；1014可隔离复用现有check/choice做预览，真实结果未给不得放行；1020先补“已选解释检查结果”汇合，1023先补真实资金资格/会话释放方式/失败决议，最后才续接完整s2与Ending。禁止批量清blockedBy。正式DayFlow/地图/AP/钱/存档/上下游链仍未验证。

## 前次续做状态（2026-10-16 03:06 +08:00，历史）

旧 speakerNameKey 生成门禁已由 E 窗口补列解除（E 工程记录 02:46），本轮官方生成 02:51:25、02:52:42、02:57:57、02:58:27、02:59:30、02:59:56、03:04:01 均 exit0。未覆盖其他窗口列/代码。

- 五稿 465 原段与 360 条 Text 源及生成 JSON 逐项相等，见 `Library/Locus/tmp/church-content/final-text-verification.json`。
- 1009 已配单根4页36正文2选择，仅根尾一条完成结果；`blockedBy=D-B03-rose-real-completion` 保持正式禁用，无日期自动入口。当前公共 `choice:` 条件复用其他窗口已实现能力，D 未改共享 C#。
- 1009 Controller/Presenter 分页断言：捡30行/不捡28行，P050末尾完成、once拒绝重播、中途取消不完成、正式阻塞拒绝均实际通过。测试隔离副本移除 blockedBy，不写正式会话。
- 1009 共享 `Assets/Scenes/PresentationDebugScene.unity` 真实UI两支已通过，30/28行，pause=0。证据工具任务 t4（03:05）；首轮缺背景失败已添加现有舞台库中性色占位修复；第二轮被其他Agent的 revolution.1013.s2舞台替换，标作并发无效测试，第三轮为有效通过。
- 1023.s2 已配受 blockedBy 保护的2页25正文，无EventDate/Ending/Result/ConditionKey；共享场景隔离UI25行到P032通过、IsEnded=false、pause=0。P033红帧/淡出/音效待实现，不能称结局验收；正式请求拒绝通过。证据工具任务 t5。
- 1014/1020/1023.s1 当前Text已生成；后续按用户选择“继续准备禁用内容”继续配表，未定金额、数值源和日程仍是正式门禁。

实际增量另含 `Tools/Narrative/configure_church_1009.py`、`Tools/Narrative/configure_church_1023_end.py`、`Narrative.xlsx` 及现有舞台库追加 church.cathedral.day、church.detention.interior 中性色占位和 church.execution.black 黑色占位；未改原条目。1009命名为广播/人群/埃利昂公爵/男人，均无冒用立绘。1009资源CG、光雨、人声、帽图未补。

当前编译工具返回 up_to_date/not_needed，随后已加载程序集执行以上断言；不宣称本轮新C#全项目重新编译。

## 首轮历史结论

本轮按规格顺序启动首件 `church.1009.s2`。已完成该事件的原稿/source-map 校验和 Text 源表增量，但在官方生成门禁处停止：Luban 读取既有 `Narrative.xlsx` 时发现 `ConversationLine` 源表缺少当前生成代码要求的 `speakerNameKey` 列。该共享结构差异来自并发工程改动范围，本轮未修改、未裁决、未用 D 配表绕过。

因此：1009 仅完成“已验证正文 Text 源增量”，尚未形成正式 Event/入口/分支/结果，也未完成编译、共享调试场景或正式链路测试。其余四个事件未施工，避免在公共生成门禁未解除时继续写入并扩大冲突。

## 已施工事件

### `church.1009.s2`

实际改动：

- `Tools/Narrative/import_church_text.py`：新增可恢复的单事件 Text 施工器。它按 D `source-map.md` 读取正文/选项映射，核对 DOCX SHA-256、非空段落数量、逐段原文顺序；清理仅规格允许的角色前缀、表情/选项注记，并保留 1009 正文原文。
- `Assets/ConfigTables/Data/Shared/Text.xlsx`：追加 38 条 `church.1009.s2.*.text`，既有 5,152 个单元格在候选文件回读校验中保持值、类型、样式和批注不变。未写 Event、EventDate、Action、ConditionKey、Result、完成事实或地图挂载。
- `Assets/Settings/Resources/Luban/tbtext.json`：由官方 `GenerateAll.bat` 尝试生成；该输出是生成器副产物，不能视为 D 事件已生成。
- `Assets/Scripts/Config/Generated/NcsConversationLineEntry.cs`：官方生成尝试期间检测到的既有工作树差异；本轮未手改。
- 施工证据目录：`Library/Locus/tmp/church-content/20261016-024350-681461-1009.s2/`，含 `Text.before.xlsx`、候选文件和 `manifest.json`。

原稿证据：

- 来源：`D:/桌面/文案/王庭教会/MA70.10.09-S2.docx`
- SHA-256：`15c3101b85329f082efa98d9c938791089d9ac5e5cb35c50d8788fa4a3d9c58d`
- 非空段落：50；进入 Text 的正文/选项行：38。
- 前置仍未伪造：仅 `story.rose.1009.s1.completed=true` 才允许正式入口；现有工程记录说明 B 新玫瑰仅有 Text、没有该完成事实生产者，因此没有配置正式触发。

## 实际验证证据

已执行：

1. 施工器：`python Tools/Narrative/import_church_text.py --event 1009.s2`，成功；输出 38 条，追加行 1289–1326，原稿 50 段和 SHA 校验通过。
2. 官方生成：`MSYS_NO_PATHCONV=1 cmd.exe /d /c 'Assets\\ConfigTables\\GenerateAll.bat'`，失败，exit code 1。
3. 失败定位：`Player semantic validation passed`；Luban 在加载 `Narrative.xlsx` 的 `ConversationLine` 表时报错：`bean:'NcsConversationLineEntry' 缺失列:'speakerNameKey'`。
4. 源表核对：当前 `ConversationLine` 表头为 `lineId,pageId,seq,speakerKey,side,textKey,voiceKey,emote,sfxKey,accentFxKey,accentTargetKey`，确实没有 `speakerNameKey`。
5. Unity 当前状态只读检查：Editor 仍在播放，Active Scene 为 `Assets/Scenes/启动场景.unity`，未切换到共享调试场景；未宣称运行验证。

已有工程验收证据仍仅来自：

- `Docs/superpowers/reports/2026-10-15-october07-acceptance.md`，覆盖 oct07，不覆盖 D。
- `Docs/superpowers/reports/2026-10-15-october07-content-map.md`，覆盖 oct07 配置，不覆盖 D。
- `Docs/superpowers/reports/2026-10-16-A-neutral-implementation.md`、`2026-10-16-rose-content-implementation.md`，说明跨批次前置和共享工程并发状态；不是 D 的运行验收。

## 仍阻塞事件

- `church.1009.s2`：阻塞于新玫瑰真实完成事实、同根选择路由能力、官方源表/生成结构的 `speakerNameKey` 差异，以及后续资源/入口联调。当前只完成 Text 源增量。
- `church.1014.s2`：未施工。阻塞于 A 活动真实完成别名、C 上午来信真实完成事实、王庭—教会倾向真实数值源、同根双组选择/知觉替代路由，以及同一公共生成门禁。
- `church.1020.s1`：未施工。阻塞于真实密信来源、同根多分支检定能力、拘押闭环/D-C04；不能按日期或旧事件 ID合成密信。
- `church.1023.s1`：未施工。贿赂金额 B 未定；革命联合倾向真实数值源、1020 真实拘押、拘押日程和同根后备/释放方式路由均未确认；不能填 0 或伪造余额不足。
- `church.1023.s2`：未施工。只能由 `1023.s1` 完整提交 `release.failed=true` 续接，不能按 day18 或日期自动播放；D-C05 Ending 接线也未验证。

## 后续续做入口

1. 由共享配置/生成物所有者先统一 `ConversationLine` 源表与生成 schema/代码的 `speakerNameKey` 契约，完成官方生成和 Unity 编译；本轮不替其他 Agent 决定列语义。
2. 生成门禁解除后，从施工器的幂等入口重新核验 `--event 1009.s2`，再为 1009 配置正式入口和根内分支；先确认 B 的 `story.rose.1009.s1.completed` 真实生产者。
3. 每个事件继续遵守“配置 → 官方生成 → 编译/校验 → `Assets/Scenes/PresentationDebugScene.unity` 或正式链路测试 → 修复 → 记录”，不把 Text 生成结果当作可玩交付。
4. 保持 D 施工包三份原稿映射不改；正式施工前继续核对 ConditionKey/Bar/Result/Event 占用，避免仅凭裸文件名或旧事件 ID 接线。

## 尚未验证项

共享调试场景、正式地图/DayFlow、AfterBusiness/MorningBeforeMap/MapAction/Immediate 安全入口、取消与重试、选择互斥、属性边界、MoneyAccount、拘押跳日、Ending、资源占位和存档恢复均未在 D 本轮运行。未新增 Scene、Canvas、平行剧情系统、钱包、临时地图行动或 D 之外事件。
