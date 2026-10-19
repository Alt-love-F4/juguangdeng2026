# 叙事美术需求占位卡（纯本地，不调用图片 API）

## 范围与交接

- 审阅原表 **93 条**，输出 **31 张**素色中文需求卡。所有需求都在 JSON 中有处置记录，不是所有旧“待补”都再画一张。
- 需求卡分组：`{"CG": 5, "CH": 14, "FX": 3, "P": 9}`；处置：`{"existing": 34, "placeholder": 31, "reuse": 23, "runtimeOnly": 5}`。
- 当前资源中已有圣徒街、俱乐部、监狱、店面、楼层对话图及大多数角色底图；旧清单的待补状态已过时。仅核文件、引用及hash，不将存在等同美术验收。
- 单角色所有待核表情及服装集中一张卡；小声、点头等可能是演技，不机械一词一张。玛丽／雅克、sorel／emmanuel、蒂博、埃利昂均按同一身份处理。
- 制卡脚本只修改本脚本、`Assets/Art/NarrativePlaceholders/*.png`、本 MD 和 `Docs/NarrativeArtPlaceholderManifest.json`。不写 Excel、生成表、StageVisuals、Prefab、Runtime，不执行 Unity 作业。
- 未调用 PackyAPI、OpenAI 或任何网络／图片 API；不需要也不检查密钥。Pillow 字体排版不是 AI 生图。

## 素材及绑定契约

1. `requirements[]` 每项含 `id/category/name/status/requirement/sourceEvidence/configuration/notes/disposition/assetPath/suggestedRegistryKeys`。
2. `disposition=placeholder` 时 `assetPath` 为新卡；否则它是现有参考（或空），**不能把每个assetPath都拿去覆盖注册贴图**。
3. `existingAssets` 保留真实路径、尺寸、SHA256；`registryEvidence` 与 `registrySnapshot` 是生成时的引用快照。`suggestedRegistryKeys` 只列当时已核实存在的 key；`proposedUnregisteredKeys` 是当时的未注册提议，后续是否可用须查当前注册表。
4. 主集成仅通过Unity为31张卡新增 `requirement.<ID>` 与提议key；CG03可把原先无texture的 `rose.club.knight_guidance` 绑定到对应需求卡。绑定仍保留 `placeholder=true` 并使用白色tint；绝不覆盖已解析真实美术。本工具不编辑注册表。
5. 普通店内对白始终 `scene.cafe`，不生成新的楼层背景卡、不覆盖经营原图。蒂博闭门特殊合成图已实际存在并注册，保留例外。
6. 暂定CG06、P09、P10、P11、FX03及动作方案待核的FX05均明确标注未批准；不能把卡片数量当正式画图订单数量。没有根据结局表擅增15张结局CG。
7. 1920×1080用于CG／道具组／特效组，768×1024用于角色差分。卡片本身不透明、非实际剪影，角色卡不应直接替换现有角色立绘。
8. 卡面文字固定为制卡时记录；主集成后续注册提议key不会触发重绘，不能把卡面旧“未注册”标签当成实时注册诊断。
9. CG04正式片段拟用现稿P018–P024，P025恢复店外背景；CG05焚信必须先核当前分支与锚点，再显式恢复。此处仅记录主集成计划，不声称已接线；旧P编号不当成当前事实。

## 历史来源与当前事实

- TSV及其文案P编号、旧配置ID、旧资源路径原样保留为历史证据；`sourceAnchorVerified=false`，不声称它们与当前段号一致。
- `resolvedSourceFiles` 仅记录当前同名源文档hash；不把短文件名映射或文件存在当作剧情接线证据。
- 旧REF02对10.29／11.05缺稿的判断已过时；全文入库与关系图、自然触发、Unity验收仍分别判断。
- 参考图、地图和现有图标先复用；未知头像或临时地点用途待确认，不自动扩成全屏背景或新的正式地点。

## 本地重现和只读检查

```powershell
$python = 'C:/Users/AltF4/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $python Tools/Narrative/build_art_placeholders.py
& $python Tools/Narrative/build_art_placeholders.py --check
```

- 首次运行仅创建缺失PNG；重跑若字节不同即报错，**绝不覆盖**任何已有图。替换正式素材应另存新文件，由主集成变更引用。
- 同一Pillow与字体hash环境下PNG字节、像素hash可复现。`--check`只读：重新排版逐一校验PNG／像素／尺寸／排版不越界，同时核93项及JSON和MD未漂移。允许且报告主集成的安全需求卡绑定漂移，不因注册表hash单独变化失败。
- 注册表绑定后优先用 `--check`，无需刷新原审计快照；它只允许新增映射到对应卡的requirement／提议key，或空占位绑定卡。删除旧key、替换真实图、错误卡ID、取消placeholder或破坏scene.cafe会失败。脚本不重写主集成绑定。
- 默认构建可主动刷新JSON／MD的证据快照，已有PNG仍受不可覆盖保护；字体或需求文本改变导致旧PNG不同会停下，不自动重制。
- 不生成 `.meta`，由Unity正常导入生成；引用请由主集成获得实际GUID，不猜GUID。

## 逐项处置

|编号|名称|原状态|处置|实际资源或需求卡|
|---|---|---|---|---|
|CG01|繁忙的圣徒街|待补／可与背景合并|reuse|Assets/Art/Background/game/SCE_圣徒街场景1.png|
|CG02|主角回头／外围邪恶线条|待补|placeholder|Assets/Art/NarrativePlaceholders/CG02_protagonist_turn.png|
|CG03|骑士画像指引贵族老爷|待补|placeholder|Assets/Art/NarrativePlaceholders/CG03_knight_guidance.png|
|CG04|阿黛尔端试饮盘|待补|placeholder|Assets/Art/NarrativePlaceholders/CG04_adele_sampling.png|
|CG05|白色信件被火焰吞噬|待补|placeholder|Assets/Art/NarrativePlaceholders/CG05_burning_letter.png|
|CG06|埃利昂在教堂露台祈福|暂定／原稿pending|placeholder|Assets/Art/NarrativePlaceholders/CG06_duke_blessing_PENDING.png|
|BG01|咖啡馆一楼大堂／日景|已有图／风格定稿待确认|reuse|Assets/Art/Background/game/SCE_一楼大厅场景底图.png|
|BG02|咖啡馆清晨光照差分|已有日景代用／待确认差分|reuse|Assets/Art/Background/game/SCE_一楼大厅场景清晨.png|
|BG03|咖啡馆傍晚差分|待补／当前空图占位|reuse|Assets/Art/Background/game/SCE_一楼大厅场景傍晚.png|
|BG04|咖啡馆夜景差分|已有调色代用／待确认|reuse|Assets/Art/Background/game/SCE_一楼大厅场景夜晚.png|
|BG05|一楼大堂促销纸花装饰|待补差分|reuse|Assets/Art/Background/game/SCE_一楼大厅场景纸花装饰.png|
|BG06|二楼会客室／教程杂乱状态|已有基底／待补杂乱差分及接线|reuse|Assets/Art/Background/game/SCE_二楼会客室场景底图.png|
|BG07|三楼宿舍／主角房间清晨|已有相关图／构图和光照待确认、待接线|reuse|Assets/Art/Background/game/SCE_ 三楼宿舍场景底图.png|
|BG08|圣徒街日景／咖啡馆所在街道|待补／与CG01共用候选|existing|Assets/Art/Background/game/SCE_圣徒街场景1.png|
|BG09|圣徒街夜晚亮灯|待补|existing|Assets/Art/Background/game/SCE_圣徒街场景夜晚.png|
|BG10|咖啡馆门外／店面|部分暂定／待补|existing|Assets/Art/Background/game/SCE_沸点咖啡馆店面场景日景.png|
|BG11|布塔克俱乐部门口|待补|existing|Assets/Art/Background/game/SCE_布塔克俱乐部门口场景底图.png|
|BG12|布塔克俱乐部会客室|待补|existing|Assets/Art/Background/game/SCE_布塔克俱乐部会客室场景底图.png|
|BG13|圣地／圣卷大教堂场景|待补／名称待统一|existing|Assets/Art/Background/game/SCE_圣卷大教堂场景日景.png|
|BG14|大堂区露天市场／日景|待补|existing|Assets/Art/Background/game/SCE_大堂区露天市场场景日景.png|
|BG15|大堂区杂货铺内部／日景|待补|existing|Assets/Art/Background/game/SCE_大堂区杂货铺内部场景日景.png|
|BG16|圣西尔万郊区|待补|existing|Assets/Art/Background/game/SCE_圣西尔万郊区场景日景.png|
|BG17|旧城区街道|待补|existing|Assets/Art/Background/game/SCE_旧城区街道场景日景.png|
|BG18|吕西尼昂旧宅／门前|待补|existing|Assets/Art/Background/game/SCE_吕西尼昂旧宅门前场景底图.png|
|BG19|教会看守所／监狱内部与晨昏变体|待补|existing|Assets/Art/Background/game/SCE_教会看守所内部场景傍晚.png|
|BG20|闭合咖啡馆门／门外蒂博剪影|待补合成图或分层|existing|Assets/Art/Background/game/SCE_咖啡馆闭门场景蒂博剪影.png|
|BG21|吧台／操作台／出餐视角|已有资源／教程构图待确认|reuse|Assets/Art/Background/game/SCE_咖啡馆出餐台场景底图.png|
|P01|信封闭合、打开与信纸|已有／优先复用|reuse|Assets/Art/Func/信封/信封出现.png|
|P02|玫瑰骑士像／玫瑰王肖像|待补／两用途可共用候选|placeholder|Assets/Art/NarrativePlaceholders/P02_rose_knight_painting.png|
|P03|西尔万名片／法语手写标题|待补版式或字体方案|placeholder|Assets/Art/NarrativePlaceholders/P03_sylvain_card.png|
|P04|十几个国王币|待补／先查可复用货币图|placeholder|Assets/Art/NarrativePlaceholders/P04_king_coins.png|
|P05|新玫瑰会贵族老爷剪影|已有可复用底图／待接线|reuse|Assets/Resources/Characters/bertrand/neutral.png|
|P06|旧宅门前一高一矮两个人影|待补一组叠层|placeholder|Assets/Art/NarrativePlaceholders/P06_two_shadows.png|
|P07|被驱逐的一家人|待补一组叠层|placeholder|Assets/Art/NarrativePlaceholders/P07_evicted_family.png|
|P08|教会查店队伍|待补群体层／共用通用NPC|placeholder|Assets/Art/NarrativePlaceholders/P08_church_search_group.png|
|P09|宽檐毡帽|暂定／原稿pending|placeholder|Assets/Art/NarrativePlaceholders/P09_wide_brim_hat_PENDING.png|
|P10|教堂四组可点击剪影|暂定／有余力才做|placeholder|Assets/Art/NarrativePlaceholders/P10_cathedral_groups_PENDING.png|
|P11|圣诺特之声推荐／广告版式|待确认展示方式|placeholder|Assets/Art/NarrativePlaceholders/P11_newspaper_layout_PENDING.png|
|FX01|淡入淡出、黑幕、震屏、背景平移|已有程序通道／无独立画图量|runtimeOnly|Assets/Resources/Dialogue/PresentationEffectLibrary.asset|
|FX02|索雷尔眼部强调／阿黛尔遮眼闪回|待核演出效果|runtimeOnly|Assets/Resources/Characters/adele/neutral.png|
|FX03|教堂金色流光|暂定／原稿pending|placeholder|Assets/Art/NarrativePlaceholders/FX03_golden_light_PENDING.png|
|FX04|教程八类操作演示与高亮|已有经营素材／动画覆盖待核|runtimeOnly|Assets/Resources/FreeCafe/ArtLibrary.asset|
|FX05|送餐／蒂博喝咖啡动作|待核动作素材|placeholder|Assets/Art/NarrativePlaceholders/FX05_thibaut_drink_motion.png|
|FX06|经营评价烟花／金钱／负面emoji|待核特效素材|placeholder|Assets/Art/NarrativePlaceholders/FX06_business_rating.png|
|FX07|仅对话框震动|程序演出项|runtimeOnly|Assets/Resources/Dialogue/PresentationEffectLibrary.asset|
|CH01|洛朗·康多尔|待补底图|existing|Assets/Resources/Characters/laurent/neutral.png|
|CH02|克洛维.康多尔|待补底图|existing|Assets/Resources/Characters/clovis/neutral.png|
|CH03|雷诺.朗特|不列入本期必画|existing|Assets/Resources/Characters/renaud/neutral.png|
|CH04|埃利昂.康多尔|已有底图／差分或接线待核|existing|Assets/Resources/Characters/elion/neutral.png|
|CH05|阿莉耶诺尔.康多尔/埃莉诺【乔装时的假名】|已有宫廷／日常图；乔装差分待补|placeholder|Assets/Art/NarrativePlaceholders/CH05_alienor_variants.png|
|CH06|王后|不列入本期必画|existing|Assets/Resources/Characters/queen/neutral.png|
|CH07|贝特朗.康多尔|已有底图／差分或接线待核|placeholder|Assets/Art/NarrativePlaceholders/CH07_bertrand_variants.png|
|CH08|萨德|已有底图／差分或接线待核|placeholder|Assets/Art/NarrativePlaceholders/CH08_sade_variants.png|
|CH09|马米翁|待补底图|existing|Assets/Resources/Characters/mamion/neutral.png|
|CH10|吕西安|待补底图|existing|Assets/Resources/Characters/lucien/neutral.png|
|CH11|勒苏|已有底图／差分或接线待核|placeholder|Assets/Art/NarrativePlaceholders/CH11_lesou_variants.png|
|CH12|西尔万.瓦隆|待补底图|placeholder|Assets/Art/NarrativePlaceholders/CH12_sylvain_variants.png|
|CH13|安托万.马尚|已有底图／差分或接线待核|placeholder|Assets/Art/NarrativePlaceholders/CH13_antoine_variants.png|
|CH14|勒费弗尔|待补底图|existing|Assets/Resources/Characters/lefebvre/neutral.png|
|CH15|杜瓦尔.米尼耶|不列入本期必画|existing|Assets/Resources/Characters/duval/neutral.png|
|CH16|阿黛尔·米尼耶|已有底图／差分或接线待核|placeholder|Assets/Art/NarrativePlaceholders/CH16_adele_variants.png|
|CH17|埃马纽埃尔·索雷尔|待补底图|placeholder|Assets/Art/NarrativePlaceholders/CH17_emmanuel_variants.png|
|CH18|麻雀|待补底图|placeholder|Assets/Art/NarrativePlaceholders/CH18_sparrow_variants.png|
|CH19|蒂博·卡隆|已有底图／差分或接线待核|placeholder|Assets/Art/NarrativePlaceholders/CH19_thibaut_variants.png|
|CH20|奥克塔夫·韦尔内|待补底图|existing|Assets/Resources/Characters/octave/neutral.png|
|CH21|欧仁·迪布瓦|已有底图／差分或接线待核|existing|Assets/Resources/Characters/eugene/default.png|
|CH22|柯莱特·布吕内|待补底图|placeholder|Assets/Art/NarrativePlaceholders/CH22_colette_variants.png|
|CH23|帕斯卡尔·勒鲁|待补底图|existing|Assets/Resources/Characters/pascal/neutral.png|
|CH24|玛丽·勒费弗尔（真实的女性身份）/雅克·马尚（男性身份）|待补底图|placeholder|Assets/Art/NarrativePlaceholders/CH24_marie_variants.png|
|CH25|客人A|待补底图|existing|Assets/Resources/Characters/guest_a/neutral.png|
|CH26|客人B|待补底图|existing|Assets/Resources/Characters/guest_b/neutral.png|
|CH27|客人C|待补底图|existing|Assets/Resources/Characters/guest_c/neutral.png|
|CH28|客人D|待补底图|existing|Assets/Resources/Characters/guest_d/neutral.png|
|CH29|军人A|待补底图|existing|Assets/Resources/Characters/soldier_a/neutral.png|
|CH30|军人B|待补底图|existing|Assets/Resources/Characters/soldier_b/neutral.png|
|CH31|军人C|待补底图|existing|Assets/Resources/Characters/soldier_c/neutral.png|
|CH32|军人D|待补底图|existing|Assets/Resources/Characters/soldier_d/neutral.png|
|CH33|教会人员通用NPC（监察官／看守）|待补通用底图|placeholder|Assets/Art/NarrativePlaceholders/CH33_church_npc.png|
|CH34|记者／通用市民男性|已有简易占位／待替换|reuse|Assets/Resources/Characters/guest_a/neutral.png|
|CH35|教程顾客老太太／黑色剪影|待确认复用通用顾客|placeholder|Assets/Art/NarrativePlaceholders/CH35_tutorial_elderly_woman.png|
|MAP01|咖啡馆|已有图标／地点语义待核|reuse|Assets/Resources/Art/Map/Locations/location_03.png|
|MAP02|集市|已有图标／地点语义待核|reuse|Assets/Resources/Art/Map/Locations/location_05.png|
|MAP03|图书馆|未知头像占位／按地点用途确认|reuse|Assets/Resources/Art/UI/Avatars/Unknown.png|
|MAP04|临时事件|未知头像占位／按地点用途确认|reuse|Assets/Resources/Art/UI/Avatars/Unknown.png|
|MAP05|圣西尔万郊区|已有图标／地点语义待核|reuse|Assets/Resources/Art/Map/Locations/location_02.png|
|MAP06|圣徒街|已有图标／地点语义待核|reuse|Assets/Resources/Art/Map/Locations/location_03.png|
|MAP07|首都法院|已有图标／地点语义待核|reuse|Assets/Resources/Art/Map/Locations/location_06.png|
|MAP08|市政厅|已有图标／地点语义待核|reuse|Assets/Resources/Art/Map/Locations/location_07.png|
|MAP09|花园区|已有图标／地点语义待核|reuse|Assets/Resources/Art/Map/Locations/location_09.png|
|MAP10|地图设定中尚未完整对应的地点|已有图集／先核映射|reuse|Assets/Resources/Art/Map/Locations/location_00.png|
|MAP11|圣诺特城总地图|已有图／地点对应待核|reuse|Assets/Art/Map/GeneratedUI/mapBackgroundSprite.png|
|REF01|五方标志／阵营徽记|文案内已有参考图／导出接入待确认|existing|策划文件/美术清单/文案内嵌参考图预览.jpg|
|REF02|故事纲要结局与世界设定插画|尚无明确画面订单|runtimeOnly|仅程序／未下单|

## 验证范围

此工具的成功只证明本地占位卡、完整台账、现存引用与确定性检查通过。未启动Unity、未测试演出、不证明结局已接线。结束面板与资源绑定由主集成负责。
