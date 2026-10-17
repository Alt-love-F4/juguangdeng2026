# Luban Object 角色配置

## 权威来源

角色身份、名称、公开简介、标签与资源路径统一配置在 `Assets/ConfigTables/Data/Narrative/Narrative.xlsx` 的 Object sheet，文本在 `Assets/ConfigTables/Data/Shared/Text.xlsx`。根据 `策划文件/策划/文案/人物.xlsx` 登记32条正式人物；另保留4个独立测试剧情对象 npc_a～npc_d、player/world，以及既有局部剧情对象。当前共47个Object，其中world/cafe不生成CharacterProfile，运行时目录为45项。不得将测试剧情人物冒充正式人物。

Object 使用策划字段 `portraitPath`、`portraitIndoorPath`、`avtarPath`（保留原拼写）。室内立绘为空时回退 portraitPath。旧 iconPath 保留兼容。路径是 Resources 相对路径，不带扩展名。world 对象无需人物图片。未有美术的人物路径可空；已填路径缺失将明确报错。

运行时 `CharacterCatalog.LoadFromTables()` 从 Luban Object/Text 创建目录；`Assets/Resources/Characters/CharacterCatalog.asset` 只是当前生成结果的检查快照，不是运行时权威源，不要用它维护第二份人物资料。

## 人物与资源

两批PSD导出保留在 `Assets/Art/Gameplay/人物-常规色` 和 `Assets/Art/Gameplay/人物2-常规色 (2)`；原PSD、总Prefab、原始PNG和GUID均不改。20名正式人物已有真实立绘，另外12名依用户确认生成明确标有“立绘待补/ART TBD”的占位图。32名各自拥有三类独立资源：

| NCS Object字段 | Resources路径 | 使用位置 |
|---|---|---|
| portraitPath | Characters/<id>/neutral | 对话/阅读基础立绘 |
| avtarPath（保留原拼写）、iconPath | Characters/<id>/avatar | 512×512裁切头像，订单卡/人物资料 |
| portraitIndoorPath | Characters/<id>/seated_placeholder | 768×1024咖啡馆坐姿示意占位，标有“坐姿占位 · ART TBD”、角色名和ID |

坐姿不是最终美术：头部使用角色头像，身体为统一椅子/坐姿示意。替换真实坐姿时可覆盖该PNG内容并保留.meta，或更改Object路径后重新生成。不得把neutral直接当最终坐姿。

阿莉耶诺尔的 `indoor.png` 保留为无圣痕日常服的**对话变体**，不再占用portraitIndoorPath（该字段现专门供咖啡馆坐姿使用）；默认neutral仍为公主服。贝特朗neutral仍为成人剪影，revealed.png保留为剧情揭示资源，不自动提前展示。玛丽沿用marie ID与公开名雅克·马尚；emmanuel与已有剧情sorel未擅自合并。

公开简介继续使用既有Text，不公开隐藏身份、秘密结盟或动机；未新增关系/能力数值。32名完整来源映射、占位状态和裁切参数见 `Tools/Characters/character-art-manifest.json`、`Tools/Characters/build_character_art.py`。生成器只处理PNG，不改Unity YAML；导入设置通过Unity TextureImporter设为Single Sprite。运行 `python Tools/Characters/build_character_art.py` 可从原始PNG重复生成（依赖Pillow/openpyxl），随后须经Unity导入。

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

运行 `Assets/ConfigTables/GenerateAll.bat` 使用官方 Luban strict。本机现有LUBAN_DIR无效时可临时指向 `Library/Locus/tmp/tables-luban-5.1.0` 中官方5.1.0工具；这是缓存路径，Library清理后需重新安装/指定有效工具，不依赖全局环境改动。

保存表 → strict生成 → Unity编译 → PlayMode。JSON/generated C#不手改。旧存档characterId为空保留原外观回退，读档不重新选人。咖啡店领域层仍不依赖cfg生成类型。

## 2026-10-17 验证

- 官方GenerateAll退出0：Player语义校验及Luban strict通过；仅Object源表与对应JSON发生内容变更，其余27个Narrative sheet及Text不变。
- 96张主PNG与.meta完整；20张真实neutral与指定PSD导出源PNG字节一致；12张基础立绘/头像为占位，32张坐姿均为占位。Sprite导入与运行时32组三资源检查无缺失。
- RuntimeCharacterTests：5/5 EditMode通过；CharacterOrderAvatarTests：3/3 PlayMode通过（分页、制作台、空订单、缺图、重绑、多行布局）。兼容fallback测试仅在存在相应非正式NPC行时检查，不声称当前表包含此类行。
- PresentationDebugScene实机确认adele/colette/renaud三槽立绘显示；CoffeeShop实机确认elion/sade/lesou使用独立seated_placeholder，左上公共订单卡显示角色avatar，进入Counter保持正确头像。公共订单头像由FreeCafeHud绑定时建立并缓存，Refresh按可见订单更新。
- Scene资产与座位布局未修改；当前大厅顾客视觉仍偏小且沿用原有座位位置，本轮仅验证资源接入，不表示桌椅坐标与最终坐姿比例已完成美术验收。Console error=0；结束已恢复启动场景Edit Mode。

