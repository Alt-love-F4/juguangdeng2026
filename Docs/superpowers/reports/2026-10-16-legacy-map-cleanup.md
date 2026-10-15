# 正式地图遗留 NCS 测试入口清理

用户在地图点击“与B见面／前往约定地点与B见面”后请求删减及处理卡住问题。

## 核实与修改

- 现场为 day2 / CityMapLeisure、currentEvent=EV_ACT_MEET、pauseOwner=1、busy=true，Presenter.hostSuspended=true。配置目标是旧占位人物 npc_b，正文为旧 A/B/C/D 示例，与 oct07 正式文案无关；首日顾客 B 教程不是这组人物。
- `Assets/ConfigTables/Data/Narrative/Narrative.xlsx`：清空 market/temporary 的 defaultAction；移除 act_meet_b、act_c_with_b 两项 Action 定义，使旧存档 RestoreFrom 也按 Get(action)==null 过滤遗留挂载；撤下 EV_TALK_CAFE、EV_REVOLUTION 两条日期入口，禁用 T_CAL_D60。其余未挂载演示定义保留。
- 保留正式 arrival、首日顾客教程、oct07.cafe.s1、oct07.s2 及 story.oct09.meeting.known。后者只代表获知正式约定，不生成此次旧测试行动。
- 卡住根因：CanUseNarrativePresentation 硬要求地图关闭，但测试行动在地图上执行，Presenter暂停而行动等待播放完成。改为活跃行动执行期间允许地图上的叙事推进，同时关闭地图操作；暂停、加载、详情和非游戏会话继续阻止演出，不按事件ID写特例。

## 验证

- 官方 GenerateAll.bat 最终 2026-10-16 02:31:41 exit0；Player semantic、Luban validation通过。生成输出通过 ForceUpdate 导入，未手改生成JSON/C#。
- 统一编译首轮遇到并发测试类重名错误；再次检查磁盘只有一个 PlayerCafeOwnerTests，另一个已为 PlayerCafeOwnerMigrationTests；未覆盖他人文件，重新收敛后 bridge ready，定向断言可执行。
- Editor内直接NUnit/TestCase：4/4通过（生成配置校验、旧档挂载过滤、地图已打开/正在打开的叙事与阻断条件两例）。完整UTF runner未执行。
- 当前Play Mode只读核实：旧两Action不存在，market/temporary挂载为空，cafe仍挂载oct07.cafe.s1；日期仅oct07.s2/day2/night。当前正式首日 arrival 对话仍运行，Console error=0，未打断。
- 观察期间会话从原卡住状态切到菜单及后续新游戏；本轮未成功对旧会话实施恢复或AP退款，未宣称恢复该份临时运行进度。未覆盖用户存档、未提交Git，其他会话修改保留。

修改前工作簿备份在 `Library/Locus/tmp/legacy-map-cleanup/Narrative-before-cleanup-*.xlsx`；这是临时备份，不是配置源。此前10月7日验收报告中的“day3旧EV_TALK_CAFE选项”为当时配置历史记录，此次已撤下该旧日期入口。
