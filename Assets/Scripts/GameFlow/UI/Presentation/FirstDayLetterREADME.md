# 第一天前的叔叔来信

本文说明信件演出及信内建角，不包含开场对话或车窗转场。
后续已接入第一天咖啡馆中的阿黛尔引入对话，见 `FirstDayIntroductionREADME.md`；信件仍只负责自己的演出，不直接触发对话。

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
- `Play(Action onFinished)`：复位到信封阶段，使用 `FirstDayLetterDraft` 的硬编码互动信件；正式流程与预览不再注入 NCS 正文。
- `Play(FirstDayLetterContent, Action)`：保留显式纯文本注入，供独立演出测试或其他内容预览。
- `Stop()`：取消动效及完成回调，不进入第一天。
- `SkipLetter()`：跳过信件并触发一次完成回调；停止动画、逐字显示和输入。

## 玩家操作

1. 右下角「跳过」按钮在信封、展开和阅读阶段都可用；点击后立即结束信件并进入第一天日期演出。跳过不会跳过后续对话或经营流程。
2. 黑底上显示信封、封蜡和闪烁点击提示。
3. 点击信封，纸张从画面下方升起，同时信封淡出。
4. 展开结束后正文逐字出现，长信可在正文区域滚动阅读。
5. 红色文字可点击：输入姓名、选择出身、两段性格、学业及毕业特质。悬停变为 `D18C00`，选中项加粗并带下划线，空白处显示所选描述。
6. 正文全部出现后显示叔叔署名；姓名和五组选项全部完成后才可确认。
7. 点击署名时将姓名、出身、背景、属性奖励及学业专精提交到 `PlayerDetailsProfile`，随后淡出并进入头像选择。反复选择或重复提交不会累加奖励，读档沿用原有存档参与者。
8. 跳过时保留已填项，缺失姓名默认“咖啡馆馆长”，缺失出身默认“平民”，其余缺失项默认第一项，然后进入既有跳过流程。图中没有资金数值，本实现不写入金币。

除显式点击「跳过」外，重复点击不会重启展开、跳过正文或重复进入第一天。返回标题、读档或重新开始时，会取消旧面板的完成回调。
所有动效和打字使用非缩放时间。

## 如何修改

在 Unity 中打开该预制体，根节点 `FirstDayLetterPanel` 中可修改：

- 正文与选项：修改 `FirstDayLetterDraft.cs`，目前硬编码，不读取 Inspector 旧草稿或 NCS 正文。旧 `letter/signature` 字段保留以避免破坏已有序列化数据。
- 正文黑色 `000000`、选项红色 `B3000F`、悬停黄色 `D18C00`。
- 沿用“乐米曲奇方块体”，参考稿12pt换算为18个Canvas单位；仅署名中的“叔叔”为21单位，对应14pt。
- `Character Interval`：逐字间隔。
- `Open Seconds / Close Seconds`：纸张入场 / 结束淡出时长。
- `Paper Rise`：从下方进入的距离。

信封和纸张使用可编辑的原生 UI 节点构建，没有新增 AI 图像或上传项目素材。
预制体根 Canvas 的排序为 700，在 UIKit 挂载后启用 Override Sorting，覆盖场景经营 HUD。

菜单 `Tools > Game Flow > Create Missing First Day Letter Panel` 仅创建缺失的预制体，不覆盖你的视觉编辑，也不修改场景。

## 验证

菜单 `Tools > Game Flow > Apply Interactive First Day Letter` 更新正文排版、字体及姓名输入层，保留现有信封/纸张素材和其他节点。

PlayMode：`FirstDayLetterTests` 检查点击等待、纸张上升、逐字显示、署名确认、取消 / 复用 / 读档和第一天衔接。
`FirstDayLetterDraftTests` 检查选项属性、姓名清理、重复提交以及存档往返。
原 `DayFlowPresentationTests` 明确使用 `BeginNewSession(playLetter: false)` 继续独立验证日程链路。
