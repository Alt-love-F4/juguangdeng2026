# 角色美术需求与正式配置的连接

日期：2026-10-19 接线；2026-10-20 补齐 NPC 通用立绘与店内黑影。

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
| 索雷尔 `sorel` | 中性三分之二身黑影占位 | 128 | 保留独立身份；允许登场的对白加载通用占位 |
| 埃马纽埃尔 `emmanuel` | 已配置原画导出 | 0 | 不会自动供 `sorel` 使用 |
| 公爵 `elion` | 已配置原画导出 | 83 | 83行允许登场 |
| 旧公爵 ID `church.duke` | 中性三分之二身黑影占位 | 0 | 当前没有该 ID 的对白登场引用；配图不自动让其登场 |

麻雀的 `rose.1011.s1` 有17句，全部 `side=none`。因此在这段看到姓名和文字、没有麻雀立绘，原因是该段演出配置，不能仅凭需求卡的 `placeholder` 状态判断。

32名原目录人物均有三类资源路径，其中20名基础立绘有真实原画导出记录，12名基础立绘仍为占位。咖啡店场景已为7名角色绑定独立坐姿；缺少专属坐姿时使用咖啡店坐姿黑影。某个角色基础图已接好，不等于其表情、服装和坐姿全部交付。

本连接只核对并呈现现有事实，不合并 `sorel`／`emmanuel` 等身份，不自动改 `side`，不把未决需求标成完成，也不修改剧情条件、存档或事件结果。`Docs/RuntimeCharacters.md` 已明确保留 `emmanuel` 与 `sorel` 的独立 ID；后续若确认身份与服装需求，应从源表及内容编译器修正。

## 实现与验证边界

- `Assets/Scripts/Narrative/Editor/NarrativeCharacterArtAudit.cs`：读取当前生成配置与实际资源，单项缺图不阻止其他角色的核对。
- `Assets/Scripts/Narrative/Editor/NarrativeArtPlaceholderBuilder.cs`：保留原绑定逻辑，扩展清单读取字段与现有窗口。
- `Assets/Scripts/Narrative/Editor/Tests/NarrativeCharacterArtAuditTests.cs`：真实麻雀资源与正式目录一致、双 ID 缺图、登场／场外台词区分、历史路径失效、差分回退、无运行时／配置写入等 EditMode 回归。

窗口不创建 NarrativeRuntime、CafeWorld、订单或测试场景。这里只读连接与资源加载测试不代表自然通关或全部演出实机验收；演出 Panel 回归仍使用 `PresentationDebugScene.unity`。

2026-10-19 最终验证：Unity 编译错误为0；实际 EditMode 叶用例 **17/17通过**（新增角色需求连接7、RuntimeCharacter6、ArtPlaceholder4）。原始结果归档于 `output/character-art-integration-tests.json`，runId `0975f0b4-777b-4224-9c67-8b585ad36b34`。Bridge 的30条 passed 回调包含 suite，不能写成30个测试。本轮未改演出 Panel 或源剧情表，未以资源测试代替自然触发／全线实机验收。


## 2026-10-20 改用项目提供的男女剪影（当前规则）

- 原图仅来自 `Assets/Art/Gameplay/剪影/` 的男站姿、女站姿、男坐姿、女坐姿，原PNG、PSD、Prefab与纸张素材保持不变；没有调用生图API。
- 对话图只保留人物透明边界上方2/3，维持原画颜色、笔触、衣服和头饰，不重绘。头像为同源头肩裁切。32个原目录角色中只替换12名缺图角色的基础图/头像，20名已有专属立绘及顾客差分不动。42个其他缺图NPC改用同源共享男女资源，姓名/ID/来客资格/对白站位均不变。
- 人物表明确客人B、客人D为女性；王后、阿黛尔、阿莉耶诺尔、柯莱特、教会女童和老妇人使用女素材。教程B太太使用女站姿/女坐姿。原稿未明确性别的配角使用默认男剪影外形，不据姓名推断性别，不写入新的剧情性别事实。玛丽保留已有女扮男专属原画。证据与外形映射见 `Tools/Characters/silhouette-art-manifest.json`。
- 咖啡店32份旧坐姿占位改为对应男女素材；42个共享占位NPC同样按源证据选择坐姿。坐姿保留完整身体与腿脚，不裁下方1/3；只去掉外围透明留白，等比放入透明262×530画布，沿用场景既有坐姿pivot，不移动桌椅。站姿对话图才保留上2/3。女坐姿原图朝左，导入咖啡店时归一为朝右以复用现有座椅flipX规则；对话站姿不镜像。
- `FreeCafeSeating.Artwork` 保留7名专属坐姿优先、显式CafeVisit图优先及验收预览规则；无专属图时读Object的worldSprite，不再把女客也统一回退到单一男黑影。没有角色ID的教程顾客按既有tutorial_customer身份取女坐姿，未知身份才用场景默认男坐姿。
- 源表通过 `integrate_content.py --character-visuals-only` → `merge_workbooks.mjs` → 官方 `GenerateAll.bat` 修改/生成。本次Object页126处路径替换，其他剧情内容不动；全量重编译仍保留已填资源。
- 上一轮自制中性黑影三张PNG及其meta、准备脚本和 `output/npc-art-2026-10-20` 预览/临时产物清理；不保留该版本作为候选。历史CH35老太太原图（此前独立教程美术批次）不再用于本轮通用占位，未删除无关历史美术。

本地裁切入口：`Tools/Characters/prepare_authored_silhouettes.py`；原 `build_character_art.py --placeholders-only` 转调同一入口，避免重建恢复旧图。当前衍生PNG仅用于游戏实际接线；来源仍标占位，不代表专属角色原画与表情验收。

验证（2026-10-20，坐姿规则修正后）：男女坐姿逐像素匹配完整原图等比处理，腿脚保留；68份站姿/头像文件hash不变，32份原目录坐姿与对应男女完整坐姿逐像素一致。源表仅Object资源路径126格变化，74个NPC资源文件均存在；旧generic目录和上一轮自制预览目录已清理。官方Luban生成通过；裁切规则3项与配置策略4项Python检查通过；Unity EditMode的RuntimeCharacterTests、NarrativeCharacterArtAuditTests、FreeCafeSceneLayoutTests合计28项通过（男女客人、教程女坐姿、专属坐姿和显式来客覆盖优先规则均保留）。未把资源检查当作本轮完整PlayMode演出验收。
