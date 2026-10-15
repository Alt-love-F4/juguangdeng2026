# E-church 工程施工记录

日期：2026-10-16。范围：王庭教会长篇 E；保留其他 Agent 修改。本记录区分源表交付、官方生成、编译与游戏验证。

## 当前结论

`church.1104.s1` 尚未形成正式可玩事件。已完成 E 长稿 Text 来源增量和原稿静态核验；根事件、日期、行动、选择、条件、结果均未写入，11.05 两份仅标题稿未创建任何配置。

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

## 当前停止点

本轮完成 Text 来源交付、官方成功生成、公共编译与真实配置加载；没有把 Text 宣称为正式可玩。本章正式根与章节预览仍未配置，下一窗口从真实前置合同及同根跨经营检查点继续，不重复 E Text 提取。
