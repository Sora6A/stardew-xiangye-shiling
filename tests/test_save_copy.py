from pathlib import Path
import sys
import tempfile
import unittest
from xml.dom import minidom

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from prepare_save_copy import prepare_copy


class SaveCopyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = self.root / "Original_10"
        self.source.mkdir()
        self.main = b'<SaveGame xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><player><name>Original</name><farmName>Home</farmName></player><uniqueIDForThisGame>10</uniqueIDForThisGame><daysPlayed>42</daysPlayed></SaveGame>'
        self.info = b'<Farmer><name>Original</name><farmName>Home</farmName></Farmer>'
        (self.source / self.source.name).write_bytes(self.main)
        (self.source / "SaveGameInfo").write_bytes(self.info)

    def test_fixture_has_distinct_identity_and_preserves_source(self):
        result = prepare_copy(self.source, self.root / "fixtures", "XYS_P0_Old", 20)
        self.assertEqual("XYS_P0_Old_20", result.name)
        doc = minidom.parse(str(result / result.name))
        self.assertEqual("20", doc.getElementsByTagName("uniqueIDForThisGame")[0].firstChild.nodeValue)
        self.assertEqual("42", doc.getElementsByTagName("daysPlayed")[0].firstChild.nodeValue)
        for path in (result / result.name, result / "SaveGameInfo"):
            self.assertEqual("XYS_P0_Old", minidom.parse(str(path)).getElementsByTagName("name")[0].firstChild.nodeValue)
        self.assertEqual(self.main, (self.source / self.source.name).read_bytes())
        self.assertEqual(self.info, (self.source / "SaveGameInfo").read_bytes())

    def test_reusing_original_game_id_is_rejected_before_write(self):
        with self.assertRaises(ValueError):
            prepare_copy(self.source, self.root / "fixtures", "XYS_P0_Old", 10)
        self.assertFalse((self.root / "fixtures").exists())

    def test_source_cannot_be_output(self):
        with self.assertRaises(ValueError):
            prepare_copy(self.source, self.source, "XYS_P0_Old", 20)


if __name__ == "__main__":
    unittest.main()
