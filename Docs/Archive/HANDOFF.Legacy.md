# 交接文档 —— 《Spotlight》咖啡店玩法（Trae Work 接手用）

> 项目路径：`e:\myproject\unity_p_taptap2026`
> 整理时间：2026-10-03 · 整理者：Trae Code（IDE 端）
> 用途：把本轮开发的全部上下文、当前状态、待办任务与避坑要点完整交接给 Trae Work。
> 配套文档：`PROMPTS_README.md`（美术工作流）、`Assets/Art/Gameplay/prompts.json`（提示词清单）

---

## 0. 美术任务状态：已完成（2026-10-03 更新）

8 张 AI 美术图已生成、后处理并接入 CoffeeShop 场景。关键事实：

1. **生成**：Seedream 生成，品红 `#FF00FF` 兜底背景（原始图保留在 `Assets/Art/Gameplay/AI/`）
2. **后处理**：品红抠图 + 最大连通域清理（去 JPEG 噪点）+ 顾客圆形徽章/道具裁边 → `Assets/Art/Gameplay/*.png`
3. **导入修复（重要）**：旧占位图的 `.meta` 是 `spriteMode: 2`（Multiple），子 sprite rect 写死旧尺寸——同名覆盖后 Unity 按旧 rect 裁切新图导致显示碎片。已通过**删除 8 个 meta 重新导入**修复（现为 Single、PPU=100、maxTextureSize=4096）；GUID 未变（编辑器会话内重导入复用原 GUID）
4. **场景重接线**：6 个世界对象 SpriteRenderer 已重挂、`customerSprites[3]` 已重连、scale 按高度匹配重调、场景已保存；根级 `Background` 空壳残留已删除
5. **脚本增强**：`apply_ai_art.py` 已加入连通域清理 + 顾客分支先抠图再裁圆，重跑不会退化

**当前场景 scale**（位置见 2.2 节，不变）：Background 0.75 / MachineEspresso 0.2081 / MachineGrinder 0.2118 / CupEmpty 0.1907 / CupLatte 0.1294 / Customer 0.2188。视觉上若某道具偏大偏小，直接微调该对象 scale 即可。

**剩余验证**：按第 8 节清单跑一次 Play Mode 亲测（重点：布局协调性、顾客徽章大小、UI 热区）。

---

## 1. 项目背景与目标

- Unity **6000.3.5f2**，2D 咖啡店模拟经营（TapTap 2026 参赛项目）
- 两层目标：
  1. **上层游戏循环**（本轮已完成）：吸引人、可完整游玩的循环
  2. **底层社交关系判定系统**（本轮只留扩展接口，暂不实现）：声望等数值影响剧情走向、咖啡拉花形状影响好感度等；策划案由用户持有
- 硬性约束：本地演算（**无服务器**）；配表遵循 **Luban** 规范（当前以 Python 等效生成器落地）；**高扩展性**
- 用户协作偏好：最小化修改、亲测验证、参数走配表与 Inspector、重大改动前备份

## 2. 已实现内容（全部通过 Play Mode 验证）

### 玩法核心（`Spotlight.Gameplay` 程序集）
| 文件 | 职责 |
|---|---|
| Core/CoffeeGameplayController.cs | 主控制器：单例 Instance + `EnsureExists()` 自举；持有档案/订单/营业周期/制作会话 |
| Core/CraftingSession.cs | 制作会话：按配方步骤推进、质检倍率、错误加料/可可失误惩罚 |
| Core/OrderManager.cs | 订单生成（按声望分段）、接单、状态流转 |
| Core/DayCycleController.cs | 营业倒计时、日结（声望/技能点/累计单数结算） |
| Core/PlayerProfile.cs | 金币/声望/技能点/库存/设备升级 |
| Core/RecipeStepParser.cs | 解析配方步骤串 |
| Core/GameplayConfigService.cs | 读取 Luban JSON 配置 |
| Core/GameplaySaveParticipant.cs | 接入 GameFlow 存档系统 |
| Social/SocialService.cs | `ISocialService` + `NullSocialService` + `SocialServiceRegistry`（**扩展点**） |
| UI/CoffeeGameplayPanel.cs | 玩法 HUD（场景内面板，见架构决定） |
| UI/CoffeeGameplayPanelRegister.cs | 注册 GameFlow 钩子（标记 IsScenePanel） |
| View/CoffeeShopSceneView.cs | 世界视图动画：顾客滑入/杯子切拉花杯/设备脉动/上餐飞杯淡出 |
| Editor/CoffeeGameplaySceneSetup.cs | 场景自动装配（InitializeOnLoad，幂等，靠 PanelV2 版本标记重建） |

### 场景
- `Assets/Scenes/SampleScene.unity`：主菜单（Build Settings 索引 0）
- `Assets/Scenes/CoffeeShop.unity`：玩法场景（索引 1）
  - 根对象：`Main Camera`（正交 size 5.4）、`CoffeeWorldRoot`、`GameplayCanvas`、`CoffeeGameplayController`
  - `CoffeeWorldRoot` 子对象（世界空间，位置已与背景美术对齐）：
    - `Background`（0,0，按高度 10.8 世界单位缩放）
    - `MachineEspresso`（-5.6, -1.0）、`MachineGrinder`（-3.1, -1.2）
    - `CupEmpty` / `CupLatte`（-4.35, -1.4）
    - `Customer`（4.8, -1.4）+ `CoffeeShopSceneView` 组件
  - `GameplayCanvas`（CanvasScaler 参考分辨率 1920x1080）> `CoffeeGameplayPanel`（含 `PanelV2` 标记子物体）
  - 已知残留：**根级 `Background` 对象为历史构建遗留**（与 CoffeeWorldRoot/Background 重叠，无实际作用），可在 Hierarchy 中安全删除

### UI 与交互
- 面板按钮按步骤置灰引导；研磨钮兼任「研磨/停！/拉花」三态
- 顶栏文本位置已校准：金币 -860 / 声望 -680 / 技能点 -500 / 计时 0 居中 / 待处理 680
- Toast 消息 2.2 秒后自动淡出
- Esc 暂停、快存、读档（存档面板）均已接入本玩法

### 配置表
- 源文件：`Assets/ConfigTables/Data/*.xlsx`（Recipe / Ingredient / Device / GameParam / Text）
- 生成：`python Assets/ConfigTables/generate_tables.py` → 输出 `Assets/Settings/Resources/Luban/*.json`
- 配方步骤串格式：`select_cup|add_coffee|grind_click_3|moka_pot_5s|add_milk_5s|latte_art|serve`
- 7 个配方：espresso / macchiato / latte / matcha_latte / honey_latte / hot_chocolate / mocha
- 文本表含 `recipe.*` 与 `ing.*` 中文名（订单列表与步骤提示已显示中文）

### 美术
- `Assets/Art/Gameplay/*.png` 共 8 张程序化占位图（由 `Assets/ConfigTables/draw_art.py` 生成，可重跑恢复）
- `Assets/Art/Gameplay/AI/` 为生成暂存目录（当前为空，待 Trae Work 填充）

## 3. 完整游戏循环（已验证的操作流）

开始营业 → 接单（顾客从右侧滑入）→ 选杯 → 加料 → 研磨连点 → 计时步骤（煮咖啡/抹茶研磨/加奶，滑杆进度 + 设备脉动）→ 可可 QTE（滑杆停在绿区 0.4~0.6）→ 拉花（当前随机形状得分，预留拖拽轨迹接口）→ 上餐（杯子飞向顾客 + 顾客开心 + 双双淡出）→ 金币入账；营业倒计时结束日结声望；技能点可升级设备（耗时减半）。

## 4. 关键架构决策（不要改）

1. **玩法面板是场景内实例，不走 UIKit 动态加载**
   - `GameplayPanelHooks.IsScenePanel = true`；面板由场景提供，钩子直接激活（见 `CoffeeGameplayPanelRegister.cs`、`GameFlowBootstrap.cs`）
   - 修复 `OpenPanel` 为 `UIKit.OpenPanel` 会导致 `UIKit panel loader returned no prefab lease` 异常
2. **控制器自举**：`CoffeeGameplayController.EnsureExists()` 在面板打开时按需创建，不依赖场景预置
3. **世界/UI 分层**：世界物体放 `CoffeeWorldRoot`（世界空间），HUD 放 `GameplayCanvas`（屏幕空间）
4. **场景切换保留但 UI 不依赖**：`GameFlowConfig.asset` 中 `MainMenuScene=SampleScene`、`GameplayScene=CoffeeShop`
5. **自动装配脚本**：`CoffeeGameplaySceneSetup` 每次编辑器加载后检查场景（文本搜索 YAML 中的标记名），缺失才补建；**重命名 `CoffeeWorldRoot`/`GameplayCanvas`/`PanelV2` 会触发重建**。彻底停用可直接删除该脚本。

## 5. 避坑指南（重要）

1. **不要再尝试外部文生图 API**：
   - `trae-api-cn.mchost.guru` 端点生成队列卡死（20 次连续探测全部返回"生成中"占位图）
   - `pollinations.ai` 免费层限流（约 1 张/40 秒，频繁 402）；且"品红底"会导致主体被染色、渐变底无法抠图
   - → 这就是改用 Trae Work 生成的原因
2. **DOTween 版本限制**：`SpriteRenderer` 没有 `DOFade`（只有 Material 版），已用 `DOTween.To` 自实现（见 `CoffeeShopSceneView.FadeSprite`）
3. **CoffeeShop 场景不要再创建 EventSystem**：`GameFlowBootstrap` 已 `DontDestroyOnLoad` 一个，重复会导致 "2 event systems" 警告
4. **UI 面板字段为 public**：便于 Inspector 直接拖拽编辑（有意为之）
5. **Unity MCP 的 `generate_image` 需要 API Key**（fal/openrouter，均未配置），当前不可用
6. Unity batchmode 存在许可证问题（ResponseCode 505），**验证必须用编辑器 + Play Mode**
7. 编辑器可能关闭了自动刷新（kAutoRefresh=False），必要时手动 `AssetDatabase.Refresh()`
8. Console 中 `[Locus] Native broker bridge is required but did not start` 报错与本玩法无关，忽略

## 6. 常用命令

| 操作 | 命令 |
|---|---|
| 修改配表后重新生成 | `python Assets/ConfigTables/generate_tables.py` |
| 恢复程序化占位图 | `python Assets/ConfigTables/draw_art.py` |
| AI 美术后处理 | `python Assets/ConfigTables/apply_ai_art.py` |

## 7. 社交系统扩展点（后续方向）

- 接口定义：`Assets/Scripts/Gameplay/Social/SocialService.cs` → `ISocialService`
- 已接入调用：`CoffeeGameplayController.OnSessionFinished` 中调用
  `SocialServiceRegistry.Current.OnOrderServed(orderId, quality, latteArtShape, price)` → 返回金币倍率
- 扩展方式：用真实实现替换 `NullSocialService` 即可接入关系判定/声望/剧情走向，**无需改动玩法代码**
- 待规划：拉花拖拽轨迹判定、顾客情绪反馈、剧情分支

## 8. 验证清单（Play Mode）

1. 打开 `SampleScene` → Play → 开始游戏（应切到 CoffeeShop 场景）
2. 开始营业 → 接单 → 顾客从右侧滑入
3. 按步骤完成一杯拿铁（含拉花）→ 上餐 → 飞杯动画 → 金币增加
4. Esc 暂停 → 快存 → 读档 → 进度恢复
5. 营业倒计时结束 → 显示日结声望

## 9. 关键文件地图

| 路径 | 说明 |
|---|---|
| `Assets/Scripts/Gameplay/` | 玩法（Core / UI / View / Social / Editor） |
| `Assets/Scripts/GameFlow/` | 游戏流程基础设施（主菜单/加载/暂停/存档/设置） |
| `Assets/Scripts/TextLocalization/` | 文本本地化服务（独立程序集） |
| `Assets/ConfigTables/` | 配表 xlsx + 生成/绘图/后处理脚本 |
| `Assets/Settings/Resources/Luban/` | 生成的 JSON 配表（运行时读取） |
| `Assets/Settings/GameFlowConfig.asset` | 场景名与全局流程配置 |
| `Assets/Art/Gameplay/` | 8 张美术 + `prompts.json` + `AI/` 暂存 |
| `Assets/Scenes/` | SampleScene（主菜单）、CoffeeShop（玩法） |
| `PROMPTS_README.md` | 美术生成工作流说明 |