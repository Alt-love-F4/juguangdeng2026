# 研磨机动画与原料拾取素材

入口为现有 `Assets/Scenes/CoffeeShop.unity`，使用该场景原有输入、相机、实体及加工流程。

## 素材来源

先读取场景实体 `m.grinder` 与 `s.beans` 的 `FreeEntityView.body`，再沿 Prefab / Sprite GUID 定位：

- 研磨机原图：`Assets/Art/Gameplay/咖啡馆/Sliced/Entities/machine.grinder/default.png`。
- 豆袋参考：`Assets/Art/Gameplay/咖啡馆/Sliced/Entities/source.beans/default.png`。
- 研磨机部件：上述研磨机目录中的 `Animated/`。机身、摇柄、握把和关闭抽屉直接从当前原图拆分，原图保留。
- 新装粉抽屉：`Animated/drawer-filled.png`；同时绑定 `grounds` 与 `equipment.grinder.drawer`，确保拖出和落地保持相同外观。
- 新散豆：`Assets/Art/Gameplay/咖啡馆/Sliced/Entities/beans/handful.png`，参考当前豆袋与研磨机，经 PackyAPI 图像编辑生成后裁切去白底。

旧目录 `Assets/Art/FreeCafe/Entities/` 中的同名图片不作为当前研磨机或豆袋的来源。

## 状态与交互

`FreeCafeGrinderVisual` 只读取现有 `ProductionPhase`，不持有库存或推进加工：

- Empty / Loaded：抽屉关闭，摇柄停在原始位置。
- Processing：摇柄沿浅椭圆绕轴转动，木握把保持直立；现有点击加速期间提高动画转速。机身不再整机左右摇摆。
- Ready：装粉抽屉在约 0.28 秒内滑出，显示可见粉堆。
- 拖出：拾取预览从抽屉实际位置开始；研磨机上的装粉抽屉隐藏，留下空槽。预览仍不扣除库存。
- 取消 / 无效投放：沿用现有预览返回机制，原抽屉重新可见，加工结果保留。
- 有效投放：沿用现有一次性提交逻辑，研磨机恢复为空，落地实体继续显示同一装粉抽屉图。
- timeScale 为 0 时部件动画同步暂停，停用组件复位部件。

散豆拾取从当前豆袋精灵的袋口位置开始，显示约 0.14 秒的轻微缩放弹出；拖动与取消仍走原有逻辑，停用或重新绑定时取消动画并复位。

散豆宽度由 0.65 调整为 0.9；装粉抽屉对应的 grounds 规格由 0.65 × 0.65 调整为 1.35 × 0.8，与机器中抽屉的显示尺寸相近。拾取和落地使用同一规格，避免拿起后缩小。

## 重新绑定与验证

`Tools > Free Cafe > Apply Grinder Animation And Pickup Art` 增量更新当前场景引用的 ArtLibrary 和三个实体 Prefab，保留场景布局。修改前备份至 `output/grinder-polish/backup-*`。

`python Tools/FreeCafe/split_grinder_art.py` 重做当前原图拆分；可用 `--pickup-sheet <1536x1024 图片路径>` 处理这次生成的双素材图。掩模与支点以当前 485 × 730 原图为准，换源图后需重新检查。

PlayMode 回归：`Spotlight.FreeCafe.Tests.FreeCafeGrinderVisualPlayModeTests`，加载真实 CoffeeShop 场景，验证部件运动与暂停、抽屉取消/提交、素材一致性及散豆取消不扣库存。
