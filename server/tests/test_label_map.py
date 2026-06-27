import unittest

from label_map import make_normalizer


class LabelNormalizerTests(unittest.TestCase):
    def setUp(self):
        self.normalize = make_normalizer(
            {"cell phone": "phone", "hot dog": "hotdog"},
            {"person"},
        )

    def test_maps_multi_word_label(self):
        self.assertEqual(self.normalize("Cell Phone"), "phone")

    def test_removes_spaces_for_unmapped_label(self):
        self.assertEqual(self.normalize("Coffee Cup"), "coffeecup")

    def test_excludes_unsupported_label(self):
        self.assertIsNone(self.normalize("person"))


if __name__ == "__main__":
    unittest.main()
