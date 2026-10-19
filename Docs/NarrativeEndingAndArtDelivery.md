# 明确结局与本地美术占位交接

日期：2026-10-19。接手总入口仍为 Docs/NarrativeIntegrationHandoff.md。

## 这轮实际修改

### 结局与面板

- 继续复用原 ResultPanel，不新增平行结局系统或临时场景。现有Prefab已原位升级，结局态显示标题、说明、ENDING编号和返回按钮；普通消息复用时清掉结局标记。
- NarrativeEndingPresentation.Create 将实际接受的 Ending 定义解析成被动面板快照。GameFlowBootstrap.EnterGameOver（含已结束存档恢复）统一使用该入口；结束仍由既有NCS安全边界选中，面板本身不写任何剧情事实。
- ResultPanel.ConfirmResult/原按钮双击只能触发一次关闭回调，动画仍使用原非缩放时间机制。
- 新增已明确开场IF0：`arrival.ending.if0`。复用原文标题“未开始就已结束”和离开王都的原文消息；只有明确选择“就此别离”并完成 arrival.leave 后才生产完成事实和选中结局。重试／接受委托不授予IF0。
- IF0也走原Ending→GameOver→ResultPanel，不再在DayFlow.EventCompleted回调中直接跳菜单漏掉面板。没有把第一次拒绝但仍可重试当作最终结束。
- 原四线15条+开场IF0 = **16条启用结局**；另2个历史交付测试结局继续禁用。没有把作者闸门改成true。

使用菜单 `Tools > Narrative > Ending Panels and Trigger Review` 查看16条定义及条件／结果来源。打开既有PresentationDebugScene并进入Play后，可逐项预览真实ResultPanel；这是只读预览，不是通关或领取结局。

### 必须区分的完成范围

13条末尾根（原四线12+IF0）具备可执行触发边界。核心用例使用当前生成配置、实际Controller、生产图游标、真实选择/检定及根尾事务，不是直接写terminal=true。**这不等于13条从新游戏到终点均自然可达。**

- 理想世界、RY1、RY2：终局根仍因作者判定未明关闭。
- 商业大亨：末尾可执行，上游11/29营收标准未定。
- 无法原谅／另一种绽放：末尾可执行，上游11/15坦诚度与失败去向未定。
- 其余路径仍需要完整正式游戏历史、培养与存档回归；不以显式历史夹具替代全程自然通关。

逐结局ID、根尾生产者和限制见 Docs/NarrativeEndingCoreHandoff.md。

## 美术图片严格不调用API

来源：策划文件/美术清单/02_美术需求可筛选表.tsv（93项），并核对表情清单和当前实际资源。

- 本地Pillow排版生成31张素色中文需求卡：5张CG、14张角色差分/服装需求、9张道具/群像/版式、3张动作/特效方案卡。
- 其他项目按已有、复用或纯程序演出处理。没有重画已有背景/角色，没有擅增结局CG，没有把暂定条目当正式画图订单。
- 每张卡标注需求ID、名称、类型、原状态、制作需求、原文依据（明确是历史锚点）、配置提示和备注。卡片是“空白说明图”，不是假装完成的插画。
- 输出目录：Assets/Art/NarrativePlaceholders。
- 另从当前61稿中新标出的6组补充需求制卡：CG07月光玛丽、CG08握手（可复用）、CG09雨中三人、CG10倒地玛丽（保留立绘替代）、CG11暖光王女（可省略）、P12双密道复合图。补充卡放Additional子目录，不覆盖原93项。**本轮总计37张需求卡，93条原清单＋6条现稿补充记录。**
- 补充台账：Docs/NarrativeAdditionalArtPlaceholderManifest.json；脚本build_additional_art_placeholders.py同样只在本地排字、不联网、拒绝覆盖不同已有PNG。
- 全93项处置和hash：Docs/NarrativeArtPlaceholderManifest.json。
- 自动生成说明：Docs/NarrativeArtPlaceholders.md。此MD由脚本生成，手工补充请写本文件，避免下次重建覆盖。

复现／只读校验：bundled Python 运行 Tools/Narrative/build_art_placeholders.py / 同命令加 --check。工具拒绝覆盖内容不同的已有PNG；没有任何网络／上传／图片API调用。

## 实际绑定与剧情挂点

菜单 `Tools > Narrative > Bind Local Art Requirement Placeholders` 仅在Editor绑定：

1. 所有需求卡登记 `art.requirement.<编号>`，供明确引用/预览。
2. `rose.club.knight_guidance` 初次接入时使用CG03需求卡；2026-10-19已替换为 `Assets/Art/NarrativeCG/CG03_knight_guidance.jpg`，运行时key现为正式资源。
3. 提议的CG key注册为新占位，但已有真实贴图、scene.cafe、故意黑幕一律不覆盖。
4. CG04：源表通过编译器在 rose.1012.s1.outside.sampling 显示 cg.adele_sampling，现稿P019–P024；P018为图意指令，P025明确恢复 cafe.exterior.day。
5. CG05：church.1020.s1 每条真实成功分支，在当前P142焚信对应页显示 cg.burning_letter（P143是美术指令，不做玩家台词）；P144恢复 scene.cafe。失败拘押支路不显示焚信。
6. CG07：当前C29 P012标记，在P013–P020使用cg.marie_moonlit，P021恢复原stage。CG09：RE11/23 P228标记，在P229–P231使用cg.rainy_triad，P232恢复原stage。不按日期/文件名给整事件套一张CG。
7. CG02仍无正式图；CG06图片已登记到独立key `cg.duke_blessing`，但 `church.1009.s2` 仍使用 `cathedral.day`，没有新增剧情挂点。补充CG08/CG10/CG11/P12及道具、差分等卡片没有确认具体演出挂点/层通道时，不自动塞成全屏背景或冒充角色立绘。

### 编号CG图片交付（2026-10-19）

- 原件保留在 `策划文件/CG/`，Unity副本位于 `Assets/Art/NarrativeCG/`；CG03–CG06均使用原始JPG，不修改源文件。
- CG03–CG05替换既有运行时占位key；CG04/CG05沿用已接好的源表挂点及背景恢复页，不改剧情表。
- CG06只替换未被剧情引用的独立素材key，不映射到共享 `cathedral.day`，也不代表 `church.1009.s2` 已获批准使用特写。
- CG01继续复用 `street.day` / `oct07.street.day` 的圣徒街正式背景；目录中未提供CG01、CG02文件，不新增替代图。

绑定报表：Docs/NarrativeArtPlaceholderBindings.json。该报表和源需求清单分别说明注册与原始需求，不把注册当作剧情已接。

`Tools > Narrative > Art Requirement Placeholders` 可以搜索并预览37张卡及合并后的99项处置；当前注册素材会显示实际key、占位/正式状态和图片路径，点击可选中文件。

普通咖啡馆对白仍用scene.cafe；特殊CG后明确恢复，不注册经营原始楼层底图作对话图。正式角色已有立绘不被带文字的竖版需求卡替换。原有咖啡馆交互保持原CafeWorld/工作台所有者。

## 唯一配表生产链

本轮改动源映射的rose.py/church.py及中央integrate_content.py中的IF0定义后，统一通过：

1. integrate_content.py生成合并计划。
2. merge_workbooks.mjs以artifact-tool更新源Narrative.xlsx/Text.xlsx。
3. Assets/ConfigTables/GenerateAll.bat完成Player semantic validation与官方Luban strict生成。

不手改JSON。新增的艺术key属于既有Presentation资源目录，不新增一套剧本字段。

## 新增验证入口

- NarrativeFormalEndingTests：原四线15结局的完整条件、末尾根、取消、互斥、快照恢复；覆盖契约另包含IF0（不冒充三个未明根可通）。
- NarrativeOpeningEndingTests：真实拒绝→确认离开才触IF0；重新抉择/接受不触IF0。
- NarrativeEndingPanelTests：全部启用结局在共享场景中打开真实Prefab；暂停时可读；双确认回调一次；普通消息复用清除Ending；实际正文根完整播放回调后才打开面板。
- NarrativeArtPlaceholderTests：93项／31卡及绑定、保留scene.cafe与现有正式图、CG04/CG05显示后恢复且不串分支。
- Python --check：31PNG解码、尺寸、文字边界、像素确定性、引用文件hash及禁止覆写。

实际运行数字和未通过项在本次交付结束前记录于下方；便携测试/静态表检查不等同Unity实机通关。

## 最终验证

- 官方 GenerateAll.bat：Player semantic validation、Luban strict均通过。当前346 Event、5507 ConversationPage、8162 ConversationLine；18条Ending中16启用、2旧测试禁用。
- Unity实际全量配置Load通过；最终静态报告GlobalErrors=0、Warnings=0，仍保留3个作者未决根的4条明确施工原因，不是删除错误假装全通。Docs/NcsIntegrationReport.md与NcsIntegrationQueue.csv已同步。
- EditMode最终集中 **95/95通过**：FormalEnding46、OpeningEnding4、Loader42、ArtPlaceholder3。实际原始结果 `output/narrative-endings/editmode-final-tests.json`（job f49fba06833643719a049322f180e834）。
- PlayMode最终集中 **78/78通过**：结局面板4项＋背景/占位/实景资源回归74项。原始结果 `output/narrative-endings/presentation-final-tests.json`（job 01a8b7760c3247319c4b0cd6dd0167e5）。
- 面板用例实际打开全部16条结局的真实ResultPanel Prefab，timeScale=0；验证关闭回调一次、普通消息复用清标记；实际生成的中立A正文根经Presenter完整接受后才触EndingSelected并打开面板。界面截图已目视检查：`output/narrative-endings/accepted-ending-panel.png`。
- 开场IF0用例覆盖真实拒绝→确认离开→根完成、重新抉择/接受不结束、中途取消/迟到回调无效，以及EventCompleted通知边界后才启用结局检查。
- 120/120内容构建测试通过。CG源映射改动仍通过现稿哈希、分支/段落覆盖、资源key与结果所有权校验。
- 本地37张PNG完成解码/尺寸/文字边界/确定性像素检查；主93项与补6项的两个 --check均通过；原有真实美术未覆盖，imageApiCalls=0。
- 初轮面板综合测试曾因逐条重复加载全量剧情表超过测试超时，已改预览复用显式只读配置快照（正式运行不改变），最终78项约18秒通过。另一个初版正文夹具缺少正式日期同步曾误入报纸入口，按实际SetOfficialDay修正，未移除正式报纸或改剧情以迎合测试。
- 修改文件git diff --check通过（Unity原生Prefab空m_Name行的格式空白已规范化，不改语义）。

**限制仍保留：**末尾根测试显式准备真实历史前置，不能代替从新游戏到所有结局的人工自然通关；商业大亨、无法原谅、另一种绽放有可执行末尾但上游作者闸门未定。理想世界、RY1、RY2的终局根仍关闭。未跑平台Player Build；本轮没有宣称之前原咖啡馆40项中的旧失败已全部修好。


## 2026-10-19 角色当前配置核对补充

`Art Requirement Placeholders` 已接入正式 Luban Object 与实际 Sprite 加载。角色条目现在分别显示基础立绘、头像、店内形象、剧情 ID、对白登场／side=none 引用和表情回退；背景与CG继续读取 NarrativeStageVisuals，独立需求卡仍保留。历史清单的“待补底图”不再充当当前角色配置结论。麻雀基础图已配置；索雷尔 `sorel` 的空图与 `emmanuel` 的已有图分别报告。使用与验证边界见 [角色美术需求与正式配置的连接](NarrativeCharacterArtIntegration.md)。

## 2026-10-19 用户请求的非结局 API 补图

本轮用户明确授权调用PackyAPI，已生成CG07月光玛丽、CG09雨中三人候选、CH35教程老太太、P04国王币四项。CG07接原有正文key，CH35接现有教程中心剪影，P04仅登记图片；CG09因主角枪口方向不符原稿保留候选、未接线。所有结局、旧正式素材及原始需求卡保留。逐项状态、来源和验证见 [非结局需求 API 美术补充](NarrativeGeneratedArtDelivery-2026-10-19.md)。本段仅记录用户本轮授权，不追认此前“本地占位、不调用API”批次为生成图。
