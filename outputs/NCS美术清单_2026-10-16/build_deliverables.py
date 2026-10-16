from pathlib import Path
import json,re,collections,csv,hashlib,datetime
out=Path(__file__).parent; root=out.parents[1]
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
sources=load(out/'source_records.json'); assets=load(out/'asset_inventory.json'); stage=load(out/'stage_inventory.json'); excel=load(out/'excel_art_rows.json')
entries=[]
def add(id,category,name,status,need,source,current='',binding='',note=''):
 entries.append(dict(编号=id,类别=category,名称=name,状态=status,制作或确认内容=need,文案依据=source,现有资源=current,配置对应=binding,备注=note))
# CGs explicitly requested; pending remains pending.
add('CG01','CG／插画','繁忙的圣徒街','待补／可与背景合并','开幕进入王都街道的繁忙全景；可与BG08圣徒街日景共用一张基底。','开幕文案.docx P049','','开幕文案；NCS oct07.street.day 仍为无图占位','不重复计算为两张；是否增加行人层由美术决定。')
add('CG02','CG／插画','主角回头／外围邪恶线条','待补','主角回头；外围若隐若现的邪恶线条向画面中央主角指向。建议人物、背景、线条可分层。','开幕文案.docx P114','','当前未找到对应专用图及NCS图片引用','需要先统一主角可视外观；文案没有要求整套主角对话立绘。')
add('CG03','CG／插画','骑士画像指引贵族老爷','待补','老爷站起凝视玫瑰骑士画像，构图表现骑士指引老爷；同场景沙发后是骑士斩断荆棘走出的巨大画像。','新玫瑰/MA10.09-S1.docx P031-P036、P072-P076；故事.xlsx 时间轴-共通线路!D60','','rose.1009.s1 的 portrait 页仍用 rose.club.reception 占位','可复用BG12会客室与道具P02画像；总纲有知觉鉴定解锁描述，触发规则另由策划处理。')
add('CG04','CG／插画','阿黛尔端试饮盘','待补','咖啡馆门外的阿黛尔端试饮托盘，使用促销场景。','新玫瑰/MA70.10.12-S1.docx P018-P019','已有阿黛尔端咖啡杯常规立绘，不等于试饮盘CG。','rose.1012.s1.conversation；当前仍复用咖啡馆预览背景','不把现有端杯立绘列为本项完成。')
add('CG05','CG／插画','白色信件被火焰吞噬','待补','一封白色的信被火焰吞噬；可交静态插画，若制作燃烧动画需再确认分层。','王庭教会/MA70.10.20-S1.docx P164','通用闭合信封存在，但没有本场焚信插画。','church.1020.s1.conversation；当前未找到对应专用图绑定','原稿只明确一张图，不自动扩成视频。')
add('CG06','CG／插画','埃利昂在教堂露台祈福','暂定／原稿pending','公爵在教堂露台祈福；双颊环形圣痕放光，可与金色流光效果分层。','王庭教会/MA70.10.09-S2.docx P014-P017','埃利昂常规立绘存在。','church.1009.s2.conversation；church.cathedral.day 是占位','先由策划／美术确认是否制作；不能列作已批准必画。')
# Backgrounds merged by art content, not by page key.
add('BG01','背景','咖啡馆一楼大堂／日景','已有图／风格定稿待确认','统一正式一楼背景，复用所有白天咖啡馆对话及新手教程大堂。','中立/MA70.10.07-S1.docx P003；文案-新手教程.docx P008-P009','Assets/Art/Background/架空咖啡馆背景_01.png；Assets/Art/Background/SCE_一楼大厅场景底图.png','oct07.cafe.day；FirstDayIntroductionVisuals.background 当前用架空咖啡馆背景_01','存在多套风格，不将文件存在当成美术最终验收。')
add('BG02','背景','咖啡馆清晨光照差分','已有日景代用／待确认差分','索雷尔到访要求清晨，先决定日景是否可直接复用；如需清晨光照则补变体。','革命联合相关事件/MA70.10.14-S1.docx P003','当前使用BG01日景。','revolution.1014.s1 → oct07.cafe.day','不默认必须独立新画一张。')
add('BG03','背景','咖啡馆傍晚差分','待补／当前空图占位','暖色夕照、营业结束气氛，可在同一楼大堂底图上做光照差分。','中立/MA70.10.13-S2.docx P144；王庭教会/MA70.10.23-S1.docx P103','','neutral.cafe.dusk、church.cafe.dusk','两个key对应同一类图，合并制作。')
add('BG04','背景','咖啡馆夜景差分','已有调色代用／待确认','夜间咖啡馆；决定现有蓝灰叠色是否满足夜景需求，必要时补灯光层。','革命联合相关事件/MA70.10.07-S2.docx P003、MA70.10.13-S2.docx P004、MA70.10.28-S1.docx P003','架空咖啡馆背景_01.png + tint(0.42,0.48,0.62)','oct07.cafe.night','目前没有独立夜景贴图；不能称为已完成专用夜景。')
add('BG05','背景','一楼大堂促销纸花装饰','待补差分','在一楼基底增加纸花装饰；宜做可开关装饰层。','新玫瑰/MA70.10.12-S1.docx P003、P066','一楼背景已有；未见纸花装饰成品。','rose.1012.s1 当前仍复用 oct07.cafe.day','纯正文提及杯碟不另增美术任务。')
add('BG06','背景','二楼会客室／教程杂乱状态','已有基底／待补杂乱差分及接线','正常二楼会客室与教程明确要求的杂乱状态。10.16若二楼开放使用二楼，否则回一楼。','文案-新手教程.docx P009；新玫瑰/MA70.10.16-S1.docx P017','Assets/Art/Background/SCE_二楼会客室场景底图.png；二楼背景.png 等草图','rose.1016.s1 的楼层背景待与开放状态接线','正常会客室不是杂乱状态已完成。')
add('BG07','背景','三楼宿舍／主角房间清晨','已有相关图／构图和光照待确认、待接线','教程三楼与11.04主角房间清晨可优先复用宿舍图，确认是否需要房间近景。','文案-新手教程.docx P009；王庭教会/MA70.11.04-S1.docx P003','Assets/Art/Background/SCE_ 三楼宿舍场景底图.png','church.1104.s1.background.morning.wake 当前仍是空图占位','已见宿舍走廊视角；不能直接断言等于主角卧室。')
add('BG08','背景','圣徒街日景／咖啡馆所在街道','待补／与CG01共用候选','繁华商业街，含咖啡馆、书商、裁缝等城市特征；预留横向平移边缘。','中立/MA70.10.07-S1.docx P029；王庭教会/MA70.11.04-S1.docx P253、P290；游戏地图.xlsx 圣诺特城!C7','','oct07.street.day；church.1104.s1.background.*.street.*（dusk除外）','地图缩略建筑图不是全屏街景。')
add('BG09','背景','圣徒街夜晚亮灯','待补','同一街道入夜后灯光亮起；需要灯光内容，单纯日图压暗不能覆盖原文。','王庭教会/MA70.11.04-S1.docx P279、P463','','church.1104.s1.background.far.street.dusk / near.street.dusk','远线、近线共用一套。')
add('BG10','背景','咖啡馆门外／店面','部分暂定／待补','正式店面外景供试饮和街边对话；开幕另有萧条状态（原稿明确暂定）。','开幕文案.docx P055；新玫瑰/MA70.10.12-S1.docx P018、P026','','开幕店面、rose.1012.s1门外段未见专用图','把“正常门外”与“萧条差分是否做”分别确认。')
add('BG11','背景','布塔克俱乐部门口','待补','俱乐部入口场景。','新玫瑰/MA10.09-S1.docx P003','','rose.club.exterior','当前是无图中性色。')
add('BG12','背景','布塔克俱乐部会客室','待补','沙发、纱窗、主位及墙上的玫瑰骑士画像；不可默认普通咖啡馆二楼就是俱乐部。','新玫瑰/MA10.09-S1.docx P031-P036','','rose.club.reception','画像见P02，老爷剪影见P05，CG03可复用构图。')
add('BG13','背景','圣地／圣卷大教堂场景','待补／名称待统一','仪式举行地点，露台与群众活动空间；如CG06取消，基础教堂背景仍有需求。','王庭教会/MA70.10.09-S2.docx P003、P022；游戏地图.xlsx 圣诺特城!C13','','church.cathedral.day','原稿称圣地大教堂，地图表称圣卷大教堂，先按同类地点候选合并，名称请策划确认。')
add('BG14','背景','大堂区露天市场／日景','待补','露天大市场，旧民房、食材摊位；10.08外部街区和11.04市场可共享基底，按镜头需要裁切。','中立/MA70.10.08-S1.docx P003、P086；王庭教会/MA70.11.04-S1.docx P089、P414；游戏地图.xlsx 圣诺特城!C11','','neutral.market.day；church.1104.s1.background.far.market / near.market','大堂区不是咖啡馆“大堂”；两者不能混用。')
add('BG15','背景','大堂区杂货铺内部／日景','待补','柯莱特搬箱、交易发生的杂货铺内部。','中立/MA70.10.08-S1.docx P012','','neutral.market.shop.day','木箱仅正文动作，未要求额外动画或道具特写。')
add('BG16','背景','圣西尔万郊区','待补','城外工人大区，低矮密集房屋和贫民生活环境。','王庭教会/MA70.11.04-S1.docx P138、P362；游戏地图.xlsx 圣诺特城!C9','','church.1104.s1.background.far.suburb / near.suburb','地图位置图标已存在，但不是演出背景。')
add('BG17','背景','旧城区街道','待补','狭窄拥挤街道、旧贵族宅邸转租的贫民居住区。','王庭教会/MA70.11.04-S1.docx P190、P299；游戏地图.xlsx 圣诺特城!C5','','church.1104.s1.background.far/near.old.intro、perception 等','各分支重复背景key不要求逐key画一张。')
add('BG18','背景','吕西尼昂旧宅／门前','待补','窄街尽头的旧式宅邸；锈铁门、庭院杂草、褪色Lusignan家徽。','王庭教会/MA70.11.04-S1.docx P204、P313','','church.1104.s1.background.far/near.old.house、estate、heirloom、eviction、aftermath','基础宅邸一套，人物剪影分层见P06、P07；是否需要不同镜头再确认。')
add('BG19','背景','教会看守所／监狱内部与晨昏变体','待补','监狱或拘押室内部基础图；清晨和傍晚光照。10.23两事件共用。','王庭教会/MA70.10.23-S1.docx P008、P014、P065；MA70.10.23-S2.docx P006','','church.detention.interior、church.detention.dawn、church.detention.dusk','不默认新增三个独立地点，按一场景+光照差分排量。')
add('BG20','背景','闭合咖啡馆门／门外蒂博剪影','待补合成图或分层','闭合的店门，门外一道蒂博·卡隆剪影；可复用店面或室内门体。','王庭教会/MA70.10.14-S2.docx P087','已有 Characters/thibaut/neutral.png 可作剪影参考。','church.1014.s2 尾段仍用咖啡馆背景代用','可以门体+人物影分层，不必再设计新角色。')
add('BG21','背景','吧台／操作台／出餐视角','已有资源／教程构图待确认','教程要求从大堂吧台切到操作台，并突出操作区域。','文案-新手教程.docx P041、P044-P055','Assets/Art/Background/工作台背景.png、出餐台背景.png；Assets/Art/FreeCafe/Backgrounds/production-workstation.png；FreeCafe物件图','CafeTutorialStep、FreeCafe ArtLibrary、教程面板','经营物件不是全数缺图；教程动画另列FX04。')
# Props and composite layers.
add('P01','道具／纸张','信封闭合、打开与信纸','已有／优先复用','开幕来信、10.07革命线来信、10.14索雷尔信、蒂博出示信封共用基础资源；信内容由文本显示。','开幕文案.docx；革命联合相关事件/MA70.10.07-S2.docx P014、P026；MA70.10.14-S1.docx P008；王庭教会/MA70.10.14-S2.docx P035','Assets/Art/Func/信封/信封出现.png、信封自动打开.png、底图.png；信封.psd','FirstDayLetterPanel；LetterReveal/LetterRead；ReadingPage.imageKey均为空','已核到闭合、打开、信纸素材；焚信CG另列CG05。现有装饰英文与各信件风格是否保留需确认。')
add('P02','道具／画像','玫瑰骑士像／玫瑰王肖像','待补／两用途可共用候选','俱乐部骑士斩荆棘大画像，以及历史讲述的玫瑰王肖像；同一人物视觉需统一。','新玫瑰/MA10.09-S1.docx P031-P034；MA70.10.11-S1.docx P102、P110-P114','','rose.history.portrait 为空图占位；rose.club.reception','可一张基础肖像适配两个场景，也可骑士构图与正式肖像各一版，由美术确定；不直接当两张已定。')
add('P03','道具／纸张','西尔万名片／法语手写标题','待补版式或字体方案','漂亮手写字体，法语为主标题、中文为译注；原稿获得三张名片，按一套设计复用。文字含Sylvain Vallon、Professeur d’histoire、J’enseigne à l’Université des Murs-Gris及中文译注。','新玫瑰/MA70.10.11-S1.docx P127及邻近名片正文','','ReadingPage rose.1011.s1.card.page，imageKey为空','当前文本阅读页不等于手写名片美术；可用纸底+排版，不必整图写死文字。')
add('P04','道具／叠图','十几个国王币','待补／先查可复用货币图','11.04报酬分支画面中出现十几个钱币；可以一个币样复制成一组。','王庭教会/MA70.11.04-S1.docx P070','未见明确国王币专用图；通用金钱UI不能直接认定满足。','church.1104.s1 fee.quoted 演出挂点待接','图中数量不替代尚未确定的报酬数值。')
add('P05','道具／剪影','新玫瑰会贵族老爷剪影','已有可复用底图／待接线','隔纱窗看不清容貌的嫡系部下，原文允许挪用贝特朗成年黑色剪影。','新玫瑰/MA10.09-S1.docx P009、P036','Assets/Resources/Characters/bertrand/neutral.png','Object rose.envoy.portraitPath 为空','只是复用视觉，不可把老爷身份改成贝特朗。门口男人原文允许无立绘。')
add('P06','道具／剪影','旧宅门前一高一矮两个人影','待补一组叠层','模糊两人影；建议分开以支持其中一人离开，配BG18。','王庭教会/MA70.11.04-S1.docx P212、P321','','11.04 far/near.old.estate 等','两条路线共用，不新增两个专属角色。')
add('P07','道具／剪影','被驱逐的一家人','待补一组叠层','一个男人、一个抱孩子的女人、一个老人，剪影即可，配BG18。','王庭教会/MA70.11.04-S1.docx P224、P333','','11.04 far/near.old.eviction 等','是群像素材组，不扩为四套表情立绘。')
add('P08','道具／剪影','教会查店队伍','待补群体层／共用通用NPC','一名前排通用教会人员，后方一群人用剪影；前排复用CH33。','王庭教会/MA70.10.20-S1.docx P012','','Object church.inspector / church.inspectors；church.1020.s1','前排通用角色只计一次，后排为人群层。')
add('P09','道具／帽子','宽檐毡帽','暂定／原稿pending','风吹来的漂亮宽檐毡帽，若采用道具贴图方案则做透明素材。','王庭教会/MA70.10.09-S2.docx P026-P027','','帽子选择段；尚无对应图片绑定','和王女日常大头饰并非自动同一素材。')
add('P10','道具／剪影','教堂四组可点击剪影','暂定／有余力才做','四组互动剪影，其中王女须戴宽檐帽；原文允许退回线性对话。','王庭教会/MA70.10.09-S2.docx P008-P013','','church.1009.s2；该精细交互不是当前图片已完成','每组人数、构图未定，不能按四个新专属角色下单。')
add('P11','道具／报纸','圣诺特之声推荐／广告版式','待确认展示方式','10.18记者事件提到下一刊推荐侧栏或商业广告；如要报纸展示，需刊头、栏框、文字版式。','中立/MA70.10.18-S1.docx P057、P080；世界局势设定案（参考，文案里报纸等地方用）.docx','Assets/Art/FreeCafe/Whitebox/future.newspaper.png 是白盒图。','两条 neutral.1018.s1.notice.* 均imageKey为空','当前配置仅文字通知，原文未明确要求独立报纸插画；不列为已批准必画CG。')
# Motion kept separate from still art.
add('FX01','演出效果','淡入淡出、黑幕、震屏、背景平移','已有程序通道／无独立画图量','复用立绘入退场、黑幕、背景平移；供开幕、夜间结束、圣徒街过渡等。','各事件演出说明；中立/MA70.10.07-S1.docx P014','Assets/Resources/Dialogue/PresentationEffectLibrary.asset','accentFxKey、accentTargetKey；rose.club.blindfold、church.execution.black','黑色占位不代表必须画黑色CG；本轮未做运行验收。')
add('FX02','演出效果','索雷尔眼部强调／阿黛尔遮眼闪回','待核演出效果','索雷尔眼部局部强调；阿黛尔放大且画框遮住眼睛，闪回结束恢复。','革命联合相关事件/MA70.10.14-S1.docx P098；MA70.10.28-S1.docx P096','阿黛尔底图已有，索雷尔底图缺。','各事件 accentFxKey/目标；现有舞台效果接口','优先用现有立绘裁切+遮罩，通常不需另画一张CG。')
add('FX03','演出效果','教堂金色流光','暂定／原稿pending','公爵祈福时金光洒下；可提供粒子贴图／光束层，与CG06联动。','王庭教会/MA70.10.09-S2.docx P016-P017','','教堂祈福段','不是音效；是否独立素材由美术和特效共同确认。')
add('FX04','演出效果','教程八类操作演示与高亮','已有经营素材／动画覆盖待核','研磨、烧水、冲泡、装杯、基底、调浓度、甜度、出餐；高亮对应可操作区。','文案-新手教程.docx P044、P051、P055','Assets/Art/FreeCafe/Entities/、Assets/Resources/FreeCafe/ArtLibrary.asset 已有物件与状态图','CafeTutorialStep / FreeCafeTutorialPanel','这是八类演示，不等于要求八段新视频；先复用经营动画。')
add('FX05','演出效果','送餐／蒂博喝咖啡动作','待核动作素材','通用NPC可不做喝咖啡动作，有交互角色需要；蒂博明确要求喝咖啡动画。','文案-新手教程.docx P067；王庭教会/MA70.10.14-S2.docx P017','蒂博常规静态立绘及杯具已有。','church.1014.s2 服务段；教程交付段','是否采用手臂差分／骨骼／Tween由团队确认，静态图不等于动作完成。')
add('FX06','演出效果','经营评价烟花／金钱／负面emoji','待核特效素材','优秀评价烟花+金钱效果；负面评价丑化emoji铺满屏幕约5秒；中等无动画。','文案-新手教程.docx P070','现有经营UI不等同于此套评价效果。','教程评价演出，需核实实际实现','不把喝彩声音纳入美术清单。')
add('FX07','演出效果','仅对话框震动','程序演出项','索雷尔拍打时只震对话框，不能全屏重震替代。','革命联合相关事件/MA70.10.14-S1.docx P212','','对应 dialogue accentFxKey / target','不新增静态画图量。')
# All named character rows, preserving explicit exclusions and existing files.
rows=collections.defaultdict(dict)
for r in sources:
 if r['file']=='人物.xlsx':
  m=re.match(r'人物!([A-Z]+)(\d+)$',r['loc'])
  if m: rows[int(m[2])][m[1]]=r['text']
ids={3:'laurent',4:'clovis',5:'renaud',6:'elion',7:'alienor',8:'queen',9:'bertrand',10:'sade',11:'mamion',12:'lucien',13:'lesou',14:'sylvain',15:'antoine',16:'lefebvre',17:'duval',18:'adele',19:'sorel',20:'sparrow',21:'church.thibaut',22:'octave',23:'eugene',24:'colette',25:'pascal',26:'marie',27:'character.guest_a',28:'character.guest_b',29:'character.guest_c',30:'character.guest_d',31:'soldier_a',32:'soldier_b',33:'soldier_c',34:'soldier_d'}
resids={21:'thibaut',19:'emmanuel'}
objects={x['objId']:x for x in excel['Object']}
for n,row in sorted(rows.items()):
 if n<3: continue
 id=ids[n]; rid=resids.get(n,id); paths=[a['path'] for a in assets if a['path'].startswith('Assets/Resources/Characters/'+rid+'/')]
 name=row['A']; note=''; need='常规角色立绘；外貌依据：'+row.get('H','人物表尚未细写，须参考角色设定。')
 if n in (5,8,17):
  status='不列入本期必画'; need=row.get('C',''); note='人物表明确暂不需要立绘；王后剪影仅有余力时考虑。'
 elif paths: status='已有底图／差分或接线待核'
 else: status='待补底图'
 if n==7:
  status='已有宫廷／日常图；乔装差分待补'; need+='；必须补无圣痕平民装。已有indoor是室内装，不能自动视为合格乔装。'; note='与埃莉诺为同一身份；11.04当前对话仍用neutral基准图；宽檐帽剪影见P10。'
 if n==9: need+='；成年剪影（保留特殊圣痕）+第一幕末少年显身两形态；两份图已存在。'
 if n==19: need+='；10.14出场为工人装束，与人物表常规中产装有区别，应列服装差分或先确认当前阶段只做工人装。'; note='当前NCS id=sorel，旧Catalog id=emmanuel；同一人，勿重复绘制。'
 if n==21: note='现有文件目录thibaut；当前NCS角色church.thibaut的portraitPath为空，需绑定；以“客人”显示不等于换角色。'
 if n==6: note='现有elion底图；当前事件Object church.duke未绑定；同一公爵。'
 if n==25: note='另有Assets/Art/Gameplay/Characters/角色设计_男革命印刷工_法式线描.png概念图，但无帕斯卡尔身份绑定证据；可先请美术确认是否复用。'
 if n==26: note='另有角色设计_女咖啡馆店主_法式线描.png概念图，未证实属于玛丽；不能当该角色已完成。'
 if n==26: need+='；玛丽真实女性身份／雅克男性身份的造型差异待确认，避免直接当两个角色。'
 if n in (27,28): need='通用客人剪影（人物表建议），服饰参考其阵营/角色设定。'
 if n in (29,30,31,32,33,34): need='通用NPC模板；人物表列出，但未给独立差分或每人独立造型要求。'; note='可按阵营/服饰复用；先确认哪些进入当前剧情，避免八个通用NPC都按专属角色制作。'
 if id not in objects: bind='当前Object表未登记该ID；旧CharacterCatalog或人物设定存在。'
 else:
  ob=objects[id]; bind='Object '+id+'；portraitPath='+(ob.get('portraitPath') or '空')+'；portraitIndoorPath='+(ob.get('portraitIndoorPath') or '空')
 add(f'CH{n-2:02}','角色立绘',name,status,need,f'人物.xlsx 人物!A{n}/C{n}/H{n}/J{n}','；'.join(paths),bind,note)
add('CH33','角色立绘','教会人员通用NPC（监察官／看守）','待补通用底图','10.20前排人员、10.23监察官与看守共用教会人员模板；10.23无需专属立绘或表情差分。','王庭教会/MA70.10.20-S1.docx P012；MA70.10.23-S1.docx P006、P012、P015','','Object church.inspector / church.guard 均空图','10.20台词有情绪标签，是否增通用差分另确认；不要因此给10.23角色各做一套。')
add('CH34','角色立绘','记者／通用市民男性','已有简易占位／待替换','10.18通用市民男性；对话立绘与入座营业形象需一致。','中立/MA70.10.18-S1.docx P002、P008','Assets/Resources/Characters/neutral_reporter/placeholder.png（512×512卡通占位）','Object neutral.reporter：portraitPath空，portraitIndoorPath指向占位；CafeVisit seatedVisualPath空','入座占位存在，不等于对话立绘已完成；可复用客人市民模板。')
add('CH35','角色立绘','教程顾客老太太／黑色剪影','待确认复用通用顾客','衣着普通、记不住脸的老太太（顾客B）；初次进店先黑影立绘。可用同类型老年女性通用底图染黑，不能用男性占位当完成。','文案-新手教程.docx P015-P016、P067','Assets/Art/Gameplay/DayFlow/NPC/npc_placeholder_customer_01.png 等通用占位；已有经营顾客图','OpeningPresentation教程数据共用 FirstDayIntroductionVisuals；该资产目前未见centerPortrait字段','不能用阿黛尔或其他专属角色代替顾客；此项不新增专属角色身份。')
# Map requirements and assets verified through actual current configuration.
text={x['key']:x['text'] for x in load(root/'Assets/Settings/Resources/Luban/tbtext.json')}
for i,row in enumerate(excel['MapPosition'],1):
 val=row.get('imgPath') or ''; name=text.get(row['nameText'],row['positionKey']); matches=[a['path'] for a in assets if '/Resources/' in a['path'] and a['path'].split('/Resources/',1)[1].rsplit('.',1)[0]==val]
 status='已有图标／地点语义待核' if matches else '缺文件'
 if 'Unknown' in val: status='未知头像占位／按地点用途确认'
 add(f'MAP{i:02}','地图图标',name,status,'地图上的地点图标；不是剧情全屏背景。','Narrative.xlsx MapPosition!第'+str(row['_excel_row'])+'行','；'.join(matches),'MapPosition '+row['positionKey']+' → '+val,'cafe和圣徒街当前共用location_03。若是临时测试点，不为它专门画正式地点。' if row['positionKey'] in ['cafe','圣徒街','temporary','library'] else '编号图标已存在，但不能仅凭文件名断言地点题材正确。')
add('MAP10','地图图标','地图设定中尚未完整对应的地点','已有图集／先核映射','旧城区、圣安托万堡垒、大教堂、灰墙学院以及俱乐部入口；对照现有location_00~09判断复用或补图。','游戏地图.xlsx 圣诺特城!C5/D10/C13/C14；新玫瑰/MA10.09-S1.docx P003','Assets/Resources/Art/Map/Locations/location_00.png 至 location_09.png；原始地点图标图集','当前MapPosition只有9行，不能把地图表全部地点认定已配置','圣安托万堡垒等仅设定提及，未明确要求当前事件全屏场景；不自动扩画。')
add('MAP11','地图图标','圣诺特城总地图','已有图／地点对应待核','沿卢米恩河分北岸、河心岛、南岸；保留现有总地图，核对各地点标牌位置。','游戏地图.xlsx 圣诺特城!B2等','Assets/Art/Map/GeneratedUI/mapBackgroundSprite.png；Assets/Art/Map/新地图系统/地图/','Assets/Resources/MapVisualTheme.asset','旧city_map_placeholder仅流程占位，不能据此说项目没有正式地图。')
add('REF01','设定参考','五方标志／阵营徽记','文案内已有参考图／导出接入待确认','沸点咖啡馆、奥尔狄亚王庭、圣诺教会、新玫瑰会、革命联合。','势力与派别设定案.xlsx Sheet1!C3-C7','原Excel内嵌5张徽记，见“文案内嵌参考图预览.jpg”。','当前Object及MapPosition没有明确引用这些内嵌图','已见参考图不等于需要重新设计；是否用于UI和是否提供透明源文件待确认。')
add('REF02','设定参考','故事纲要结局与世界设定插画','尚无明确画面订单','故事、思路、世界局势、圣痕学里的事件或道具描述，不自动转为每段一张CG；以后补到具体演出说明再追加。','故事.xlsx；思路构想.xlsx；世界局势设定案（参考，文案里报纸等地方用）.docx；圣痕学.docx','','Ending、旧模板事件、CutScenePanel不等于已有视频需求','两份11.05施工稿只有标题，10.29稿正文为空，没有可据以制作的新画面细节，见逐文件覆盖表。')
# Outputs.
(out/'art_requirements.json').write_text(json.dumps(entries,ensure_ascii=False,indent=2),encoding='utf-8')
with (out/'02_美术需求可筛选表.tsv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.DictWriter(f,fieldnames=list(entries[0]),delimiter='\t'); w.writeheader(); w.writerows(entries)
counts=collections.Counter(e['类别'] for e in entries)
header=f'''聚光灯｜NCS与现有文案美术需求／占位清单
核对日期：2026-10-16（按本次读取的磁盘文件；不含Word尚未保存的改动）
文案目录：D:\\桌面\\文案
项目目录：{root}

给美术的阅读方式
1. 本清单共{len(entries)}个需求组。一个组可能含多张差分，也可能与另一个组共用底图；不是“缺{len(entries)}张图”。
2. 优先看“待补底图／待补／待替换”。“已有图／待接线”应先交程序绑定，不要重复绘制。
3. “暂定／pending／待确认”保留原稿意图，未当成已批准的制作订单。
4. 文件存在仅说明有素材，不代表风格、构图、角色服装或演出验收通过。本轮做了文件、配置、主要图片预览核对，未运行Unity验收。
5. 相同人物、路线和时间差分尽量复用。黑幕、淡入淡出、震屏等优先由程序实现，不算独立CG。
6. 角色表里的无立绘角色、仅被提及的人、正文普通动作，不自动增加美术量。声音不在本清单内。

核对范围
- 原始文案：35份（28 DOCX + 7 XLSX），含单元格批注、人物/阵营内嵌参考图；排除~$锁文件。
- 当前Narrative.xlsx与生成JSON：42个Object、265个ConversationPage、1863个ConversationLine、5个ReadingPage、3个Notice、9个MapPosition、25个OpeningPresentation、3个CafeVisit；所检查美术字段一致。
- 当前背景登记：40个key；38个placeholder=1（含2个黑幕用途和多路线重复）；两个有图条目是同一咖啡馆底图的昼/夜调色。
- 人物原画、Resources角色目录、CharacterCatalog、FirstDayIntroductionVisuals、信封Prefab构建引用、地图图集、教程经营素材均纳入核对。
- 文案原段号为当前DOCX非压缩索引（P001含空段计数），表格为sheet!单元格；工程旧报告段号可能不同，以本清单当前原文提取为准。

先处理的明显缺口
- CG01~CG05五组确定插画；CG06教堂祈福原稿pending，另行确认。CG01可以与圣徒街日景合并。
- 当前事件重点人物：柯莱特、索雷尔工人装、麻雀、西尔万、通用教会人员、记者正式形象；王女无圣痕平民装。
- 场景优先：圣徒街、市场/杂货铺、俱乐部、教堂、旧城/旧宅、郊区、看守所；晨昏和纸花作为差分统筹。
- 蒂博、公爵、贝特朗成年剪影已找到，重点是对应NCS身份和绑定。
- 所有表情标签详见03清单；现有常规立绘不足以表示这些标签已做成差分。

分类统计（需求组，不是张数）
'''+ '\n'.join(f'- {k}：{v}组' for k,v in counts.items())+'\n\n'
lines=[header]
for category in counts:
 lines.append('='*58+'\n'+category+'\n'+'='*58)
 for e in entries:
  if e['类别']!=category: continue
  lines.append(f"\n[ ] {e['编号']}  {e['名称']}\n    状态：{e['状态']}\n    需求：{e['制作或确认内容']}\n    来源：{e['文案依据']}")
  if e['现有资源']: lines.append('    已有：'+e['现有资源'])
  if e['配置对应']: lines.append('    配置：'+e['配置对应'])
  if e['备注']: lines.append('    注意：'+e['备注'])
lines.append('''
交付规格说明
- 源文案并未统一规定像素尺寸、分层文件格式、表情差分张数；本清单不擅自把建议规格当成硬性要求。
- 人物优先保留透明底、统一站姿比例和落脚位置；现有基准角色约743~1432像素宽、1721~2272像素高，交付时由项目主美确定最终规格。
- 背景需适配当前游戏画幅与对话框遮挡区；平移/推近需要留边。用现有正式构图作为统一基准。
- 原稿明确需要叠图的素材（剪影、币、纸花、光束）建议分层交付，以免为了消隐某一人重画整张背景。
- 最终替换仍使用现有NCS字段和NarrativeStageVisuals映射；不要把本清单编号当成新的剧情字段标准。

文件说明
01_给美术的需求与占位清单.txt：本文，可直接转发。
02_美术需求可筛选表.tsv：相同条目，Excel/WPS可打开筛选状态。
03_角色表情差分清单.txt：当前配置和文案表情标签的逐角色去重。
04_程序配置与资源逐项对照.txt：全部背景key、对象美术字段、地图、阅读等绑定证据。
05_逐文件覆盖与原文索引.txt：35份资料逐文件核对范围和美术原文位置。
06_现有图片文件索引.tsv：本次扫描的612个项目图片/源图文件，包含副本，不能用文件数估算美术工作量。
现有资源预览_1/2.jpg、现有地图与信封预览.jpg、文案内嵌参考图预览.jpg、其他文档内嵌图预览.jpg：帮助认图；不代表美术验收。
''')
(out/'01_给美术的需求与占位清单.txt').write_text('\n'.join(lines),encoding='utf-8-sig')
print('entries',len(entries),'categories',dict(counts))
