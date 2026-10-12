# Presentation Debug Scene

项目统一的演出 UI 调试入口：`Assets/Scenes/PresentationDebugScene.unity`。
它独立于正式游戏流程，不启动 GameFlow，不推进日程，不写存档，不加入正式 Build Settings。

## 打开与运行

Unity 菜单 `Tools > Game Flow > Open Presentation Debug Scene`，进入 Play Mode，点击 Game 视图获得键盘焦点。
右侧按钮与快捷键等价；UI 看不到时先确认已进入 Play Mode、Game 视图不是 Scene 视图。

| 按键 | 实际演出 |
|---|---|
| Q | 新一天 / DateChangePanel |
| W | NCS 信件 / FirstDayLetterPanel（Shift+W：面板草稿） |
| E | NCS 对话 / DialoguePanel（Shift+E：原硬编码长文本） |
| C | 三人选项对话 / DialoguePanel（两个可用选项、一个灰色条件选项，选择后播放回应） |
| V | 阅读演出 / ReadingPanel（多页、长文本分页、配图、旁白/头像对话） |
| B | 通知演出 / NoticePanel（时间、配图、进度提示） |
| N | 视频过场 / CutScenePanel（无视频时显示占位，长按 Enter 跳过） |
| R | 经营前 / PreBusinessPanel |
| T | 开店 / CafeOpeningPanel |
| Y | 经营结束 / BusinessEndPanel |
| U | 城区地图 / CityMapPanel |
| I | 夜晚前 / PreNightPanel |
| O | 住房夜晚 / HomeNightPanel |
| P | 一天结束 / DayEndPanel |
| F | NCS 首次角色登场对话，默认 first_day_cafe |
| G | 独立特效试验台 |
| 1~0 | 黑影入场、受惊、轻/重震屏、白闪、黑场、换位、镜头推近/复位、淡出 |
| Space / Enter | 对话/阅读先补全文字，再翻页 / 下一句；通知确认关闭 |
| ← | 阅读返回上一段/文本页 |
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
新演出应扩展 Preview 枚举、PlayPreview 分支、快捷键与 builder 中的按钮 / 说明。当前三类剩余演出已接入 V/B/N；面板接口与数据契约在 `PresentationContent.cs`，字段保留 `pageId/pageCondKeys/pageBarConds/titleKey/imageKey/textKey/speakerKey/voiceKey/timeKey/barConds/videoPath` 供未来 NCS 适配。
长剧本传入 `DialoguePanel.Play(DialogueScript, onFinished, onCancelled)`；调试场景中的演示数据与正式日程解耦。

缺失时：`Tools > Game Flow > Create Missing Presentation Debug Scene`。
更新生成布局：保存调试场景，再 `Tools > Game Flow > Rebuild Presentation Debug Scene`，确认后覆盖同一场景，保留 scene GUID。
Builder 在 `Assets/Editor/GameFlow/PresentationDebugSceneBuilder.cs`，不要修改框架包。
仅更新 V/B/N 入口可用 `Tools > Game Flow > Update Reading Notice CutScene Debug Entries`，保留同一场景的其他对象。
三个 Prefab 缺失时用 `Tools > Game Flow > Create Missing Reading Notice CutScene Assets`（只生成缺失项，不覆盖美术修改）。

## 回归

Unity Test Runner PlayMode：`Spotlight.GameFlow.Tests.PresentationDebugSceneTests`。
覆盖实际共享场景层级、按钮绑定、共享预览入口、日期结束 / 回调隔离、暂停 / 重播 / 退出清理。新增类型的轻量用例为 `RemainingContentPanelsSmoke`：打开三个真实 Prefab、阅读前进/返回/取消、通知确认、过场长按跳过和释放重置。视频只验证调试占位和跳过接口，不把占位当作正式视频资源；没有运行完整回归。
截图测试同步输出 `output/presentation-debug-room.png` 与 `output/presentation-debug-dialogue.png`。

## 特效试验台

G 与 1~0 共用实际 DialoguePanel；没有对话时数字键自动打开试验台。角色 ID、效果 ID 和镜头/黑场入口仍留给后续 NCS 原子调度适配；本轮只接对话/信件内容。
资源参数集中在 PresentationEffectLibrary.asset，详见 `Docs/PresentationEffects.md`。

## NCS 内容调试

W/E/F 现在读取指定 NCS 内容，默认对话 ID `first_day_cafe`；信件使用 `opening.letter.body` / `opening.letter.signature`。
在场景中 `PresentationDebugSceneController` 的 NCS content selection 字段可改 ID/key，不需要新建场景。
有已初始化运行时则使用当前配置和文本 resolver；未启用 NCS 时直接读取已有 Luban 数据，不创建 NCS Runtime，也不推进事件和存档。
Shift+W / Shift+E 和公共 `PlayHardcodedLetter()` / `PlayHardcodedDialogue()` 保留原测试入口。
详情见 `Docs/NcsOpeningPresentation.md`；轻量回归为 `NcsOpeningDataTests` 和 `NcsOpeningPresentationSmokeTests`。

## 选项演出测试

按 `C` 或点击右侧「C 三人选项对话」按钮。用空格 / Enter / 继续按钮推进八句台词，或点击带底图的 SKIP，以每页约 0.1 秒快速逐条播放；到选项处自动停止，不会代选。选择后的回应恢复正常打字。最后一句全部显示后自动弹出选项。
- 「支持他的计划」「提醒他保持谨慎」可以点击，短暂显示已选择反馈，然后播放对应回应台词。
- 「询问只有线人知道的情报」显示灰色禁用态和「需要更多情报」提示，不接受选择。
- 选项打开时空格 / Enter 不会代选；Esc 取消，Backspace 从头重播；F9 暂停仍可选择并播放回应。
- 数据样例在 `DialoguePreviewDialogue.CreateOptions()`；公共入口是 `PresentationDebugSceneController.PlayDialogueOptions()` / `Preview.HardcodedOptions`。
- `DialogueScript` 接受 `options` / `optionPrompt` 快照；Panel 提供 `IsChoosing` / `SelectedOptionId` / `SelectOption(id)`。选项 UI 支持列表滚动。
- 这是单个脚本结尾的单选演出预览，不是完整 NCS 页/分支系统；`resultKey` 仅保留为数据，不执行 NCS 结果，不触发正式事件或写入存档。
- 按钮缺失时保存共享调试场景，再执行 `Tools > Game Flow > Update Dialogue Options Debug Entry`，仅补充入口和调整快捷栏布局，不重建场景或复制 Prefab。

## 三角色显示与朝向

C 样例同时展示店主（左）、街区线人（中）、印刷工（右），中间角色参与台词与选项回应。第三人暂时复用店主图片；独立资源位置为 `Assets/Resources/Dialogue/DialoguePreview.asset` 的 Center Portrait。
`DialoguePanel.prefab` 已追加中间角色节点，未重建左右原有节点。三人时使用并排布局；双人剧本和重播会恢复美术原有的左右布局。所有立绘已取消水平镜像，入场、换位与角色替换都保持原图朝向。
