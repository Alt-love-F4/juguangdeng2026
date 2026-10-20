import sys
import unittest
from copy import deepcopy
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from dialogue_presentation_revision import apply


class DialoguePresentationRevisionTests(unittest.TestCase):
    def test_repeated_branch_text_is_cleaned_once_without_losing_identity(self):
        sheets = {'Text': {'rows': [dict(key='speech', text='阿黛尔：「你好。」', comment='source'),
            dict(key='prose', text='阿黛尔离开了'), dict(key='duval.name', text='杜瓦尔')]},
            'ConversationLine': {'rows': [dict(lineId='a', pageId='church.test.a', speakerKey='adele', side='right', textKey='speech'),
                dict(lineId='b', pageId='church.test.b', speakerKey='adele', side='right', textKey='speech'),
                dict(lineId='c', pageId='church.test.c', speakerKey='adele', side='right', textKey='prose'),
                dict(lineId='d', pageId='revolution.route.1124.s1.d', speakerNameKey='duval.name', side='right')]}}
        apply(sheets)
        lines = sheets['ConversationLine']['rows']
        self.assertEqual(['adele', 'adele', None, 'duval'], [r.get('speakerKey') for r in lines])
        self.assertEqual('none', lines[2]['side'])
        self.assertEqual('none', lines[3]['side'])
        self.assertEqual('「你好。」', sheets['Text']['rows'][0]['text'])
        before = deepcopy(sheets)
        self.assertEqual([], apply(sheets))
        self.assertEqual(before, sheets)


if __name__ == '__main__': unittest.main()
