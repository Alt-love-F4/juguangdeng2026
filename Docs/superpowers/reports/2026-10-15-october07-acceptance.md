# 10 月 7 日接入：施工与验收记录

日期：2026-10-15。按入口、S1、S2 顺序施工；所有既有用户/其他 Agent 改动保留，未提交 Git。

## 包一：入口与状态（已验收核心正式流程）

已落地：Editor/Development 首日 order_confirmed 后交接页的“测试：跳到 10 月 7 日”；安全节点/一次点击保护/旧回调失效；保留角色/NCS 会话，正式 BeginNewDayFlow 同步日期与 AP，不调用 EndDay/发奖励/自动覆盖存档。

正式入口使用同一 NCS Controller/队列：MorningBeforeMap、AfterBusiness、BeforeNight、MapAction，旧默认 Immediate；Event.eventProperty 配置 entry 与 blocksSave。候选资格保存来源/跨越计数/入口/排序，Cross 回落保留，Hold 入口重查；once 仅成功后提交，取消可重试。地图 red-dot 使用稳定 mount/day/显式 phase 实例与 seen 快照；0AP 必做不出事项面板。

### 已实际验证

- 官方 `Assets/ConfigTables/GenerateAll.bat`：exit 0（Player semantic + Luban strict）；S1 文本/来源映射见 content-map 报告。
- `unity_recompile`：结构修改编译成功、domain reload completed、bridge ready。
- Unity Test 专用工具被本工作区 Settings 禁用。没有声称 UTF 测试通过；替代执行为 connected Editor 内直接反射调用 NUnit 无参 Test 方法，执行 SetUp/TearDown。第一轮 39/39 pass（Action22 + Entry10 + MapContent7）；第二轮 Entry12 + MapContent7 + MapUiRepair14，共33/33 pass。
- 真实 PlayMode：新游戏开场 → 正式 arrival 对话，选择“接受委托” → 首日地图 → 咖啡馆开店/教学对话 → order_confirmed 后交接页，实际找到并点击测试按钮。进入 day=2 / 670年10月7日 / CityMapLeisure，Actions.CurrentDay=2，AP=当前配置上限5；PlayerRuntime 引用与资金未变，重复提交 false。
- day2 上午没有 S2/夜间剧情，NCS busy=false、safe=true、无错误候选入队。
- 已通过真实地图查询确认 cafe 仅 oct07.cafe.s1 可展示；S1 未完成时结束当天被阻止并展开对应详情。

### 发现并修复/待复核

- 生成数据在首次 Play 前未导入：C#新字段与旧 TextAsset 导致 NcsActionEntry 异常；通过 AssetDatabase.ImportAsset 正式生成目录后修复，不手改生成物。
- MapActionNewContentDot 缺 CanvasRenderer：已修类依赖和创建顺序，新增3个构造/Mask裁剪回归并直接调用通过。旧运行实例不能仅添renderer消除Graphic缓存，必须重建/重启。修后真实地图已不报此错。
- 详情卡模板实际尺寸包含外伸执行按钮；已按真实 bounds 适配，实际截图确认完整。已看门改为实际可见标题/正文与遮挡 raycast；真实 day2 详情打开后 unseen=true→false，但行动未完成且仍阻止结束当天。新增3项回归直接调用通过。
- FreeCafeRuntime 在 PlayerCafeOwner.Bind 后重复注册 SaveId 的根因已修复：会话 owner 唯一注册，场景仅绑定恢复回调，卸载保留世界。PlayerCafeOwnerTests 5/5直接调用通过；正式 day2 快照经 CoffeeShop 恢复至 Map 无 duplicate 错误。快照仅保存在 Library/Locus/tmp/oct07-acceptance/，未写用户存档槽。

包一已完成上述阻塞修复及正式复核并向用户报告；完整 UTF runner 仍被工作区禁用。

## 包二：S1

已验收并报告。正式地图 day2 必做行动实际 Execute 按钮 → NCS MapAction → 40个唯一源正文行完整推进（非 Skip）；咖啡馆昼→显式中性色圣徒街占位；初始旁白无阿黛尔，出场后固定右槽 center=.75，旁白/玩家/路人不清空人物。共享 nod(.25s)及仅背景 pan(1.6s/.02/zoom1.05) 已接入，淡切.3s。实际播放禁止取消且 CanSave=false，末尾才行动完成，返回 CityMapLeisure、CanSave=true、只展开正常经营详情（未自动进店）。Console error=0。

追加回归直接调用28/28通过（S1 5、CafeOwner 5、MapUiRepair 17、Handoff 1），不是完整 UTF runner。实跑修复两项：旧 effect preset 没有 zoom 字段时为0，校验现仅对 BackgroundPan使用该字段；PSD solo portrait原frame居中，依script side应用两槽位置而非保留居中。

资源：原图仍在 Assets/Art/Background/架空咖啡馆背景_01.png；稳定引用 Assets/Resources/Dialogue/NarrativeStageVisuals.asset，不移动/复制原图。街道明确占位及cautious/dejected基准图回退均有工程诊断。安全快照 day2-after-s1.json 已捕获在Library临时目录，未写用户槽。

## 包三：S2

已完成三包集成与最终验收。正式链路、独立预览、隔离存读档、中断重试及菜单清理均已验证；工具限制与资源占位见下文。

- 正式源表：Narrative.xlsx、Text.xlsx、schema.xml 增量配表；官方 GenerateAll.bat 最终 exit0（2026-10-15 23:14:12 本地生成记录），Player semantic/Luban strict 无 ERROR。生成 JSON 已 ForceUpdate 导入，完整项目编译通过。来源和全部47段映射见 content-map。
- 单根 oct07.s2、七步同 stageOwner，未排入等待父事件的子event。知觉由真实 perception≥2 的 Inline 快照计算；两正式分支均实跑：1 无条件句/无提示，2 在 p020 显示“知觉 ≥ 2 · 已察觉”，到 p021 清除。
- p013 信封 Reveal 不拦正文、不提前拆信；p022 才启用同 owner 原信封，原纸图/拆信动画复用。正文为称呼与7×33=231个两位数字 token，原文顺序不变。滚到底才可读完，关闭精确接 p031；关信时两事实仍 false、day仍2。
- 17条尾段 p031:p047 全部结束后，且仅此时 story.oct07.s2.completed 与 story.oct09.meeting.known 为true，回原 PreNightPanel。无物品/奖励/额外任务。
- 完整正式日结：PreNight → HomeNight → DayEndPanel（day3）→ DateChangePanel → MorningBeforeMap 原有 EV_TALK_CAFE 选项 → CityMapLeisure。最终 day=3、王国纪元670年10月8日、CanSave=true、NCSbusy=false、Console error=0。不是卡在 NarrativeEntry；次日上午既有选项须正常选择。
- 日结临时替换 GameSaveService 到 Library/Locus/tmp/oct07-acceptance/save-slots，并 finally 还原服务，未覆盖用户自动槽。真实 day3-after-s2.json 安全快照已写入同临时目录；另保留 day2-before-s1 / day2-after-s1。
- 已直接执行 S2 Inline 7/7、PlayMode Sequence 3/3、通用信件12/12 NUnit/协程断言（包含原开场取名/5组选项/署名提交恢复）。UTF runner 仍禁用，不称完整UTF通过。

### 视觉与预览回归进度

- 正式信纸修复：纸面按原 sprite 比例、屏幕80% fit；滚动区域及读完控件在纸内，不拉伸纸图，generic恢复保留开场布局。1920×1080 实跑纸面1275.76×864；截图见 Library/Locus/Screenshots/locus_game_20261015_155703_054.png。
- 9:16 实际 GameView 278×495：纸面与正文完整在屏内，231 token、splitTokens=0、滚轮可到底，未到底CanFinish=false；截图 locus_game_20261015_163209_541.png。信纸关闭仍接 p031。动态改变视口时按root实际尺寸重新fit，最终动态缩屏/长英文换行/数字token不拆分回归已通过。
- GM五入口已实跑：完整S1、S2知觉1、S2知觉2、知觉节点、信纸节点。全章预览也已完整推进（S1全部40行；S2两分支均含p031/p047且p020按属性分流）；取消/退出后Pause OwnerCount回基线，正式Participants序列化快照和Formal history均逐字不变，DayFlow仍day3。
- 已修直接信纸节点的空舞台等待：改为就绪后autoAdvance，不再在未ready时调用Skip；自然播完在根事件结算时释放chapter自身pause。实际打开 `Assets/Scenes/PresentationDebugScene.unity` 实跑五入口及全部完整分支：S1 40条、S2低33条/高34条对话，均自然结束Pause OwnerCount=0，不需额外Esc。知觉直达重建oct07.cafe.night/tint(.42,.48,.62,1)、左右人物为空；信纸直达自动进入Envelope。GM外层独立会话暂停继续按原ExitTest释放。
- 使用虚拟InputSystem Keyboard真实Enter输入关闭共享信纸，首句精确为oct07.s2.p031；无同次输入穿透。新增Enter/Scroll回归直接协程执行通过。
- 最终回归首轮失败均已定位并复测：动态缩屏fixture原在宽屏已全文可见，保留合法已读状态并加长测试正文；S1重试fixture改从false初值构造，不回写onlySetTrue事实；CafeVisit取消清理Manual candidate并跳过失主批次，持有pumping防EventFailed边界重入，保留原失败断言并加强迟到回调/显式重试检查。最终全部通过。
- 原字体缺部分标点/三角符号的警告及纸图内嵌装饰为既有资源现状；未改正文以绕开字体。圣徒街、小心翼翼/沮丧差分和猫叫继续明确占位/缺失，不冒用无关资源。

### 最终 C06、存档与回归收口

- 最终统一编译：compilation=completed、domain_reload=completed、bridge=ready。之后直接调用全部相关Test/TestCase/UnityTest方法，执行SetUp/TearDown并驱动协程：**59/59通过**（NarrativeEntry14、CafeVisit12、Inline7、Letter14、Sequence9、Dialogue C06 3），Console error=0。此为Editor内直接逻辑/协程断言，**不是完整Unity Test Framework runner**；专用测试工具仍被工作区禁用。此前31+24批次仅扫描Test方法，最终补齐TestCase与新菜单取消两例，以59项为最终口径。
- 三个真实安全快照均通过正式ConfirmLoad确认→CoffeeShop场景恢复→Map安全边界：day2-before-s1为day2/S1false/S2false；day2-after-s1为day2/S1true/S2false；day3-after-s2为day3/S1true/S2true/meetingtrue。每次最终CanSave=true、NCSbusy=false、Pause OwnerCount=0，无重复结算/重播已完成章。全部manual slots只写入Library/Locus/tmp/oct07-acceptance/final-load-slots，finally还原原GameSaveService，未触碰用户槽。
- 三个restore的PlayerState、DayFlow及FreeCafe世界JSON逐字相同，NCS既有候选/事实/行动/seen记录未丢失；两个较早快照仅补入新增S2/meeting默认false键，每次场景恢复cafeVisitVersion递增1，其余NCS字段保持。恢复后快照为同临时目录下*-restored.json，不要求旧版本原始JSON逐字相同。
- 正式S2外部DialoguePanel.Stop：从p003/NarrativeEntry返回CityMapLeisure，day仍2、两事实false、busy=false、CanSave=true、pause=0；再次结束当天准确重进oct07.s2/p003。旧回调不结算/once不消耗由最终Sequence/Entry断言覆盖。
- 最终正式菜单实跑发现持久NCS UI不会随场景自动卸载：原OpenMainMenu仅暂停presenter，在已Reveal信封的阶段留下busy与两项pause。根修为先设Menu并HideForMainMenu使DayFlow回调失效，再调用可复用Presenter.InterruptForHost释放整个复合舞台，不用Dispose终止后续会话。sceneUnloaded也复用同一入口。
- 修后分别从已Reveal的Envelope及已打开的Reading退出菜单：session=Menu、busy=false、dialogue=false、letter=false、pause=0、S2/meeting事实false，Console error=0；退出后重新正式读取day2-after-s1并结束当天可再进p003，外部Stop仍返回有效地图入口。新增Reveal/Read两项host中断用例检查release exactly-once、迟到page/letter/root回调失效和重新请求。
- 原开场复用回归已包含取名、五组选项、署名提交及sprite/布局恢复；未另造开场流程。独立预览与Formal history/Participants隔离、真实共享场景五入口、自然结束pause归零、虚拟Keyboard Enter关信接p031均已通过。

结论：整章代码与配置交付完成。资源替换项仍为圣徒街背景占位、阿黛尔指定差分与猫叫；不据此宣称最终美术/音效齐备。未执行Git commit，用户及其他Agent改动保留，动态字体序列化差异未擅自回滚。

## 窗口 C：革命联合后续施工 checkpoint（2026-10-16）

本节只记录本批新增证据，不扩大 10 月 7 日既有验收结论。

- 已配置（源表）：`revolution.1013.s2`、`revolution.1028.s1`、`revolution.1014.s1` 已按 `C-revolution/source-map.md` 追加到现有 Narrative/Text 工作簿。回读计数为 C13 68 段/52 条对白行/3 选择，C28 157 段/144 条对白行/3 选择，C14 219 段/171 条对白行/12 选择。C14 的 Reading 页面、匿名索雷尔显示名字段及 wisdom4/perception6/wisdom5 Inline 配置已写入源表。
- 已配置（阻塞保留）：三个根均 `onceOnly=true`、`blocksSave=true`，均没有伪造完成结果、好感/派系/信任数值或合作事实。C28 闪回条件只引用 `story.revolution.1013.s2.completed`；历史学专精仍为 C-B06 阻塞。C14 P191 作用域/读数时点、正式数值 key、B 的真实赴约完成事实和演出资源仍为阻塞。
- 官方生成：未通过。2026-10-16 02:45:52，`Assets/ConfigTables/GenerateAll.bat` 的 Player semantic validation 通过，但 Luban 因工作树中既有 `rose.1009.s1.eventKind=narrative` 不在 schema 允许的 `actor,world` 集合而退出 1。该既有行未由本批修改；失败过程中写出的部分 JSON 不能作为本批生成完成证据。
- Unity/运行：编辑器当前 `editing`、bridge ready；本批尚未完成 Unity 导入、`Assets/Scenes/PresentationDebugScene.unity` 共享场景预览，或分支/取消/一次性/存档/日程实跑。故本批 0 个事件可正式放行。C29 空稿仅登记，未创建事件、完成态、合作或结局。
- 后续放行：先在不覆盖其他 Agent 改动的前提下修复/确认上述既有生成阻塞，再重新官方生成并导入；之后严格按 C13 → C28 → C14 执行共享场景和 V13/V28/V14 验收。旧的 59/59 证据仍只属于 oct07 批次。
