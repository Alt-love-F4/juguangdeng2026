# NCS 指定开场内容接入

## 修改数据的位置

| 内容 | 配置文件 / Sheet | 指定值 |
|---|---|---|
| 演出标题、角色名、视觉资源 | `Assets/ConfigTables/Data/Narrative/Narrative.xlsx` / OpeningPresentation | 默认 presentationId = `first_day_cafe`；titleKey / leftNameKey / rightNameKey / visualsResource |
| 台词、说话方、动作、自动推进 | 同工作簿 / OpeningLine | 按 presentationId 筛选、seq 排序；textKey / speaker / gesture / autoAdvanceSeconds |
| 实际标题、姓名、台词文本 | `Assets/ConfigTables/Data/Shared/Text.xlsx` / Text | Opening 表引用的文本 key |
| 第一封信正文 | 同 Text 表 | `opening.letter.body` |
| 第一封信署名 | 同 Text 表 | `opening.letter.signature` |

这次只新增两个共享文本条目，保留原信件文案，不新增 Letter 表或改变 NCS schema。
信件面板没有独立标题/日期字段，因此本次只接正文、署名，不额外改 UI 布局。

## 读取优先级

1. 显式注入 `IDialogueScriptSource` / NarrativeConfiguration 优先，不使用另一会话覆盖测试数据。
2. 有已初始化 NarrativeRuntime：读取它的 Configuration，文本经过现有 ResolveText（注入 resolver 优先）。
3. 没有 Runtime：直接使用 LubanNarrativeLoader.Load，不创建 Runtime / HUD，不启用 NCS gameplay 模块。
4. 信件缺任一指定值或加载失败：记录 Warning，回退完整面板草稿，不显示 key，也不混用新正文和旧署名。
5. 对话缺指定演出、必需文本、资源或非法动作：返回清楚的失败信息；正式流程保持既有重试逻辑，调试场景显示失败原因。

## 业务接口

```csharp
// 播放任意已有 OpeningPresentation；正常结束/取消沿用既有 UIKit 生命周期。
DialoguePlayback.PlayNcsOpening("first_day_cafe", onFinished, onCancelled);

// 明确注入一个配置用于测试（不会被当前 runtime 覆盖）。
var source = new LubanOpeningDialogueSource("first_day_cafe", configuration);
DialoguePlayback.Play(source, onFinished, onCancelled);

// 面板只接收已解析的快照。
letterPanel.Play(new FirstDayLetterContent(body, signature), onFinished);
```

DayFlowController 的 Inspector 支持 `openingPresentationId`、`firstDayLetterBodyKey`、`firstDayLetterSignatureKey`。
新游戏和 IntroLetter 恢复使用同一信件配置入口。对话完成、取消、跳过和经营流程不变。
Panel 本身不访问 NCS singleton；适配器位于 `Assets/Scripts/GameFlow/UI/Presentation/NarrativePresentationDataAdapter.cs`。
此接入只提供内容，不 QueueEvent，不结算 Result，不替换现有 NCS 事件/选项或原子特效调度。

## 共享调试场景

继续用 `Assets/Scenes/PresentationDebugScene.unity`：

- W：NCS 指定信件；Shift+W：原面板草稿。
- E：NCS 指定对话；Shift+E：原七句硬编码长文本测试。
- F：NCS 首日引入对话。
- G / 1~0：原独立特效预览保持不变。
- Inspector 的 NCS content selection 可修改开场 ID / 信件文本 Key。

不存在正式 NCS Runtime 也可测试；不会推进剧情状态和存档。不要创建新的 UI 调试场景。

## 配表生成与验证

游戏实际读取 Resources/Luban JSON，而不是 Excel。修改 Excel 后应运行现有 GenerateAll.bat / Spotlight > Narrative > Generate Luban。
本次环境的旧默认 `D:/Luban/src/Luban/bin/Release/net8.0/Luban.exe` 不存在，已使用项目现有 `generate_tables.py` 的 read_table 逻辑从 Text.xlsx 定向生成 tbtext.json，并核对旧条目不变、两个新 key 唯一。
未声称运行过完整 Luban strict schema 生成；这次未修改任何生成 C# 类型或表结构。

轻量检查：`NcsOpeningDataTests`（指定 ID、排序、动作、resolver、显式配置覆盖、缺值回退）与 `NcsOpeningPresentationSmokeTests`（共享场景信件/对话/草稿入口）。

本次轻量结果：数据/面板 PlayMode 6/6 通过（共享调试场景），UI 与测试程序集编译成功、0 Error。未跑完整回归；未测试 NCS 事件结算/选项分支。
