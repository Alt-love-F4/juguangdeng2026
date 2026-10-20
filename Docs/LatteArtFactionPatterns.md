# 拉花工作台改版与势力徽记图案（2026-10-20）

## 画面

- 入口与流程不变：`FreeCafeMiniGameGateway` → `FreeCafeLatteArtBench.Begin`，液面仍是 CoffeeShop 场景里现有的 `LatteArtBench/Plane`（`LatteArtPrototype`），不新建场景或 Canvas 体系。
- 修复圆形溢出：原 `Background` SpriteRenderer 引用的底图资源已丢失，旧 `FitBackgroundToView` 读到 0 尺寸后直接返回，Plane 按原始尺寸显示。现改为按镜头尺寸直接缩放、摆放 Plane：杯碟整体不超过镜头高度的 `layoutHeightFraction`（默认 0.9），并夹在左右两栏之间（视口 x 0.262–0.772）。镜头尺寸/位置变化时自动重新适配；退出时恢复 Plane 原始 localPosition/localScale。
- 背景与杯型：`FreeCafeLatteArtStage` 程序生成胡桃木桌面和俯视杯（拉花杯=金边瓷杯+碟，小杯=钴蓝线白瓷+碟，大杯=陶土马克杯+软木垫），按当前杯子 `specId` 选择；小杯液面更小。杯图排在液面之下（sortingOrder 低于 Plane 的 1000）。`woodBackground` 仅在关闭 `useGeneratedTable` 时使用。
- 左栏：参考图案列表（预览、拟合度、拟合条、势力徽记），悬停卡显示名称、所属势力、寓意与画法。未解锁图案显示为「？？？」，不可选、不参与识别，悬停不泄露势力名。
- 右栏：当前杯型卡（杯图、名称、杯中容量）、奶缸卡（`Content/FoamUsage`）、描线参考开关、提交/重画。
- 描线参考：选中图案以半透明线稿叠在杯面上，`raycastTarget=false`，不会挡住注奶。
- 测试依赖的节点名保持不变：`LatteArtPresetOverlay/Content/PresetPanel/PresetList/<id>Button/{Preview,Fit,Best,Selected}`、`Content/FoamUsage`、`Content/Submit`、`Content/Reset`、`Content/PatternMeaning/Meaning`、`PresetPanel/StatusCard/Status`。

## 图案

| ID | 名称 | 模板 | 势力 |
| --- | --- | --- | --- |
| heart | 爱心 | Heart | — |
| tulip | 郁金香 | Tulip | — |
| rosetta | 树叶 | Rosetta | — |
| bean | 咖啡豆 | Bean | — |
| sword_crown | 剑与王冠 | SwordCrown（王冠轮廓 + 一道剑身） | court 奥尔狄亚王庭 |
| concord_ring | 圣谐之环 | ConcordRing（圆 + 中央菱形） | church 圣谐教会 |
| new_rose | 五瓣玫瑰 | NewRose（五瓣轮廓 + 花茎） | rose 新玫瑰会 |
| broken_chain | 断链 | BrokenChain（两节开口相对的链环） | revolution 革命联合 |

- 每个徽记图案都是 2 笔，可按任意起点、方向和 ±20° 倾斜绘制。旧 X/A/王冠模板保留（存档中已有杯子仍能显示名称），不再出现在默认列表。
- 图案只决定杯中内容的名称，不改变订单评分或剧情结果；没有新增剧情字段。

## 解锁规则

- 日常四款始终可用。徽记图案在 `WorldFactionCatalog.IsEncountered` 为真后解锁：任一列出的**已有**事实为 true 即可（剧情根尾写入的 `story.<root>.completed`、`known.*` 或 `route.*`），只读，不写任何状态。
  - 王庭：`known.alienor`、church.1009.s2 / revolution.1028.s1 / church.1104.s1 / church.1111.s1 完成、`route.church`
  - 教会：`known.sade`、revolution.1014.s1 / rose.1016.s1 / church.1020.s1 / church.1023.s1 / church.1111.s1 完成、`route.church`
  - 新玫瑰会：rose.1011.s1 / 1016.s1 / 1024.s1 / 1024.s2 / 1026.s2 / 1103.s1、revolution.1029.s1 / 1107.s1、church.route.1112.s1 完成、`route.rose`
  - 革命联合：`known.lesou`、`known.mary`、rose.1024.s1 / 1024.s2 / 1026.s2、revolution.1028.s1 / 1029.s1、church.1111.s1 完成、`route.revolution`
  - 共通分流根 church.1111.s1 是所有路线必经，因此王庭、教会、革命联合最晚在 11/11 解锁；新玫瑰会由其前期线索或分流后各线解锁。
- 直接运行 CoffeeShop（活动场景为 CoffeeShop 且不存在 `GameFlowBootstrap`）时全部解锁，便于测试。

## 详情面板 · 世界情况

- 世界页选择项：世界稳定度 + 已遇到的势力（沸点咖啡馆始终可见）。选中势力时，在原 PSD 信息区上方显示势力档案卡（徽记、名称、称号与领袖、可滚动简介），进度条显示对应的现有倾向 Bar（王庭与教会共用 `faction.church`）。未遇到的势力完全不显示。
- 简介文本取自 `策划文件/文案/z-全设定集/势力与派别设定案.xlsx`，删去尚未揭晓的秘密（杜瓦尔与革命联合的关系、贝特朗苏醒等），定义在 `Assets/Scripts/GameFlow/UI/WorldFactionCatalog.cs`。
- 徽记图：`Assets/Resources/UI/Factions/{cafe,court,church,rose,revolution}.png`，由策划提供的徽记去白底、去文字裁出；沸点咖啡馆徽记取自设定案内嵌图片。
- 同时清理了详情面板中的开发用语（“NCS 存档”“表内方向”“正式相识记录尚未写入”等）。

## 验证状态

- 已通过：使用 Unity 自带 Roslyn 单独编译 GameFlow.UI、FreeCafe.Unity、FreeCafe.Editor、Narrative.Editor、Assembly-CSharp(-Editor) 及 FreeCafe EditMode/PlayMode、GameFlow Presentation/Narrative 测试程序集，0 错误；Python 复刻识别算法验证 8 个图案在平滑手抖下均正确识别（置信度 0.80–0.88），次优候选 ≤0.58，纯圆/方框/乱涂/重复描线仍不识别。
- 尚未执行：Unity Test Runner（EditMode `LatteArtPatternRecognizerTests`、`FreeCafeProductionTextTests`；PlayMode 拉花、详情面板、`GameplayHudPlayModeTests`）与实机截图。
