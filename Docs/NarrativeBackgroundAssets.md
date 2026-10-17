# 对话背景资产与后续复用

> 当前默认策略已改为：咖啡馆内普通对话使用 `scene.cafe` 实景，不叠加下面目录中的咖啡馆图片。开场/教程填写 `Dialogue/CafeSceneVisuals`。空背景仍是继承，不能用于隐藏。配表规则与后续 agent 约束以 `Docs/NcsCafeSceneBackground.md` 和根 `AGENTS.md` 为准。

## 资源入口

正式对话背景引用 `Assets/Art/Background/game` 内的完整对话 PNG。原有一楼、二楼、三楼经营底图仍保留给经营场景，绝不注册为对话底图。

明确禁止把以下经营底图用于 `ConversationPage.bgOverride` 或 `DialogueVisualsAsset.background`：`SCE_一楼大厅场景底图.png`、`SCE_二楼会客室场景底图.png`、`SCE_ 三楼宿舍场景底图.png`。它们只由实际游戏场景组装使用；对话需要咖啡馆实景时填 `scene.cafe`，需要静态画面时使用对应的完整“对话场景” PNG。

- 统一目录：`Assets/Resources/Dialogue/NarrativeStageVisuals.asset`。
- 普通 NCS 对话与章节预览通过 `NarrativeStageVisuals.Resolve` 读取背景快照。
- 开场和教程正式配表使用 `CafeSceneVisuals` 透出实景；旧 `FirstDayIntroductionVisuals` 仅保留图片型演出的兼容配置，不作为当前教程默认值。
- 单独指定其他 `visualsResource` 的开场演出保持自己的背景，不被教程默认规则覆盖。
- 本次仅调整源表已有背景/visualsResource 字段及生成 JSON；不修改事件解锁条件、二楼开放状态或流程结算逻辑。

## 后续填表方式

咖啡馆内普通新对话在 `ConversationPage.bgOverride` 填 `scene.cafe`。下表图片稳定 key 用于图片型演出；店外仍按实际地点填写，无需把原图搬进 Resources，也无需逐事件修改代码。

同一地点可以继续使用剧情命名空间，例如：

```text
neutral.cafe.dusk                           -> cafe.dusk
church.detention.dawn                      -> detention.dawn
church.1104.s1.background.morning.wake       -> cafe.bedroom.dawn
church.1201.s1.background.near.market        -> market.day
church.1201.s1.background.far.street.dusk    -> street.night
church.1201.s1.background.near.old.house     -> old.house.day
future.event.old.street.day                 -> old.street.day
```

这些规则只识别登记过的地点后缀，不依赖日期。`far/near` 分支可共享同一图片。也支持目录内原图的完整文件名、无扩展名文件名和 `Assets/Art/Background/game/...png` 路径。

空 `bgOverride` 仍遵循原有 NCS 背景继承行为；不要把空值理解成自动猜测地点。

## 稳定 key 清单

| 稳定 key | 文件名（位于 game 目录） |
| --- | --- |
| `cafe.day` | `SCE_一楼大厅对话场景日景.png` |
| `cafe.dawn` | `SCE_一楼大厅场景清晨.png` |
| `cafe.dusk` | `SCE_一楼大厅场景傍晚.png` |
| `cafe.night` | `SCE_一楼大厅场景夜晚.png` |
| `cafe.paper_flowers` | `SCE_一楼大厅场景纸花装饰.png` |
| `cafe.second_floor.day` | `SCE_二楼会客室对话场景日景.png` |
| `cafe.second_floor.cluttered` | `SCE_二楼会客室场景杂乱.png` |
| `cafe.dormitory.day` | `SCE_三楼宿舍对话场景日景.png` |
| `cafe.bedroom.dawn` | `SCE_三楼主角房间场景清晨.png` |
| `street.day` | `SCE_圣徒街场景1.png` |
| `street.night` | `SCE_圣徒街场景夜晚.png` |
| `cafe.exterior.day` | `SCE_沸点咖啡馆店面场景日景.png` |
| `cafe.exterior.declining` | `SCE_沸点咖啡馆店面场景萧条.png` |
| `club.exterior` | `SCE_布塔克俱乐部门口场景底图.png` |
| `club.reception` | `SCE_布塔克俱乐部会客室场景底图.png` |
| `cathedral.day` | `SCE_圣卷大教堂场景日景.png` |
| `market.day` | `SCE_大堂区露天市场场景日景.png` |
| `market.shop.day` | `SCE_大堂区杂货铺内部场景日景.png` |
| `suburb.day` | `SCE_圣西尔万郊区场景日景.png` |
| `old.street.day` | `SCE_旧城区街道场景日景.png` |
| `old.house.day` | `SCE_吕西尼昂旧宅门前场景底图.png` |
| `detention.interior` | `SCE_教会看守所内部场景底图.png` |
| `detention.dawn` | `SCE_教会看守所内部场景清晨.png` |
| `detention.dusk` | `SCE_教会看守所内部场景傍晚.png` |
| `cafe.door.thibaut` | `SCE_咖啡馆闭门场景蒂博剪影.png` |
| `cafe.bar.day` | `SCE_咖啡馆吧台场景底图.png` |
| `cafe.workstation.day` | `SCE_咖啡馆操作台场景底图.png` |
| `cafe.serving.day` | `SCE_咖啡馆出餐台场景底图.png` |

## 兼容与优先级

1. `Context Overrides` 仅在页面/演出 ID 匹配前缀、且背景仍等于指定旧 key 时生效。例如初到城市的街景和店面不能共用旧地图占位；10.14 索雷尔清晨、10.12 纸花促销/门外试饮/制作段落、教程操作视角有局部迁移规则。明确填写其他背景 key 不受这些旧值迁移影响。
2. `Entries` 中的完整 key 精确匹配优先于 `Aliases`，可为某一剧情单独定制。
3. `Aliases` 按列表顺序匹配，`*` 是唯一通配符，必须匹配整个 key。更具体的旧城区规则排在通用街景规则之前；目标必须是已存在的 `Entry`，不允许别名链或循环。
4. 未登记的 key 仍尝试原有 Resources 路径；仍找不到时明确报错，绝不随便回退到咖啡馆掩盖遗漏。

`oct07.cafe.day/night`、`oct07.street.day` 保留原 key，但咖啡馆日景改为独立完成的对话版；夜景不再对日景叠蓝灰滤色。旧 `phase_pre_business` / `phase_night_choice` 对话背景分别映射一楼对话日景/夜景，不修改这些通用占位图片本身。

特别约束：`SCE_一楼大厅场景底图.png`、`SCE_二楼会客室场景底图.png`、`SCE_ 三楼宿舍场景底图.png` 是经营场景底图，不属于对话背景。对话注册表中的一、二、三楼日景均指向带“对话场景”名称的完整版本；三楼宿舍经营底图不会被任何对话 key 直接引用。

`rose.club.blindfold` 与 `church.execution.black` 是剧情要求的黑场，以 `solidColor` 明确登记，不应换成场景图片或报告为缺图。

历史美术对照表中的 `rose.history.portrait` 是独立画像/CG 需求，不属于本轮 28 张场景背景；不将整幅俱乐部会客室冒充画像特写。此 key 仍需后续对应画像资产单独登记。

## 完全新地点

若日后确实需要清单外的新地点，在同一目录新增图片，在目录资产 Inspector 的 `Entries` 添加 key 与 Texture 引用；需要带剧情前缀自动复用时，再在 `Aliases` 添加 `*.新key` 指向该条目。只改资产配置，不需要新增资源系统或修改解析代码。若新增的规则与旧规则重叠，具体规则应排在前面。

图片到位不代表自动开放楼层或启用未完成事件；楼层开放与剧情分支仍由已有 NCS 条件决定。

`Spotlight/Narrative/Build S1 Visual Resources` 已更新为正式日景、夜景、街景引用，保留目录其他条目与规则，不会重新生成旧占位。

## 验证边界

新增 `NarrativeBackgroundAssetsTests` 覆盖全部稳定 key、旧值迁移、未来命名空间、特定规则优先、蒙眼黑场与未知 key 报错；`NcsOpeningDataTests` 补充教程视角及自定义开场资源保持原样的断言。

遵照不干扰另一 agent 的要求，本轮只做资源引用、规则覆盖与代码静态检查，不启动 Unity 编译、Play Mode 或场景重建。后续运行态回归应使用已有 `Assets/Scenes/PresentationDebugScene.unity`。
