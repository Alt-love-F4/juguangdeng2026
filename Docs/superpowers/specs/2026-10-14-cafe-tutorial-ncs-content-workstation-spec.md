# 咖啡馆新手教程：NCS 内容配置与操作台入口 Spec

日期：2026-10-14。本文是本次施工的唯一执行依据。

## 1. 本次目标

将 `Assets/文案-新手教程.docx` 中的咖啡馆新手教程内容整理进现有 NCS 配置，并把新玩家进入咖啡馆后的流程做到“开场介绍结束，玩家点击操作台并成功打开”为止。

操作台打开之后的咖啡制作玩法、蒙版和逐步操作引导交给另一位玩法负责人继续施工。

## 2. 施工范围

### 2.1 NCS 内容必须全部准备好

按原稿整理并配置以下内容。台词、旁白、剧情顺序和分支尽量保留原稿；发现错字、重复、缺失或与玩法事实冲突时，记录问题，不要静默改写。

- 阿黛尔介绍咖啡馆、大堂、操作间、二楼和店铺现状。
- 老太太进店、与阿黛尔和玩家的对话。
- 知觉 `>= 3` 和知觉 `< 3` 两条分支。
- 知觉分支之后的汇合内容。
- 顾客 B 的订单和第一杯咖啡说明。
- 阿黛尔后续制作步骤的讲解台词。
- 研磨、烧水、冲泡、装杯、基底、浓度、甜度等可选演示文本。
- 出餐后的五档评价文本：独一无二、滴滴香浓、中庸之道、食不知味、好想退钱。
- 对应的顾客反馈、奖励/重做相关文案和结尾剧情。
- 顾客离开、阿黛尔收尾、玩家独白和黑场前文本。

后续文本本次必须配好，但不要求在本次施工中全部播放或接入实际制作流程。

### 2.2 替换现有开场

当前 `first_day_cafe` 是已有的 NCS 线性开场，内容来自 `OpeningPresentation`、`OpeningLine` 和 `Text` 配置，不是 C# 台词硬编码。

本次要用原稿内容替换/扩展当前开场，避免原有临时测试台词和原稿重复。继续使用现有 NCS 到对话演出的链路：

```text
OpeningPresentation / OpeningLine / Text
    -> LubanOpeningDialogueSource
    -> DialogueScript
    -> DialoguePlayback / DialoguePanel
```

修改 Excel 或其他源配置后，重新生成 Luban JSON。不要直接编辑生成后的 JSON。

### 2.3 玩法只做到操作台入口

本次可运行流程必须是：

```text
进入咖啡馆
  -> 播放原稿对应的开场介绍
  -> 开场结束
  -> 进入首次咖啡馆教程限制状态
  -> 只允许点击操作台
  -> 玩家点击操作台
  -> 操作台成功打开
  -> 发出操作台打开完成信号
  -> 将控制权交给后续玩法教程
```

开场结束到操作台打开之间：

- 只能点击操作台。
- 顾客、原料、设备、吧台其他区域、其他楼层和无关物体不能推进玩法。
- 不能直接进入自由制作。
- 不要求本次实现完整蒙版；可以提供焦点目标 ID 或接口。

## 3. 明确不做的内容

本次不要实现：

- 研磨、烧水、冲泡、装杯、调浓度、调甜度和出餐的实际玩法。
- 制作步骤的真实完成判定。
- 咖啡杯、原料、设备、订单状态的 NCS 逻辑。
- 完整蒙版、聚焦动画和操作提示动画。
- 评分、金币、奖励、重做、烟花、音效和差评表现。
- 操作台打开之后的逐步解锁流程。
- 首日信件、日期动画、地图首日流程和其他主循环改动。

之前的首日开局 Spec 不属于本次施工范围。除非本 Spec 明确要求，不要修改信件、日期、地图和 DayFlow 的其他行为。

## 4. NCS 配置组织

继续使用现有 NCS 内容表和运行时读取方式：

- `OpeningPresentation`：一段对话的 ID、标题、角色名、视觉资源。
- `OpeningLine`：台词顺序、说话人、文本 key、动作和自动推进。
- `Text`：具体中文文本。
- 现有 `LubanOpeningDialogueSource`、`NarrativePresentationDataAdapter`、`DialoguePlayback` 和 `DialoguePanel`：负责读取和播放。

首段开场可以继续使用 `first_day_cafe`，也可以拆出新的稳定 ID；如果拆分，必须保证最终只有一个正式入口，不同时播放两份重复开场。

后续内容使用稳定的语义 ID，不使用场景物体名称或显示顺序作为协议。推荐教程 ID：

```text
first_cafe_tutorial
```

推荐内容/步骤 ID：

```text
customer_arrives
perception_success
perception_failure
order_confirmed
workstation_open
make_coffee_base
pour_into_cup
adjust_strength
adjust_sweetness
serve_customer
result_excellent
result_good
result_average
result_bad
result_refund
ending
```

最终采用的 ID 可以调整，但必须在交付记录中列出实际 ID 和对应 NCS 段落。

知觉分支沿用现有玩家属性和 NCS 条件系统。不要在咖啡玩法侧复制一份知觉判定。

## 5. 给后续玩法 Agent 的接口

本次必须把后续玩法能够使用的内容读取和步骤通知边界写清楚。实际类名可适配当前工程，但职责必须保持。

推荐提供：

```csharp
bool TryGetCafeTutorialStep(
    string tutorialId,
    string stepId,
    out CafeTutorialStepDefinition definition);

void NotifyTutorialStepCompleted(
    string tutorialId,
    string stepId);
```

步骤定义至少应能表达：

- `tutorialId`
- `stepId`
- 对应的 NCS 对话段落 ID
- 对应的 `focusTargetKey`
- 玩法完成信号 ID
- 下一步骤 ID或后续内容 ID
- 是否为可选演示

本次真正启用的完成信号只有操作台打开：

```text
tutorialId = first_cafe_tutorial
stepId = workstation_opened
```

后续玩法 Agent 的预期使用方式：

```text
读取当前步骤定义
  -> 请求播放对应 NCS 对话
  -> 根据 focusTargetKey 显示蒙版/高亮
  -> 开放对应玩法操作
  -> 玩法根据真实领域状态判定完成
  -> 回报 tutorialId + stepId
  -> 请求下一段 NCS 内容
```

注意：

- 玩法 Agent 不需要复制 NCS 台词到 C#。
- 蒙版不能自行宣布玩法完成。
- NCS 不判断杯子、设备、订单或配方状态。
- 重复、过期、非当前步骤的完成信号必须被忽略，不能推进教程。
- 本次只要求操作台打开信号能工作；后续步骤接口可以先完成定义和读取，不得假装后续玩法已经完成。

## 6. 验收标准

本次施工完成必须满足：

1. 从正式新游戏流程进入咖啡馆后，能看到原稿对应的开场内容。
2. 阿黛尔、玩家、旁白等角色显示正确，台词来自 NCS 配置。
3. 后续教程文案已经配置，可以通过稳定 ID 读取。
4. 知觉成功/失败内容已经配置，并使用现有 NCS 条件和属性来源。
5. 开场介绍结束后，玩家只能点击操作台。
6. 点击操作台后，操作台能正常打开。
7. 操作台打开后，能发出稳定的 `workstation_opened` 完成信号。
8. 本次没有启动完整咖啡制作流程。
9. 本次没有实现完整蒙版和逐步操作引导。
10. 不影响首日信件、日期动画、地图和其他未授权流程。

## 7. 工程 Agent 交付记录

完成后必须记录：

- 修改了哪些源表、配置、代码和测试文件。
- 当前正式使用的教程 ID、开场 ID、步骤 ID 和文本 key。
- 原稿哪些内容已经配置，哪些内容仍未配置。
- 操作台限制在哪个类中实现。
- 操作台打开完成信号由哪个类发出，后续 Agent 如何监听。
- 后续玩法 Agent 如何读取 NCS 教程内容。
- 执行过的测试和结果。
- 未完成事项、已知问题和后续接手人需要注意的内容。

交付记录不能只写“完成”，必须让另一个 Agent 能根据文档直接接入。
