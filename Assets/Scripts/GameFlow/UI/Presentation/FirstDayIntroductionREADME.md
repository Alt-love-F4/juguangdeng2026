# 第一天阿黛尔引入（Luban 配置）

流程：新游戏 → 信件署名确认 → 第一天日期 → 营业准备 → 咖啡馆入场 → 阿黛尔引入对话 → 开始经营。

## 修改配表

- `Assets/ConfigTables/Data/Narrative/Narrative.xlsx` 的 `OpeningPresentation` 表配置演出 ID、标题/角色名文本 key 和视觉资产路径。
- 同一工作簿的 `OpeningLine` 表按 `seq` 排列台词行，指定说话方、文本 key 和动作。
- `Assets/ConfigTables/Data/Shared/Text.xlsx` 的 `Text` 表保存标题、角色名和全部台词正文。
- `Dialogue/FirstDayIntroductionVisuals` 继续提供左右立绘和背景；表内 `visualsResource` 可改用其他 Resources 下的 `DialogueVisualsAsset`。
- 修改后运行 `Assets/ConfigTables/GenerateAll.bat` 或 Unity 菜单 `Spotlight/Narrative/Generate Luban`。菜单会导入生成 JSON 并执行配置校验。

运行时 `LubanOpeningDialogueSource` 将这些行转换为 `DialogueScript` / `DialogueLine`。主循环仍负责流程时机和完成/取消/存档状态；它不依赖 NCS 事件触发器，也不改变现有 `DialoguePanel` 输入、逐字显示和生命周期。

完成前不启动经营；取消回到准备并可重试；每次新游戏第一天一次；完成状态随存档记录；对话阶段读档从第一句开始。第二天、已在经营/后续阶段的旧存档、缺少日程数据的旧存档均不补播。

`IDialogueScriptSource` 保留为测试替身和可注入扩展点。传入 null 会使用默认 Luban 开场，不再回退到硬编码教程。存档中的 JSON 序列化与台词配表无关。

## 当前会话内容与信件

正式对话默认 ID 为 `first_day_cafe`。DayFlow 的 `openingPresentationId` 和调试场景同名字段可指定其他 OpeningPresentation。
有已初始化 `NarrativeRuntime` 时读取 `Configuration` 并使用 `ResolveText`（保留注入 resolver 优先）；没有运行时也能直接读取 Luban 配置，不启用剧情模块。
开头信件读取共享 Text 表的 `opening.letter.body` / `opening.letter.signature`，通过 `FirstDayLetterContent` 注入既有面板；正文/署名 key 可在 DayFlow Inspector 修改。
新游戏和 IntroLetter 存档恢复都走相同接口，动画和完成/取消流程不变。详见 `Docs/NcsOpeningPresentation.md`。
