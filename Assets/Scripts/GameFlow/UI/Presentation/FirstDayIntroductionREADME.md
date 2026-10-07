# 第一天阿黛尔引入（当前硬编码）

流程：新游戏 → 信件署名确认 → 第一天日期 → 营业准备 → 咖啡馆入场 → 阿黛尔引入对话 → 开始经营。

## 当前修改台词的位置

- 正式第一天台词：`FirstDayIntroductionDialogue.cs`（13 段）。
- F8 演示台词：`DialoguePreviewDialogue.cs`（7 段，与正式剧情分开）。
- 正文、人物姓名、标题和动作都在 C# 中定义。没有 JSON 对话表，不从 ScriptableObject 读取台词。
- `FirstDayIntroductionVisuals.asset` / `DialoguePreview.asset` 只存立绘、背景引用，没有文本字段。阿黛尔暂用已有女店主设计图。
- 控制器已经移除 JSON 表、剧本 ID 和剧本资源配置字段。

复用 `DialoguePanel` / `DialoguePlayback`，只替换剧本来源，不改变逐字显示、分页、输入和生命周期。
完成前不启动经营；取消回到准备并可重试；每次新游戏第一天一次；完成状态随存档记录；对话阶段读档从第一句开始。
第二天、已在经营/后续阶段的旧存档、缺少日程数据的旧存档均不补播。

## 后续再接 Luban

只保留 `IDialogueScriptSource.CreateScript()` 边界。当前没有新增 Luban 表结构、生成代码或运行时表依赖。
未来正式决定 schema 后，实现 Luban 数据源，将表行转换成 `DialogueLine` / `DialogueScript`，在加载完成后注入：

```csharp
flow.SetFirstDayIntroductionSource(myLubanSource);
```

不用修改对话面板或日程流程；传入 null 可恢复默认硬编码剧本。
这里的 JSON 存档序列化是既有存档机制，与已移除的 JSON 台词表无关。
