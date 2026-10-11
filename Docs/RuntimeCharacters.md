# Luban Object 角色配置

## 权威来源

角色身份、名称、公开简介、标签与资源路径统一配置在 `Assets/ConfigTables/Data/Narrative/Narrative.xlsx` 的 Object sheet，文本在 `Assets/ConfigTables/Data/Shared/Text.xlsx`。根据 `策划文件/策划/文案/人物.xlsx` 登记32条人物；另保留4个独立测试剧情对象 npc_a～npc_d，以及 player/world。不得将测试剧情人物冒充正式人物。

Object 使用策划字段 `portraitPath`、`portraitIndoorPath`、`avtarPath`（保留原拼写）。室内立绘为空时回退 portraitPath。旧 iconPath 保留兼容。路径是 Resources 相对路径，不带扩展名。world 对象无需人物图片。未有美术的人物路径可空；已填路径缺失将明确报错。

运行时 `CharacterCatalog.LoadFromTables()` 从 Luban Object/Text 创建目录；`Assets/Resources/Characters/CharacterCatalog.asset` 只是当前生成结果的检查快照，不是运行时权威源，不要用它维护第二份人物资料。

## 人物与资源

9名正式人物有PSD导出图片：欧仁、蒂博、阿黛尔、埃利昂、萨德、勒苏、安托万、阿莉耶诺尔、贝特朗。原PSD与总预制体不动。Resources/Characters 下按稳定ID分目录。阿莉耶诺尔室内路径使用日常形象；贝特朗默认使用剪影，revealed.png保留为剧情后续资源，不自动提前展示。

简介已避开隐藏身份、秘密结盟、复仇/政变动机。尚无素材的具名人物不会伪造头像。已有头像暂复用人物图，应由美术补充裁切头像。此版没有新增关系数值或擅自设置能力初值。

## 地图与咖啡馆对话统一

地图 NCS Conversation 的链路是：地图行动或 NCS 事件 → `NarrativePresenter` → `NarrativeConversationEventPlayback` → `DialoguePlayback` → `DialoguePanel`。

咖啡馆顾客点击的链路是：`CafeWorld.Talk`（只负责聚焦订单、记录首次交互、返回内容）→ `FreeCafeRuntime` → `CafeDialoguePlayback` → 同一个 `DialoguePlayback` / `DialoguePanel`。咖啡馆领域程序集不直接引用 UI。

两边都按 Object 的角色 ID 查 `NarrativeCharacterResolver`，都使用同一套姓名、头像、立绘和 `emote` 解析。以后将咖啡馆交互接入 NCS 时，只需在 `CafeDialoguePlayback.PlayCustomer` 前增加 `NarrativeRuntime.QueueEvent(eventId)` 或把顾客的 `interactionPresentationKey` 映射到正式 Conversation，不需要改 DialoguePanel。

咖啡店创建人物、世界Sprite、订单头像统一查表。只有 tags 含 cafe_customer 且有世界Sprite的正式人物进入普通顾客选择；员工不当顾客。固定角色 personId 与 Object ID 一致，选择时排除已在场人物；正式人物已全在场时使用表内 guest_a 泛用身份。guest_a实例按到访分配独立personId。

对话、正式开场、F8预览、PresentationDebugScene 三人/特效/阅读预览统一用Object人物。旧测试故事继续使用表内 npc_a～npc_d，不改变既有Bar/Trigger/关系。Details调试场景也读取正式表数据。程序化单元测试仍允许隔离DTO，不是游戏配置源。

```csharp
var left = NarrativeCharacterResolver.CreateDialogueCharacter("eugene");
var right = NarrativeCharacterResolver.CreateDialogueCharacter("adele");
DialoguePlayback.Play(new DialogueScript(new[] {
    new DialogueLine(DialogueSpeaker.Left, "来一杯咖啡吧。"),
    new DialogueLine(DialogueSpeaker.Right, "欢迎。")
}, left, right));
```

NCS ConversationLine 的 emote 已传到对话资源选择，但本次只有基准美术，没有虚构表情差分。happy/angry缺失回退基准立绘。

## 生成与校验

运行 `Assets/ConfigTables/GenerateAll.bat` 使用官方 Luban strict。本机默认安装目录缺失，本次官方5.1.0下载在 `Library/Locus/tmp/character-migration/luban/Luban`，可通过临时 LUBAN_DIR 指向它；Library清理后需安装工具或重新设置有效路径。

保存表 → strict生成 → Unity编译 → PlayMode。JSON/generated C#不手改。旧存档characterId为空保留原外观回退，读档不重新选人。咖啡店领域层仍不依赖cfg生成类型。
