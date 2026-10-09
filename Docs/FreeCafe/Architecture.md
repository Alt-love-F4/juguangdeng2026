# FreeCafe：自由空间经营架构

## 目标

FreeCafe 是当前项目的新主架构。它不再把咖啡制作建模为“订单步骤 -> 指定按钮 -> 工作台槽位”，而是把整个咖啡店视为一个由实体、能力、空间和数据规则组成的可持续世界：

```text
玩家输入
  -> 空间中的实体交互
  -> 能力与配表规则校验
  -> 实体状态/内容变化
  -> 订单在交付时评价结果
```

当前正式场景是 `Assets/Scenes/CoffeeShop.unity`。三层楼、四个小区域都在这个 Unity Scene 内；FreeCafe 的领域 EditMode/PlayMode 测试位于 `Assets/Scripts/FreeCafe/Tests/`，PlayMode 测试直接加载此场景验证交互，不依赖已删除的独立 Sandbox 场景。

## 分层

### 1. Domain：纯 C# 世界状态

`Assets/Scripts/FreeCafe/Domain/`

- `CafeWorld`：唯一的运行时领域状态源。
- `CafeTypes`：实体、能力、设备任务、订单、快照和配表结构。
- 不依赖 `GameObject`、Unity 输入、UI 或预制体，因此可以在 EditMode 测试、存档恢复和未来服务器模拟中复用。

领域层只做规则，不负责表现：

- 实体唯一 ID 和空间坐标。
- 物品跨区域移动。
- 容器内容物。
- 设备独立任务和并行加工。
- 取出、移动、组合、加工、交付、丢弃、补货。
- 存档验证和 ID 分配。

### 2. Unity：表现与输入适配

`Assets/Scripts/FreeCafe/Unity/`

- `FreeCafeRuntime`：把领域状态驱动到场景，并接收拖拽、点击、暂停、存档等输入。
- `FreeEntityView`：一个实体预制体的通用视图。实体状态变化时更新同一个视图，不通过销毁/重建来表达普通状态变化。
- `FreeCafeHud`：黑边 UI、订单摘要、营业、补货、暂停、存档和楼层切换。
- `FreeCafePortal`：一楼营业区、出餐台、顶楼、地下室之间的入口。
- `FreeCafeAreaManager`：同一 Unity Scene 内的镜头锚点管理。三层楼、四个区域通过主摄像机位置切换，不加载 Scene、不停用楼层、不重建实体。
- 普通 Camera + Physics2DRaycaster：实体点击和拖拽；上下黑边由 Overlay UI 绘制。旧视口组件已删除。

### 3. Data：配表驱动

`Assets/Resources/FreeCafe/catalog.json` 是玩法配表；`art-briefs.json` 是美术合同。

实体通过 `EntitySpec` 描述：

- `id` / `label` / `artId`
- `capabilities`
- `capacity` / `initialStock` / `refillCost`
- `width` / `height`
- 来源物品 `sourceItemId`

加工通过 `OperationSpec` 描述：

- 设备、输入、输出、耗时。

商品通过 `ProductSpec` 描述：

- 杯型、配料、价格。

因此新增一种材料、机器或商品时，优先新增配表和白模资产合同，而不是在控制器中增加 `if/else`。

## 实体和状态

### 实体不是状态机节点的替身

杯子、咖啡豆、设备、顾客都保留自己的实体 ID。普通变化直接修改实体：

- 杯子加入牛奶：`contents` 增加一份 `Portion`。
- 咖啡豆研磨完成：同一个实体的 `specId` 从 `beans` 变为 `grounds`。
- 设备运行：设备的 `task.phase` 变为 `Running`。
- 设备完成：`task.phase` 变为 `Ready`，玩家取走后回到空闲。

只有“真正生成了新的对象”时才生成新 ID，例如从来源堆取出一份咖啡豆。这样可以稳定处理拖拽、存档、引用和未来网络同步。

### 能力组合

`Capability` 是可组合的标签，不用深层继承树表达玩法：

- `Movable`：可以拖动。
- `Source`：可以取出物品。
- `Portion`：可以作为材料加入容器/设备。
- `Container`：可以接收材料。
- `Processor`：拥有独立加工任务。
- `Customer`：可以承接订单。
- `Delivery`：交付区域。
- `Discard`：废弃。

例如杯子是 `Movable + Container`，一份咖啡豆是 `Movable + Portion`，研磨器是 `Processor`。以后加入报纸、热点词条和印刷机时，复用相同的实体/能力/交互管线即可，不需要为每类物体重新写一套拖拽框架。

## 交互语义

- 普通拖拽：落到容器、设备、顾客或交付区时尝试执行组合。
- `Shift` 拖拽：只摆放，不执行组合。
- 拖拽到黑边 UI、越界或容量不足：拒绝，原物品和库存不改变。
- 加工中的输入不能移动；点击设备可在确认后中止，输入保留。
- 订单不锁定制作顺序；玩家可以先做库存、并行做多个杯子，然后在交付时评价最终内容。
- 跨区域携带是显式选项。关闭时切换区域不改变世界状态；开启时选中的物品跟随玩家移动到新区域。

空间仍然有意义：实体必须在同一区域才能直接组合，物理上不允许隔着楼层把杯子放进顾客手中。玩家应先把物品拖到顾客所在区域，或者使用一楼出餐区的交付实体。

## 同场景内的楼层与区域

楼层是空间分组，区域是交互范围与镜头目的地，两者不应混用。一楼大厅 `Cafe` 与出餐台 `Counter` 同属 `floor1`，但有独立区域根和镜头锚点；顶楼 `TopFloor` 属于 `floor2`，地下室 `Basement` 属于 `floorB1`。

切换入口统一调用 `FreeCafeRuntime.Enter(AreaId)`：检查目标配置、取消未提交的拖拽、按携带选项移动选中物品、更新领域当前区域，再同步视图和镜头。`FreeCafeAreaManager.Focus` 只负责镜头，不代替领域区域切换；不要让 UI 直接调用它。

- 四个区域根保持激活，已有设备任务与实体状态继续存在。
- 非当前区域的实体碰撞体关闭，点击/拖拽/入口还会校验当前区域，避免跨楼误操作。
- 领域中的实体 `x/y` 和区域边界均为区域局部坐标。新游戏初始布局直接读取 Scene 中 Prefab 实例相对于区域根的位置；JSON `layout` 仅用于编辑器补缺失实例，不能覆盖已编辑的布局。读档位置仍以存档为准。输入提交用 `InverseTransformPoint`，世界位置用 `TransformPoint`，不把楼层偏移写入存档。
- 镜头仅在区域变化时跳转，不每帧重置位置，不修改 `Camera.rect`；黑边/HUD 继续使用 Overlay UI。
- 布局、Inspector 配置和扩展步骤见 `Docs/FreeCafe/InSceneAreas.md`。

## 后续拓展

### 人物与 NCS 扩展边界

人物仍为 `WorldEntity`，人物身份存储于 `person`（`CafePersonIdentity`），独立于座位编号和 Prefab 生命周期。当前普通客人只自动下单，点击只选择订单，不触发剧情或发放热点词条。

后续通过显式 `ICafeCustomerIdentitySource` 适配现有 NCS 的角色 key、姓名和美术；该接口只提供身份快照，不能推进剧情或结算事件。真正的对话和剧情调度继续接入现有 NCS / GameFlow，不在咖啡系统新增平行剧本字段。旧 `NpcSpec` / `npcId` 保留兼容，但普通到店生成不再自动套用示例 NPC。

专用实体 Prefab、大厅营业立牌、人物视图锚点与旧存档补齐规则详见 `Docs/FreeCafe/EntityPrefabs.md`。

### 地下室印刷

建议继续复用实体能力：

- 热点词条：`Movable + Portion + Topic`（未来可扩展能力标志）。
- 报纸版面：`Container + DocumentSurface`。
- 印刷机：`Processor`。
- 印刷成品：新的可交付实体。

拖动词条到报纸版面时由领域规则校验版面容量、栏目位置和组合结果；印刷机用独立 `ProcessingTask`。不需要为报纸再创建“工作台槽位系统”。

### 更自由的空间玩法

后续可以增加：

- 碰撞/遮挡/可达区域规则，但应作为独立 `PlacementPolicy`，不要把槽位重新写回实体核心。
- 物品堆叠、数量拆分、合并和批量拖动。
- 多人或 NPC 自动搬运：调用和玩家相同的 `Move` / `Interact` 命令。
- 事件记录和回放：记录领域命令，而不是记录 GameObject 状态。

## 美术资产合同

所有白模和提示词集中保存在 `CafeArtLibrary`，通过玩法配表的 `artId` 关联；场景和 Prefab 不挂载额外的美术需求标记组件。规范文件：

- `Docs/FreeCafe/ArtGenerationBriefs.md`
- `Assets/Resources/FreeCafe/art-briefs.json`
- `Assets/Resources/FreeCafe/ArtLibrary.asset`
- `Assets/Art/FreeCafe/Whitebox/`

生成正式美术时保持同一 asset ID、画布尺寸、pivot 和主体轮廓，只替换 `ArtLibrary` 的 `variants`。顶楼、地下室已经预留未来词条、报纸和印刷机白模，不要先把未来系统硬编码进咖啡制作。

## 旧框架策略

旧版 `Gameplay` / `Interaction` 咖啡工作流已经从项目中清理，不再参与 FreeCafe 主场景。当前新功能应只依赖 `Spotlight.FreeCafe.Domain`、`Spotlight.FreeCafe.Unity` 和共享的 `Spotlight.GameFlow` 边界，不要重新引入旧控制器或旧工作台系统。
