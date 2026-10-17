# 清水壶、废弃桶与教程重来

## 工作台操作

- 右上角加热炉旁的清水壶使用已有容器交互：左键拿起，对准接水容器，按住右键并上下移动鼠标控制倾倒。松开右键停止，放手后回到原位。
- 清水壶持续供应常温清水，倾倒不扣库存或金币。接收容器仍遵守自己的容量、温度混合、配方和溢出规则。
- 清水壶不接收原料，不可当作出餐杯、丢弃或直接加热；需要热水时先倒入加热炉。
- 左下角使用绿色搪瓷废弃桶，投放判定从 1 × 1.3 扩大为 1.9 × 1.8 区域单位。丢弃行为和费用继续使用原有业务规则。

## 教程

- 首次营业教程、黑咖啡操作练习、精粹咖啡操作练习使用同一个教程面板。
- 取水步骤改为使用右上角清水壶注水，按加热炉收到的实际水量判定。目标来自 `production.portionMl`（当前 200 ml），达到后结束本步并将水壶归位；不会等待无限水壶倒空。
- 教程仅在注水步骤限制流量，防止最后一帧超过目标；正常制作继续自由控制注水量。
- 面板上方常驻“重来”。制作中点击会取消拖拽/倾倒、恢复练习前的工作台，再从咖啡豆步骤开始。首次营业教学仍保留首位教学客人和教学标记，不直接解锁营业、不结算订单。
- 进入工作台前的箭头引导同样显示“重来”，此时重置当前箭头提示。

## 资源与维护

- 规格/默认布局：`Assets/Resources/FreeCafe/catalog.json`。
- 水壶保留 `source.water` / `s.water` 标识以兼容旧存档，能力改为 Movable + Container，`infiniteLiquidId=water`，不再是拖出单份原料的 Source。
- 液体内容仍归 `WorldEntity.contents` 管理，`PourLiquid` 在转移前后补充供水容器。原有杯壶、加热和液体模型继续共用。
- 新资源为 `Assets/Art/FreeCafe/Entities/container.water/default.png`、`Assets/Art/FreeCafe/Entities/waste.pail/default.png`，原水瓶/桶图片保留。
- 增量应用菜单：`Tools > Free Cafe > Apply Water Pitcher And Waste Pail`，只更新对应美术、两种实体预制体和 CoffeeShop 中两个实例。
- 新手教程预制体：`Assets/Resources/UI/FreeCafeTutorialPanel.prefab`。

## 回归入口

- `FreeCafeInfiniteWaterTests`：重复倾倒、溢出、不污染/删除/交付供水容器、旧库存快照恢复。
- `FreeCafeWaterAndTutorialPlayModeTests`：真实容器拖拽、扩大废弃桶范围、拿着水壶时重来、按接水量推进教程；直接加载 CoffeeShop。
- `FreeCafeHudPlayModeTests`：首次营业教程从箭头引导到实际按铃交付，包含重来后的首位客人和教学状态检查。
- 实机截图及测试日志在 `Logs/WaterAndWaste/`。

## 场景手调布局

- CoffeeShop 中实体根节点的位置/缩放由场景初始化；Body 子节点的尺寸、偏移，以及 Collider 的尺寸/偏移和标签位置在绑定及状态刷新时保留，不再按 catalog 默认尺寸覆盖。
- 旧进度只恢复供水壶、原料来源和废弃桶的内容/库存；它们的位置始终采用当前场景布局。普通可移动杯壶继续恢复存档位置。
- 调整完成后保存场景即可，日常布局调整不需要执行 Apply Water Pitcher And Waste Pail；该菜单会重新应用美术默认布局。

## 本轮验证

- 场景布局保留修复后：相关 PlayMode 32 项、定向 EditMode 122 项全部通过。
- 全量 EditMode 中配方/角色资料的 4 项失败在恢复修改前 catalog 后同样复现；记录见 Logs/WaterAndWaste/baseline-known-failures.json。
