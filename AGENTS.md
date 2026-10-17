# Agent guidance: shared presentation debug scene

## NCS 咖啡馆实景背景约定（2026-10-16）

- **咖啡馆内普通对话使用游戏实际场景，不叠加全屏背景图。** 在 `Assets/ConfigTables/Data/Narrative/Narrative.xlsx` 的 `ConversationPage.bgOverride` 显式填写 `scene.cafe`；空值仍是继承上一页，不是关闭背景。
- 开场 `first_day_cafe` 与 `cafe_tutorial.*` 在 `OpeningPresentation.visualsResource` 填 `Dialogue/CafeSceneVisuals`。该资源的 `backgroundKey=scene.cafe`，立绘和文本仍走原有适配层。
- `scene.cafe` 不是图片路径或黑幕：面板必须取消旧背景淡入淡出并关闭自身背景层。街道、俱乐部、教堂等店外演出仍填写实际图片 key；进出店内时显式切换，不能整事件统一清空。
- **店内特殊叙事画面是实景规则的例外。** 闭门蒂博剪影等特写/合成图仍显式填写图片 key，例如 `cafe.door.thibaut`（兼容策划旧 key `church.cafe.thibaut.departure`），不得因发生地点在咖啡馆而批量替换为 `scene.cafe`。特殊画面结束、回到普通店内对话时显式恢复 `scene.cafe`。2026-10-17 核对：图片及注册均保留，`church.1014.s2` 尚未录入正式源表；后续按 source-map 的 P072（P071 后/P073 前，需求清单 P087）接入，不得把资源已登记写成剧情已接线。
- 正式店内页若从地图触发，复用 `GameFlowBootstrap.TransitionToGameplayScene` 准备 `CoffeeShop` 后再显示对话；预览不加载正式场景。该标记不负责解锁楼层、切楼、推进时间或模拟晨昏光照。
- 一、二、三楼原始经营底图仅供实际经营场景组装：`SCE_一楼大厅场景底图.png`、`SCE_二楼会客室场景底图.png`、`SCE_ 三楼宿舍场景底图.png` 不得作为 `ConversationPage.bgOverride`、`DialogueVisualsAsset.background` 或对话背景目录的 Texture。对话只能使用对应的完整对话构图（或 `scene.cafe` 实景标记）；不要删除或覆盖经营原图。
- 配表必须修改源 Excel 再通过项目现有官方 Luban 管线生成 JSON；不要仅手改生成 JSON，不新增平行剧情字段或复制一套演出系统。
- 详见 `Docs/NcsCafeSceneBackground.md`；后续修改使用已有 `PresentationDebugScene.unity` 回归图片→实景→图片及连续翻页/取消/重播，不能另建临时场景。

当需要测试、截图或验证演出 UI 时，**必须优先使用**：

```text
Assets/Scenes/PresentationDebugScene.unity
```

不要为了测试单个 Panel 重新创建临时 Scene、Canvas 或输入系统。

## 快速入口

进入 Play Mode 后使用以下按键：

- `Q`：新一天 / `DateChangePanel`
- `W`：NCS 第一天来信 / `FirstDayLetterPanel`；`Shift+W` 使用面板草稿
- `E`：NCS 指定开场对话 / `DialoguePanel`；`Shift+E` 使用硬编码长文本示例
- `C`：三人选项对话测试 / `DialoguePanel`，八句台词（SKIP 快速逐条播放，到选项处停止）后展示两个可选项、一个带条件提示的灰色选项；点击后播放回应。只演示，不结算 NCS。
- `V`：阅读 / `ReadingPanel`，空格或 Enter 补全文字再翻行/页，← 返回
- `B`：事件通知 / `NoticePanel`，空格或 Enter 确认关闭
- `N`：视频过场 / `CutScenePanel`，长按 Enter 跳过（默认 1.2 秒，调试占位不代表真实视频）
- `R`：经营前阶段 / `PreBusinessPanel`
- `T`：开店演出 / `CafeOpeningPanel`
- `Y`：经营结束 / `BusinessEndPanel`
- `U`：城区地图 / `CityMapPanel`
- `I`：夜晚前阶段 / `PreNightPanel`
- `O`：住房夜晚 / `HomeNightPanel`
- `P`：一天结束 / `DayEndPanel`
- `F`：NCS 首次登场对话（默认 `first_day_cafe`）
- `G`：独立特效试验台；`1~0`：黑影入场、受惊、轻/重震屏、白闪、黑场、换位、镜头推近/复位、淡出
- `Space` / `Enter`：继续（对话先补全文字）
- `Esc`：关闭当前演出
- `Backspace`：重播上一次演出
- `H`：收起 / 显示右侧说明，方便无遮挡截图
- `F9`：切换 timeScale 暂停，验证非缩放时间演出
- `F12`：恢复说明与调试提示

场景中右侧也会显示完整按键说明，底部会提示当前状态。W/E/F 只读 NCS 配置（有运行时则优先使用其文本解析器，否则直接读取 Luban 数据）；Shift+W/Shift+E 保留硬编码入口，其余样例仍为硬编码。不启动 NCS 事件、不推进 DayFlow、不写入存档。修改已有演出 Panel 后，应在这个场景回归验证。

如果场景缺失，使用 Unity 菜单：

```text
Tools > Game Flow > Create Missing Presentation Debug Scene
```

构建逻辑位于 `Assets/Editor/GameFlow/PresentationDebugSceneBuilder.cs`，运行时入口位于 `Assets/Scripts/GameFlow/UI/Presentation/PresentationDebugSceneController.cs`。


## 场景与层级约定

- 调试场景只在 Editor 直接打开并运行，不加入正式 Build Settings。
- `DebugRoomCanvas` 是背景，sortingOrder=50；正式演出使用原有 Prefab（700/800）。
- `DebugHelpOverlay` 为右侧窄栏，sortingOrder=1000；不要在这个高层 Canvas 添加全屏不透明背景。
- `DebugCamera` 提供清屏与摄像头；演出 UI 的 Screen Space Overlay 不依赖相机。
- 测试 UI 使用 `presentation-debug` 标签和 Transient 缓存策略，退出场景只关闭自己的 UI，并恢复进入前的 timeScale。
- 扩展 `Preview` / `PlayPreview` / 公共方法，再更新 builder 里的按钮与说明，重建同一个场景。不要复制 UI Prefab 或接入正式 DayFlow。
- 打开：`Tools > Game Flow > Open Presentation Debug Scene`；重建：`Tools > Game Flow > Rebuild Presentation Debug Scene`（会覆盖此调试场景布局，需先保存场景）。
- PlayMode 回归测试：`Spotlight.GameFlow.Tests.PresentationDebugSceneTests`。测试应加载共享场景，而不是另建测试布局。

## 演出特效边界

- 特效字段来自 NCS；不要自行新增平行剧本字段标准。对话/信件已通过 `NarrativePresentationDataAdapter` 读取指定 ID/key；Panel 仍只消费快照，不依赖 NCS 全局状态，不结算事件。特效预览仍独立于 NCS 原子调度。
- 三人演出使用 `DialogueScript.CenterCharacter` / `DialogueSpeaker.Center`；中间特效目标为 `center` 或角色 key。没有第三角色时保持原双人布局。所有立绘不再镜像；不要恢复右侧负缩放。第三人测试图暂时复用现有资源，可换 `DialogueVisualsAsset.CenterPortrait`。
- `DialoguePanel.PlayEffect` / `PlayCameraCue` / `PlayFade` 是后续适配边界；参数预设在 `PresentationEffectLibrary.asset`。
- 角色/震屏/镜头/遮罩通道可并行；震屏按等级替换，不叠加。退出必须取消 handle、协程与回调，复位舞台。
- 回归使用共享场景和 `PresentationEffectsTests`，详见 `Docs/PresentationEffects.md`。不另建演出场景。

## NCS 内容接口（已接入）

- 正式开场与恢复信件：`DayFlowController` 的 `openingPresentationId` / `firstDayLetterBodyKey` / `firstDayLetterSignatureKey` 为 Inspector 可配置字段。
- 默认对话 ID：`first_day_cafe`，来自 `Narrative.xlsx` 的 `OpeningPresentation` / `OpeningLine`，按 `seq` 排序。
- 默认信件 Key：`opening.letter.body` / `opening.letter.signature`，来自 `Text.xlsx` 的 `Text` sheet。找不到完整信件时明确警告并使用完整草稿，不能显示原始 key 或混合新旧内容。
- `DialoguePlayback.PlayNcsOpening(id, ...)` 可播放任意已有 OpeningPresentation；不隐式启用 NCS 模块或触发事件结果。
- `IDialogueScriptSource` 显式注入优先；文本解析优先使用已有 `NarrativeRuntime.ResolveText`。
- 保持 Panel 无 NCS 依赖；不要将现有 NCS 事件/选项调度改成新的平行剧情系统。
- 后续更改 Excel 要重新生成 Luban JSON；本轮没有改表结构，没有新增 Letter 表。详见 `Docs/NcsOpeningPresentation.md`。

<!-- packy-image:start -->
## PackyAPI image capability (Windows, this user)

All projects have a user-wide image-generation skill at `C:\Users\AltF4\.agents\skills\packy-image\SKILL.md` (invocation: `$packy-image`). When a user requests new AI-generated pictures or a task benefits from raster imagery, read that skill and use its helper rather than changing the Codex main model. Prefer this PackyAPI route unless the user specifies another provider. This capability supports new images, image-to-image edits, up to 16 local reference images, and optional transparent PNG masks. For an edit use the actual source image with edit.ps1, never text-only reconstruction.

The model/endpoint are in the skill's `config.json`; the key is separate and Windows-encrypted, never in a project or chat. Check key availability first; if missing, direct the user to the hidden-input setup script. Do not reuse a disclosed key or read Codex auth files. Generate only the images reasonably needed, without automatic retries or unrequested batches. Ask before uploading sensitive content. Save images in the current project's appropriate outputs/assets directory and show them by absolute filesystem path. Report actual failures and never claim an untested API/model works.
For one-request model selection pass `-Model` (`gpt-image-2`, `gpt-image-2.5-flare`, or `gpt-image-2.5-sunburst`) without changing global defaults. Preserve source images and save edited results to new files. User-requested editing uploads the referenced files to PackyAPI; do not include unrelated private project content. A mask must match the first image and uses alpha=0 for the edit region. Do not claim PackyAPI editing compatibility until a real edit succeeds; installation tests are network-free.
<!-- packy-image:end -->
