# 加热与沸腾动画

读取 CoffeeShop 场景实体，确认 m.stove 使用 `Assets/Art/FreeCafe/Entities/machine.stove/default.png`，m.heater 使用 `Assets/Art/Gameplay/咖啡馆/Sliced/Entities/machine.heater/default.png`。前者虽位于旧目录，仍是当前炉台的实际引用。原图保持不变。

- `FreeCafeHeatingVisual` 只负责热源：独立炉台读取其 heatingVesselId 对应的容器，核对停靠位置且正在 Processing；银壶读取 Processing + selfHeating。火焰与暖光渐入，完成、拿走或中断时渐出。不用 Boiling 状态点火。
- `FreeCafeBoilingVisual` 独立读取容器的 Boiling 状态且有液体，显示蒸汽与轻微抖动。已覆盖所有七种可加热容器；炉台本身不冒沸腾蒸汽。替代 FreeEntityView 原先仅对银壶执行的沸腾抖动。
- 沸腾容器被拿走时蒸汽继续跟随容器，出口随容器倾斜，蒸汽仍向世界上方飘。沸腾动画不用于判断加热是否开启。
- 动画沿用现有实体、加工与库存状态，不推进计时、温度或液体。timeScale=0 或经营模拟暂停时动画时钟冻结。停用复位可见性及抖动位移；销毁释放各自网格。

火焰/暖光/蒸汽使用 `CafeThermalEffects.shader` 程序绘制，采用暖橙色、柔和米白色，没有新增图片，也不改变现有素材。共享材质为 Resources/FreeCafe/ThermalEffects；每个效果独立使用 MaterialPropertyBlock，无每帧材质创建。

Inspector 的 Burner UV / Flame Size UV / Glow Size UV 可调整炉火位置和大小；Steam Origin UV / Steam Size UV 控制蒸汽。UV 以实际精灵原图左下角为原点。

菜单 `Tools > Free Cafe > Apply Heating And Boiling Animation` 增量更新该场景引用的 Prefab 与已有实例，保存前备份到 output/heating-animation/backup-*，不重建场景布局。
