# 咖啡经营：正交玻璃杯液面

大、小杯沿用原实体、碰撞框、拖拽、份数/毫升库存和存档。原精细杯子 Sprite 保留，但杯子的 `Art` Renderer 禁用，改由原 `Liquid` 扩展节点内的 World Space Canvas、Image 和现有 `GravityLiquidImage` 绘制开放杯口的三面玻璃框。没有新增场景、输入系统或第二份液体库存，不改拉花小游戏实现。

## 驱动关系

- `CafeWorld.LiquidSnapshot` 汇总 contents 内的实际毫升；快照增加 coffee/milk/water/matcha/cocoa/other 成分体积，不暴露可变库存集合。冷/热牛奶、水归为同一显示色；固体零毫升添加物不提高液面。
- `OrthographicGlassCupPresenter` 按各成分毫升比例混色，不再只看第一种原料；支持纯咖啡、牛奶、稀释咖啡、抹茶和可可。颜色是可配置的视觉混合，不是物理光谱/折射模拟。
- 液量目标为 `volumeMl / capacityMl`，范围 0–1；初次绑定/恢复立即同步，后续变化平滑过渡，清空立即归零。未启用制作的旧份数模式按杯容量映射显示，读取不会迁移或写入存档。
- 快照实际增量触发入杯波纹；重复刷新同一液量不会重复激发。直立接液也有波动，不需要受液杯倾斜。倒出减量可响应倾斜，平移/旋转继续使用 GravityLiquidImage 的重力与阻尼模拟。
- 实际倾倒后，FreeCafeRuntime 逐帧刷新源/目标的液体展示，不等待原有 0.2 秒文字刷新；首次绑定重置运动基线，避免实例定位时产生假晃动。
- 杯子把实体所属区域设为 `MotionSpace`，仅忽略区域整体平移；制作子场景滑入不再把液面甩斜，杯子自身位移与旋转仍会激发重力晃动。
- `AllowSpilling=false`：演出不能独立扣除液量，实际溢出仍由 CafeWorld 结算。静止后波纹自动衰减，暂停仍可回稳。
- 杯身尺寸按明确杯型设置，小杯 0.8×1、大杯 1×1.2（实体本地单位），保留现有 spawnScale。Canvas 排序跟随原杯体，悬停/选择/无效放置颜色同步到 Image；Image 不拦截射线，输入仍由原 BoxCollider2D 接收。
- 显式配置其他 `ICafeLiquidPresenter` 的实体不会被自动替换。

## 编辑与资源

已更新：

- `Assets/Prefabs/FreeCafe/FreeEntity_cup_small.prefab`
- `Assets/Prefabs/FreeCafe/FreeEntity_cup_large.prefab`

玻璃框使用现有 Shader 的程序化矩形杯壁，不需要生成或上传图片；保留原 PNG/Sprite 以便回退。杯子来源堆叠图、机器/壶美术不改变。

Inspector 中可调 `OrthographicGlassCupPresenter` 的 smallSize/largeSize、fillResponse、成分颜色和 glass。`GravityLiquidImage` 保持其液面波幅、阻尼等原参数。World Space 的 motionInfluence 设为 0.7，区别于像素 UI 默认的 0.003。

仅更新杯子资源：`Tools > Free Cafe > Update Orthographic Glass Cups`。更新前将两份原 Prefab 备份到 `output/glass-cups/backup/<时间>`，不改 CoffeeShop 场景布局。完整实体构建器也会维护同一套杯子结构。

## 回归

- 经营实体集成：加载既有 `CoffeeShop`，运行 `Spotlight.FreeCafe.Tests.FreeCafeGlassCupPlayModeTests`，检查尺寸、排序、输入、混色、液量上升、入杯波动和恢复。
- 快照业务：`Spotlight.FreeCafe.Tests.FreeCafeProductionTests`。
- 通用液面回归：`Spotlight.GameFlow.Tests.GravityLiquidImageTests`，沿用 `Assets/Scenes/PresentationDebugScene.unity`，不另造演出 Scene/Canvas。
- 日常查看：进入咖啡制作，选择大/小杯，分别倒入咖啡、水和牛奶。液面由真实入杯量驱动，停止倒入后观察回稳。

## 2026-10-12 本地验证

- 经营 EditMode 程序集：74 项通过、0 失败（含配方、液体快照、存档和 Prefab 回归）。
- 玻璃杯 PlayMode：6 项通过、0 失败，包含同帧运行时倾倒刷新与实际 CoffeeShop 相机截图。
- 通用 GravityLiquidImage PlayMode：18 项通过、0 失败，加载共享 PresentationDebugScene，包含直立接液波动和 MotionSpace 父级平移隔离。
- 实际截图：`output/glass-cups/small-coffee.png`、`large-latte.png`；裁切对比：`cups-preview.png`。未创建新演出测试场景。
- 原 `FreeCafeProductionPlayModeTests` 全套另跑 10 项，6 通过、4 失败：壶向咖啡壶倾倒的鼠标倾斜断言、真实拖壶测试未检测到液体、拉花 UV 指针位置、旧占位小游戏面板显示。没有修改这些输入/拉花/占位逻辑；未证明失败是否在本次修改前已存在，不能将该全套标记通过。详细结果在 `output/glass-cups/production-status.json`。
- Bridge 的 passedTests/failedTests 计数可能包含父 suite，以上使用实际测试项数，不把父 suite 当作独立用例。
- 未执行 Player 构建或移动设备验证。
