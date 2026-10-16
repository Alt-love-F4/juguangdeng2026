# A：中立后续工程施工记录

日期：2026-10-16。当前记录对应本次工程Agent施工窗口；本批没有把旧报告或规格静态核对冒充为本批游戏验收。

当前交付：五篇已完成受阻隔离配置与真实UI分支验证，354正文/20选项。A01四路、A02–A05共73候选样本都有通过证据；资源收尾后A04连续26/26、六条舞台路径、十一项负向检查通过。首次批跑曾偶发停顿，复跑未再现，根因未定。五根正式门禁5/5拒绝，关系/经济/品质/报刊等定义未定，尚不是正式可玩交付。最新细节见末尾13:xx续做；前面的未施工/冲突/未运行是历史快照。

## 首轮中断结论（历史状态，已由下方续做更新）

首轮未向 A-neutral 正式源表、schema、生成物、场景或共享核心代码写入内容。A01–A05 均未形成正式可玩交付；A01 的正文仍可在公共能力和关系量表解除后作为下一步首件施工。原因是当前工作树存在其他Agent未合并的核心代码冲突，且 A 批所需的根内多轮分支/临时检定能力和正式关系定义尚未具备可验证状态。

## 已有工程验证证据（不是本批实测）

- `Docs/superpowers/reports/2026-10-15-october07-acceptance.md`：10月7日 S1/S2 的正式入口、信件续接、日结到 day3、共享调试场景预览和既有回归证据；报告明确 UTF runner 被工作区禁用，且这些证据只覆盖 oct07 范围。
- `Docs/superpowers/reports/2026-10-15-october07-content-map.md`：oct07 源表写入、官方生成和既有配置定位；不覆盖 A-neutral。
- `Docs/superpowers/specs/2026-10-16-content-batches/A-neutral/` 三份文件：A01–A05 的源段、条件、分支、阻塞和验收矩阵。规格静态完整不等于本批实装或运行通过。

## 首轮核对结果（历史快照）

### 工作树与共享代码

`git status --short` 显示大量其他Agent未提交改动；以下共享核心文件仍是 `UU` 未合并状态：

- `Assets/Scripts/FreeCafe/Unity/FreeCafeRuntime.cs`
- `Assets/Scripts/GameFlow/UI/PlayerCafeOwner.cs`
- `Assets/Scripts/GameFlow/UI/Presentation/NarrativeConversationEventPlayback.cs`
- `Assets/Scripts/GameFlow/UI/Presentation/PresentationDebugSceneController.cs`
- `Assets/Scripts/Narrative/NarrativePresenter.cs`

`Assets/Scripts/Narrative/NarrativePresenter.cs` 的冲突区正涉及正式分页/信件/通知播放实现；当前文件含 `<<<<<<< Updated upstream`、`=======`、`>>>>>>> Stashed changes`，不能据此编译或继续叠加 A 批接线。`FreeCafeRuntime.cs` 也存在三个冲突区。未替任何Agent裁决冲突，未覆盖、回滚或清理其改动。

静态代码仍显示以下 A-CAP01 关键边界：

- `NarrativeController.SelectChoice` 当前只允许一个主动 `activeAbilityCheck`；第二个不同检定会抛出 `Only one active ability check is supported per event presentation.`。
- `NarrativeController.CompleteEvent` 按当前根事件、已选 Choice 和单个主动检定汇总提交；尚不能证明 A03/A04 的多组选择、失败/成功互斥页和根尾原子结果已接通。
- `NarrativePresenter.PageVisible` 对非空 `pageBarConds` 直接抛出 `Unsupported presentation page bar conditions`；不能把 A04 的智慧门槛或其他临时结果写成未支持字段。

### 当前源表与资源

使用只读工作簿扫描确认：

- `Assets/ConfigTables/Data/Narrative/Narrative.xlsx` 当前没有 `neutral.1008`、`neutral.1010`、`neutral.1013`、`neutral.1018`、`story.neutral.*`、`colette` 或 `antoine` 命中。
- `Assets/ConfigTables/Data/Shared/Text.xlsx` 当前没有 `neutral.*` 文本 key 命中。
- Narrative 当前可见内容仍为既有 `oct07.s1` / `oct07.s2` 等配置；没有 A 批根 Event、EventDate、Action、Choice、ConversationPage、ConversationLine、Result 或关系 Bar 写入。
- `CharacterCatalog.asset` 是当前扫描到的相关角色索引文件，但本次没有把它当作 A01 正式 NCS Object/关系量表证据；A-CAP03/A-B010 仍需统一确认正式对象、关系方向、初值、范围和结果绑定。

因此本次没有生成 A 批 Text/Narrative 行，也没有生成可供官方校验的新增内容。

## 首轮按事件状态（历史快照，当前状态见末尾续做）

### A01 `neutral.1008.s1`

- 源文和分支规格已完整：day3、`market` MapAction、0AP、A/B/C 四路、B 的 language>=2 成功/失败、共同尾声和取消不结算。
- 可独立内容仍未配入 Narrative/Text；没有写入 `market` 挂载、Event、Action、Choice、ConversationPage/Line 或 Result。
- 阻塞：A-CAP01 根内分支/一次性结算能力未解除；A-CAP03/A-B010 的 `colette` 对象和真实关系 Bar 未确认；A-R001/002/003/008 仍为资源或音效占位。
- A-B001（常规采购价格/数量未定）不阻塞初遇，但不得据此新增采购扣款、物品或常规采购 Action。

### A02 `neutral.1010.s1`

- 正文/五路分支可以独立做隔离预览，但正式入口受 A-B003 同日开店/营业前时序冲突影响。
- A-B002/A-B004 的 10.13 旧 S1 别名和“两天后”日期矛盾未获定案；A-B005 的成本降低没有基数/数值/消费点。
- 未注册正式必做、未写促销解锁、未写 `frugal_preparation`。

### A03 `neutral.1010.s2`

- 正文、首轮三选一和接待后第二轮三选一未配表或预览。
- 受 A-B003 时序、A-B002/A-B004 别名/日期、A-B011 活动对话目标未定，以及 A-CAP01/A-CAP03 影响。
- 未写 `story.neutral.1010.s2.completed`、`antoine_approval` 或任何关系增量。

### A04 `neutral.1013.s2`

- 无赏识路径、赏识附加段、两轮选择和失败正文均未配表。
- 受 A-B002/A-B004 身份日期、A-B005 活动经济、A-B006 q2B wisdom>=2/3 冲突、A-CAP01/A-CAP04 影响。
- 未绑定革命联合同名 `revolution.1013.s2`，未注册正式日程卡点，未写任何活动收益或完成事实。

### A05 `neutral.1018.s1`

- 记者正文、品质通过推荐、品质失败广告接受/拒绝三路均未配表。
- 受 A-B007 真实品质阈值/快照口径、A-B008 广告费与币种、A-B009 下一刊委托/排期/回执、A-CAP02/A-CAP05 影响。
- 未写品质判定、MoneyRemove、广告委托、推荐委托或 `story.neutral.1018.s1.completed`。

## 首轮生成、编译和运行状态（历史快照）

- 官方 `Assets/ConfigTables/GenerateAll.bat`：本批未运行。原因：A批没有源表增量可生成，且共享核心代码存在未合并冲突；运行会产生与本批无关的生成/导入风险。
- Luban JSON/C#：本批未生成、未手改、未导入。
- Unity 编译/domain reload：未运行；当前 `UU` 文件状态不足以宣称可编译。
- `Assets/Scenes/PresentationDebugScene.unity`：本批未启动预览。
- 正式入口、MapAction、真实检定、结果一次性、取消/恢复、存档、DayFlow、演出隔离：本批均未运行。
- 本批没有失败复测记录；以上项目状态统一为“未运行或被阻塞”，不标为通过。

## 下一事件与最短续做步骤

1. 由当前共享代码所有者先合并 5 个 `UU` 文件，完成一次 Edit Mode 编译/domain reload；保留 oct07 现有分页、信件、预览和经营改动。
2. 复查 A-CAP01 是否已通过代码与测试证据解除；若未解除，只做 A01 正文/来源/隔离预览所允许的增量，不注册正式多轮根事件。
3. 由总控提供 A-B010 的统一 `colette` 对象与关系 Bar 方向/初值/上下界，或确认现有实际键；没有该证据不得把 +1/+2 写入样例关系或默认0。
4. 在共享表可安全串行写入后，先只施工 A01：按 `source-map.md` 逐段配置 `neutral.1008.s1`，挂现有 `market`，不创建“大堂区”新 positionKey，不加入采购扣款/物品结果。
5. 运行 `Assets/ConfigTables/GenerateAll.bat`，记录真实退出码、引用检查和 60正文/3选项的完整性；编译后用 `Assets/Scenes/PresentationDebugScene.unity` 预览，再测 day3 market 的 A、B(language1)、B(language2)、C、取消和重试。
6. A01 本批实测闭环后，按真实前置再处理 A02/A03；在 A-B002/003/004/005/006、A-CAP02/04/05 未解除前，A04/A05 只保留隔离预览或继续正文准备，不注册会卡日程的正式必做。

本记录只描述本次实际核对与未施工边界；不把 A-neutral 宣称为已实装或整包通过。

## 续做：2026-10-16 02:50–02:54 +08:00

### 当前交付状态

共享5个UU已由其他工程解除；本次扫描Assets/Scripts没有冲突标记，不再沿用首轮“存在未合并冲突”的当前判断。当前新增行首speakerNameKey及choice/anyChoice/not的局部页条件读取可复用，但SelectChoice仍只有单activeAbilityCheck，未证明多节点快照与条件结果事务已完成。关系量表A-B010仍缺统一定义；因此没有注册A批正式根或必做入口。

本次推进了全部可独立的源文本入库：**354正文+20选项=374条**。这是Text配置交付，不是可播放预览或正式事件交付。

| 事件 | Text实际行范围 | 数量 | 当前运行状态 |
|---|---|---:|---|
| neutral.1008.s1 | 1722–1784 | 60正文+3选项 | 未配置根/分支/关系，未运行 |
| neutral.1010.s1 | 1785–1839 | 52正文+3选项 | 时序/别名/经济仍阻塞，未运行 |
| neutral.1010.s2 | 1840–1912 | 67正文+6选项 | 两轮路由/关系/时序仍阻塞，未运行 |
| neutral.1013.s2 | 1918–2023 | 100正文+6选项 | 智慧2/3与经济/全天经营仍阻塞，未运行 |
| neutral.1018.s1 | 2024–2100 | 75正文+2选项 | 品质/费用/报刊接口仍阻塞，未运行 |

1913–1917为并发工程写入，本次重新加载源表后保留，没有覆盖。以上行范围是写入时实际定位，后续以稳定key为准。

### 本次改动与证据

- `Tools/Narrative/import_neutral_text.py`：按单事件读取同一source-map、校验DOCX哈希和完整源段、剥离开头角色/表情或选项编号/外引号/检定注记；以当前源表哈希检查并发变化，只追加缺失Text，保留来源完整原文批注。A05两项标签为“需要宣传”与“不需要宣传”。
- `Assets/ConfigTables/Data/Shared/Text.xlsx`：仅本批374条Text；key为`neutral.<date>.<sN>.text.pNNN`。不复制正文到C#，未改原稿或其他窗口规格。
- `Assets/Settings/Resources/Luban/tbtext.json`：仅官方bat输出，未手改。
- 本统一报告。

备份/候选/manifest分别位于：

- `Library/Locus/tmp/neutral-content/20261016-025030-137813-1008/`：A01首写63条，6884个既有cell值/类型/样式/批注保持。
- `Library/Locus/tmp/neutral-content/20261016-025134-882380-1008/`：仅修正本次首写的3个选项标签，7133个其他cell保持。
- `Library/Locus/tmp/neutral-content/20261016-025217-365290-1010.s1/`：A02 55条，7136个既有cell保持。
- `Library/Locus/tmp/neutral-content/20261016-025227-559743-1010.s2/`：A03 73条，7356个既有cell保持。
- `Library/Locus/tmp/neutral-content/20261016-025237-558840-1013.s2/`：A04 106条，7668个既有cell保持，含其他工程新增行。
- `Library/Locus/tmp/neutral-content/20261016-025247-014794-1018.s1/`：A05 77条，8092个既有cell保持。

### 本批实际验证

1. 02:51:47–48：`MSYS_NO_PATHCONV=1 cmd.exe /d /c 'Assets\ConfigTables\GenerateAll.bat'`，A01源增量官方生成 **exit0**，Player semantic passed、Luban strict validation完成，仅tbtext覆写。
2. 02:53:06–07：同一官方命令全批生成 **exit0**，Player semantic及Luban validation通过，仅tbtext覆写。D报告的旧speakerNameKey缺列表门禁已由当前共享工程修复，本次未替其改schema或Narrative。
3. 最终只读验证：五份DOCX SHA与施工包一致；完整 **527/527源段**按P编号和段内换行逐字匹配source-map。374条显示文本按授权剥离规则与最终tbtext.json逐项一致，覆盖63/55/73/106/77条。
4. 只读协作复核发现原施工器仅检查显示段及全句表情删除范围过宽，已改为全源段核验和仅行首角色元数据剥离。修改后的清理结果与全部374条生成文本仍一致。

### 失败、修复与复测

- 首次施工器用过滤结构段计数误得84对63，在写表前拒绝；改为源编号对照，最终进一步扩展到527全段校验通过。
- 姓名后表情导致前缀未去掉，在写表前拒绝；修正行首解析后成功。
- A01首次Text选项曾保留外引号/检定注记；在官方生成前，仅修复本次新增3个单元格，最终生成和逐条比对通过。
- 最终JSON核对首轮错误读取`content`字段，KeyError只读失败；改为实际`text`字段，374条比对通过。

### 03:00之后施工与验证

- A01 已追加隔离配置：`neutral.1008.s1` 保留 `blockedBy=A-B010-relationship-definition`，无 Result；补齐 8 页、60 行、3 选项、Inline language 检定及两个显式背景占位。官方生成 exit0。
- A01 在 `PresentationDebugScene` 的真实 UI 链路通过：低语言路由、language=2 成功路由、第一组选项按钮回调、完成回调、取消后面板/预览释放均通过；截图证据在 `Library/Locus/Screenshots/locus_game_20261015_190843_415.png`。正式事件仍被 blockedBy 保护。
- A02–A05 已按源图逐根写入隔离配置，共生成 Event/PresentationIndex/SetupParams、对应 ConversationPage/Line/Choice；未创建 Result、EventDate、Action。施工器在发现共享 Text 批注和 Excel 空值归一化时修正为保留既有批注/样式并继续 CAS 校验。官方 `GenerateAll.bat` 最终通过：Player semantic passed，Luban strict validation exit0。
- Unity 导入回读确认五个 A 根都存在且仍带 blockedBy。A02–A05 隔离入口已加入白名单和 dependency fixture；实际启动目前被共享配置错误 `ConversationPage rose.1016.s1.wisdom: checkId requires PassiveCheck` 阻塞，未将该并发窗口错误归因于 A 批，也未绕过校验。
- 期间修复了缺失的 `character.guest_a/b/c/d` 与 `neutral.unknown` Object 引用；严格生成曾因此失败一次，补齐后复测通过。A05 选项标签 P069 的源图类型解析也已修正。

### 当时未运行与剩余门禁（已由13:xx续做更新）

03:00阶段 A02–A05 被共享 `rose.1016.s1` 校验挡住；该错误随后由其他窗口解除，不再是当前阻塞。当前结果见下方续做。所有正式 A 根仍受 `blockedBy` 保护，业务定义未定前不放行。

## 续做：2026-10-16 13:xx，隔离配置与真实UI验收

### 当前交付边界

五篇均已配入 NCS 原有 Event / ConversationPage / ConversationLine / Choice / Inline Check 链路，并在 `Assets/Scenes/PresentationDebugScene.unity` 运行真实 DialoguePanel 和选项按钮。五篇共 **354正文、20选项、374文本**，不是把选项标签重复算作正文。

| 事件 | 正文/选项 | 本轮已验证内容 | 正式状态 |
|---|---:|---|---|
| `neutral.1008.s1` | 60/3 | A、B语言1、B语言2、C四路；选择后取消三路；day3隔离预览 | 关系A-B010未定；无正式Action |
| `neutral.1010.s1` | 52/3 | 五路，智慧2/3、语言1/2的真实Inline结果 | 时序/别名/经济/关系阻塞 |
| `neutral.1010.s2` | 67/6 | 稳态属性下全部12路，两轮检定独立；拒绝不进接待或q2 | 时序/正式前置/活动对话等阻塞 |
| `neutral.1013.s2` | 100/6 | 候选智慧门槛2共26样本、门槛3共27样本，各含无赏识5样本 | 2/3仍未定案；经济/正式营业续接未验收 |
| `neutral.1018.s1` | 75/2 | 品质成功、品质失败接受/拒绝三路；显式资金不足禁用接受 | 品质/广告费/下一刊接口未定 |

这里的完成回调和“已验证NCS结算”日志只属于独立内存调试会话。根没有 Result、EventDate、Action，不发关系、金钱、经营奖励、报刊委托或正式完成事实。正式状态污染的完整存档/DayFlow验收未做：调试场景正式Runtime未Ready，空快照不能证明正式存档隔离已验收。

### 本轮实质修复

- `Tools/Narrative/configure_neutral_remaining.py` 修复17条误作正文的选项行，保留原物理行、Text、Choice、批注/样式。A02–A05正文回到52/67/100/75。补全独占选择门控，先选择短路再读check；A04 visit整段要求显式赏识；q2B通过/失败由显式候选样本分流；A05只有advert_offer挂广告组，closing无条件。P089记者旁白不再被误识为角色台词。
- `Assets/Scripts/GameFlow/UI/Presentation/NarrativeChapterPreview.cs` 保留调用方语言/智慧值，移除覆盖属性3和猜成功的fixture；A04必须明确赏识样本，A05必须明确品质样本；账目/资金依赖缺失时诊断，不向正式Condition注入未知key。资金不足样本中广告接受禁用。A03正式为MorningBeforeMap且没有EventDate，隔离副本改Immediate后可实际启动；正式源入口不变。
- `Tools/Narrative/repair_neutral_stage.py` 只补五个原文退场后的清槽：A01 P083右；A02 P074右；A03 P107/P112右；A04 P135左。A04保留右槽阿黛尔，A03未在仍说话的P037之前清场。
- `Assets/Resources/Dialogue/NarrativeStageVisuals.asset` 登记市场、杂货铺、傍晚三个显式占位；咖啡馆昼复用`oct07.cafe.day`。傍晚保持注册表默认占位色，不自定正式调色。音效尚未匹配，柯莱特/记者及缺失表情差分仍有资源待补。

### 源表安全与生成

- 分支修复对既有38,467格逐格回读：仅140个授权值变化，其余38,327格不变；其中非本批32,623格全不变。Text SHA未变；物理行列、舞台字段及所有既有元数据保留；四根二次apply均byte-stable。51项静态路由/短路检查通过。
- 分支修复证据：`Library/Locus/tmp/neutral-content/20261016-131148-379411-A05-repair/final-verification.json`。
- 五格清槽补丁：其余38,462格签名不变，metadata和docProps/core.xml保持；二次apply无写入，旧SHA拒绝。证据：`Library/Locus/tmp/neutral-content/20261016-132101-340516-stage-repair/repair-evidence.json`及`idempotency-evidence.json`。
- 13:14官方生成exit0；13:22清槽后再生成exit0，Player semantic与Luban strict validation通过。命令为`MSYS_NO_PATHCONV=1 cmd.exe /d /c 'Assets\ConfigTables\GenerateAll.bat'`。对应日志`Library/Locus/tmp/neutral-generate-final.log`、`neutral-generate-stage.log`；没有手改生成物。
- Unity ForceUpdate导入后Loader成功回读69个events及354条中立正文；本轮Edit Mode编译检查为up_to_date，无编译错误。

### 运行证据、失败与复测

1. A01四路及逐页/行数通过：`Library/Locus/tmp/neutral-a01-runtime.json`；清槽后四路再次通过：`neutral-final-a01.json`。三种选择后取消均不触发完成、不标记happened并释放panel/preview，见`neutral-a01-cancel.txt`。
2. A02–A05共73候选样本，测试器验证精确page/sourceLine序列、选项按钮、check ID/次数/值/阈值、未选检定不执行、完成一次、调试结算日志一次、自然完成及Dispose后panel/presenter/pause释放。脚本/独立矩阵为`Library/Locus/tmp/neutral-matrix.cs.txt`、`neutral-matrix-cases.json`。
3. 首轮61/73通过，12项全为A03空入口批次；修复隔离Immediate后12/12通过，见`neutral-matrix-first-results.json`、`neutral-matrix-a03-retest.json`。这是入口修复，不是修改正式日程或通过假accepted fixture绕过。
4. 清槽后最终回归：A02/A03/A05共20/20；A04门槛2首次23/26，门槛3首次26/27。失败为偶发停首句或末句、回调未完成；四项单独复跑4/4通过。最终73个样本均有当前版本通过证据，但**连续批跑的偶发停顿未定位，不能写成73/73单次稳定全绿**。证据：`neutral-final-a02-a03-a05.json`、`neutral-final-a04-threshold2.json`、`neutral-final-a04-threshold3.json`、`neutral-final-a04-failures-retest.json`。测试推进曾由WaitFrames改WaitSeconds(.01)，仍有一项首次停顿，不以修改等待方式宣称根因已修。
5. 十一项负向检查全部通过：缺A04/A05必需样本拒绝，A05资金不足接受禁用/拒绝可完成，A02/A05第一轮后取消、A03/A04第二轮后取消均done0且未happened，四根新建重试从根首句开始且无旧check，pause回到基线。证据：`Library/Locus/tmp/neutral-negatives.json`及同名cs.txt。
6. 六条代表路径实测舞台全部完成，五处清槽、A04左安托万/右阿黛尔、A03和A05未知名→实名切换均通过，见`Library/Locus/tmp/neutral-stage-runtime.json`。该版安托万Object未绑已有图，slot身份正确但portrait不可见；随后资源/表情补丁及复测见后续追加。

### 仍需定案与未覆盖项

A-B010三人正式关系方向/初值/范围缺来源；A-B002/003/004促销别名、日期及日内时序未定；A-B005准备成本/活动收益缺基数、数值和真实消费回执；A-B006智慧2/3未定；A-B007真实记者订单品质阈值及有效交付口径未定；A-B008广告费/币种、A-B009下一刊正文/排期/消费者和A-B011人物活动对话目标未定。这些不能用0、自动成功、仅放拒绝路或完成布尔替代。

真实市场Action/AP、正式入口日期边界、关系/钱/经营/报刊事务、真实记者制作续接、存读档恢复，以及旧oct07/其他窗口完整回归不在上述隔离UI通过范围。共享核心代码和其他批次未提交改动保留；本报告只归属A批实际变更与证据。

### 13:35–13:40资源与结构表情收尾

- `Tools/Narrative/repair_neutral_emote.py` 仅补 A04 P007开心、P090平静、P145疲惫三格emote；五份Docx SHA和527段源文再次逐项核验。38,464个非目标单元格精确value/type/style/comment/link不变，其他ZIP payload不变；二次apply及过期SHA拒写通过。证据目录：`Library/Locus/tmp/neutral-content/20261016-133548-149398-emote-repair/`。
- `Tools/Narrative/repair_neutral_visual_bindings.py` 补 A01 P023慌张、A02 P024开心、A03 P011平静；另将Object antoine.portraitPath绑定现有`Characters/antoine/neutral`。Unity已确认原PNG为Single Sprite且Resources.Load成功，没有新建/复制图片或改GUID。该批仅4格变化，其余38,463格和工作簿/Sheet元数据不变，二次apply无写入。证据目录：`Library/Locus/tmp/neutral-content/20261016-133652-542222-visual-bindings/`。
- 首次四格补丁因源证据误用柯莱特短名而在写入前拒绝，改为源文全名“柯莱特·布吕内”后成功；没有绕过源校验。
- 最终Narrative SHA：`7267a7b30afaf90dca441d70a1cfe2b2124a68041da8031cb5410cb20892cfc7`。13:37官方生成exit0并导入，日志：`Library/Locus/tmp/neutral-generate-visual-final.log`。Loader仍为69 events、354条本批正文；五个根均保留blockedBy，无本批Result/EventDate/Action。
- 资源补丁后，前述四项偶发失败样本4/4完成；六条舞台路径再跑6/6完成，所有目标清槽/身份断言通过。A03 P011右槽安托万、A04 P090左槽安托万均实际可见；A04 P135只清左、右侧阿黛尔保留。证据：`Library/Locus/tmp/neutral-visual-final-a04-diagnostics.json`、`neutral-visual-final-stage.json`。
- A04 P090截图：`Library/Locus/Screenshots/locus_game_20261016_054006_132.png`。共享场景真实显示左安托万、右阿黛尔、咖啡馆昼景及正确台词/姓名；抓图后已取消并释放panel/pause，恢复调试说明。
- 六条结构表情已准确入表；缺少的表情差分仍回退同人基准图并有`PortraitFallback`诊断，不将六个emote字段修复称为六套美术已完成。柯莱特、记者、市场/杂货铺/傍晚背景与音效仍有占位。
- 立绘绑定后的连续A04候选门槛2批跑 **26/26全通过**（精确page/line、check、完成一次、panel/presenter/pause自然释放），证据`Library/Locus/tmp/neutral-visual-final-a04-batch.json`。带入场/退出诊断采样的该批未再现停顿。门槛3本轮使用既有27样本证据加四项针对复测；没有把候选2的通过视作正式门槛定案。
- 最终生成配置原样建立独立Controller检查五根正式门禁，**5/5拒绝**，A01 day3日期条件已通过而仍被A-B010拒绝；没有移除正式blockedBy。证据`Library/Locus/tmp/neutral-final-formal-guards.json`。这证明配置门禁，不等于正式Runtime入口/存档验收。
- 同一最终资源版本十一项缺输入/资金不足/取消/新建重试负向检查 **11/11通过**，证据`Library/Locus/tmp/neutral-visual-final-negatives.json`。取消后panel不播放、pauseOwner回基线。
- 偶发停顿的只读复核确认首句依赖Panel Update/effects.Tick、末句依赖DOTween完成回调；reading pause残留是潜在机制，尚无本轮现场证据命中，未改共享Panel核心也未宣称已修复。再次遇到时应采集transition active/playing/elapsed、effects.IsPaused、readingPauses、外部输入门、真实elapsed和中断回调，不能只增大上限或以单跑通过掩盖。

## 续做：用户补充决策后的正式迁移（已完成本批接线与有界验收）

### 已确认规则，取代上方历史未定项

- 三位本批角色对玩家的正式关系：范围−2～5，默认初始0；仅新增柯莱特、阿黛尔、安托万，不调整现有示例NPC。
- A02/A03保留10月10日同日先后，S2紧接S1；促销保留10月13日，“两天后”改“三天后”，少量时间措辞按用户许可调整。原DOCX/source-map继续作为不可变来源，展示文本另留before/after。
- A04智慧门槛确定为2；赏识事实控制安托万后篇到场/不到场，不挡住整篇，也不虚构一个空人物活动解锁。
- 准备成本倍率0.8、促销收入倍率1.2做可修改GameParam。**用户尚未确定具体作用哪笔支出/收入，故本轮不接真实账本、不扣减或增加任何对应金额。资格flag也不代表经济效果已消费。**
- 记者品质直接复用真实经营判定：饮品种类正确、`score.total >= ProductSpec.minimumScore`（当前espresso为60）、满足当前经营配置要求的拉花条件；不能用豆品质、普通顾客杯、自动出餐或重做中分数代替。
- 广告沿用单一经营货币，暂定100，由配置统一驱动余额门槛与最终扣费；成功推荐和失败后接受广告分别提出下一刊请求，报刊展示沿用既有Notice而非新增报刊引擎。

### 已核查的实现边界

- A02必须依赖`story.rose.1009.s1.completed`，不能以`story.oct09.meeting.known`代替。当前源表B密见仍有`blockedBy=B-B01+B-B02`且无本根完成Result/正式Action；因此A批测试必须把B完成标为fixture，不能声称从B自然营业链可达，本轮不越权替B写完成事实。
- A04采用早间根→WaitBusiness→下午段→WaitNight→晚间根尾事务。当前AfterBusiness是在一轮经营结束后回调，不是仍可交付订单的营业中途回调；报告不能称为中途来访。
- 记者独立采用CafeVisit的arrived/auto hook：从开店后累计经营时间150秒到达（当前300秒营业的一半，属可调中段映射，不是真实钟点）；对话后WaitService释放arrived握手，等待确切visit/order的最终真实杯交付回执续播。
- 记者世界资源原为空，会被CafeVisitBridge拒绝到访。已通过Unity AssetDatabase复制现有通用顾客占位至`Assets/Resources/Characters/neutral_reporter/placeholder.png`，仅新副本改Single导入，原资源未改；Resources.Load<Sprite>已通过。后续将仅用于记者世界占位，不宣称正式记者立绘完成。
- 本批源表/生成/编译/正式验收结果见下方最终记录；前述隔离UI结果仍属于历史证据，不能代替正式链验收。

### 正式交付内容与源表安全

- `Tools/Narrative/configure_neutral_formal.py` 已完成A01–A04：三条真实关系Bar、17个根尾Result、23个结果条件、市场0AP一次必做Action、A02/A03同晨日期优先级100/90、A04两次无条件checkpoint与正式赏识事实、智慧2。90新增行、36修改单元格；648静态源表场景通过。完整行号及15处时间措辞before/after见`Docs/superpowers/reports/2026-10-16-A01-A04-formal-source-tables.md`。
- `Tools/Narrative/configure_neutral_reporter.py` 已完成A05：day13早间预约/Spawn事务、指定espresso真实制作来访、arrived/auto正文、WaitService、推荐/广告互斥请求、广告余额检查及根尾支付、day14早间两条Notice、printed去重。原77个A05正文/选项Text与源DOCX一致，全部原舞台/表情保留。
- **WaitService实际接缝已修正为arrival→wait_service→serve**：P013点单后等待真实制作，P014上杯/尝味文字在交付后才出现，未采用早期讨论的serve后等待。
- 广告费单源是GameParam `neutral.reporter.advertisement_fee=100`。修改该参数后，重新运行记者脚本plan/apply，将同一值同步到`Choice BAR_GTE cafe.money`与`MONEY_REMOVE target=cafe`再官方生成。脚本已验证100→237只改变两消费者且不覆回参数；拒绝小数、旧SHA与无plan写入。余额在选项与根尾实际账户preflight各检查一次，支付失败不消费完成、请求、once，允许原回执续播重试。
- 两个脚本都经备份、候选逐cell回读、工作簿/Sheet/cell元数据保护、源证据SHA与CAS、磁盘二次apply byte-noop。一次并发CAS拒写确认仅CafeVisit两新列，吸收新基线后安全应用，没有覆盖并发目标值。没有直接修改生成JSON/C#。
- schema白名单新增`WaitService`、Condition `result` owner、`PRESENTATION`，CafeVisit末尾增加`orderProductId`/`requireRealProduction`；保留其他批次schema改动。354正文、20Choice不减；本批根前缀Text由374变378（新增本批报刊标题/正文及printed标签匹配前缀），另有ConditionKey显示标签。
- 最终源表SHA：Narrative `dc83ded4a7a82aea5eaa7a460694a9a2295f0b27555de7c686a3215a0f67ea20`；Text `55205f3dc74cb794c30f0ccd3490faa1762aa801567bce82902ced6ec048081a`；GameParam `a6bd3bac57db4691ad691eb61cb9e78e9430eb092d832bc73307cb40dd94ec2a`。schema `d21370534f18bd5de1065856944ef77d7f94e869b7ebe1b729a51db2f49e9fd3`。
- 16:30、16:40两次官方`GenerateAll.bat`成功，最终日志`Library/Locus/tmp/neutral-generate-formal-final.log`。Unity完整编译/domain reload通过，Loader回读72 events，五根blockedBy已移除，前三种入口分别为MapAction / MorningBeforeMap / 记者hook Immediate；记者世界占位Sprite实际可加载。

### 通用代码与实际修复

- 根尾条件Result使用同组AND/跨组OR，优先选择短路，Inline只读已访问缓存；unknown经not或value=false仍拒绝。多个检定收益在同一根尾事务一起提交，取消和旧回调不重复提交。
- CafeVisit hook为request携带exact visitInstanceId/characterId/orderId/productId；含WaitService根没有有效上下文时入口直接拒绝，不按角色猜订单，不允许普通手动Queue绕过。真实经营最终交付才产生持久receipt；重做、自动服务、未服务离店不确认。WaitService释放到店握手并让经营继续，续播读取同一经营satisfied判定，不重新计分、不再次收经营收入。
- 服务checkpoint可存读、缓存最终回执、取消后显式`NarrativeRuntime.ResumeServiceCheckpoint()`重试。未交付离店清理待根、不写once、放行夜间入口；未服务已离店不接受迟到新回执。
- **真实Unity先验收发现9项失败**：JsonUtility将null自定义class展开全默认空壳，导致未服务存档恢复被拒与checkpoint无法接回执。已在Narrative复制链、checkpoint克隆、domain visit/feedback恢复处将严格全默认对象归一为null；任何非默认字段/部分身份不会被吞。修复后真实Unity60/60通过，不能用先前离线47/47掩盖此差异。
- 正式Loader还发现result-only调度的entry/blocksSave被RequiresPresentation误当演出payload；现仅排除entry/blocksSave/blockedBy三控制属性，保留其他payload校验，schedule可无空演出同步结算。
- `NarrativeChapterPreview.Neutral.cs` 保持五篇预览可用：仅在新Load的隔离副本去本根Results/EventDate/前置及等待页，用显式样本读品质，不影响正式表或Runtime。正式智慧检定仍按配置2执行。无赏识样本保持初始false，不写违反onlySetTrue约束的false。

### 最终验证与明确限制

- 工作区Unity Test专用工具禁用。以下纯同步NUnit方法由真实Unity已编译测试程序集反射运行，含Setup/参数展开，**不是Unity Test Runner完整生命周期运行**；未改设置启用工具。
- A01–A04正式配置 **36/36**：实际Luban+Controller+fake presenter，覆盖日期拒绝、A01四路及取消、B completed与meeting-known区分、A02/A03同晨顺序、多检定独立收益、正式关系、A04无赏识分支、智慧/语言边界、Business/Night token、取消及缓存恢复。证据`Library/Locus/tmp/NeutralFormalAcceptanceTests-final.txt`。
- A05正式配置 **15/15**：实际CafeWorld从豆→研磨→热水→精粹萃取→杯→交付产生回执，经真实outbox送Controller；合格推荐、重做后成功、耗尽重做失败、广告接受/拒绝、余额不足禁用、尾款不足与提交竞态整根重试、day14两Notice互斥/printed去重。A04 completed是fixture，未合成品质回执。证据`Library/Locus/tmp/NeutralReporterAcceptanceTests-final.txt`。
- 服务/结果/旧来访/Business-Night恢复回归 **60/60**，含严格空壳逐字段负例，证据`Library/Locus/tmp/neutral-service-unity-null-fixed-tests.txt`。修复前48pass/9fail保留在`neutral-service-unity-reflection-tests.txt`；不能删除失败历史。
- 共享`PresentationDebugScene`真实Presenter+DialoguePanel：A04智慧2且赏识=true，morning→organize_pass→afternoon→visit→accounts_pass→visit_tail→closing，Business/Night各一次，根完成一次，两检定成功；业务token使用实际CafeWorld Open→Night。证据`Library/Locus/tmp/neutral-formal-a04-ui.json`。
- 同场景A05两条真实UI路径通过：合格espresso score100/minimum60→推荐；真实水杯score0/类型不符→广告拒绝。真实交付前只播arrival，WaitService后才播serve；根完成各一次、无自身panel/presenter/pause残留。证据`Library/Locus/tmp/neutral-formal-a05-ui.json`及`neutral-formal-a05-ui-failed-cup.json`。A04完成仍为fixture，测试使用独立Controller/CafeWorld，没有改正式Runtime singleton。
- 五篇隔离预览兼容回归各一条代表路径 **5/5**，证据`neutral-preview-formal-compatibility.json`与`neutral-preview-formal-a04-a05-compatibility.json`。测试器一次索引在LINQ predicate中递增出错，修测试器后重跑通过；不是正文选项错误。
- **共享Editor并发干扰需留痕**：正式A05 UI早期按全局pause/panel断言失败，现场查到不属于本测试的BusinessEndPanel/NarrativePresenter lease。后改为验证本次presenter.pauseOwner及本根source释放，另外记录全局foreignPauses；最终基线/结束全局均为1，但不宣称全Editor无暂停。Console最终仍有来自`NarrativeTerminalFadeTests.SetUp`的“已有对话”6次合并错误，堆栈是另一并发测试；本批未清日志或改其代码，也不能宣称Console零错误。
- 相关源码`git diff --check`通过（仅Git CRLF提示），最终退出Play Mode回Edit Mode。所有既有工作树与其他批次修改保留，未提交Git。
- **尚未处理**：成本0.8/收益1.2实际作用哪笔账仍按用户要求暂缓；B密见自然完成事实仍缺，因此从新档B→A02→A05整条自然DayFlow/存档链未验收。本轮不替B放行。A04下午段当前在经营endpoint结束后；A05 150秒仅当前营业中段参数；day14 Notice是暂定下一刊排期。正式记者立绘、柯莱特资源、部分背景/表情/声音仍为占位。旧隔离批跑偶发首末句停顿未复现定位，未声称修复。
