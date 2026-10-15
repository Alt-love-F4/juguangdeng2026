---
id: kd_ab81edbe-c976-4964-8773-d82dbc5bfe4e
injectMode: inherit
injectAgents:
- unity
aiEditMode: inherit
---

# 初来乍到实施检查点

2026-10-15。任务尚未完成，主规格为 `Docs/FirstArrivalAndPassiveChecksSpec.md`。

## 本检查点已验证

- 修正 `Assets/ConfigTables/configure_first_arrival.py`：删除正文中的出场制作说明，保留无引号心理活动的 player 身份，恢复拒绝结局标题。
- 新增三个匹配实际用途的表内模板：Tpl_ArrivalConversation、Tpl_PassiveCheck、Tpl_NarrativeSignal。避免虚构 lines 文本键和缺失／未声明槽位。顾客 B 自动检定和结果通知使用对应模板。
- 已修改 Excel 并通过正式 GenerateAll.bat 生成；Unity 导入后 NarrativeConfigurationValidator.Validate 返回空列表。
- 更新真实配置测试的模板数与三个模板存在断言。完整 Unity 编译及 domain reload 成功。
- 测试 run c8e5b121ce454ef9b90c81c90f9d5564：FirstArrivalTests、NarrativeAbilityCheckTests、ActualGeneratedLubanDataLoadsAndValidates，共 6 项通过。
- 导入脚本重复执行后两份工作簿所有 sheet 值相同，已验证幂等性。

## 尚需完成

- 正式 conversation 页面条件：当前 PageVisible 的 pageBarConds 抛错，pageCondKeys 临时使用状态 bool；需依据项目约定实现并测试。
- 对话取消：当前 Sequence 取消后外层 PlayEvent 仍可能发 Finished；legacy conversation 路径缺取消处理。检查 sessionVersion 和所有完成通知，避免旧会话推进新会话。
- 副文本每页都从 request.PassiveHint 注入，会在多页成功事件重复显示；适配器还有重复首行克隆。应仅第一个实际正文首框显示。
- 正式播放器预扫描角色并用最后出现的角色初始化，可能让阿黛尔提前出现；须核查渐入及恶意效果。街道、门口、恶意已有占位背景绑定，未截图验收。
- `arrival.complete` / `arrival.leave` 暂保留 Tpl_Narration setupType，因为 loader 不允许无结果无表现事件；目前内容为空，应核查是否闪 UI。
- 共享 PresentationDebugScene 预览尚未实现；不能启动 DayFlow 或结算真实 NCS。
- 正式新游戏日期→arrival.start→地图→教程全链路未实测；顾客 B 提示、取消／重开／快进／暂停等验收未完成。
- 之前全量 265 测试有 44 失败，不得称全部与任务无关。多数 loader 测试夹具缺 tbncsabilitycheck；另有 EditMode DontDestroyOnLoad、浮点精度及验证器测试失败，需逐项归因，修复范围内回归。
- Git 状态含未知 `.workbuddy/` 和 InitTestScene4beb48bf-7d65-4daf-8107-21956721e1c9.unity，未清理。保留既有用户改动。
