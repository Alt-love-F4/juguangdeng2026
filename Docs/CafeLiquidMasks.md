# 容器液体遮罩

现有 CoffeeShop 场景继续使用 GravityLiquidImage / OrthographicGlassCupPresenter，不改倾倒和库存逻辑。

## 遮罩与杯壁

GravityLiquidUI.shader 接受独立的 `_ContainerMask`。Alpha 表示可盛液区域，排除把手、杯脚、壶盖和炉台；遮罩通过 Image 的局部坐标采样，不依赖主图的图集 UV。未传入遮罩时保留原来的三面杯壁。

生成的遮罩在 R 通道保存有符号距离，距离单位为整张图片的高度，编码为 `0.5 + distance / (2 * distanceRange)`，默认 distanceRange = 0.125。`_WallThickness` 沿遮罩轮廓向内偏移，保持曲边、斜边一致的壁厚，而不是围绕中心缩放。外轮廓与内轮廓之间绘制杯壁。也支持普通 Alpha 遮罩：关闭距离场标记后，使用多方向采样内缩。

在实体的 FreeEntityView Inspector 中可调整 Liquid Container Mask、Liquid Mask Is Distance Field、Liquid Mask Distance Range 和 Liquid Wall Thickness。默认内缩为图片高度的 0.018；增大 Liquid Wall Thickness 可以加厚外框。普通手绘透明 PNG 不勾距离场标记。遮罩应与容器主图使用同一画布；液体层自动匹配主图的支点、尺寸、位置、旋转和翻转。

遮罩开启 Read/Write 后，液面高度按遮罩内缩后的面积求解，倾斜时继续保持世界重力方向。运行时只读取现有容量和体积快照，不改变业务体积或溢出规则。

## 当前素材来源与生成

先读取 CoffeeShop 场景的实体与 Sprite 引用，再读取该场景的 PrefabLibrary 中实际拾取的单杯素材。来源及轮廓保存在 `Tools/FreeCafe/liquid-mask-shapes.json`。

覆盖七种实际盛液容器：小杯、大杯、通用加热壶、咖啡壶、萃取壶、萃取滤杯、拉花缸。杯堆取出的实体继承对应单杯 Prefab 的遮罩。

- `python Tools/FreeCafe/generate_liquid_masks.py`：使用 Pillow / numpy / OpenCV 生成 Alpha + 距离场，保留原图。输出 `Assets/Art/Gameplay/咖啡馆/LiquidMasks/`。
- `Tools > Free Cafe > Apply Container Liquid Masks`：核对来源后绑定当前场景引用的七个实体 Prefab，并刷新已有场景容器。保留场景布局，保存前备份到 `output/liquid-masks/backup-*`。
- 导入需关闭 sRGB、Alpha Is Transparency、压缩、Mipmaps；开启 Read/Write，使用 Clamp / Bilinear。工具会设置这些选项，避免透明区域距离数据被擦掉。
- 静态轮廓示意：`output/liquid-masks/container-mask-preview.png`，蓝色为内缩后的液体区域，深色为杯壁；此图是素材对齐示意，不是运行时截图。
