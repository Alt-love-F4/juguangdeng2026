# 地图对白使用阅读式面板：原因与修正

日期：2026-10-19。此文件是当前呈现约定；原稿总览的旧约定不再作为地图对白渲染依据。

## 确认的原因与证据边界

- 原稿 `策划文件/文案/b-地图事件-日常/日常对话_270组.xlsx` 的总览 **B22** 指定普通段落使用阅读、20组选择事件使用对话。先前导入器确实照此实现，而不是 Presenter 把 Conversation 错分到 Notice。
- `Tools/Narrative/Content/daily.py` 先前将196个普通日常、9个首次到访、15个新闻反应，共220根配置为 Reading；每句还写成 `mode=narration` 和 `角色名：正文`。ReadingPanel 因而没有正常角色槽位，视觉容易被理解为通知。
- 正式 `MapSceneController` 分别绑定 Conversation、Reading、Notice 到各自现有 bridge。`MapActionDetailsView.Notice` 是操作失败 toast，不负责承载这些故事正文。
- 排查时用户已经退出 Play Mode，活动场景为启动场景。**没有捕获到用户刚才那条的 live eventId/panel 实例，因此不宣称实际实例一定是 NoticePanel。** 本次修正针对确认存在的220根阅读式对白配置。

## 本次变更

1. 240个地图日常/首次到访/新闻反应根统一走现有 Conversation + DialoguePanel：220个迁移，原有20组选择事件保持。
2. 保留原根ID、条件、日期、AP成本、池轮换、成长及选择回应。每句原文、说话人、左右槽位分别入表；入场/动作仍是旁白。普通段保留已有Text key，去掉正文中的姓名前缀，姓名交由对白角色栏显示。
3. 每个迁移页显式带对应地点背景。没有更改 `scene.cafe` 或店内特殊CG规则。
4. 泛称“教会帮工”建立独立无肖像Object `daily.actor.church_helper`，不借用命名角色。任何新增未知speaker在编译时抛错，不能静默降为旁白。
5. 真正的六期报纸继续Reading，九条公共公告继续Notice；地图人物对新闻的反应是Conversation，两者不可混为一类。咖啡馆30组非模态点单/闲谈快照保持。
6. 本次没有修改原始文案工作簿、剧情调度/咖啡馆生产代码、Panel Prefab、存档格式或图片。没有调用图片API。

## 单一生成链与实际差异

只运行 `integrate_content.py --modules daily`，保留已补接的四条主线/CG，不全量覆盖其他模块。然后使用现有artifact-tool写入正式源Excel，再运行官方 `Assets/ConfigTables/GenerateAll.bat`，没有手改JSON。

- Event/PresentationIndex各220条类型改变。
- ConversationPage新增220，ConversationLine新增869；相应ReadingPage/ReadingLine删除220/869。
- Object新增1。Text新增650个说话人显示名称；原日常正文改为无姓名前缀，中央默认项部分comment重写，但非日常Text的正文内容不变。
- Result/Condition/Action/Choice/Ending、已有ConversationPage/Line均无语义改动。
- 正式加载快照：346 Event；5727 ConversationPage / 9031 ConversationLine；14 ReadingPage / 180 ReadingLine；9 Notice；240 daily根全部Conversation。

备份、官方生成日志、focused merge-plan、source-semantic-diff及测试结果位于 `output/narrative-map-dialogue-fix/`。`output/narrative-integration/content-audit.json` 已只读重建全部模块源审计；focused计划单独保存在本次目录中。

## 验证

- Python内容测试：129/129通过，其中daily14项；逐源核对全部240地图根、958行地图台词/回应、40个选择回应页、报纸DOCX日期/全文、公告和咖啡馆快照。
- 官方Player语义校验和Luban strict生成通过。Luban提示清空后的Reading表尾部连续空行可优化导出性能；不是结构错误。
- Unity实际 `LubanNarrativeLoader.Load` 加载上述新快照；IntegrationReport GlobalErrors=0、Warnings=0。原来3个建设阻塞根的4条原因仍保留，不作假放行。
- EditMode：57/57通过（DailyContent、LubanLoader、ReadingReport）。
- 旧Reading Presenter PlayMode回归：6/6通过。
- 新增地图真实面板PlayMode：1/1通过（一个用例内逐个验证九个地点的普通日常；真实DialoguePanel的正文和姓名均匹配，无Reading/Notice调用；host中断退AP、无成长/完成/轮换记录，迟到回调不能补结算）。job `0ce2352962e449548f1158a728bf3ce6`。
- 旧Reading回归job `70406292ed8547e592dfb1e98a075479` 的6项不含新增地图用例；已另行等待新测试编译后单独执行，不能把旧6项误记成地图通过。EditMode job `91c38dcb88f645a1b0cd39e23f3c6baa`。
- 排查开始时Console已有两条MapSceneController被销毁后访问的异常；本次没有修改其生命周期，不宣称该历史异常已修复。

## 接手与重测

- 新测试在 `Assets/Scripts/GameFlow/UI/Presentation/Tests/NarrativeMapDailyPanelTests.cs`。仅加载既有PresentationDebugScene，不能新建测试Scene/Canvas。
- 测试显式准备day26、已相识人物、四处主线前置和首访历史，再从真实 `NarrativeActionController.Execute` 调用九地普通日常。不是新游戏自然可达/全程通关证据。
- 修改或生成文件后先等待Unity完成Assets/Refresh及编译。确认新测试确实被发现执行；只看到旧Reading测试通过不能算地图面板通过。
- 用户复测需重新进入Play Mode/新建NCS会话，以加载新表；不要清用户存档，也不要为了切面板补发事件或成长。已完成的once首访不应重放。
- 若仍出现类似情况，先记录地点/行动名/游戏日期，再读取实际eventId、presentationClass、正在播放的Panel类型，区分公告、操作toast、读报和地图对白。
