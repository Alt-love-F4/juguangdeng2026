"""2026-10-20 NPC visual defaults. Source Excel remains the runtime authority.
Fill missing NPC resources with neutral placeholders; never copy another NPC's identity
or overwrite later authored art. Applied after content modules merge their Object rows.
"""
PORTRAIT = 'Characters/generic/neutral_placeholder'
AVATAR = 'Characters/generic/avatar_placeholder'
INDOOR = 'Characters/generic/seated_placeholder'
STALE_TAGS = {'no_portrait', 'portrait_pending', 'portrait-pending', 'C-R04-missing-portrait'}


def apply(sheets):
    changes = []
    for row in sheets['Object']['rows']:
        if row.get('objType') != 'npc':
            continue
        before = dict(row)
        if not str(row.get('portraitPath') or '').strip():
            row['portraitPath'] = PORTRAIT
        if not str(row.get('avtarPath') or '').strip() and not str(row.get('iconPath') or '').strip():
            row['avtarPath'] = AVATAR
        if not str(row.get('portraitIndoorPath') or '').strip():
            row['portraitIndoorPath'] = INDOOR
        if row['portraitPath'] == PORTRAIT:
            tags = [t.strip() for t in str(row.get('tags') or '').split(',') if t.strip() and t.strip() not in STALE_TAGS]
            if 'portrait_placeholder' not in tags:
                tags.append('portrait_placeholder')
            row['tags'] = ','.join(tags)
        fields = {key: {'before': before.get(key), 'after': value} for key, value in row.items() if before.get(key) != value}
        if fields:
            changes.append({'objId': row['objId'], 'fields': fields})
    return changes
