# 工作室开场标志动画（Organic Game Workshop）

Unity 启动画面结束后、主菜单出现前，播放一次有机游戏工坊标志动画（约 5 秒）。

## 流程

- 入口：`GameFlowBootstrap.StartupRoutine` 在第一个 Build 场景（`启动场景`）启动时调用 `StudioLogoSplash.Play()`，等动画淡到全黑后打开主菜单，再由 `Reveal()` 淡出黑幕露出菜单。
- 每次运行只播一次；返回主菜单不会重播。测试或工具在其他场景里创建的 Bootstrap 不播放。
- 任意键 / 鼠标 / 触屏 / 手柄 A、Start 可在 0.25 秒后跳过（0.28 秒淡黑）。`StartGame` 会直接移除动画。
- 动画只用非缩放时间，并以音轨播放位置校准；音量跟随设置里的 主音量 × 音效音量。

## 分镜（秒）

| 时间 | 画面 | 声音 |
| --- | --- | --- |
| 0.05–0.35 | 发光种子从上方落下 | 玻璃般的叮 |
| 0.35–1.2 | 镜头拉近：根部弹出，藤蔓自下而上生长，三片叶子随生长依次展开，荧光绿↔薄荷绿循环变色 | 木质 pop 音阶上行 + 生长气声 |
| 1.3–1.85 | 镜头后拉，手柄左右两半带残影从屏幕两侧加速合拢 | 双侧呼啸，空气上扬音 |
| 1.85 | 撞击：白闪、震屏、双冲击波、叶片/火花爆散，白色圆徽章弹出，全部变为品牌深绿 | 低频重击 + 碎裂 + 和弦 |
| 2.0–2.32 | 十字键与四个按键顺时针依次旋转弹出（先亮绿后定色） | 8-bit 琶音 |
| 2.42–3.2 | ORGANIC 逐字落下回弹，GAME WORKSHOP 逐字滑入 | 逐字轻响 + 微光 |
| 3.35–4.0 | 光带扫过徽章，高光处闪星 | 风铃闪烁 |
| 4.55–5.05 | 淡入全黑，随后露出主菜单 | 混响尾音 |

## 素材

`Tools/StudioSplash/generate_studio_splash.py` 从 `Tools/StudioSplash/source/organic_game_workshop_logo.jpg`（团队图标）生成：

- `Assets/Resources/StudioSplash/studio_splash_atlas.png`：按笔画切分的白色图层 + 辉光/粒子贴图，运行时着色；所有图层拼合后与原图一致。
- `studio_splash_layout.json`：图集像素矩形、标志坐标矩形与动画锚点（叶子根部、手柄内侧等）。
- `studio_splash_sfx.wav`：与时间轴同步的整段合成音效。

更换图标或调整节奏时：修改脚本后在项目根目录运行 `python Tools/StudioSplash/generate_studio_splash.py`；时间轴常量须与 `Assets/Scripts/GameFlow/UI/StudioLogoSplash.cs` 保持一致。音效可直接用同名同长度的正式音频替换。
