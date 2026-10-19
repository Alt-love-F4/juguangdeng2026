import re
from pathlib import Path
import unittest

from Tools.Narrative import integrate_content
from Tools.Narrative import ending_revision
from Tools.Narrative.Content import church, rose

PROJECT = Path(__file__).resolve().parents[3]


class FilledPlaceholderMergeTests(unittest.TestCase):
    def test_existing_filled_king_coin_values_survive_source_xx_tokens(self):
        source = integrate_content.read_book(integrate_content.TEXT)["Text"]["rows"]
        current = {row["key"]: row for row in source}
        compiled = {row["key"]: row for row in church.build(PROJECT)["Text"]}
        keys = (
            "church.1104.s1.text.p096", "church.1104.s1.text.p100",
            "church.1104.s1.text.p101", "church.1104.s1.text.p103",
            "church.1104.s1.text.p110", "church.1104.s1.text.p115",
            "church.1104.s1.text.p383",
        )
        for key in keys:
            with self.subTest(key=key):
                existing, incoming = current[key], compiled[key]
                self.assertRegex(incoming["text"], re.compile(r"(?i)x{2,}"))
                self.assertIn("120国王币", existing["text"])
                self.assertEqual(existing["text"], integrate_content.preserve_filled_text(existing, incoming)["text"])

    def test_concrete_new_source_revisions_are_not_suppressed(self):
        text_source = integrate_content.read_book(integrate_content.TEXT)["Text"]["rows"]
        existing = {row["key"]: row for row in text_source}
        compiled = {row["key"]: row for row in rose.build(PROJECT)["Text"]}
        key = "rose.route.1115.s1.text.p113"
        incoming = compiled[key]
        self.assertNotRegex(incoming["text"], r"(?i)x{2,}")
        self.assertEqual(incoming, integrate_content.preserve_filled_text(existing.get(key), incoming))

    def test_only_concrete_existing_values_replace_source_placeholders(self):
        incoming = {"key": "example", "text": "xx国王币", "comment": "new source"}
        concrete = {"key": "example", "text": "120国王币", "comment": "Luban value"}
        placeholder = {"key": "example", "text": "xx国王币", "comment": "old placeholder"}
        self.assertEqual("120国王币", integrate_content.preserve_filled_text(concrete, incoming)["text"])
        self.assertEqual("xx国王币", integrate_content.preserve_filled_text(placeholder, incoming)["text"])
        self.assertEqual("xx国王币", integrate_content.preserve_filled_text(None, incoming)["text"])

    def test_ideal_world_result_summary_matches_the_revised_epilogue(self):
        ending=next(row for row in ending_revision.ENDINGS
            if row['endingId']=='rose.route.1115.s1.ending.ideal_world')
        self.assertIn('其后的十五年未发生叛乱',ending['message'])
