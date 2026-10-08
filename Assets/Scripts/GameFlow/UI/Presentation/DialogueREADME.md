# 双 / 三角色对话演出

与日程面板一样，使用独立 `DialoguePanel.cs` / `DialoguePanel.prefab`，通过 UIKit 加载和回收。左右两个 Image 显示真实 Sprite；背景与深色暖金对话框复用项目现有美术风格。角色设计稿当前仍带原始底色，可在预制体或剧本中替换为正式透明立绘，不会改动源图。

## 正式第一天流程

阿黛尔引入已接在：信件 → 第一天日期 → 营业准备 → 咖啡馆入场 → 对话 → 开始经营。
第一天正式台词由 `LubanOpeningDialogueSource` 按 NCS 的 `OpeningPresentation` / `OpeningLine` 读取，F8 示例台词由 `DialoguePreviewDialogue.cs` 硬编码。
两份视觉资源仅存立绘/背景引用，不存台词。正式开场读取既有 Luban/NCS 配表；Panel 不直接读取表。
保留 `IDialogueScriptSource`，显式注入仍优先；详见 `FirstDayIntroductionREADME.md`。

## 快速测试

1. 打开现有 SampleScene，进入 Play Mode。
2. 在主菜单或营业阶段按 **F8**，播放七句示例。加载、暂停、日程过渡和既有 Narrative 演出中不触发测试。
3. **空格 / 回车 / 点击「继续」**：逐字显示时第一次补完当前页，再次输入翻页/换句。第六句用于测试长文本分页。
4. **Esc /「退出」**：取消演出，不触发完成回调。
5. 演出结束/取消后自动关闭，可再次 F8。测试热键仅编辑器和 Development Build 启用。

## 业务接口：一次传入大量台词

```csharp
var lines = new List<DialogueLine>();
// 当前台词直接写在 C# 中，不预建 UI 或每句动画。
lines.Add(new DialogueLine(DialogueSpeaker.Left, "欢迎光临。"));
lines.Add(new DialogueLine(DialogueSpeaker.Right, "一杯热咖啡。", gesture: DialogueGesture.Nod));
lines.Add(new DialogueLine(DialogueSpeaker.Narrator, "门铃再次响起。"));

var script = new DialogueScript(lines,
    new DialogueCharacter("店主", leftSprite),
    new DialogueCharacter("客人", rightSprite),
    title: "咖啡馆 · 第一幕", background: cafeTexture);
DialoguePlayback.Play(script,
    onFinished: () => { /* 接下一段剧情 */ },
    onCancelled: () => { /* 玩家取消；不应结算正常完成 */ });
```

- `IDialogueScriptSource.CreateScript()`：由 Luban/NCS 数据源或硬编码示例实现；面板不负责 I/O。`DialoguePlayback.PlayNcsOpening(presentationId, ...)` 可直接读取指定开场。
- `DialogueVisualsAsset` 只配置立绘和背景；正式台词来自 NCS，演示台词保留在 C# 中，不使用资源中的台词列表。既有 `DialogueScriptAsset` 类型保留兼容，但不用于当前剧情播放。
- `DialoguePlayback.Play(source)` 自动打开面板，正常结束后先关闭，再调用完成回调。
- `IDialoguePresentation.Play(script, onFinished, onCancelled)` / `Advance()` / `Stop()` 是底层演出接口；直接调用 Panel.Play 的调用方负责正常结束时关闭面板。
- `DialogueScript` 复制台词列表作为只读快照，不受调用者后续 Clear/Add 影响；拒绝空脚本、null 行。
- `DialogueLine` 支持旁白/左/右、姓名覆盖、点头/摇头、自动推进秒数（0 为手动）、左右角色替换。`replaceLeft/right=true` 且角色为 null 可隐藏该侧，换句会保留角色状态。
- TMP 使用纯文本和分页，避免超长句溢出或外部文本被当成富文本标签；每页分别逐字显示。

## 生命周期和调整

所有动效/逐字显示/自动推进使用非缩放时间；同一帧按钮和键盘重复输入只处理一次；进退场期间不推进。完成回调只调用一次，Stop/隐藏/关闭/销毁会取消旧动效并清掉回调，不推进日程或修改剧情数据。重复 Play 会重置立绘、背景、姿态与页码。

`DialoguePanel.IsAnyPlaying` 用于屏蔽经营操作、日程经营控制按钮和 Esc 暂停，不修改全局 timeScale。返回主菜单/开启新会话时由 GameFlow 清理演出。当前仅实现线性台词演出，不包含选项分支、保存断点或自动接入既有 Narrative 原子；这些留在上层剧情逻辑。

打开 `Assets/Resources/Art/UIPrefab/DialoguePanel.prefab` 可调整布局、两张角色精灵和文本样式；组件参数可调整进退场、角色聚焦和打字速度（0 立即显示）。缺失资产可通过 `Tools > Game Flow > Create Missing Dialogue Assets` 补建，已有资产不覆盖。

PlayMode 测试：`Spotlight.GameFlow.Presentation.Tests` 中的 `DialoguePresentationTests`，覆盖大量台词快照、双立绘绑定、揭字/推进分离、同帧去重、分页、暂停时间播放、取消、复播重置、自动推进和关闭清理。


## 独立特效表现层（不接 NCS）

新特效统一在共享 `PresentationDebugScene` 使用 G 和 1~0 验证；不要再创建特效 Scene。
`DialoguePanel` 实现 `IPresentationEffectPlayer`；`PlayEffect`、`PlayCameraCue`、`PlayFade`、`SwapPortraits` 是未来 NCS 适配边界，当前不访问 NCS 运行时或表。
`DialogueLine` 增加与既有 NCS 同名的 `accentFxKey` / `accentTargetKey`，`DialogueCharacter.CharacterKey` 用于非说话人目标解析。
参数预设集中在 `PresentationEffectLibrary.asset`，详见项目 `Docs/PresentationEffects.md`。
所有动画通道由 Panel 拥有与清理。入场完成前不显示正文；替换先淡出旧角色再入场新角色；立绘、文字和按钮均不做水平镜像。
运行时可视内容放在 `EffectStage`，输入遮罩仍覆盖全屏；测试/业务查询立绘应使用 `LeftPortrait` / `RightPortrait`，不要假定旧的 Transform 路径。

## 第三个角色位

- `DialogueScript(..., centerCharacter: character)` 可同时展示左、中、右三人；不传中间角色时维持原双角色布局。
- 中间角色台词用 `DialogueSpeaker.Center`；逐句替换 / 移除用 `replaceCenter: true, centerCharacter: newCharacter / null`。
- `DialogueScriptAsset` 支持 `centerCharacter`；`DialogueVisualsAsset.CenterPortrait` 支持单独配置第三张立绘。
- 中间角色同样支持说话高亮、沉默压暗、点头、受惊、入退场和特效目标 `center` / `CharacterKey`。
- `SwapPortraits()` 仍只交换左 / 右角色，中间角色保持不动；所有角色保持原图朝向。
- 共享调试场景按 C 查看三人选项对话；第三人暂时复用店主立绘，之后在 `DialoguePreview.asset` 的 Center Portrait 换图即可。
- 本次只扩展演出数据接口，不修改 NCS Excel 表结构；现有 NCS 双人开场仍按原配置播放。
