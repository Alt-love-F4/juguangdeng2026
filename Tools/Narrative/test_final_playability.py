import copy
import unittest
from Tools.Narrative.final_playability import apply

class FinalRoutePolicyTests(unittest.TestCase):
    def test_real_conditions_replace_unproduced_eligibility(self):
        sheets={'Condition':{'rows':[dict(conditionId='old',ownerType='choice',target='route.eligible.church')]}}
        apply(sheets)
        rows=sheets['Condition']['rows']
        self.assertFalse(any(r['target'].startswith('route.eligible.') for r in rows))
        for faction in ['church','rose','revolution']:
            self.assertTrue(any(r['target']=='faction.'+faction and r['condType']=='BAR_GTE' for r in rows))
        before=copy.deepcopy(rows);apply(sheets);self.assertEqual(before,sheets['Condition']['rows'])
    def test_church_requires_actual_princess_outing(self):
        s={'Condition':{'rows':[]}};apply(s)
        self.assertTrue(any(r['ownerId']=='church.1111.s1.choice.s4.1' and r['target']=='story.church.1104.s1.completed' for r in s['Condition']['rows']))

if __name__=='__main__': unittest.main()
