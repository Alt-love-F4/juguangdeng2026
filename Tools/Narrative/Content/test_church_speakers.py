"""Focused presentation semantics, independent of unrelated source hash gates."""
import unittest
from Tools.Narrative.Content import church


class ChurchSpeakerTests(unittest.TestCase):
    def test_named_prose_and_inner_thoughts_are_not_speech(self):
        for text in [
            '阿黛尔是什么意思？',
            '阿黛尔离开之后，我和埃莉诺在咖啡馆中随机找了个座位坐下',
            '王女离开后，阿黛尔小心翼翼地凑过来',
            '阿莉耶诺尔，不知道你是否能够走出一条不一样的道路呢？',
            '阿黛尔看着我说：这是真的吗？',
        ]:
            self.assertIsNone(church._speaker(text), text)
            self.assertEqual(text, church.speech_body(text) if church._speaker(text) else text)

    def test_only_explicit_source_labels_resolve_and_prefix_is_removed(self):
        for text, speaker, body in [
            ('阿黛尔：「欢迎光临！」', 'adele', '「欢迎光临！」'),
            ('阿黛尔·米尼耶：欢迎光临！', 'adele', '欢迎光临！'),
            ('阿黛尔`米尼耶: 欢迎光临！', 'adele', '欢迎光临！'),
            ('埃利昂公爵：「吾主圣光」', 'elion', '「吾主圣光」'),
            ('{playername}；原来如此！', 'player', '原来如此！'),
            ('王女：谢谢你。', 'alienor', '谢谢你。'),
        ]:
            self.assertEqual(speaker, church._speaker(text))
            self.assertEqual(body, church.speech_body(text))

    def test_compiler_line_keeps_prose_offstage_and_strips_only_speaker_label(self):
        compiler = church._Compiler(None)
        root = object.__new__(church._Root)
        root.c = compiler
        root.root = 'church.test'
        root.path = type('Source', (), {'name': 'fixture.docx'})()
        root.source = ['阿黛尔离开了', '阿黛尔：「再见！」', '男人：「谢谢。」']
        root.audit = {'decisions': []}
        root.mark = lambda *args: None
        for i in range(1, 4):
            root.line(i, page={'pageId': 'church.test.'+str(i)})
        lines = compiler.rows['ConversationLine']
        self.assertEqual('none', lines[0]['side'])
        self.assertNotIn('speakerKey', lines[0])
        self.assertEqual('adele', lines[1]['speakerKey'])
        self.assertTrue(lines[2]['speakerKey'].startswith('church.generic.'))
        self.assertEqual(['阿黛尔离开了', '「再见！」', '「谢谢。」'],
            [r['text'] for r in compiler.rows['Text'] if r['key'].startswith('church.test.text.')])


if __name__ == '__main__':
    unittest.main()
