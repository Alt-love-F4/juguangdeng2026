# 窗口 E｜王庭教会长篇交接

> 本文件是完整施工包的工程交接附件。唯一入口为 [spec.md](./spec.md)，完整原文与配置定位见 [source-map.md](./source-map.md)。工程须完整读取三份文件；末尾施工提示词供复制到工程 Agent 窗口，不代表规格作者在本窗口开始实现。

日期：2026-10-16；优先级 P3；**仅 Spec 交付，不实施**。三份文件均在本目录。正文身份/流程以 `spec.md` 为准，完整原稿及每条拟定配置定位以 `source-map.md` 为准。本表集中列总控依赖、能力、资源和验收。

## 1. 交付状态与施工顺序

| 源稿 | 覆盖 | 当前结论 |
|---|---|---|
| MA70.11.04-S1.docx | 473 非空源段，42 个逻辑页面/边界分组，405 正文+8 选项文本全映射 | **1 个正文事件可分段施工；完整正式发布阻塞**。分页/章节不是 42 个事件或奖励 |
| 【施工中】MA70.11.05-S1.docx | 1 标题，0 正文 | **未完成源稿，不施工** |
| 【施工中】MA70.11.05-S2.docx | 1 标题，0 正文 | **未完成源稿，不施工** |

立即可交工程的独立内容：来源映射、确定的文本提取/技术错字映射、角色/场景源行、旧城 success-only Inline 检定内容和资源挂点。源正文的独立录入与资源占位预览可以先做，但不要让半成品根进入正式 EventDate 候选。静态数据可先在工程既有内容预览流程推进，不新增脚本格式或空子事件。

推荐施工顺序：

1. 总控合并 E-B01–04 与 D/其他批次事实、金额和倾向口径；记录最终生产者及所有数值。11.05 不等待、不补稿。
2. 工程统一完成 E-C01（同根跨经营/夜间）、E-C02（局部快照路由）；与其他窗口共享的功能只做一次。不是新叙事系统，也不为 E 阻塞 A/B 早期版本。
3. 按 source-map 配现有 Narrative / Text 链路，先早晨与三个报酬响应，再两条游览路线，再夜间选择及两末支。source-map 的 boundary/source 节不是普通空 Dialogue 页。
4. E-C03 修别名和角色源，E-C04 扩展现有隔离预览输入；挂 E-R 资源、缺资源诊断。使用正式生成配置后再预览，不用另一份硬编码台词冒充正式链路。
5. 走真实 DayFlow→经营→AfterBusiness→BeforeNight→唯一最终结算；执行本表验收矩阵。记录“已运行/结果/证据”，不把本规格当验收记录。

## 2. 跨批次输入输出及别名

下列新增 ID 为拟议，尚未写配置；有同义正式 key 时复用并由总控回填映射，不造第二事实。当前源表没有 `church.1104.s1`、`story.church.1103.s1.*`。

| 事实 / 依赖 | 值 / 生产者 | 读取点 / 来源 / 合并边界 |
|---|---|---|
| `story.church.1103.s1.completed` + `story.church.1103.s1.shelter.accepted`（拟） | 两个 bool；原 11.03 求庇护事件完整完成时写；只有选择庇护且完成才 accepted=true | 本根初始候选/晨间开始；E04-P002、故事 C85:D86。生产窗口尚未分配/正文未发现：E-B01。10.24 和新玫瑰徽记关系由上游保证，不在本根重编 |
| `story.church.1014.s2.completed` 或更细“已听跛脚工人谈救济”事实（拟） | D 的 10.14-S2；P041、P046–048，各选择后都讲到 | 本稿 P149–150/P350–351。请总控确认可达性保证；不能仅因剧情“应该听过”置 true，见 E-B04 |
| 当前正式终局/结束态 | 既有 NCS ending 状态；D 10.23-S2 end if2 等生产 | 终局后本根不得启动；不新增 `alive=true` 假事实，不要求所有 D 稿 completed |
| “此时已倾向革命联合”分类 | 正式 faction 状态/阈值生产者待总控明确，E-B03 | 本稿 P440/P453、故事 D86；只在夜谈分流，不作为根入口；未知≠false |
| `story.church.1104.s1.completed`（拟） | 本根唯一 FINAL 写 true，onlySetTrue=true | 后续确认消费；不在本包创建 11.05 事件 |
| `story.church.1104.s1.thibaut_reported`（拟） | 本根 FINAL 与 completed 同批写 true；只读时间线 F86 依据 | 后续王庭获报/来访消费者由总控接线。本章不增加蒂博台词/露面/奖励；预览不写 |
| fee/route/night 的局部选择与各检定快照 | 同根局部上下文，仅供本章路由、检查点与去重 | 不是可由未来事件直接消费的全局 Flag；有明确新需求再扩正式事实 |

**旧编号消歧**：P002 真引用 11.03 前置；P040/044/048/059 的 `MA70.11.03-S1-1`、P074/077/252 的 `…-2`、P428 的 `…-3` 都是 11.04 稿内旧别名，分别映射 `church.1104.s1.fee.*` / `.route.*` / `.night.*`。不跨 namespace 解析裸编号。

**角色合并**：埃莉诺=阿莉耶诺尔·康多尔；已有美术/旧目录 ID `alienor`，源表 Object 当前未有该行，需统一补正式身份引用而非把旧 CharacterCatalog.asset 当内容编辑源。阿黛尔全拼/简称/反引号异写均 `adele`。西瓦尔只被提及，没有发言或立绘；路人“男人”和“男子”分别是租客和买主，不共用人物身份。

## 3. 真正阻塞与可继续范围

| ID | 类型 / 证据 | 需要的决定或交付 | 目前可继续 / 禁止替代 |
|---|---|---|---|
| E-B01 | 前置剧情未交付：E04-P002；故事 D85/D86；源目录文件名扫描没有 11.03 DOCX | 明确 11.03 归属、原稿、完整完成/庇护选择事实的真实生产者及与10.24的可达链 | 继续所有文本和隔离预览；正式根入口关闭。禁止默认已庇护、仅看 date 或给玩家强塞上游事实 |
| E-B02 | 未定报酬及社会消费数值：P043/P061/P064，P095/P099/P100/P102/P109/P114，P379/P383/P384/P386/P393/P398；作者明确未定 | 至少确认 Q=友情价报酬、C=旧羊毛衣价格；蔬果/三口生活参考金额 F 是否也等于 C 须由文案/数值明确。不能因为都是 xx 就把 Q/C/F 绑定为一个数 | 配确定分支和完整带占位源映射，隔离预览保留 xx 并标未定。两条正式路线都经过市场，不能称“免费/1币支已能完整正式发布”；不把数值填0、不删支 |
| E-B03 | 原文“倾向革命联合”无阈值：P440/P453、故事 D86；当前 Bar 未查到正式玩家 faction 数值，ConditionKey 有名为 revolution 的旧 bool 但无本章语义证据 | 给出真实值源、分类口径、门槛/写入时机、未就绪处理与具体 ID；确认是态度分值还是明确加入事实 | 完整保留两末支，隔离测试可各设 true/false 夹具但不是正式算法。不能拿 `revolution`、>=0 或最高分阵营硬凑 |
| E-B04 | 跨章回忆连续性：P149–150/P350–351；D/10.14-S2 P041/P046–048 来源吻合，但该事件依经营触发；两游览路线都无条件回忆 | 确认所有能进入 11.04 的正式存档必经历那段，或提供“没听过”时的文案/路由；“前几日”距10.14已有21天，也交文案确认是否故意使用宽泛措辞 | 不擅自把10.14设为整章新硬门、不删除回忆、不编未听过替代台词；其余独立段可先做 |
| E-B05 | 两篇11.05源稿各仅标题；故事 D87 的梗概不能代替正文 | 后续提供正文及两文件与庇护分支的对应关系 | 本轮登记完成，不施工；不阻塞本包11.04的独立文档/早期 A/B |

工程能力阻塞另列 E-C，不和剧情缺稿混在一起。

不是阻塞、可直接采用的常规细节：day30、MorningBeforeMap 根入口、0 剧情 AP、两位角色左右正向布局、约0.3s场景淡切、确定姓名 token 纠错、晚间三选项共同接 P432、原文未给奖励不添加结果。未给具体 BGM/voice 时留空，不填猜测资源。

## 4. 必须统一补齐的公共能力

### E-C01｜同一根事件跨实际经营及夜间检查点（当前缺失）

**来源**：P069/P070、P412–413；一个完整事件的要求来自 Assignments E。当前 `NarrativeConversationStepType` 只有 Dialogue/PassiveCheck/LetterReveal/LetterRead；`DayFlowController.FinishBusiness` 在 BusinessEnd 后调用 AfterBusiness，完成后固定 EnterCityMap；`RunNarrativeEntry` 以排空完成回调决定下步。静态未找到本章需要的 business suspend/resume 步骤。不是“已有 ResumePendingEvents 就已支持”。

**最小契约**：在现有 NCS Controller/Presenter/DayFlow 边界内增加统一可配置交接，持有同一个 rootRequest/transaction、局部选择和待提交结果；P069 释放演出输入但保留根检查点，玩家进行真实经营。携带 day/owner/业务实例 token 的完成才在 AfterBusiness 接 P071；P412 释放游览舞台并保留夜间待续，原 EndDay 经 BeforeNight 接 P413。无关候选不插入同一段正文，暂停期间其他实际可用活动不因“根没完成”永久禁用。

**保存/取消**：正文不允许保存，检查点须显式序列化 phase、choice/check快照、pending奖励、经营是否完成/token。已结算经营取消不能从头重做或回滚真实经营收益；只重试未完成演出阶段。保留既有业务/钱包存档语义，不写根前钱包绝对值。尚未支持检查点的版本不得放出整章，也不能靠提前 CompleteEvent 伪装。

**验收**：同一根一个 completion；P069/P412不是 completed；待经营时结束当天阻止并聚焦cafe；待夜时不被自身必做检查挡住 BeforeNight；迟到经营/夜间回调无效；重复点击不多发钱/开店/推进日期；day31不会新启动本根；业务结束后重进演出不重发业务奖励。不能“active父等同队列子”死锁。

### E-C02｜局部选择及同一检定快照的互斥页路由（部分已有、缺分流）

**已有**：`SelectChoice` 在活动根内暂存选项，一组只能选一个；`CompleteEvent` 汇总所选 resultOwner。`EvaluateInlineAbilityCheckForPresentation` 缓存 checkId 的结果；PassiveCheck 成功显示本页、失败直接跳页。**缺口**：`PageVisible` 仅查询正式 state 的正向条件，`pageBarConds` 非空抛错；正式 Result 到尾部才写，不能拿选择的 SET_FLAG 结果立即驱动下一页，也不能用空成功/失败页或先完成子事件绕过。

**契约**：利用现有配置/适配层补最小、通用的根局部选项门与检定快照正/反条件；在源表/schema/loader/validator 的单一正式来源接线，具体列名由统一工程定，不在本 spec 创造 DSL。选择 fee/route、morning 知觉成功/失败、远路线智慧成功/失败、夜谈阵营 true/false 都互斥；旧城成功插段失败略过可沿现有能力。检查同一快照，不给 pass/fail 各跑一次检定；单根多组选择需配置允许组集合，不能只允许第一个 opts 组。

**验收**：8个选项各可选、3组互斥；8个条件内容页（morning两页、两路线旧城各一页、智慧两页、阵营两页），再叠加路线门组合按明确门显示，禁止串支；翻页/恢复不刷新快照；外部 flag 不被 UI 提前写入；根末尾只有选中报酬结果。错误配置要诊断，不将未知值默认为失败。

### E-C03｜同角色别名与精确舞台指令（需工程确认/最小补齐）

**证据**：`NarrativeCharacterResolver.Resolve` 当前优先 `profile.displayName`；`CharacterCatalog.LoadFromTables` 使用正式 Object/Text 生成，旧 asset 里的 alienor 不证明正式表里可解析。仅把名字 key 写“埃莉诺”或手改 CharacterCatalog.asset 都不是正确交付。

**契约**：同一人物身份 `alienor` 复用资源/关系，演出快照可指定本段显示别名；正式 Object/Text 注册按上游统一角色表补齐。不永久把角色正式姓名改成别名。P014 在 P015 前两人同次登台；P425 仅阿黛尔离场；首句“房东？”的局部显示名保留。舞台仍用已有 Dialogue/PlayEffect/PlayFade，不另开剧情字段体系；对象叠图若缺能力只做明确预览占位并登记，不造角色。

**验收**：全程对白为埃莉诺、玩家名动态；原文叙述中的阿莉耶诺尔保留；下一独立事件不残留别名。未有立绘的配角只名字/文本。连续旁白不清场，末句黑幕前仍可读。

### E-C04｜沿用隔离预览并支持本章输入（当前入口不足）

**证据**：`NarrativeChapterPreview.Play(eventId, perception=2, startPageId=...)` 当前内部 `SetDate(2)`、只赋 perception；不是通用 day30/wisdom/选项预设入口。复用 NarrativeDebugSession、共享 PresentationDebugScene 与正式生成配置；需要注入 day30、perception、wisdom、前置、阵营分类及选项/检查点上下文。

**契约/验收**：直接预览分支页要重建背景、两角色/阿黛尔已离场、别名、route/fee/检定状态；未定金额允许测试占位但禁止提交。预览“经过经营”只切隔离游标并显式标模拟，不启动正式经营/DayFlow，也不算业务联调通过。退出恢复自身timeScale/输入/owner；不写正式AP、资金、completed、红点、历史、用户存档。UI回归只用 `Assets/Scenes/PresentationDebugScene.unity`，不另建 Scene/Canvas/InputSystem。

## 5. 资源占位和替换表

全部编号只在工程/GM显示，玩家界面不露 E-R。以下是**存在性/路径静态核对或明确未核验**，不是截图/资源可用性验收。文件没在本次定点位置找到不等于全项目没有。缺背景用现有 NarrativeStageVisuals 的中性色占位机制，缺音效静音并诊断；缺特定人物不得换他人。新逻辑 key 以 `church.1104.s1.*` 规划，不能当真实已存在资源路径。

| ID | 用途 / 来源段 | 拟用资源 / 当前状态 | 替换入口与定位 |
|---|---|---|---|
| E-R001 | 房间清晨 P003 | 未查定正式房间图；中性色背景占位，不拿咖啡馆冒充 | NarrativeStageVisuals 新稳定背景登记→morning.wake；P005前 |
| E-R002 | 咖啡馆昼 P012 | 已有 `oct07.cafe.day`；真实图源 `Assets/Art/Background/架空咖啡馆背景_01.png`，绑定已核对 | 复用 `Assets/Resources/Dialogue/NarrativeStageVisuals.asset`；morning.cafe |
| E-R003 | 乔装王女 P014，开心/惊讶/平静/思索原段 | `Assets/Resources/Characters/alienor/neutral.png`、indoor.png 存在；无圣痕平民装是否符合未视觉核验；不默认正式合格 | 正式 Object/角色解析沿用 alienor；同角色基准差分回退；E-C03 别名 |
| E-R004 | 阿黛尔 P014，激动/开心/思索；P425离场 | 正式 Object `adele`，`Characters/adele/neutral`；没查定全部表情差分 | 同角色基准图回退，按 source-map 的 emote；不得镜像 |
| E-R005 | 友情价十几个钱币 P062 | 未查定物件图；隔离预览空物件/明确缺图，不凭图定Q | 同一演出资源适配/已有物件层→fee.quoted P063前；不调用开场业务 |
| E-R006 | 大堂区露天市场 P078/P362 | 未查定背景；中性色占位 | StageVisuals→far.market/near.market |
| E-R007 | 圣西尔万郊区 P119/P320 | 未查定背景；中性色占位 | StageVisuals→far.suburb/near.suburb |
| E-R008 | 旧城区 P161/P261 | 未查定背景；中性色占位 | StageVisuals→far/near.old.intro；其后宅邸/驱逐仍同区域 |
| E-R009 | 圣徒街日景 P220/P253 | 已有 `oct07.street.day` 是显式占位，不是真实街景图 | 同一StageVisuals条目可复用，占位状态保留 |
| E-R010 | 鸟叫 P004 | 未核验音频；缺时静音诊断 | ConversationLine.sfxKey→P005；单次，不在翻页重复 |
| E-R011 | 市场/圣徒街人声 P079/P254/P363 | 未核验音频，不因共用编号假设必须同一个clip | sfxKey→P080/P255/P364；仅当前场景owner管理 |
| E-R012 | 郊区人声鼎沸 P120/P321 | 未核验音频；静音占位；不改源中“人并不多” | sfxKey→P121/P322 |
| E-R013 | 较低旧城人声 P162/P262 | 未核验音频，不编数值音量 | sfxKey/既有音频预设→P163/P263；按“不是很大声”调后验收 |
| E-R014 | 吕西尼昂宅邸 P173/P273 | 未查定图；需求含窄街、锈铁门、杂草、褪色Lusignan徽 | 同StageVisuals背景条目→P174/P274前；不本轮生成图 |
| E-R015 | 宅邸门前高矮两人影 P181/P281 | 未查定叠图；可用明确无图预览占位，不用正式NPC代演 | 同背景/物件资源登记→P182/P282；买主离开P185/P285后移除其影，老妇人仍留原场景 |
| E-R016 | 激烈争吵 P192/P292 | 未核验音效 | sfxKey→P194/P294前，场景owner停止 |
| E-R017 | 男人/抱孩子女人/老人剪影 P193/P293 | 未查定叠图；不是三张新独立角色立绘 | 背景/物件登记→P194/P294；随P203/P303离开后清除，仅本场景 |
| E-R018 | 很重关门声 P200/P300 | 未核验音效；不加震屏 | sfxKey→P201/P301前一次 |
| E-R019 | 圣徒街傍晚亮灯 P243/P403 | 未查定亮灯图；中性色占位，不把日图暗化称真实灯光 | StageVisuals→far/near.street.dusk |
| E-R020 | 咖啡馆夜 P413（正文推定） | 已有 `oct07.cafe.night` 以咖啡馆图+tint绑定，可复用 | StageVisuals→night.return/private/question/两末支；不另造夜景路径 |
| E-R021 | 阿黛尔退场 P425 | 已有 `fx_portrait_fade_out`，库中duration=.22；须确认能只作用adele | `Assets/Resources/Dialogue/PresentationEffectLibrary.asset`→P426前目标adele，保持alienor；不得顺带清场 |
| E-R022 | 两分支黑幕 P452/P473 | 已有PlayFade/fx_transition_black；库中 .6s可供预览默认，需确认黑幕终态保持 | 末句P451/P472之后显式步骤；完成回调接FINAL；不能绑末句一开始盖字 |

房间、市场等若实施时找到正式资源，回填真实路径/GUID与是否占位；只用合法稳定引用，不挪图、破坏GUID或复制整套Prefab。源码中提到小猫、煤灰、点头、口型等但未标额外美术/动作指令的叙述保留为文字，不随意扩华丽演出。

## 6. 工程验收矩阵（本窗口均未执行）

| 验收号 | 最小输入 / 操作 | 预期与检查点 |
|---|---|---|
| E-V01 | 比对原DOCX与source-map | 473唯一非空段；405正文+8选择；2标题稿各1段；无遗漏/改写。标题、选项头、条件、奖励说明不显示 |
| E-V02 | 扫本根所有Event/Page/Line/Text/Choice/Result引用 | 无ID冲突/孤立目标；42分组不是42事件；根只一个once与completed；没有空boundary Dialogue页 |
| E-V03 | day29、30、31 × 庇护true/false/未知 | 只day30+真实完成庇护+晨间能进入；未知配置拒绝发布/诊断，不装成false；已完成/终局不入队 |
| E-V04 | perception=2、3，早晨节点各一次 | 2只P030–032；3只P022–028+成功小字；均P034，不双播/重检定 |
| E-V05 | fee.free / fee.one / fee.quoted | 三项正常可见可选；响应各自独占；P069前无资金变化，最终分别无结果/+1/+Q；Q未定则不开放正式版本 |
| E-V06 | P069不经营直接结束当天；其他地点行动 | 结束被阻止并聚焦cafe；其他真实行动正常；不是全局busy死锁；没有额外剧情AP或事项 |
| E-V07 | 实际经营完成、重复/错误day/token回调、业务中断 | 仅正确一次回调进入P071；未经营/失败不能假续接；经营结算仅业务自己执行一次；根不提前completed |
| E-V08 | route.far | 市场→郊区→旧城→圣徒街→P412；绝不进入P253–411，P159/P160目的地正确 |
| E-V09 | route.near | 圣徒街→旧城→郊区→市场→返圣徒街→P412；P360/P361大堂区、P402回去、P404再次回到；没有智慧段 |
| E-V10 | 两路线旧城perception=2、3 | 2分别跳P169–170/P269–270；3显示正确本路线两句；各只读一次同类检查，不串用另一条路线 |
| E-V11 | far智慧=3、4 | 3只P234–240阿黛尔安慰、无失败提示；4只P226–232玩家安慰+成功小字；共同P242；near不读取wisdom |
| E-V12 | 已听过/没听过D/1014的存档 | E-B04解决前不能把无前情存档作为完整正式通过；不能自动发已听过事实或无依据删回忆 |
| E-V13 | 到P412后重复EndDay、另有必做未完 | 本根进入待夜不completed；别的必做沿原规则；自身不能卡住BeforeNight；夜续接一次，不开第二次经营 |
| E-V14 | P014 / P425 /旁白/配角与表情 | P015前两人就位；只在P425后阿黛尔退；所有旁白不清王女、同人不重复入场/镜像；无立绘者不冒用他人 |
| E-V15 | night.wait/ask/hurry | 三项均可见可选，无暗门/奖励；各直接到P432，选择不重复作正文，不生成源外回应 |
| E-V16 | 阵营分类true/false/未知 | true只P454–472→473；false只P441–451→452；未知阻塞并诊断；只读一次晚间快照；无检定提示 |
| E-V17 | 已选1币，中间经营变钱，最终黑幕 | 最后以当时真实余额+1；不是覆盖早晨余额；钱/完成/获报同批；任一提交失败不标完成 |
| E-V18 | fee任意 × route任意 × night任意，最终重复回调 | 18种选择组合目标都可达；各适用检定和倾向边界另测；只1最终提交、无关系/属性/道具/Ending/ADVANCE_DAY额外结果 |
| E-V19 | 早晨取消、经营后下午取消、晚间取消，旧回调迟到 | 未完成不发奖励；经营后不从P003重跑；恢复本阶段与选择/check快照；旧回调不能完成新实例 |
| E-V20 | 各检查点保存/恢复（正文保存禁止） | 保留根cursor/业务token/待报酬/选项；钱包按业务快照；不重新经营、再判旧check或翻出另一分支。未支持即标未通过 |
| E-V21 | 从市场/旧城/智慧/夜谈任一页做隔离预览 | 使用正式生成内容+真实条件逻辑；重建背景、角色、别名、门；停止后正式AP/资金/完成/红点/存档未变 |
| E-V22 | 完成末句、黑幕、交回DayFlow | 黑幕不提前盖末句；末尾才completed；正常PreNight/HomeNight/DayEnd一次，日期只由正式日程推进 |
| E-V23 | 11.05 S1/S2在正式候选/表中查找 | 没有因标题而生成的事件/结果/自动完成/虚构公爵台词；时间线F87管制没有被本包实现 |
| E-V24 | 配置生成/编译/共享场景/正式全链路 | 工程后续分别记录工具、命令、结果；S1/S2原有流程回归。此窗口全部未运行，静态阅读不能代替 |

最短正式正向与边界路径见 spec §9。组合验收不要求复制18套内容，只需覆盖8个选项及所有门的正/反边界，并用静态可达性确认笛卡尔组合；游戏/Unity 自动化在工程阶段使用隔离夹具。本窗口只运行文档覆盖与路由模型静态审计，不执行游戏测试。

## 7. 静态事实核对与交付限制

- 本轮只读提取源 DOCX/XML、故事/人物工作簿、现有 Narrative/Text 表头/关键行、NCS/DayFlow/演出少量目标代码；完整源文和哈希在 source-map。
- Narrative当前关键字段：Event.eventProperty；EventDate.day/timeSlot；ConversationPage.pageCondKeys/pageBarConds/optionGroupKey/stepType/checkId；ConversationLine.speakerKey/side/emote/sfxKey/accentFxKey/accentTargetKey；Choice.groupKey/resultOwnerId/checkId；AbilityCheck.mode；AbilityCheckCondition.attributeId/threshold；Result支持MONEY_ADD及SET_FLAG。不是新建一套表。
- 真实属性依据 Narrative!Bar 第21/22行 wisdom/perception 与 PlayerSessionCoordinator.ReadAttribute；资金 Bar 第16行 cafe.money 是外部 provider。ConditionKey 中旧 revolution bool 的存在不证明本章倾向分类已定义。
- 现有 `NarrativeStageVisuals.asset` 核对到 oct07.cafe.day / oct07.street.day占位 / oct07.cafe.night；角色资源路径存在，不等于乔装正确。CharacterCatalog.asset只作资源查找线索，编辑权威仍是正式表。
- 2026-10-16 00:57 +08:00 复读 acceptance：S1已验收；S2正式链路、完整日结和GM预览已有通过记录，最终中断/预览收尾及部分回归仍在进行。content-map是配置作者的交付记录，不能据其旧免责声明否认后续运行报告。本窗口未复测，也不声称所有收尾项完成。
- 未启动新Agent/任务/窗口，未改其他窗口文档、SharedContext/Assignments、原稿、表、schema、生成物、代码、资源或场景；未运行生成/导入/Unity/测试/提交等写操作。

交付后停止；工程后续回填正式配置行号、真实事实ID、资源实际绑定和运行验证记录。不会自动开始实现。

## 8. 本轮文档自查（已执行，仅静态）

475个非空源段与原文件逐字核对、三个源文件SHA-256复核通过；405正文映射和8选项ID唯一；216条文档逻辑组合全部可达且单条无重复正文。E-B01–05、E-C01–04、E-R001–022在三文档引用一致；目录仅三个Markdown。以上不是生成配置、NUnit、PlayMode或UI验收；没有为检查文档启动Unity或修改游戏。

## 9. 工程交付与续做记录要求

工程后续使用 `Docs/superpowers/reports/2026-10-16-content-batch-E-church-engineering.md` 作为本批工程记录入口；若已有记录先完整读取并保留，若不存在由工程窗口创建。本规格窗口不创建工程执行记录、不伪填运行结果。每个阶段和每轮结束都更新：

- **交付范围**：事件/阶段、实际修改文件、正式配置 ID/行号、可玩与仅预览范围、未完成部分；同根分段不改成多次奖励。
- **证据分类**：已有工程证据（来源、日期、版本、覆盖范围）、本轮实际通过/失败（命令/测试、日志/截图）、尚未验证；文档核对单列，不与游戏测试混记。
- **阻塞及影响**：缺失原文/数值/前置/玩法、来源段、影响哪些分支与发布条件、解除条件；独立内容继续，不伪造成功。
- **续做入口**：下一事件/阶段、准确配置/代码位置、所需输入、最近失败及最短复验步骤、已知并发写入协调事项。没有新证据时保持未验证状态。

已有验收记录应在实际开工时重新读取；本包历史静态核对和10月7日工程证据不自动延伸为11月4日本批事件已通过。工程处理顺序依可施工性和真实依赖，不因文件排序强行放出阻塞内容。

## 10. 可直接复制到工程 Agent 的施工提示词

以下是交给工程窗口的执行指令；本窗口只产出该文本，不执行其中的实现、生成或测试操作。

```text
你是本项目“文案转 NCS”的工程施工窗口，工作目录为 E:\Unity project\聚光灯2026。本次接手窗口 E“王庭教会长篇”完整施工包。

先读项目 AGENTS.md 和适用技能，再从唯一入口完整读取以下三份文件，不能只读摘要或其中一份：
1. Docs/superpowers/specs/2026-10-16-content-batches/E-church/spec.md（唯一入口与施工结论）
2. Docs/superpowers/specs/2026-10-16-content-batches/E-church/source-map.md（完整原文、分支、拟定配置映射）
3. Docs/superpowers/specs/2026-10-16-content-batches/E-church/handoff.md（前置、阻塞、公共能力、资源和验收矩阵）
同时读取 Docs/superpowers/specs/2026-10-16-content-batch-planning/SharedContext.md、Assignments.md，以及 Docs/superpowers/specs/2026-10-15-october07-narrative-integration-spec.md 第1–8节。

施工前重读以下工程记录的当前版本，并检查 Docs/superpowers/reports/ 下是否已有更新的相关验收、交付或续做记录：
- Docs/superpowers/reports/2026-10-15-october07-acceptance.md
- Docs/superpowers/reports/2026-10-15-october07-content-map.md
- Docs/superpowers/reports/2026-10-16-content-batch-E-church-engineering.md（本批工程记录；若已存在先读，若不存在由本工程窗口建立）
核对当前工作区差异、正式配置和相关代码；保留其他 Agent 的修改，不覆盖、不清理或回滚无关差异。同一 Excel/核心代码由既有工程入口串行整合；发现同文件并发施工先协调写入范围。

先核对 E-B01–05 跨批次前置/数值/文案和 E-C01–04 当前能力，读取 D-church 及总控的最新相关交接（存在时），确认真实事实ID、生产者、完成/庇护语义、回忆前情、革命倾向口径及金额。规格记录是读取时快照；已由其他工程补齐的能力直接复用，不重复开发。缺少真实来源时记录阻塞，不擅自编造阈值、前置或剧情。

按事件逐个配置→官方生成→编译与测试→修复→复验→记录，再推进下一个独立内容。本批只有一个有正文根事件 church.1104.s1（670.11.04/day30）：可按早晨、真实经营、两条互斥游览、夜谈分段施工，但保留一个根事务和唯一最终结算；不能把分页、经营结束或看过详情当成完成。两份11.05稿只有标题，不创建空事件或成功状态。复用已有 NCS/DayFlow/演出/资源与配置链路，必要能力仅作统一最小扩展，不重建系统；只通过正式源表/schema和 Assets/ConfigTables/GenerateAll.bat 生成，不手改生成物。

真正缺文案、缺数值、跨批次事实未接线或依赖尚未开发的经营/玩法时，把来源、影响范围、解除条件、下一步写入阻塞表，并继续其他独立文本/配置准备/隔离预览工作。阻塞内容保持未开放，不用0、自动成功、假经营回调、删分支或预览结果冒充正式完成。若无法生成/编译/运行测试，也如实记录，不伪造通过。

逐项执行 handoff.md 的 E-V01–24。演出UI测试优先使用 Assets/Scenes/PresentationDebugScene.unity，不另建临时Scene、Canvas或输入系统；同时验证真实日程、真实经营续接、分支边界、一次性结果、取消恢复及预览隔离。旧报告/静态文档审计属于已有证据，不代表本批已配表、生成、编译或游戏验证通过；明确区分已有证据、本批实跑通过、本批失败/阻塞、尚未验证，并列来源、时间、命令/测试和日志或截图。

每个事件阶段及每轮结束都在本批工程记录中留下交付和续做信息：已完成/未完成、实际修改文件与配置ID/行号、生成与测试证据、资源占位、阻塞及影响、下一步准确入口和最短复验步骤。已知问题修复后重新验证；只把实际跑通的范围标为通过。最终报告真实可玩范围和剩余阻塞，不把Spec写完或静态核对通过当成游戏已验证。
```
