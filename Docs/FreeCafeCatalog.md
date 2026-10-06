# Free Cafe 配表说明

运行时唯一读取的经营配置是 `Assets/Resources/FreeCafe/catalog.json`。它是当前原型的可编辑配置表，不要把数值复制到 C#。

## 可配置内容

- `entities`：杯子容量、原料初始库存、每单位补货价格、实体尺寸。
- `operations`：设备、输入、输出、加工秒数、点击次数、点击间隔和 QTE 类型。
- `products`：餐品 ID、名称、杯型、基础售价、品质加价和配方材料。
- `recipes`：每个 `Recipe ID` 的步骤文案、操作类型和关联设备操作。制作提示 UI 直接读取此表。
- `economy`：客流、惩罚、升级、自动化和咖啡豆品质价格。
- `npcs` / `areas` / `layout`：顾客、区域和场景初始布局。

## 当前策划案已落地的数值

- 意式浓缩 50；玛奇朵 80；拿铁 80；抹茶 80；蜂蜜拿铁 100；热可可 100；摩卡 120。
- 咖啡豆基础/高级/特级单位价格 5/10/15。
- 牛奶、抹茶粉、蜂蜜和奶油顶每单位 5；可可每单位 10。
- 咖啡豆库存 5；牛奶、抹茶、可可、蜂蜜和奶油顶库存 8。
- 研磨点击 3 次、萃取 5 秒、牛奶加热 5 秒；这些都由 `operations` 表读取。

## 制作提示

场景底部的“制作提示”按钮打开配方面板。面板内容不是硬编码，而是遍历 `recipes`，并显示每一步关联的 `operations` 数值。新增餐品时，先添加 `products`，再添加对应 `recipes`，无需修改 UI 文本代码。

## 当前兼容策略

现有原型仍保留自由拖拽：配方可以按任意顺序组合，交付时由 `products.ingredients` 校验结果。新策划案中的严格步骤、确认上餐和拉花交互已通过 `recipes` 表建立数据入口和提示 UI；后续切换严格流程时，可在运行时增加“订单当前 RecipeStep”校验，不需要重新设计配置结构。

## 设备拖出产物

`entities.dragOutput` 控制设备是否允许拖出已完成的产物；当前仅研磨器启用。投入咖啡豆、完成点击研磨后，从研磨器拖出咖啡粉，可投到摩卡炉、杯子或放在场景空地。拖动的是产物预览，不是设备；取消、UI 区域和无效目标不清空设备。成功松手才提交原有产物，不额外消耗豆子或复制产物。

## 普通摄像机与实体输入

已移除 `FreeCafeViewport`。世界摄像机使用完整 Viewport Rect (0,0,1,1)，不再每帧修改 Camera.rect。场景生成器默认 Orthographic Size 为 7.5；已编辑场景保留手动设定的镜头大小，可以直接在 Camera Inspector 调整。上下黑边仍由 ScreenSpaceOverlay HUD 绘制，黑边和按钮拦截鼠标，非 UI 区域由实体 Collider2D 和摄像机 Physics2DRaycaster 接收点击/拖拽。

FreeCafeRuntime.World Camera 应指向实际启用且显示世界的摄像机。同一台摄像机上启用 Physics2DRaycaster，EventSystem 使用 InputSystemUIInputModule。拖拽优先使用 PointerEventData.pressEventCamera，并在拖拽期间固定该摄像机，通过 ScreenPointToRay 投射到实体平面；未提供事件摄像机的测试调用才使用 World Camera。无需额外视口服务。

拖拽偏移以鼠标按下的 `pressPosition` 为基准，不以超过拖动阈值后才触发的 BeginDrag 当前坐标为基准，避免实体始终偏离鼠标和正确接收目标。制作提示面板在 HUD Awake 默认关闭，主动打开后才阻挡实体输入。
