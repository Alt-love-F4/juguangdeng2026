"""Source-backed male/female NPC placeholders. Runtime authority remains Object Excel.
Never replace authored art or infer an author's missing gender from a person's name.
"""
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
FEMALE_IDS=set(json.loads((ROOT/'Tools/Characters/silhouette-art-manifest.json').read_text(encoding='utf-8'))['femaleIds'])
PORTRAIT='Characters/silhouettes/male/neutral_placeholder'
AVATAR='Characters/silhouettes/male/avatar_placeholder'
INDOOR='Characters/silhouettes/male/seated_placeholder'
STALE_TAGS={'no_portrait','portrait_pending','portrait-pending','C-R04-missing-portrait'}
LEGACY={
    'portraitPath':'Characters/generic/neutral_placeholder',
    'avtarPath':'Characters/generic/avatar_placeholder',
    'portraitIndoorPath':'Characters/generic/seated_placeholder',
}


def resource_path(character_id,field):
    sex='female' if character_id in FEMALE_IDS else 'male'
    kind={'portraitPath':'neutral','avtarPath':'avatar','portraitIndoorPath':'seated'}[field]
    return 'Characters/silhouettes/'+sex+'/'+kind+'_placeholder'


def apply(sheets):
    changes=[]
    for row in sheets['Object']['rows']:
        if row.get('objType')!='npc':
            continue
        before=dict(row)
        for field in LEGACY:
            path=str(row.get(field)or'').strip()
            known_shared=path.startswith('Characters/silhouettes/') and path.endswith('_placeholder')
            if field=='avtarPath' and not path and str(row.get('iconPath')or'').strip():
                continue
            if not path or path==LEGACY[field] or known_shared:
                row[field]=resource_path(row['objId'],field)
        if row['portraitPath'].startswith('Characters/silhouettes/'):
            tags=[t.strip() for t in str(row.get('tags')or'').split(',') if t.strip() and t.strip()not in STALE_TAGS]
            if 'portrait_placeholder'not in tags:
                tags.append('portrait_placeholder')
            row['tags']=','.join(tags)
        fields={key:{'before':before.get(key),'after':value} for key,value in row.items() if before.get(key)!=value}
        if fields:
            changes.append({'objId':row['objId'],'fields':fields})
    return changes
