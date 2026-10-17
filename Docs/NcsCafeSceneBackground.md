# NCS 咖啡馆实景背景约定

## 当前策略

咖啡馆内普通对话透出游戏实际 `CoffeeShop` 场景，不绘制 DialoguePanel 的全屏背景。经营场景负责组装楼层底图、家具和环境；对话仍负责文本、立绘、选项与原有 UI 特效。

不要为此删除经营底图或把经营底图重新挂进对话 RawImage。此前生成的完整背景图片继续保留，供店外及明确需要图片的演出使用。

## 配表协议（不改变 schema）

源文件：`Assets/ConfigTables/Data/Narrative/Narrative.xlsx`。

| 字段和值 | 语义 |
| --- | --- |
| `ConversationPage.bgOverride` 留空 | 原有背景继承，不是关闭背景 |
| `ConversationPage.bgOverride = scene.cafe` | 明确使用咖啡馆实景；关闭旧图片与交叉淡化层 |
| `ConversationPage.bgOverride = oct07.street.day` 等图片 key | 显示指定图片，可从实景重新切回图片 |
| `OpeningPresentation.visualsResource = Dialogue/CafeSceneVisuals` | 开场与教程使用实景；该现有类型的资源通过 `backgroundKey=scene.cafe` 传递策略 |

`scene.cafe` 大小写精确匹配；不是 Resources 图片路径，不是空白占位，也不是蒙眼/处刑黑场。只有已登记的标记可用，不接受凭空填写 `scene.xxx`。

进店的第一张店内页必须明确填 `scene.cafe`；后续连续店内页可以继承，但跨事件应显式填写。出店页填街景图片 key，防止上一页的实景模式继续继承。

店内时间与灯光使用游戏当前状态。标记不修改时间、不替换为清晨/夜景图片、不解锁或自动切换楼层。需要二楼、三楼或操作区域镜头时，应接现有玩法的楼层/镜头入口，不能靠图片背景伪装完成。

## 店内特殊画面例外（2026-10-17）

实景只替代普通店内对话背景，不替代剧本指定的特写、剪影合成图或其他特殊叙事图片。明确填写的特殊图片 key 必须保留，不能按咖啡馆地点批量改成 `scene.cafe`。

- 闭门蒂博剪影使用 `cafe.door.thibaut`，对应 `Assets/Art/Background/game/SCE_咖啡馆闭门场景蒂博剪影.png`；策划旧 key `church.cafe.thibaut.departure` 已作为别名映射到同一资源。
- 显示顺序为普通店内 `scene.cafe` → 特殊画面 `cafe.door.thibaut` → 普通店内 `scene.cafe`。回到普通对话时必须显式恢复；留空会继承上一张特殊图片。
- 当前图片、Texture 注册及原有文件名别名均未删除。核对源 Excel、生成 JSON 及实景迁移前版本，均没有 `church.1014.s2` 正式页面或此图的页面引用；本次没有为补图而新建事件、伪造台词或修改生成 JSON。
- 后续接入 `church.1014.s2` 时，按 `Docs/superpowers/specs/2026-10-16-content-batches/D-church/source-map.md` 的 P072 演出锚点（P071 后/P073 前；需求清单使用 P087 编号）切到此图，不应从告别段开头就显示已闭合的门。将相应后半段拆为独立页面后在 `ConversationPage.bgOverride` 填图片 key；返回营业场景的页面显式恢复 `scene.cafe`，再通过官方 Luban 管线生成。

本次只补资源别名和交接规则，未改配表或运行时代码，未执行 Unity 编译及 Play Mode。

## 本次源表修改范围

- `ConversationPage`：47 个原 `oct07.cafe.day/night` 的店内页面改为 `scene.cafe`。涉及 `oct07.s1` 的店内页、`oct07.s2`、`revolution.1013.s2`、`revolution.1014.s1`、`revolution.1028.s1`；信件前后背景继承也使用相同标记。
- `OpeningPresentation`：25 个 `first_day_cafe` / `cafe_tutorial.*` 演出使用 `Dialogue/CafeSceneVisuals`。
- 共 72 个单元格。没有改动前四行、表结构、文本、条件、选项、触发入口或剧情结算。
- `arrival.*` 为开幕街边/店面段落，不因旧字段名称包含 `phase_pre_business` 就改成店内。街道、俱乐部、教堂、蒙眼黑场保持原配置。

## 实现边界

- `NarrativeBackgroundKeys.CafeScene` 是共享保留 key；旧 `A_BG` 原子也识别它，而非尝试加载同名图片。
- `NarrativeStageVisuals.Entry.useSceneBackground` 是演出快照的显式策略。它不得同时设置 Texture、placeholder 或 solidColor。
- `DialogueScript` 对此标记禁止回退到旧 Texture；预览、历史重建和提示副本保留策略。
- `DialoguePanel.ApplyBackground` 先结束旧背景交叉淡化，再关闭背景 RawImage。连续翻页也会执行，不依赖重新创建面板。
- `NarrativeConversationEventPlayback` 对正式播放复用既有 `GameFlowBootstrap.TransitionToGameplayScene("CoffeeShop", ...)`；从地图触发也先准备实际咖啡馆。挂起期间释放舞台会使回调失效，不能取消后再弹回对话。
- 预览不加载 `CoffeeShop`、不创建 NCS Runtime 或推进 DayFlow；共享调试场景下背景透明，显示其现有底层布局。
- 自定义其他 `visualsResource` 的演出保留显式背景配置，不按 ID 名字猜测店内。

## 后续 agent 必须遵守

1. 先读根 `AGENTS.md` 的本约定，再编辑源表。不要把店内页恢复成 `oct07.cafe.day/night`，也不要用空值代替 `scene.cafe`。
2. 不要把“取消对话底图”实现成隐藏整个经营场景或把对话背景染黑；蒙眼/处刑黑场另有显式配置。
3. 不要自动为所有 `cafe.*` key 改成实景：店面外景和特殊插图不是普通店内对话。按实际剧情页定位。
4. 使用 `GenerateAll.bat` 或项目配置的官方 Luban 严格生成。禁止只手改 JSON；本次生成代码与既有类型完全一致，无需改 schema。
5. 改动不涉及正式事件/楼层解锁/存档语义；图片资源也不得成为新的剧情事实来源。

## 验证

本次已执行 Player 语义校验和官方 Luban `client + cs-newtonsoft-json + json + --strict` 生成；仅 `tbncsconversationpage.json` 和 `tbopeningpresentation.json` 数据发生变化，生成 C# 无变化。源 XLSX 除指定两张 sheet 的 72 个单元格外，其他单元格及包成员保持不变。

新增/调整回归覆盖：标记快照与 Texture 回退抑制、当前店内配表、开场教程配置、自定义资源优先、共享场景连续“图片→实景→图片”及交叉淡化清除。

遵照不干扰另一 agent 的要求，本次未启动 Unity 编译或 Play Mode，新增 Unity 回归尚未执行。后续运行验证只用已有 `Assets/Scenes/PresentationDebugScene.unity`，正式入口还需核查从地图到咖啡馆的加载、取消和后续 DayFlow。
