# Presentation Debug Scene

项目统一的演出 UI 调试入口：`Assets/Scenes/PresentationDebugScene.unity`。
它独立于正式游戏流程，不启动 GameFlow，不推进日程，不写存档，不加入正式 Build Settings。

## 打开与运行

Unity 菜单 `Tools > Game Flow > Open Presentation Debug Scene`，进入 Play Mode，点击 Game 视图获得键盘焦点。
右侧按钮与快捷键等价；UI 看不到时先确认已进入 Play Mode、Game 视图不是 Scene 视图。

| 按键 | 实际演出 |
|---|---|
| Q | 新一天 / DateChangePanel |
| W | 第一天来信 / FirstDayLetterPanel |
| E | 双角色长剧本 / DialoguePanel |
| R | 经营前 / PreBusinessPanel |
| T | 开店 / CafeOpeningPanel |
| Y | 经营结束 / BusinessEndPanel |
| U | 城区地图 / CityMapPanel |
| I | 夜晚前 / PreNightPanel |
| O | 住房夜晚 / HomeNightPanel |
| P | 一天结束 / DayEndPanel |
| F | 首次角色登场对话 |
| Space / Enter | 继续；对话先补全文字，再翻页 / 下一句 |
| Esc | 停止当前演出 |
| Backspace | 重播上一次 |
| H | 隐藏 / 显示右侧说明（截图时隐藏） |
| F9 | 切换 timeScale 暂停，验证演出在暂停时仍播放 |
| F12 | 恢复说明与提示 |

日期测试数据为王国纪元 670年10月6日 → 670年10月7日，可在 Controller Inspector 修改。
Q 演出自动结束；W 点击信封、阅读与署名；其他日程面板使用自己的继续按钮。

## 层级与生命周期

- DebugCamera：正交摄像头，提供清屏。Screen Space Overlay 本身不需要摄像头引用。
- DebugRoomCanvas：背景层级 50，不挡实际 Panel。
- 正式演出：UIKit 加载实际 Resources Prefab，原有层级 700/800。
- DebugHelpOverlay：层级 1000，只在右侧窄栏显示与拦截点击；不要增加高层全屏不透明背景。
- EventSystem 使用 InputSystemUIInputModule 的默认动作。
- 所有预览统一标记 `presentation-debug`，使用 Transient；切换、停止、禁用或退出场景仅清理自己的 Panel。
- 回调版本号隔离已取消演出，退出场景恢复原 timeScale，不干扰游戏拥有的对话。

## 给后续 Agent 的约定

优先复用此场景验证与截图，不另建演出测试场景或测试版 Panel。
入口是 `PresentationDebugSceneController.PlayPreview(Preview)`，已有 public PlayXxx 方法可由按钮或代码直接调用。
新演出应扩展 Preview 枚举、PlayPreview 分支、快捷键与 builder 中的按钮 / 说明。
长剧本传入 `DialoguePanel.Play(DialogueScript, onFinished, onCancelled)`；调试场景中的演示数据与正式日程解耦。

缺失时：`Tools > Game Flow > Create Missing Presentation Debug Scene`。
更新生成布局：保存调试场景，再 `Tools > Game Flow > Rebuild Presentation Debug Scene`，确认后覆盖同一场景，保留 scene GUID。
Builder 在 `Assets/Editor/GameFlow/PresentationDebugSceneBuilder.cs`，不要修改框架包。

## 回归

Unity Test Runner PlayMode：`Spotlight.GameFlow.Tests.PresentationDebugSceneTests`。
覆盖实际共享场景层级、按钮绑定、11 类预览、QWER 键盘输入、日期结束 / 回调隔离、暂停 / 重播 / 退出清理。
截图测试输出 `output/presentation-debug-room.png` 与 `output/presentation-debug-dialogue.png`。
