# 正式结局核心交接（2026-10-19）


## IF0 开场拒绝增补（同日主集成新增，以此处为最新覆盖口径）

主集成发现已写明的开场终点“未开始就已结束”原本直接返回主菜单，不经过 Ending/ResultPanel。中央现补正式 `arrival.ending.if0`，因此最新覆盖应为 **16 个启用结局（原四线15 + 开场IF0），总18行含2个禁用旧测试结局**。

- 新开场 Ending 由中央 `integrate_content.py` 的默认配置合并阶段维护，不属于四线编译器原有 15 条声明；不要为了匹配数量向四线模块重复添加。
- 复用已有原稿文本 `arrival.refuse.01.08`（标题）和 `arrival.refuse.01.04`（消息），不编造结局说明。
- 每个 OR 条件组必须同时要求 `story.arrival.leave.completed=true` 与 `EVENT_HAPPENED arrival.leave`。完成事实只由 `arrival.leave` 根尾生产，初次拒绝/重新尝试不能生产。
- 本文件的全量覆盖契约已改为显式 **四线15 + IF0**，并增加上述 IF0 表结构/文本/生产者检查；四线各项正文夹具仍保持原样，不把 IF0 硬套进 Conversation End 模板。
- 主集成拥有开场专用测试文件，验证真实拒绝→离开→结局面板、重试继续以及取消/恢复边界；本 worker 不重复修改该文件。
- 下文 **46/46** 为 IF0 接线前、四线15项版本的实际离线证据。增补后用例数量仍为46，但全量契约已要求16；必须等待中央官方生成与最终 EditMode 结果，**不能把此前46/46直接当作新16项配置验证完成**。

## 范围及写入冻结

本 worker 仅新增：

- `Assets/Scripts/Narrative/Tests/NarrativeFormalEndingTests.cs`
- 上述文件的 `.meta`
- `Docs/NarrativeEndingCoreHandoff.md`（本文）

**C# 已完成且冻结，可以中央运行 Unity EditMode/PlayMode。** 没有修改现有 Controller/Runtime、GameFlow、Prefab、Excel、生成 JSON、内容编译器；没有运行 Unity 作业，没有调用图片或其他网络 API。

主集成负责 ResultPanel / GameOver / 调试场景预览及空白需求图。本文只认证核心条件/事务，不认证面板、真实制作、存档宿主或自然通关。

## 总结：三种状态必须分开

1. **配置与原稿覆盖：** 初次审计时启用 15 个四线 Ending（中立 4、新玫瑰 5、教会 4、革命 2），另 2 个旧测试结局禁用。逐项对比四个当前源稿编译器的内存 `build()` 输出与正式生成 Ending 集合，完全一致；源文件 hash/段落漂移检查通过。
2. **真实根的核心结算：** 12 个结局末尾根在显式历史夹具下，以实际生成配置、实际 `NarrativeController`、实际选择/检定/根尾结果完成并选中正确 Ending；验证取消不完成、重复迟到回调无效、已结束快照恢复不重播。不是手写 `terminal=true` 后宣称完成。
3. **正式新游戏可达性：** 不等于上述 12 个均自然可达。3 个结局项被结局根自身的作者闸门关闭；另 3 项末尾根能跑但上游有明确根级阻塞；其余 9 项没有被本次三处已知根级闸门直接/传递挡住，**仍不能声称 9 条完整新游戏路线已经人工验收**。

本次未发现需要修改既有核心 Controller 的新缺陷。初轮失败来自测试夹具日期及前置报纸顺序，已按正式源修正；没有改动正式剧情来让测试变绿。

## 15 项逐结局核对

表中的“根尾”均指 Presenter 最后真实完成回调被 Controller 接受后才提交；取消、仅显示 End 或仅到达日期都不算完成。

| Ending ID | 正文终点根 | 根尾证据及关键分支 | 核心测试 / 可达性限制 |
|---|---|---|---|
| `neutral.ending.a` | `neutral.route.1111.s1` | `story.<root>.ending.a` + `story.<root>.completed` + `route.neutral` | 根完成/取消/恢复通过；全量初始流程未认证 |
| `neutral.ending.b` | `neutral.route.1122.s1` | 实际选择 `choice.future.b` 才产 `ending.b`，另需 completed + route；未来 A 是留下，不能误选 B | 两选择互斥通过；显式夹具种入 1118 完成及实际庇护历史，非自行支付贿赂 |
| `neutral.ending.c` | `neutral.route.1130.s2` | ending.c + completed + route；未入会与实际谈判失败是 OR 组，各组共同前置完整 | 非会员直接根通过；额外验证真实 D 检定失败 → `QUEUE_EVENT` C 正文 → C 结局，不直接置 C 终点 |
| `neutral.ending.d` | `neutral.route.1130.s1` | 真实 Inline `check.negotiation`（language≥5）成功才产 ending.d，另需 completed + route | **末尾根通过但上游 1129 营收闸门关闭**；夹具显式种入 1129.completed / committee，不能宣称玩家可达 |
| `rose.ending.trust_limit.ending` | `rose.ending.trust_limit` | terminal + completed；入口还要求 pending，正式日期 **第21天/10月26日夜晚** | 根完成/取消/恢复通过；pending 的源生产者为 1024.s1 或 1024.s2 的真实拒绝分支 |
| `rose.ending.job_requirements.ending` | `rose.ending.job_requirements` | terminal + completed；1101 的真实合作拒绝路径产 pending 后排队 | 根完成/取消/恢复通过；显式 pending 历史夹具，不代表 1101 全路径验收 |
| `rose.route.1115.s1.ending.ideal_world` | `rose.route.1115.s1` | `terminal.ideal_world` + completed；terminal 还需要 `integration.rose.route.1115.candor.low` | **根自身关闭**：坦诚度初值、1..2 缺口、知觉失败尾声不明；不修改 readiness，不伪选低坦诚度 |
| `rose.route.ending.unforgiven.ending` | `rose.route.ending.unforgiven` | terminal + completed；1122 的 `choice.p063` 产生 pending 并排队 | **末尾根通过，上游 1115 关闭**；1122 要求 1115.completed + plan_known |
| `rose.route.ending.bloom.ending` | `rose.route.ending.bloom` | terminal + completed；1122 的 `choice.p064` 产生 pending 并排队 | **末尾根通过，上游 1115 关闭**；不得以有尾声图/文本声称全线可达 |
| `church.ending.if2` | `church.1023.s2` | terminal.if2 + completed；结果还守卫 `church.detention.release_failed`，入口要求真实拘押/释放失败历史 | 根完成/取消/恢复通过；夹具明确拘押事实，不跳日/不制造释放成功 |
| `church.ending.if4` | `church.1111.s1` | 实际 `choice.s1.2` → `choice.s2.2` 且 language<4；terminal.if4 + completed | 根完成/取消/恢复通过；language=4 同选择不产 IF4。先完成当天正式 `newspaper.1111` 阅读，才进入真实分流正文 |
| `church.ending.ry1` | `church.route.1123.s1` | `church.thibaut.followed` → terminal.ry1 + completed + route.church；RY1 End 防止落入 RY2 | **根自身关闭**：RY1115 旧选项引用歧义，必须作者确认；不授予 construction.ready，不伪造 `host:end_business` 成功 |
| `church.ending.ry2` | `church.route.1123.s1` | not:church.thibaut.followed → terminal.ry2 + completed + route.church | **同一根关闭**；有配表/终点不等于玩家可选 |
| `revolution.end01` | `revolution.route.1124.s1` | 真实 `c0.1`（courage≥18）产 end01.selected；另需 shared terminal | 根完成/取消/恢复通过；选择后的第一结局 End 确实终止，不串进第二结局 |
| `revolution.end02` | `revolution.route.1124.s1` | 真实 `c0.2` 并逐个回答 c1/c2（夹具选 `.1`），产 end02.selected；另需 shared terminal | 根完成/取消/恢复通过；不会出现 end01.selected |

### 源稿定位

- 中立 A：`a-中立（共通+支线）/入线MA70.11.11-S1(中立结局A）.docx`，P284 根尾。
- 中立 B：`入线MA70.11.22-S1包含中立结局B.docx`，P092/P093 选择，P219 结局。
- 中立 D：`入线MA70.11.30-S1中立结局D商业大亨.docx`，P059 检定、P107 失败回接、P225 终点。
- 中立 C：`入线MA70.11.30-S2中立结局C.docx`，P131 终点。
- 新玫瑰两短结局分别来自 `MA70.10.26-Short Ending 1 信任的界限.docx`、`MA70.11.01—结局分支 Short Ending2 工作需求要看清.docx`。
- 理想世界：`NR70.11.15-S1 结局分支-Normal Ending 理想新世界.docx`，P099 +2、P104 <1、P115 >2，没有初值和中间区间规则，P138 知觉失败回接缺失。不能推断“第二选项=高坦诚度”。
- 两长结局：`NR结局-结局分支—BAD Ending：无法原谅.docx`、`NR结局-True Ending：另一种绽放.docx`，通过 `rose.route.1122.s1` 的 P063/P064 二选一路由。
- 教会 IF2：`a-王庭教会（共通+支线）/MA70.10.23-S2-End if2.docx`；IF4：`MA70.11.11-S1.docx` 的 P066-P077 失败分支。
- RY1/RY2：`RY70.11.23-S1【结局1&2】.docx`；编译器标识 `source.RY1115_duplicate_choice_reference_needs_author_confirmation` 是作者判定阻塞，不是玩家未达成成就。
- 革命：`a-革命联合（共通+支线）/RE70.11.24-S1（结局）.docx`，P025 第一结局 End，P196 第二结局 End；不能展开为一次自动同时完成两终局。

所有上述路径均相对于 `策划文件/文案`，实际 hash/完整段落锚点由现有四线 `_audit` 保持单一来源，本 worker 不再建立平行剧情字段或复制整份原文。

## 条件图 / 生产者审核细节

- 15 个四线启用 Ending 全部至少有 2 个不同的肯定证据（terminal/selected + completed/共用terminal）；所有组都是 `BOOL_EQ true` 的 AND，当前每个 Ending 仅 group 1，无空条件、无“日期到了就结局”、无初值 true 的结局事实。
- 所有非 route 结局条件事实的 `SET_FLAG true` 生产者都归属对应真实正文根；不存在无关演示、外部事件或 Choice owner 提前直接写结局事实。
- route 由 `church.1111.s1` 的真实选择结果生产，不用测试初值模拟正式入线。测试中的 route 是明确历史夹具，正文根条件照常评估。
- 当前中立重复 route 条件、教会重复 terminal/completed 条件仍在同一 AND 组，语义冗余但不产生 OR 放行；本任务不擅自清表或改源码。
- `NarrativeController.CompleteEvent` 先在清理 choices/checks 前冻结 `ownerType=result` 守卫，再一次预检/提交。测试实际走了这些路径，包括 Choice.resultOwner 的 `neutral.stayed`，不绕过真实结果归属。
- `NarrativeConversationTraversal` 直接复用现有 internal 生产游标（已向 Core.Tests 公开），并用 `ReadPresentationCondition` 读取页守卫。只在达到真实 End 后，测试宿主才允许调用 pending completion；不使用 ImmediatePresenter 对正文盲完成。
- 当前 12 个可直接测试的末尾根路径没有必须经过的 Gameplay（IF4 language3 不进入 language≥6 的 latte 步骤）。若未来正文新增玩法，测试会明确失败“requires a real host”，**不会发布伪成功 receipt**。RY 根目前被挡在入口，未对其 `host:end_business` 造回调。

## 实际验证结果

### 已执行（本 worker）

1. 四线 `build(Path)` 内存构建和源 hash 校验；4+5+4+2 条结局声明与当前启用 Ending 集合一致。
2. 使用真实生成 Config / `LubanNarrativeLoader.Load()`、现有 Controller/State/条件/结果/游标，在隔离 .NET 便携测试宿主执行新增用例：**46/46 通过**。
   - 1 项全量覆盖/标题/消息/结果生产者契约。
   - 15 项逐结局正式入口与安全边界用例：其中 12 项实际播放根/做选择/检定/根尾结算/快照恢复，3 项明确作者阻塞的入口拒绝。
   - 12 项到达最后真实 End 后取消 + 迟到完成回调：均不写结局/完成事实，不遗留选择。
   - 15 项逐结局门禁矩阵：去掉任一不同条件事实都不结束；**这组是隔离条件契约，不用来替代前面的实际正文测试**。
   - 3 项互斥/边界：1122 留下、1130 谈判失败排队 C、IF4 language=4 非失败分支。
3. 将当前实际 Narrative 源码和整个 Core.Tests 重新使用已安装 Unity Roslyn + 真实 Unity/NUnit/Config 引用进行隔离编译：两程序集通过。旧 sidecar Runtime DLL 过期导致第一遍测试程序集编译报告旧 `PresentationCancelled` 签名缺失，刷新当前 Runtime 后通过；没有修改生产代码掩盖此环境问题。
4. 仅自有新增文件的空白检查通过。

便携证据目录（本机临时，不是工程交付物）：

```text
C:/Users/AltF4/AppData/Local/Temp/ncs-ending-core-ag3rz6t0/
  run-2.log
  unity-roslyn-runtime-compile.log
  unity-roslyn-test-compile.log
```

便携宿主对 Unity 日志、资源读取、JsonUtility 使用本机既有 shim，读取的是当前真实 Luban JSON；它不模拟 Unity 场景/UI/MonoBehaviour 生命周期，不能称为 Unity 测试通过。

### 交中央执行

```text
Assembly: Spotlight.Narrative.Core.Tests
Class/filter: Spotlight.Narrative.Tests.NarrativeFormalEndingTests
Expected: 46 tests
```

Unity 作业仅由主集成运行。本 worker 没有对 UI/Player 存档、输入 owner、ResultPanel 回主菜单实际效果作验收声明。主集成另做 15 个真实 Prefab 面板的 PlayMode，这与 Controller 核心用例是两个证据层。

## 后续工作与必须保留的边界

1. 中央合入与跑新增 EditMode，保持现有三处根级闸门：Rose1115、Neutral1129、Church1123。不可把 `construction.*` / `integration.*` / `blocked.*` 统一置真，亦不能随意关掉 Ending.enabled 隐藏未完成状态。
2. 真实新游戏人工测试优先做一个已明确结局（如中立 A、IF4 或两短结局），确认真实历史生产者 → 正式入口 → 实际根尾 → GameOver → 面板 → 回主菜单，取消不结算。
3. D 需先裁定营收量纲/周期/阈值及权威 provider，不能以钱包余额替代。未入会 C 不依赖此闸门；会员低营收 C 路径仍不能宣称完成。
4. Rose1115 需策划提供坦诚度完整规则/知觉失败尾声，再解锁理想世界与长结局前置；不要用测试中的 pending 历史声称玩家已经到过 1122。
5. RY1123 需作者确认旧选项引用，随后以共享演出调试场景和实际 `host:end_business` 验证两个 End、结局互斥，不临时新建 Panel 测试场景。
6. 美术占位不改变结局事实/分流。可以缺图仍显示已达成结局面板，不能因为有占位图就写已完成剧情。


## 中央最终验证（覆盖最新IF0口径）

官方源表生成后，Unity EditMode最终集中95/95通过，其中本文件FormalEnding46项全部实际运行通过；开场专用4项、Loader42项及资源占位3项同步通过。PlayMode结局面板及背景回归78/78通过，16条真实结局面板均显示。后半程人工完整自然通关仍未被这些历史夹具替代。完整交付和原始结果定位见Docs/NarrativeEndingAndArtDelivery.md。
