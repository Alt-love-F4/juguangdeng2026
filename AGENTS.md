# Agent guidance: shared presentation debug scene

当需要测试、截图或验证演出 UI 时，**必须优先使用**：

```text
Assets/Scenes/PresentationDebugScene.unity
```

不要为了测试单个 Panel 重新创建临时 Scene、Canvas 或输入系统。

## 快速入口

进入 Play Mode 后使用以下按键：

- `Q`：新一天 / `DateChangePanel`
- `W`：第一天来信 / `FirstDayLetterPanel`
- `E`：双角色对话 / `DialoguePanel`
- `R`：经营前阶段 / `PreBusinessPanel`
- `T`：开店演出 / `CafeOpeningPanel`
- `Y`：经营结束 / `BusinessEndPanel`
- `U`：城区地图 / `CityMapPanel`
- `I`：夜晚前阶段 / `PreNightPanel`
- `O`：住房夜晚 / `HomeNightPanel`
- `P`：一天结束 / `DayEndPanel`
- `F`：首次登场对话 / `FirstDayIntroductionDialogue`
- `Space` / `Enter`：继续（对话先补全文字）
- `Esc`：关闭当前演出
- `Backspace`：重播上一次演出
- `H`：收起 / 显示右侧说明，方便无遮挡截图
- `F9`：切换 timeScale 暂停，验证非缩放时间演出
- `F12`：恢复说明与调试提示

场景中右侧也会显示完整按键说明，底部会提示当前状态。所有演出使用硬编码测试数据，不推进 DayFlow、不写入存档。修改已有演出 Panel 后，应在这个场景回归验证。

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

<!-- packy-image:start -->
## PackyAPI image capability (Windows, this user)

All projects have a user-wide image-generation skill at `C:\Users\AltF4\.agents\skills\packy-image\SKILL.md` (invocation: `$packy-image`). When a user requests new AI-generated pictures or a task benefits from raster imagery, read that skill and use its helper rather than changing the Codex main model. Prefer this PackyAPI route unless the user specifies another provider. This capability supports new images, image-to-image edits, up to 16 local reference images, and optional transparent PNG masks. For an edit use the actual source image with edit.ps1, never text-only reconstruction.

The model/endpoint are in the skill's `config.json`; the key is separate and Windows-encrypted, never in a project or chat. Check key availability first; if missing, direct the user to the hidden-input setup script. Do not reuse a disclosed key or read Codex auth files. Generate only the images reasonably needed, without automatic retries or unrequested batches. Ask before uploading sensitive content. Save images in the current project's appropriate outputs/assets directory and show them by absolute filesystem path. Report actual failures and never claim an untested API/model works.
For one-request model selection pass `-Model` (`gpt-image-2`, `gpt-image-2.5-flare`, or `gpt-image-2.5-sunburst`) without changing global defaults. Preserve source images and save edited results to new files. User-requested editing uploads the referenced files to PackyAPI; do not include unrelated private project content. A mask must match the first image and uses alpha=0 for the edit region. Do not claim PackyAPI editing compatibility until a real edit succeeds; installation tests are network-free.
<!-- packy-image:end -->
