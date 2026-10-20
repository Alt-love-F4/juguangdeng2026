# 后台剧情自动测试：本次验证结果

## 实现验证

- Unity 6000.3.5f2 完成全量编译和域重载，最终桥接恢复 ready；测试结束恢复 Edit Mode，未修改当前 CoffeeShop 场景。
- 游戏内全局GM自动挂载 `NarrativeAutoGM` 与 `NarrativeAutoRunner`，不需要手工挂组件。
- Play Mode 实际执行 IF0：真实选择拒绝与离开，提交 `arrival.leave`，接受 `arrival.ending.if0`，真实 ResultPanel 显示，并在 `WaitForReview=true` 时停留，正式时间缩放为0。
- 实际PNG检查：标题、Ending ID、现有占位插画和已展开结局正文均出现在截图；工具实时窗被隐藏，没有仅保存空白或上一帧。
- 日常音乐在Play Mode实际基线观测为 `music.track.阳光的音乐`；两首候选的随机及override逻辑已编译，未进行主观听感验收。

## 自动运行结果

### 离线完整63预设

复用原项目Controller、生产对话游标、真实结果事务与CafeWorld；仅Resources/JSON等Unity基础设施使用文件适配。原NUnit断言：后台剧情和宿主合计34/34通过，咖啡领域12/12通过。

| 分类 | 轮数 | 命中目标 | 结局种类 |
|---|---:|---:|---:|
| 第1天起的辅助策略 | 48 | 21 | 7 |
| 明示历史起点 | 15 | 12 | 12 |
| 合计 | 63 | 33 | 13（去重） |

其余：24轮因阵营资格没有生产者在11/11锁定，3轮中立D到65天未产生结局，3轮历史起点保留作者施工阻塞。完整离线记录保存在 `Library/Locus/tmp/story-auto/final-batch-results.json`。

### Unity Play Mode实际31轮快速回归

实际报告目录：

`C:/Users/AltF4/AppData/LocalLow/DefaultCompany/聚光灯2026/NarrativeAutoTests/20261020-055738-619/`

- 31/31轮结束，没有留存运行中的后台会话。
- 19轮真实接受结局，去重13种；其中7轮从第1天起，12轮为明示历史场景。
- 8轮明确blocked、1轮达到65天no-ending、3轮content-blocked。
- 本批成功保存18张真实结局PNG；一轮信任的界限开局策略因Editor渲染/焦点切换未收到截图，错误已入报告；同一结局历史场景截图成功。
- 随后已补充“排队截图超时→当前已渲染backbuffer重试”，Unity编译通过。该重试分支尚未再次进行故障注入复现，不把本批失败改记为成功。
- `manifest.json`、`summary.csv`、每轮JSON/日志与PNG均实际落盘；可从GM“打开结果目录”查看。

实际出现的13种结局：

- `arrival.ending.if0`
- `church.ending.if2`、`church.ending.if4`
- `neutral.ending.a`、`neutral.ending.b`、`neutral.ending.c`、`neutral.ending.d`
- `revolution.end01`、`revolution.end02`
- `rose.ending.job_requirements.ending`、`rose.ending.trust_limit.ending`
- `rose.route.ending.bloom.ending`、`rose.route.ending.unforgiven.ending`

没有运行通过的3种：`church.ending.ry1`、`church.ending.ry2`、`rose.route.1115.s1.ending.ideal_world`，均保留明确作者阻塞。

## 验证边界

- “出现13种结局”包含历史注入场景，不代表13种都能从新游戏自然获得。
- 所有开局策略均有明确后台数值／制作／解锁辅助，不认证玩家合法成长、操作难度或经济平衡。
- 工作区 Unity Test 工具被禁用；没有绕过设置调用Unity Test Framework。以上NUnit为实际离线执行，Play Mode为实际功能运行核验，不冒充Editor测试套件全绿。
- 音乐3个新增NUnit用例静态编译通过，但未运行；Play Mode只核对真实活动基线，不替代听感验收。
- 结局插画中的占位卡按现有资产真实显示，未擅自替换为最终美术。
