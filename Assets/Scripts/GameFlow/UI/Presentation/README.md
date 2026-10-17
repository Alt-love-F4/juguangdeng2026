# 日程演出面板

现在每个日程页面有自己的 `XXPanel.cs` 和 `XXPanel.prefab`，不再由 `DayFlowController.CreateUi()` 在运行时拼出同一套界面。

## 最简单的使用方式

```csharp
var panel = UIKit.OpenPanel<CafeOpeningPanel>(
    UILevel.Pop, cachePolicy: PanelCachePolicy.Reusable);
IDayFlowPresentation presentation = panel;
presentation.Play(() =>
{
    UIKit.ClosePanel(panel);
    // 在这里进入下一阶段；演出自己不修改日程状态。
});
```

日期面板先调用 `SetDates(旧日期, 新日期)`，再调用接口 `Play()`。
`Stop()` 只中止自己的动效和完成回调，不推进日程。面板的关闭/停用/销毁也会自动 Stop。

## 脚本与预制体

所有预制体位于 `Assets/Resources/Art/UIPrefab/`，UIKit 按脚本类型名加载。

| 脚本 / 预制体 | 内容 | 结束方式 |
|---|---|---|
| DateChangePanel | 日期文字显现 | 动效与停留完成后自动结束 |
| PreBusinessPanel | 经营前准备 | 进入准备按钮 |
| CafeOpeningPanel | 开店背景展开 | 开始营业按钮 |
| BusinessEndPanel | 营业结束 / 结算入口 | 进入闲暇按钮 |
| CityMapPanel | 城区地图占位页面 | 前往城区按钮 |
| PreNightPanel | 返家 / 夜晚前过渡 | 返回住房按钮 |
| HomeNightPanel | 夜晚活动占位页面 | 结束夜晚按钮 |
| DayEndPanel | 一天结束文字显现 | 进入下一天按钮 |

营业操作复用 `CoffeeShop` 场景内的 `business.start` 营业立牌与 `FreeEntityView` 点击链路：未营业时点击开始营业，营业中显示“结束营业”，点击后通过 `RequestBusinessEnd()` 进入现有打烊流程。`BusinessControlsPanel` 已不再由日程打开。
地图和夜晚仍然是可等待确认的阶段页面，并未变成自动播完的短片；后续可以在各自预制体中加入实际交互。

## 自由修改每种表现

1. 在 Unity Project 中打开对应 `XXPanel.prefab`。
2. 修改 Background 的贴图、Content 中的布局、Title / Detail / Continue 的内容。
3. 在根节点组件中修改进场时长、退场时长，以及本类型独有的动效参数。
4. 修改该类的 `BuildEntrance(Sequence sequence)` 设计自己的进场动效；公用基类只保留播放、结束、取消的收尾约定。

`DayFlowPresentationPanel` 统一实现 `IDayFlowPresentation`，具体 XXPanel 通过继承共同遵守接口。
默认动效使用 DOTween 核心 API，以免依赖未被项目 UI asmdef 引用的 DOTween UI 模块。
动效使用非缩放时间；按钮在进场结束前不可点击，退场期间的重复点击也不会重复推进。

## 日程与生命周期

`DayFlowController` 用一个当前面板引用串联流程，并通过接口触发，不再创建 Canvas、按钮、文本或临时 Sprite。
UIKit 负责加载、资源租约与可复用缓存，不新增平行加载器或对象池。
返回标题时停止并关闭当前面板，旧完成回调失效。新游戏重置日期；读档按保存阶段恢复，不无条件播放新游戏开场。
演出和等待确认期间屏蔽经营输入及 Esc 暂停，营业期间释放。
各预制体携带独立 Canvas / GraphicRaycaster，排序 700，高于场景经营 HUD 的 600；在 UIKit 挂载后启用 Override Sorting。
营业操作面板在暂停或其他流程页面打开时隐藏并停止接收点击。

## 创建 / 验证

`Tools > Game Flow > Create Missing Day Flow Panels` 只创建缺失预制体，不覆盖已有预制体或场景。
已生成的预制体随项目提交，不依赖运行时生成或手动执行菜单。
PlayMode 测试程序集：`Spotlight.GameFlow.Presentation.Tests`。
