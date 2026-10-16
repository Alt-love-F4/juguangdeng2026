# E-church 工程施工记录

日期：2026-10-16。范围：王庭教会长篇 E；保留其他 Agent 修改。本记录区分源表交付、官方生成、编译与游戏验证。

## 当前结论

`church.1104.s1` 当前为一个带施工门禁的根、42个步骤（39页正文、2个Wait检查点、1个终端黑幕）、405行对白、8个选项和3个Inline检定。同根检查点、DayFlow真实CafeWorld经营回执、取消/恢复和旧回调防护已实现并有专项测试证据；本轮中间页预览与正式会话/存档隔离6/6通过，舞台/黑幕/检查点回归13/13通过，并提供Editor菜单入口。正式根仍无EventDate/Result，E-B01–04及资源/完整验收门禁保持。11.05两份仅标题稿未创建配置。下方历史记录按时点保留；当前状态以末尾续做/复验段为准，不能把公共能力或隔离夹具通过等同正式整章发布。

## 本轮实际交付

- `Tools/Narrative/import_church_long_text.py`：可恢复、幂等、带 SHA/CAS 检查的 E 文本施工器；只消费 E `source-map.md`，不创建事件。修复 source-map 表格行匹配和“原文 xx 15 处、显示文本仍有 14 处”的统计边界。
- `Assets/ConfigTables/Data/Shared/Text.xlsx`：追加 405 条正文与 8 条选项，共 413 条，实际行 `876–1288`；413 个 key 唯一，既有 3500 个单元格值/类型/样式/批注保持。当前 SHA-256：`b21b1becd164660dd77f76fbd37a07d750eaf6bf8ce985fca5f4bdb6b7212b41`。
- `Assets/ConfigTables/Data/Narrative/Narrative.xlsx`：为配合已存在的并发 schema 增量，补 `ConversationLine!M` 的 `speakerNameKey` 表头、类型和说明；没有写入 E 事件行。该公共变更需由共享表所有者继续收敛。
- 11.05 S1/S2：未写 Text、Event、EventDate、Result 或完成事实。

证据目录：`Library/Locus/tmp/E-church/20261016-024315-014418`（Text manifest/候选/备份）；`Library/Locus/tmp/E-church/20261016-024626-016684`（ConversationLine 结构对齐 manifest）。

## 证据与结果

### 已有工程证据（非本批实测）

- `Docs/superpowers/reports/2026-10-15-october07-acceptance.md`：oct07 S1/S2 已有正式链路、共享场景、日结和 59/59 Editor 内直接断言；不覆盖 E。
- `Docs/superpowers/reports/2026-10-16-rose-content-implementation.md`：B 仅 Text 增量，且记录公共核心/源表并发状态；不覆盖 E。
- `Docs/superpowers/reports/2026-10-16-legacy-map-cleanup.md`：官方生成曾在 02:31:41 通过，但不包含本轮 E 增量。

### 本轮实际通过

- E 源静态审计：DOCX 475 个非空段与 source-map 逐段一致；E04 分类为标题 1、控制 26、演出 33、正文 405、选项 8；Text 显示条目 413；原文未定 `xx` 15 处，剥除作者/奖励标记后仍保留 14 处，金额不擅自补值。
- Text 写入成功：413 行、key 无重复；`{playernam}`→`{playername}`技术修正按 source-map 执行，未改原稿；两个 11.05 文件仍无配置。
- `unity_recompile(editor_status=editing)`：Unity 编译/domain reload completed，bridge ready；这是公共代码当前状态的收敛，不是 E 事件编译通过。
- Unity Console 读取：当前无 error 条目。

### 本轮失败/阻塞

- `MSYS_NO_PATHCONV=1 cmd.exe /d /c 'Assets\\ConfigTables\\GenerateAll.bat'` 02:43:24：Player semantic passed；Luban 读取 `Narrative.xlsx` 时失败，原因是 schema/C# 已要求 `ConversationLine.speakerNameKey`，表缺列。已补 `ConversationLine!M` 后重试。
- 同命令 02:46:36：表结构读取通过，但官方校验被并发内容 `Event!rose.1009.s1.eventKind=narrative` 阻断；当前 schema set 仅允许 `actor,world`。该值不属于 E，未擅自改动。
- 失败生成已刷新部分 Luban JSON，不能视为成功生成或可运行交付。随后直接 `LubanNarrativeLoader.Load()` 观察到 `TbText.GetOrDefault(null)` 异常，说明公共生成物尚未收敛；未据此继续运行 E。

## 阻塞表

| ID | 来源 | 影响 | 解除条件 / 下一步 |
|---|---|---|---|
| E-B01 | E spec/handoff，11.03 庇护真实生产者未交付 | day30 根入口不能开放 | 确认 11.03 完成+庇护事实 key、生产者及未知值语义；再接 EventDate Hold/Once |
| E-B02 | E source-map P043/P061/P064、P095 等 15 个 `xx` | fee.quoted、两路线市场段和最终金额不能正式放出 | 确认 Q、C、F；不填 0、不绑定为同一数值 |
| E-B03 | E source-map P440/P453，故事时间线 F86 | 夜间 true/false 分支无法正式配置 | 确认革命联合真实 faction 值源、阈值/分类和未知处理；不能借旧 `revolution` demo bool |
| E-B04 | E source-map P149/P150/P350/P351，依赖 D/10.14-S2 | 回忆连续性未证明 | 确认可达 day30 存档均经历 D 段，或提供未听过文案；不新增假前置 |
| E-C01 | E handoff §4，现有 DayFlow/Presenter 无同根跨经营、跨 BeforeNight 检查点协议 | P069→真实经营→P071、P412→P413 不能安全续接 | 统一实现 root transaction/cursor/business token/checkpoint/迟到回调失效；不能用假经营回调或 CompleteEvent 代替 |
| E-C02 | E handoff §4，PageVisible 仍拒绝 `pageBarConds`，Choice/Inline 多组路由未统一 | fee/route、同一知觉快照、智慧、阵营互斥页不可正式配置 | 统一局部 choice/check 快照门与 validator/loader；未知值不得默认为失败 |
| E-C03 | E handoff §4，alienor 正式别名仍需 Object/Text/解析支持 | 全程“埃莉诺”显示和局部房东问号无法验收 | 统一角色显示别名覆盖，保持 `alienor` 身份/资源/关系不变 |
| E-C04 | E handoff §4，章节预览当前固定 day=2、只支持 perception | day30、wisdom、前置、route/fee/night 隔离预览不能声称完整 | 扩展既有 `NarrativeChapterPreview` 输入，继续使用共享 PresentationDebugScene |
| 公共生成阻塞 | 本轮命令输出，其他窗口内容 | 官方 Bat 当前不能成功生成，现有 JSON 可能半刷新 | 修正 `rose.1009.s1.eventKind` 为 schema 合法值并重跑 Bat；确认 ConversationLine M 列合并无冲突 |

## E-V01–24 状态

- E-V01：静态通过 E 段分类/475 原段核对；未做游戏读取。
- E-V02：Text key 无重复；正式 Event/Page/Choice/Result 未创建，根完整性未通过。
- E-V03：未通过，E-B01 未解除且无正式根。
- E-V04–05：文本已落表；知觉/三 fee 分支未配置、未运行，E-B02 阻塞 quoted。
- E-V06–07：未验证；E-C01 缺失。
- E-V08–12：两路线文本已准备；路由、旧城快照、回忆前情未验证。
- E-V13–20：未验证；跨阶段、单一结果、取消恢复/存档均受 E-C01 阻塞。
- E-V21：未验证；章节预览输入不足，且生成失败。
- E-V22：未验证；无最终黑幕/DayFlow 接线。
- E-V23：静态通过，11.05 无配置/结果/自动完成。
- E-V24：本批官方 Bat 未通过；Unity 公共编译完成但 E 未编译/运行。

## 续做入口

1. 共享表所有者先修复 `rose.1009.s1.eventKind` 的 schema 合法值，并用官方 Bat 重跑；确认 `ConversationLine!M=speakerNameKey` 与既有行空值兼容，直到 Luban `SUCCESS`。
2. 重新运行 `python Tools/Narrative/import_church_long_text.py --apply` 做幂等校验，确认 Text 行仍为 876–1288；再官方生成并 ForceUpdate 导入完整 JSON。
3. 总控先回填 E-B01–04 与 E-C01–04；之后按 source-map 先配置早晨、三 fee、真实经营边界，再两条路线、夜间分支，始终保持一个 `church.1104.s1` 根和唯一 FINAL。
4. 最短复验：Bat exit0 → Unity recompile/import → E-V01/02 静态检查 → 共享 `Assets/Scenes/PresentationDebugScene.unity` 隔离预览；正式 day30 路径必须在真实庇护/经营/BeforeNight 前置接线后再测。

## 最终复验更新（02:51 后）

并发整合已修复公共 schema/表数据错误，以下为本轮实际最终结果，取代上文“当前生成失败”的临时状态；前两次失败记录保留追溯。

- 02:51:13 官方 `GenerateAll.bat` **exit0**，Player semantic、Luban strict validation 均通过，SUCCESS。未手改任何生成物。
- 紧接编译首轮发现其他窗口正在修改的 `NarrativeChapterPreview.cs` 将 ChoiceSelected 事件放入对象初始化器导致 CS0079；尝试修复时文件已由并发工程更正，原子 edit 未匹配、未产生修改。再次 `unity_recompile` 结果 **compiled / compilation completed / domain_reload completed / bridge ready**。
- Edit Mode 通过 `AssetDatabase.ImportAsset(..., ForceUpdate)` 导入正式 Luban JSON，然后实际 `LubanNarrativeLoader.Load()` 成功：55 个根 Event，E 11.04/11.05 根数量 **0**。此前空 key 加载异常已在完整生成/导入后消失。
- 重新从原 DOCX/source-map 计算 413 条预期正文/选项，与官方 `tbtext.json` **逐 key、逐文本全量相等**；金额占位保持。Text 当前源 SHA-256 为 `8b645f498665b6d18db7d1aa7cfa8290dd850709aef20287c3ed877ae72d413a`（其他窗口持续增量写入，早期哈希只作阶段快照）。
- E-V24 的“官方生成、公共编译和配置加载”子项现已通过；共享场景、E 正式链路、S1/S2 运行回归仍未执行，不能标整项通过。
- E-C03 已有并发的 speakerNameKey/schema 工作，E-C02 也有 ReadPresentationCondition 增量；本轮未完整验收这些新增能力，续做须重读当前实现，不按初始快照重复开发。
- 资源 E-R001–022 未绑定或视觉核验，本轮没有改资源；继续沿 handoff 原占位清单。

下一步从已生成/已加载的 413 条 E Text 继续；上文公共生成阻塞已经解除，无需再次修 rose.eventKind。真正剩余发布门禁仍是 E-B01–04 与 E-C01/02/03/04 的本章端到端接线/验收。E-B05 两份标题稿不施工。

## 早期停止点（已由后续施工推进）

02:51 时仅完成 Text、生成和加载。以下记录覆盖随后新增的施工根、隔离预览和验收，不再沿用“根与预览未配置”的结论。

## 03:01 后施工与验收

### 本轮实际交付

- `Tools/Narrative/configure_church_long.py`：从同一 source-map 构建正式源表增量，含完整来源、行定位、短锁、两工作簿 CAS、备份/候选回读及逐文件替换日志。应用证据为 `Library/Locus/tmp/E-church/20261016-030126-641542-config`。初次候选验证因无样式与零 StyleArray 的表示差异失败；归一签名后成功，未覆盖并发内容。
- `Narrative.xlsx` 的 Event 第62行登记唯一 `church.1104.s1`；另有 PresentationIndex/SetupParams 各1、ConversationPage 39、ConversationLine 405、Choice 8、AbilityCheck/AbilityCheckCondition 各3、Object 9。Text 新增11条身份/显示名，复用已导入的413条原文。没有把42逻辑组配置成42事件，没有空 boundary 页。
- 三组选项为 `.fee.free/one/quoted`、`.route.far/near`、`.night.wait/ask/hurry`；检定为 `.morning.perception`、两路线共用的 `.old.perception`、`.street.wisdom`。每页保留 `dependency:E-C01.stage.morning/tour/night`，阵营末支保留 `dependency:E-B03.revolution` 正反门。正式 Controller 遇未解决 dependency 仍抛诊断。
- `NarrativeController.Entries.cs` 的 `check:` 门复用现有 Inline 缓存；正反门读取同一快照，不另建主动检定/事件。
- 既有 `NarrativeChapterPreview` 扩为 partial，新增 `NarrativeChapterPreview.ChurchLong.cs` 的 `PlayChurchLongConstruction(stage, perception, wisdom, revolution)`。仅新建独立内存配置和 DebugSession 内允许 blocked/result-free E 根，设 day30、属性和一个阶段；夜谈必须显式提供阵营夹具，未知拒绝。仅该内存根转换 Immediate；正式源仍 blocked，无 Date/Result。该入口可通过 Editor 调用，尚未添加调试场景按钮。
- `NarrativeStageVisuals.asset` 中为 E 正文使用但尚未登记的背景 key 添加显式 `placeholder=true`、0.3秒淡切的条目；既有咖啡馆昼/夜和街景条目复用，未把占位当正式美术。
- `PresentationCheckGuardTests.cs` 增加 oct07 正反页门两组断言及 E 三组选择/三次独立 Inline 缓存断言。

### 实际通过的生成、源码与配置检查

- 03:01:37 官方 `GenerateAll.bat` exit0，semantic/strict validation通过；没有直接修改生成物。
- Unity 编译和 domain reload completed / bridge ready。最后一次测试修正后再次完整编译通过，无该文件语义警告。
- 真实 `LubanNarrativeLoader.Load()` 得到 E：1根、39页、405行、8选项、0结果。
- 只读再次运行配置器 plan/prepare：`would_append={}`，源表幂等、E无Date/Result。逐项比较894条期望记录的已赋运行字段与官方JSON，差异0；Text保留原导入器comment，AbilityCheck comment不导出，来源注释不纳入运行字段比较。
- Editor内直接调用 NUnit 测试方法：oct07 perception=1/2 正反门共享一次属性读取；E三组各选一项、拒绝同组第二项、三检定各读取一次，修改属性后正反门仍用原快照，无活动主动检定、无根happened/completed；再次从正式JSON加载仍有blockedBy。以上是实际执行断言，未声称 Unity Test Runner suite 全量运行。
- E测试初稿调用不存在的正式 completed 条件时抛 Unknown condition key；改为 TryGetCondition 检查未写true，重新编译及上述三组断言均通过。没有为使测试通过而创建完成事实。

### 共享场景实际播放

全部在 `Assets/Scenes/PresentationDebugScene.unity`，使用正式生成内容和隔离夹具；均自然结束后 Dispose，没有运行真实经营或正式 DayFlow。

| 输入/分支 | 实际结果 |
|---|---|
| morning perception=2、fee.one | 33个不同source line，P030失败支有、P022成功支无，P058到达，结束暂停owner=0 |
| morning perception=3、fee.free | 30行，P022成功支有，正常结束 |
| morning perception=3、fee.quoted | 35行，P022成功支有，正常结束；xx仅保留为未定正文 |
| tour perception=3/wisdom=4、far | 152行，P080远线有、P255近线无，P226智慧成功有、夜间P413无 |
| tour perception=3/wisdom=4、near | 146行，P080远线无、P255近线有、智慧段无、夜间P413无 |
| tour perception=2/wisdom=3、far | 150行，旧城P169成功插段无，P234智慧失败支有，正常结束 |
| tour near、perception=2/3 | 分别144/146行，P269成功插段分别无/有，两次自然结束 |
| night revolution=false、ask | 33行，P441非革命支有、P454革命支无 |
| night revolution=true、ask | 41行，P441非革命支无、P454革命支有 |
| night wait / hurry | 两项均被面板接受且自然结束，补齐8选项覆盖 |
| tour播放中Dispose | 对话关闭，暂停owner恢复调用前基线 |
| night未传revolution | 明确抛错，未知未作为false |

曾因施工根仍是 MorningBeforeMap 而未接受预览；仅隔离配置转换 Immediate 后复测通过。后续尝试补视觉读取时遇另窗正在播放 `neutral.1008.s1`，预览正确拒绝；未清除或中断该对话，待其结束才切回 Edit Mode 编译。当前Editor最终为Edit Mode。

### 当前验收边界与剩余工作

- E-V01、V23静态证据保持；V02的根/正文/选择引用和官方生成字段已通过施工态检查，但唯一FINAL尚未配置。V04/V08/V09/V11/V15/V16的上述分段输入已实跑；V05只证明各响应可选可达，没有正式报酬结算。V10两路线知觉2/3均已覆盖运行；远线知觉3那轮未单独打印P169，证据强度仍区分逐句集合与目标断言。
- E-C02的局部选择、正反门、三次检定缓存已有代码/断言/分段运行证据；检查点保存恢复仍受E-C01限制。
- E-C04已支持day30/属性/分段/显式阵营夹具；任意中间页背景、角色、退场、选择与检定上下文重建未全做，不能标完整通过。
- 测试场景 `NarrativeRuntime.Instance.IsReady=false`，本轮未得到正式活跃存档前后JSON比较；取消和owner恢复已实测，但正式AP/资金/红点/历史/用户存档全量隔离仍应在就绪正式host旁回归。不得把没有输出snapshot比较结果描述为比较通过。
- E-C03仅完成alienor正式身份和speakerNameKey别名数据。补查早晨P015时面板speaker=你，left/right Image均active但sprite为空，未满足P014两人已登台；当前CreateScript只在各角色第一句登场，不能将其声明为源指令完成。P425仅adele退、末句后最终黑幕、对象叠图及音效仍未视觉验收/完整接线。资源当前为明确占位，E-R不能标通过。
- 最新只读复核仍无E-B01真实11.03庇护生产者、E-B02金额Q/C/F、E-B03正式阵营分类/未知语义、E-B04回忆连续性保证。没有新增这些事实或擅自确定数值。
- E-C01正式同根跨经营/BeforeNight协议仍缺；NCS当前active request/快照并不等于业务checkpoint，不能持有全局busy等待或提前CompleteEvent代替。E-V03/V06/V07/V12/V13/V17–20/V22及V24正式全链路部分保持未通过。

### 03:23 舞台槽位接入及复验

本段更新前文P015双立绘为空及P425退场未接入的状态。正式ConversationLine新增可选 `stageLeftKey/stageRightKey/stageCenterKey`：空保持，`-`清空，对象key放置；旧行全空继续既有行为。通过schema、Narrative.xlsx、官方生成C#/JSON、loader/校验、Presenter快照、现有DialogueLine替换链路接通；没有按E事件ID硬编码。未知对象、同槽舞台与发言人冲突抛诊断。清空沿现有ChangeCharacters淡出并移除身份，因此不会在后续旁白重新出现。

- 新工具 `Tools/Narrative/configure_church_stage.py` 只追加三列和三处源锚点，备份/CAS/候选逐cell保留验证通过。证据 `Library/Locus/tmp/E-church/20261016-032249-stage`。P015=alienor左+adele右，独立夜谈P414重建两人，P426右槽清空；主配置器同步更新，plan/prepare仍 `would_append={}`。
- 03:23官方GenerateAll.bat exit0，semantic/strict成功；typed C#由官方生成。Unity完整编译/domain reload成功，正式JSON导入后真实loader成功。
- 新 `ConversationStageSlotTests` 四个测试方法在Editor实际执行通过：未知Object诊断、旁白同时登台与单独清空、纯选项无正文兼容、源配置锚点正确。
- 共享PresentationDebugScene实际跑早晨/夜谈：P015 speaker=你，两槽sprite=neutral、身份alienor/adele；P016 speaker=埃莉诺、两立绘保持；P414两人齐；P426左neutral/右空、身份alienor/空；P432 speaker=埃莉诺，左保持右仍空。两段自然结束，最后pause owner=0。
- 回归实际调用October07S1PresentationTests的配置完整性与Offstage/旁白/省略表情两个测试方法，均通过；E三组选项及三检定缓存断言再次通过。不是全量Test Runner suite。
- 当前退场状态/提前登台可标本轮运行通过；乔装美术、缺失表情、音效、叠图、最终黑幕以及正式经营检查点仍未因此通过。最后Editor回到Edit Mode。

### 03:33 终端黑幕接入及复验

- 按 E04-P452/P473 的共同控制语义，在唯一演示根末尾追加 `church.1104.s1.night.fade`，seq 为全部正文之后的第40页，`stepType=FadeOut`，仅保留 `dependency:E-C01.stage.night`；不创建正文行、不创建结果、不创建第二根。两种阵营末支仍通过各自正反门汇合到这一页。
- `Tools/Narrative/configure_church_long.py --apply` 成功，证据 `Library/Locus/tmp/E-church/20261016-033250-277658-config`；官方 `GenerateAll.bat` 03:33 exit0，semantic/strict通过，loader实读E页数40。
- 共享场景逐帧跑夜谈 revolution=false/true：分别停在P451/P472时 overlay alpha=0，完成回调未触发；继续后两条都观察到全黑帧、自然完成且回调触发，最终 pause owner=0。两末支没有提前黑幕或提前完成。
- 现有 `DialoguePlayback.FadeStage` 使用同一 continuity owner 和 retained stage，完成后释放会清空 overlay，因此本轮证据是“黑幕在释放前出现”，不是完成后永久保持黑屏。取消/外部Stop中断 fade 的正式宿主回归仍待E-C01接入；没有用黑幕冒充FINAL。

### 12时后检查点施工及首轮复验

本段取代前文“检查点协议缺失/仅40页”的当前判断；前文保留历史。公共文件继续按分工串行编译和运行，没有回滚其他窗口内容。

- `NarrativeController.Checkpoints.cs`：同一根RequestId保留cursor、day、stage、业务token/confirmed、局部选择、Inline/属性检查缓存；Wait释放busy，不CompleteEvent、不写once。AfterBusiness只恢复已确认经营，BeforeNight只恢复待夜检查点；待续根防重复入队，优先于普通同阶段候选，恢复仍复查day/终局/锁/施工门禁。
- `NarrativeEventRequest.PresentationToken`区分同一段的每次播放。取消后的同页重试即使RequestId和ResumePage相同，也拒收旧检查点回调。公共快照复制时合入当前续接段的新选择/检查缓存，不改变此前已捕获快照；属性条件优先按稳定conditionId恢复，兼容旧行索引缓存。
- Presenter的WaitBusiness/WaitNight只交接，不Finished；释放stage/pause后延后一帧发CheckpointReached。该交接期间仍算活动session；宿主中断/卸载会取消，避免controller永久busy。恢复优先ResumePageId，已选组选项不重复弹出；construction preview显式忽略Wait，只结束该隔离段。
- `DayFlowController`实际记录endpoint owner、观察到Open、day/token。旧Night和延迟Start不能确认；Finish只接受曾Open且同owner，结束后须真实Night才Confirm。已结算Night不再次调用End；确认失败保留凭据并停止推进。恢复未观察Open的已签发凭据时重发启动请求，已观察Open不重开经营。
- 已确认Business的下午演出取消后，恢复PreBusiness或再次EndDay直接重试AfterBusiness；不重新经营。新增保存retryEntry，避免早晨取消后只存在于Panel回调里的重试目的地丢失。正文仍遵守blocksSave，检查点允许保存；不宣称任意句游标恢复。
- `configure_church_checkpoints.py`只迁移E页序和stage门，并追加`.checkpoint.business`（seq9）、`.checkpoint.night`（seq36）；所有页改用`checkpointStage:0/1/2`，阵营仍保留未解决dependency。P071重建两人舞台。12:05及12:10官方GenerateAll.bat exit0；真实loader读42页。主配置器同步，最新只读prepare为`would_append={}`，E无Date/Result，正式blockedBy完整保留。
- `NarrativeDebugSession`转发checkpoint/cancel并退订旧presenter，清当前请求/完成回调/epoch，不访问正式Runtime。

首轮已运行证据：

| 范围 | 工具与结果 | 证据边界 |
|---|---|---|
| 核心同根协议、确认、保存、取消、迟到回调 | Unity Test Runner Edit，`NarrativeCheckpointTests` **6/6通过**，runId `aad0cfd6198f45f2a2ae24560df88707` | fake presenter，真实controller；含同段旧token和活动续接快照新增回归 |
| 真实经营宿主、恢复、交接中断及旧DayFlow | Unity Test Runner Play **20/20通过**，runId `562367c83134401aa2a3b789990d8ab5` | DayFlowCheckpointTests 3项+PresenterCheckpointHandoffTests 2项+原DayFlowPresentationTests 15项；CafeWorld真实Open/Night，endpoint为受控适配器，不是Bootstrap跨场景全链 |
| 完整E同根正文隔离运行 | 早晨37行→下午152行→夜谈33行，completed计数0→0→1，每段busy释放、pause回0，同RequestId | t9日志；经营Confirm为人工隔离凭据，不作实际经营证据；阵营显式false，根只在内存移除门禁 |
| 编译 | 后续修复前完整compiled/domain_reload completed/bridge ready | 新增终端fade/资金/初始取消回归仍在后续收敛，以下最终复验段补结果 |

完整E隔离运行日志：`E:\Locus\data\temp\tool-results\7622d70a-a8c6-4da6-88d8-ee30f7a8fbbf\task_81da2d651715458eb17d8b538e175644.log`。首次宿主/交接合并测试因父查询使用editing状态停止了Play而中止（4项通过、无断言失败），不能算通过；重新完整运行得到上述20/20。

### 尚待补齐的正式合同与验收

最新只读跨批次复核：E-B01仍无church.1103根及庇护生产者；E-B02仍14条显示文本含xx，Choice fee三项resultOwner均空、E无Result；E-B03无正式分类源，旧revolution默认false不具本章语义；D1014正文已交付但根仍blocked且无Date/完成Result，不能证明E两路线的回忆连续性。因此没有清除E-B01–04，没有登记拟议完成/获报事实或用0代替Q。

E-C01公共协议已进入已实现/专项通过状态；E真实前置→day30正式候选→CoffeeShop运行时跨场景→最终钱/完成/获报的整章验收仍未完成。终端fade外部Stop取消、真实钱包最终增量及生成正文与DayFlow合并的隔离运行已在下段补验通过。资源乔装/表情/对象叠图/音效仍未通过，任意中间页上下文与正式存档/AP/历史/红点全量隔离仍待验收。保持正式root的全部blockedBy以及无EventDate/Result；11.05继续不施工。

### 本轮最终复验：检查点、真实钱包、黑幕及生成整章联调

最终补丁：`NarrativeConversationEventPlayback`的终端fade同步登记PlaybackStopped，并以一次性回执统一正常完成、handle取消、Stop/Close/禁用及启动异常。回执前退订，正常release不会再次取消；避免面板自身协程被停止后NCS永久等待。未改共享效果核心或另建UI系统。

| 测试 | 实际结果与证据 | 明确边界 |
|---|---|---|
| NarrativeCheckpointTests + PresentationCheckGuardTests | Edit **9/9通过**；runId `d943c614462b4f06866deb018894a601` | 6项checkpoint+3项check/choice；GameFlow资金测试属于Play程序集，不在本次Edit发现范围 |
| DayFlowCheckpointTests | Play **4/4通过**；runId `ca4c7feb7f284705b3416d38a4519570` | 真实CafeWorld、可控endpoint；增加初始晨间取消→PreBusiness存档→恢复保留候选重试，不跳过正文 |
| NarrativeCheckpointMoneyTests | 同run Play **4/4通过** | fixture自有事实/resultOwner：fee.one尾部以最新CafeWorld钱包+1；free无钱包写；两Wait不发钱；实际经营一次；读/提交拒绝均不写完成/获报/once，保留待夜重试后只提交一次；无Q假值、无正式E Result |
| NarrativeTerminalFadeTests | 同run Play **6/6通过** | 共享场景真实DialoguePanel/adapter/Presenter/Controller；Stop、Close、禁用、StopEffects取消无结果且释放busy/pause；末句可读，全黑后才完成一次；启动异常后下一次fade仍正常 |
| PresenterCheckpointHandoffTests | 同run Play **2/2通过** | 交接一帧内重复Interrupt只取消一次；正常通知时已idle，能立即重入Play |
| ChurchLongCheckpointIntegrationTests | 独立复跑 Play **1/1通过**；runId `29dfc9a3ed1c42a9933626922fb69379` | 真实loader的42页/405行/8选项+正式Presenter顺序+DayFlow+真实CafeWorld；fee.one/near/night.wait，perception3/wisdom4，显式非革命fixture。对白完成与fade成功由适配器模拟，不是视觉/正式前置验收 |
| 原DayFlowPresentationTests | 首轮Play **15/15通过**；runId `562367c83134401aa2a3b789990d8ab5` | 原日程面板、保存恢复、新会话与完成回调回归 |

上述为跨已记录运行的**41个不同测试用例通过**，不是全项目测试套件。`ca4c...`整次17项为16通过、1失败；唯一失败是整章测试断言JSON还原后的active必须null。现场确认Unity JsonUtility把null嵌套类序列化为eventId空的对象，未表示活动演出；修正测试为null或空eventId后，整章单独复跑1/1通过。没有通过删除恢复步骤或绕过controller修测试。

整章合并测试实测：晨间Wait后completed=0；未经营EndDay被挡；Open→Night仅一次、BusinessEnd进入AfterBusiness；游览Wait后仍completed=0；保存Runtime与DayFlow JSON、重建真实Presenter和恢复外部provider后，BeforeNight从P413起播，不重跑早晨/游览、不重经营；仅非革命末支及一次模拟fade，最终completed=1、once写一次、checkpoint清空、进入原PreNight。重新loader仍blocked且无Result。真实经营端为测试适配器持有的CafeWorld，不是CoffeeShop运行时跨场景启动，也没有正式E完成/获报合同。

最后完整编译结果为compiled / compilation completed / domain_reload completed / bridge ready，相关C#语义无警告；后续测试断言修正再次收敛up_to_date。本轮相关源文件`git diff --check`通过（仅报告LF/CRLF提示）。最终Editor为Edit Mode，ActiveScene=`Assets/Scenes/PresentationDebugScene.unity`，sceneDirty=false、pauseOwners=0、dialoguePlaying=false、compiling=false。Console出现已知`flow.business_end`缺音频警告，该素材未在本轮补造。

### 后续准确入口

1. 先补E-B01真实11.03完整完成/庇护生产者与ID、E-B02明确Q/C/F及C/F关系、E-B03正式革命分类源/门槛/未知处理、E-B04回忆可达保证或权威替代稿。当前源表与最新D报告均未提供，不清门禁。
2. 输入确认后，通过正式Excel/schema登记ConditionKey/根和fee结果owner、EventDate day30 Once，再官方GenerateAll.bat生成；保留唯一根、共用末尾fade及最终事务。不得把当前fixture结果写回正式E。
3. 复验真实Bootstrap/Map/CoffeeShop跨场景日程、正文禁止保存及四个安全点存读、fee.one最新钱包+1/fee.quoted+Q、完成/获报同批一次、正常夜晚到日结一次。现有专项可复用，但不能声称替代这一正式端到端验收。
4. E-R资源及任意页预览上下文/正式AP和存档隔离继续按handoff矩阵验收；11.05两标题稿不施工。公共核心/表仍有并发窗口，下一次编辑前重读现状、协调锁和生成，不清理共享差异。

### 16时后续做：中间页预览与资源实查

本轮不改E正文/源表合同，不清门禁。新增 `PlayChurchLongPageConstruction(pageId, fee, route, night, perception=2, wisdom=3, revolution=null, completed=null)`：只接受本根有正文的Dialogue/PassiveCheck页。fee用free/one/quoted，route用far/near，night用wait/ask/hurry；跳过的决策必须显式提供，目标页自己的选项和后续选项仍由玩家作答，晚间始终需要显式阵营夹具。未知、控制步骤、隐藏路线、失败检定或缺上下文均拒绝，不改播下一页。

- `NarrativeChapterPreview.cs`只增加两个私有hook；新 `.ChurchLongPage.cs`按源页序预检门和真实检定定义，并在独立Controller/Presenter内静默pre-roll当前阶段。通过真实selectChoice选择被跳过的选项，以共享CreateScript/DialogueCharacter.Resolve折叠角色、别名和表情；不播放被跳过的音效/瞬时特效、不写正式历史。目标首行保留显式替换/清空优先级，不让P426后阿黛尔复活。旧分段API保持。
- Editor菜单 `Tools > Game Flow > Church Long Isolated Preview`，代码 `Assets/Editor/GameFlow/ChurchLongPreviewWindow.cs`。只在既有共享演出场景Play Mode启用，不自动切场景/改输入系统；所有决策和阵营默认“未提供”。窗口只持有自身preview，关闭/退出Play释放自身owner，不接正式DayFlow。
- 16:23:57官方GenerateAll.bat exit0；此前共享CafeVisit schema/loader已新增订单字段而生成类未同步，一次生成后完整编译通过。另一窗口已完成其CafeVisit表头迁移，本轮未重复写表或手补生成物。期间共享Rose新测试的SceneHandle类型错误由其窗口修正后再次编译通过，没有回滚共享代码。
- 新 `ChurchLongPagePreviewTests` 六项UnityTest覆盖实际DialoguePanel、ready且provider-bound的正式Runtime、AP/钱包/玩家/CafeWorld完整JSON、正式历史、已知设置键及现有指定Saves目录只读hash。首次6/6在测试配置序列化被Vector2.normalized递归挡住，尚未触及预览；加坐标converter后4/6通过，余两处测试断言为p071非发言角色的规范姓名（p073才引入别名）和Unity texture fake-null比较，按实际源序/Unity对象语义修正。最终复跑结果待下段记录；失败不记作通过。

资源实查为有边界的源/注册表/图片检查，**不是播放/听音/正式资源验收**：

| E-R | 当前实查与处理 |
|---|---|
| 002、004基准、020 | 现有咖啡馆昼、阿黛尔neutral、咖啡馆夜tint绑定可复用。004激动/开心/思索等差分未找到；当前中文emote回退基准图，不是表情已交付。 |
| 003乔装王女 | `Assets/Resources/Characters/alienor/neutral.png`可见额头圣痕和宫廷服饰，不符合本稿乔装；`Assets/Resources/Characters/alienor/indoor.png`无圣痕、服饰较简，可列候选但未获正式美术批准。需要事件作用域换装选择，不能全局改Object或把indoor世界图自动当对白图。 |
| 001房间 | 候选 `Assets/Art/Background/SCE_ 三楼宿舍场景底图.png`实为多床公共宿舍且图上有白边，不能静默等同主角房间；保留现有中性色占位，待美术/构图确认。 |
| 006–009、014、019 | 市场、郊区、旧城/宅邸、圣徒街昼/傍晚仍为显式textureless占位，无已核准替代；智慧/merge专用背景条目也须一并替换。 |
| 005、015、017 | 未核到钱币/门前高矮人影/被驱逐家庭合法叠图。当前三角色槽和全屏色遮罩不是独立物件层，不借正式NPC剪影代演；叠图能力及图源仍待交付。 |
| 010–013、016、018 | 当前405条E行sfxKey均空。未核到鸟叫/争吵/重关门合适clip；已有 `ambience.city` 是loop=true的初配环境声，未听音确认，不能直接当一次性sfxKey反复插入。现行panel owner仅Stop清理，跨背景循环会累积；接音须先明确场景循环slot/停止语义，不编音量。 |
| 021阿黛尔退场 | P426 stageRightKey="-"清身份，左侧保留；已有ChangedIdentity淡出，不额外叠一遍accent。 |
| 022末尾黑幕 | 当前共用FadeOut=.3s，等成功及一帧全黑后release；release会清黑，不是永久黑幕。不要换成会淡回透明的fx_transition_black。此前六项真实UI专项通过，正式交接视觉仍保留整章验收边界。 |

审计日志：`E:\Locus\data\temp\tool-results\7622d70a-a8c6-4da6-88d8-ee30f7a8fbbf\task_6452d22f04c949c8adb202bcf52c1f17.log`。最新D报告仍未提供E-B04回忆可达保证；E-B01–04及未验收资源门禁继续保留。

### 本轮最终复验：中间页、隔离与回归

| 范围 | 实际结果 | 边界 |
|---|---|---|
| ChurchLongPagePreviewTests | Play **6/6通过**，runId `c99f4e94f4d54324bf397aca9bf3de49`，23.888s | 7个游览目标、3个晚间目标、3个决策页；15种隐藏/缺上下文、7种无效输入；正式busy持有独立PreBusinessPanel拒绝预览，不关闭原panel。实际UI首帧/角色/别名/背景key、TMP可读、取消及重复Dispose通过；正式配置/生成配置、Runtime/player/world完整JSON、AP/钱包/历史、已知Prefs和指定已有Saves文件名/时间/hash不变。 |
| ConversationStageSlotTests + NarrativeTerminalFadeTests + PresenterCheckpointHandoffTests + ChurchLongCheckpointIntegrationTests | Play **13/13通过**，runId `2a8cb92348f448cea77c8b58938a5c06`，11.067s | 分别4+6+2+1项。生成整章仍为受控经营endpoint/模拟对白与fade适配器；真实UI fade由独立6项覆盖，不能据此声称正式Bootstrap跨场景验收。 |
| Editor入口实跑/截图 | 菜单加载39个正文目标，fee/route/night/faction均默认未提供；窗口启动night.question到P432，left=埃莉诺/right=null，停止pauseOwners=0/dialoguePlaying=false | 使用共享PresentationDebugScene和正式生成内容。截图再次确认王女仍是带圣痕/宫廷服饰的基准图，因此乔装资源门禁未通过。 |
| 编译/静态/配置 | 全编译compiled/domain_reload completed/bridge ready，最后收敛up_to_date；相关patch whitespace无错误；实际loader72 Events；E仍42页、Date0/Result0、blockedBy全部保留 | 新增Editor证据回调 `Assets/Editor/GameFlow/ChurchLongPreviewVerification.cs` 只为六个E测试各写一份 `Library/Locus/tmp/church-page-preview-results/<method>.xml`，防共享窗口覆盖总测试摘要。六文件均Passed。 |

本轮新增6个不同用例，加此前41个已记录用例，共47个不同用例跨记录通过；本轮19项包含旧回归，不叠加成60个。不是全项目测试套件。

中间失败/并发边界保留：`b1d...`为4通过/2测试断言失败后已修；`301de...`为3通过后Play被停止，Editor.log明确“Playmode tests were aborted because the player was stopped”，不计通过；`7e90...`为5通过/1因共享Neutral partial正在写入而出现CS0103编译日志，后续文件收敛并完整6/6通过；另两次runId在其他窗口启动新测试/域重载后不可查询，不记结果。未用LogAssert吞编译错误或清理共享差异。共享Neutral的result-only调度根一度使全局Loader拒绝，其窗口完成并重编译后Loader72事件正常，未在E窗口修改其合同。

截图：`Library/Locus/Screenshots/locus_editor_window_20261016_090140_627.png`（菜单/显式输入），`Library/Locus/Screenshots/locus_game_20261016_090250_571.png`（P432、咖啡馆夜、仅埃莉诺、正文可读）。窗口已停止并关闭，自身owner已释放。随后已请求Edit；另一窗口又进入Play，最后recompile工具实报editor=playing/up_to_date，不强行中断共享Editor，也不把它写成最终Edit状态。

当前可交付是正式生成正文的分段/任意可达页隔离预览、公共检查点与专项测试。正式前置/数值/革命分类/回忆连续性、乔装及缺失资源、真实Bootstrap→Map→CoffeeShop跨场景完整结算仍未验收；继续按前述准确入口推进，保留E-C01/E-C04发布门禁直至整章正式验收，不因专项通过自动删门禁。
