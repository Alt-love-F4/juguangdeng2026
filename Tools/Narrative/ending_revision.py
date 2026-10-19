"""Author-approved 2026-10-19 ending presentation revision. No trigger/fact changes.
Only integrate_content.py writes these titles/messages into the existing source Excel.
Art metadata is exported as a design manifest, never read by gameplay.
"""
REVISION = "2026-10-19"
ENDINGS = [
 dict(endingId="neutral.ending.a", id="END-N-A", displayCode="中立 A / End if1", title="平凡之日", tone="warm", source="MA70.11.11-S1(中立结局A）.docx", textOrigin="新拟·用户定稿", message="沸点咖啡馆终究关了门。\n我换了营生，也换了住处，日子一天天规律起来。\n圣徒街没有因为少了那间铺子就停下脚步。\n只是偶尔，我仍会在某个安静的夜里为自己煮一杯咖啡——\n那味道，和从前一样。", brief="夜。主角在狭小新住处的小桌边坐下，就着灯端起一杯自煮的咖啡；柜上摆着叔叔留下的旧挂钟，钟面刚被擦过。窗外是圣诺特的夜，远处卢米恩河上船只驶过桥下。暖黄灯光与冷蓝窗色对比，人物可作半身或背影，突出平淡的余韵。"),
 dict(endingId="neutral.ending.b", id="END-N-B", displayCode="中立 B", title="远方的咖啡馆", tone="warm", source="MA70.11.22-S1包含中立结局B.docx", textOrigin="新拟·用户定稿", message="我们最终在一座远离王都的小镇安顿下来。\n铺面不大，桌椅漆色各不相同，招牌却还是那个名字。\n第一位客人推门进来时，阿黛尔笑得像从前一样。\n有些东西换了地方，却并没有走远。", brief="清晨，外省小镇。临街的小铺面挂着新刷过漆的沸点咖啡馆木招牌；阿黛尔在窗边摆正一张桌子，回头笑；远处集市上农户摆摊、运货马车沿石板路经过。暖色晨光，画面明亮开阔，与王都的阴冷形成对照。"),
 dict(endingId="neutral.ending.c", id="END-N-C", displayCode="中立 C / End if3", title="沸点依旧", tone="warm", source="MA70.11.30-S2中立结局C.docx", textOrigin="新拟·用户定稿", message="革命改变了许多东西，日子却还是要照常过下去。\n面包要烤，店门要开，账本上的数字不会自己变整齐。\n阿黛尔仍旧记着她的新点心，墙上的旧钟不紧不慢地走着。\n沸点咖啡馆的招牌安静地悬在门前——和从前一样。", brief="傍晚，沸点咖啡馆打烊后的店内。壁炉余火暖光，桌上压着阿黛尔留下的点心配方，旁边一只空杯。窗外圣徒街路灯次第亮起，远处有报童与修缮门窗的工人剪影。静谧、温暖，略带一天的疲惫。"),
 dict(endingId="neutral.ending.d", id="END-N-D", displayCode="中立 D", title="商业大亨", tone="warm", source="MA70.11.30-S1中立结局D商业大亨.docx", textOrigin="新拟·用户定稿", message="谈判成功之后，圣诺特的商业渐渐恢复了往日的繁忙。\n我开起了第二家沸点咖啡馆，也成了协会里说得上话的人。\n只是每当经过圣徒街，我仍会顺道回到最初的那家店。\n阿黛尔说，我如今很少亲自站在柜台后面了。\n那杯咖啡的味道，比记忆中稍有不同，却依旧熟悉。", brief="沸点咖啡馆窗边，夕阳穿过玻璃在桌面染上一层金色。主角端着咖啡坐在窗边，柜台上摊着新的账本与几封未拆的商业信件；窗外圣徒街车马往来、灯火初上。构图稳重、明快，暗示生意版图的扩张。"),
 dict(endingId="rose.ending.trust_limit.ending", id="END-R-IF1", displayCode="新玫瑰 / End if1", title="信任的界限", tone="cold", source="MA70.10.26-Short Ending 1 信任的界限.docx", textOrigin="新拟·末句原文沿用", message="最先消失的是声音，然后是意识。\n我不知道 B 太太究竟是什么人，也没能问出口。\n只记得叔叔很早以前说过的话——\n「世界不是非黑即白的。有时坏人值得信任，好人却会强迫你牺牲。」\n「一定要擦亮眼神，弄清该信任什么人。」", brief="大堂区街头，人来人往。慈祥的B太太站在主角身前，一手轻扶主角手臂，另一手已将匕首没入主角腹部；血在石板地上慢慢铺开。主角神情错愕，旁观路人皆作模糊处理。低饱和冷调，唯血为暖色。非特写、不另扩展血腥表现。"),
 dict(endingId="rose.ending.job_requirements.ending", id="END-R-IF2", displayCode="新玫瑰 / End if2", title="工作需求要看清", tone="cold", source="MA70.11.01—结局分支 Short Ending2 工作需求要看清.docx", textOrigin="新拟·用户定稿", message="贝特朗的声音很轻，像在念一句悼词。\n我没有说谎，我确实不想让沸点成为棋盘上的走卒。\n可一个从一开始就落在棋盘上的人，没有资格这样抱怨。\n飞镖落下时，我最后想到的，是那家还没打烊的咖啡馆。", brief="沸点咖啡馆一角，孩童身形、神色冷冽的贝特朗站在主角对面；一根飞镖停在空中、划出弧线，主角背抵墙面、无处可退。灯光昏暗，构图压低主角、抬高贝特朗，营造黑色幽默式的压迫感。"),
 dict(endingId="rose.route.1115.s1.ending.ideal_world", id="END-R-NORMAL", displayCode="新玫瑰 / NORMAL ENDING", title="理想新世界", tone="cold", source="NR70.11.15-S1 结局分支-Normal Ending 理想新世界.docx", textOrigin="原文沿用·用户提供末段", message="玫瑰王率领部众发动了一次彻底的政变，将王室、教会、革命联合诸人全部枭首示众。\n一场全方位的清洗带来了全方位的服从，他成了民众心中唯一的君主。\n直到他在亲征途中死于箭伤，新一轮的诡谲风云才重新攀上奥尔狄亚王国的天空。", brief="王都城门外，长长的围观人群与看客；城楼上悬着示众的人头浮屠。远处，孩童之躯的玫瑰王立在高处，万民跪伏。构图宏大而阴冷，金色阳光与暗红阴影对比。远景表现，不追加血腥特写。作者终局闸门仍关闭。"),
 dict(endingId="rose.route.ending.bloom.ending", id="END-R-TRUE", displayCode="新玫瑰 / TRUE ENDING", title="另一种绽放", tone="cold", source="NR结局-True Ending：另一种绽放.docx", textOrigin="原文沿用·用户提供结算段", message="由于{playername}店长在经营沸点咖啡时的不懈努力，\n新玫瑰会在一夜之间接管王都，虽然不可避免地造成了血腥，\n但至少没有把任何人的头颅挂上城门示众。\n活下来的诸人都开启了意料之外，情理之中的新征程。", brief="沿用原文：二楼包厢→一楼大堂；结算后逐角色立绘＋文字滑动。首屏复用完整二楼对话构图，不使用经营原始底图。角色小传沿用原稿顺序，不用一张新CG冒充整段小传。", reuseKey="cafe.second_floor.day"),
 dict(endingId="rose.route.ending.unforgiven.ending", id="END-R-BAD", displayCode="新玫瑰 / BAD ENDING", title="无法原谅", tone="cold", source="NR结局-结局分支—BAD Ending：无法原谅.docx", textOrigin="原文沿用·信件末段", message="我走了。\n你要好好的。\n\n{playername}\n一个无法被原谅的人。", brief="清晨，空荡的沸点咖啡馆。桌上摊着那封写给阿黛尔的信，纸角被一只空杯压着；门口透进的光在地板上拉出长长的影子。画面无人，冷清、静默、悲凉。"),
 dict(endingId="church.ending.if2", id="END-C-IF3", displayCode="王庭教会 / End if3", title="茫然的历史参与者", tone="cold", source="MA70.10.23-S2-End if2.docx（新编号End if3，暂不改文件名）", textOrigin="新拟标题·原文末段沿用", message="不知道今日是何日，不知道咖啡馆怎么样了，不知道阿黛尔怎么样了，不知道我离开之后神秘人是否还在接触咖啡馆……\n不知道……不知道……什么都不知道……\n这里是哪里？刑场吗？我要死了吗？为什么？为什么？为什么？\n带着这样的想法，我迎来了人生的终点。", brief="刑场远景。灰白天空下，主角被两名制服士兵押上行刑台，只是一个渺小的背影；前景模糊的围观人群与欢呼剪影，背景是人流与旗帜裹挟的王都街景。近乎黑白。末尾一帧红为可选演出需求，静态CG不扩成视频。内部旧endingId保留存档兼容。"),
 dict(endingId="church.ending.if4", id="END-C-IF4", displayCode="王庭教会 / End if4", title="再见，亚拿尼亚", tone="cold", source="MA70.11.11-S1.docx（王庭教会·用户给定P0091）", textOrigin="新拟标题·原文末段＋标题收尾", message="来不及跟阿黛尔说一声，来不及留一张纸条，就这样被带走了。\n我又被关进了监察司的看守所。\n很快，我就失去了意识……\n再见，亚拿尼亚。", brief="清晨的沸点咖啡馆。吕西安的背影刚走出门口，门外已涌入一排监察司制服的身影；主角被夹在中间回头，望向空着的吧台，桌上没喝完的拿铁还冒着热气。冷调，主角框在门口的光与室内的暗之间。亚拿尼亚所指待确认，不据此新增角色或立绘。"),
 dict(endingId="church.ending.ry1", id="END-C-RY1", displayCode="王庭教会 / 结局1", title="美丽新世界", tone="cold", source="RY70.11.23-S1【结局1&2】.docx", textOrigin="原文沿用·用户提供末段", message="一个没有圣痕的王室继承人，「背叛」自己的出身，\n去创造一个面向大多数底层百姓的世界——那一定很有意思。\n阿莉耶诺尔·康多尔，我也是如此相信，并且期待着你所能创造出的新世界。", brief="原文可选CG：王女与主角握手，角落阿黛尔欣慰地笑。即位CG升级备选：教堂外，阿莉耶诺尔立于台阶，民众剪影簇拥；主角与阿黛尔在人群边，暖光、希望感。只保留一张需求，原握手/即位二选一待美术确认；作者终局闸门仍关闭。", optional=True),
 dict(endingId="church.ending.ry2", id="END-C-RY2", displayCode="王庭教会 / 结局2", title="历史的分叉路，左还是右？", tone="cold", source="RY70.11.23-S1【结局1&2】.docx", textOrigin="原文沿用·用户提供末段", message="究竟是哪里做错了？\n因为我没有选择埃利昂？因为我选择了王女？还是因为我放弃了革命联合和新玫瑰会？\n如果能够重来，我该走向哪一条路？\n带着这样的不甘和疑问，我迎来了人生的终点。", brief="夜，沸点咖啡馆。王庭禁卫军自门口涌入，将主角与阿黛尔围在中间；埃利昂站在门口，冷冷俯视，身后是长廊暗影。阴冷、绝望。岔路意象仅为可选替代构图，不重复计图。作者终局闸门仍关闭。"),
 dict(endingId="revolution.end01", id="END-RE-01", displayCode="革命联合 / END01", title="历史会判我无罪", tone="cold", source="RE70.11.24-S1（结局）.docx", textOrigin="原文沿用·用户提供结尾", message="革命，革命在呼唤。我必须如此，我只能如此。\n历史，历史在前进。我必须如此，我只能如此。\n……\n历史会判我无罪。", brief="燃烧中的王宫／市政厅为背景，勒苏率军攻入的剪影；前景主角站在欢呼人群之外，低头看着自己的手。暖红火光与冷色人群对比，宏大与沉重并存。"),
 dict(endingId="revolution.end02", id="END-RE-02", displayCode="革命联合 / END02", title="历史尚未结束", tone="warm", source="RE70.11.24-S1（结局）.docx", textOrigin="原文沿用·用户提供结尾", message="过去发生的一切，我都无法让它重新来过。\n但明天究竟会是什么样子，总还有些事情值得去做。\n历史，历史仍在前进，\n历史，历史尚未结束。", brief="北港乡野小屋的傍晚。主角站在叔叔房门外，门缝透出暖黄灯光；阿黛尔拎着一袋菜从走廊那头走来，二人走向亮着灯的厨房。温柔、安静，留有余韵。本项按明确画面保留暖光，不机械套暗线冷调。"),
]

def apply(sheets):
    endings={r['endingId']:r for r in sheets['Ending']['rows']}
    texts={r['key']:r for r in sheets['Text']['rows']}
    for item in ENDINGS:
        eid=item['endingId']
        if eid not in endings: raise ValueError('Missing existing ending: '+eid)
        for field,value in [('titleText',item['title']),('messageText',item['message'])]:
            key='ending.presentation.'+eid+'.'+field
            endings[eid][field]=key
            texts[key]={'key':key,'text':value,'comment':REVISION+' user-approved ending presentation; '+item['textOrigin']+'; '+item['source']}
    # Source-spelling reference only: never change stable event/ending IDs or facts.
    for row in texts.values():
        value=row.get('text')
        if isinstance(value,str) and '进入end if2' in value and str(row['key']).startswith('church.1023.s1'):
            row['text']=value.replace('进入end if2','进入end if3')
    sheets['Text']['rows']=list(texts.values())
