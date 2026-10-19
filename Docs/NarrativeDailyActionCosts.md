# 地图日常“分段任务”和重复扣点审计

日期：2026-10-19。

## 用户反馈与确认结论

反馈：市政厅“办咖啡馆手续”每次短对话结束回到地图，再执行才有下一段，担心一个任务被拆分重复扣行动点。

源稿 `策划文件/文案/b-地图事件-日常/日常对话_270组.xlsx`：

- 总览“调用边界”要求点击地点后选择日常行动，播放对应段落，行动结束后结算地点属性；日期是解锁条件，新闻当日替换一次普通行动台词。
- 市政厅有24个独立短事件（含2个选择事件），同名“办咖啡馆的手续”。CIV-01是初次办理，CIV-R01明确“复访”；CIV-02在10月15日查传单消息后、CIV-03在10月23日增税后、CIV-04/CIV-17/CIV-18在11月5日后；CIV-22在11月16日后。不能把全部段落串成一次当前日期的长任务。
- 正式表每地只有一个Repeat日常Action，要求ActionPoint(1)，resultEvents是内容池，不是要逐一付款完成的任务清单。市政厅27根=24日常+2新闻+1首访；九地均已只读审计。
- 现有运行时先抽出“未播放首访+一则当前可用日常/新闻”，在一次Execute里只调用一次ConsumeRequirements。首访和正文自动续接；正文的多页、选项回应及QUEUE_EVENT续接均没有再调用Execute或ConsumeRequirements。
- 本轮未复现同一次行动自动多次扣点；重复点击“执行”确实会开始新一回独立造访并重新收费，这是Repeat定义，不应误报为已修复的扣点bug。

源稿没有给出普通活动成长数值或AP成本的具体数值；ActionPoint(1)和十分点成长是此前集成采用的实现预设。不能把它们冒称为策划逐条明定的成本。

## 实际问题与修复

1. 标题相同且待办持续“可进行”，把重复日常表现成没做完的多段任务。现在可选日常的待办在成功后标“本次已完成 · 自选再次行动另计消耗”，地图按钮变“再次行动”，不再暗示继续完成同一条任务。
2. 行动卡说明明确首访/正文自动续接，首访/翻页/选项回应不再额外收费；再次行动是新造访。完成toast再次说明该边界。
3. 可选日常在旧Todo投影中preventRest=true，HasActionableContent也把它当待完成事项，可能诱导玩家耗光AP才休息。现在仅现有Repeat、非mustExecute、确认为dailyPool的活动不阻止休息；真正必做、Once、Daily、交付、进度和教程门禁不变。
4. 复用已有HasExecutedToday，不新增任务进度、计费状态或存档字段。结果、条件、源Excel/生成JSON、原始文案均未修改，不统一串接所有CIV，也不取消独立重复活动的正常费用。

## 文件与边界

- `Assets/Scripts/Narrative/NarrativeActionController.cs`：新增纯查询IsDailyPoolAction复用现有UsesDailyPool；没有改扣点、退款或调度实现。
- `Assets/Scripts/GameFlow/UI/MapActionDetailsView.cs`：活动类型、首次说明、再次行动按钮及完成提示；不改已有地图布局/Prefabs，保留本轮之前其他人的改动。
- `Assets/Scripts/GameFlow/UI/GameplayTodoProjection.cs`：可选日常完成/休息语义，其余任务仍按原规则。
- `Assets/Scripts/Narrative/Tests/NarrativeDailyContentTests.cs`：九地生成表的首次到访+正文一次执行一次AP；双击、迟到回调、取消后退款、保留首访事实、重试跳过已完成首访、QUEUE_EVENT续接不另扣点。
- `Assets/Scripts/GameFlow/UI/Tests/MapUiRepairTests.cs`：Repeat日常不是未完成任务、可休息且下次仍可付费、按钮显示再次行动。
- `Assets/Scripts/GameFlow/UI/Presentation/Tests/NarrativeMapDailyPanelTests.cs`：真实DialoguePanel九地首访连续正文单次计费、市政厅CIV08真实选项A只进A回应、取消无成长/退款。
- `Assets/Scripts/Narrative/Editor/Tests/Spotlight.Narrative.Editor.Tests.asmdef`：当前美术测试已直接引用GameFlow.UI的NarrativeStageVisuals但缺程序集引用，补一条引用使Unity可编译。没有改其他人的美术测试/资源绑定。

测试使用已有共享PresentationDebugScene，正文、技能前置、人物、日期显式准备，不是自然全程通关证明；业务关店回归使用现有正式地图/咖啡馆。

## 验证记录

- EditMode最终101/101通过，job 09c7d25680e94908a7c7da406167a827；包含DailyContent、Action、BatchContinuity、MapUiRepair。核对新九地用例及新UI用例实际被发现执行，不能拿旧程序集结果代替。
- PlayMode日常真实面板3/3通过，job ecd7d78ff1594bac8065e86ced98ace0：九地普通中断、九地首访+正文、市政厅选择+回应。
- 最终PlayMode **4/4通过**，job ee1c0041e31f4124b158159a29fc4862：上述3项+正式营业→地图行动关店handoff，确认挂起请求恢复后只执行一次、不跳日期。fullName及NUnit XML已核对，见 output/narrative-daily-action-audit/playmode-final.json / playmode-final.xml。
- 源稿摘录、九地Action/内容池统计及市政厅每根条件见 output/narrative-daily-action-audit/source-audit.json。

## 不做的改变

- 不退款用户此前主动开始的多次独立造访：现存档只有整体行动/AP和事件历史，没有足够证据区分合法重复与误操作，不能凭猜测补点或重置数值。
- 不把公开消息未来日期内容、互斥选项、一次性选择事件全拼到首访里。
- 不把正常重复活动偷偷改为每日一次/免费；若要改变日常长度/单次AP成本或同日次数，需要明确新的玩法要求再改源表与官方Luban生成。
