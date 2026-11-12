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

    def write_cooperative_source(self, client_id=-22):
        host = "<name>XYSNETHost</name><farmName>XYSNET02</farmName><UniqueMultiplayerID>-11</UniqueMultiplayerID>"
        client = (f"<Farmer><name>XYSNETClient</name><farmName>XYSNET02</farmName>"
                  f"<UniqueMultiplayerID>{client_id}</UniqueMultiplayerID><isCustomized>true</isCustomized>"
                  f"<homeLocation>FarmHouse-test-cabin</homeLocation></Farmer>")
        self.main = (f"<SaveGame><player>{host}</player><uniqueIDForThisGame>10</uniqueIDForThisGame>"
                     f"<farmhands>{client}</farmhands></SaveGame>").encode()
        self.info = f"<Farmer>{host}</Farmer>".encode()
        (self.source / self.source.name).write_bytes(self.main)
        (self.source / "SaveGameInfo").write_bytes(self.info)

    def test_cooperative_copy_changes_world_and_farm_names_but_keeps_actor_ids(self):
        self.write_cooperative_source()
        result = prepare_copy(self.source, self.root / "fixtures", "XYSNET03", 20, cooperative=True)
        self.assertEqual("XYSNET03_20", result.name)
        main = minidom.parse(str(result / result.name))
        info = minidom.parse(str(result / "SaveGameInfo"))

        def values(doc, tag):
            return [element.firstChild.nodeValue for element in doc.getElementsByTagName(tag)]

        self.assertEqual(["20"], values(main, "uniqueIDForThisGame"))
        self.assertEqual(["XYSNET03", "XYSNETClient"], values(main, "name"))
        self.assertEqual(["XYSNET03", "XYSNET03"], values(main, "farmName"))
        self.assertEqual(["-11", "-22"], values(main, "UniqueMultiplayerID"))
        self.assertEqual(["FarmHouse-test-cabin"], values(main, "homeLocation"))
        self.assertEqual(["XYSNET03"], values(info, "name"))
        self.assertEqual(["XYSNET03"], values(info, "farmName"))
        self.assertEqual(["-11"], values(info, "UniqueMultiplayerID"))
        self.assertEqual(self.main, (self.source / self.source.name).read_bytes())
        self.assertEqual(self.info, (self.source / "SaveGameInfo").read_bytes())

    def test_cooperative_copy_rejects_farmhand_aliasing_host_before_write(self):
        self.write_cooperative_source(client_id=-11)
        with self.assertRaises(ValueError):
            prepare_copy(self.source, self.root / "fixtures", "XYSNET03", 20, cooperative=True)
        self.assertFalse((self.root / "fixtures").exists())

    def test_cooperative_copy_requires_a_dedicated_test_source(self):
        with self.assertRaises(ValueError):
            prepare_copy(self.source, self.root / "fixtures", "XYSNET03", 20, cooperative=True)
        self.assertFalse((self.root / "fixtures").exists())


if __name__ == "__main__":
    unittest.main()
