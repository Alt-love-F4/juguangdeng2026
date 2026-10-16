# D 王庭教会前中段施工交付记录

日期：2026-10-16（施工时间约 02:43–02:44 +08:00）。范围仅 D 批次 `church.1009.s2`、`church.1014.s2`、`church.1020.s1`、`church.1023.s1`、`church.1023.s2`。本记录不把规格、配置存在或静态文本核对写成游戏验收通过。

## 当前结论

本轮按规格顺序启动首件 `church.1009.s2`。已完成该事件的原稿/source-map 校验和 Text 源表增量，但在官方生成门禁处停止：Luban 读取既有 `Narrative.xlsx` 时发现 `ConversationLine` 源表缺少当前生成代码要求的 `speakerNameKey` 列。该共享结构差异来自并发工程改动范围，本轮未修改、未裁决、未用 D 配表绕过。

因此：1009 仅完成“已验证正文 Text 源增量”，尚未形成正式 Event/入口/分支/结果，也未完成编译、共享调试场景或正式链路测试。其余四个事件未施工，避免在公共生成门禁未解除时继续写入并扩大冲突。

## 已施工事件

### `church.1009.s2`

实际改动：

- `Tools/Narrative/import_church_text.py`：新增可恢复的单事件 Text 施工器。它按 D `source-map.md` 读取正文/选项映射，核对 DOCX SHA-256、非空段落数量、逐段原文顺序；清理仅规格允许的角色前缀、表情/选项注记，并保留 1009 正文原文。
- `Assets/ConfigTables/Data/Shared/Text.xlsx`：追加 38 条 `church.1009.s2.*.text`，既有 5,152 个单元格在候选文件回读校验中保持值、类型、样式和批注不变。未写 Event、EventDate、Action、ConditionKey、Result、完成事实或地图挂载。
- `Assets/Settings/Resources/Luban/tbtext.json`：由官方 `GenerateAll.bat` 尝试生成；该输出是生成器副产物，不能视为 D 事件已生成。
- `Assets/Scripts/Config/Generated/NcsConversationLineEntry.cs`：官方生成尝试期间检测到的既有工作树差异；本轮未手改。
- 施工证据目录：`Library/Locus/tmp/church-content/20261016-024350-681461-1009.s2/`，含 `Text.before.xlsx`、候选文件和 `manifest.json`。

原稿证据：

- 来源：`D:/桌面/文案/王庭教会/MA70.10.09-S2.docx`
- SHA-256：`15c3101b85329f082efa98d9c938791089d9ac5e5cb35c50d8788fa4a3d9c58d`
- 非空段落：50；进入 Text 的正文/选项行：38。
- 前置仍未伪造：仅 `story.rose.1009.s1.completed=true` 才允许正式入口；现有工程记录说明 B 新玫瑰仅有 Text、没有该完成事实生产者，因此没有配置正式触发。

## 实际验证证据

已执行：

1. 施工器：`python Tools/Narrative/import_church_text.py --event 1009.s2`，成功；输出 38 条，追加行 1289–1326，原稿 50 段和 SHA 校验通过。
2. 官方生成：`MSYS_NO_PATHCONV=1 cmd.exe /d /c 'Assets\\ConfigTables\\GenerateAll.bat'`，失败，exit code 1。
3. 失败定位：`Player semantic validation passed`；Luban 在加载 `Narrative.xlsx` 的 `ConversationLine` 表时报错：`bean:'NcsConversationLineEntry' 缺失列:'speakerNameKey'`。
4. 源表核对：当前 `ConversationLine` 表头为 `lineId,pageId,seq,speakerKey,side,textKey,voiceKey,emote,sfxKey,accentFxKey,accentTargetKey`，确实没有 `speakerNameKey`。
5. Unity 当前状态只读检查：Editor 仍在播放，Active Scene 为 `Assets/Scenes/启动场景.unity`，未切换到共享调试场景；未宣称运行验证。

已有工程验收证据仍仅来自：

- `Docs/superpowers/reports/2026-10-15-october07-acceptance.md`，覆盖 oct07，不覆盖 D。
- `Docs/superpowers/reports/2026-10-15-october07-content-map.md`，覆盖 oct07 配置，不覆盖 D。
- `Docs/superpowers/reports/2026-10-16-A-neutral-implementation.md`、`2026-10-16-rose-content-implementation.md`，说明跨批次前置和共享工程并发状态；不是 D 的运行验收。

## 仍阻塞事件

- `church.1009.s2`：阻塞于新玫瑰真实完成事实、同根选择路由能力、官方源表/生成结构的 `speakerNameKey` 差异，以及后续资源/入口联调。当前只完成 Text 源增量。
- `church.1014.s2`：未施工。阻塞于 A 活动真实完成别名、C 上午来信真实完成事实、王庭—教会倾向真实数值源、同根双组选择/知觉替代路由，以及同一公共生成门禁。
- `church.1020.s1`：未施工。阻塞于真实密信来源、同根多分支检定能力、拘押闭环/D-C04；不能按日期或旧事件 ID合成密信。
- `church.1023.s1`：未施工。贿赂金额 B 未定；革命联合倾向真实数值源、1020 真实拘押、拘押日程和同根后备/释放方式路由均未确认；不能填 0 或伪造余额不足。
- `church.1023.s2`：未施工。只能由 `1023.s1` 完整提交 `release.failed=true` 续接，不能按 day18 或日期自动播放；D-C05 Ending 接线也未验证。

## 后续续做入口

1. 由共享配置/生成物所有者先统一 `ConversationLine` 源表与生成 schema/代码的 `speakerNameKey` 契约，完成官方生成和 Unity 编译；本轮不替其他 Agent 决定列语义。
2. 生成门禁解除后，从施工器的幂等入口重新核验 `--event 1009.s2`，再为 1009 配置正式入口和根内分支；先确认 B 的 `story.rose.1009.s1.completed` 真实生产者。
3. 每个事件继续遵守“配置 → 官方生成 → 编译/校验 → `Assets/Scenes/PresentationDebugScene.unity` 或正式链路测试 → 修复 → 记录”，不把 Text 生成结果当作可玩交付。
4. 保持 D 施工包三份原稿映射不改；正式施工前继续核对 ConditionKey/Bar/Result/Event 占用，避免仅凭裸文件名或旧事件 ID 接线。

## 尚未验证项

共享调试场景、正式地图/DayFlow、AfterBusiness/MorningBeforeMap/MapAction/Immediate 安全入口、取消与重试、选择互斥、属性边界、MoneyAccount、拘押跳日、Ending、资源占位和存档恢复均未在 D 本轮运行。未新增 Scene、Canvas、平行剧情系统、钱包、临时地图行动或 D 之外事件。
