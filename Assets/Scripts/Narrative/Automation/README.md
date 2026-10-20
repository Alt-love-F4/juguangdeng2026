# 后台剧情演出器

`NarrativeAutomationPresenter` 是纯 C# 的 `INarrativePresenter`，不是点击模拟，也不创建 UI。它不替代正式 GameFlow 宿主、不访问单例或存档。无 UI 的运行结果不能作为视觉验证。

```csharp
var plan = NarrativeAutomationPlan.ForEnding(config, targetEndingId);
var presenter = new NarrativeAutomationPresenter(config, plan);
var controller = new NarrativeController(config, presenter);
presenter.Bind(controller); // 自动连接 EffectRequested；仅一次
presenter.GameplayRequested += attempt =>
{
    // 宿主使用 attempt.Request / attempt.Data 启动真实领域操作。
    // 收到真实交付或取消回执时：
    // attempt.Complete(new NarrativeGameplayResult { passed = ..., cancelled = ... });
};
// 在宿主 Update/有界循环中调用；不在回调中递归推进。
presenter.Tick(32);
```

- `Play` / `Drain` 只排工作。`Tick(budget)` 返回消耗的工作单位，不超过预算；根、原子、页与完成回调分步运行。等待 Gameplay 回执时返回 0。重入 Tick 返回 0。
- 对话使用生产 `NarrativeConversationTraversal` 的根内显式边、隐藏页线性回落和重访上限。一次 `MoveNext` 内隐藏页扫描由生产游标总访问上限约束；budget 是推进单位数，不是耗时或逐条条件上限。
- 选项仅从当前组的已解锁选项选择，使用 `Controller.SelectChoice`；恢复存档中的已选组选项保留。Inline、主动/分支检定分别调用真实 Controller 服务，结果仅由根完成事务应用。
- Reading、Notice、Letter 和纯视觉原子快速完成；Notice 的 `barConds` 与正式演出一样作为显示信息，不新增条件含义。`pageCondKeys` 使用真实 guard（包括 `bar:key:ge:n` 数值 guard）。旧 `pageBarConds` 字段与正式 Presenter 一样明确拒绝，绝不忽略或自创解析。
- `GameplayRequested` 没有宿主时保持等待。`Complete` 幂等、复制结果且仅由后续 Tick 消费；取消/过期回执不提交结果。`GameplayCompleted` 由 Controller 自动订阅，产生当前根的临时 Gameplay guard，不写持久完成事实。宿主自己负责释放实际 Gameplay 资源。
- `Bind` 自动订阅 `EffectRequested`；`PlaySetup` 经预算式 `Drain` 处理，禁止含选项，其他纯表现效果只记日志。
- `ChoiceSelector` 返回合法 choiceId，返回 null 时回落到 plan。Context 提供 Request、Controller、PageId、GroupKey、Choices（只含合法项）、Scores。非法/锁定显式覆盖会取消并报错，不自动绕过。
- `CurrentPage` 提供事件、页、可见性、类型、选项组、选中项、原始 text keys 与 Gameplay 等待状态。`PageChanged` / `LogAdded` 供宿主实时观察；`Log` 是最近 2048 条的快照，建议订阅事件，不用绝对下标消费有界日志。
- `LastError` 保存错误信息；`Cancelled` 不等于 `Finished`，取消不提交根。Dispose 注销效果订阅并以 `disposed` 通知 Controller。

## 结局倾向计划

`ForEnding(config, id)` 为启用结局建立目标、路线及已知的明确选项覆盖；`ForRoute` 建立路线选择。`ChoiceOverrides` 与 `ChoiceWeights` 可自定义。配置视为构造后的只读快照；索引按配置对象缓存，换配置需新建 plan/presenter。

评分不改事实：检查真实 Choice resultOwner、根尾 result 的 choice/anyChoice guards、当前世界 guard，以及检定的纯 `InspectAbilityCheck` 预览；按照目标结局 BOOL 条件与路线事实的相关性打分，并有限深度回溯声明式生产者依赖。它是可解释的贪心倾向，不是完整路径证明，不保证远期可达。`Evaluate(controller)` 返回真实 ending、各条件诊断和最佳 OR 组进度，不凭目标名称宣称完成。成功/失败属性测试值由宿主明确提供，plan 不改能力数值。

原作者 `blockedBy`、construction/integration gates、早期竞争结局均保留。例如中立 1111 S1 当前无选项且无条件生产 A 的终末事实，B/C/D 的新开局运行可能被 A 先截获；不能靠跳根、关闭结局判断或写事实解决。历史场景能验证单根事务，不证明从第一天自然可达。

## 验证边界

新增 Core Tests 覆盖预算、1500 页、显式边、隐藏页、数字 guard、失败 Inline 回落、主动分支、选项锁定、无 UI 阅读/信件/通知、PlaySetup drain、真实 Gameplay 回执等待/取消/幂等/重入，以及只读计划评分和生产结局覆盖。可以在 Unity EditMode 中运行 `NarrativeAutomationPresenterTests` / `NarrativeAutomationPlanTests`；离线 harness 需要适配 Unity 的 Resources/Mathf/Debug/JSON，不能声称等同于 Unity UI、输入、资源导入或视觉验收。
