# Coffee UI + Entity Sandbox

场景：`Assets/Scenes/CoffeeUIEntitySandbox.unity`

这个场景从 `CoffeeShop` 场景复制生成，保留完整的咖啡店实体，同时增加 `CoffeeUIDebugOverlay` 诊断面板。它不是正式玩法 UI，而是一个可以快速复现问题的测试台。

## UI 测试顺序

1. **开始营业**：验证 Button、控制器状态变更和 Toast 日志。
2. **交流 / 接单**：验证 NPC 交互和订单创建。
3. **自动取杯**：验证 `ItemSourceEntity -> CarryableEntity -> PlacementEntity` 的取杯链路。
4. **添加咖啡豆**：验证原料来源、库存扣减和杯子内容绑定。
5. **调用当前动作**：实际调用正式 `CoffeeGameplayPanel.grindButton.onClick`，用于验证“筛选咖啡豆 / 研磨 / 设备”等动作按钮。
6. **Dump Debug JSON**：将当前 UI、实体、订单、步骤和按钮状态写入 Unity Console，并同步到 `CoffeeUIDebugOverlay.DebugJson`。

## CLI 读取

```powershell
unity-cli.exe --host localhost --port 6400 --output json tool call get_component_values `
  --json '{"gameObjectName":"CoffeeUIDebugOverlay","componentType":"CoffeeUIDebugOverlay"}'
```

重点看：

- `DebugJson.actionButtonInteractable`
- `DebugJson.step`
- `DebugJson.hasActiveCup`
- `DebugJson.beanPopupActive`
- `DebugJson.entityCount`
- `DebugJson.itemCount`

CLI 点击示例：

```powershell
unity-cli.exe --host localhost --port 6400 --output json tool call click_ui_element `
  --json '{"elementPath":"/CoffeeUIDebugOverlay/DebugPanel/Buttons/ActionButton"}'
```

## 实体测试

场景左侧保留杯子来源、原料来源、三个工作台、研磨器、摩卡壶、牛奶加热器、抹茶研磨碗、可可壶和出餐口。实体的拖拽、悬停高亮、非法放置拒绝和运行时物品数量可以在 Debug JSON 中观察。

该场景默认不加入 Build Settings，避免测试入口进入正式发行包。

## 动作按钮预制体

筛豆按钮预制体：Assets/Resources/Interaction/UI/BeanSelectionButton.prefab`n
组件：Assets/Scripts/Gameplay/UI/BeanSelectionButton.cs。正式面板通过 CoffeeGameplayPanel.beanSelectionButton 订阅其 Clicked 事件，并由配方步骤统一更新文本与 interactable 状态。
