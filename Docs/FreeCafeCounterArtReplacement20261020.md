# 2026-10-20 台面美术替换

## 来源与覆盖

- 来源是 `Assets/Art/FreeCafe/Entities/台面道具/` 和 `台面道具 (1)/` 的本地 PNG 导出及其 PSD 画布信息；没有调用生图或上传。
- 两份 `桌面道具.png` 主体一致，像素差异集中在蜂蜜罐盖位置；两份 `重叠.png` 是相同的杯子前景遮挡层。它不是半杯饮料图，不能拿来表示半容量。
- 13 类实际换图：豆袋、可可盒、抹茶瓶、牛奶罐、蜂蜜罐、奶油、糖、大小杯、磨豆机、银色热水器、萃取上下壶。
- 完整杯用于单杯/杯堆顶层，重叠层用于下面的杯子；大小杯两个版本均保持原画布、PPU、pivot。
- 蜂蜜罐默认用 `(1)` 导出的真实闭盖图；从普通版取得开盖图，仅去除邻近奶油碎片。取料期间开盖，取消、提交、禁用后复位；只读取原有取料 owner，不扣库存、不新增玩法状态。
- 磨豆机身体、手柄、握把、抽屉重新从新图切出；出粉抽屉由新木抽屉及同套豆袋内部拼合，保留旧画布，继续共用 `grounds` / `equipment.grinder.drawer` 引用。
- 背景来自 `图层 12.png`：PSD 图层偏移 `(0,-301)`，裁切 PNG `(0,301,4096,2605)`，得到原始 4096×2304 视口。覆盖同一个 `SuppliedCounterBackdrop` 资源，不创建新场景/Canvas。
- 本批没有提供对应新图的水壶、拉花杯、搅拌器、炉具、原料拾取图等维持现有素材，不拿不相干的图强行顶替。

## 增量方式与重放

1. `python Tools/FreeCafe/replace_counter_art_20261020.py`：只写既有 `Assets/Art/Gameplay/咖啡馆/Sliced/` 下派生 PNG（加蜂蜜罐 open/closed 两张），保留原路径/GUID、既有场景布局和导入设置。保留所有源 PNG/PSD，修改前自动备份到 `output/counter-art-20261020/backup-*`。输出用临时文件原子替换，避免导入器读到半张图。
2. Unity 等待导入/编译完成，打开并保存既有 CoffeeShop 场景，运行 `Tools > Free Cafe > Apply Latest Counter Exports (2026-10-20)`。仅给现有蜂蜜罐接状态图及展示组件，不重建全部实体/布局；修改前备份库、罐 prefab 和场景。
3. 容器画布沿用原尺寸，现有液体遮罩已重新生成并检查预览，遮罩数据不变。不要为了重放该批美术调用旧版全场景重建菜单。

`output/counter-art-20261020/manifest.json` 记录源 PNG SHA-256、输出尺寸及裁切证据；contact-sheet.jpg 为道具核对图。源 PSD 不应加入普通进度提交。

## 回归入口

- EditMode：`FreeCafeLatestCounterArtTests`、`FreeCafeCounterPlacementTests`、`FreeCafeEntityPrefabTests`。
- PlayMode：`FreeCafeSourceJarPlayModeTests`、`FreeCafeCounterArtPlayModeTests`、`FreeCafeGrinderVisualPlayModeTests`、`FreeCafeCupStackTiltPlayModeTests`。
- 这些是实际 CoffeeShop 制作交互/资源回归，不涉及演出 Panel；演出 UI 回归仍使用项目既有 PresentationDebugScene。
- 结果记录见 `output/counter-art-20261020/verification.json`；画面证据见 `output/counter-art-validation/counter-art.png`。
