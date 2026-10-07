# 独立演出特效表现层

## 本次边界

沿用 UIKit 的真实 DialoguePanel 与共享 PresentationDebugScene，不启动 NCS、不读取 NCS 配表、不调用触发器、不结算事件、不新增存档逻辑。
效果字段/原子来自既有 NCS 约定；这次只建立可调用的表现入口，不新建另一套 NCS 或剧本字段标准。
现有线性 DialogueScript 仍是本地演示入口，不代表已经实现了完整的页/选项/Reading/Notice/视频过场方案。

## 已实现效果

| ID / 入口 | 行为 |
|---|---|
| fx_enter_shadow_walk | 当前立绘以黑影、小比例、远处偏移入场，随后显色落位；右侧位移镜像。首次入场与新角色替换默认调用。 |
| fx_accent_shake_startle | 单个目标角色横向震动并衰减，结束回原位；可作用于非说话人。 |
| fx_shake_light | UI 演出舞台轻震动，固定级别 1。 |
| fx_shake_heavy | UI 演出舞台重震动，固定级别 2。 |
| fx_flash_white | 全屏白闪，结束清除；遮罩不拦截输入。 |
| fx_transition_black | 压黑后恢复，可在全黑时执行画面切换回调。 |
| fx_portrait_fade_out / fx_portrait_fade_in | 默认退场/换位用的淡出淡入。新增的是内部效果资源 ID，不是 NCS 新字段。 |
| SwapPortraits | 淡出两侧 → 隐藏时交换角色 → 右侧图像水平镜像 → 淡入。文字与姓名条不镜像。 |
| PlayCameraCue | UI 舞台的缩放、焦点偏移与震动；结束保留目标镜头，调用默认参数复位。 |
| PlayFade | in/out/black/transition；out/black 保留黑场，in 恢复，transition 自动恢复。 |

角色高亮/沉默压暗、轻微说话聚焦与旧点头指令仍保留。镜像仅作用于 portrait Image，不翻转边框、文字或按钮。
当前美术资源保留原始设计稿底色；效果作用于实际 Sprite。将来换正式透明立绘即可得到无底色的角色剪影，不需要改效果逻辑。

## 资源与归属

效果参数由美术/程序在 `Assets/Resources/Dialogue/PresentationEffectLibrary.asset` 管理，剧本只填效果 ID。
`PresentationEffects` 是 UI-local 的表现对象，由 Panel 初始化、Tick 和 Dispose；没有静态全局动画状态或独立资源加载系统。
角色进退场、左右强调、震屏、镜头和遮罩各有自己的通道：角色震动可以与震屏/白闪并行，不会互相杀掉。
同一时刻只保留一个震屏；重震屏阻止低级轻震屏覆盖，同级或高级新效果替换旧效果，不叠加。

所有效果使用非缩放时间，timeScale=0 时仍播放。
Stop/隐藏/销毁取消所有本 Panel 拥有的效果和协程、清遮罩、恢复舞台位置/比例，并清理回调。
取消/替换的 handle 为 Cancelled，不调用旧 completed；完成为 Completed。
未知效果/目标或非法参数明确报错，不悄悄使用别的角色或取消有效效果。

## 后续 NCS 适配边界（本轮不连接）

| NCS 输入 | 表现层入口 |
|---|---|
| accentFxKey + accentTargetKey | DialogueLine 的同名数据，再由 PlayEffect 调用；目标留空时用说话人。 |
| A_FX 的 fx | 已注册 ID 可交给 PlayEffect；任意粒子 prefab 路径不在这个 UI 内置效果目录中，后续沿用现有 NCS 资源层。 |
| A_CAMERA 的 zoom / shake / duration | PlayCameraCue；UI shake 的单位为参考分辨率上的像素，适配器必须明确与世界镜头的单位转换。 |
| A_FADE 的 type / duration | PlayFade；可等待返回 handle。 |
| 页内立绘指令及阻塞开关 | 适配器调用 PlayEffect(..., blocking: true) / SwapPortraits，等待 handle 或 IsEffectBlocking；不新增策划字段。 |

NCS 角色对象解析、textKey、本地化、BGM/SFX/配音、条件、选项、事件队列及精确存档仍由后续适配器/现有对应系统负责。
本轮没有注册一个假的 NCS presenter，也没有修改 NarrativeController/NarrativePresenter/配表。

```csharp
// 当前可用：直接硬编码本地演示；未来由 NCS 适配器生成同样的调用。
var line = new DialogueLine(DialogueSpeaker.Left, "我说话，但客人受惊。",
    accentFxKey: PresentationEffectIds.AccentShakeStartle,
    accentTargetKey: "npc_guest");

panel.PlayEffect(PresentationEffectIds.ShakeHeavy);
panel.PlayEffect(PresentationEffectIds.FlashWhite); // 可与震屏/单角色震动同时播放
var blocking = panel.PlayEffect(PresentationEffectIds.EnterShadowWalk, "right", blocking: true);
panel.PlayCameraCue(zoom: 1.08f, duration: .35f, focus: new Vector2(35f, -8f));
panel.PlayFade("transition", duration: .6f, atBlack: () => { /* 全黑时切换画面 */ });
```

## 测试入口

复用 `Assets/Scenes/PresentationDebugScene.unity`：G 打开特效试验台，1~0 或右侧按钮触发。
数字键在没有对话时自动打开试验台，并等待入场后执行。H 收起说明；F9 暂停/恢复游戏时间；Esc 停止；Backspace 重播。

1 黑影入场，2 右侧受惊，3 轻震屏，4 重震屏，5 白闪，6 黑场切换，7 左右换位，8 镜头推近，9 镜头复位，0 右侧淡出。
继续推进试验台台词可验证非说话人震动与第三名角色替换。

PlayMode：PresentationEffectsTests、DialoguePresentationTests、PresentationDebugSceneTests、FirstDayIntroductionTests。
菜单 `Tools > Game Flow > Run Presentation Effects Regression` 执行四组回归；详细 XML 写入 `output/presentation-effects-tests.xml`。
新增/修改效果应复用本场景及测试，不另造测试 Scene、Panel 或 NCS 通道。
