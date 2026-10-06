# 第一天前的叔叔来信

本轮只实现信件演出，不包含开场白、属性选择、车窗转场、阿黛尔对话或经营教学。

## 已接入的流程

新游戏的场景加载完成后：

`GameFlowBootstrap.StartGame` → `DayFlowController.BeginNewSession` → `FirstDayLetterPanel` → 玩家确认署名 → `DateChangePanel` → 第一天原有流程。

第一天日期为 **王国纪元 670年10月6日**。后续日期用实际日期加天数计算，跨月不会出现 10月32日。
每次开始新游戏播放一次；下一天和已过开场阶段的读档不会再次播放。
若在信件阶段采集存档，恢复后从信封重新阅读，不提前推进日期。

## 面板与接口

- Prefab：`Assets/Resources/Art/UIPrefab/FirstDayLetterPanel.prefab`
- 类：`FirstDayLetterPanel : UIPanel, IDayFlowPresentation`
- UIKit 负责资源加载与面板缓存；面板只负责自己的点击、逐字显示和动效。
- `Play(Action onFinished)`：复位到信封阶段并等待玩家点击。
- `Stop()`：取消动效及完成回调，不进入第一天。

## 玩家操作

1. 黑底上显示信封、封蜡和闪烁点击提示。
2. 点击信封，纸张从画面下方升起，同时信封淡出。
3. 展开结束后正文逐字出现，长信可在正文区域滚动阅读。
4. 正文全部出现后，才显示可点击的叔叔署名。
5. 点击署名后信纸淡出，调用一次结束回调，进入第一天日期演出。

重复点击不会重启展开、跳过正文或重复进入第一天。返回标题、读档或重新开始时，会取消旧面板的完成回调。
所有动效和打字使用非缩放时间。

## 如何修改

在 Unity 中打开该预制体，根节点 `FirstDayLetterPanel` 中可修改：

- `Letter`：信件正文（当前是可替换的初版文案）。
- `Signature`：署名，不预设叔叔姓名。
- `Character Interval`：逐字间隔。
- `Open Seconds / Close Seconds`：纸张入场 / 结束淡出时长。
- `Paper Rise`：从下方进入的距离。

信封和纸张使用可编辑的原生 UI 节点构建，没有新增 AI 图像或上传项目素材。
预制体根 Canvas 的排序为 700，在 UIKit 挂载后启用 Override Sorting，覆盖场景经营 HUD。

菜单 `Tools > Game Flow > Create Missing First Day Letter Panel` 仅创建缺失的预制体，不覆盖你的视觉编辑，也不修改场景。

## 验证

PlayMode：`FirstDayLetterTests` 检查点击等待、纸张上升、逐字显示、署名确认、取消 / 复用 / 读档和第一天衔接。
原 `DayFlowPresentationTests` 明确使用 `BeginNewSession(playLetter: false)` 继续独立验证日程链路。
