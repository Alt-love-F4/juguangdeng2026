# 咖啡店数据表

策划来源是 Data 下的 Excel，运行时读取 Assets/Settings/Resources/Luban 下的生成JSON。
表头沿用 Luban 约定：第1行为 ##var，第2行为 ##type，第3行为 ##group，第4行为 ##，第5行开始数据；不要更改前4行。

- Text：菜单与配方文本。
- Ingredient：库存、补货、解锁。
- Device：加工耗时和升级。
- GameParam：营业时长、初始资金、客流等。
- Recipe：配方元数据；steps 已清空，不再是现有配方的权威步骤来源。
- RecipeStep：有序、结构化的制作步骤；设备时间来自 Device，而不是重复嵌入步骤字符串。
- WorldObject：物品、来源、位置、设备、NPC的角色定义和初始表现。
- Npc：稳定身份、外观、初始台词。

先检查：python Assets/ConfigTables/generate_tables.py --validate
生成：python Assets/ConfigTables/generate_tables.py
项目使用等效Luban生成器，schema.xml同步描述表结构；不要手改生成JSON。
新增物品/实体或调整布局后，在CoffeeShop场景运行 Tools/Spotlight/Build Cafe Workspace。
只调价格/时间/库存时重新生成表即可，无需重装场景。

完整说明见 Docs/CafeArchitecture.md。

# Luban 配表管线

全部叙事配置使用 **Excel → Luban generated C# + JSON**，不使用 ScriptableObject 作为配置来源。

## 多人协作（7人团队）

**新成员请先阅读 [多人协作指南](COLLABORATION_GUIDE.md)**，了解文件分工、命名规范、冲突处理。

### 文件结构（多人模式）

```
Data/
├── Narrative_Core.xlsx          ← 主策划：Object / Bar / Template / SetupParams
├── Narrative_Story_*.xlsx       ← 各剧情策划：Trigger / Event / Result / Condition / Choice
├── Narrative_World.xlsx         ← 世界事件策划：日期事件、全局播报
├── Text_UI.xlsx                 ← UI 文本
├── Text_Story_*.xlsx            ← 各剧情线文本
└── Text_World.xlsx              ← 世界事件文本
```

### 生成流程（已自动化）

```bat
:: 一键完成：合并分散 Excel → 校验冲突 → Luban 生成
Assets/ConfigTables/GenerateAll.bat
```

或手动分步：
```bat
:: 步骤 1: 合并（会检查主键冲突）
python Assets/ConfigTables/merge_narrative.py

:: 步骤 2: Luban 生成
Assets/ConfigTables/GenerateAll.bat
```

> 合并时会自动备份旧的 `Narrative.xlsx` 和 `Text.xlsx` 到 `*_backup_MMDD_HHMM.xlsx`

---

## 单人/兼容模式

如果仍使用单文件模式，保留以下结构：
- `Assets/ConfigTables/Data/Narrative.xlsx`：Object、Bar、Trigger、Event、EventDate、Result、Condition、Template、SetupParams、Choice 共 10 个工作表。
- `Assets/ConfigTables/Data/Text.xlsx`：唯一文本源，包含 UI 与叙事文本。

**注意**：单人模式与多人模式不要混用。`GenerateAll.bat` 会检测 `Narrative_*.xlsx` 是否存在，存在则优先走多人合并流程。

策划从 [策划使用手册](策划使用手册.md) 开始：包含各表字段、茶话完整样例、新增事件步骤、条件/结果/演出配置与排错。

- `Assets/ConfigTables/Data/Narrative.xlsx`：Object、Bar、Trigger、Event、EventDate、Result、Condition、Template、SetupParams、Choice 共 10 个工作表。
- `Assets/ConfigTables/Data/Text.xlsx`：唯一文本源，包含 UI 与叙事文本；原 Narrative 的 Text 内容已合并，原 UI 文本完整保留。
- `Assets/ConfigTables/Defines/schema.xml`：唯一 schema。`luban.conf` 的 client target 导出 `cfg.Tables`。
- `Assets/游戏叙事控制系统.xlsx`：原设计文档，保留不改，不是运行时数据输入。

## 策划工作流

1. 在 Excel 中编辑第 5 行及之后的数据，A 列留空。文本表 B/C/D 列为 `key / text / comment`。
2. 前四行为 Luban 标准表头：`##var` 字段名、`##type` 类型、`##group` 客户端分组 `c`、`##` 字段说明。不要覆盖表头。
3. 保存并关闭 Excel，运行 `Assets/ConfigTables/GenerateAll.bat`，或在 Unity 选择 `Spotlight/Narrative/Generate Luban`。
4. 仅退出码 0 表示生成成功。返回 Unity 等待脚本编译与资源导入后测试。游戏不直接读取 Excel；Excel 修改必须重新生成。

```bat
Assets\ConfigTables\GenerateAll.bat
```

等价命令（在 `Assets/ConfigTables` 中执行；输出目录必须为绝对路径）：

```bat
D:\Luban\src\Luban\bin\Release\net8.0\Luban.exe --conf luban.conf -t client -c cs-newtonsoft-json -d json --strict -x outputCodeDir="<项目绝对路径>\Assets\Scripts\Config\Generated" -x outputDataDir="<项目绝对路径>\Assets\Settings\Resources\Luban"
```

工具路径可用 `LUBAN_DIR` 环境变量覆盖，指向含 Luban.exe 的目录。工具本体不复制到 Unity 包体。

## 表与生成 API

生成代码在 `Assets/Scripts/Config/Generated/`，程序集 `Spotlight.Config` 引用 `Unity.Newtonsoft.Json`。`cfg.Tables` 构造参数为 `Func<string, Newtonsoft.Json.Linq.JArray>`；所有表都有 `DataList`。Excel 原 camelCase 字段生成 PascalCase readonly 字段，JSON 仍保留原字段名。不要手改 generated C# 或 JSON。

| Excel sheet | 生成表 / 行类型 | 模式 / 主键 | Resources JSON |
| --- | --- | --- | --- |
| Text.xlsx/Text | TbText / TextEntry | map / key | Luban/tbtext |
| Object | TbNcsObject / NcsObjectEntry | map / objId | Luban/tbncsobject |
| Bar | TbNcsBar / NcsBarEntry | list / ownerId+barId 复合索引 | Luban/tbncsbar |
| Trigger | TbNcsTrigger / NcsTriggerEntry | map / triggerId | Luban/tbncstrigger |
| Event | TbNcsEvent / NcsEventEntry | map / eventId | Luban/tbncsevent |
| EventDate | TbNcsEventDate / NcsEventDateEntry | list / 无 ID，按行顺序 | Luban/tbncseventdate |
| Result | TbNcsResult / NcsResultEntry | map / resultId | Luban/tbncsresult |
| Condition | TbNcsCondition / NcsConditionEntry | map / conditionId | Luban/tbncscondition |
| Template | TbNcsTemplate / NcsTemplateEntry | map / tplName | Luban/tbncstemplate |
| SetupParams | TbNcsSetupParams / NcsSetupParamsEntry | map / setupKey | Luban/tbncssetupparams |
| Choice | TbNcsChoice / NcsChoiceEntry | map / choiceId | Luban/tbncschoice |

JSON 文件位于 `Assets/Settings/Resources/Luban/`，上表 Resources 路径不带 `.json`。BarId 只需在同 ownerId 下唯一，不同对象允许相同 BarId；运行时 adapter 拼接 `ownerId.barId`。Bar 最后一列 `autoStep` 为 int，现有行迁移值为 1。

## 字段约定与校验

- key 与所有主键区分大小写。文本 `text`、`comment` 可为空；保留中文、换行和引号，Luban 负责 JSON 转义。
- `contentKey`、`derivedSources`、`derivedWeights` 均为 string，多项用 `|` 分隔；adapter 解析。不要将它们改为 Luban array。`atomSeq`、`slotDef`、`slotValues` 保留原项目语法。
- 枚举列保留 Excel 原字面量，由 schema 的 Luban `set` validator 在同一 strict 生成流程中校验；运行时通过显式 mapping 转成 DTO 枚举，不依赖更改现有枚举数值。
- Object：`npc/player/world`。Bar：`stat/relation/world`，`highIsGood/lowIsGood`，`monotonic/event_driven/derived`。
- Trigger：`up/down/both`，`cross/hold`，`once/repeat`。Event：`actor/world`。EventDate：`morning/noon/night`，`once/daily/weekly/yearly`。
- Result 使用原全大写标签。设计标准 `OBJ_STATE`、`AWARD_ACH` 和兼容旧名 `OBJECT_STATE`、`AWARD_ACHIEVEMENT` 均允许，adapter 映射到同一 DTO 值。
- Condition 的 `ownerType` 只允许 `event/choice`，`condType` 按 schema 中的 15 种条件标签校验。Template 的 `interruptType` 只允许 `progressive/playback`。
- Luban strict 拒绝 map 重复主键、Bar 重复 ownerId+barId、非法枚举及 schema 标注的坏引用。可选空对象/文本引用用 `ref=表名?`，非空仍必须存在。
- 多 key 字符串、条件多态 owner、Result 的事件或 choice-result owner 等动态引用仍需运行时/Editor adapter 的语义校验，不能由简单单表 `ref` 替代。

## 最小运行库与验证

`Assets/Scripts/Config/RuntimeSupport/BeanBase.cs` 和 `ITypeId.cs` 原样复制自官方 `D:/Luban/src/Luban.Tests/RuntimeSupport/`，与 generated C# 编译在同一程序集；`LICENSE.txt` 保留官方 MIT 许可，来源 https://github.com/focus-creative-games/luban 。JSON 后端不需要 ByteBuf 或 SimpleJSON。

迁移时执行过真实 Luban strict 生成（退出 0），以及隔离副本检查：重复 Object 主键、重复 Bar 复合索引、非法 objType、坏 Trigger eventId 均退出 1；不同 owner 的同名 BarId 退出 0。正式数据不受这些负例修改。

`LocalizedTMPText` 仍通过 TMP 初始 text 作为 key 注入 `tbtext`。非配表动态数值不需要挂载。旧 TextTable 归档及 GenerateText 兼容入口已移除，只维护 Excel。
