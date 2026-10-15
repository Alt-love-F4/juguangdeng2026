# 咖啡馆 PSD 台面接入

## 使用

- 场景仍为 `Assets/Scenes/CoffeeShop.unity`，没有另建 Scene / Canvas / 输入系统。
- 已替换：制作区背景、豆袋、可可盒、茶叶罐（沿用抹茶来源）、牛乳罐、蜂蜜罐、奶油、方糖、研磨器、银色自加热壶、精粹壶上下段、大小杯。
- PSD 的「备用物体」不替代明确标注的正式器皿。原始 PSD、导出 PNG 和原始导出 Prefab 均保持不变。
- 缺少对应美术的咖啡粉、拉花杯、原有咖啡壶、炉台等保持原素材。没有增加占位图片或用容器图替代咖啡粉。

### 银色自加热壶

沿用 `m.heater` / `machine.heater` 身份（不创建第二套液体或温度系统）。原料拖入、容量检查、成分混合、倾倒、存档、温度判定均复用现有容器流程。

1. 加入水或其他原料。
2. 左键点击银色壶开始自加热，不需要放到炉台。
3. 使用既有 `heatSeconds`（当前 4 秒）和 `heatingTargetCelsius`（当前 100℃），温度连续上升。
   自加热模式通过既有 `ProductionEntityState.selfHeating` 随存档保存；原有炉台加热仍检查炉台位置，移开即停止。
4. 拿起会暂停加热，保留实际温度与内容；归位后可再次点击加热。
5. 空壶不能启动。右键点击的清空行为沿用项目既有处理。

### 归位

- 杯子可自由摆放，倾倒后也保留拿起时的位置，不弹回旧位置；原料仍沿用原有拖放规则。
- 其他容器松开鼠标后归位，并保留内容物。右键仍按住倾倒时不会提前归位；松开右键且左键也未按住才归位。
- 明确丢弃内容仍执行原规则；归位本身不丢弃、不生成实体、不消耗库存。
- 上下壶仍为两个独立实体。拖下方小炉/下壶时只移动下段：拿起瞬间解除组装，上方过滤器保留原位置。拖已组装的上方过滤器时整组移动，松手/取消后整组归位；不会把自己的下段当成新的投放目标。
- 已拆开的过滤器可独立拿起，松手停放在下壶旁，拖回下壶可重新组合，不自动重新组装。
- 上段/下段间距来自 `production.extractionFilterOffsetX/Y`，与裁切后的原图对齐。原有其他配置未提供这些值时使用旧默认值。

### 杯堆

- 完整杯口素材用于最上杯；开口的下杯素材用于下层，位置和渲染顺序分别设置，形成真实嵌套遮挡。
- 来源杯堆的库存是权威数量，可见数量为 `min(实际剩余数量, maxVisibleCups)`。两个杯堆各自有 **最大显示堆叠数**（默认 5）；0 个隐藏，1 个只有完整杯，可见 N 个就是 1 个完整顶杯 + N−1 个下层杯。超过上限只隐藏多余层，实际库存、数量文字和存档保留原数量。
- 上限 5、库存 6 时显示 5；取出一次剩 5 仍显示 5，再取出剩 4 才显示 4。拖拽预留也先按真实库存减一再应用上限，取消则还原；不会因为视觉上限而提前减少层数。
- 杯堆以底杯为固定锚点，层间距由各自 `FreeCafeCupStackVisual.cupSpacing` 设置。数量减少时只移除顶层，其余下层不重排。顶杯当帧下移一层并使用完整素材。
- 开始拖出顶杯时，拖拽预览从实际顶杯位置生成，杯堆立即少显示一层并晋升下一只；此时不扣真实库存。成功放置只扣一次，取消则恢复数量并立即收掉预览，避免复原后多出一只返回中的杯子。自由杯堆拿起顶杯时也立即切换下杯，取消可恢复。
- 同型空杯可继续归还到来源杯堆，不再受初始库存上限限制。不会丢弃有内容的杯子，不会重复归还同一实体。
- 自由摆放的同型空杯也能叠放；从顶层依次取出。每只杯保持自己的 ID，堆叠关系 `cupStackBelowId` 随世界快照保存；循环、异型、失效引用的存档会被拒绝。
- 堆叠杯先取出再加入原料或注入液体。不同型号及非空杯不合并，原有液体倾倒规则保留。

### 数量配表和编辑器预览

- 初始数量在 `Assets/Resources/FreeCafe/catalog.json` 的 `entities` 中修改：`source.cups.small.initialStock` 当前 **4**，`source.cups.large.initialStock` 当前 **5**。不要修改单杯 `cup.small/cup.large`，也不要修改水瓶来源的数量。
- 在 CoffeeShop 场景选中 `s.cups.small` 或 `s.cups.large`，找到 `FreeCafeCupStackVisual` 的 **杯子层间距**。小杯默认 **0.45**，大杯默认 **0.7**，是两组独立参数（相邻杯中心距离、以杯堆本地单位计）。场景采用的实例在运行时会直接接管，因此 Inspector 调整同时用于游戏。
- 组件支持 Edit Mode，修改层间距后无需进入 Play Mode 就会重画杯堆。右键组件菜单 **刷新初始杯堆预览** 可在修改 JSON 后手动刷新初始数量；Prefab 模式也能预览。仅刷新 Sprite、碰撞范围和数量文字，不初始化世界、不扣库存。
- 同一组件的 **最大显示堆叠数** 可分别控制两个杯堆（默认 5、最小 1），修改后也支持未运行预览。
- 保存场景或 Prefab 才会保留 Inspector 改动。初始数量用于新世界；载入已有存档时仍恢复其已存的剩余数量。
- 拖出的预览杯尚未生成 ID，不能参加堆叠关系查找。`CupAbove` 对无 ID 的预览直接返回空，确保大小杯在整个拖动过程中使用完整素材。

## 静态美术边界

这轮不实现新动画。精粹壶上段与下段是原始像素裁切，不是重绘；启用 `FreeCafeExtractorVisual.staticArtwork` 后不创建旧占位开盖/粉层/滴水动画。原有非 PSD 美术仍可使用旧表现。

## 维护入口

- 无损裁切：`python Tools/FreeCafe/slice_counter_art.py`（依赖 Pillow、numpy、opencv-python）。输出到 `Assets/Art/Gameplay/咖啡馆/Sliced`，`slices.json` 记录裁切矩形和世界坐标比例。
- Unity 菜单：`Tools > Free Cafe > Apply Supplied Counter PSD Art`。只接线提供的美术，不生成缺图、不重建 HUD。执行前需保存场景并退出 Play Mode；会备份当前场景、Prefab、美术库到 `output/counter-art-backup/<timestamp>`。
- Play Mode 快速定位制作台：`Tools > Free Cafe > Preview Supplied Counter (Play Mode)`。
- 修改演出 Panel 时仍遵循项目原有 `PresentationDebugScene` 约定；本次没有修改这些 Panel。

## 验证

- 域层：`FreeCafeCounterPlacementTests`（自加热/中止/连续温度/存读档/原位与内容保留/上下壶/空杯堆叠与存档循环校验）。
- PlayMode：`FreeCafeCounterArtPlayModeTests`（素材接线、上下段独立、静态表现、杯堆可见层、点击加热、松手归位与杯子自由摆放）。
- 相关旧测试调整了“杯堆已满禁止归还”及占位动画断言，保留液体、成分、萃取与身份断言。
- 报告和截图：`output/counter-art-validation`。

### 2026-10-15 回归记录

- 添加显示上限前的针对性 EditMode：26/26（`editmode-stack-extractor.json`）。
- 添加显示上限前的针对性 PlayMode：27/27（`playmode-stack-extractor.json`，杯堆/上下壶/位移动画/原萃取流程）。截图 `counter-art.png` 为实际 CoffeeShop 制作台，默认小杯 4 只、大杯 5 只。
- 调整初始库存与预览组件前的全量 EditMode：470 项中 468 通过、2 失败（`editmode-latest.json`）。失败为 `FreeCafeVisitTests.ProductionRemakeDoesNotEmitServedButFinalSuccessDoes`（订单状态期望 Served、实际 Leaving）和 `RuntimeCharacterTests.ObjectResourcesAndIndoorFallbackMatchDesign`（角色数期望 38、实际 39）。不是全量测试全绿；这两项在本次杯堆/拖动规则调整前的回归中也曾失败，未在这轮改动其他业务逻辑来绕过断言。
- 报告内 `passed/failed` 包括 NUnit suite 节点，应按 `totalTests` 与实际用例失败项统计，不能将 suite 节点作为额外用例。

- 显示上限改动已通过 Unity 编译，新增了 6→5→4、拖拽预留/取消、存档真实数量、独立上限和未运行预览的断言。本轮测试启动时先遇到未保存场景，随后编辑器进入 Play Mode，尚未执行这些新断言；上述通过数量来自添加显示上限之前的回归。
