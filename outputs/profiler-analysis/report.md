# Unity Profiler 卡顿分析（2026-10-11）

## 结论与边界

当前 Map.unity 的采样表明，明显卡顿主要发生在 Unity EditorLoop / Application.Message。游戏主循环没有对应的百毫秒尖峰。消息处理内部未暴露足够的细分标记，因此不能据此断言具体是某个插件、Windows 消息、驱动或窗口交互导致。

使用已存在的 Unity MCP execute_code 桥接只读导出内存中的 RawFrameDataView。未修改游戏源码、场景或资源；临时将 ProfilerDriver.profileEditor 从 false 改为 true，读取后恢复 false。原有录制保持开启，Deep Profiling 保持关闭。

## 原始录制

- Unity 6000.3.5f2，Play Mode，Map.unity，Direct3D11。
- 读取时窗口选中帧为 276929，已不在滚动缓存中。本报告分析当时仍可读取的帧 280531–282530。
- 2,000 帧，共约 26.35 秒。
- 帧耗时均值 13.17 ms，中位数 9.04 ms，P95 17.99 ms，P99 133.93 ms，最大 168.57 ms。
- 超过 33.33 ms：79 帧；超过 50 ms：62 帧；超过 100 ms：41 帧。
- EditorLoop 平均 10.91 ms，最大 165.65 ms。
- PlayerLoop 平均 1.48 ms，最大 6.36 ms。
- Update.ScriptRunBehaviourUpdate 平均约 0.137 ms，最大 1.036 ms。
- PostLateUpdate.PlayerUpdateCanvases 平均约 0.139 ms，最大 1.267 ms。
- 最慢帧 281848：全帧 168.57 ms，EditorLoop 165.65 ms，PlayerLoop 1.93 ms。

## 展开编辑器后的补充录制

有效分析范围 284072–285148，共 1,077 帧、约 14.61 秒。284071 是开启编辑器采样命令所处的 2,051 ms 过渡帧，不纳入业务卡顿判断。

- 25 帧超过 100 ms，25 帧的 Application.Message 均超过 70 ms。
- Application.Message 最大 114.83 ms；保存的慢帧中，其子调用没有显著耗时，尚不能定位内部阻塞来源。
- 帧 284623：165.58 ms；Application.Message 108.42 ms；YokiFrameEditorFileBridgePump.OnEditorUpdate 36.50 ms；PlayerLoop 1.77 ms。
- GameView.Paint 平均约 4.11 ms；SceneView.Paint 平均约 2.91 ms。这些是包含子调用的时间，不能与嵌套 PlayerLoop 等项目重复相加。
- RepaintAllProfilerWindows 最大约 7.00 ms；Profiler.FlushMemoryCounters 平均约 0.724 ms。
- 未观察到与百毫秒尖峰匹配的大型托管 GC 标记；这不等于零分配或完全排除所有 GC 问题。

## 源码检查

YokiFrame 的 Core/Adapters/Unity/Editor/FileBridge/YokiFrameEditorFileBridgePump.cs:116 在 EditorApplication.update 上执行工具注册刷新、命令处理、心跳、遥测和存储清理。Publishing.cs 的 WriteHeartbeatState 同步刷新心跳、快照和 registry。36.5 ms 已定位到整个回调，内部哪个操作导致尖峰尚未单独打点，不能将其直接归因于某次磁盘写入。

Assets/Editor/GameFlow/DialogueVerificationTemp.cs:18 每次编辑器 update 查询测试请求文件。本轮均值约 0.095 ms、最大 0.745 ms，可作为低优先级清理项，不是当前主因。

## 下一步验证顺序

1. 在相同交互下最大化 Game 窗口、停止 Profiler 实时重绘，比较卡顿是否变化；这是待执行的对照实验。
2. 使用独立 Development Player 复现同一场景并连接 Profiler，以确认发布环境是否也出现卡顿。本轮未构建或测量 Player，也未完整测量 GPU。
3. 对 YokiFrame 编辑器桥接内部增加受维护方支持的采样点，或在独立对照中暂停桥接以确认尖峰贡献；本轮没有修改受管包或关闭服务。
4. 若 Application.Message 的百毫秒空白仍存在，使用原生线程栈 / 系统跟踪进一步确定消息处理与等待来源。

## 证据文件与统计方法

- current-capture.json：首次 2,000 帧及 marker 按帧聚合。
- editor-capture.json：临时展开编辑器采样的帧及 marker 聚合。
- slow-frame-details.json：仍在缓存中的 8 个慢帧的原始顺序、sample index、直接子节点数。
- 上述文件是 JSON 诊断导出，不是可直接导入 Unity Profiler 的二进制 .data 捕获。
- 聚合保留单次耗时 >= 0.05 ms 的标记，小标记统计会略低估；详细列表保留 >= 0.1 ms。时间为包含子节点的耗时，跨层级不可相加。
