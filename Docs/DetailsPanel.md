# 详情面板：组件绑定与展示接口

## 职责边界

- `DetailsPanel`：继承 `GameFlowPanel`，管理窗口、页签、关闭、悬停及展示刷新。
- `DetailsPanelData : IUIData`：面板自己的不可变展示快照，不依赖对象表、NCS、存档或真实业务模型。
- `IDetailsPanel`：面向调用方的显示接口，暴露 `CurrentData`、`CurrentPage`、`Refresh`、`SelectPage`、`RequestClose`。
- `DetailsAttributeBinding` / `DetailsSectionBinding`：序列化保存 UI 组件引用，不存业务数据。
- `DetailsSpecializationItem`：徽章的图标、名称和悬停组件引用。
- `DetailsHoverTarget`：接收当前快照的标题和描述，管理鼠标进入/离开。

当前默认内容仍全部为占位，不读取底层系统、不结算事件、不修改存档、不实现新游戏头像选择。

## 数据类

```text
DetailsPanelData
├─ Profile : DetailsProfileData
│  └─ Name / Background / Introduction / Avatar (Sprite)
├─ Attributes : IReadOnlyList<DetailsAttributeData>
│  └─ Kind / Name / Value / Description
├─ Specializations : IReadOnlyList<DetailsSpecializationData>
│  └─ Name / Description / Icon (Sprite)
├─ Relationships : DetailsSectionData
├─ World : DetailsSectionData
│  └─ Title / Heading / Description
└─ IsPlaceholder
```

四项属性固定按照语言、勇气、智慧、知觉排列；输入数值钳制为 0–20。
未提供的属性补 0，重复属性、非法枚举或列表中的 null 元素会明确抛出参数异常。
集合在构造时复制并对外只读，外部修改原列表不会影响已提交快照。
关系和世界当前只有文本展示接口，不提前定义未实现的业务数据结构。

## 打开与刷新

```csharp
var data = new DetailsPanelData(
    profile: new DetailsProfileData("主角", "开场形成的背景", "咖啡馆馆长介绍", avatar),
    attributes: new[]
    {
        new DetailsAttributeData(DetailsAttributeKind.Language, 8, "语言的作用"),
        new DetailsAttributeData(DetailsAttributeKind.Courage, 6, "勇气的作用"),
        new DetailsAttributeData(DetailsAttributeKind.Wisdom, 10, "智慧的作用"),
        new DetailsAttributeData(DetailsAttributeKind.Perception, 7, "知觉的作用")
    },
    specializations: new[]
    {
        new DetailsSpecializationData("咖啡调制", "专精的说明", badgeIcon)
    });

var panel = UIKit.OpenPanel<DetailsPanel>(UILevel.Pop, data: data);
IDetailsPanel display = panel;
display.Refresh(newData);  // 完整替换内容，不改变当前页签；不接受 null。
display.SelectPage(DetailsPanel.Page.MyInfo);
display.RequestClose();
```

- 不传打开数据：`OnOpen` 使用 `DetailsPanelData.CreatePlaceholder`，仍显示原占位样例。
- `OnInit`：检查并保持 Prefab 引用，绑定一次按钮监听。
- `OnOpen`：接收本轮快照，刷新并恢复“我的信息”页。
- `Refresh`：统一刷新简介、头像、属性数值/条形进度/悬停、专精和另两页文本。
- 有头像时显示头像 Image；没有头像时显示原中性剪影占位。
- 专精使用原六个徽章视图，可按数据数量增加；超过可见区域可以滚动。空列表显示“暂无专精”。
- 每次刷新清理旧悬停、隐藏多余徽章并将专精滚动位置复位。
- `OnClose`：释放本轮快照及头像/专精的 Sprite 引用；重开不残留上一轮数据。
- `OnBeforeDestroy`：解除按钮监听。没有新增业务事件订阅、资源加载器或存档写入。

## Prefab 引用关系

`Assets/Resources/Art/UIPrefab/DetailsPanel.prefab` 的原窗口、按钮、文字、图标、悬停组件全部保留。
运行时通过序列化字段访问，不使用 `Transform.Find` 搜索或重新创建窗口。
额外新增头像 Image 和专精滚动容器；原徽章对象只移动到滚动 Content 内，原组件引用仍保持。

编辑器菜单：

- `Tools > Game Flow > Create Missing Details Panel`：只创建缺失资源。
- `Tools > Game Flow > Bind Details Panel Components`：在已有 Prefab 上补齐新增组件与引用，不重建原窗口。
- `Tools > Game Flow > Run Details Panel Regression`：补齐绑定、更新共享场景入口并运行回归。

如果手工删除或重命名预设节点，需要相应调整绑定；不会在运行时偷偷重建缺失组件。

## 预览与测试

唯一预览场景仍为 `Assets/Scenes/PresentationDebugScene.unity`，进入 Play Mode 按 **M**，
**H** 收起说明，**Backspace** 重播，**F9** 验证非缩放时间关闭。未新增测试 Scene、Canvas 或输入系统。

回归覆盖：原显示交互、UIKit 传入数据、快照复制和属性边界、组件引用保持、头像切换、
刷新后悬停更新、动态专精/空列表、缓存关闭与重开默认数据，以及共享场景原有入口。
结果：`output/details-panel/test-results.xml`；Game View 截图：`output/details-panel/*.png`。
