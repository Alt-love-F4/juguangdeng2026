# 玩家运行时与 NCS 数值实施验证记录

本轮已实际开发、生成、编译和运行验证，但未满足全部验收，不能作为全量验收通过报告。工作区原有大量未提交条件系统改动，过程中又出现其他 Agent 的 CafeVisit / 演出暂停修改；均保留，未回滚或提交 Git。

## 实施内容

- 新增 Player.xlsx 五表、Text、schema、Luban 生成产物及生成前语义校验。四属性正式内部 Bar 为 0..20；资金、行动点及正式日期为 external。
- PlayerRuntime / PlayerState 仅持有身份、稳定建角选项 ID、提交标志与草稿。旧 PlayerDetailsProfile 仅迁移 DTO。详情、头像和信件改接正式入口。
- PlayerSessionCoordinator 在初始化屏障内预检、提交奖励，异常恢复所有者；重复提交直接返回，不重复设余额/属性/专精。
- NCS 统一内部/external/derived 读取，provider 订阅、恢复基线、操作最终变化比较和安全边界消费。增加 MONEY_ADD/REMOVE 严格预检及 CafeWorld 业务接口。
- PlayerSaveBridge 优先新版，旧属性仅填真正缺失的 NCS 值，已有条件 true/false 优先；新档仅写新版玩家结构。
- CafeWorld 保留于游戏会话，地图阶段也注册经营存档。正式初始化/恢复先经过 CoffeeShop authored 配置建立所有者，再由现有 DayFlow 进入地图/经营；场景切换保留余额。
- 经营旧档评分 100.0000229 的实际浮点误差：迁移只将 (100,100.0001] 归一为 100 后继续严格校验，混合运算采用 double 中间值。
- 调试详情入口使用隔离占位快照，不启动正式玩家会话；沿用已有场景、控制器和 UI。

## 存档保护

测试前备份原有 save_0、save_1、save_2：
`Library/Locus/tmp/player-runtime-verification/original-saves/`

原自动档没有 freecafe.world.v1，不能安全推断资金。将备份原字节复制到独立 original-load-check/slots 后通过正式 ConfirmLoad(0) 验证，显示明确失败并停留 Menu；没有删除原档绕过迁移。

正式运行期间自动保存可能更新真实槽 0；原始字节仍在备份。新增手动槽 3 用于新格式往返、槽 4 用于草稿往返，未删除这些验证存档。

## 实际通过

- 多次 Luban strict 成功；玩家 Python 配置校验 5/5。
- Unity 完整编译通过；最后检查为 up_to_date。
- 第一轮 EditMode 208：167通过/41失败，修复旧表夹具及等待演出保存边界。
- 第二轮 EditMode 227/227通过。
- 合入过程中扩展经营回归，最后一次 EditMode 388：386通过/2失败。结果：`Library/Locus/tmp/player-runtime-verification/edit-final.json`。
- PresentationDebugScene 实际打开详情、三页切换、关闭重开；PlayerRuntime.Current 保持 null；信件预览可打开。
- 正式启动→信件填写五组、姓名→确认→地图→进入咖啡馆→开店演出与介绍→FreeCafeBusiness→结束营业→地图→下一天，实际日序/NCS calendar 均为2。
- 正式贵族建角余额1000、语言5/勇气2/智慧0/知觉2、宗教专精。修改语言为6、移除专精后详情即时刷新且关闭重开不复活。
- 新档保存→菜单→正式读取：姓名运行验证、余额1100、当天剩2行动点、verification.token数量2、语言6及专精移除状态保持。
- 草稿快照恢复回 IntroLetter，保留姓名和五个稳定选项ID，creationSubmitted=false；跳过后使用commoner/500基础结果。
- 旧slot2首次因咖啡评分微小越界拒绝，修复迁移后正式进入Playing，余额490，行动点5；保存快照见 legacy-slot2-restored.json。

## 失败与未完成

- 当前原自动档缺少经营快照，不能继续完整恢复；此为明确失败路径，不宣称兼容成功。
- 最终扩展EditMode仍有2项 RuntimeCharacterTests 失败：角色目录预期38实际8、独立访客测试NullReference。位于并发新增访客功能范围，未删除断言或改目录掩盖。
- 完整Presentation PlayMode运行中途报错，49通过/11失败；定向轮33通过/4失败后再次修复旧fixture。
- 最后 FirstDayIntroductionTests：10项，6通过/4失败；共同原因 Missing NCS opening presentation: first_day_cafe。最新配表/演出集成仍需修复并复测。结果 play-introduction-final.json。
- 未取得全部PlayMode回归通过。不得以较早227/227代表最终全工程通过。
- 旧slot2进入Playing后的详情复核被并发Editor/脚本重载打断，未完成稳定截图和二次保存读取。
- 三种出身均有真实领域所有者的Edit测试；仅贵族执行了完整正式UI路径，其他出身未逐个截图。
- crossing、derived、资金整批失败、初始化与恢复等逻辑由新增测试覆盖；没有逐个在正式场景可视化播放全部阈值案例。
- 最后 git diff --check 报现有/并发 Unity 字体与调试场景 YAML trailing whitespace；未对结构资产做原始文本清理。

## 产物

测试/状态：`Library/Locus/tmp/player-runtime-verification/`，包括 edit-first.json、edit-second.json、edit-final.json、play-tests.json、play-targeted.json、play-introduction-final.json、draft.json、formal-before-save.json、formal-after-load.json、legacy-slot2-restored.json。

截图：
- 调试我的信息：`Library/Locus/Screenshots/locus_game_20261013_140005_745.png`
- 调试人物关系：`Library/Locus/Screenshots/locus_game_20261013_140031_792.png`
- 调试世界情况：`Library/Locus/Screenshots/locus_game_20261013_140102_642.png`
- 正式建角：`Library/Locus/Screenshots/locus_game_20261013_141133_278.png`（专精效果当时显示key，后已配表修中文，未补截图）
- 正式详情：`Library/Locus/Screenshots/locus_game_20261013_141327_748.png`
- 正式经营：`Library/Locus/Screenshots/locus_game_20261013_141849_230.png`
- 读档后详情：`Library/Locus/Screenshots/locus_game_20261013_142235_148.png`
