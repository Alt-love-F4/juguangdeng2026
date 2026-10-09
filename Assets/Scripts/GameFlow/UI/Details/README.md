# 详情面板配置与接入

Map 的详情按钮通过 GameFlowBootstrap.OpenDetails 打开真实数据窗口。DetailsPanel 保持展示快照职责，DetailsGameDataAdapter 读取当前 NarrativeRuntime 的对象、进度值及 PlayerDetailsProfile。

`Assets/Resources/DetailsDisplayConfig.asset` 补充当前 NCS 表尚缺的展示字段：男女剪影、共用问号头像、按 objectId 的人物头像和初始解锁状态、按 barKey 的说明与上下限说明、专精图标与文本。当前 4 名人物和 11 条进度配置已按表 ID 建立，描述留空待策划填写；所有 NPC 默认未解锁。四属性没有表数据时读取档案，初始为 0；专精初始为空。

剧情使用现有可存档的 NCS object_state 结果：

- target `details.character.<objId>`，value `unlocked` / `locked`，覆盖初始解锁配置。
- target `details.bar.<ownerId.barId>`，value `visible` / `hidden`，覆盖初始显隐配置。世界页在每次打开、NCS TurnCompleted 或主动调用 RefreshDetails 时重新生成列表。
- 专精可以用 NCS flag `details.specialization.<id>` 或 PlayerDetailsProfile.Current.Specializations 中的 ID 解锁。

背景形成后调用 `PlayerDetailsProfile.Current.Background = 已解析背景文本`；属性调用 `SetAttribute(index, value)`，index 按语言、勇气、智慧、知觉排序，值钳制为 0–20。已有 `player.language` / `player.courage` / `player.wisdom` / `player.perception` 进度条时优先读取其当前值。背景选项本身尚无策划配置，当前没有自行添加选项。

新游戏读完或跳过叔叔信均进入男女头像选择，选择只影响剪影。档案 participant 为 `game.player-details-profile.v1`，新增 AvatarSelection 状态编号为 11，保留旧状态序号。

验收：新游戏跳过信后选择头像；Map 打开详情；切换三页；配置解锁人物并点开/返回；配置多于 6 人验证翻页；人物及世界多进度验证滚动和悬停；窗口内点击保持打开，关闭按钮/外部区域/Esc 关闭；关闭后地图输入恢复。正式描述和背景选项需要策划补充后再验收内容准确性。
