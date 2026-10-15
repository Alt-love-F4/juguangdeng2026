# 项目音频接入（B 方案）

## 一个地方配置

- Unity 菜单：`Tools > Audio > Game Audio`（游戏音频配置）。
- 唯一默认配置资产：`Assets/Resources/GameAudioCatalog.asset`。
- 可搜索 Key / 说明、只显示缺失 / 临时替代项、查看诊断。在 Play Mode 展开条目可试听，使用“停止试听”结束循环或恢复之前的音乐。
- 每个 Key 可改素材、启用状态、分类、基础音量、音高、循环、淡入 / 淡出、同 Key 并发数和触发间隔。不要改掉已被代码引用的固定 Key；新增剧情声音可另加 Key。
- 直接拖 `Assets/Art/audio` 中的 AudioClip 即可，不必移动到 Resources。`resourcePath` 是 AudioKit loader 的资源地址，不是任意 Assets 路径；不会自动解释为文件系统路径。
- 缺素材保持空白并在首次实际触发时告警；不自动寻找“差不多”的音频。取消勾选 `enabled` 可主动静音某条配置。
- 修改正在播放的素材 / 音量不会改写现有 voice；停止后重新播放以试听新配置。运行中的资产编辑请注意 Unity 的持久化行为。
- `Create Initial Audio Catalog` 仅在资产不存在时初建，已有资产不会被覆盖。

## 架构和边界

`GameAudio` → `GameAudioService` → 已有 YokiFrame AudioKit。没有修改 YokiFrame 包源码，没有新建播放器或对象池。

- 独立 `Spotlight.Audio` 程序集供 UI、经营 Unity 适配、Narrative 使用；`FreeCafe.Domain` 仍不引用 Unity / 音频服务。
- `CatalogAudioLoader` 仅将配置中的直接引用交给 AudioKit，其他地址委托给之前的 loader。框架仍负责资源缓存、播放和资源释放。
- 保留显式注入的 AudioKit backend。未注入时，项目 Unity 适配器复用 `UnityAudioKitBackend`，仅将淡变更新时钟适配为非缩放时间，保证 `timeScale=0` 时 UI 与演出淡变不挂起。
- 一次性声音按 Key 限流 / 控并发；循环按 owner + slot 幂等维护。关闭只清理自己的 owner，不调用全局 StopAll。
- 经营循环在暂停 / 经营模拟暂停 / 离开所在区域时停止，恢复后从头播放（不是采样位置续播）；UI / 剧情 / BGM 继续使用非缩放时间。
- 主场景 / DayFlow 设置基线音乐；地图覆盖层与剧情使用临时 owner 覆盖。最近明确请求的 owner 优先，释放后恢复下一层 / 基线，同曲不重启。
- 配置窗口的活动列表显示项目持有的声音与 owner，不代表系统中其他模块直接播放的所有声音；短音结束观察依赖导入长度。
- 启动时创建常驻 `GameAudioRuntime`。场景加载 / 卸载时检查 Listener；场景没有活动 Listener 才启用兜底 Listener，不往演出高层 Canvas 添加内容。运行中手动生成额外 Listener 的系统仍应遵守单 Listener 约定。

## 已接入触发

| 范围 | 触发 / 去重与停止规则 |
|---|---|
| 通用 UI | 真实 Button.onClick；悬停与主动导航选中；默认自动焦点静音。静态场景、既有面板及动态按钮工厂安装共用组件，不改每个按钮的业务事件。 |
| 取消 / 返回 | 设置、存档、暂停恢复、确认、结果、详情、对话、阅读、视频支持动作层统一反馈；动作层负责声音时按钮默认确认静音，避免鼠标 / Escape 两条路径重播。 |
| 页签 / 翻页 | 详情、任务页签，订单 / 配方翻页，阅读行 / 页；有语义的接口仅在真实变化时播放。 |
| 保存 / 载入 | 手动保存实际结果；读取 / 删除实际失败。自动保存不额外出声。 |
| 地图 | 行动拒绝 / 完成；动态按钮；侧栏真实开关；临时地图 BGM 覆盖关闭后释放。 |
| 违规 UI | `FreeCafeRuntime.Report` 的失败进入 `OperationToastPanel`，由 Toast 播放 `ui.error`。倾倒拒绝按错误变化限流。取消到屏幕外的拖拽不当违规。 |
| 出品 UI | 领域 `ActionResult.feedback` 明确区分满意 / 重做 / 不满意，由同一 Toast 使用三种不同 Key；不靠 success 布尔值或解析中文提示判断。 |
| 取放 / 投料 | 成功事务后播放；拖拽预览不出声；特殊结果不叠加通用放置音。 |
| 倾倒 / 拉花 | 实际接受液体 / 奶泡时循环；停流、空壶、暂停、松手、取消或关闭 owner 时停止。 |
| 设备 | 研磨 / 搅拌 / 加热按运行状态和区域维护循环；只有同一生产状态从 Processing 正常完成才播完成音；清空 / 读档不当成完成。 |
| 其他经营 | 研磨加速、即时打奶、物理铃声、丢弃 / 清空、拉花完成；顾客状态边沿与区域变化。物理按铃可以与交付结果音组合，这是两个不同含义的声音。 |
| DayFlow | 菜单、经营、城区、夜晚音乐；咖啡馆 / 城区环境；日期、开店、经营结束、日结。 |
| 剧情 | 保留 NCS `sfxKey` / `voiceKey` / `bgmOverride`，经快照传给 Panel；A_SFX / A_BGM 改走同一服务。逐句语音在换句 / 快进 / 关闭停止；补全文字不重播。 |
| 阅读 / 信件 / 通知 / 视频 | 现有 ReadingLine.voiceKey 贯通；翻页、拆信、签名、通知；视频 Direct 音轨在 Play 前及播放期间应用统一音量。自动视频失败不伪装为用户取消音。 |

### 音量映射

- 总音量 → AudioKit Master。
- 音乐 → Music。
- 音效 → Sfx、UI、Ambience、Voice（首版不增加额外滑条）。
- 视频音轨基础音量 / 分类在同一 Catalog 顶部配置，乘以 Master 与所选 Bus，一次应用，避免旧剧情 AudioSource 再乘一遍。
- 滑条拖动过程不连续放试听音，松开 / 离开选中时播放一次；初始化 `SetValueWithoutNotify` 静音。

## 素材初配

导入的 24 个音频文件中，目录当前共 60 个 Key，42 项已关联，18 项留空；17 个已关联项明确标为临时替代。Key 不要求与文件一一对应。

- 菜单：`Dorian-C#-一号主菜单(1)`；经营：`休闲`；城区：`日常推进`；夜晚：`回忆`。
- 研磨：`研磨旋转`；加热：`水沸腾`；确认：`确认选择`；翻页：`纸张翻动声`；开店：`游戏进入音效`。
- 城区环境：`中世纪街道`；咖啡馆环境临时使用低音量 `洽谈`。
- 其余音乐已登记为 `music.track.<文件名>`，供 NCS 已有 bgmOverride / A_BGM 引用。例如 `music.track.紧张`。不修改表结构、不新增平行剧情字段。
- 以上按文件名和导入元数据初配，尚不等于完成听感验收；尤其需要试听 MP3 循环首尾、底噪、瞬态和背景 / 提示的响度。

### 仍缺素材（18 项）

| Key | 用途 | 建议 |
|---|---|---|
| `ui.panel_open` | 打开弹窗 / 侧栏 | 可后补 |
| `ui.panel_close` | 关闭弹窗 / 侧栏 | 可后补 |
| `cafe.cup_pickup` | 取杯 | 核心经营，优先补齐 |
| `cafe.item_place` | 放下器皿 | 核心经营，优先补齐 |
| `cafe.ingredient_add` | 材料投入 | 核心经营，优先补齐 |
| `cafe.liquid_pour` | 液体倾倒 | 核心经营，优先补齐；需可循环片段 |
| `cafe.grinder_turn` | 手动研磨加速 | 可后补 |
| `cafe.mixer_run` | 搅拌机运行 | 核心经营，优先补齐；需可循环片段 |
| `cafe.extraction_drip` | 过滤滴水 | 可后补；需可循环片段 |
| `cafe.milk_froth` | 即时打奶完成 | 核心经营，优先补齐 |
| `cafe.discard` | 丢弃杯子 | 核心经营，优先补齐 |
| `cafe.container_empty` | 清空容器 | 核心经营，优先补齐 |
| `cafe.latte_pour` | 拉花有效注奶 | 可后补；需可循环片段 |
| `cafe.customer_arrive` | 顾客实际到店 | 可后补 |
| `cafe.customer_leave` | 顾客实际离店 | 可后补 |
| `cafe.area_transition` | 切换大厅 / 制作台 | 可后补 |
| `flow.business_end` | 营业结束 | 可后补 |
| `flow.day_end` | 日结 | 可后补 |

### 已有临时替代（17 项）

| Key | 用途 | 当前替代文件 |
|---|---|---|
| `ui.select` | 按钮悬停 / 主动导航选中 | H-pitch wooooooodblock 高音调西洋木鱼.mp3 |
| `ui.cancel` | 返回 / 取消 | M-pitch wooooooodblock 中音调西洋木鱼.mp3 |
| `ui.error` | 违规操作 UI / 行动拒绝 | L-pitch wooooooodblock 低音调西洋木鱼.mp3 |
| `ui.save_success` | 手动保存成功 | 确认选择.mp3 |
| `ui.tab` | 切换页签 | M-pitch wooooooodblock 中音调西洋木鱼.mp3 |
| `map.action_complete` | 地图行动完成 | 确认选择.mp3 |
| `cafe.machine_ready` | 设备完成 | H-pitch wooooooodblock 高音调西洋木鱼.mp3 |
| `cafe.delivery_bell` | 出餐铃 | H-pitch wooooooodblock 高音调西洋木鱼.mp3 |
| `cafe.order_success` | 出品 UI — 顾客满意 | 确认选择.mp3 |
| `cafe.order_remake` | 出品 UI — 要求重做 | M-pitch wooooooodblock 中音调西洋木鱼.mp3 |
| `cafe.order_failed` | 出品 UI — 不满意离开 | L-pitch wooooooodblock 低音调西洋木鱼.mp3 |
| `cafe.latte_complete` | 拉花提交完成 | 确认选择.mp3 |
| `ambience.cafe` | 咖啡店环境 | 洽谈.mp3 |
| `flow.date_change` | 日期切换 | 纸张翻动声.mp3 |
| `presentation.envelope_open` | 拆开信封 | 纸张翻动声.mp3 |
| `presentation.signature` | 签名确认 | 确认选择.mp3 |
| `presentation.notice` | 新通知展示 | M-pitch wooooooodblock 中音调西洋木鱼.mp3 |

优先替换：真实出餐铃、错误 / 重做 / 失败反馈、加工完成提示。当前三种出品与违规已有可播放素材，但不是专门设计的最终音。

配音与剧情专用音效另由填写的 NCS Key 决定；当前没有据空字段臆造配音或批量新增脚本内容。

## 验证

- 使用 Unity 编译、`Spotlight.Audio.Tests` 和现有共享演出场景 `Assets/Scenes/PresentationDebugScene.unity`，不新建临时 Scene / Canvas / 输入系统。
- 音频单元测试：句柄 / loader、Key 限流、循环幂等、owner 隔离、音乐覆盖与恢复、淡出尾音清理、按键绑定去重、禁用按钮、滑条初始化静音、目录完整性、语音 Bus。
- 演出音频回归：同一场景中违规 / 三种出品 Key、timeScale=0 淡出、对话补全文字不重复播放。
- 测试执行结果与现有项目其他测试阻塞项见下方验收记录；通过自动化不等于耳机 / 扬声器最终试听通过。

## 本次验收记录（2026-10-15）

### 已通过

- Unity 6000.3.5f2 已完成本轮脚本编译；没有修改已安装的 YokiFrame 包。
- EditMode 定向回归 **101 / 101 通过**：`GameAudioTests` 15 项、`FreeCafeClassificationTests` 86 项，涵盖满意 / 重做 / 失败结果语义。结果保存在 `.unity/test-results/audio-and-classification-tests.json`。
- 最终共享演出场景 PlayMode 回归 **5 / 5 通过**：违规与三种出品 Toast 的不同声音 Key、`timeScale=0` 时淡出、同一句对话补全文字不重放、其余内容面板冒烟、日期自动结束与回调隔离。使用既有 `PresentationDebugScene.unity`，未另建演出场景 / Canvas / 输入系统。结果保存在 `.unity/test-results/audio-presentation-tests.json`。
- 已通过实际 Unity 菜单命令打开统一配置窗口；Catalog 检查为 60 Key、42 已关联、18 缺素材、17 临时替代（临时替代包含在已关联数中）。最终定向测试后 Console 查询无 Error；这不抹去下述咖啡店回归的已记录失败。
- 排除接手前已有字体改动后，`git diff --check` 无空白错误。

### 未通过 / 未完成的验收边界

- 咖啡店实景 4 项 PlayMode 回归均在 SetUp 被 `InvalidOperationException: Duplicate FreeCafe world in loaded scenes` 阻断，**尚未到音频断言，不计为通过**。包含已有杯具按钮、实际豆子拖拽、托盘 / 空铃 Toast 测试，以及新增的研磨暂停 / 清空 / 完成 / 读档音频生命周期测试。结果保存在 `.unity/test-results/audio-cafe-playmode-tests.json`。
- 已排除仅由启动场景自动加载引起的可能：改为从既有共享调试场景启动仍复现。代码上 `FreeCafeRuntime.Awake()` 先调用 `PlayerCafeOwner.Bind(World)`，后者已经以相同的 `freecafe.world.v1` 注册保存代理；随后 Runtime 的重复注册检查立即抛异常。两处所有权约定应单独统一。本轮没有顺带修改玩家会话 / 存档所有权系统，也没有用忽略异常的方式伪造音频验收通过。
- 更大范围 EditMode 曾出现 Narrative fixture 缺少 `tbncsabilitycheck`、当前角色表与测试预期不一致（例如缺少 `eugene`）、配方 / 来访 / 保存评分预期等失败，以及 EditMode 调用 `DontDestroyOnLoad`。记录在 `.unity/test-results/audio-inherited-editmode.json`；不能据此声称全项目回归通过。
- 共享演出回归仍有已有字体缺少 `▶` 的警告；未改动字体资产。硬编码旧预览依赖不存在的角色时亦不属于本轮音频通过范围，新增对话音频用现有 NCS 入口验证。
- 尚未进行人工听感 / 混音验收，也未完成 MP3 循环接缝、音量平衡和最终素材替换。18 项缺素材仍会按目录提示，17 项临时替代不是最终成品。
