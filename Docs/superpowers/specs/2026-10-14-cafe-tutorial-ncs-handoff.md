# 咖啡馆教程 NCS 配置交接说明

日期：2026-10-14。用途：交给咖啡模拟玩法负责人及其工程 agent 使用。本文说明如何读取本轮准备好的 NCS 教程内容，以及如何接入后续蒙版和操作步骤。

## 1. 本轮交付到哪里

本轮 NCS 工程只保证：

```text
进入咖啡馆
-> 播放文案对应的开场介绍
-> 限制其他交互，只允许点击操作台
-> 玩家点击操作台
-> 操作台成功打开
-> 发出“操作台已打开”信号
```

后面的研磨、烧水、冲泡、装杯、浓度、甜度、出餐、评分、奖励、蒙版和逐步输入限制由玩法 agent 接手。

## 2. 内容从哪里读

优先使用项目已有的 NCS 内容链路：

- `OpeningPresentation`：定义一段对话的 ID、标题、角色名和视觉资源。
- `OpeningLine`：定义该段对话的顺序、说话人、文本 key、动作和自动推进参数。
- `Text`：保存所有实际显示的中文文本。
- 生成后的 `Assets/Settings/Resources/Luban/tbopeningpresentation.json`、`tbopeningline.json`、`tbtext.json`：运行时读取结果，不要直接手改这些生成文件。
- `LubanOpeningDialogueSource`：将 NCS 配置转换为 `DialogueScript`。
- `DialoguePlayback` / `DialoguePanel`：负责播放演出。

修改内容时，应改 Excel 源表，再运行项目既有的 Luban 生成流程。玩法 agent 不应把台词复制到自己的 C# 字符串里。

## 3. 内容 ID 的使用规则

当前首段入口使用：

```text
tutorialId = first_cafe_tutorial
openingPresentationId = first_cafe_tutorial_opening
```

如果实施时决定继续复用 `first_day_cafe` 作为首段 ID，必须在结果记录中明确说明；不得同时保留两套会播放相同内容的入口。

后续内容按稳定语义 ID 组织，推荐：

```text
first_cafe_tutorial.customer_arrives
first_cafe_tutorial.perception_success
first_cafe_tutorial.perception_failure
first_cafe_tutorial.order_confirmed
first_cafe_tutorial.make_coffee_base
first_cafe_tutorial.pour_into_cup
first_cafe_tutorial.adjust_strength
first_cafe_tutorial.adjust_sweetness
first_cafe_tutorial.serve_customer
first_cafe_tutorial.result_excellent
first_cafe_tutorial.result_good
first_cafe_tutorial.result_average
first_cafe_tutorial.result_bad
first_cafe_tutorial.result_refund
first_cafe_tutorial.ending
```

以上是推荐命名，不要求玩法 agent 按显示顺序猜测步骤。最终实际 ID 必须以 NCS 配置和交付记录为准。

## 4. 玩法 agent 如何接入

玩法侧只依赖稳定的教程 ID 和步骤完成信号，不依赖 NCS Excel 的内部结构。推荐流程：

```text
读取教程步骤定义
-> 请求播放该步骤对应的 NCS 段落
-> 等待 DialoguePanel 播放完成
-> 打开对应蒙版和玩法输入
-> 玩家完成真实操作
-> 玩法侧确认领域状态完成
-> 回报 tutorialId + stepId
-> 请求下一段 NCS 内容
```

建议提供或适配成类似下面的接口，实际类名以工程现有架构为准：

```csharp
bool TryGetCafeTutorialStep(
    string tutorialId,
    string stepId,
    out CafeTutorialStepDefinition definition);

void NotifyTutorialStepCompleted(
    string tutorialId,
    string stepId);
```

第一阶段至少需要一个完成信号：

```text
tutorialId = first_cafe_tutorial
stepId = workstation_opened
```

不能用“点击了某个 GameObject 名称”作为跨系统协议，也不能由蒙版自己宣布操作完成。完成信号必须来自玩法实际状态。

## 5. 本轮交接验收

玩法 agent 接手时应能确认：

- 能读取后续教程文本和段落 ID。
- 能让玩家在首日咖啡馆中只点击操作台。
- 操作台打开后能收到 `workstation_opened` 对应的完成信号。
- 能根据稳定 step ID 绑定未来蒙版目标。
- 不需要复制阿黛尔台词到玩法代码。
- 不需要修改 NCS 的属性、条件、文本解析和对话播放基础设施。

## 6. 交接时必须记录

玩法 agent 完成后，应补充：

- 实际使用的 `tutorialId`、`stepId` 和 NCS 段落 ID。
- 每个步骤对应的蒙版目标键。
- 玩法完成信号由哪个类发出。
- 操作台打开、制作步骤完成和出餐完成分别如何判定。
- 失败、取消、读档和重复信号如何处理。
- 哪些内容仍未接入，哪些文案因玩法原因发生改动。
