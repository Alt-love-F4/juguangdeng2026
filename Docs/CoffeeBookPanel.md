# CoffeeBookPanel

正式预制体：`Assets/Prefabs/FreeCafe/CoffeeBookPanel.prefab`。

- 原始 PSD 与 `Assets/Art/Gameplay/咖啡馆/咖啡谱ui.prefab` 保持不变。
- `CoffeeBookPanel/咖啡谱ui` 保留原十个图层的名称、父子关系和绘制顺序，图片使用 UGUI `Image`。
- `图层 1` 是 PSD 的整张场景参考背景，保留节点但不绘制，以免覆盖真实咖啡馆。
- `精粹咖啡` 中的烘焙文字图片不绘制，标题、配方、注意事项、页码、菜单和按钮均使用 `TextMeshProUGUI`。
- `CoffeeBookPanelView` 只读取现有 `FreeCafeRuntime.World`；沿用已解锁配方顺序，不新增配表或制作规则。正文支持滚动，空列表禁用选择与翻页。
- 大堂 `FreeCafeHud` 和制作区 `FreeCafeProductionHud` 各自持有同一 prefab 的实例；打开时仍沿用现有模态输入拦截与关闭逻辑。

## 编辑器入口

- `Tools > Free Cafe > Rebuild Coffee Book Panel`：由原 PSD 导出 prefab 重新生成 UGUI prefab，重新绑定并保存现有 `CoffeeShop` 中的两个入口。不新建 Scene、Canvas 或输入系统；若 CoffeeShop 已打开，保存前请先确认其中的其他改动。
- `Tools > Free Cafe > Preview Coffee Book In Presentation Debug Scene`：只在共享 `PresentationDebugScene` 的现有 `DebugRoomCanvas` 下实例化预览，支持 Undo，不自动保存调试场景。
- `Tools > Free Cafe > Capture Coffee Book Preview`：利用同一调试场景既有相机与 Canvas 渲染预览，保存到 `.unity/capture/coffee-book-preview.png`。临时摄像机目标与 Canvas 模式会在结束后恢复。
- `Tools > Free Cafe > Validate Coffee Book Panel`：检查原图层名称、顺序、UGUI/TMP 组件和序列化引用。
- `Tools > Free Cafe > Clear Coffee Book Preview`：仅移除自己的调试预览，支持 Undo。

## 回归

`Spotlight.FreeCafe.Tests.FreeCafeHudPlayModeTests` 包含 PSD 层级、UGUI/TMP 组件、配方选择、空列表、拉花文本与模态输入拦截检查。测试沿用 `CoffeeShop`，视觉预览使用共享 `PresentationDebugScene`。
