# 五批内容中途核验与整合顺序

日期：2026-10-16。性质：总控只读中途审查，不是最终游戏验收。没有操作 Unity、运行生成/导入、改代码/表格或打断任何工程任务；只新增本报告。并发工作区持续变化，下面的计数与哈希仅代表取样时刻。

## 当前判断

五批 Spec/source-map/handoff 都已存在。已见大量正文入表和局部演出配置，但不能称为五批正式可玩：多个根仍有 blockedBy，批次间完成事实缺生产者，新增正式地图 Action 尚未形成。S1/S2 有工程运行报告；本窗口没有独立复跑。

旧 A/B 报告中的 5 个 UU、Presenter 冲突及 speakerNameKey 生成失败已经有后续修复记录。本轮实际 `git diff --name-only --diff-filter=U` 无输出，脚本内无行首冲突标记。不把历史失败当成当前故障。D 报告早于当前配置，不能只读它的开头认定“仍只有 Text”。

## I1：共享文件写入没有统一互斥，仍有覆盖风险（高优先级）

证据：`Tools/Narrative/configure_rose_1009.py` 在两工作簿哈希统一检查后依次 replace，无共用锁；`import_revolution.py` 逐表 save/check/copyfile，也未使用长篇工具的共用锁。`configure_church_long.py` / `import_church_long_text.py` 有 `narrative-source-write.lock`，但其他写入者不遵守同一锁，不能使全项目安全并行。

哈希检查与替换之间有窗口，跨两本工作簿也非原子事务。当前没有证据证明已发生数据丢失，不能报告“已经覆盖”；风险由当前文件写法和多写入者共同产生。

处理：保留各窗现有产物；当各窗到安全交接点后，设唯一整合工程 owner。共用 Excel/schema/生成/Unity/核心代码都串行；其他窗口可继续只读审查和准备自己的补丁/内容。若必须维持并发，所有写入器和生成都必须遵守同一个互斥协议及失败恢复记录，不能只给一个脚本加锁就放行。截止压力下推荐前一种。

## I2：跨批次事实合同冲突，导致下游无法触发（高优先级）

证据：B handoff §2 读取 `story.church.1009.s2.hat_helped`；D spec §2 的1009段只承诺 completed，明确不加永久帽子事实；当前 `configure_church_1009.py` 也仅写 completed。这不是单靠重新生成可解决的差异。

最小统一建议：保持 D 原始两选择及无奖励，新增可持久的选择历史证据供 B 读取。可复用已有持久选择历史，或采用 `story.church.1009.s2.hat_helped`，只能选择一个权威读取方式。若采用 flag，在选帮助且 D 全根正常完成时一次写 true，选择忽略则不写 true；不能命名为“抓住帽子”，原文并未保证抓住。取消、预览不写；B 必须同时区分 D 尚未完成和 D 已完成但没帮，不能把未知前置当失败路径。

此处是为已写下游提供历史事实，不新增剧情、好感或物品。整合 owner 在合同表中记录实际生产者、读取者和验证用例，修改相关施工规格的一处权威说明后落实。

## I3：大量根处于施工门禁，完成事实生产链不闭合（高优先级）

取样时，rose.1009.s1 受 B 尾部/地点门禁约束，缺真实 completed；neutral.1010.s1 缺正式完成生产者；D1009/C14/后续 B 内容依赖这些上游。已有 blockedBy 的根在 `NarrativeController.Entries.cs` 的 EventCanStart 中被显式拒绝，不会自动靠“数值到了”恢复。

处理：先解除最早事件的具体内容/入口/结果问题，验证后再开放；不能批量清空 blockedBy，也不能 GM 正式置完成旗来冒充可玩。隔离预览可显式给测试前置，但不作为正式通路证据。

推荐优先顺序：S1/S2 稳定版本 → A 10月8日 → B 10月9日 → D 10月9日 → A 10月10日与 B 10月11/12日按真实前置 → A/C 10月13日 → C/D 10月14日 → 更晚内容。E 11月4日长篇单列，缺制作/经营跨阶段能力时不能拖住早期闭环。

## I4：公共数值与时间线未定，不能用各批默认值拼接（内容阻塞）

A-B010、C-B05 都需要统一正式角色好感/派系倾向/信任的量表方向、key、初值、范围与封顶策略。只做布尔完成事实不能替代原文 +1/+2 结果。

A 的10.13旧S1与实际S2别名、“两天后”与日期、10.10两份文案的营业前后顺序另需裁决。代码新增优先级无法修复文本时序矛盾。关系/金额无权威数据时继续隔离预览并列阻塞，不填0、不删结果。总控应集中最少的产品决策，不让五窗各定一遍。

## I5：公共分支能力不能按旧报告一刀切判缺失（待实跑确认）

当前已有 ReadPresentationCondition 的 choice/anyChoice/not/check，以及 Inline 快照字典、ATTRIBUTE 条件等增量。部分旧报告仍称“只有单 activeAbilityCheck”，但那是特定主动分支接口的限制，不足以证明所有根内多次被动检查均不支持。

整合前先按实际用法列能力测试：两组选择互斥/汇合、两次独立 Inline、选择后真实属性条件、根尾结果一次性、取消重试、信件插入恢复。能用现有接口完成就配表，不让每批重建分支系统；做不到的最小公共补丁只由一个 owner 实现并回归 oct07。

## 静态核验结果与限制

- 本轮源表快照中新增批次 Text 与已生成 tbtext 的逐 key/文本比较见下方机器记录。相等只证明生成文本一致，不证明与全部 DOCX、分支路由和正式入口全量一致。
- 批次 Event/Choice/ConversationPage/Text 主键抽查未发现重复。Bar 的 barId 单列重复不构成主键冲突，schema 使用 ownerId+barId 复合键；CafeVisitHook 为 list，不能误报为 map 主键重复。
- 审查期间 E 新根写入早于 JSON 刷新，出现过源表领先生成物。活跃工程中这属待完成步骤；最终必须在暂停写入后重新生成、完整导入、编译与真实加载，不把其中一刻状态当损坏，也不跳过最终一致性检查。
- 本报告未启动 Unity，不声称当前公共编译、所有分支或最终 Build 已通过。

## 交接时必须做的五件事

1. 五窗各留下实际修改清单、最新配置 ID、最后成功生成/运行证据、阻塞和下一步。报告按更新时间看续做段落，不从旧开头复制现状。
2. 整合 owner 建一张事实合同表：key、类型/默认、生产者与提交点、消费者、预览夹具、验证结果；先统一帽子、关系和事件别名。
3. 确定唯一写入者后做源表备份/可恢复基线，保留全部现有差异；源表与生成物/程序集的最终一致性在同一稳定检查窗口验证。
4. 先跑一条真实早期链路，再逐批开放；按“文本已入库 / 可预览 / 正式触发 / 分支与结果验证”四档报告。仍有门禁的一律不算正式交付。
5. 新增公共能力后回归 oct07 的地图回接、读信、知觉1/2、取消和日结，再检查相邻批次完成事实。正式源测试不靠手动设置完成值冒充上游。

本轮不自动向其他 Agent 发送消息、不修改它们的任务；上述方案作为用户的整合建议和后续工程接手依据。

## 本次最终只读快照

```json
{
  "checked_local_time": "2026-10-16T03:03:18",
  "source_hashes": {
    "Narrative.xlsx": "0e8ae2afd21ec3b8a7f0dde03fc67d1f86d780ba1e656f7f26090e7b22e29726",
    "Text.xlsx": "9d04c269be4fa3f991f074b02b30a9ff3f9498dd8eae43eaa676f81d221391a6"
  },
  "stable_during_final_check": true,
  "batch_text_keys": 1828,
  "text_vs_generated_mismatches": [],
  "batch_actions": 0,
  "batch_events": [
    {
      "id": "rose.1009.s1",
      "properties": "entry=MapAction;blocksSave=true;blockedBy=B-B01+B-B02;contentBoundary=P071"
    },
    {
      "id": "revolution.1013.s2",
      "properties": "entry=Immediate;blocksSave=true;blockedBy=C-B01,C-B02,C-B05,C-G04"
    },
    {
      "id": "revolution.1028.s1",
      "properties": "entry=BeforeNight;blocksSave=true;blockedBy=C-B05,C-B06,C-G07"
    },
    {
      "id": "revolution.1014.s1",
      "properties": "entry=MorningBeforeMap;blocksSave=true;blockedBy=C-B03,C-B04,C-B05,B-real-completion,C-G02,C-G05,C-G07"
    },
    {
      "id": "church.1009.s2",
      "properties": "entry=Immediate;blocksSave=true;blockedBy=D-B03-rose-real-completion"
    },
    {
      "id": "neutral.1008.s1",
      "properties": "entry=MapAction;blocksSave=true;blockedBy=A-B010-relationship-definition"
    },
    {
      "id": "church.1104.s1",
      "properties": "entry=MorningBeforeMap;blocksSave=true;blockedBy=E-B01,E-B02,E-B03,E-B04,E-C01,E-C04,E-resources-unverified"
    },
    {
      "id": "church.1023.s2",
      "properties": "entry=Immediate;blocksSave=true;blockedBy=D-B01-release-failure-unavailable,D-C05-ending"
    }
  ]
}
```
