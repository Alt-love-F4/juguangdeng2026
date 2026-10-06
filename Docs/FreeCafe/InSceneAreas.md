# 同一个 Unity 场景内的三层楼 / 四个小区域

## 当前生产场景

`Assets/Scenes/CoffeeShop.unity` 已配置，使用普通主摄像机、Physics2DRaycaster 和 Overlay HUD。无需 FreeCafeViewport，也不加载其他 Unity Scene。

```text
FreeCafeApp
  FreeCafeRuntime
  FreeCafeAreaManager
Main Camera
BuildingViews
  floor2                       (0, 40, 0)
    TopFloor                   区域根，局部 (0, 0, 0)
      CameraAnchor
      背景 / 白模 / 实体
  floor1                       (0, 0, 0)
    Cafe                       大厅，局部 (0, 0, 0)
      CameraAnchor
      背景 / 进入出餐台的入口 / 实体
    Counter                    出餐台，局部 (60, 0, 0)
      CameraAnchor
      背景 / 返回大厅的入口 / 实体
  floorB1                      (0, -40, 0)
    Basement                   区域根，局部 (0, 0, 0)
      CameraAnchor
      背景 / 白模 / 实体
HUD                            Screen Space - Overlay
```

一楼两块背景不能完全重叠，否则只移动摄像机仍会同时看到它们。当前将 Counter 放到大厅右侧；60 的横向距离和上下楼 40 的间距只是占位布局，不是玩法数值。保持区域范围不重叠即可调整这些 Transform。保留了当前地下室根的 0.97 缩放。

## Inspector 怎么配置

在 `FreeCafeApp` 上找到 `FreeCafeAreaManager`：

| 字段 | 配置 |
| --- | --- |
| World Camera | 实际显示世界并带 Physics2DRaycaster 的主摄像机 |
| Camera Offset | 默认 `(0, 0, -10)`，世界坐标偏移 |
| Areas / Cafe | First；Content Root = Cafe；Camera Anchor = Cafe/CameraAnchor |
| Areas / Counter | First；Content Root = Counter；Camera Anchor = Counter/CameraAnchor |
| Areas / TopFloor | Second；Content Root = TopFloor；Camera Anchor = TopFloor/CameraAnchor |
| Areas / Basement | Basement；Content Root = Basement；Camera Anchor = Basement/CameraAnchor |

镜头落点为 `CameraAnchor.position + cameraOffset`。锚点表示画面中心，**不要再把 CameraAnchor 的 Z 设置成 -10**，否则会与偏移重复。只调整 CameraAnchor 即可调整该区域构图；相机旋转、Orthographic Size 和 Viewport Rect 不由管理器改写。

`FreeCafeRuntime.areaRoots` 顺序仍为 Cafe、Counter、TopFloor、Basement，必须与管理器中的 Content Root 对应。运行时会校验缺失/重复的锚点和不一致的区域根。

当前是瞬间切换，没有镜头滑动或淡入淡出。修改锚点后重新进入对应区域即可看到新落点；管理器不每帧锁定镜头。

## 切换的职责划分

```text
楼层按钮 / 场景入口
  -> FreeCafeRuntime.Enter(目标区域)
  -> 检查配置，取消当前未提交的拖拽
  -> 按 CarrySelection 选项携带选中物品
  -> CafeWorld.SetArea，更新领域中的当前区域
  -> 同步实体视图与交互资格
  -> FreeCafeAreaManager.Focus
  -> 主摄像机移动到该区域的 CameraAnchor + Offset
```

外部按钮应调用 `runtime.Enter(AreaId.Counter)` 等入口，不直接调用管理器 Focus。Focus 只移动镜头，不更新可交互区域、携带物品或拖拽状态。

四个区域根不因导航而 SetActive(false)。离开区域后，设备领域任务继续 Tick，视图实例和内容保持不变。只有当前区域的实体碰撞体可交互；入口、点击、拖拽和接收目标也做区域校验。加工容器内部的材料仍使用原有隐藏规则，这不是楼层切换隐藏。

## 坐标与存档

- 配表 layout、实体 x/y、区域边界都基于区域根的局部坐标。
- 显示与屏幕坐标测试：`AreaToWorld` / `TransformPoint`。
- 拖拽落点提交：`WorldToArea` / `InverseTransformPoint`。
- 存档仍保存 area + 区域局部 x/y，而不是世界坐标。
- 读档会根据保存的当前 area 恢复镜头。

因此地下室平移到 Y=-40 或根缩放为 0.97，拖拽不会把楼层偏移当成越界，也不需要修改咖啡数值表。

## 未来怎么扩展

1. 调整现有画面：移动对应 CameraAnchor；调整楼层间距：移动 floor 根；调整一楼两块背景间距：移动 Cafe / Counter 根。
2. 背景仍用 SpriteRenderer，黑边、营业按钮和导航仍用 Overlay UI；不要把楼层内容塞进 HUD。
3. 新设备、NPC、词条仍走实体与领域规则，区域管理器不承接其玩法。
4. 镜头动画将来可放在管理器的表现层，取消与输入阻挡仍由 runtime 协调。
5. 当前 AreaId 和 runtime 的根数组仍是固定四区。新增第五区时需要扩展 AreaId、areas/layout 配置、根引用、HUD 导航与测试，不是仅添加一个空锚点即可。

## 编辑器迁移工具与验证

`Tools/Free Cafe/Configure In-Scene Areas` 用于旧层级迁移：退出 Play 后运行，先备份当前内容到 `.unity/recovery/area-layout-时间戳/`，然后复用现有楼层根、修正区域归属、添加镜头锚点和管理器并保存。已有独立的 Counter 偏移会保留；与大厅重叠时才默认偏移到右侧。

此工具会调整层级，不是单纯检测工具。当前生产场景已迁移，日常只需在 Inspector 调整，不必再次运行，也不应运行全量场景重建器覆盖手工美术。

本次 17 个 PlayMode 测试全部通过，其中新增覆盖：三层四区仅镜头切换且视图实例保持、地下室局部坐标拖拽与读档回镜头、跨区取消临时取料和非当前区输入拒绝。原有实际鼠标事件队列、营业按钮、研磨产物拖拽和杯子状态测试同时通过。结果位于 `.unity/test-results/in-scene-areas/playmode.json`。
