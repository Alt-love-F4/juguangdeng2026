# 革命联合 CURRENT 内容编译器交接（分支级阻塞修订）

日期：2026-10-19。本文取代之前「8个革命根整体阻塞」的结论。**当前编译输出没有整根施工门禁；仍有两个明确未实现的可选节点。编译及静态测试通过，不等于Unity整线实机验收。**

## 1. 修改范围与 API

只修改三个自有文件；未写Excel、生成JSON/C#、Schema、Runtime、场景或其他内容模块：

- `E:/myproject/unity_p_taptap2026/Tools/Narrative/Content/revolution.py`
- `E:/myproject/unity_p_taptap2026/Tools/Narrative/Content/test_revolution.py`
- `E:/myproject/unity_p_taptap2026/Docs/NarrativeRevolutionContentHandoff.md`

```python
from pathlib import Path
import revolution
rows = revolution.build(Path("E:/myproject/unity_p_taptap2026"))
# Existing Narrative sheets + Text; no new story schema.
# rows['_audit']: every source anchor, source/text hashes, decisions, scoped issues.
# rows['_replace_roots']: only eleven owned revolution roots; NOT oct07.s2.
```

纯Python标准库、无写入或生成。DOCX是唯一文案来源；CharacterCatalog/注册背景只读验证，Text.xlsx只读旧/新差异审计。每个非空正文段含表格段都有锚点；源清单/段落文本变化立即报错，防止旧段号计划悄悄读新稿。

## 2. 源覆盖与当前触发状态

12份现稿、2,905个非空段。保留工作中的oct07.s2，输出11根、2个Ending。`_audit.summary.blocked_roots=0`，`pending_optional_nodes=2`。所有已知根仍受**真实完成前置、日期、路线、互斥状态**限制，零施工门禁不是无条件可启动。

| 当前文件 | 段数 | 根 | 输出状态 |
|---|---:|---|---|
| MA70.10.07-S2 | 47 | oct07.s2 | 保留，不替换 |
| MA70.10.13-S2 | 68 | revolution.1013.s2 | 中立活动真实根尾Queue |
| MA70.10.14-S1 | 219 | revolution.1014.s1 | 正常主干；仅P201信任奖励未定义 |
| MA70.10.28-S1 | 159 | revolution.1028.s1 | 知识/回忆/三个回应 |
| MA70.10.29-S1 | 628 | revolution.1029.s1 | 未拘押+三种释放、玫瑰关系、合作选择 |
| MA70.11.07-S1 | 345 | revolution.1107.s1 | 主干与检定；缺图不阻塞 |
| RE70.11.11-S2 | 20 | revolution.route.1111.s2 | 中央实际选革命后Queue |
| RE70.11.12-S1 | 210 | revolution.route.1112.s1 | 真实数值检查、手枪历史事实 |
| RE70.11.15-S1 | 340 | revolution.route.1115.s1 | 检定、会议、徽章历史事实 |
| RE70.11.22-S1 | 372 | revolution.route.1122.s1 | 仅P233–234可选推断fail closed |
| RE70.11.23-S1 | 301 | revolution.route.1123.s1 | 三个原稿相同「开枪」选项 |
| RE70.11.24-S1（结局） | 196 | revolution.route.1124.s1 | 多日后日谈、互斥End01/02 |

段落均分类/映射；演出指令分类不意味着所有声音/CG/镜头已制作。

### 当前文本与旧配表

`_audit.configuredTextDiff` 只读Text.xlsx，比较旧ncs.*键与当前文本，旧a/b拆分合并比较。本轮原工作簿审计基线：C13的P065「承载者→承载着」、C14的P117改为「⌈这话说得倒是直接——那么，你认为应该由谁来统治这个国家呢？⌋」。C28全159段重新锚定，含旧段号移动/分类变化；旧140/新142文本锚点、68同段号变化**不是改写68句**。中央合并后差异计数会变化，不能把旧计数当永久测试前提。

## 3. C14：修复九日主链断点，保留真正未知的可选效果

- 必须 `story.rose.1011.s1.completed=true` 与 `story.oct07.s2.completed=true`，day9、MorningBeforeMap、未完成。没有 `blockedBy` 或 `integration.<root>.ready` 根条件。
- 勇气≥6控制对应选项；智慧≥4、知觉≥6、智慧≥5各按原稿作用域。
- 当前219非空段稿的复合条件在**P191**：语言≥3 AND 革命倾向≥2；P217实际是茶杯叙述。用Inline语言检查加数值guard，条件结算仍在根尾。
- 革命倾向读中央可变 `faction.revolution`，不新增副本。c4/c5尚未结算的选择增量按12种实际组合投影，基线0时选c5.1即可满足≥2；不会因为「奖励还未根尾提交」而永远不进可选对话。
- 仅P201「革命联合信任+1」没有契约权威Bar ID。`C14_REVOLUTION_TRUST_BAR_UNRESOLVED`只标记该**可选效果**，不把信任冒充倾向、不伪奖励，不挡对白、主干、completed或known.sorel。
- `_audit.sources[].branchIssues` / `_audit.unresolvedDependencies`有scope=optional-effect、blocksRoot=false、段号及false声明。当前不输出未知目标Result。后续确定ID再补准确的条件根尾效果。

## 4. C29：具体原稿矛盾与有界复用方案

### 4.1 未拘押内容并非整段缺稿

当前P050/P069的标签写「说服与贿赂」；但P064明确说「就因为我没被他们抓走？」。P051–081本身已有调查未拘押的完整对话。P083–154则明确问10/23「从看守所里出来」并分别回答说服/贿赂。P156–198是营救专属对白。不能让未拘押玩家冒认10/23获释，也不应因矛盾标签锁整根。

编译中有界复用（不改源、不增新对白）：

| 实际来源 | 真实事实/父根 | 播放 |
|---|---|---|
| 10/20洗清未拘押 | church.1020.s1实际完成 + `story.church.1020.s1.cleared=true`，三释放false | P051–081，含P064；跳过P083–198全部获释/营救内容 |
| 10/23说服 | church.1020.s1、church.1023.s1实际完成 + `church.release.persuasion`，cleared/另两释放false | P051–081但不播P064；P083–112说服部分 |
| 10/23贿赂 | 同上 + `church.release.bribery`，其余来源false | P051–081但不播P064；P083–085与P115–153贿赂部分 |
| 10/23营救 | 同上 + `church.release.rescue`，其余来源false | P156–198；不播非营救审问 |

现有P199「那么，接下来谈谈新玫瑰会吧」复用为四种来源的共同过渡，接P201；不新增句子。源标签矛盾与该重用在`decisions`中明确登记，属于编译映射决定，不声称原稿标签本来就准确。

四来源×两玫瑰状态共8组事件DNF。每组均含日期、未完成、C1028、真实C1020父根；仅三释放组要求C1023完成。**无来源、多个来源true、两个玫瑰状态相同均fail closed**。中立促销完成不是「未拘押」或「获释」事实，不能替代真实父根/结果。

### 4.2 10/26仅合作方可达，不能当全根前置

当前10/26源P006/P023仅向已合作玫瑰的玩家发邀请。因此：

- `story.rose.1024.allegiance`分支才要求 `story.rose.1026.s2.completed`，P268–284的10/26传话回顾也同样限定。
- `story.rose.1024.safe_refusal`分支不要求10/26完成，跳过该历史回顾，P285开始的现有神佑者/读心能力说明与其余会谈保留。
- 不把未知玫瑰关系当拒绝，不生产假的10/26完成。
- C29最终选择只在根尾生产`story.revolution.1029.s1.cooperated/refused`，并只结算所选倾向+2/-2；两种选择均实际见面，因此成功根尾均可生产known.mary。

**跨模块实际可达性提醒**：当前玫瑰1024-S2编译入口只接受贿赂/营救，尚不接受「说服获释」。C29保留准确说服分支，但能否正常到达其玫瑰父链必须中央与玫瑰/教会模块核对；不能靠本模块造一个玫瑰合作事实解决。本模块不因此给其余C29来源加整根施工锁。

## 5. 其他局部未完成与非阻塞项

### 唯一另一个fail-closed节点：RE1122

- P232的语言检定没有数字门槛，也没有完整失败/成功块。仅将P233–234两句「判断/推断」放在false的局部guard，`RE1122_LANGUAGE_THRESHOLD_UNSPECIFIED`，scope=optional-dialogue、blocksRoot=false。P235开始的是新的主动提问，至P260的现有会谈保持公共内容；不伪造检定成功或隐藏信息奖励。
- c3第三选项在源中没有独立回应：选完直接进入既有共通后文，零新增回应/奖励；这不需要锁根或假设成功。
- c4仅有两个按钮。P304误标选项③，但P305–309的「我明天会待在咖啡馆」明确回应P295第二按钮，而非P294政治话题。映射到c4.2，原误标留在audit，属于已登记语义映射决定，不改原DOCX。

### 物件、连续性、美术

- RE1112手枪与RE1115徽章目前只作为**源叙事历史事实**；无重复库存、未知ITEM_ADD、武器战斗系统或物理物件回调。源没有要求玩家实际操作这些物件，故不再因为「物件究竟算哪个host库存」锁整根。
- RE1123 P193让阿黛尔拿枪、P288主角用同一把枪仍有连续性疑点。准确保留文字并记录，不编造交接玩法；没有条件分支以虚构库存为成功前提，因此它是文案修订项而非触发门禁。
- 缺图一律artPending、blocksRoot=false；缺少对应外景/房间资源时使用已注册纯色 `church.execution.black` 作中性背景，不把咖啡馆实景错误继承到市政厅/北港/密道。
- 普通咖啡馆明确 `scene.cafe`，不改用户已测试咖啡馆交互，不挂全屏经营底图、不覆盖原特殊CG。
- 当前C14红茶是原稿叙述动作，不偷换为blackcoffee、不假造红茶recipe/订单/成功Gameplay。若后续要改为实操，须真实产品与已有Gameplay桥接；本模块不自动完成任何Gameplay。咖啡馆/教程原有链未重写。
- 镜头、音效、CG指令都有原始audit锚点，未全部变成演出调用；不把「已分类」说成「美术/演出全部完成」。

## 6. 终局：复用已存在宿主，不再假设缺失

已只读核实 `E:/myproject/unity_p_taptap2026/Assets/Scripts/GameFlow/UI/GameFlowBootstrap.cs`：

1. BindNarrativeSession订阅EndingSelected。
2. OnEndingSelected进入GameSessionState.GameOver。
3. EnterGameOver调用DayFlow.StopForGameOver、DialoguePlayback.Stop、NCS Runtime.StopForGameOver，清UI栈、Time.timeScale=0，并显示ResultPanel。
4. 读档已有SelectedEndingId也会进入同一路径。

所以删除原先 `RE1124_TERMINAL_HOST_LIFECYCLE_UNRESOLVED` 整根假阻塞。RE1124挂真实11/24 MorningBeforeMap，依赖RE1123实际完成；一个演出承载多日后日谈，没有Gameplay、ADVANCE_DAY或返回营业的QUEUE_EVENT。

- 第一终局选项勇气≥18；End01的End截断第二结局。
- 两个既有Ending表结构行：`revolution.end01` / `revolution.end02`；各依赖所选终局fact与 `story.revolution.terminal`。
- completed/terminal/selected仍在**成功根尾同批结算**，随后走上面的既有GameOver。没有进入演出就提前提交旗帜。
- 活跃演出期间保留现有宿主占用和输入/Gameplay规则。**取消演出仍是事务取消**：没有terminal/ending选中，不声称「一旦点开后日谈，就算取消也已经不可逆终局」。若产品要这种不可逆进入/强制重播行为，需要中央另定宿主规则；本次不把其当已实现或当整根门禁。

## 7. 集成IDs与所有权

- C13真实前置 `story.neutral.1013.s2.completed`；中立编译器现有根尾QUEUE_EVENT已指向 `revolution.1013.s2`，不另挂日期自动播。
- 中央 `church.1111.s1` 实际选革命后才结算唯一route flag，再QUEUE `revolution.route.1111.s2`；革命模块不生产路线旗帜。
- known.sorel：C14成功根尾，Object=sorel，不用emmanuel。
- known.mary：C29成功根尾，Object=marie，合作/拒绝均已相识；没有新建mary。
- known.lesou：C1107成功根尾，Object=lesou，不新建重复ID。
- known.eugene：12稿未发生实际初见，只有false声明，不能为日常系统造相识。
- marie的Object默认「雅克·马尚」，所有其台词用speakerNameKey覆盖当前时点的「？？？/玛丽/玛丽（？）」，不提前泄露。lefebvre=勒费弗尔，不冒充勒苏或玛丽。
- 后日谈杜瓦尔在门后，只显示原文逐行名字，不放其立绘。
- 无具名身份的工人/代表等用11个无立绘通用Object，审计见genericActors，未替换已有45个具名Object。
- Bar与route声明归中央；完成事实由其所有根生产。替换仅 `_replace_roots` 的11个革命根；保留oct07.s2，旧全根integration.ready声明即使被中央留在表里也不再是本模块任何根的条件，不能拿旧声明存在推断根仍被锁。

## 8. 本轮验证与中央剩余工作

```powershell
python -B -m unittest discover -s Tools/Narrative/Content -p test_revolution.py -v
```

**37项测试通过**。新增/强化重点：

- C14基线faction.revolution=0正常启动、可进入语言/倾向复合对白、正常完成并生产known.sorel；无未知信任奖励。
- C29四种真实来源×两种玫瑰状态，完整8组DNF；全部16种来源布尔组合×4种玫瑰组合拒绝未知/矛盾。
- 未拘押玩家无需C1023完成，不说「10/23获释」、不生产释放事实；未拘押也不能靠中立完成替代C1020真父根。
- 拒绝玫瑰合作者无需C1026完成，不播放传话历史；合作方仍必须真完成。
- RE1122仅未定可选推断关闭，第三选项可收束、第二问题正确回应、主干可到End。
- 两结局互斥、Ending行真实条件、既有GameFlowBootstrap终局接线代码核实、无新增Gameplay/推进日期逃逸。
- 缺外景美术非阻塞，用注册中性背景，不继承咖啡馆。
- 仍保留原32项中的源覆盖/哈希、漂移fail closed、当前文本、前置、日期、检定门槛/专精OR、结果根尾所有权、取消假设、阅读、身份、资源、无Excel/DOCX写入等回归。

测试中的PresentationModel仅是输出真值表oracle，不是新增游戏执行器。测试没有模拟完整CafeWorld/DayFlow、没有实操Unity输入。本轮未生成Luban、未编辑Excel、未跑Unity PlayMode/Build或正式通关。

中央下一步：

1. 合并本模块当前v2输出（不要沿用v1的8个根blockedBy），去掉这些根的旧条件/旧结果，再官方生成。
2. 真正跑C13促销后Queue→C14初见→后续教会调查→C29四种来源；先查真实上游结果，禁止GM假完成。
3. 核对玫瑰1024-S2是否应覆盖说服获释；这是具体上游分支可达性问题，不是把所有革命根锁掉的理由。
4. 在既有PresentationDebugScene.unity验证图片→实景、阅读→对白、选择、取消、重播、timeScale=0；正式游戏验证咖啡馆实际交互不被剧情覆盖。
5. 用真实最终日期和正常数值走两Ending，核实自动停DayFlow/营业/存档与返回菜单，特别覆盖取消/重播/读档边界。
6. 只给两个局部未完成节点补设计与实现，别将全部integration.*设true或凭缺图锁主链。
