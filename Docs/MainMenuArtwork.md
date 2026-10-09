# 正式主菜单美术接入

当前 `UIRoot/Level_Common/主界面` 使用 PSD 美术的 uGUI 版本，逻辑仍是 `MainMenuPanel`，没有另建菜单流程。

## 入口

| 正式 UI | 原功能 |
| --- | --- |
| 开始游戏 | `GameFlowBootstrap.StartGame()`；按既有配置进入 Map/新游戏流程 |
| 继续游戏 | `OpenSaves(false)`；保留完整存档管理，不自动覆盖或跳过存档选择 |
| 设置 | `OpenSettings(false)` |
| 退出游戏 | `QuitGame()`；与原主菜单一致，直接退出（Editor 中停止 Play Mode） |

菜单通过 UIKit 命名栈管理；关闭存档/设置后返回主菜单。按钮底板整个区域都可点击，并保留方向键导航、颜色反馈和按压反馈。

## 资产与编辑

- 源美术：`Assets/Art/PSD/主界面.prefab` 和其 PNG 图层，保持不变。
- 正式资源：`Assets/Resources/Art/UIPrefab/MainMenuPanel.prefab`，保留原资源路径与 GUID，因此正式启动场景和返回菜单也使用新美术。
- `UIRoot.prefab` 只保存 Root/层级/Storage，不再包含旧的调试菜单实例。
- 场景中的 `主界面` 是正式 Prefab 的编辑预览。`GameFlowBootstrap.mainMenuScenePreview` 显式引用它，在 Awake 时隐藏并销毁预览，再由 UIKit 加载唯一的受管实例。场景预览不作为第二个活动菜单参与流程。
- 全屏背景保持位置/比例，只淡入淡出；Artwork 用 `AspectRatioFitter.EnvelopeParent` 适配窗口。装饰图层不拦截点击，底图阻止点击穿透。

修改源 PSD 图层并重新导出后，可执行：

- `Tools > Game Flow > Rebuild Main Menu From PSD Artwork`：更新正式 Prefab。
- `Tools > Game Flow > Install Main Menu Artwork In Current Scene`：更新当前已有 UIRoot 下的预览，并绑定 GameFlowBootstrap；操作后保存当前场景。只替换主菜单，不重建场景或输入系统。

生成器：`Assets/Editor/GameFlow/MainMenuArtworkBuilder.cs`。

## 验证

使用共享 `Assets/Scenes/PresentationDebugScene.unity` 的现有布局验证。未改 Build Settings，也没有新建测试场景/Canvas/EventSystem。原演出调试对象的启用状态保留用户当前设置。

`Spotlight.GameFlow.Tests.MainMenuPanelTests` 检查四个按钮的绑定、PSD 图层复用、导航、有限布局数值、全屏比例和无背景滑动/缩放。运行时另外检查受管菜单唯一性、真实 GraphicRaycaster 命中、打开和返回存档/设置、退出停止 Play Mode，以及开始游戏场景切换。
## 本次验证结果

- 主菜单资产测试：2/2 通过。
- 共享场景 Play Mode：只有一个 MainMenuPanel 与一个 GameFlowBootstrap；四个按钮中心均由真实 GraphicRaycaster 命中对应按钮。
- 点击设置／继续游戏：分别打开 SettingsPanel／SaveManagementPanel，返回后菜单恢复且导航栈深度为 1。
- 点击退出游戏：按原行为停止 Editor Play Mode。
- 点击开始游戏：进入 `Assets/Scenes/Map.unity`，出现 FirstDayLetterPanel；主菜单和加载面板进入缓存，GameFlowBootstrap 保持唯一。
- 验证结束后退出 Play Mode，回到共享场景；未执行读档、手动保存、删除存档或设置修改。
- 运行时截图：`output/main-menu/main-menu-runtime.png`。
