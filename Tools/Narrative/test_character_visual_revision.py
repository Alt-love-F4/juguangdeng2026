import unittest
from character_visual_revision import apply, PORTRAIT, AVATAR, INDOOR


class CharacterVisualRevisionTests(unittest.TestCase):
    def test_missing_npc_resources_are_filled_without_changing_identity_or_eligibility(self):
        row = dict(objId='unknown.worker',objType='npc',nameText='worker.name',tags='revolution,generic,no_portrait,portrait_pending')
        sheets = {'Object': {'rows': [row]}}
        self.assertEqual(1,len(apply(sheets)))
        self.assertEqual((PORTRAIT,AVATAR,INDOOR),(row['portraitPath'],row['avtarPath'],row['portraitIndoorPath']))
        self.assertEqual('unknown.worker',row['objId'])
        self.assertEqual('worker.name',row['nameText'])
        self.assertEqual('revolution,generic,portrait_placeholder',row['tags'])
        self.assertNotIn('cafe_customer',row['tags'])
        self.assertEqual([],apply(sheets))

    def test_authored_portraits_avatars_and_seated_art_survive_content_regeneration(self):
        authored=dict(objId='named',objType='npc',portraitPath='Characters/named/neutral',avtarPath='Characters/named/avatar',portraitIndoorPath='Characters/named/indoor',tags='cafe_customer')
        before=dict(authored)
        self.assertEqual([],apply({'Object': {'rows':[authored]}}))
        self.assertEqual(before,authored)
        authored['portraitIndoorPath']=''
        apply({'Object': {'rows':[authored]}})
        self.assertEqual(INDOOR,authored['portraitIndoorPath'])
        self.assertEqual(before['portraitPath'],authored['portraitPath'])
        self.assertEqual(before['avtarPath'],authored['avtarPath'])
        self.assertEqual(before['tags'],authored['tags'])

    def test_world_player_and_explicit_icon_are_preserved(self):
        rows=[dict(objId='world',objType='world'),dict(objId='player',objType='player'),dict(objId='npc',objType='npc',iconPath='Icons/known')]
        apply({'Object': {'rows':rows}})
        self.assertNotIn('portraitPath',rows[0])
        self.assertNotIn('portraitPath',rows[1])
        self.assertNotIn('avtarPath',rows[2])
        self.assertEqual('Icons/known',rows[2]['iconPath'])


if __name__ == '__main__':
    unittest.main()
