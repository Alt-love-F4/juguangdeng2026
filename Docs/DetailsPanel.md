# DetailsPanel：PSD 布局与数据显示接口

正式资源位于 `Assets/Resources/Art/UIPrefab/DetailsPanel.prefab`。
三个页面从 `Assets/Art/Func/我的信息.prefab`、`人物信息.prefab`、`世界情况.prefab` 转换；源预制体和 PNG 保持不变。

## 布局转换

`DetailsPanelPrefabBuilder` 将 PSD 的 4096×2304 画布统一转换为 2048×1152 逻辑坐标。
每个 SpriteRenderer 图层保留名称、位置、原始纹理尺寸、枢轴、缩放和绘制顺序，转换为 RectTransform + Image。
原文字图层保留为禁用的 Image，文字由相应位置的 TMP 控件显示，避免旧文字与动态数据重叠。
原生装饰、地图、纸张、头像框、属性卡底和页签不单独拉伸；`DetailsCompositionFit` 对整幅布局等比适配 UIKit 的父画布。

- 页签视觉顺序为人物关系、我的信息、世界情况。接口枚举仍为 MyInfo / Relationships / World。
- 每页保留自己的选中页签图层。点击热区独立于装饰图片。
- 我的信息保留左侧照片、左下说明卡、右侧人物简介、横排四项属性和专精区域。
  属性使用 HorizontalLayoutGroup；专精使用三列 GridLayoutGroup + ScrollRect。无专精时显示“暂无专精”。
- 人物关系保留左侧头像与介绍、左边熟悉程度条、右侧七列五行格子。
  前三个格子留给特殊信息，最后一格保留翻页按钮，每页容纳 31 人；点击人物只更新左侧档案，不隐藏格子。
  未解锁人物显示问号并禁用点击；分页保留当前人物选择。介绍过长可滚动，特殊信息可悬停查看完整描述。
- 世界情况保留左侧选项和右侧说明、刻度与进度条。左侧使用 VerticalLayoutGroup + ScrollRect，切换选项更新右侧数据。
  `Assets/Art/Func/UGUI/WorldIdleButton.png` 是从原未选中按钮图层裁出的单个按钮，用于自动排列。
- 我的信息悬停说明使用 PSD 固定悬挂卡位置；其他页的说明卡跟随指针并限制在窗口内容范围内。

转换菜单：`Tools > Game Flow > Rebuild Details From PSD`。此菜单重新生成工作预制体，会覆盖工作预制体上的手工修改；源 PSD 导出资源不会被覆盖。
`Create Missing Details Panel` 仅补建缺失资源；`Bind Details Panel Components` 仅在旧版预制体尚未转换时执行迁移。
`Apply Formal Details Art` 是显式重新转换入口。

## 快照与接口

面板只消费 `DetailsPanelData` 快照，不查询 NCS、结算事件或写存档。
`DetailsGameDataAdapter` 是读取游戏状态并生成快照的既有适配边界。

```text
DetailsPanelData
├─ Profile : Name / Background / Introduction / Avatar
├─ Attributes : Kind / Name / Value / Description
├─ Specializations : Name / Description / Icon
├─ Relationships / World : Title / Heading / Description（空内容说明）
├─ Characters : Id / Name / Introduction / Avatar / Unlocked / Bars
├─ WorldBars : Key / Name / Value / Min / Max / Description / LowerDescription / UpperDescription
└─ IsPlaceholder
```

四项属性固定按照语言、勇气、智慧、知觉排列，值钳制为 0–20；未提供的属性补 0。
重复属性、非法枚举和列表里的 null 会抛出参数异常。输入集合在构造时复制并对外只读。
进度条根据 `(Value - Min) / (Max - Min)` 显示，支持非零下限；相等上下限显示空条。
人物的第一条 Bars 数据用于左侧熟悉程度显示，其余 Bars 同时显示在特殊信息区域。

```csharp
var data = new DetailsPanelData(
    profile: new DetailsProfileData("主角", "背景", "介绍", avatar),
    attributes: new[] { new DetailsAttributeData(DetailsAttributeKind.Language, 8, "影响表达与沟通") },
    specializations: new[] { new DetailsSpecializationData("咖啡调制", "专精说明", badgeIcon) },
    characters: new[] {
        new DetailsCharacterData("npc_1", "人物姓名", "人物介绍", portrait,
            unlocked: true, bars: new[] { new DetailsProgressData("familiarity", "熟悉程度", 5, 0, 10) })
    },
    worldBars: new[] { new DetailsProgressData("stability", "王国稳定", 7, 0, 10,
        "下限说明", "上限说明", "当前世界指标的描述") });

var panel = UIKit.OpenPanel<DetailsPanel>(UILevel.Pop, data: data);
panel.Refresh(nextSnapshot);                 // 完整刷新，保留当前页签
panel.SelectPage(DetailsPanel.Page.Relationships);
bool selected = panel.ShowCharacter("npc_1"); // 未解锁/不存在时返回 false
panel.ChangeRelationshipPage(1);
panel.ReturnToRelationships();               // 清除当前人物选择，格子始终保留
panel.SelectWorldProgress("stability");
var current = panel.CurrentData;
var key = panel.SelectedWorldProgressKey;
var page = panel.RelationshipPageIndex;
panel.RequestClose();
```

`IDetailsPanel` 统一提供数据、页签、人物、世界指标、分页、滚动和说明卡接口。
`SelectedCharacterId` / `SelectedWorldProgressKey` / `RelationshipPageIndex` / `RelationshipPageCount` 用于读取当前显示状态。
`ShowCharacter(id)` 会定位人物所在页；锁定或不存在的人物返回 false。`SetRelationshipPage(index)` 使用从 0 开始的索引，越界返回 false。
`ScrollTo(region, position)` 支持专精、人物介绍和世界选项，1 为顶部、0 为底部。
`ShowAttributeDescription(kind)` / `ShowSpecializationDescription(index)` 可供控制器和调试代码直接打开说明卡；`HideDescription()` 关闭。

可显式注入 `IDetailsPanelDataSource`，调用 `BindDataSource(source, refreshImmediately)` 和 `RefreshFromSource()` 读取快照；
数据源返回 null 时保留现有显示并返回 false。面板不自动轮询，数据变化由调用方主动刷新。
`UnbindDataSource()` 解除读取来源。输入源异常向调用方传播，不静默切换占位数据。

事件包括 `DataRefreshed`、`PageChanged`、`CharacterSelected`、`WorldProgressSelected`、`RelationshipPageChanged`、`Closed`。
按钮点击和外部调用走同一接口，重复选择不重复发送选择事件。刷新移除了当前选择时也会发送状态变化事件，选择清空时 ID/key 为 null。
关闭时清理数据源与事件订阅，缓存重开需重新绑定。`Closed` 在显示数据清理后发送一次。

打开/关闭沿用 GameFlowPanel 动画，页签和说明卡增加非缩放时间淡入；Inspector 可设置持续时间，0 表示立即显示。
快速切换、关闭和停用会取消动画并复位透明度。组件右键菜单 `Details` 提供样例快照、刷新注入源和打印显示状态入口，仅在 Play Mode 修改显示。

所有控件引用在编辑器序列化绑定，运行时不按节点名查找。
每次刷新隐藏旧悬停、清理过期人物/世界控件并复位专精滚动位置；关闭清理快照及图片引用。
专精模板保持隐藏，仅其克隆参与自动排列。缓存重开恢复我的信息页。

## 信件素材

`FirstDayLetterPanel` 使用 `Assets/Art/Func/信封` 的背景、信封出现、信封自动打开和底图。
正式素材 Image 不拦截点击，原信封点击、展开、逐字阅读、滚动、署名确认和跳过入口仍由原演出脚本维护。
每次 Play 恢复关闭信封图片，避免重播沿用打开状态。
菜单：`Tools > Game Flow > Apply Formal First Day Letter Art`。

## 验证

统一使用 `Assets/Scenes/PresentationDebugScene.unity`。Play Mode 按 M 打开详情、W 打开信件、H 收起说明。
未新增调试 Scene、Canvas 或输入系统。

`DetailsPanelTests` 覆盖页签/关闭/悬停、快照刷新、头像、专精增长与清空、暂停及缓存重开、
锁定人物/分页/选择后保留格子、世界选项滚动与刻度刷新，以及正式信件打开/阅读/署名/跳过/重播。
截图输出到 `output/details-panel/`；`details-reference-*.png` 使用对应参考图的显示样例，其他截图验证不同数据。
Unity 菜单 `Tools > Game Flow > Run Details Panel Regression` 输出 XML 到 `output/details-panel/test-results.xml`；
通过 bridge 单独运行时可输出 JSON 到 `output/details-panel/test-results.json`。

最新接口与效果回归：`DetailsPanelTests` 的 16 个用例全部通过（结果：`output/details-panel/display-api-tests.json`），
包含新增数据源/订阅生命周期、统一选择事件、跨页定位、非缩放时间淡入和主动滚动测试。
此前连同共享场景回归共运行 22 个用例，15 个通过、7 个失败。
失败用例均来自共享场景的对话演出，错误为缺少 `player/adele` 开场角色配置或找不到 `adele` / `eugene`；
相关对话/NCS 配置未在此次修改中调整。Unity 编译无错误。

接口调用示例：

```csharp
IDetailsPanel view = panel;
view.BindDataSource(mySource); // mySource implements ReadDetails()
view.CharacterSelected += id => Debug.Log($"Selected character: {id}");
view.ShowCharacter("npc_1");
view.SelectWorldProgress("stability");
view.ScrollTo(DetailsScrollRegion.Specializations, 1f);
view.ShowAttributeDescription(DetailsAttributeKind.Wisdom);
view.RefreshFromSource();
```
