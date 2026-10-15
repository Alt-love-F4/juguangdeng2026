# UI 音效分层与试听

## 已实施

保留 YokiFrame 播放后端、UI 总线及原始素材。按钮的通用 ui.select / ui.confirm 等 Key 按所属面板解析为独立 Key；空 Key 仍表示静音。传入 Component/GameObject owner 的 GameAudio.Play 同样支持通用 UI Key 解析，明确的分层 Key 不再二次解析。

配置：Assets/Resources/GameAudioCatalog.asset。

首批域：menu、system.settings/save/pause/confirmation/result、details、map.tasks/action/inventory/location、cafe.production/latte/business、story.dialogue/reading/letter/notice。

可以在任意 UI 子节点添加 UIAudioScope，填写完整前缀，例如 ui.details.inventory 或 ui.cafe.recipe。最近节点的显式 Scope 优先于该节点的默认面板映射；缺失的事件配置逐级回退，禁用的配置不会回退。动态按钮同样按当前父级解析。

已预留独立配置但尚需实际子区域挂接：details.inventory、details.character、cafe.order、cafe.recipe。无可识别面板的按钮继续使用通用 UI 配置。没有 owner 的业务调用保持原行为。

## 当前素材：时代感实体拟音（第二版）

根据用户明确的 18/19 世纪法国、咖啡经营与剧情背景，改用纸张、书本与皮革实体拟音，不再使用第一版 Interface Sounds。

来源：https://kenney.nl/assets/rpg-audio
下载：https://kenney.nl/media/pages/assets/rpg-audio/8e99002d76-1677590336/kenney_rpg-audio.zip
作者：Kenney；许可证 CC0，允许商业使用，无强制署名。
本地：Assets/Art/audio/UI/PeriodFoley/License.txt。

- 悬停：音量设为 0，避免鼠标经过按钮持续干扰剧情。
- 通用确认：轻放书本；经营确认使用另一书本落放版本；剧情确认使用轻微皮革触动。
- 页签、翻页：短书页声；面板开合：开合书本。
- 保存：合上账本；错误：低音量合书，不用电子报警。
- 叙事翻页、通知一并更新；信封与签名使用材质近似音，标记 provisional，并非真实拆信/笔尖录音。
- 音量 0.12–0.20，原始音高；按界面保留独立 Key。234 条 UI/叙事配置已更新。

这是时代氛围候选，不是历史考据录音，也未通过用户主观听感验收。BGM、环境、设备和 cafe.* / map.* 业务反馈保持原样，部分业务反馈仍使用旧木鱼。旧包保留便于回退，Catalog 不再引用第一版 UI 包。

## 下一层规划

- 主菜单：开始、继续、退出。
- 设置：页签、滑杆结束、返回；存档：选槽、覆盖确认、保存结果、删除。
- 详情：角色、背包、物品选择；地图：地点、任务追踪、行动提交、行动结果。
- 经营：订单、配方、材料分类、制作、拉花；实体操作声继续使用 cafe.*，不混入按钮声。
- 叙事：对话选择与推进、阅读翻页、信封与签名、通知。

按真实交互语义继续细分，而不是给每个按钮强制独占音频文件；可以共用文件但独立配置音量和启用状态。

## 人工试听

从正常游戏入口进入，依次试听主菜单、设置、存档、地图、咖啡制作和剧情。快速扫过按钮、连续切页、取消弹窗，检查是否刺耳或重复触发。暂停时再测试一次 UI。将某一个分层 confirm 配置禁用，确认其他界面仍有点击声。用现有 Audio Catalog Inspector 的预览功能比较新旧素材；最终听感需用户确认。
