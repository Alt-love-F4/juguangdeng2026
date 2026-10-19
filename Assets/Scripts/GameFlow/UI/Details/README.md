# 详情面板配置与接入

Map 的详情按钮通过 GameFlowBootstrap.OpenDetails 打开真实数据窗口。DetailsPanel 保持展示快照职责，DetailsGameDataAdapter 读取当前 NarrativeRuntime 的对象、进度值及 PlayerDetailsProfile。

`Assets/Resources/DetailsDisplayConfig.asset` 补充当前 NCS 表尚缺的展示字段：男女剪影、共用问号头像、按 objectId 的头像与初始解锁覆盖、按 barKey 的说明与上下限说明、专精图标与文本。资产里保留的 npc_a 等角色/进度项属于早期原型，不再作为正式剧情角色名单；正式名单与解锁状态从 NCS Object、known.<角色ID> 和当前存档生成。四属性没有表数据时读取档案，初始为 0；专精初始为空。

剧情使用现有可存档的 NCS object_state 结果：

- target `details.character.<objId>`，value `unlocked` / `locked`，覆盖初始解锁配置；剧情相识的权威展示依据为现有 NCS fact `known.<objId>`。
- target `details.bar.<ownerId.barId>`，value `visible` / `hidden`，覆盖初始显隐配置。世界页在每次打开、NCS TurnCompleted 或主动调用 RefreshDetails 时重新生成列表。
- 专精可以用 NCS flag `details.specialization.<id>` 或 PlayerDetailsProfile.Current.Specializations 中的 ID 解锁。

背景形成后调用 `PlayerDetailsProfile.Current.Background = 已解析背景文本`；属性调用 `SetAttribute(index, value)`，index 按语言、勇气、智慧、知觉排序，值钳制为 0–20。已有 `player.language` / `player.courage` / `player.wisdom` / `player.perception` 进度条时优先读取其当前值。背景选项本身尚无策划配置，当前没有自行添加选项。

新游戏读完或跳过叔叔信均进入男女头像选择，选择只影响剪影。档案 participant 为 `game.player-details-profile.v1`，新增 AvatarSelection 状态编号为 11，保留旧状态序号。

验收：新游戏跳过信后选择头像；Map 打开详情；切换三页；配置解锁人物并点开/返回；配置多于 6 人验证翻页；人物及世界多进度验证滚动和悬停；窗口内点击保持打开，关闭按钮/外部区域/Esc 关闭；关闭后地图输入恢复。正式描述和背景选项需要策划补充后再验收内容准确性。

## 剧情关系与世界概况（2026-10-19）

- 人物列表读取正式 NCS 的 known.角色ID；已有 details.character.角色ID object_state 和 DetailsDisplayConfig.initiallyUnlocked 仍作为明确覆盖。无好感 Bar 的临时剧情 NPC 只有在真实相识后才加入列表。
- 阿黛尔在 DayFlowController.IsFirstSessionGuideActive 期间可临时显示，提示明确说明正式相识状态尚未写入；该显示不会设置 NCS fact，也不会写存档。教程结束后仍按 known.adele 判断。
- 关系卡 hover 显示介绍、相识状态和实际 Relation Bar 当前值/范围；解锁前不暴露关系值。人物页保留数值进度，同时明确数值不等同对人物态度的自动推断。
- 世界页概况从当前存档读取日期、稳定度、拘押状态及 route flags；指标只展示 world.stability 与四条 faction.*，日期单独呈现，不将训练、日历原始值或咖啡馆资金混作世界指标。
- 世界概况卡置于原世界指标选择滚动区顶部；人物卡片和势力指标可 hover 查看详细描述。UI 复用现有 DetailsPanel、PSD 页面和 tooltip，不增新场景或存档字段。
- 验证：Unity PlayMode 3/3 通过，覆盖正式 NCS 相识/好感与世界指标投影、首日教程阿黛尔临时可见且不写相识事实、详情面板人物/势力悬停与世界概况卡。UI截图位于 output/details-panel/details-relationship-live.png 和 details-world-overview-live.png。
