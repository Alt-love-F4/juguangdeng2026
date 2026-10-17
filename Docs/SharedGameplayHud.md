# 跨场景游戏抬头 UI

地图原有行动点、日期、详情、物品栏和待办入口已移至 `Assets/Resources/Art/UIPrefab/GameplayHudPanel.prefab`。`GameplayHudPanel` 通过现有 UIKit 管理，整个游戏会话复用一份实例，不依赖隐藏地图来提供 UI。

## 操作

- `T` 或待办按钮：展开/收起待办事项。
- `B` 或背包按钮：展开/收起物品栏；与待办互斥。
- `Tab` 或详情按钮：打开详情；详情内 `Tab`、`Esc` 或原关闭按钮关闭。
- `Esc`：先关闭展开的抽屉，避免同一按键再打开暂停菜单。

日期、行动点直接读取 `NarrativeRuntime.Actions`；待办继续订阅现有行动源；详情使用 `DetailsGameDataAdapter` 和已有 `DetailsPanel`。详情打开时取得 `PresentationPauseRegistry` 的暂停租约并屏蔽咖啡馆世界输入，关闭/切场景/销毁时释放。抽屉动画、刷新和快捷键在 `timeScale=0` 时仍工作。待办的开关保留已生成条目、展开项和滚动位置；数据或追踪/分类变化仍及时刷新。菜单、加载、正式演出/引导阻塞期间遵守现有流程门禁。

## 三种入口

1. 正式游戏：沿用 `GameFlowBootstrap`、`PlayerSessionCoordinator` 和正式 NCS 会话；共享抬头跨玩法场景保留。场景替换经营世界后重绑值提供者，避免详情读取离开的世界。
2. 直接运行 `Map` 后进入咖啡馆：`StandaloneGameplaySession` 复用正式会话绑定，默认创建仅提交一次；通过已有 `GameSaveParticipantRegistry` 交接经营数据。地图协程必须保持运行至 `allowSceneActivation=true`，只在完成回调隐藏地图。
3. 编辑器直接运行 `CoffeeShop`：自动创建共享抬头，使用场景的咖啡馆世界和现有 NCS/玩家初始化。没有正式流程时提供默认角色，不额外加载地图，也不启动开场、推进日期或重放剧情。已经显式注入的玩家会话优先。

正式流程接管时释放独立会话的绑定。UIKit EventSystem 作为跨场景输入所有者保留，场景输入系统按已有 guard 去重。打开正式常驻地图时，地图排序 650、共享抬头 660，详情仍使用原有弹窗层级。咖啡馆内共享抬头缩放为 75% 并上移，顶部两侧给订单与货币留位；打开地图时恢复地图原有抬头布局。

## 编辑与生成

抬头布局编辑共享预制体；地图本身保留地图、缩放、地点和行动弹窗。
`Tools/GameFlow/Extract Shared Gameplay HUD` 可从旧地图提取原有布局，也可重新绑定共享预制体的日期/分段行动点引用。地图重建器会调用同一提取逻辑。根节点必须保持单位缩放、全屏锚点；抬头排在抽屉之后，保证抽屉展开时仍可点击切换按钮。原图、嵌套抽屉预制体和剧情配置不复制。

## 回归入口

`GameplayHudPlayModeTests` 加载真实 CoffeeShop、Map 和启动场景，覆盖三种入口、暂停时按钮和键盘、屏幕射线命中、资金及值提供者交接、单一输入系统、场景切换后实例复用。截图输出至 `Logs/GameplayHud`。

地图布局和初始化继续使用 `MapLayoutTests`、`MapUiRepairTests`、`MapInitializationPlayModeTests`、`MapLayoutPlayModeTests`；独立切换卡加载回归使用 `MapStandaloneEntryPlayModeTests`。演出 UI 的专门回归继续使用共享 `PresentationDebugScene.unity`。

## 咖啡馆大厅美术 HUD

大厅使用 `Assets/Art/Gameplay/咖啡馆/大厅ui.prefab` 导出的订单纸框、翻页箭头、货币图标和花纹，生成 `Assets/Prefabs/FreeCafe/CafeLobbyHud.prefab`，挂在 CoffeeShop 原有 `FreeCafeHUD/PlayerInterface` 下；继续使用原 Canvas 和 FreeCafeHud 数据绑定。左上展示订单，左右箭头逐项翻页，点击订单聚焦对应顾客；右上实时显示金币和声望。没有订单时显示空态并禁用翻页。

运行时大厅预制体不包含源 PSD 的整张背景、行动点、固定日期和重复快捷入口。场景实景和跨场景抬头继续由已有实现提供；源 PSD、导出图片和源美术预制体保持原样，可继续用于美术迭代。订单与货币收进顶部留白，底部旧导航、提示卡和工具卡已移除；制作台复用同一个 `CafeLobbyHud` 实例与订单纸框，隐藏行动点抬头、金币声望与楼层切换，只保留订单及当前配方、咖啡谱、返回大厅、菜单、按选中容器显示的清空操作。旧工作台订单纸堆、独立翻页和底部提示已移除，咖啡谱与结算弹窗继续由原 `FreeCafeProductionHud` 管理。大厅翻页可浏览后点击聚焦；工作台翻页直接沿用 `PageProductionOrder` 切换交付订单，显示页始终跟随真实聚焦订单。场景箭头仍是进入制作台的入口，退出制作台恢复大厅 UI。右下三个按钮从上到下为三楼宿舍、二楼会客室、一楼大堂，当前楼层显示橙色并禁用，其他楼层沿用 `CanEnterArea` / `Enter` 和已有镜头平移，暂停及引导门禁仍生效。历史存档枚举 `TopFloor` 对应二楼、`Basement` 对应三楼，不改枚举数值。

更新美术后使用 `Tools/Free Cafe/Rebuild Lobby HUD From PSD` 重新生成并绑定到 CoffeeShop；正式大厅 HUD 重建器也会应用该样式。仅保存 CoffeeShop，保留其他打开的场景。
