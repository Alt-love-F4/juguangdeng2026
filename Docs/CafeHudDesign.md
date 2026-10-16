# 咖啡馆玩家 HUD 重构说明

## 玩家 HUD 与 GM 的边界

玩家 HUD 只保留策划案中可见的经营信息：天数、王国币、声望、咖啡豆/牛奶库存、来客订单、当前提示、咖啡谱、返回大堂和前往制作台。

开始营业、结束营业、免费补货、调试加钱/声望、修改营业时间、生成顾客、实体 ID、跨楼层传送、刷新实体和快速存档统一放入 `~` GM 面板。GM 使用 `FreeCafeGMPanel` 现有全局实例，不新增第二套调试入口。

## 视觉规则

- 移除 `TopBlackBar` / `BottomBlackBar` 的玩家可见状态，不再由制作 HUD 恢复。
- 玩家面板使用暖奶油纸卡、咖啡棕描边、橄榄绿强调按钮和轻微纸卡阴影。
- 卡片间的透明区域不添加 Graphic，不拦截实体拖拽；卡片本身才接收 UI 射线。
- `CafeHudSurface` 是运行时可绘制的圆角纸卡，不依赖新的图片资源；后续有正式九宫格美术时可替换该表现组件。
- 摄像机清屏色改为暖棕色，防止背景图边界再次显示黑色上下带。

## 扩展约束

- 大厅 HUD 与制作 HUD 的显示节点、按钮和文字均为可选引用：可以移动、改名、隐藏或删除；缺失引用只停用对应显示或入口，不应阻断世界模拟、其他按钮绑定和场景切换。运行期间已销毁的 Unity 对象同样按缺失处理。
- 订单按钮与文字数组允许为空、长度不一致或包含空槽，不再要求固定三条订单和四个区域按钮。保留的按钮仍按 Inspector 中的数组索引对应订单或区域。
- 替换为新节点后，将新组件重新拖入对应 Inspector 字段；脚本不会按旧名称重建节点，也不会自动生成被删除按钮的替代入口。请保留经营运行时及 HUD 控制脚本，并为需要使用的功能留下至少一个有效入口。
- 隐藏或禁用 HUD 控制器时，会关闭其拥有的咖啡谱弹窗，避免界面已隐藏但仍锁住世界输入；重新启用不会重复注册按钮事件。
- `playerRoot` / `productionRoot` 的显隐仍由大厅、工作台状态控制；布局、图片和字体由场景编辑。咖啡谱独立 Prefab 的内部绑定仍需保持完整，这不属于大厅/制作 HUD 的可选节点约定。
- PlayMode 回归：`FreeCafeHudPlayModeTests` 中的 `RemovedPlayerHudWidgetsDoNotBlockStartupOrRemainingControls`、`OrderRowsCanBeSparseAndDifferInLengthWithoutLosingTheirClickBindings`、`RemovedProductionHudWidgetsKeepReturnAndWorldSimulationWorking` 和 `AllHudDisplayReferencesCanBeEmptyWithoutLockingGameplay`，均复用实际 `CoffeeShop` 场景。

`FreeCafeHud` 仅管理玩家展示和玩家可用入口；`FreeCafeProductionHud` 仅管理制作订单/萃取/出品卡片；`FreeCafeGMPanel` 承担开发调试和测试传送。世界实体和 `CafeWorld` 仍然是唯一权威状态。

重建现有场景：`Tools > Free Cafe > Rebuild Player HUD`。该菜单实际复用并保存 `Assets/Scenes/CoffeeShop.unity`，不会创建新场景。


## 工作台完整 HUD（2026-10-16）

`CoffeeShop` 的 `FreeCafeHUD/ProductionInterface` 复用原有 Overlay Canvas，以 1920×1080 为布局参考。根节点铺满画面；纸卡之外保持透明并允许操作世界实体。原 `台面ui` 导入预览包含烘焙的场景、样例字和教程遮罩，因此保留资源并停用预览节点，正式界面使用其物品详情纸框、选中/未选中按钮切片和 TMP 动态文字。

- 左上订单纸显示头像（缺失时使用现有 Unknown 占位图）、姓名、咖啡名称和满意分数，纸张堆叠随真实订单数量变化。左右按钮切换当前订单；“查看咖啡谱”打开当前订单对应的现有咖啡谱。
- 右上为咖啡谱与菜单入口。左下保留小杯/大杯选择，右下返回咖啡厅及清空所选容器。
- 底部“正式出品”只有杯子独立放入真实出餐口、存在当前订单且教程允许时可用。“废弃”走原有丢弃流程。场景铃铛与正式按钮共享 `FreeCafeRuntime.DeliverCup`。
- 所有容器悬停显示名称、真实内容物及用量、温度和容量。研磨器与加热设备加工时显示实际剩余时间换算的进度（当前场景使用自加热壶，也兼容独立炉台）；研磨速度读取已有升级规则。萃取开始后额外显示时间、热水量两个金/绿判定条。卡片没有 GraphicRaycaster，CanvasGroup 不拦截倒液和拖放。
- 出品产生一次只读回执：实际评分与糟糕/一般/良好/精品四档表情章、顾客/饮品、交易反馈、王国币与声望变化。重做和不满意离开也显示原流程反馈。确认关闭后恢复输入，避免重复交付。

拉花继续使用现有拉花杯拖至杯面上方的入口、小游戏事务和真实奶泡消耗。左侧图案列显示预览、实时拟合百分比和唯一最佳高亮；悬停展示寓意说明。王冠、爱心、X、A、郁金香、树叶暂作参考，**寓意文案均为占位**，可通过 `LatteArtPatternPreset.meaning` 配置。右下提交与删除重画，右上显示小杯、大杯、拉花杯俯视示意，木板背景复用现有柜台素材。工作台拉花杯沿用原实体与美术。

### 编辑入口与绑定

在 CoffeeShop 场景运行 `Tools > Free Cafe > Apply Workstation HUD` 可重建并保存；执行前会在 `output/workstation-hud-backup/` 备份场景。原 `Rebuild Player HUD` 也会复用该构建器。后续直接在场景调整 RectTransform 大小即可；再次重建会覆盖这些手动布局。

主要绑定在 `FreeCafeApp` 的 `FreeCafeProductionHud`；容器卡片样式在 `FreeCafeContainerPanels`（`cardWidth`、`paperFrame`、字体）。拉花图案、寓意与木板背景在现有 `FreeCafeLatteArtBench`。结算纸卡为 `FreeCafeHUD/DeliveryReceipt`，不引入新的世界状态、存档字段或演出场景。

新增行为回归 `FreeCafeWorkstationHudPlayModeTests`：真实出餐口门槛、按钮和铃铛共用结算、确认恢复输入、订单咖啡谱跳转、研磨进度及拖放不受卡片遮挡。现有拉花测试跟随提交/删除按钮的新位置，萃取入口测试在开始萃取后验证判定条。
