"""No-output, network-free regression checks for supplied silhouette preparation."""
import unittest
from PIL import Image, ImageOps
from prepare_authored_silhouettes import appearance, source_art, standing, seated


class AuthoredSilhouetteTests(unittest.TestCase):
    def test_seated_retains_full_source_including_feet(self):
        for sex in ('male','female'):
            with self.subTest(sex=sex):
                art=source_art(sex,'seated')
                if sex=='female':
                    art=ImageOps.mirror(art)
                art=ImageOps.contain(art,(262,530),Image.Resampling.LANCZOS)
                expected=Image.new('RGBA',(262,530))
                expected.alpha_composite(art,((262-art.width)//2,530-art.height))
                actual=seated(sex)
                self.assertEqual(expected.tobytes(),actual.tobytes())
                self.assertIsNotNone(actual.getchannel('A').crop((0,354,262,530)).getbbox())

    def test_only_standing_is_cropped_to_top_two_thirds(self):
        for sex in ('male','female'):
            with self.subTest(sex=sex):
                art=source_art(sex,'standing')
                art=art.crop((0,0,art.width,round(art.height*2/3)))
                art=ImageOps.contain(art,(728,1496),Image.Resampling.LANCZOS)
                expected=Image.new('RGBA',(768,1536))
                expected.alpha_composite(art,((768-art.width)//2,1536-art.height-20))
                self.assertEqual(expected.tobytes(),standing(sex).tobytes())

    def test_explicit_female_and_male_placeholder_appearances(self):
        for cid in ('queen','guest_b','guest_d','church.generic.7148666b21','church.generic.5d7522a804'):
            self.assertEqual('female',appearance(cid))
        for cid in ('renaud','duval','octave','guest_a','guest_c','soldier_d'):
            self.assertEqual('male',appearance(cid))


if __name__=='__main__':
    unittest.main()
