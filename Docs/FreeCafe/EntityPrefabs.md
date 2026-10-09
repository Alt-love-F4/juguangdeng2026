# 实体 Prefab、营业入口与人物结构

## 在现有场景中使用

打开 `Assets/Scenes/CoffeeShop.unity`，进入 Play Mode：

1. 默认进入一楼大厅 `Cafe`；右下方有木质 **启动营业** 立牌。
2. 点击立牌开始营业；首批客人到店并自动生成订单。重复点击不会重复开店或生成第二批订单。
3. 点击客人切换当前订单，前往制作台制作饮品，再按原有交付逻辑出餐。
4. 打烊后立牌变为 **开始新一天**，点击使用现有 `StartDay` 规则开始下一天。

营业入口不再依赖 GM 面板。GM 调试入口仍保留；这次没有恢复 HUD 上已经隐藏的旧营业/存档调试按钮。

## 每种实体的独立资产

当前目录有 **35 种实体规格，对应 35 个独立 Prefab**；初始场景布局有 **18 个真实 Prefab 实例**。

- 注册表：`Assets/Resources/FreeCafe/EntityPrefabLibrary.asset`。
- 独立资产：`Assets/Prefabs/FreeCafe/FreeEntity_<specId>.prefab`（规格 id 的点替换为下划线）。
- 场景编辑容器：每个区域根下的 `AuthoredEntities`。
- 编辑器看到的实例在运行时按 `authoringEntityId` 接管，不是隐藏预览后再生成重复实体。
- `FreeEntity.prefab` 仅是编辑器结构模板；运行时缺失专用映射会明确报错，不静默回退。
- `FreeEntityView` 负责点击、拖拽和展示；数据、加工、身份、订单、营业状态仍由 `CafeWorld` 权威管理。

公共结构含 `Art`、`Label`、`FillStatus`、`FillBarBackground`、`FillBar`，以及 `Effects`、`Liquid`、`PourOrigin` 扩展锚点。

本轮只是补齐真实实体资产/视图结构，不等于完成所有设备动画或真实 2D 流体。水、糖、拉花入口仍可复用现有占位美术；未来可直接替换对应 Prefab 的部件或美术库变体。

## 营业立牌

专用 Prefab：`FreeEntity_business_start.prefab`，规格和初始实体 id 都是 `business.start`。

- 能力：`BusinessControl`，不能拖动；位置由大厅初始布局配置。
- 同一实体含点击碰撞体、木质支架/纸面几何、主标题与状态标题，不创建额外 Canvas 或输入系统。
- `FreeCafeRuntime.Click` 调用 `CafeWorld.ActivateBusinessControl`，验证实体能力与当前区域，再复用 `StartDay`。
- `FreeCafeBusinessSignView` 只展示准备/营业/打烊状态，没有第二份营业状态。
- 暂停、GM、配方弹窗及非当前区域仍阻止点击。
- 旧存档恢复时补齐缺失的这个固定入口，不重置设备、库存、已有位置和订单。

## 人物实体

人物仍然是 `WorldEntity`；`customer` 专用 Prefab 新增 `FreeCafePersonView`。

```text
FreeEntity_customer
├─ Art                         人物外观，可追加 Animator / 分层部件
├─ Label                       人物名字
├─ PersonAnchors
│  ├─ Portrait                 头像 / 人物展示锚点
│  ├─ Speech                   后续对话表现锚点
│  └─ Order
│     └─ OrderCaption          当前想要的饮品与耐心
├─ Effects / Liquid / PourOrigin
└─ 现有公共状态节点
```

`WorldEntity.person` 保存 `CafePersonIdentity`：

- `personId`：人物身份，不使用座位实体 id 作为身份。普通到店客人的身份由独立订单/访问 id 派生；座位 `customer_0` 被复用时，新客人有新身份。
- `displayName`：名字。
- `role`：预留 Customer / Staff / Player，当前只实现 Customer。
- `visualId`：美术库 key；缺失时回到规格美术，不影响订单规则。
- `narrativeCharacterId`：未来引用既有 NCS 角色的 key，目前普通客人为空。

订单依旧通过 `CustomerRequest.customerId` 引用世界实体；人物身份不持有订单副本。点击人物只聚焦订单，不启动对话、发词条或结算 NCS。普通客人不再自动套用旧的示例 `NpcSpec` 常客/记者身份；旧字段 `npcId` 与旧数据保留兼容，不把它误当作 NCS id。

### 后续 NCS 接入边界

- `ICafeCustomerIdentitySource` 可显式注入 `CafeWorld`；Unity 侧在 `FreeCafeRuntime.customerIdentitySource` 配置实现该接口的 MonoBehaviour。
- 身份入口只提供人物快照，默认生成匿名客人；读取身份与读档不会再次调用入口，也不会隐式启用 NCS。
- 后续适配器可以把既有 NCS 人物 key / 姓名 / 美术 key 转为身份。不新建平行剧本字段或剧情调度系统。
- 真实对话/剧情事件的触发和结算必须另由现有 NCS / GameFlow 边界接入，不能藏在身份生成或人物视图刷新中。
- 本轮不实现人物走路、店员 AI、对话、关系、剧情到访条件。

## 编辑器布局为准（无需重建）

- 直接在 `CoffeeShop` 的区域根下面移动实体 Prefab 实例，保存 Scene 并进入 Play Mode 即可。`AuthoredEntities` 是编辑分组，不是固定布局数据源；可以增加分组或将实体移到其他区域根。
- 新游戏在 `Awake` 绑定之前读取真实场景实例的 `authoringEntityId`、`prefabSpecId` 和相对于区域根的 `x/y`，替换内存中的初始布局；**不修改 JSON 资源，也不先按旧坐标刷回场景**。
- 只有实际存在的场景实体参与初始布局；从 Scene 删除的实体不会在启动时被 JSON 静默补回。重复身份或非法坐标明确报错。运行时会根据场景碰撞体（包含分组缩放）扩大内存中的摆放边界，不把超出旧白盒范围的物体挪回去；不修改配表边界或编辑器镜头。
- 读档优先恢复保存的位置、库存和加工状态，不用场景初始位置覆盖已有进度。测试布局应开始新游戏，而不是读取旧存档。
- 手动运行实体修复菜单也不再覆盖已有实例的位置、父级、缩放、文本或其他 Inspector override；JSON 坐标仅用于补充缺失的实例。
- 不直接移动 `Art` 图片代替实体根；移动带 `FreeEntityView` 的 Prefab 根，碰撞体、状态和标题才会一起移动。

## 动态取料 / 杯具 / 产物的尺寸

动态实体不再直接使用 Prefab 的 1 倍根缩放。`FreeCafeSpawnScale` 在启动时读取场景尺寸体系：已有同规格实例优先，然后按对应来源、加工设备的根缩放推导，最后使用该区域的 `AuthoredEntities` 分组缩放。每个动态实体仍使用自己的专用 Prefab 与规格大小，不把牛奶瓶/机器的整张图复制成产物。

拖出预览、松手后实际实体、出餐杯、设备产物、存档恢复重新生成的实体使用同一生成入口；跨区域携带与读档保留该规格的来源尺寸体系。此规则只影响显示与碰撞体的 Transform，不修改毫升、配方、库存或容量。

普通调整场景分组/来源根缩放后，重新进入 Play Mode 即可，不需要批量修改 Prefab 或在运行时硬编码一个放大倍数。

## 制作台标题归属

`ProductionWorkspaceLabels` 不再生成。制作说明存于实体 Prefab 的 `WorkspaceCaption` 子节点，`FreeEntityView.workspaceCaption` 引用该节点。显示层级跟随所属实体，且文本不参与射线交互。

- 研磨/滴滤/加热/搅拌 → 对应机器。
- 出餐托盘 → `delivery`。
- 杯具/原料/小料 → 对应杯具来源、豆来源、蜂蜜来源。
- 清水和方糖 → 各自来源标题，不再共享一个空间标题。
- 加热壶 ②/③ 预留说明 → 加热壶内的 `UpgradeHints`，暂不生成假设备实体。

迁移已有散落说明使用 `Tools > Free Cafe > Migrate Workspace Labels To Entity Prefabs`，保留已有文案/字体并确认迁移前后实体布局一致；无对应归属的自定义说明保留并警告，不静默删除。正常编辑/运行不需要重复迁移。

## 增量重建与回归

只有新增规格、缺少专用 Prefab 或需要补齐初始实例时运行（普通移动布局不需要）：

`Tools > Free Cafe > Rebuild Entity Prefabs And Layout`

菜单在编辑模式执行，拒绝覆盖未保存场景；备份到 `output/entity-prefab-validation/<时间戳>`。已有专用 Prefab/GUID、用户自定义子节点和组件保留，只增量补公共结构和更新 Prefab 基础外观；已有场景布局与 override 不覆盖。重跑会复用现有身份，不增加重复实例。

回归菜单：

- `Tools > Free Cafe > Verification > Run EditMode Tests`
- `Tools > Free Cafe > Verification > Run PlayMode Tests`

测试覆盖目录专用 Prefab、场景实例接管、取料预览、杯具存档、营业入口幂等/区域校验、人物身份/旧档迁移、真实 Pointer Raycast 点击、人物订单聚焦，以及旧制作/HUD 回归。
