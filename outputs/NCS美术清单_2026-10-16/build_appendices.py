from pathlib import Path
import json,re,collections,csv,hashlib,datetime
from docx import Document
out=Path(__file__).parent; root=out.parents[1]
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
sources=load(out/'source_records.json'); inv=load(out/'source_inventory.json'); assets=load(out/'asset_inventory.json'); refs=load(out/'config_references.json'); stage=load(out/'stage_inventory.json'); excel=load(out/'excel_art_rows.json')
text={x['key']:x['text'] for x in load(root/'Assets/Settings/Resources/Luban/tbtext.json')}; objs={x['objId']:x for x in excel['Object']}
# supplement text in Word text boxes (not normal paragraphs)
extras=[]
for a in inv:
 p=Path(r'D:\桌面\文案')/a['file']
 if p.suffix!='.docx': continue
 d=Document(p)
 for i,para in enumerate(d.paragraphs,1):
  full=''.join(para._p.xpath('.//w:t/text()'))
  if full and full!=para.text: extras.append({'file':a['file'],'loc':f'P{i:03}（含文本框）','text':full})
(out/'source_textbox_supplement.json').write_text(json.dumps(extras,ensure_ascii=False,indent=2),encoding='utf-8')
# Expressions: combine all generated tags with source-only tags.
em=collections.defaultdict(lambda:collections.defaultdict(list)); alias={'阿黛尔·米尼耶':'adele','阿黛尔':'adele','柯莱特·布吕内':'colette','安托万·马尚':'antoine','埃莉诺':'alienor','索雷尔':'sorel','埃马纽埃尔·索雷尔':'sorel','？？':'sorel','？？？':'sorel','麻雀':'sparrow','西尔万':'sylvain','客人':'church.thibaut','埃利昂公爵':'church.duke','监察官':'church.inspector','疑似领头的人':'church.inspector','{playername}':'player'}
for x in excel['ConversationLine']:
 if x.get('emote'): em[x['speakerKey']][x['emote']].append('ConversationLine '+x['lineId'])
for r in sources:
 if not r['file'].endswith('.docx'): continue
 tags=re.findall(r'[#＃]([\u4e00-\u9fffA-Za-z]+)',r['text'])
 if not tags: continue
 head=re.split('[：:]',r['text'],maxsplit=1)[0]; head=re.sub(r'[（(].*','',head).strip()
 if head not in alias:
  # actor entry direction, e.g. 【立绘出场：阿黛尔...】
  if '立绘出场' in r['text']:
   head=re.split('[：:]',r['text'],maxsplit=1)[-1]; head=re.sub(r'[（(].*','',head).strip('【】[] ')
 if head in alias:
  for tag in tags: em[alias[head]][tag].append(r['file']+' '+r['loc'])
cn={'neutral':'平静／常态','happy':'开心','angry':'生气','cautious':'小心翼翼','confused':'迷惑','dejected':'沮丧','embarrassed':'尴尬','sad':'悲伤','thinking':'思考／思索','excited':'兴奋'}
lines=['角色表情／演技标签清单（2026-10-16）','范围：当前ConversationLine.emote + 文案中的#标签。相近词可合并表情设计，但清单保留原词。','重点：小声、点头、吃力等可能是语气/动作，不机械要求每个词画一张差分。','CharacterProfile.PortraitFor 当前仅支持happy/angry专用字段；LoadFromTables只填基准portrait和室内worldSprite，未建立全部标签的差分映射。旧CharacterCatalog的happy/angry也为空。','因此“文案/配置有表情标签”不代表“已经显示出该表情”，交图后还需程序在既有系统内接入。','当前角色底图目录未见完整表情差分集合；不把普通同名副本当新表情。','教会10.23明确使用通用NPC且无需专属差分，保留此要求；主角开心只表示台词演技，不由此强制增加整套主角立绘。','']
for who,tags in em.items():
 name=text.get(objs.get(who,{}).get('nameText',''),who)
 lines.append(f'[{who}] {name}')
 for tag,locs in sorted(tags.items()):
  distinct=list(dict.fromkeys(locs)); label=tag+('（'+cn[tag]+'）' if tag in cn else '')
  lines.append('  [ ] '+label+'；出现'+str(len(distinct))+'处；例如：'+'；'.join(distinct[:3]))
 lines.append('')
lines += ['角色身份合并：埃莉诺=阿莉耶诺尔（alienor）；sorel=旧emmanuel；church.thibaut=旧thibaut；church.duke=旧elion。','王女另需服装差分：无圣痕平民装；索雷尔另需工人装，均不能只靠表情覆盖。']
(out/'03_角色表情差分清单.txt').write_text('\n'.join(lines),encoding='utf-8-sig')
(out/'expression_references.json').write_text(json.dumps(em,ensure_ascii=False,indent=2),encoding='utf-8')
# Technical crosswalk.
lines=['NCS配置与现有资源逐项对照（2026-10-16）','源表：Assets/ConfigTables/Data/Narrative/Narrative.xlsx；文本表：Assets/ConfigTables/Data/Shared/Text.xlsx','生成配置：Assets/Settings/Resources/Luban/*.json','美术字段Excel/JSON对照：本次检查0处差异。读取覆盖详见excel_art_rows.json与excel_json_differences.json。','此为静态文件/绑定核对，不代表Unity运行通过或图片最终验收。','', '一、全部43个ConversationPage背景值（同图重复key已列明）']
g=collections.defaultdict(list)
for x in excel['ConversationPage']:
 if x.get('bgOverride'): g[x['bgOverride']].append(x)
for key,pages in g.items():
 st=next((s for s in stage if s['key']==key),None); files=next((r['files'] for r in refs if r['field']=='bgOverride' and r['value']==key),[])
 lines.append('\nKEY: '+key)
 if st:
  lines.append('  登记状态：placeholder='+str(st['placeholder'])+'；贴图='+(st['file'] or '空')+'；tint='+str(st['tint']))
  if key in ['rose.club.blindfold','church.execution.black']: lines.append('  用途：黑幕，通常无需美术另画纯黑图。')
 elif files: lines.append('  直接Resources路径：'+'；'.join(files))
 else: lines.append('  未找到Resources文件或登记项，需工程核实。')
 lines.append('  引用页：'+'；'.join(x['pageId'] for x in pages))
lines += ['', '二、全部42个Object美术字段（空字段不自动等于要新增立绘）','当前运行角色源为Object表；旧CharacterCatalog.asset中有图不代表该Object已绑定。']
for ob in excel['Object']:
 lines.append('\n'+ob['objId']+' / '+text.get(ob.get('nameText',''),ob.get('nameText',''))+' / '+str(ob.get('objType')))
 for k in ['portraitPath','portraitIndoorPath','avtarPath','iconPath']: lines.append('  '+k+' = '+str(ob.get(k) or '空'))
 used=[x for x in excel['ConversationLine'] if ob['objId'] in [x.get('speakerKey'),x.get('stageLeftKey'),x.get('stageRightKey'),x.get('stageCenterKey')]]
 lines.append('  当前对话使用：'+str(len(used))+'行；示例：'+'；'.join(x['lineId'] for x in used[:4]))
 if ob['objId'] in ['npc_a','npc_b','npc_c','npc_d']: lines.append('  旧示例角色/模板引用，旧Catalog用Unknown头像；不混入当前文案正式角色交图量。')
 if ob['objId'] in ['world','cafe','player','oct07.passerby.a','unknown','neutral.unknown','church.broadcast','rose.butake','neutral.shopkeeper'] or ob['objId'].startswith('church.1104.s1.npc.'):
  lines.append('  文案未要求此ID各自专属立绘；世界对象/主角/旁白/匿名/群像需按实际演出处理。')
lines += ['', '三、全部非空图片引用去重（排除背景，已在上一节展开）']
g=collections.defaultdict(list)
for r in refs:
 if r['field']!='bgOverride': g[(r['table'],r['field'],r['value'])].append(r)
for (table,field,key),rs in g.items():
 lines += [f'\n{table}.{field}: {key}','  文件：'+'；'.join(rs[0]['files']),'  使用者：'+'；'.join(x['id'] for x in rs)]
lines += ['', '四、阅读、通知、入座和Opening数据']
for sheet in ['ReadingPage','Notice','CafeVisit','OpeningPresentation']:
 lines.append('\n'+sheet)
 for r in excel[sheet]:
  fields=['pageId','presentationKey','presentationId','imageKey','visitKey','characterId','seatedVisualPath','visualsResource']
  lines.append('  '+' | '.join(f'{k}={r.get(k) or "空"}' for k in fields if k in r))
lines += ['','5个ReadingPage和3个Notice的imageKey均为空：2个测试阅读页、1个测试通知不形成正式画图订单；其余信件/名片见主清单。','3个CafeVisit的seatedVisualPath均为空：从CharacterProfile的室内/基准资源回退，记者室内图是简易占位。','25条OpeningPresentation共用Dialogue/FirstDayIntroductionVisuals：当前左立绘为空、右立绘为阿黛尔、背景为架空咖啡馆_01；序列化未见centerPortrait。教程人物名字在表中不代表已配顾客图。','', '五、旧Catalog与已有角色目录（身份核对用，不能代替Object）']
cat=load(out/'catalog_inventory.json')['characters']
for c in cat: lines.append(c['characterId']+' / '+c['displayName']+' → '+(c.get('portrait') or '无立绘')+'；happy='+(c.get('portraitHappy') or '空')+'；angry='+(c.get('portraitAngry') or '空'))
lines += ['', '六、对照中应保留的区别','1. 任意图片文件存在 ≠ 已有美术定稿 ≠ 当前NCS已绑定。','2. 角色Object.portraitIndoorPath对应营业形象回退，不自动改变剧情服装；不能把alienor/indoor当成11.04乔装已合格。','3. 所有43个背景值均有资源或背景登记可静态定位；40个登记项中38个显式占位；两黑幕用途不计缺画。','4. 当前图片引用没有发现指向不存在文件的非空路径；主要缺口是空字段、占位底色、错误服装/差分和未接线。','5. 多路线同地点优先复用；共40个背景key不能折算成40张背景订单。','6. 模板SetupParams/旧CutScene事件与CutScenePanel入口不构成源文案已要求某个具体视频。','7. 本轮不改动任何配置、代码、Prefab或源文案。']
(out/'04_程序配置与资源逐项对照.txt').write_text('\n'.join(lines),encoding='utf-8-sig')
# Source coverage per file, including exact visual hits and embedded media.
terms=['CG','立绘','头像','肖像','背景','插图','图片','画面','美术','差分','剪影','动画','视频','图标','过场','插画','场景转换','素材','名片']
notes={
 '世界局势设定案（参考，文案里报纸等地方用）.docx':'两段文本+世界局势参考地图（已看图）；外部国家未具体设定，不推导新场景订单。',
 '中立对话+地图故事表格.xlsx':'地图地点/12轮Loops与民众态度设计；ROLES列实际指定柯莱特，其余是地点与对白设计，无明确新增CG订单。',
 '人物.xlsx':'完整32行角色（含明确暂不需要者）及9个角色内嵌参考图组，已对应主清单CH01-CH32。',
 '势力与派别设定案.xlsx':'已看5方内嵌徽记；按已有设计参考列入REF01，不误报全部缺图。',
 '咖啡厅中立对话.xlsx':'主要为必要/非必要中立对话分组模板，没有明确新增图像制作说明。',
 '圣痕学.docx':'圣痕位置、形状、规则/破碎的视觉设定，作为角色设计约束；没有要求一张独立CG。',
 '思路构想.xlsx':'分支、结局构想；没有明确分镜/插画指令，不自动为每个结局画CG。',
 '故事.xlsx':'主线/时间轴；明确3-4张复杂CG的控制意见与新玫瑰知觉CG线索；内嵌两张故事流程图已看，不是额外美术订单。',
 '文案-新手教程.docx':'包含场景、顾客老太太剪影、操作动画、结算效果；评分截图为玩法参考。另补取P047文本框。',
 '派别（旧）.docx':'旧设定参考，身份以现有人物表/事件为准；不据旧稿给明示不需要立绘的角色新增订单。',
 '游戏地图.xlsx':'地理设定+已有地图参考图；与现有MapPosition、图标对应，图标题材不等于全屏场景。',
 '革命联合相关事件\\MA70.10.29-S1.docx':'文档正文为空（XML也无w:t文本），本轮无可提取的画面需求。',
 '王庭教会\\【施工中】MA70.11.05-S1.docx':'仅标题/触发说明，未有可下单的画面描述。',
 '王庭教会\\【施工中】MA70.11.05-S2.docx':'仅标题/触发说明，未有可下单的画面描述。',
 '新玫瑰\\MA70.10.16-S1.docx':'短篇未完稿；已提取埃莉诺/萨德和一楼/二楼条件场景。'}
lines=['逐文件覆盖与美术原文索引（2026-10-16）','35份原稿全部纳入；正文/单元格和批注提取，不将锁文件作为文案。','源文案保持原样；P段号对应Word主正文含空段的原位置，不与旧工程报告混用。','35份文案SHA256已复核，较首次提取无变化。内嵌图统计包括原Office包媒体条目，不直接等于需求张数。','officecli在本机是另一套同名TUI工具，文档view命令不可用；实际使用python-docx/openpyxl及OpenXML只读解析，并对关键内嵌图作视觉查看。','']
for a in inv:
 file=a['file']; rs=[r for r in sources if r['file']==file]; hits=[r for r in rs if any(t.lower() in r['text'].lower() for t in terms)]
 lines += ['='*58,file,f'非空文本项：{a["records"]}；Office包内媒体条目：{a["embedded_images"]}', 'SHA256：'+a['sha256'],'处理：'+notes.get(file,'已提取背景、角色、表情及演出/道具要求，合并到主清单；普通叙述不逐句扩为插画。')]
 if not hits: lines.append('未命中明确美术关键词；仍纳入角色/地点/设定检查。')
 for r in hits: lines.append(r['loc']+'：'+r['text'])
 for r in extras:
  if r['file']==file: lines.append(r['loc']+'：'+r['text'])
 lines.append('')
(out/'05_逐文件覆盖与原文索引.txt').write_text('\n\n'.join(lines),encoding='utf-8-sig')
with (out/'06_现有图片文件索引.tsv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.DictWriter(f,fieldnames=['path','width','height','size','sha256'],delimiter='\t',extrasaction='ignore');w.writeheader();w.writerows(assets)
# Read snapshot hashes for comparison later.
paths=[root/'Assets/ConfigTables/Data/Narrative/Narrative.xlsx',root/'Assets/ConfigTables/Data/Shared/Text.xlsx',root/'Assets/Resources/Dialogue/NarrativeStageVisuals.asset',root/'Assets/Resources/Characters/CharacterCatalog.asset']+list((root/'Assets/Settings/Resources/Luban').glob('*.json'))
manifest={'checked_at':datetime.datetime.now().astimezone().isoformat(),'sources':inv,'config_hashes':{p.relative_to(root).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in paths},'art_group_count':len(load(out/'art_requirements.json')),'static_only':True,'source_textbox_supplements':len(extras)}
(out/'audit_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print('expression actors',len(em),'distinct actor/tags',sum(map(len,em.values())),'textbox supplements',len(extras))
