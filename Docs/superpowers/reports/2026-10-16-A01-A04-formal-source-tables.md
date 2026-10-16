# A01–A04 正式源表配置交付

## 范围、授权与边界

本阶段依据用户明确授权，将 A01–A04 从 blocked 预览源表转换为正式配置。实现文件：`Tools/Narrative/configure_neutral_formal.py`。本代理独占 Excel 写权，完成后已交还父代理。

- 仅写 `Narrative.xlsx`、`Text.xlsx`、`GameParam.xlsx`；没有调用 Unity、运行生成、修改 C# 或生成物。
- 不调用旧 `configure_neutral.py` / `configure_neutral_remaining.py` 的 build/apply。复用 repair helpers 的只读签名/metadata 和源文证据校验函数。
- 三条正式关系：`colette.trust_player`、`adele.trust_player`、`antoine.trust_player`；owner 对 player，范围 -2..5、init/reverse=0、event_driven、ncs、autoStep=0。未改示例 npc。
- A01：day3 market，`neutral.1008.s1.action` ONCE/mustExecute=true，**requirements 留空表示 0AP**；没有 `ActionPoint(0)`（既有 PositiveInt parser 不允许 0）。replaces/focus=false；Action 和 Event 均有 day/map.active/uncompleted 条件；根移除 blockedBy。
- A02/A03：day5 morning once，优先级 100/90，entry=MorningBeforeMap；A02 要求 `story.rose.1009.s1.completed=true`，A03 要求 A02 completed。
- A04：day8 morning once，优先级 100，entry=MorningBeforeMap，要求 A02 completed；赏识只控制 visit 等七页，不能锁整根。
- A04 新增 `q2_wisdom` Inline，threshold=2；accounts 页换正式 choice/check guard。无条件 `wait_business` seq7 → afternoon seq8；无条件 `wait_night` seq16 → closing seq17。除授权 seq/pageCondKeys 外，其余既有页字段、全部 line/stage/metadata 均保留。
- 共 17 个 Result，全部 eventId=原根，completed 为各根最后 seq；13 个分支结果使用 23 个 `ownerType=result`、ownerId=resultId、groupIndex=1、condType=PRESENTATION、value=true 条件，choice 在 check 之前。
- 不写 `antoine_dialogue_unlocked` 或虚构常规活动 Action；实际消费为 A04 赏识到场/不到场。
- 只新增可调参数 `neutral.promotion.preparation_cost_multiplier=0.8`、`neutral.promotion.revenue_multiplier=1.2`，**未接账、未填 Money 结果**。资格 flags 不代表经济结算。
- 仅新增 A05 暂定广告费参数 `neutral.reporter.advertisement_fee=100`；父/后续 A05 脚本负责同步余额条件与 MoneyRemove。改价后需执行消费端脚本并统一生成。
- B completed 仅声明 ConditionKey initialValue=false/onlySetTrue=true；本包不写 B 完成 Result。不以 meeting.known 代替真实完成。B 仍受阻时不能宣称 A02 自然可达。

## 已执行校验

1. 默认只读计划，`--apply --expected-sha256 PLAN.json` 校验三份工作簿 SHA；共享 cooperative lock；每次替换前重新检查所有当前 SHA 和 DOCX/source-map SHA。
2. 写前逐份复制备份并核对 SHA；候选文件全部生成完成后回读。
3. 验证所有原有单元格：目标只允许 value 改动且 cell metadata 不变；其余 cell signature（值、类型、style、comment、hyperlink）不变。验证 workbook/sheet metadata、sheet 顺序、列数及精确追加行范围。
4. GameParam 原文件没有 core properties 且有空 `t="str"` 单元格 A20；脚本保留原 core part 缺失状态和空字符串类型，未以归一化方式豁免这些 metadata。
5. 候选回读后再次构建配置，必须零新增/零修改。正式提交后真实执行第二次 apply：`idempotent=true`、`writes=false`、`changed=false`，三份 SHA 字节级不变。
6. 648 组静态配置遍历（language/wisdom=1..3、赏识 true/false、两轮 choice）通过；稳定属性实际可达路径 A01=4、A02=5、A03=12、A04=26。A04 抽象 30 组合中有 4 个要求相同属性同阈值一成一败，不能伪造为稳定属性可达。确认未选支路不执行 check、无赏识不执行 q2、A04 所有路径均经过两个 wait 后到 closing。
7. **以上是源表静态/幂等校验，不是 Unity、真实经营或正式自然链运行验收。** 父代理后续统一生成与运行测试。

保留原有非目标 cell 数量/签名 SHA：

| 工作簿 | 原有非目标 cell | signature SHA256 |
|---|---:|---|
| Narrative | 40127 | `e362436c7bc6669249922477d03a89583e00b18a0da78214be4dfaed8dd47b58` |
| Text | 9765 | `6eb0b6ce57f6ddd6a8781c9c7f84efe70d0351a2d0c8041741e2483a7e77e706` |
| GameParam | 80 | `4e935e97ac30d2fa7a578875126c64d9ae3e91f81929a90f03fa7a0d4447c004` |

期间 CAS 曾拒绝一次并行 schema-only 修改（CafeVisit 追加 I/J 两列）。逐 cell 比较证明只有这两列扩展、没有 A01–A04 目标冲突后重新计划，以最新 Narrative SHA 为基线；本次原样保留该扩展。此前失败候选未替换任何源表。

## 精确新增行：共 90 行

以下均为 Excel **物理行号**。本阶段旧行仅改 36 cells（Narrative 21、Text 15）；未清行、未插入移动旧行。

### Narrative.xlsx：74 行

| Sheet | 行 | key / owner / 说明 |
|---|---:|---|
| Bar | 23 | trust_player / colette → player |
| Bar | 24 | trust_player / adele → player |
| Bar | 25 | trust_player / antoine → player |
| ConditionKey | 34 | story.rose.1009.s1.completed |
| ConditionKey | 35 | story.neutral.1008.s1.completed |
| ConditionKey | 36 | story.neutral.1010.s1.completed |
| ConditionKey | 37 | story.neutral.1010.s1.frugal_preparation |
| ConditionKey | 38 | story.neutral.1010.s2.completed |
| ConditionKey | 39 | story.neutral.1010.s2.antoine_approval |
| ConditionKey | 40 | story.neutral.1013.s2.completed |
| ConditionKey | 41 | story.neutral.1013.s2.orderly_service |
| ConditionKey | 42 | story.neutral.1013.s2.warm_recommendation |
| Action | 10 | neutral.1008.s1.action |
| EventDate | 8 | neutral.1010.s1 / day5 morning once 100 |
| EventDate | 9 | neutral.1010.s2 / day5 morning once 90 |
| EventDate | 10 | neutral.1013.s2 / day8 morning once 100 |
| AbilityCheck | 33 | neutral.1013.s2.q2_wisdom |
| AbilityCheckCondition | 34 | neutral.1013.s2.q2_wisdom.1 |
| ConversationPage | 264 | neutral.1013.s2.wait_business / seq7 WaitBusiness |
| ConversationPage | 265 | neutral.1013.s2.wait_night / seq16 WaitNight |

Result 的精确物理行、根尾 seq 和值：

| 行 | resultId | seq | type / target / value |
|---:|---|---:|---|
| 76 | neutral.1008.s1.result.q1a.colette | 1 | REL_ADD / rel:colette>player / +2 |
| 77 | neutral.1008.s1.result.q1b.colette | 2 | REL_ADD / rel:colette>player / +1 |
| 78 | neutral.1010.s1.result.q1a.adele | 1 | REL_ADD / rel:adele>player / +1 |
| 79 | neutral.1010.s1.result.q1b.frugal_preparation | 2 | SET_FLAG / story.neutral.1010.s1.frugal_preparation / true |
| 80 | neutral.1010.s1.result.q1c.adele | 3 | REL_ADD / rel:adele>player / +2 |
| 81 | neutral.1010.s2.result.q1b.antoine | 1 | REL_ADD / rel:antoine>player / +1 |
| 82 | neutral.1010.s2.result.q2a.antoine | 2 | REL_ADD / rel:antoine>player / +2 |
| 83 | neutral.1010.s2.result.q2a.antoine_approval | 3 | SET_FLAG / story.neutral.1010.s2.antoine_approval / true |
| 84 | neutral.1013.s2.result.q1a.adele | 1 | REL_ADD / rel:adele>player / +1 |
| 85 | neutral.1013.s2.result.q1b.orderly_service | 2 | SET_FLAG / story.neutral.1013.s2.orderly_service / true |
| 86 | neutral.1013.s2.result.q1c.warm_recommendation | 3 | SET_FLAG / story.neutral.1013.s2.warm_recommendation / true |
| 87 | neutral.1013.s2.result.q2a.antoine | 4 | REL_ADD / rel:antoine>player / +2 |
| 88 | neutral.1013.s2.result.q2b.antoine | 5 | REL_ADD / rel:antoine>player / +2 |
| 89 | neutral.1008.s1.result.completed | 3 | SET_FLAG / story.neutral.1008.s1.completed / true |
| 90 | neutral.1010.s1.result.completed | 4 | SET_FLAG / story.neutral.1010.s1.completed / true |
| 91 | neutral.1010.s2.result.completed | 4 | SET_FLAG / story.neutral.1010.s2.completed / true |
| 92 | neutral.1013.s2.result.completed | 6 | SET_FLAG / story.neutral.1013.s2.completed / true |

Condition 新增 37 行；63–85 中 key 使用上表 resultId 后接 `.choice` / `.check`：

| 行 | conditionId |
|---:|---|
| 49 | neutral.1008.s1.map |
| 50 | neutral.1008.s1.uncompleted |
| 51 | neutral.1008.s1.action.day |
| 52 | neutral.1008.s1.action.map |
| 53 | neutral.1008.s1.action.uncompleted |
| 54 | neutral.1010.s1.day |
| 55 | neutral.1010.s1.uncompleted |
| 56 | neutral.1010.s1.previous |
| 57 | neutral.1010.s2.day |
| 58 | neutral.1010.s2.uncompleted |
| 59 | neutral.1010.s2.previous |
| 60 | neutral.1013.s2.day |
| 61 | neutral.1013.s2.uncompleted |
| 62 | neutral.1013.s2.previous |
| 63 | neutral.1008.s1.result.q1a.colette.choice |
| 64 | neutral.1008.s1.result.q1b.colette.choice |
| 65 | neutral.1008.s1.result.q1b.colette.check |
| 66 | neutral.1010.s1.result.q1a.adele.choice |
| 67 | neutral.1010.s1.result.q1b.frugal_preparation.choice |
| 68 | neutral.1010.s1.result.q1b.frugal_preparation.check |
| 69 | neutral.1010.s1.result.q1c.adele.choice |
| 70 | neutral.1010.s1.result.q1c.adele.check |
| 71 | neutral.1010.s2.result.q1b.antoine.choice |
| 72 | neutral.1010.s2.result.q1b.antoine.check |
| 73 | neutral.1010.s2.result.q2a.antoine.choice |
| 74 | neutral.1010.s2.result.q2a.antoine.check |
| 75 | neutral.1010.s2.result.q2a.antoine_approval.choice |
| 76 | neutral.1010.s2.result.q2a.antoine_approval.check |
| 77 | neutral.1013.s2.result.q1a.adele.choice |
| 78 | neutral.1013.s2.result.q1b.orderly_service.choice |
| 79 | neutral.1013.s2.result.q1b.orderly_service.check |
| 80 | neutral.1013.s2.result.q1c.warm_recommendation.choice |
| 81 | neutral.1013.s2.result.q1c.warm_recommendation.check |
| 82 | neutral.1013.s2.result.q2a.antoine.choice |
| 83 | neutral.1013.s2.result.q2a.antoine.check |
| 84 | neutral.1013.s2.result.q2b.antoine.choice |
| 85 | neutral.1013.s2.result.q2b.antoine.check |

既有 `neutral.1008.s1.day` 完全复用，没有重复追加。

### Text.xlsx：13 行

| 行 | key |
|---:|---|
| 2446 | bar.colette.trust_player.name |
| 2447 | bar.adele.trust_player.name |
| 2448 | bar.antoine.trust_player.name |
| 2449 | condition.story.rose.1009.s1.completed |
| 2450 | condition.story.neutral.1008.s1.completed |
| 2451 | condition.story.neutral.1010.s1.completed |
| 2452 | condition.story.neutral.1010.s1.frugal_preparation |
| 2453 | condition.story.neutral.1010.s2.completed |
| 2454 | condition.story.neutral.1010.s2.antoine_approval |
| 2455 | condition.story.neutral.1013.s2.completed |
| 2456 | condition.story.neutral.1013.s2.orderly_service |
| 2457 | condition.story.neutral.1013.s2.warm_recommendation |
| 2458 | neutral.1008.s1.action.name |

### GameParam.xlsx / Sheet：3 行

| 行 | key | value |
|---:|---|---|
| 21 | neutral.promotion.preparation_cost_multiplier | 0.8 |
| 22 | neutral.promotion.revenue_multiplier | 1.2 |
| 23 | neutral.reporter.advertisement_fee | 100 |

## 15 条显示文案 before/after 与授权

**以下每一条均由用户逐条明确授权**，目的为保留 10 月 10 日 S2 接 S1、10 月 13 日促销，调整正式显示的时间词；只改对应 Text 的 C 列显示内容，保留相同 key 和原有 metadata。原 DOCX/source-map 不改，继续作为原始证据而非“已修订原稿”。

### A02 / neutral.1010.s1

- P066，Text C1830：
  - before：那就定在两天后，怎么样？
  - after：那就定在三天后，怎么样？
- P067，Text C1831：
  - before：两天后……好！时间应该足够了！
  - after：三天后……好！时间应该足够了！
- P068，Text C1832：
  - before：那今天营业结束后，我们就一起商量一下具体的安排。
  - after：那就先按这个安排准备，今天营业结束后，我们再一起核对一下细节。

### A03 / neutral.1010.s2

- P003，Text C1840：
  - before：清晨的咖啡馆还没有正式营业。
  - after：早间的第一拨客人散去后，咖啡馆迎来了一段短暂的整理时间。
- P004，Text C1841：
  - before：为了准备即将到来的促销活动，我比往常早起了一些。桌椅已经摆放妥当，柜台上堆着几张写了一半的告示，旁边则是昨晚整理好的账簿。
  - after：为了准备即将到来的促销活动，我趁客流稍缓整理了一下店里。桌椅已经摆放妥当，柜台上堆着几张写了一半的告示，旁边则是昨晚整理好的账簿。
- P007，Text C1843：
  - before：早安。请问这里已经开始营业了吗？
  - after：早上好。请问现在方便接待吗？
- P011，Text C1846：
  - before：看来我来得稍早了一些。不过，既然门没有锁上，想必也不至于打扰太久。
  - after：看来我来得不太凑巧。不过，既然门没有锁上，想必也不至于打扰太久。
- P012，Text C1847：
  - before：您好，先生。我们确实还没正式营业。
  - after：您好，先生。我们正在整理店里。
- P025，Text C1855：
  - before：您是第一次来这里吗？虽然还没正式营业，不过咖啡已经准备好了。若是不介意，不妨进来坐坐。
  - after：您是第一次来这里吗？我们正在整理，不过咖啡已经准备好了。若是不介意，不妨进来坐坐。
- P026，Text C1856：
  - before：如此说来，我倒没有拒绝的理由了。能在清晨遇到一位热情的店主，总是件令人愉快的事。
  - after：如此说来，我倒没有拒绝的理由了。能在早间遇到一位热情的店主，总是件令人愉快的事。
- P030，Text C1857：
  - before：您要是愿意的话，也可以先坐一会儿。我们很快就能营业了。
  - after：您要是愿意的话，也可以先坐一会儿。我们很快就能整理好。
- P034，Text C1859：
  - before：抱歉，先生，我们还需要准备一会儿。如果您不介意，可以等正式营业之后再来。
  - after：抱歉，先生，我们还需要整理一会儿。如果您不介意，可以稍后再来。
- P035，Text C1860：
  - before：当然，遵守营业时间也是经营的一部分。既然如此，我便不打扰了。
  - after：当然，先把店里安排妥当也是经营的一部分。既然如此，我便不打扰了。
- P052，Text C1872：
  - before：正是如此。所以我们打算在两天后举办一次促销活动。
  - after：正是如此。所以我们打算在三天后举办一次促销活动。
- P113，Text C1912：
  - before：距离正式营业已经没有多少时间，我收拾好桌上的账簿，将写了一半的促销告示先放到一旁，准备迎接今天的第一批客人。
  - after：短暂的整理告一段落，我收拾好桌上的账簿，将写了一半的促销告示先放到一旁，准备继续招呼今天的客人。

## 本阶段冻结 SHA256 与证据

以下为**本代理交还写权时**的阶段哈希，不代表后续父代理/A05 扩展后的当前哈希。

| 文件 | before SHA256 | after SHA256 |
|---|---|---|
| Narrative.xlsx | `19bdb936f1fb97be9343d1b78e4768ee016670b23ab4a2fbeadf60c4d1d73207` | `4cac35cbc1caf064841308d95058e995853a64a1b37827a69d9d6ec03fcc658b` |
| Text.xlsx | `05f26252d74720f857d6005e742ff4599d93f2cffca36eb5f298d9a2ee274cac` | `cbaec12782aadc1e0a1cffc6ce14fa89fe59347168f155b4efe983ae75612f31` |
| GameParam.xlsx | `e13efdae846da00721a8e283143d864f631bcbcd47a87291972f5ba4daa69469` | `a6bd3bac57db4691ad691eb61cb9e78e9430eb092d832bc73307cb40dd94ec2a` |

证据目录：`Library/Locus/tmp/neutral-content/20261016-162916-197256-A01-A04-formal/`

- `formal-evidence.json`：全部 90 新增行字段值/物理行、36 编辑 before/after、候选/备份/提交 SHA、保存 cell 签名 SHA、源文 SHA。
- `idempotence-evidence.json`：提交后真实二次 apply 的零写入证据。
- `Narrative.before.xlsx` / `Text.before.xlsx` / `GameParam.before.xlsx`：逐份备份，SHA=before。
- `*.candidate.xlsx`：全部回读验证后的候选，SHA=after。

原始证据保持不变：

| 来源 | SHA256 |
|---|---|
| A-neutral/source-map.md | `1447b2784eca6638ccf67a572ba8863d0abd3b6db0e02bb6015bac2457ca108a` |
| MA70.10.08-S1.docx | `5d472f1b8fdc51487bb2171928e4ae2332a6d259024580886954a4f414519d58` |
| MA70.10.10-S1.docx | `e54ba6e06d1213498379775c5f43707818ffe3bfed94bc7f34c671440c940a0f` |
| MA70.10.10-S2.docx | `788e091ab3a06de3cc2ee78d8adc388dbb9a87cd4f9a514bdd760ccd92760120` |
| MA70.10.13-S2.docx | `4cd41271eaaf9c1f3a6892ad96e52729d3bf7553f4271428b4ed7d57cb841caa` |

静态模型 trace SHA256：`5399e8dee358d2071ce8eb2514d88f459edf88aa2a13848240527a3e7d9be4bf`。

复跑方式（后续源表追加不应调用旧 blocked 脚本覆盖正式配置）：

```text
python Tools/Narrative/configure_neutral_formal.py > PLAN.json
python Tools/Narrative/configure_neutral_formal.py --apply --expected-sha256 PLAN.json
```

参数表可调不等于已消费：未来经济/费用调整还需对应消费端契约和同步生成，不能把本报告的占位 GameParam 当作账本已接入。
