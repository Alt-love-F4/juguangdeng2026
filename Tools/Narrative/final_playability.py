"""Approved final-playability central rules. Applies idempotently to source-row dictionaries."""

def apply(sheets):
    def rows(name): return sheets[name]['rows']
    conditions = rows('Condition')
    conditions[:] = [c for c in conditions if not str(c.get('conditionId','')).startswith('playable.route.')
                    and not (c.get('ownerType') == 'choice' and str(c.get('target','')).startswith('route.eligible.'))]
    # Eligibility is read from committed player actions, never an unproduced construction flag.
    routes = {
        'church': ('church.1111.s1.choice.s4.1', ['story.church.1104.s1.completed']),
        'rose': ('church.1111.s1.choice.s4.2', ['story.rose.1024.allegiance', 'story.rose.1101.s1.completed']),
        'revolution': ('church.1111.s1.choice.s4.3', ['story.revolution.1029.s1.cooperated','story.revolution.1107.s1.completed']),
    }
    for faction, (choice, facts) in routes.items():
        for index, fact in enumerate(facts):
            conditions.append(dict(conditionId=f'playable.route.{faction}.{index}', ownerType='choice', ownerId=choice,
                                   groupIndex=1, condType='BOOL_EQ', target=fact, value='true'))
        conditions.append(dict(conditionId=f'playable.route.{faction}.inclination', ownerType='choice', ownerId=choice,
                               groupIndex=1, condType='BAR_GTE', target='faction.'+faction, value='0'))
    # Paid advertising has a visible, conditional payoff in the next scheduled newspaper.
    if all(name in sheets for name in ['ReadingPage','ReadingLine','Text']):
        page='playable.newspaper.1021.advert'
        additions={
            'ReadingPage':('pageId',dict(pageId=page,presentationKey='newspaper.1021.presentation',seq=2,titleKey=page+'.title',pageCondKeys='neutral.newspaper.advertisement')),
            'ReadingLine':('lineId',dict(lineId=page+'.line',pageId=page,seq=1,mode='narration',textKey=page+'.text')),
        }
        for table,(key,row) in additions.items():
            rows(table)[:]=[r for r in rows(table) if r.get(key)!=row[key]];rows(table).append(row)
        for key,text in [(page+'.title','商讯 · 沸点咖啡馆'),(page+'.text','圣徒街的沸点咖啡馆现已重新营业。香醇咖啡与新鲜点心，欢迎各位街坊前来小坐。\n（店长委托刊登的广告）')]:
            rows('Text')[:]=[r for r in rows('Text') if r.get('key')!=key];rows('Text').append(dict(key=key,text=text,comment='Final playable revision: paid advertisement payoff'))
    return {'routePolicy':'Committed cooperation/meeting + non-negative faction inclination; no fabricated eligible flags.'}
