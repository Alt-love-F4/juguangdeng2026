"""Narrow presentation-only source revision; no route/condition/result regeneration."""
from Content.church import _speaker, speech_body, speech_label


def apply(sheets):
    texts = {r['key']: r for r in sheets['Text']['rows']}
    original_texts = {k: dict(v) for k, v in texts.items()}
    changes = []
    for row in sheets['ConversationLine']['rows']:
        page = str(row.get('pageId') or '')
        text = texts.get(row.get('textKey'))
        if page.startswith('church.') and row.get('speakerKey') and text:
            current = row['speakerKey']
            source_text = original_texts[row['textKey']]
            if '[speaker-label-stripped]' in str(source_text.get('comment') or ''):
                continue
            body = source_text.get('text') or ''
            label = speech_label(body)
            # Source generic actors already have verified identities. Named actors must
            # have an explicit exact label; a narrative sentence beginning with a name
            # must never introduce that portrait or take over the nameplate.
            is_speech = bool(label) and (current.startswith('church.generic.') or _speaker(body) == current)
            if not is_speech:
                changes.append({'lineId': row['lineId'], 'kind': 'narration', 'speakerBefore': current})
                row['speakerKey'] = None
                row['side'] = 'none'
            else:
                cleaned = speech_body(body)
                if cleaned != body:
                    changes.append({'lineId': row['lineId'], 'kind': 'speaker-prefix', 'textKey': row['textKey']})
                    text['text'] = cleaned
                    text['comment'] = str(source_text.get('comment') or '') + ' [speaker-label-stripped]'
        elif page.startswith('revolution.route.1124.s1.') and not row.get('speakerKey') and row.get('speakerNameKey'):
            name = texts.get(row['speakerNameKey'], {}).get('text')
            if name == '杜瓦尔':
                row['speakerKey'] = 'duval'
                row['side'] = 'none'
                changes.append({'lineId': row['lineId'], 'kind': 'offstage-identity'})
    return changes
