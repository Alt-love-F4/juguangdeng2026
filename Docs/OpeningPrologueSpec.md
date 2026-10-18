# 开场序章演出 Spec

> 后续衔接补充：叔叔信件结束后先进入“初来乍到”，再进入地图引导和“重新启航”。具体见 [初来乍到与自动检定表现补充 Spec](FirstArrivalAndPassiveChecksSpec.md)；本文仅定义信件之前的黑屏序章。

## 1. 目标

补齐玩家点击「开始游戏」后的第一段开场流程：在叔叔的信件之前，播放一段独立的黑屏文字序章，建立世界观、时代氛围和主角身份，然后再进入现有的叔叔信件流程。

本功能是低频复用的固定开场内容，不接入 NCS、不接入 Excel/Luban 配表，也不复用 `DialoguePanel`。

## 2. 最终流程

```text
主菜单点击「开始游戏」
    -> 加载/初始化新游戏所需的基础运行时
    -> 全屏黑屏
    -> 播放开场旁白序章
    -> 序章结束后进入叔叔的信件界面
    -> 沿用现有信件、角色创建、第一天和新手教程流程
```

序章结束后必须进入现有叔叔信件流程，不直接跳到第一天经营或咖啡馆对话。

## 3. 核心决定

- 序章做成独立的 `OpeningProloguePanel` Prefab。
- 序章不复用 `DialoguePanel`、NCS 对话数据或现有角色对话流程。
- 文案严格使用 `D:\桌面\开幕文案.docx` 中「1. 开场白」的最终文本。
- 不删除、不改写文案，只按语义合并成不超过 10 段。
- 玩家每段都需要点击才能继续。
- 序章不能通过普通输入跳过。
- 画面内始终显示 `Skip` 按钮，点击后直接结束序章并进入叔叔的信件。
- `Skip` 不需要二次确认。
- 序章期间不推进 DayFlow、不写入存档、不进入经营玩法。
- 序章使用不受 `Time.timeScale` 影响的时间。

## 4. 序章文案

以下文字必须全部保留。实现时可以把多行合并为同一个显示段，但不得删字或改文案。

建议先拆成 8 段，每段点击一次，共 8 次点击；如果排版需要可以调整为 8-10 段，但不得超过 10 段。

### 第 1 段

圣诺特，奥尔狄亚王国的心脏，奥尔狄亚人永恒的骄傲

这座都城在神与王的引领下熠熠生辉

自那神佑者降临于苍茫大地

奥尔狄亚王国已屹立于世间600余年

### 第 2 段

神佑者的血脉庇佑着这个伟大的国度

神佑者的荣光曾欲万古长存

### 第 3 段

然普照万物的光日渐消隐

无名之人的愤怒在街巷间涌动

野心家的欲望在阴影中生长

### 第 4 段

神安能庇佑奥尔狄亚人又一个百年？

抑或，我们将淹没在沸腾的人民之声中？

### 第 5 段

圣诺特，那千万生灵留恋的地方

我曾作为芸芸众生中一员生活在这座大都市

经营着一座平凡的咖啡馆

### 第 6 段

顾客来来往往，车水马龙

我曾想安然度日

在咖啡和茶点的芳香中就此了解余生

### 第 7 段

然而那激情的岁月中，无人可以作壁上观

我亦是如此

### 第 8 段

孩子们，现在，我将为你讲述那段故事——

### 非显示说明

以下内容是流程说明，不显示为正文：

- 开始时全屏黑屏。
- 最后一段结束后保持黑屏，然后切入主角阅读叔叔信件的场景。

## 5. 演出行为

### 5.1 初始状态

- Panel 打开时必须是全屏纯黑。
- 黑色遮罩覆盖整个画面，不能先露出地图、咖啡馆或其他 Gameplay UI。
- 正文初始透明度为 0。
- Skip 按钮在序章打开后可见并可点击。

### 5.2 单段播放

每段按照以下顺序播放：

```text
正文透明度 0
    -> 淡入
    -> 等待玩家点击
    -> 淡出
    -> 下一段
```

默认参数建议：

- 淡入：0.8 秒
- 淡出：0.5 秒
- 每段不自动推进
- 点击区域覆盖整个画面，点击正文、空白区域或 Skip 以外的位置都算推进
- 淡入/淡出动画未结束前，普通推进输入无效，避免误触连跳

具体时长必须做成 Prefab Inspector 可调字段，不要写死在播放逻辑中。

### 5.3 玩家输入

普通推进支持：

- 鼠标左键点击画面
- 空格
- Enter

普通推进只前进一段，不直接结束序章。

序章期间：

- Esc 不关闭 Panel
- 不打开暂停菜单
- 不响应地图、经营或其他玩法输入
- 不写存档

### 5.4 Skip

Skip 按钮始终显示，建议放在右下角，不能遮挡正文。

点击 Skip 后：

1. 立即停止当前淡入/淡出动画和协程。
2. 清理输入监听、Tween、回调和临时状态。
3. 关闭 `OpeningProloguePanel`。
4. 调用完成回调，进入叔叔信件流程。

Skip 是正式画面中的正式功能，不做 Editor/Development Build 隐藏逻辑。

## 5.5 分镜 CG 演出（2026-10-18 补充）

5.2 的「整段淡入」已升级为可交互过场 CG，文案与段数不变：

- 第 1~7 段各配一组分镜格（共 14 格），第 8 段「孩子们……」只有文字、居中显示。
- 每段开始时，正文逐字渐显（每字轻微上浮），分镜格**同时**按顺序出场；有配图时正文位于分镜下方。
- 出场方式由每格的 `ProloguePanelArt.entrance` 决定：主要使用 `FlyFrom*`，即从屏幕半程位置边滑动边渐显（`flyDistance=0.5`，非屏幕外飞入）；第 2 段光环用 `SplitVertical` 上下展开，第 7 段眼部特写用 `ZoomIn` 推近。全部完成后会缓慢推近。
- 输入：演出中第一次点击/空格/Enter 立即补全本段（文字和分镜停到最终状态），再次输入才翻页；淡出期间输入无效。Skip 行为不变。
- 所有动画都由每段的本地时钟求值（不用 Tween），补全、Skip、重播都落在确定的画面上，并使用非缩放时间。

资源与参数：

- 裁好的分镜：`Assets/Art/UI/OpeningPrologue/prologue_XX_y.png`；原始分镜参考图保存在同目录 `Source~/`（Unity 不导入）。
- 每段可调：`text`、单字渐显时长 `fadeInSeconds`、`fadeOutSeconds`、分镜根节点 `art`、`artDelaySeconds`（默认 0，即图文同时出场）、正文纵向区域 `textArea`、字号 `fontSize`。
- 每格可调：`entrance`、`delay`、`duration`、`flyDistance`、`flyFadePortion`、`flyTilt`、`flyParallax`、推近幅度 `driftScale`。
- 全局：`charStaggerSeconds`（逐字间隔）、`charRisePixels`。
- 生成/重建：`Tools > Game Flow > Rebuild Opening Prologue Panel`（`OpeningProloguePrefabBuilder` 内含排版坐标与出场配置；重建会覆盖 Prefab 中的手动修改）。
- 预览：`PresentationDebugScene` 按 `Z`。

## 6. Prefab 结构

建议资源路径：

```text
Assets/Resources/Art/UIPrefab/OpeningProloguePanel.prefab
```

建议脚本路径：

```text
Assets/Scripts/GameFlow/UI/Presentation/OpeningProloguePanel.cs
```

Panel 至少应包含：

- 全屏黑色背景遮罩
- 正文 `TMP_Text`
- 全屏点击区域或继续输入绑定
- `Skip` 按钮
- 根部 `CanvasGroup`
- 可选的正文淡入淡出 Tween 绑定

建议序列化字段：

```text
canvasGroup
background
narrationText
advanceArea
skipButton
beats
defaultFadeInSeconds
defaultFadeOutSeconds
finalBlackHoldSeconds
```

每个 `beat` 至少包含：

```text
text
fadeInSeconds
fadeOutSeconds
```

文案必须存在 Prefab 的序列化字段中，方便策划或开发在 Unity Inspector 里直接修改。不要把每段文字散落在 C# 播放逻辑中。

## 7. 播放接口和生命周期

建议提供类似以下的公共入口：

```csharp
public void Play(Action onFinished);
public void Skip();
public void Continue();
```

具体接口名称可以遵循项目现有 Panel 约定，但必须满足以下行为：

- 重复调用 `Play` 不产生重复实例或重复回调。
- 正常完成只调用一次完成回调。
- Skip 只调用一次完成回调。
- Panel 关闭、场景切换或对象销毁时，必须取消 Tween、协程、输入监听和回调。
- 播放结束后必须恢复进入前的 `Time.timeScale`。
- 不改变现有 DayFlow 保存字段和 NCS 状态。

## 8. GameFlow 接入

当前 `GameFlowBootstrap.StartGame()` 会初始化新游戏并进入现有 Gameplay/DayFlow 流程。工程实现需要在新游戏正式进入叔叔信件之前插入序章。

接入原则：

- 不重写现有叔叔信件逻辑。
- 不修改现有第一天 NCS 开场对话的内容接口。
- 不把序章文本放进 `DayFlowController` 的 NCS 或 Excel 数据结构。
- 序章完成后，再进入现有信件入口。
- 新游戏、恢复旧存档、返回主菜单、重新开始之间不能互相残留序章状态。
- 旧存档恢复不能重新播放这个序章。
- 新游戏才播放序章。

如果当前工程的 DayFlow 初始化必须提前完成才能打开信件，可以先完成必要的运行时初始化，但不能推进第一天阶段、写入存档或触发经营事件。

## 9. 调试入口和验证

必须使用共享场景：

```text
Assets/Scenes/PresentationDebugScene.unity
```

为 `OpeningProloguePanel` 增加独立的 Preview 入口，方便测试，不新建测试 Scene、Canvas 或 EventSystem。

至少验证：

1. 点击开始游戏后不会短暂看到地图或咖啡馆。
2. 初始状态是全屏黑屏。
3. 正文分段数量不超过 10 段。
4. 每段必须点击后才能继续。
5. 空格、Enter、鼠标点击均能推进当前段。
6. 淡入/淡出过程中重复点击不会连跳或重复回调。
7. Skip 始终可见，点击后能进入叔叔信件。
8. Skip 后不会继续执行旧序章 Tween 或协程。
9. 正常播放结束后能进入叔叔信件。
10. 序章期间 Esc 不会关闭演出或打开暂停菜单。
11. 序章期间不推进 DayFlow、不写存档。
12. 旧存档恢复不会重新播放序章。
13. 重复开始新游戏时不会出现多个 Panel、多个 Skip 回调或旧文字残留。
14. `Time.timeScale = 0` 时，序章仍能正常播放、点击和 Skip。

## 10. 验收标准

本需求完成的标准：

- 玩家点击「开始游戏」后，首先看到完整黑屏序章。
- 开场文案严格使用最终文案，全部文字保留。
- 序章通过不超过 10 次普通点击完成。
- 玩家不能通过普通输入跳过序章，但可以点击画面上的 Skip。
- Skip 和正常完成都会可靠进入叔叔信件，且只进入一次。
- 开场序章是独立 Prefab，文字和基础演出参数可以在 Unity Inspector 修改。
- 不影响现有叔叔信件、第一天流程、NCS 对话和新手教程。
- 共享 PresentationDebugScene 中有可重复使用的预览入口。
