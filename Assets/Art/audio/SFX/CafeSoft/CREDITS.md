# 柔和咖啡交互音效

## 统一调音

打开 **Tools > Audio > Game Audio**，搜索 `cafe.`。配置资产是 `Assets/Resources/GameAudioCatalog.asset`。

- 每个声音可配置开关、素材、音量、音高、淡入淡出、冷却与同时发声数量。
- 顶部 **经营交互动态音效 / cafeDynamics** 控制倾倒流量对应的音量/音高范围、接收容器液位的音高增量、加热动态范围、豆子拖动轻响的距离及冷却。
- 用户设置里的总音量、音效音量与静音继续生效，不另建独立音量系统。
- 默认小音量、带宽柔化，短声淡入淡出；持续声音接缝经过短交叉混合。主观听感仍需结合耳机/音箱与背景音乐试听微调。
- **Tools > Audio > Apply Soft Cafe Sounds** 是显式重置这批咖啡音效默认值的工具；会覆盖相关 `cafe.*` 条目调音，日常调音不要重复运行。不会重建配置资产或改动音乐/UI条目。

## 素材来源与许可

### Water sounds — Vinrax（CC BY 3.0）

- 原始作品：https://opengameart.org/content/water-sounds
- 作者主页：https://opengameart.org/users/vinrax
- 原始下载：https://opengameart.org/sites/default/files/water_sounds.wav
- 许可：https://creativecommons.org/licenses/by/3.0/
- 使用文件：`LiquidPourLoop.wav`、`LiquidDripLoop.wav`、`LiquidSplash.wav`、`MilkFoam.wav`。
- 修改：截取、单声道重采样、高低通柔化、峰值调整、短声淡入淡出；循环交叉混合。奶泡为水声拟音，不是真实蒸汽录音。

### Pouring coffee beans — Focus on Fun Games Company（CC BY 3.0）

- 原始作品：https://opengameart.org/content/pouring-coffee-beans
- 作者主页：https://opengameart.org/users/focusonfungames
- 原始下载：https://opengameart.org/sites/default/files/pourdust-noise-reduced.wav
- 许可：https://creativecommons.org/licenses/by/3.0/
- 使用文件：`BeansRustle.wav`、`BeansAdd.wav`、`PowderAdd.wav`。
- 修改：从作者降噪版本截取、单声道重采样、滤波柔化、峰值调整及淡入淡出。粉末为豆子声低通拟音。

### Impact Sounds / RPG Audio — Kenney（CC0）

- https://kenney.nl/assets/impact-sounds
- https://kenney.nl/assets/rpg-audio
- https://creativecommons.org/publicdomain/zero/1.0/
- `CupLift.wav` ← impactGlass_light_000.ogg；`CupSetDown.wav` ← impactWood_light_000.ogg；`SoftTouch.wav` ← impactSoft_medium_000.ogg；`ServiceBell.wav` ← impactBell_heavy_002.ogg。
- `GrinderTurn.wav` ← metalPot2.ogg；`MachineSwitch.wav` ← metalClick.ogg；`ClothPickup.wav` ← cloth2.ogg。
- 修改：截取（部分）、单声道重采样、滤波、峰值调整及淡入淡出。器皿/手摇研磨/顾客动作均为拟音，可在统一配置中替换。
- 原包许可见 `Kenney-Impact-License.txt` 与 `Kenney-RPG-License.txt`。

### 沿用项目已有素材

- `GrinderLoop.wav` 来自 `Assets/Art/audio/SFX/研磨旋转.mp3`。
- `HeatingLoop.wav` 来自 `Assets/Art/audio/SFX/水沸腾.mp3`。
- 修改：连续段截取、单声道重采样、柔化滤波、峰值调整、循环交叉混合。混合机复用降调研磨声作为柔和拟音。
- 这两份源素材原已存在于项目；本次未重新核实其原始外部授权，发布前应沿项目原始授权记录确认。

## 发布署名

CC BY 3.0 素材必须保留作者、作品链接、许可证链接及修改说明。随构建携带的 `Assets/StreamingAssets/AudioCredits.txt` 提供对应说明；发布到商店时，也建议在游戏内制作人员名单或发行说明中包含这些署名，不要将该文件删掉。

## 验收建议

取杯/叠杯 → 拖动豆子 → 投豆 → 启动并多次点击研磨 → 拖出粉末/投入滤杯 → 热水壶和炉子分别加热 → 不同角度倾倒 → 滴滤/拉花 → 清空/丢弃 → 按铃交付。核对小流量轻、大流量更明显；停止/空壶/满杯/取消/切区/暂停均不残留循环。先以背景音乐正常音量试听，再调整 `cafe.*` 条目，不必修改代码。
