# 窗口 C 革命联合施工与验收

日期：2026-10-16。当前状态：三篇源表与官方生成已完成，独立共享场景正文矩阵已实跑；三篇均因正式剧情/数值依赖继续阻塞，不能正式放出。C29 空稿不施工。

## 已有工程证据

10 月 7 日已有 NCS、入口、Inline、通用信件、取消/存档/日程证据见 `2026-10-15-october07-acceptance.md`。这些不作为 C 批正式入口和奖励验收。

## 本批新增配置与生成证据

- 已完整读取 spec/source-map/handoff、AGENTS、旧报告与工作树。444 个原段与 DOCX 逐段相同，三份 SHA256 与 source-map 一致；正文没有写入 C#。
- 现有 Narrative/Text 源表追加 C13 52 对白行/3 选择，C28 144/3，C14 171/12；C14 P017 仅 ReadingLine。三根 onceOnly、blocksSave，完成 bool 默认 false/onlySetTrue；未发出缺正式数值的部分结果或伪完成。
- 正式 schema/运行时最小扩展：逐行 speakerNameKey；同根 choice/anyChoice 页条件；ATTRIBUTE_GTE 通过真实 AbilityReader，并缓存活动选择的读数/判断，所有根清理边界清缓存；现有演出通道末尾 FadeOut 等待完成后释放根舞台。
- C14 c2.3 的 `ATTRIBUTE_GTE courage 6` 已入源表。C13/C28 空正文最终 FadeOut 已入源表；C13 P063/C28 P150 使用现有退场效果；C14 P136 使用现有背景平移。静态复核后补正 C14 P007 信图示页序（位于 P006 与 P008/首选项之间），并将 P216 的索雷尔退场边界映射为 `fx_portrait_fade_out/sorel`。
- 最初 02:45 官方生成因并行 B 窗 eventKind=narrative 失败。B 窗随后修为 actor。本窗口于 03:01:23 首次重跑 `Assets/ConfigTables/GenerateAll.bat`，并于 03:12:49 在 C14 补丁后再次运行；两次均 exit 0，Player semantic 与 Luban strict 通过。03:12:49 生成物随后强制导入 Unity；此前失败输出不算通过证据。
- 统一编译首次发现 Narrative 反向引用 GameFlow 程序集问题，修为现有 DTO 的 fadeStage 回调，由 GameFlow 适配层执行。修后 compilation completed/domain_reload completed/bridge ready；ForceUpdate 导入 JSON 后 loader 成功，确认三根和两个最终 FadeOut。

## 本批共享场景运行证据

均在 `Assets/Scenes/PresentationDebugScene.unity`，通过现有 NarrativeChapterPreview/独立 NarrativeDebugSession、正式生成内容执行；没有正式奖励和完成结果。历史/复合条件为显式测试夹具，不能作为正式语义裁定。

|事件|新增运行证据|尚未验证/阻塞|
|---|---|---|
|C13|最终生成物下三选择分别只出现 P043/P047/P051 对应回复，均到 P065；三路各选择一次、自然结束且最终 FadeOut 完成，暂停 owner=0。开头取消后清场、blocksSave 活动中为 true、重新隔离预览可重试|C-B01 A真实完成/唯一续接及同日恢复、C-B02密信范围、C-B05好感正式绑定未解决；正式入口/好感+1/一次性/存读档/原日程未验|
|C28|历史有/无×革命C13完成真/假×三选项共12路全部通过：P036仅历史夹具真，P093仅革命C13完成真，选择仅1次，各到P155，自然结束且最终FadeOut完成，pause=0。取消重试通过|历史权威身份C-B06、好感C-B05未解决。眼部遮框/放大、准确夜环境音、已有BGM淡弱、表情差分未完成精确演出；正式数值/存档/BeforeNight日程未验|
|C14|高属性路线读完整P017后到P018，c2.3/c4.3/c5.1、P078/P116/P175及复合测试正文P192、P219均出现；名牌P026=？？、P045=？？？、P047=索雷尔；结束pause=0。低属性wisdom3/perception5/courage5下c4四项×c5三项12路均完成到P219，勇气第三项拒绝选择，P078/P116/P175/P192均不出现，pause=0。取消重试通过。补丁后静态 JSON 确认 P007 图示顺序和 P216 索雷尔退场映射|P191作用域C-B03、读T0或T0+Δ C-B04、正式数值C-B05、B真实完成生产者未解决。历史专精与 reciprocity guard 仍是依赖阻塞；补丁后已追加 c2.1/c4.1/c5.2 路径的 wisdom3/4/5 三路：P078 仅 wisdom≥4，P175 仅 wisdom≥5，reciprocity=false 均跳过 P192；三路均确认图示先于 P008、读信完成、四次选择到 P219、pause=0。Reveal 与 Reading 阶段分别取消均恢复 letter/dialogue/generic=false、pause=0，并可重试。演出精确矩阵仍待补。正式情报/倾向/信任/选项事实/联系事实不提交，正式MorningBeforeMap/存档日程未验|

直接调用新增 NUnit 方法并执行 Setup/Cleanup：ATTRIBUTE_GTE courage5失败/6成功、UI与选择同快照/根结束重读、未知/未就绪/非法值三组通过。这是 Editor 内直接调用，不是完整 UTF runner。

三根正式 ExplainEvent 均返回明确施工阻塞；预览不会开放正式事件。静态核验确认 C 页 seq 无重复、C28 闪回只读 C13 完成事实、C14 三项属性 Inline 与 courage6 条件正确。新增全量 Excel↔Luban JSON 审计：按主键归属共 849 个目标行、7,456 个 schema 导出字段，缺行/多行/字段差异均为 0；386 条正文 Text 逐字一致，Text 全表 2,436 个 key 唯一，P017 仅 Reading。该审计仍不等同正式剧情依赖、事务提交或演出资源验收。一次共享矩阵遇到其它窗口脚本重载 `domain_reload_interrupted`，该轮丢弃，C28后续12路是重跑有效结果。共享Editor出现 neutral.1008.s1 演出时已让出，没有停止别窗预览。

## 续做检查与局部演出（03:37）

- 全量只读审计：C13/C28/C14 分别 125/314/410 个目标行，合计 849 行、7,456 个 schema 导出字段；Excel↔JSON 无缺行、多行或字段差异。386 条正文 Text（55/147/184）逐字匹配 source-map 允许转换；P017 每侧 Text 1、ReadingLine 1、ConversationLine 0、Choice 0。审计没有重新提取 DOCX。
- C14 补丁后智慧 3/4/5 路线均到 P219，P078 仅≥4、P175 仅≥5；均确认图示先于 P008、信纸读完、四次选择、reciprocity=false 跳过 P192、pause=0。Reveal 和 Reading 两处取消后 dialogue/letterPlaying/generic 均 false、pause=0，并可重试。
- 已实现 `fx_dialogue_shake` 独立通道，复用配置中的轻震参数，只移动 dialogueBox，不移动 EffectStage；P188 源表和 JSON 已绑定此效果。03:37:16 官方 GenerateAll exit0，Player semantic/Luban validation 通过；Unity 编译同步报告 up_to_date/bridge ready，导入生成物成功。
- 实际程序集独立对象验证通过：局部位移且舞台不动、自然完成只回调一次、替换旧效果取消、Reset 恢复对话框初始位置。新增 PlayMode 测试源码未通过完整 UTF runner 执行；尚未追加 P188 最终整章视觉回归。拍打声音仍为空，未用其它音效代替。
- 03:41 新增 `fx_background_pan_left_to_right` 并绑定 P136，保留旧往复效果语义。新效果复用原背景参数与通道，缩放保持边缘覆盖，横移由负到正单调推进；只影响背景。03:41:07 官方生成通过，Unity 编译同步 up_to_date/bridge ready，生成物强制导入。实际程序集检查通过单调运动、边缘覆盖、舞台/立绘不动、完成及 Reset 位置/缩放恢复。新增效果的最终整章视觉回归仍待执行。
- 精确效果余项：P088 缺正式索雷尔立绘/眼部参数；C28 局部放大遮眼与进入/退出恢复未实现；P149 BGM 淡弱未接事件所有权边界。门铃/脚步/拍打/准确夜环境音未找到可靠资源。

## 正式放行门

- C13：C-B01/02/05 与 G04 真完成续接/恢复、完整 V13。
- C28：C-B05/06 与遮眼/音频等精确演出、完整 V28。
- C14：B真实赴约完成，C-B03/04/05，批准后才补复合快照/条件结果集合，完整 V14。
- C29：0 非空原段；没有创建事件、日期、文本、完成或合作事实。C-B07/08 保留。

不把根的隔离 happened 标记等同正式 `story.*.completed`，不把本轮独立预览当正式奖励、一次性、存档或日程通过。没有运行用户存档槽，没有 commit/回滚/清理其它 Agent 改动。
