# 咖啡店楼层与区域结构

## 实际场景

- 正式场景：`Assets/Scenes/CoffeeShop.unity`
- 测试场景：`Assets/Scenes/CoffeeUIEntitySandbox.unity`（正式场景副本 + 默认收起的调试浮层，不参与生产构建）
- 三层、四个视图：一楼营业区 / 一楼出餐台 / 顶楼 / 地下室。
- 顶楼目前使用“二楼背景.png”草图；代码枚举保持 SecondFloor，界面展示“顶楼”。
- 小区域不通过 LoadScene 切换，而在同一 Unity Scene 中切换可见组。经营模型和放置记录不会因切换销毁。

```text
BuildingRoot                         # 视觉与世界入口
  Floor_First
    CafeArea                         # 营业区背景 + 右侧出餐台入口
    ServingCounterArea               # 出餐台背景
  Floor_Second                       # 顶楼背景
  Floor_Basement                     # 地下室背景
InteractionRuntime                   # 常驻交互规则与放置数据
  CoffeeWorkspace (prefab)
    CafeEntities                     # NPC
    ServingCounterEntities
      ItemSources                    # 杯子堆、原料来源
      Stations                       # 制作机器
      PlacementSlots                 # 工作台与出餐口
      CarryableItems                 # 运行时生成的杯子与原料
CoffeeLocationRoot                   # 地点绑定、当前区域、切换事件
CafeHUD (prefab)                      # 常驻经营 UI，按区域显示制作按钮
LetterboxUI                          # 上下黑边，只占上下区域
LocationNavigationUI                 # 右下黑边内的地点按钮
Main Camera + CoffeeWorldViewport    # 中间区域 + Physics2DRaycaster
EventSystem
```

旧 CoffeeWorldRoot / GameplayCanvas 仅作为禁用的迁移参考保留，不承担新场景的显示或交互。

测试场景的右上黑边提供“调试面板 / Debug”按钮，展开后可使用原有 Debug JSON、Slider、Toggle 和诊断操作；收起时不会遮住右侧世界入口。

## 交互与职责

- CoffeeLocationController：Inspector 的 locations 绑定区域根和实体可见组；共享可见组取并集；切换前 CancelDrag。
- CoffeeLocationPortal：世界 Collider2D 的 Pointer 点击，右侧入口有高亮及标签。
- CoffeeLocationNavigationUI：按钮发出切换意图，不直接控制背景、库存或订单。
- CoffeeWorldRuntime：常驻规则、台账与存档；动态物品始终生成在出餐台 CarryableItems 下。
- CoffeeLocationHudUI：只切换 UI 显示和折叠管理面板，不控制经营数据。
- CoffeeWorldViewport：摄像机中间视口与背景等比适配；UI 黑边固定各 160 / 1080 高度。
- 暂停或制作小游戏打开时禁止切换，避免关闭未完成的小游戏。
- 制作辅助按钮只在出餐台显示；世界制作操作也校验当前区域。NPC 保留在营业区。
- 顶楼和地下室目前只有背景与导航，还没有新增玩法。

## 美术说明

原始背景草图不修改。三张楼层草图的黑边另裁出 `Assets/Art/Background/World` 副本，用于 SpriteRenderer。出餐台草图没有相同的黑边，直接使用原图。

目前背景仍为草图，出餐台草图上已画出的器具是背景像素，不会自动成为可交互实体。独立道具 prefab 仍显示在其上；正式美术应提供不含道具的背景 + 独立透明道具精灵，才能消除视觉上的重复。不同图片比例采用等比完整显示，不拉伸；可能留有侧边空余。

## 编辑与重建

日常改布局：编辑背景、实体 prefab、locations 绑定；无需反复运行生成器。

生成菜单（必须退出 Play Mode）：
1. 打开 CoffeeShop，`Tools > Spotlight > Build Cafe Workspace`：重建数据驱动实体和 HUD，并自动重建区域结构及其引用。
2. `Tools > Spotlight > Build Cafe Locations`：仅重建区域、背景、入口与导航。
3. `Tools > Spotlight > Build Coffee UI Entity Sandbox`：从正式场景复制测试场景并装配调试浮层。

**生成器不是无损场景合并器**：会替换生成的 Workspace/HUD 或 BuildingRoot 等根节点。手工添加内容先做版本提交/备份，或放入独立 prefab；不要把唯一的手工创作放在会被生成器替换的节点下。

## 后续扩展

- 印刷台可以作为地下室的实体可见组，保留现有 Interaction 的物品 / 来源 / 放置目标抽象，但实现自己的印刷规则适配器，不把咖啡配方判断塞进通用基类。
- 热点词条的数据定义与运行时实例分离：词条来源、拖出的词条实例、报纸版位分别对应 Source / Carryable / Placement；NPC 对话产生词条由业务模型负责。
- 当前区域注册为场景序列化绑定，不宣称已建立楼层 Excel 表。区域数量或解锁条件增多时，再抽出 LocationDefinition 配置与条件服务。
- 如未来需要位置存档，使用项目现有 GameSaveParticipantRegistry 注册独立地点记录；本次不改存档格式，进入场景默认一楼。
- 场景多到需要加载优化时，经营数据仍保持常驻，区域视图改为按需加载 prefab 或 additive scene；导航调用方无需直接依赖加载实现。

## 验证入口

PlayMode：CoffeeLocationPlayModeTests（入口/按钮射线、共享可见组、切换数据保留、拖拽取消、暂停与小游戏防护）。
现有 CafePlayModeTests 已调整为在出餐台测试制作，继续覆盖完整配方流程。

### 本次验证结果

通过 CLI 完成 3 个地点 PlayMode 测试、1 个完整七配方 PlayMode 测试、17 个 EditMode 模型测试，共 21 个唯一测试方法全部通过。CLI 原始计数包含 suite 节点及重复回调，不作为用例数量；去重报告保存在 `.unity/test-results/CoffeeLocationVerification.json`。另对重新生成的沙盒执行 Play 与按钮切换冒烟检查，检查时 Console 无 Error/Exception。

预览菜单：`Tools > Spotlight > Capture Cafe Preview`，Play Mode 时输出当前地点含 UI 的 PNG 到 `.unity/capture`。

环境提示：最后一次正式场景预览时，Console 另出现 `[Locus] Native broker bridge is required but did not start.`。这是编辑器工具桥的启动日志，本次未改动该插件；不能将其描述为整个项目 Console 始终零错误。上述 21 项测试的实际结果均为通过。
