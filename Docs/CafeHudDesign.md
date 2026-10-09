# 咖啡馆玩家 HUD 重构说明

## 玩家 HUD 与 GM 的边界

玩家 HUD 只保留策划案中可见的经营信息：天数、王国币、声望、咖啡豆/牛奶库存、来客订单、当前提示、咖啡谱、返回大堂和前往制作台。

开始营业、结束营业、免费补货、调试加钱/声望、修改营业时间、生成顾客、实体 ID、跨楼层传送、刷新实体和快速存档统一放入 `~` GM 面板。GM 使用 `FreeCafeGMPanel` 现有全局实例，不新增第二套调试入口。

## 视觉规则

- 移除 `TopBlackBar` / `BottomBlackBar` 的玩家可见状态，不再由制作 HUD 恢复。
- 玩家面板使用暖奶油纸卡、咖啡棕描边、橄榄绿强调按钮和轻微纸卡阴影。
- 卡片间的透明区域不添加 Graphic，不拦截实体拖拽；卡片本身才接收 UI 射线。
- `CafeHudSurface` 是运行时可绘制的圆角纸卡，不依赖新的图片资源；后续有正式九宫格美术时可替换该表现组件。
- 摄像机清屏色改为暖棕色，防止背景图边界再次显示黑色上下带。

## 扩展约束

`FreeCafeHud` 仅管理玩家展示和玩家可用入口；`FreeCafeProductionHud` 仅管理制作订单/萃取/出品卡片；`FreeCafeGMPanel` 承担开发调试和测试传送。世界实体和 `CafeWorld` 仍然是唯一权威状态。

重建现有场景：`Tools > Free Cafe > Rebuild Player HUD`。该菜单实际复用并保存 `Assets/Scenes/CoffeeShop.unity`，不会创建新场景。
