# 手动角色配置

本阶段不修改 NCS schema、Excel 或生成代码。角色定义暂由 `Assets/Resources/Characters/CharacterCatalog.asset` 提供。

## 使用

在目录的 Characters 列表填写唯一 characterId、displayName、avatar、portrait、worldSprite。默认示例为 eugene（欧仁）、adele（阿黛尔）、thibaut（蒂博·卡隆）。头像暂复用完整人物图，后续可替换为美术裁切的小头像。

咖啡店 FreeCafeRuntime 的 characterCatalog 为空时自动读取默认目录。显式 customerIdentitySource 优先；目录为空时保持匿名顾客。目录角色按座位轮换外观模板，每次到访有独立 personId。这里是展示/模板配置，不代表固定剧情人物的到店调度；尚未接入 NCS 事件、关系或到店规则。

对话可直接调用：

```csharp
var left = NarrativeCharacterResolver.CreateDialogueCharacter("eugene");
var right = NarrativeCharacterResolver.CreateDialogueCharacter("adele", "happy");
DialoguePlayback.Play(new DialogueScript(new[] {
    new DialogueLine(DialogueSpeaker.Left, "来一杯咖啡吧。"),
    new DialogueLine(DialogueSpeaker.Right, "欢迎。")
}, left, right));
```

也可给任意运行时对象挂 CharacterDialoguePreview，填写角色 ID，Play Mode 下使用组件菜单 Play Character Dialogue（或绑定按钮 Play 方法）。播放复用现有 DialoguePanel，不需要 NCS 表。

## 边界

- characterId 是配置 ID，personId 是人物实例 ID，customer_N 是复用的座位实体 ID。
- 角色目录只保存静态显示数据，不保存订单、好感度或剧情进度。
- 世界人物继续复用 FreeEntity_customer.prefab，仅替换世界 Sprite。此版不支持专属人物 Prefab 或员工/剧情人物生成。
- 原 PSD 与导入集合 Prefab 保留不动。资源放到各自文件夹；资源引用是 Unity Sprite，运行时无需 Editor API 或文件系统路径。
- 后续 NCS 接入应通过适配器把表行转换为相同的 CharacterProfile，再调用 CharacterCatalog.Configure；不让 CafeWorld 依赖配表生成类型。
- 此版 happy/angry 可通过直接对话入口选立绘，未改变 NCS ConversationLine 的表情传递链路。
- 旧存档 characterId 为空时继续使用原 CafeArtLibrary，读档不重新抽取身份。
