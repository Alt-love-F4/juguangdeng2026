# B 新玫瑰工程交付与续做记录

本记录从2026-10-16 02:27首次Text增量持续追加，保留既有用户及其他Agent改动，未提交Git。**最新进度以文末增量为准；下方首次状态表属于历史快照。**

截至本轮收尾：276条Text、四篇已写范围隔离预览、1011同根Reading、1011/1012/1016角色舞台、day7正常营业安全入口及逐次真实制作事实回执均已施工并有分层验证。正式四根仍blocked且无Result生产者，真实制作接管/两杯归属、B的评级与订单及未决剧情合同仍未闭合，不能称正式全链完成。

## 首次状态分栏（02:27–02:38历史）

| 事件 | 已实现 | 已生成 | 本次已验证 | 待验证/阻塞 |
|---|---|---|---|---|
| rose.1009.s1 | Text 68条，含两选择文本、混合段拆分和独立尾文；未配正式根/路由/人物/地图 | 官方 tbtext；不是事件生成 | 原稿76段及SHA、68条生成文本逐项一致；旧单元格保持 | B-C01/01a及核心合并；B-B01尾部检定、B-B02真实挂点；无正式或预览播放 |
| rose.1011.s1 | Text 128条，含11项选择、混合段、法中名片；未接制作/阅读/路由 | 官方 tbtext | 原稿140段及SHA、128条生成文本逐项一致；P129两换行保留 | A/B完成未实现；B-B03/04/05；B-C01/02/04/07；无运行验收 |
| rose.1012.s1 | Text 71条，两历史路径正文保留；未接营业入口/制作 | 官方 tbtext | 原稿86段及SHA、71条生成文本逐项一致 | B/D完成未实现，帽子事实契约冲突；B-B05、B-C02/03；无运行验收 |
| rose.1016.s1 | 仅已有前段Text 9条，P016/P017没有伪造正文/结尾 | 官方 tbtext | 原稿17段及SHA、9条生成文本逐项一致；无正式触发/结果 | B-B06缺稿/B-B07身份前置/B-B05品级/B-C05楼层；尚无隔离可播放预览，继续禁用 |

本次未新增任何 B Event/EventDate/Action/ConditionKey/Result、未写 completed/appreciation、未创建平行剧情标准，故没有可以在地图点击或GM播放的 B 根。仅存在 Text 不会触发 NCS；不得用 NarrativeChapterPreview.Play("rose.…") 复现当前增量，现有预览查找根会失败。

## 既有工程验证证据（来源与范围）

完整读完 AGENTS.md、B spec/source-map/handoff，A/D spec和依赖handoff，以及当前两个 october07 报告。适用技能读取 `.agents/skills/yokiframe/SKILL.md`、`references/tablekit-luban.md`；TableKit settings 的 LubanSkillsPath/Agent/MCP 为空，实际配置路径指向缺失的项目 Luban目录，未据此改设置。发现并使用官方 `D:/Luban/ai/skills/luban-excel-fill/SKILL.md` 与 `luban-generate-debug/SKILL.md`，生成遵循项目 bat 的真实 D:/Luban 安装。

- `Docs/superpowers/reports/2026-10-15-october07-acceptance.md` 当前69行完整版本：S1/S2最终已验收；记录真实地图/S2两属性分支、信封读信、日结day3、隔离存读档及共享场景回归，最后直接调用59/59通过，明确不是完整UTF runner。这是 oct07 历史工程证据，本次没有重跑。
- `Docs/superpowers/reports/2026-10-15-october07-content-map.md` 当前283行完整版本：旧源表、生成及逐段定位，后续交叉引用parent验收；不能算rose验证。
- 接手首次reports只有三份旧报告，02:37复查出现 `Docs/superpowers/reports/2026-10-16-A-neutral-implementation.md`，已完整读97行。其当前结果为A01–A05未配表/未生成/未运行；不得将A02完成视作可用。
- B source-map 的319段历史静态核对只证明文档覆盖；本次另执行原稿与待写Text/生成数据核对，仍不证明游戏路径。

## 跨批次依赖及总控合并项

| 事实 | 当前实际证据 | 写入时点/状态 | 对B影响 |
|---|---|---|---|
| story.oct09.meeting.known | Narrative ConditionKey第29行、Result第74行；生成结果同ID；Controller.CompleteEvent汇总结果提交；oct07最终验收第44/62行 | oct07.s2完整P047后与story.oct07.s2.completed提交；历史运行报告已覆盖，本次未复测 | 1009上游输入可复用；不是B会面完成 |
| story.neutral.1010.s1.completed | A spec §4/handoff §2约定，A报告当前明确未施工；本次源表和生成数据无该key/root | A02 P076后完整根提交的拟议契约；尚无实际生产者 | 1011正式资格不成立，不以day6或meeting代替 |
| story.church.1009.s2.completed | D spec §1/handoff §4.2拟议；源表/生成无该key/root | D P050后拟提交；未实现、未验收 | 1012所有路径前置不可伪造 |
| story.church.1009.s2.hat_helped | B handoff §2/§4拟议；D spec第26行明确“不…永久事实”，D handoff §4.2只输出completed | **契约冲突**：B要求选①且全根完成提交帮助事实，D不承诺永久事实；无实际配置/代码写入 | 总控须与D统一权威历史读取或根尾事实合同，不能由B双写、不能把未知当false |
| story.rose.* | 本次检查Event/EventDate/Action/Result/ConditionKey生成记录均无B根 | 尚无正式生产者，本次不提前注册完成键 | 下游A/D/B不放行 |

附带身份差异：B handoff §2记Catalog萨德“监察司”与源人物“教务司”差异，保持交总控核对；本次未改Catalog或身份。埃莉诺沿alienor，贵族部下不可借bertrand身份，此次Text没有新增姓名对象。

## 当前工作树及公共能力核对

`git status --short` 初始已有大量暂存/未暂存改动，以及以下5个UU文件：

- Assets/Scripts/FreeCafe/Unity/FreeCafeRuntime.cs
- Assets/Scripts/GameFlow/UI/PlayerCafeOwner.cs
- Assets/Scripts/GameFlow/UI/Presentation/NarrativeConversationEventPlayback.cs
- Assets/Scripts/GameFlow/UI/Presentation/PresentationDebugSceneController.cs
- Assets/Scripts/Narrative/NarrativePresenter.cs

正文冲突标记实际出现在 FreeCafeRuntime（141/154/909起）、PlayerCafeOwner（12/31/80起）、NarrativePresenter（272–399）。Presenter同时保留两套有重叠的分页路径，冲突正覆盖B需要的正式演出入口；本次没有按ours/theirs裁决或改Git index。另两UU即使工作文件无标记，也不代表索引已合并。

| 编号 | 当前代码/配置证据 | 本次判断 |
|---|---|---|
| B-C01/01a | NarrativeController.SelectChoice325起仍单activeAbilityCheck；Presenter.PageVisible662起只读正式bool，bars抛Unsupported；冲突分支先继承背景再过滤页 | 未补齐。须统一根内临时路由、当前组校验、可见页舞台继承，不能先写flag |
| B-C02 | enum仍只有Dialogue/PassiveCheck/LetterReveal/LetterRead，当前源表无制作步骤 | 未找到合法同根制作返回端到端证据；不接假成功 |
| B-C03 | 当前源表/生成无B营业事件；现有CafeVisit不自动证明首次营业ready | 未验证真实营业安全入口 |
| B-C04 | Presenter.ReadingPages649起明确单callback将读完/取消同作完成；无通用Reading conversation step | 未补齐，不可直接用名片阅读结算 |
| B-C05 | 既有包提示的money-only/provider与楼层未就绪问题没有新的实现验收 | 未确认真实开放读取，不用贴图/导航当开放 |
| B-C06 | 既有Inline缓存/evaluator、真实Player属性及politics专精可复用；来源B包及oct07历史证据 | 可复用接口，B threshold6/5/7尚未配置/实测 |
| B-C07 | schema ConversationLine.side第225行仍left,right,none | center尚未开放；未动共享schema/目录 |

核心冲突使叠加正式分页/制作/阅读实现无法可靠验证；按用户共享串行整合要求保留现场，继续了不依赖它们的文本源表增量。没有把全部缺口归因于美术。

## 本次实际改动与验证

实际改动路径：

- `Tools/Narrative/import_rose_text.py`：可恢复的增量Text施工器，仅消费B source-map，核对原DOCX SHA和段落；按§3拆分、剥离人物和表情元数据；完整原文批注与段号落表。拒绝已有不同文本、未知B键及并发源表变化；备份/候选/manifest位于Library临时目录。
- `Assets/ConfigTables/Data/Shared/Text.xlsx`：只追加600–875行，共276条。既有行逐cell核对值、数据类型、样式、批注保持；每轮写前重新打开当前文件并核SHA，保留前一轮及并发工作。
- `Assets/Settings/Resources/Luban/tbtext.json`：只由官方bat生成，未手改。
- 本报告。

ID为 `rose.<date>.s1.text.pNNN`，指定混合段沿`.a/.b/…`，原父文本键不重复写入。1009 P036内注不展示，P038/043/051旁白拆离；1011 P052七段、P116后句才带原兴奋注的来源边界保留，名片P129两换行；1012三组混合段拆分；1016仅9段显示文本。`{drinkname}`仍保留在未引用的源Text P046，待真实订单上下文，**目前无正式页面会把它显示给玩家，不得将此行直接放行**。

| 时间(+08:00) | 实际命令/检查 | 结果 | 证据 |
|---|---|---|---|
| 02:34:06 | `python Tools/Narrative/import_rose_text.py --event 1009` | 68条，Text600–667；原稿76段核对；2396个既有cell保持 | Library/Locus/tmp/rose-content/20261016-023406-671306-1009/manifest.json |
| 02:34:17 | `MSYS_NO_PATHCONV=1 cmd.exe /d /c 'Assets\ConfigTables\GenerateAll.bat'` | exit0；Player semantic passed、Luban strict结束；仅tbtext overwrite | 本次工具输出 |
| 02:35:10 | 同施工器`--event 1011` | 128条，Text668–795；原稿140段核对；2668个cell保持 | Library/Locus/tmp/rose-content/20261016-023510-596441-1011/manifest.json |
| 02:35:22 | 同官方bat | exit0；Player semantic/Luban strict通过 | 本次工具输出 |
| 02:35:32 | 同施工器`--event 1012` | 71条，Text796–866；原稿86段核对；3180个cell保持 | Library/Locus/tmp/rose-content/20261016-023532-605816-1012/manifest.json |
| 02:35:49 | 同官方bat | exit0；Player semantic/Luban strict通过 | 本次工具输出 |
| 02:36:06 | 同施工器`--event 1016` | 9条，Text867–875；原稿17段核对；3464个cell保持 | Library/Locus/tmp/rose-content/20261016-023606-301933-1016/manifest.json |
| 02:36:21 | 同官方bat | exit0；Player semantic/Luban strict通过 | 本次工具输出 |
| 02:36后 | `unity_recompile(editor_status=editing)` | up_to_date/not_needed，domain_reload unchanged；**未实际重编冲突源码，不算本批编译通过** | 工具结果 |
| 02:36后 | Edit Mode AssetDatabase.ImportAsset(tbtext.json, ForceUpdate)并读取TextAsset | Editor解析到68/128/71/9条 | connected Unity输出；未切Play Mode |
| 02:37 | 原稿/source-map→施工器预期→最终JSON全条比对；查Event/EventDate/Action/Result/ConditionKey | 319段与276条文本静态检查通过；无B根/结果、无A/D目标事实；未误启用1016 | Library/Locus/tmp/rose-content/final-verification.json |

最终Text源SHA256：`fdac08de8bf7d7af181325f72411b1b646a7929d8169584d370e437a732afc15`。接手Narrative SHA为`5069534c5cd6ab8fe3311eb623fbb2245ba649871e433b2ed81cb53bea66272a`，本次未写Narrative.xlsx。

### 失败、修复、复测

1. 环境无rg，首个定位命令退出127；后续使用list/grep和Python，不影响文件。
2. 1009导入器初轮错误匹配后面的混合段表，引发IndexError，写表前停止；收紧匹配及六列校验，复跑成功68条并生成。
3. 1011逐段核对发现P024原稿有一个段首空格，而Markdown解析strip丢失，写表前assert中止；改为只剥一层Markdown单元格边界空格，保留源缩进；随后140段核对成功、正式显示仍按规格移除排版空格。
4. 不存在本批运行失败修复记录；没有运行分支UI，不能把以上静态修复称演出修复复测。

## 待验证/阻塞及资源占位

- **未运行**：共享PresentationDebugScene演出、真实地图赴约、真实营业入口、两分支/知觉智慧边界、制作双杯/评级/重试、名片续接、取消/迟到回调、存读档/DayFlow及旧oct07运行回归。没有新场景、Canvas或硬编码剧情预览。
- **未完成编译验证**：核心源码冲突真实存在；当前Editor可执行已有程序集和Console error=0只反映当时已加载状态，不能证明工作文件可编译。
- B-B01：1009知觉比较符/阈值、失败是否跳CG或尾文、合法终点；B-B02：布塔克俱乐部真实建筑/坐标。影响1009正式根及A/D下游；1009文本已可继续接线。
- B-B03：1011正确理由后的智慧门槛及失败出口；B-B04：真实西尔万订单饮品名称来源；B-B05：普通/优秀到真实成品评价映射。不得默认60/90或借豆品质。
- B-B06/07：1016缺稿、两杯规则/失败处理/完整收束及“王女身份”前置冲突；继续禁止正式触发和任何completed结算。
- B-R001–013仍沿handoff：会所门外/会客室/骑士CG、玫瑰王画像、纸花装饰/试饮CG、门铃等未绑定；麻雀/西尔万立绘、埃莉诺平民装/萨德及楼层尚未做本次视觉核验。没有用别人的立绘或咖啡馆冒充会所，没有新增图/音频或更改资源GUID。

## B-1 后续增量（2026-10-16 02:44–02:50 +08:00）

### 实际改动

- `Tools/Narrative/configure_rose_1009.py`：按当前并发 Narrative/schema 表头写入 B-1 前段配置；脚本具备工作簿 SHA compare-and-swap、候选备份和幂等查重。当前 schema 新增了 `ConversationLine.speakerNameKey`，B-1不填该字段，保留无立绘/显示名通过 Object 的既有解析路径。
- `Assets/ConfigTables/Data/Narrative/Narrative.xlsx`：追加/确认 `rose.1009.s1` blocked Event、6个 ConversationPage、62条 ConversationLine、2个 Object、2个 Choice、2个 Event Condition、PresentationIndex、SetupParams和三条Text辅助键。未写 Action、EventDate、Result、ConditionKey；没有地图挂点和完成事实。
- 官方生成物仅由 `Assets/ConfigTables/GenerateAll.bat` 更新，未手改。
- `Assets/Scripts/Narrative/NarrativePresenter.cs`：合并后的已改动文件中，修正 ConversationPages 的背景继承顺序，先过滤页再应用 `bgOverride`，避免未命中页污染共享舞台；选择回调增加当前 page `optionGroupKey` 校验。该共享文件仍属于并发改动，本次不裁决其余行为。

### 生成/校验/结果

- 首次生成失败：`ConversationLine` 当前 schema要求 `speakerNameKey`，初次候选工作簿按旧表头生成被官方拒绝；补齐当前表头处理后重试。
- 第二次失败：新增 Event 的 `eventKind=narrative` 不在当前 schema `actor,world` 集合；修复为 `actor` 后复测。
- 2026-10-16 02:49:18 官方生成最终 exit 0，Player semantic passed、Luban strict validation passed，生成 `tbncsevent/tbncspresentationindex/tbncsconversationpage/tbncsconversationline/tbncschoice/tbncscondition` 等输出。
- Unity Editor 导入后读取正式生成配置：`rose.1009.s1` entry=`MapAction`、`blockedBy=B-B01+B-B02`、6 pages、62 lines、2 choices、0 results、0 actions、0 event dates；`ValidateConversationSteps`返回空。`unity_recompile`返回 `up_to_date/not_needed`，未发生domain reload；不能据此宣称源码编译通过。Console错误检查此前为0，仍不替代编译/运行验收。

### B-1 当前边界

- 1009 P003–P071已进入正式配置数据，但 Event 有明确施工阻塞，不会被 `EventCanStart` 接受；没有 Action/Date/Result，因此没有地图正式入口或完成结算。P072–P076尾部仍未接入。
- 尚未执行共享 `PresentationDebugScene.unity` 播放或真实地图测试；当前 `NarrativeChapterPreview.Play("rose.1009.s1")`会加载到 blocked 根但不应接受正式入口，不能当作预览通过。需要独立隔离预览入口或总控解除B-B01/B-B02后再做正式链路测试。

## B-1 运行复测追加（约02:51–03:00，+08:00）

本段覆盖上文B-1“尚无隔离预览入口”的旧状态，历史保留。

- 当前共享核心5个UU已由并发工作合并，`git status`不再UU、grep无冲突标记。未由B裁决或回滚。当前还有其他Agent新增的教会配置及speakerNameKey能力，不把它们列为B实现或B验收。旧A/D未实装结论只是首次核对时点，正式联调前必须再读当前对方交付。
- `NarrativeChapterPreview.PlayRose1009Construction()`新增受限入口：只允许result-free且有blockedBy的1009片段，在新加载的内存配置移除门禁、设day4和meeting测试前置，正式配置保持blocked；choice/read条件接现有Controller，不写正式状态。未加地图Action，不伪造root.completed。该方法可在共享场景PlayMode由工程工具调用，尚未加键盘按钮。
- 真实编译检查首轮失败CS0079：ChoiceSelected为event，误在初始化器赋值；修成`+=`。后续unity_recompile返回up_to_date，桥接可执行包含新方法的程序集。没有完整UTF runner结果，不声称执行了全项目强制重编。
- 在 `Assets/Scenes/PresentationDebugScene.unity` PlayMode执行真实Presenter的PlayEvent迭代序列，以测试播放回调消费页：P045路径52行、P046路径56行，均仅包含所选独占正文，汇P065、末句P071；两路断言通过。使用正式生成的6页62行2选项，不是C#硬编码正文。此项是PlayMode内顺序逻辑检查，未逐句操作可视UI。
- 同次检查正式Controller在day4/meeting=true下QueueMapActionEvent(root)==false：blocked门禁断言通过；未移除源表blockedBy，0 Action/Date/Result保持。
- 可视隔离预览首轮因`rose.club.exterior`未注册报Missing narrative background，随后按handoff明确占位修复：Unity API向 `Assets/Resources/Dialogue/NarrativeStageVisuals.asset` 增量加rose.club.exterior/reception中性色及rose.club.blindfold黑色、fade .3；保留原3项和并发新增项，不借图代演。该资产已持久保存。
- 复测 `new NarrativeChapterPreview().PlayRose1009Construction()`：10帧后IsPlaying=true、Source=rose.1009.s1.p004；Dispose后3帧DialoguePanel.IsAnyPlaying=false，取消释放检查通过。未比对正式存档/完整Participants JSON，未验证暂停OwnerCount，不能宣称全部隔离验收。
- 一次EditMode直接构造Presenter检查因DontDestroyOnLoad禁止在EditMode失败，随后使用规定共享场景PlayMode重跑并取得上述结果。一次工具脚本Stack类型歧义/Action字段拼写错误只影响工具脚本，改成List/actionKey后成功，不是游戏源码错误。
- 可视UI全章双分支、背景切换/黑屏观感、资源别名、门卫中途改称布塔克、迟到回调/存读档及真实地图均未完整测试。当前Object门卫显示“男人”，正式放行前需用现有speakerNameKey按P038后的原称谓处理；不得据此称身份演出全验收。
- 结束时已回EditMode并恢复 `Assets/Scenes/启动场景.unity`，未保存或重建共享场景。

当前各事件最终口径：1009=前段配置已实现/官方生成成功/顺序双路与预览启动取消局部通过/正式全链阻塞；1011、1012、1016=Text已实现并生成、游戏未验证。1016仍无正式Event/Action/Date/Result，本次没有补剧情或结算。

## 03:21–03:33 增量续做：1012/1016配置、生成与运行复测

- 并行依赖复核：当前 `neutral.1010.s1` 已有 blocked/result-free 根但没有 completed 生产者；`church.1009.s2` 已有 completed Result 但仍受 D-B03 门禁；`story.church.1009.s2.hat_helped` 尚无权威生产者。B-B01–05未定案，未解除任何正式根门禁。共享 Excel/schema/生成/Unity仍按唯一整合入口串行。
- 新增 `Tools/Narrative/rose_write_guard.py`，B工具统一使用 `Library/Locus/tmp/narrative-source-write.lock` 的排他创建、owner校验和占用即失败；增量修正1009文本/配置工具接入该锁。新增 `repair_rose_setup_slots.py`，将B四个 `Tpl_ArrivalConversation` setup 的可选 `opts` 显式写为 `opts=`，避免 validator 报 missing slot；保留备份/CAS。
- 新增并应用 `Tools/Narrative/configure_rose_1016.py`：9条正文（P004–P015）、arrival/wisdom/order三页、智慧Inline threshold=7、独立Object sade；root保留 `blockedBy=B-B06+B-B07+B-C02+B-C05`、contentBoundary=P015，无Action/EventDate/Result/completed。加载规则发现后将 wisdom 页修正为 `PassiveCheck`，官方生成成功。
- 新增并应用 `Tools/Narrative/configure_rose_1012.py`：12页、71行、仅新增埃莉诺别名Text，复用adele/alienor/player；各页显式依赖阶段，post_brew要求明确A/H/D且D=true；root保留B-C02/B-C03/B-B05/B-history/B-fixture/B-resources门禁，无正式Result/Action/Date。官方生成成功。
- 新增 `Assets/Scripts/GameFlow/UI/Presentation/NarrativeChapterPreview.Rose.cs`：1012 preparation/brew_retry/post_brew 显式夹具预览、1016 P004–P015预览。只用于独立内存配置，禁止把真实制作、D未知、A/H未知当false，取消不结算。
- 官方 `Assets/ConfigTables/GenerateAll.bat` 在03:25与03:28串行成功（Player semantic validation passed、Luban validation passed、Unity JSON/typed C#生成）；生成期间源SHA保持稳定。Unity随后重新导入并完整compile/domain reload通过。中间发现并修正三槽行级字段在PresentationLine定义缺失的CS0117，以及1016 checkId必须PassiveCheck的loader错误。当前全局配置校验仅剩并发A-neutral旧setup的5个missing opts诊断，不属于B新增行。
- PlayMode共享 `PresentationDebugScene` 复测：1016 wisdom=6失败、7成功；正式blocked root拒绝。1012三个隔离阶段分别从P004.a、P016、P014启动；A/H四组合实际页序通过：00=短线P053，01=短线P053，10=长线P085含非帮助P083，11=长线P085含帮助P075；每次取消后 `PresentationPauseRegistry.OwnerCount=0`、无对白泄漏。没有声称真实营业、制作、品级或正式完成。
- 该轮之后已退出Play并恢复 `Assets/Scenes/启动场景.unity` EditMode；后续1011复测也执行相同恢复。

### 后续验证补充

1016实际Presenter序列：wisdom6共7行、wisdom7共9行，仅7包含P011/P012，均末句P015，Controller正常退回空闲。1012实际Presenter序列：A/H=00/01/10/11分别31/32/40/38行，尾句分别P053/P053/P085/P085，H=true含P036，长线两帽子子路互斥。三根正式blocked拒绝、结果/日期仍为空；post_brew缺D输入拒绝，pause owners=0。工具首次在无active presentation评估Inline被拒，随后显式独立内存启动fixture后重跑通过，未绕过正式状态。最新276条源Text与JSON逐条相同；静态凭据 `Library/Locus/tmp/rose-content/current-static-verification.json`。Console有一条并发sorel无可见portrait的effect错误，本B隔离序列未用该角色，保留记录且未清Console；不宣称全项目零错误。

下列历史恢复步骤是早期时点，已被上述增量覆盖；当前优先完成1011工具审查与生成/验证，其他正式阻塞不变。

## 03:38–03:40 最新收尾：1011前订单片段

- `Tools/Narrative/configure_rose_1011.py` 初稿审查未通过（原Choice ID、长短线路由、阅读边界及未注册前置问题），未应用该初稿。父窗口收窄成独立P003–P045：10页、31句、两组5选项、知觉Inline threshold6、sparrow/sylvain Object；只读plan和正式源配置均停在P046未知订单名/真实制作之前。没有把后制作、智慧/历史长线和名片伪装已接。
- 初次落表曾把5个Choice标签也作为对白，父窗口核查后只删除这5条B行，并修正工具过滤规则；最终31句，不是manifest早期36句。备份分别为033803-402850-1011-config、033826-choice-line-repair。
- 03:38:54官方生成通过，随后Unity compile及domain reload completed。强制导入JSON后loader成功，1011读取31行；全局validator仍为前述5个A-neutral missing opts，B没有新增校验错误。
- 共享场景Play内真实Presenter顺序检查：perception5/6 × drink3 × news2 共12组合通过；仅知觉6含P015–P017；每饮品/新闻只含唯一回应，所有组合末句P045。测试播放回调自动选择，未逐句点击全UI。可视 `PlayRose1011Construction(6)` 从P003启动，Dispose后对白关闭且pause owners=0。
- 新增预览入口在 `NarrativeChapterPreview.Rose.cs`：1011(perception)、1016(wisdom)、1012(stage,A,H,D)。正式源四根都保留blocked，无B completed生产者。1016停止P015而不是完整章终点；1012post_brew是显式夹具而不是实物制作验收。
- 最终Editor已回 `Assets/Scenes/启动场景.unity` EditMode。未Git提交/暂存/清理他人文件；共享工作区仍有并发未跟踪及修改。项目Logs/Editor.log是旧日志，不作为本轮编译证据，本轮使用工具报告compiled/domain_reload completed。

最新各事件：1009=6页62句2选项/P071；1011=10页31句5选项/P045；1012=12页71句/三个隔离阶段；1016=3页9句/P015。均官方生成，局部运行检查如上，正式地图/营业/制作/存读档全链尚未验收。正式放行仍需B-B01–07、B-C02/03/04/05及D帽子合同；不能再将这些称为已完成。

后续具体恢复：先由总控定案1009知觉两侧终点和会所地图挂点，再闭合B→D→A/B的正式完成生产链；并行可接1011真实制作/订单/评级与通用名片阅读，之后推进后制作两组选项与历史两阈值。1012沿首次真实营业就绪边界和两杯真实提交联调；1016等待原稿，不添加完成结果。

## 下一条可执行步骤与恢复入口（历史）

1. 总控/现有共享文件所有者串行整合5个UU，保留oct07七步信件与模板、stageOwner/菜单取消/经营owner的双方行为；实际完成统一编译/domain reload。不要用现有up_to_date记录替代这一项。
2. 接着只做B-1：复查统一NCS根内临时选择/可见页舞台能力是否已补；未补则最小接线并验证当前页choice组，保留根尾事务。补独立rose.envoy/rose.butake身份及显示名时不得借bertrand身份。写前重读Narrative/schema/代码并检查并发。
3. Text已存在，运行上述施工器同一event可核对/幂等恢复，不复制正文到C#。读取manifest entries的key/page/source定位，把1009 P003–P071双路接到共享配置的隔离预览；B-B01/02未确认前不注册正式必做或completed。已存完整尾文只能明确预览，不推定检定。
4. 生成仍走官方bat；真实编译后优先打开 `Assets/Scenes/PresentationDebugScene.unity`，1009双选项分别到P052/P063再汇P065，取消不提交。待真实地图挂点及尾部定案才测day3/4/5、known真假、AP0、已看非完成及一次结算。
5. 1011在1009与A前置真实成立后继续；独立知觉和文本可先配置预览，制作/订单名/名片取消接口/智慧未齐不得启用整根。1012继续依赖上述结果及D完成和经总控统一的帽子合同；分别核A/H四组合，缺C拒绝。
6. 1016只接已有P004–P015隔离预览，智慧6/7，P016明确工程边界停止；二楼Unknown拒绝，P017不当结尾。本次尚无此预览入口，不能宣称可用。

当前恢复点（历史）：Text源表已安全落地、JSON已官方生成并导入；当时Unity保持原启动场景Edit Mode。后续状态以以下增量为准；不需重做276条文本，四事件均未达到游戏交付完成条件。

## 14:14–15:00 增量：1011后制作、同根普通阅读、1009尾文与1012终场

本节覆盖此前03:40恢复建议；源规格未被修改，正式四根仍保留blocked/result-free。14:14只读核查没有找到B-B01–07权威定案；A1010仍没有completed生产者，D1009虽有completed Result但仍依赖B1009正式完成；D帽子帮助事实的跨批合同仍冲突，不能用同根临时choice当作跨根历史。

### 实际配置与工具

- `Tools/Narrative/configure_rose_1011_postbrew.py` 已串行apply、幂等验证。新增15页81句、两组6选项、第二知觉Inline threshold5，以及ReadingIndex/1 ReadingPage/3 ReadingLine（P128–P130）。原10页只在 `dependency:B1011.stage.pre_order` 生效；新增后段只在 `dependency:B1011.stage.post_brew` 生效。Q3①②或Q4①③统一去P068–P073早退，Q4②长线要求显式智慧通过夹具；不提供未知智慧失败的可播夹具。历史专精和低品也必须显式输入，不替代真实Player/成品接口。
- 1011当前25页112句对白、11选项，另有3行名片阅读。P046/P047及真实制作边界P048未接；root contentBoundary记录 `isolated-stages;missingOrder=P046-P048`。没有订单默认名、评级映射、正式Result或completed。
- `Tools/Narrative/configure_rose_tail_previews.py` 已串行apply；证据目录 `Library/Locus/tmp/rose-content/20261016-142644-675494-tail-previews`。1009原六页加front阶段guard，新增独立portrait页4句P073–P076；使用 `rose.club.reception` 中性占位，不伪称CG或猜知觉门槛。1012追加唯一seq13 `terminal_fade`/FadeOut，限定post_brew阶段，短长两路共用。工具17项拒绝用例、内存xlsx往返、旧cell元数据保留及幂等检查通过。
- 源表写入沿 `rose_write_guard.py` 共同锁、输入CAS和备份执行；不能据锁文件不存在推断其他旧窗口没有写表。保留共享工作区并发修改，未暂存、提交或清理他人文件。

### 同根普通Reading接线

- schema及 `NarrativePresentationDefinitions.cs` 增加独立Reading步骤，enum仅在尾部追加；保留已有Letter和并行WaitBusiness/WaitNight。`LubanNarrativeLoader.ConversationSteps.cs` 独立校验普通Reading，不要求信封/重复标题。
- 新 `NarrativePresenter.Reading.cs` 负责按seq复制阅读快照、完成后同根续接、取消/Stop-only中断、迟到回调失效和幂等释放；同栈Stop/Complete信号在下一帧分辨。新 `ConversationReadingPlayback.cs` 复用ReadingPanel prefab和同owner对白舞台暂停lease，完成/取消/停止/释放分开，旧owner不能关闭新阅读。
- `ReadingPanel.cs` 增加AllowCancel、IsSuspended，输入尊重 `DialoguePanel.IsExternalInputBlocked`，暂停恢复抑制同帧输入。已绑定 `NarrativePresenter.cs`、`NarrativeRuntime.cs`、`GameFlowBootstrap.cs`、`NarrativeTestPresentationBridge.cs`、`NarrativeDebugGM.cs` 和 `NarrativeChapterPreview.cs`；正式/预览释放路径均清理普通Reading。原 `ReadingEventPlayback` 及Letter逻辑保留。
- `NarrativeStageVisuals.asset` 经Unity API追加 `rose.history.portrait` 中性灰占位，保留其他条目。不是已验收的玫瑰王画像美术。

### 生成、编译与实际验证

- 14:30:47官方 `Assets/ConfigTables/GenerateAll.bat` exit0，Player semantic及Luban校验通过；本次生成持共同锁且Narrative/Text/schema SHA在生成期间稳定。生成JSON经Editor强制导入，loader成功。稍早recompile返回up_to_date，不作为强制重载证据；本轮结束前再次调用recompile，实际返回compiled/compilation completed/domain_reload completed/bridge ready，Editor editing，5文件semantic warnings none。
- `Locus.UnityTestApi` PlayMode run `f4c21c41b1e84d96b3438b0b412f51ac`：20/20 passed、0失败、0跳过。范围为 `ConversationReadingPlaybackTests`、`ConversationReadingStepTests`、`October07S2SequenceTests`。覆盖三行快照/排序、自然完成一次、取消/仅停止/销毁不完成根、迟到回调、旧owner释放、正式禁取消与暂停输入，以及既有S2信件/宿主中断与重试。仅此范围通过，不声称全项目回归通过。
- 1011生成配置+实际Presenter：后制作lowGrade两值 × Q3/Q4五种路径 × history两值 × perception4/5，共40组通过。早退末P073，长线末P140，名片仅长线一次；低品P050、知觉P112、历史P095/P132及互斥P100与输入一致。长线仅在智慧通过的隔离夹具下测试；真实智慧规则和失败路由仍缺。
- 1011前订单原12组（perception5/6 × drink3 × news2）重新验证阶段隔离，均末P045、不泄漏后段。真实UI长线一次自然完成，三行ReadingPanel关闭后续接并最终完成内存根；另一次在名片取消，根完成回调没有执行、预览停止。清理后pause owners=0。
- 1009新增阶段：共享UI前段和画像分别从P004/P073启动、取消清理通过。最新生成Presenter前段两路52/56句止P071，portrait独立4句止P076，互不串段。首次补充测试误用Immediate RequestEvent启动MapAction被正确拒绝；修正为已知约定+day4+world.map.active及QueueMapActionEvent后通过，不是修改正式入口规避拒绝。
- 1012生成Presenter的A/H=00/01/10/11均末P053或P085后执行唯一一次FadeOut。这里检查实际步骤调度，未重复声称真实制作/营业全链通过。
- 14:55共享场景实际Game视口查看普通名片三行：法文重音、中文译注与P129多行显示正常，无重复标题或截断；未使用新Scene/Canvas。截图为 `Library/Locus/Screenshots/locus_game_20261016_065508_201.png`、`locus_game_20261016_065523_757.png`、`locus_game_20261016_065540_978.png`（文件名UTC、检查时区本地UTC+8）。这是复用阅读样式，手写字体/名片专图仍未验收。
- 14:56重新逐条核对276条原稿Text与JSON相等（68/128/71/9）；最新静态凭据已更新 `Library/Locus/tmp/rose-content/current-static-verification.json`。当前四根：1009=7页66句2选项；1011=25页112句11选项+3阅读行；1012=13页71句含1 FadeOut；1016=3页9句。四根均无Result，正式请求仍被blocked拒绝。
- 最新全局 `NarrativeConfigurationValidator.Validate` 仍仅返回5条A-neutral missing slot opts（1008.s1、1010.s1、1010.s2、1013.s2、1018.s1）；B没有新增诊断。本轮实际UI有表情差分回退及 `rose.history.portrait` 背景占位警告，保留而不清Console。源码定向git diff --check通过。

### 当前交付边界与下一步

Editor已退出Play并保持 `Assets/Scenes/PresentationDebugScene.unity` EditMode；没有活动阅读、对白或本窗口暂停owner。共享场景沿本轮用户现有场景保留，未再切回旧启动场景。

可恢复入口位于 `NarrativeChapterPreview.Rose.cs`：1009前段 `PlayRose1009Construction()`、画像 `PlayRose1009PortraitConstruction()`；1011前段 `PlayRose1011Construction(perception)`、后段 `PlayRose1011PostBrewConstruction(perception,lowGrade,wisdomPassed,historySpecialization)`；1012 `PlayRose1012Construction(stage,A,H,D)`；1016 `PlayRose1016Construction(wisdom)`。每轮新建并最终Dispose，后段长线必须显式wisdomPassed=true和history布尔；wisdomPassed=false立即拒绝，不预置成真实智慧失败。

B-C04普通阅读的实现和上述范围验收已完成；独立审查随后补齐通用预览与地图fallback绑定，结果见下一节。但1011正式root保留旧blocked清单以避免部分能力完成被误当作整根放行。可独立正文/阅读/尾黑屏已落地；正式下一步依赖总控/作者定案B-B01–07、会所地图，以及既有玩法owner的真实制作/订单名/评级/营业/楼层合同。需先闭合B→D→A/B真实完成生产链，并统一D帽子帮助事实，再做正式存读档/一次结算和营业回归。1016仍等补稿。不能用本轮隔离成功清空blocked、增加completed或宣布四篇正式发布。

## 15:00–15:16 独立审查修复及最终补测

- 独立只读审查发现两处ConversationReadingPlayback遗漏：通用 `PresentationDebugSession` 和 `MapSceneController.EnsureNarrativeRuntime()` fallback。父窗口仅补这两处委托；通用预览沿自身tag和Transient缓存，`ConversationReadingPlayback.PlayPreview` 添加带tag/cachePolicy重载，避免共享调试场景标签清理遗漏。没有改写地图行为或其他窗口既有改动。
- `PresentationDebugSessionTests` 新增普通同根Reading可达、使用production panel、取消不完成/无Runtime及暂停泄漏的回归。`ConversationReadingPlaybackTests` 新增allowCancel=true时暂停跨帧不揭示/不取消、恢复同帧不前进，以及真实adapter旧Session Complete/Cancel/Stopped在释放后与新owner开始后均静默的回归。
- 首轮组合run `2a305168a7214ed589d84d1da1d8ecbe` 在22项通过、0断言失败之后被Unity Test Runner自身异常中断：`PlayModeRunTask.cs:52 NullReferenceException` 与退出任务错误；状态error/Total460不可信，不计作整轮通过。未修改Unity包或清Console，拆分组恢复。
- 最终正式测试run `bc1ff95902514645902fa37939868b83`：Reading两套+October07S2共22/22 passed、0失败/0跳过。独立run `b20dfb34fca647f6af515825efac9ec7`：PresentationDebugSessionTests共3/3 passed、0失败/0跳过。两次均正常结束，不使用中断run替代证据。
- 首次地图fallback启动遇到共享导入状态中的 `church.1020.s1.cleared.fade: FadeOut must be the final step`，Runtime未ready；不是新Reading绑定为空。随后磁盘已不含该页，强制导入最新共享JSON后loader通过，仍仅5条已知A-neutral missing opts。先用独立内存fixture确认Runtime setter转发至真实ReadingPanel，再用最新正式生成配置重新初始化地图fallback：IsReady=true且Runtime/Presenter普通Reading委托均非空。销毁临时Runtime后Instance=null、pause owners=0。没有修改church配置或解除B门禁。
- 所有临时检查只在共享 `PresentationDebugScene` 中执行并清理，没有新增测试场景/Canvas/Input或持久测试对象。最终Editor恢复该场景Edit Mode。测试期间Runner生成的InitTestScene曾成为活动场景，确认无脏改后恢复共享场景。
- 最终定向diff --check通过；测试发现包含新增用例。共享源码期间仍有其他窗口修改，已保留。最新验证凭据为 `Library/Locus/tmp/rose-content/20261016-reading-and-tail-verification.json`；正文/正式阻塞和本节之前所述边界保持有效。

## 16:20起增量：B-C07旁白身份与三槽舞台修复

### 源迁移、生成与共享前置

- 新增 `Tools/Narrative/repair_rose_stage.py`，只允许修改1011/1016的ConversationLine白名单字段；默认只读，apply要求新鲜SHA，共同锁、CAS、备份和候选回读。实际85格/64行变更，对全40,136个旧格逐项核对仅白名单值改变，工作簿/Sheet/目标单元格元数据检查通过。证据目录 `Library/Locus/tmp/rose-content/20261016-162058-209062-stage-repair`；迁移前SHA `372f53f3ae71f9884794c5344eacd8b3575bfaba427a095fcf4848f43d43c848`，候选/落表SHA `686ea902fcd86e090aaea958a2a4c83f06de643fe50fd01e8cc93fb1bbfee117`。
- 1011 P015/P042/P057/P120/P138按原稿改为旁白，清除错误speaker/name，不修改正文。阿黛尔左、麻雀右、西尔万中；P003先放左右，P043才入中，P069/P126仅清中槽。独立后制作入口P050/P052.a补三槽；P103画像段清三槽，P110.a/b/c保留西尔万身份但Offstage，P116.a恢复。1016 P004清场，P008同时放两访客，发言保留本地“埃莉诺”别名，不改全局Object名字。
- `schema.xml` ConversationLine.side原先不含center，本轮只补允许center，不改其他窗口新增字段。迁移工具11/11内存自测通过，包括93个白名单格的第三值拒绝、非白名单篡改拒绝、候选元数据保留、CAS拒绝、schema前置及部分迁移幂等；self-test无文件写入。当前源表再次只读幂等，0格变更。
- 16:21首次官方生成失败，原因为并行CafeVisit schema/loader已增加 `orderProductId` / `requireRealProduction`，但源sheet缺列；没有手改生成类或回滚并行代码。仅补两列的var/type/group/说明表头，原有数据格保持空，未填真实订单或启用制作。该共享前置证据 `Library/Locus/tmp/rose-content/20261016-162319-835604-shared-header-convergence`，旧40,136格保留。16:23:47重跑唯一官方入口 `Assets/ConfigTables/GenerateAll.bat` exit0，Player semantic与Luban validation通过，持锁期间Narrative/Text/schema SHA稳定。
- 首次完整编译返回compiled、compilation/domain_reload completed、bridge ready。新增播放测试出现Unity6 `Scene.handle`为SceneHandle而非int的编译错误，仅将快照转换为GetHashCode修复；随后编译收敛，定向semantic warnings none。生成JSON经Editor导入，121行1011/1016全字段与源表一致；276条B正文再次与原稿/source-map逐条核对相等。共享源随后继续由其他窗口改变，不把本轮SHA当作整个共享工作区冻结状态。

### 自动化与实际UI验证

- 新增 `RoseStagePresentationTests.cs` / `RoseStagePlaybackTests.cs`。UnityTestApi Play run `f6d2bea1959e4939b06eef6271afaf2c`：**12/12 passed，0失败/0跳过**（8项配置/adapter、4项真实UI）。播放测试按需复用共享PresentationDebugScene，未新建测试Scene/Canvas/Input。
- 真实UI覆盖：前段P003左右双人、P042仍无中人、P043中人出现；低品/非低品两个独立后制作入口三槽齐备，Q3早退P069只清中槽；长线P103全空、P110三句不重新上台、P116.a恢复三人、P126离场后真实ReadingPanel逐行读完三行名片、P132至P140仍只有左右；1016 P004全空、P008双访客、P009/P014局部别名。每条取样路径主动Dispose，检查零根完成、owner恢复、舞台清空、无预览宿主/Runtime替换及场景/timeScale/输入门禁恢复。不是全分支自然结束或正式全链验收。
- UnityTestApi Play run `eb25558f88bc43878f2ac1feb1daeeee`：**17/17 passed，0失败/0跳过**，覆盖ConversationStageSlotTests和两套ConversationReading测试。暂停/恢复同帧门禁、取消/Stop-only不完成、旧owner迟到回调隔离及三行续接仍通过。此前组合回归run `ebc8232e97d24962979eef3495dd8bb8` 在共享编译期间停于starting、0项执行，显式取消；不计作测试失败或通过。
- 独立只读审查无阻断问题，并确认85格白名单、121行源生成一致、11项自测与幂等。按建议补强五旁白speakerNameKey为空及P110保留sylvain身份/空stage命令的测试断言。首轮补测 `7aa356af228a45558548a85c86c95696` 0项停于starting后取消；随后 `176de1f3745e482ea2e4d112bc7a43d6` 真实执行8项全部失败，统一原因是共享新增 `neutral.1018.s1.schedule` result-only事件被 `RequiresPresentation` 的eventProperties判定误视为演出。强制导入最新JSON仍复现，排除单纯导入不同步。父准备最小改动时CAS失败，重读发现并行owner已修正忽略entry/blocksSave/blockedBy，故本窗口未修改该方法、未回滚对方修复，仅在 `PresentationPauseTests.cs` 补4项控制元数据/仍保留bg载荷判定的回归。完整compile/domain_reload completed后loader恢复、仍只有既有5条missing opts诊断。run `387768df5d0d48c380dd8dd0d4628233`：**15/15 passed，0失败/0跳过**（Rose配置补强8项+暂停7项）。这里的元数据测试不代表门禁大小写支持变化；B继续使用规范blockedBy且独立检查实际拒绝。
- 正式入口只用未解除门禁的生成配置做独立内存检查：四根均accepted=false，happened=false；1009已有completed条件为false，其余三根未定义completed条件，不为测试补造。四根Result仍为0。首次观察误用GetCondition读取未定义键抛错，改用TryGetCondition完成检查；未改源配置或正式State。

### 边界与恢复

- P008舞台内部快照仍使用全局名“阿莉耶诺尔·康多尔”，P009发言再覆盖为“埃莉诺”；P008是玩家发言，当前speaker标签只取当前说话人，不显示该访客名。没有改全局身份，也不宣称已验证其他未来UI不会暴露此内部快照名。
- sparrow/sylvain仍缺自己的portrait资源，表情差分有fallback，画像背景是明确中性占位。本轮验证角色key/槽位/退出清理，不等于三套美术可见或正式画像验收。
- 正式四根blocked/result-free及B-B01–07、真实订单/评级/制作/营业/楼层、跨批历史事实的裁定边界保持。并行窗口已在实现CafeVisit制作/营业合同，下一轮应先核对其最终接口与源表，不重复新增平行系统，不据此自动启用B根。
- 最新共享配置在result-only元数据修复后，真实舞台+Reading+三槽最终复跑 `fd0ab6241a774cca971683b94d11e1cf`：**21/21 passed，0失败/0跳过**。与上述15项补强测试合计36项不同用例最终通过；未把较早重叠轮次累加成独立覆盖数。
- 本轮静态凭据已刷新 `Library/Locus/tmp/rose-content/current-static-verification.json`；16:38一致性观察期间源SHA稳定，正文68/128/71/9，舞台121行全字段一致。完整本轮运行证据另见 `Library/Locus/tmp/rose-content/20261016-stage-verification.json`。
- 本轮最终测试结束后，另一共享窗口已接续使用Editor（当前test handle已换为其他run，观察到PlayMode的非本轮演出/暂停owner），故不强制切EditMode、不关闭它的UI或删除Runner场景。本轮四项真实UI的teardown已实际断言场景、timeScale、输入门禁、owner计数、预览宿主及Runtime恢复；本轮没有待清理的自有演出。当前共享PresentationDebugScene仍在加载场景中。最终定向diff --check、新增文件尾空白检查与凭据JSON解析通过。

## 17:00–18:19增量：1012舞台与正式营业安全入口

### 已落地内容与生成

- 新增 `Tools/Narrative/repair_rose_1012_stage.py`，默认只读、apply要求新鲜SHA并复用共同锁/CAS/备份/元数据及候选回读。10项纯内存零写自测通过；实际只改32行43格（27个side、16个stage槽命令，修正此前手算44格），全41,148个既有格及元数据核对通过。71行身份仍为adele11/alienor16/player16/旁白28，不改正文、emote或本地别名。
- 准备P004.a、重做P016、后制作P014各恢复阿黛尔左槽并清另两槽；P032访客首次说话时切换为埃莉诺右槽；短线P050清右、P052阿黛尔回左；长线P060双人、P064清左、P085阿黛尔回左。P032清左是访客焦点布局，不补写离场；P085整段末尾才描述进来，当前命令在该未拆段落开始生效，这是已说明的源粒度限制。试饮CG、装饰和平民装资源仍未验收。
- 迁移证据 `Library/Locus/tmp/rose-content/20261016-180744-416924-1012-stage-repair`：before SHA `0892de2403ed086a54287440b81919b349024446e36f65b2a16182c014b88057`，after SHA `caff8f11784123b909888bc0548f1bad557ab3aa7ab7053b5a540bd4f1842688`。18:08唯一官方 `Assets/ConfigTables/GenerateAll.bat` exit0，Narrative/Text/schema在持锁生成期间SHA稳定；随后compile/domain_reload completed、bridge ready，最终收敛up_to_date。
- 独立静态复核PASS：276条正文（68/128/71/9）与source-map、Text.xlsx、tbtext.json逐字一致；1012的71行×15字段=1,065项源生成一致；工具幂等0格。四根仍blocked，Result/Action/EventDate/CafeVisitHook直接激活项均0；不新增completed定义，保留早已存在的1009 completed=false且无生产者。凭据 `Library/Locus/tmp/rose-content/20261016-1012-static-verification.json`。

### B-C03安全入口候选接线

- 新增 `RoseBusinessEntry` 和可选 `IDayFlowBusinessEntryEndpoint`，由既有 `DayFlowController.Update` 在day7真实营业安全边界请求 `rose.1012.s1`。不改变原 `IDayFlowGameplayEndpoint` 合同。`FreeCafeRuntime.NarrativeBusinessEntryReady` 要求Playing、正常营业/Open、非教程、开业已完成、无拖拽/InputBlocked/业务暂停、来访bridge已ready且无handoff、无本owner待恢复快照。InputBlocked覆盖现有配方/生产HUD及GM面板；不能仅检查GameplayBlocked。
- helper要求calendar一致、Controller安全且可存快照、非busy/ended、无Business/Night/Service checkpoint，并要求1011/D1009 completed键已定义且true、1012 completed键已定义且false、尚未happened；最后仍由QueueEvent检查正式blocked/entry/条件。本轮不补任何这些正式事实，当前生成配置仍拒绝请求。
- 接受后按Controller/day防重复；取消不逐帧重播，旧回调无效。新会话/读档Reset只重置临时attempt记录；提供 `DayFlowController.TryRetryRoseBusinessEntry()` 显式API，不新增自动弹窗。审查发现先Reset再检查checkpoint/source会在拒绝后丢失旧记录，已改helper `TryRetry`复用全部门禁；QueueEvent拒绝恢复原记录、抛异常保留已请求状态，避免逐帧重播。
- 这是正式host接线已具备、剧情门禁仍保留的候选能力。真实FreeCafe modal/GM/pendingRestore门禁已静态核对，未在完整CoffeeShop建立正基线做现场测试；不能把fake endpoint宿主测试当作完整经营场景验收。

### 验证、失败记录与剩余施工

- 最终Play run `7d3d18a12691464ba65d2088cdd2eb38`：RoseBusinessEntryTests **31/31 passed，0失败/0跳过**。包括真实Controller的day/readiness/calendar/未知或false前置/source blocker/缺事件/完成/happened/结束/并发/恢复门禁，真实Business/Night/Service checkpoint，接受/取消/旧回调/新Controller/显式重试；取消后checkpoint或source拒绝重试，解除条件后普通请求仍不自动重播。同步Play宿主fixture使用真实CafeWorld和fake endpoint，覆盖CityMap/BusinessTransition/延迟Open/nonready/disabled拒绝、正常Open只请求一次、busy/nonready拒绝重试不解除防重播状态。
- Play run `7628eec0a3744311abc1ec191c694ff9`：RoseStagePlaybackTests **5/5 passed**。新增一项实际共享UI测试中跑完1012准备/重做及四种A/H路径的源锚点，核对角色槽/别名/短线P053和长线P085，逐路主动Dispose；同组既有1011/1016舞台继续通过。不是正式两杯制作或全路径自然完成结算验收。
- Play run `0cd6e0064b0d466994d26e108a70c050`：Reading前缀、三槽、暂停及Rose配置 **32/32 passed**。DayFlowCheckpointTests独立run `901ffcab892a4f1389a71c33c4b5ff3d` **4/4 passed**（在最终retry重构前，checkpoint业务本身未改）；本轮共72项不同用例已最终通过，不累加初版入口29项重叠结果。
- 如实保留Runner异常：组合run `979c80ee96824b1198f490aaacf43cf8` 执行0项时报player stopped；`5e0a5beeaae74c34969cc2a8f0b8bad9` 执行27项通过、0断言失败后同样中断，报告的Total527/529不作为覆盖。后续拆组有上述正常终态；Edit尝试 `078ffdd37a7a4da1b268adf11a154d79` 发现0项，不计覆盖。一次查询被domain reload断开，后续只读查得32项正常passed，未借中断结果替代。
- 正式全链仍未完成：B-B01–07、D帽子事实冲突、真实两杯制作归属/评级/订单名及楼层开放来源未闭合。最新独立工程审查确认A05 WaitService只承接一个来访订单的最终真实杯回执，不能表示B1012黑+奶两杯，也不能把A评级裁定复用给B。下一可独立工程增量是让现有ServeProduction对重做和最终交付都输出不可变的逐次制作事实回执，沿既有保存/outbox转发，不确认现有WaitService、不解释为B普通/优秀；随后还需明确临时制作请求归属与接管。B-C05需经营/建筑owner给出真实floor开放字段和恢复ready合同，不能从图片/镜头推断。
- 本轮真实UIteardown已断言场景、输入门禁、timeScale、pause owner、预览宿主及Runtime恢复。该轮观察共享PresentationDebugScene在Play、有非本窗口对白和3个pause owner，当时未关闭/切模式或删除他人Runner资产。汇总凭据 `Library/Locus/tmp/rose-content/20261016-business-entry-and-1012-verification.json`；正式四根不得据本轮解除blocked或填completed。

## 18:20–18:32增量：逐次真实制作事实回执

- 新增 `CafeProductionAttemptReceipt` 和 `CafeWorld.ProductionAttempts.cs`：仅在既有真实来访/订单关联的 `ServeProduction` 实际消耗杯子后记录一次，包括重做和最终交付；普通自动订单/前置拒绝不制造该来访回执。复制visit/order/cup/product/classifiedProduct、visit内从1连续序号、原经营score/minimumScore/correctType/latteArtSatisfied/satisfied/willRemake。satisfied仍仅代表当前经营订单标准，不映射为B普通/优秀。
- `ProductionAttempts(visitInstanceId,orderId)` 只返回精确归属的只读列表与独立DTO副本，后续订单分数/Catalog阈值和调用方修改不能覆盖保留的事实。列表沿CafeVisitInstance既有保存序列化，不加入cafeVisitFeedback outbox、不触发StageHandoff、不确认WaitService、不改Bridge/Controller及最终service_completed语义；不新增临时制作request合同。原有可复用器皿交付能力保留，普通耗材杯仍不能重复交付。
- 恢复校验先于State替换，检查visit/order/product身份、连续序号、有限且0–100的score、有限minimumScore、原布尔快照自洽及最终回执对应。旧JSON无字段按空列表兼容；minimumScore允许101，历史阈值/maxRemakes不按新Catalog重判；重做后EndVisit保留事实而不制造最终完成。独立审查指出活跃order.remakes可被调低多获重做、score有限但越界可过关，已修为remakes不少于保留重做记录数以及score范围，并补原子拒绝测试。remakes允许更大值以兼容旧存档无回执的历史重做。
- 首轮Edit run `4cd223893bc942978bb966f3ed57f24c`：81项78通过/3失败。两项新增fixture错误分别是把lastProductionScore设为-123违反既有存档校验、假定EndVisit后全店无其他订单而要求Deliver必false；已改合法不同分数，并只验证本visit没有错误记录/最终receipt。一项既有 `EveryConfiguredPerfectRecipeScoresHundredAndEmptyCupScoresZero` 在多产品循环消耗杯库存，cocoa时stock0、ServingCup=null；独立内存复现未调用新record路径，修为每产品建立独立世界，不改经营库存/出品逻辑。
- 最终Edit run `f4d4b5c8201f42fd9f6b49add9896633`：**81/81 passed，0失败/0跳过**，包含10项新增逐次回执测试，以及最终服务回执、Visit、Production、Delivery、ReusableVessel回归。编译已收敛up_to_date、bridge ready。测试涵盖实际ServeProduction重做/成功/最终失败、前置拒绝、耗材杯不重用、精确归属/复制查询、阈值变化/旧JSON、往返/序号续接/无重复结算、损坏身份/序号/分数/重做次数原子拒绝。与前一增量72项合计153项不同用例通过，非正式B全链。
- 开始本轮Edit验证前再次观察无活动对白、全部NarrativePresentationUI隐藏且无progress，测试run已passed；返回Edit后共享PresentationDebugScene无dirty、pause owners=0，没有销毁未知owner对象。未删除Runner资产。最终状态随共享窗口并发继续变化，应以新观察为准。
- B-C02仍缺演出暂停到临时制作的请求归属、两杯分组/独立身份及正式评级合同，本轮只补可复用原始事实层。B-C05仍等权威楼层开放来源；B-B01–07及D帽子合同不变。下一施工先取得这些内容/玩法合同，再接实际request与NCS恢复，不能继续堆通用记录层冒充剧情可玩。
- 最终独立复审确认上述两项P2已修，无新增阻断。最终定向diff --check、新增文件空白及5份验证JSON解析通过；最终Editor只读观察为共享PresentationDebugScene EditMode、dirty=false、pause owners=0、无对白。回执验证汇总 `Library/Locus/tmp/rose-content/20261016-production-attempt-verification.json`。
