# UI 重力液体（2D URP）

## 快速使用

- 将 `Assets/Prefabs/UI/GravityLiquidImage.prefab` 拖入**现有 Canvas**，或选中一个现有 Canvas 子物体，使用 `GameObject > UI > Gravity Liquid Image`。
- 也可以直接在已有 `Image` 上添加 `GravityLiquidImage` 组件；它会创建自己的材质实例，无需手动赋材质。
- 建议 `Image.Type = Simple`、关闭 `Use Sprite Mesh`；默认无需 Sprite，白色矩形 Image 即可。Image Color 为白色；颜色 alpha 依然可以控制整体透明度。
- 调 `Fill Amount`（0～1）、液体/深层/液面颜色、`Wall Thickness` 与 `Wall Color`。
- Play Mode 下平移、晃动或绕 Z 旋转 RectTransform，液面会跟随重力保持世界水平，并通过弹簧阻尼回稳。
- `Allow Spilling` 默认开启：倾斜超过杯口容量时逐渐损失液量；关掉则保留液量，适用于纯视觉演出。
- 运行时 `CurrentFill` 是倒出后的实际液量；Inspector 的 `Fill Amount` 是初始/重新设定值。组件右键 `Refill and settle` 可补满到设置值并回稳。

```csharp
liquid.FillAmount = 0.6f;
liquid.AddImpulse(2f); // rad/s 液面角速度冲量
liquid.Refill();
liquid.ResetMotion(); // 瞬移、换父级后清除运动历史
```

咖啡经营的大、小杯通过 `OrthographicGlassCupPresenter` 消费实际体积与成分比例，复用本效果绘制正交玻璃框；详见 `Docs/FreeCafeGlassCups.md`。`ConfigureAppearance` 设置液体/深部/表面/杯壁颜色，`AddPourImpulse(tilt, strength)` 同时激发角速度与波纹（tilt=0 的直立接液也能产生波纹），`MotionInfluence` 可适配世界单位。展示层关闭自动 spilling，不反写业务库存。

## 实现与边界

- 一个 Image 同时绘制液体、液面高光、下/左/右三面杯壁；杯口开放，上方透明。
- 组件通过 `BaseMeshEffect` 将 Image 局部顶点位置写入 UV1，自动开启所在 Canvas 的 `TexCoord1` 通道（不会移除其他 UI 使用的通道），避免 Canvas 批处理将位置变为 Canvas 坐标后导致旋转/位移错误。Shader 使用保留的局部顶点坐标而非 Sprite UV 求容器位置，兼容 Sprite Atlas UV；源 Sprite alpha 始终裁切输出，RGB 仅在 `Texture Influence > 0` 时参与。
- Sprite 不是实体杯形碰撞体：物理容器始终是 Image 的矩形区域。若源图只有不透明杯壁、内部完全透明，将没有可见液体；应使用内部不透明的遮罩或不指定 Sprite。
- 支持常见的 Screen Space Overlay / Camera Canvas 和正面 World Space Canvas，URP Unlit。**不响应 2D Light**。
- 兼容 `Mask` stencil、`RectMask2D` 和 softness、CanvasGroup/Image alpha。
- 重力是世界 XY 方向（默认向下）。Screen Space Overlay 的世界单位通常是 UI 像素；`Motion Influence` 用于把位移加速度缩放到重力单位。World Space Canvas 可按场景尺度调高此值。
- `MotionSpace` 默认空，保持原来的世界位移采样；指定 Transform 时，采样位移减去该空间的位置，忽略其整体平移。适用于滑入/滑出的经营子场景，仍保持世界方向的重力和杯体自身运动响应；修改空间时会清除运动历史。
- 重力方向通过局部基向量变换，考虑父级 Z 旋转、非等比缩放和镜像。效果面向 2D XY 平面，不用于透视三维杯体或 X/Y 轴翻转。
- C# 模拟液面角度与阻尼、倾倒液量；Shader 绘制波浪。**不是 Navier–Stokes 流体模拟**，不模拟液滴、飞溅粒子、真实黏度、泡沫、透明折射或任意杯形碰撞。
- 静态平面通过矩形截面积求液面高度，维持液量；小振幅波浪为视觉项，不做逐波积分守恒。
- 每个组件私有材质，禁用/销毁时释放并恢复原材质。Resources 内的材质引用 Shader，避免只通过 Shader.Find 引用在构建中被剥离。
- `Use Unscaled Time` 默认开启，暂停 timeScale 后仍回稳；关闭后随游戏暂停。

## 参数起点

| 参数 | 默认值 | 作用 |
|---|---:|---|
| Fill Amount | 0.6 | 初始液量占内部面积比例 |
| Wall Thickness | 0.02 | 相对 Image 高度的杯壁厚度 |
| Motion Influence | 0.003 | 平移加速度敏感度（UI 像素单位） |
| Rotation Influence | 0.18 | 杯体转动激发的晃动 |
| Spring Frequency | 2.2 | 回稳弹簧频率 |
| Damping Ratio | 0.45 | 越大越快消除晃动 |
| Max Slosh Angle | 35° | 相对重力的最大动态偏角；不限制杯体倾角 |
| Max Wave Amplitude | 0.025 | 动态波浪相对高度 |
| Idle Wave Amplitude | 0 | 默认静止后液面完全平稳 |
| Spill Rate | 0.8 | 每秒最多减少的内部面积比例 |

## 共享场景验证

使用 `Tools > UI Effects > Preview Gravity Liquid in Shared Debug Scene`：打开项目现有 `Assets/Scenes/PresentationDebugScene.unity`，只在它原有 `DebugRoomCanvas` 内添加预制体，不另建 Scene、Canvas、输入系统，也不会自动保存场景。撤销即可移除预览。

自动测试类：`Spotlight.GameFlow.Tests.GravityLiquidImageTests`（PlayMode，加载共享场景）。涵盖面积求解、开放杯口、世界水平面、非等比缩放、暂停回稳、倾倒补液、材质隔离/恢复、Mask 更新及截图。

## 文件

- Shader：`Assets/Art/Shaders/GravityLiquidUI.shader`
- 默认材质：`Assets/Resources/UI/GravityLiquidUI.mat`
- 运行时：`Assets/Scripts/GameFlow/UI/Effects/GravityLiquidImage.cs`
- 面积求解：`Assets/Scripts/GameFlow/UI/Effects/LiquidContainerMath.cs`
- Editor 菜单：`Assets/Editor/GameFlow/GravityLiquidImageEditor.cs`
- 测试：`Assets/Scripts/GameFlow/UI/Presentation/Tests/GravityLiquidImageTests.cs`

## 本次验证结果

- 在当前 Unity Editor 运行 `Spotlight.GameFlow.Tests.GravityLiquidImageTests`：**16/16 Passed**。
- 实际渲染截图包含 25° 倾斜杯体；像素级检查液体顶端仍水平，避免只有 C# 数值正确而 Canvas 批处理后渲染错误。
- 截图与结果位于项目 `output/liquid-ui/shared-scene-preview.png`、`output/liquid-ui/test-results.json`（本地输出目录，不纳入版本控制）。
- 未执行 Player 构建或移动设备验证。
