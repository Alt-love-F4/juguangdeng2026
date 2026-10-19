# 文案6点集成记录（2026-10-19）

## 数据来源与差异

正式编译器现从 `策划文件/文案6点/文案` 读取当前原稿；旧 `策划文件/文案` 不再作为本批输入。所有表仍使用现有 NCS Excel、编译模块、`integrate_content.py`、`merge_workbooks.mjs` 和官方 Luban，没有新建第二套运行时剧情源。

- 中立线 16 份和革命联合 12 份与旧版逐字节相同。只更新来源目录和哈希审计入口，不重复添加 Root/Ending。
- 新玫瑰线 17 份无新增/删除 Root。四份有差异：Short Ending 1 标点/作者标签、11/15 理想世界结尾明确改成“十五年未发生叛乱”、True Ending 若干正文精修、BAD Ending 标签修正。全部复用现有 Text key 与 Ending ID；11/15 仍受坦诚值与知觉失败回接未定的根闸门保护。
- 王庭教会线 16 份无新增/删除 Root 或 Ending。10/23 处死稿从 `if2` 改名标注为 `if3`、11/11 说明补全结局名，都保持原稳定 Root/Ending ID。11/4 新稿把原 P096 价格句断在 P097；编译器将两个 DOCX 锚点合成原 P096 Text key，后续锚点保持旧编号，并在审计中保留原 480 个源段证据。
- 11/4 新 P061/P064 明确收取 50 王国币，故第三个向导报价按原结果所有者接入 `MONEY_ADD cafe 50`，选项可触发；不扩散到同一稿内市场外套价格。12 个市场 `xx` Text key 已由当前工作区 Luban 填为 120，合并器只对仍含 `xx` 的新值保留这些具体映射。新的、非占位玫瑰文本仍正常覆盖旧值。
- 新版日常表仍为 270 组、代码集合不变；203 组有文本/结构改动（188 组正文/结构、15 组标点或空白），67 组不变。240 个地图根继续使用 Conversation，30 个咖啡馆快照仍是非模态入口。教堂新名“圣卷大教堂”映射回已有地图位置，不创建新地图位置。
- 新版日常 NEWS 反应现在使用各期报纸真实完成事实和作者给定日期窗口。10/14 的窗口剔除 10/16–18，NEWS-08 需仪式尚未举行，NEWS-11 需 OLD-02（救济开始后旧城实际行动）已完成。报纸本身仍在原阅读器中触发。
- 六期报纸 Word、开幕/教程 Word、设定参考表均无需重复注入。设定文件不作为整表可玩剧情；咖啡馆对话两本表是不同内容但与现有订单/闲谈功能有重叠，本轮未追加第二套池。

## 尚未接入的咖啡馆内容

专用“咖啡厅中立对话”表不能整体拷入订单选择：饮品口味、糖量、温度等触发要匹配真实订单。新版 20 组闲谈快照继续保留现有格式和条件备注，但正式运行时目前仅有 `NarrativeCafeAmbientContent.TryOrder` 的点单 Hint/Toast 调用，没有闲谈选择/调度入口。将来应在现有非模态 Hint/Toast 所有者中补带条件的选择，不打开模态 DialoguePanel，不暂停顾客耐心，不复制咖啡互动。

## 结局与角色槽位

本批没有新增结局 ID。玫瑰理想世界复用原 Ending，并已将结算摘要同步新版“十五年未发生叛乱”；开场 IF0 与四线既有结局保持原 NCS 根尾/Ending/ResultPanel 边界。作者未定的三条终局闸门及教会 RY1123 歧义不因文案覆盖自动解锁。

`DialoguePanel` 在正式台词播放前按稳定角色 ID 做舞台分配：同一角色保留原槽，新角色先占空槽；三槽满后再使用配表请求槽替换。首句角色不提前出场、显式退场和中心特效目标仍走原机制；选项回应也使用同一分配器。回归用例位于 `PresentationEffectsTests.NewSpeakersFillEmptyPortraitSlotsAndKeepTheirAssignedPosition`，覆盖玩家 + 两位同侧来客、重复台词和三槽满后的替换。

美术仍只使用已登记的本地白底/素色需求卡，不调用图像 API。需求卡不代表正式资源或演出验收。

## 验证与接手边界

- 纯内容编译与图遍历回归：当前 138 项通过（以 2026-10-19 最后一次全套运行记录为准）。
- 唯一源表合并预检无缺失跨模块 QueueEvent；保留 12 个已有具体金额 Text 映射。
- 官方 Player semantic validation 与 Luban strict 已成功；当前配置量级为 351 Event、5727 ConversationPage、9147 ConversationLine、300 Choice、36 AbilityCheck、18 Ending、11791 Text。18 条 Ending 中 16 启用、2 条历史测试禁用；这不是 16 条全部自然可达的证明。
- Unity 定向验证：`NarrativeConfigurationValidatorTests.ActualGeneratedLubanDataLoadsAndValidates` EditMode **1/1通过**；`NarrativeConfigurationValidatorTests` 整组 Editor **25/25通过**（2026-10-20）；共享 `PresentationDebugScene` 中 `PresentationEffectsTests` 整组 PlayMode **17/17通过**，包括自动补位、同角色表情更新、双人换位和效果通道。修复项包括同角色已占请求槽时保持槽位不动。`NarrativeArtPlaceholderTests` 的全限定类型修复已编译；不把较早日志中的失败记录当成本次最终结果。
- 没有跑全套 Unity 测试、目标平台 Player Build、新游戏逐日自然通关或所有结局路径；咖啡馆闲谈正式调用入口仍未实现。定向通过不能外推成整个剧情全部验收。
- 后续 Agent 必须分别记录静态配置、编译、Unity 测试和自然通关，不能把前者合并成“所有剧情均正常”。
