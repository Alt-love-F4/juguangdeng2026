# 角色美术需求与正式配置的连接

日期：2026-10-19。

`Tools > Narrative > Art Requirement Placeholders` 现在同时读取美术需求台账、正式 Luban `Object` 与实际 Sprite 资源。菜单位置不变，窗口标题为“美术需求与当前资源”。默认显示所有需求，可按编号、名称、明确的角色 ID 搜索；“仅含需求卡”是台账筛选，不代表筛选出全部未配置角色。

## 当前状态从哪里来

- 需求到人物的关联使用已有清单的 `objectEvidence.objId`，不按显示名猜测身份，也不使用清单中历史 `portraitPath=空` 的文字作为配置结论。
- 当前路径来自 `LubanNarrativeLoader.Load()`，加载复用 `CharacterCatalog.LoadSprite`；头像优先 `avtarPath`，为空时读 `iconPath`；店内形象为空时沿用基础立绘，与正式角色目录保持一致。
- `Tools/Characters/character-art-manifest.json` 只提供原画导出／占位来源说明，不作为游戏配置源，不代表美术验收。未找到来源记录的可加载图显示“美术状态待核”。
- 背景／CG仍按原来的 `NarrativeStageVisuals` 查找。角色立绘不需要为了让窗口识别而额外注册为背景，独立需求卡也不会替换角色立绘。
- 每个明确角色 ID 分别展示基础图、头像、店内形象、Conversation／Reading 引用数、允许立绘登场的对白数、`side=none` 数量，以及最多八条引用示例。
- `side=none` 只显示姓名和文本，不让该句引入人物立绘。有对白引用不代表所有台词都显示立绘；引用计数也不代表事件自然可达。Opening 使用左右／中央槽位和独立适配，不混入这些按 `speakerKey` 统计的数字。
- 当前正式 `CharacterCatalog.LoadFromTables` 只绑定基础立绘。窗口列出源表里的非基础表情；在允许登场的对白中，它们仍回退基础图。文件夹里存在某张差分 PNG 不能证明已被运行时绑定。

窗口有“游戏当前资源”和“原始需求卡／记录”两个预览入口；基础图、头像和店内形象各有预览按钮。“重读当前配置”及项目资源变化会重新建立只读快照。配置应先改源 Excel，再运行既有官方 Luban 生成管线。

## 此次核对发现

| 角色／ID | 当前基础图 | Conversation 引用 | 立绘登场规则 |
|---|---|---:|---|
| 麻雀 `sparrow` | 已配置原画导出 | 112 | 83行允许登场，29行 `side=none` |
| 索雷尔 `sorel` | 路径为空 | 128 | 128行配置为登场，但没有基础图 |
| 埃马纽埃尔 `emmanuel` | 已配置原画导出 | 0 | 不会自动供 `sorel` 使用 |
| 公爵 `elion` | 已配置原画导出 | 83 | 83行允许登场 |
| 旧公爵 ID `church.duke` | 路径为空 | 0 | 当前没有该 ID 的对白登场引用 |

麻雀的 `rose.1011.s1` 有17句，全部 `side=none`。因此在这段看到姓名和文字、没有麻雀立绘，原因是该段演出配置，不能仅凭需求卡的 `placeholder` 状态判断。

32名正式人物均有三类资源路径，其中20名基础立绘有真实原画导出记录，12名基础立绘仍为占位；正式坐姿仍为占位。某个角色基础图已接好，不等于其表情、服装和坐姿全部交付。

本连接只核对并呈现现有事实，不合并 `sorel`／`emmanuel` 等身份，不自动改 `side`，不把未决需求标成完成，也不修改剧情条件、存档或事件结果。`Docs/RuntimeCharacters.md` 已明确保留 `emmanuel` 与 `sorel` 的独立 ID；后续若确认身份与服装需求，应从源表及内容编译器修正。

## 实现与验证边界

- `Assets/Scripts/Narrative/Editor/NarrativeCharacterArtAudit.cs`：读取当前生成配置与实际资源，单项缺图不阻止其他角色的核对。
- `Assets/Scripts/Narrative/Editor/NarrativeArtPlaceholderBuilder.cs`：保留原绑定逻辑，扩展清单读取字段与现有窗口。
- `Assets/Scripts/Narrative/Editor/Tests/NarrativeCharacterArtAuditTests.cs`：真实麻雀资源与正式目录一致、双 ID 缺图、登场／场外台词区分、历史路径失效、差分回退、无运行时／配置写入等 EditMode 回归。

窗口不创建 NarrativeRuntime、CafeWorld、订单或测试场景。这里只读连接与资源加载测试不代表自然通关或全部演出实机验收；演出 Panel 回归仍使用 `PresentationDebugScene.unity`。

2026-10-19 最终验证：Unity 编译错误为0；实际 EditMode 叶用例 **17/17通过**（新增角色需求连接7、RuntimeCharacter6、ArtPlaceholder4）。原始结果归档于 `output/character-art-integration-tests.json`，runId `0975f0b4-777b-4224-9c67-8b585ad36b34`。Bridge 的30条 passed 回调包含 suite，不能写成30个测试。本轮未改演出 Panel 或源剧情表，未以资源测试代替自然触发／全线实机验收。
