"""Artificial native XML fixtures only; no actual game, saves or profile writes."""
import contextlib
import io
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
from xml.etree import ElementTree as ET

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import check_network_preparation as preparation


class NetworkPreparationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        # Directory is a host label, not farm name or authoritative world ID.
        self.save = Path(self.temp.name) / "ArtificialHost_42"
        self.save.mkdir()
        self.host = "<name>ArtificialHost</name><farmName>XYSNETFresh</farmName><UniqueMultiplayerID>10</UniqueMultiplayerID>"
        self.client = "<Farmer><name>XYSNETClient</name><isCustomized>true</isCustomized><farmName />" \
                      "<UniqueMultiplayerID>-20</UniqueMultiplayerID><favoriteThing>tofu</favoriteThing></Farmer>"
        self.write()

    def write(self, roster=None, extra="", host=None, encoding="utf-8"):
        host = self.host if host is None else host
        roster = self.client if roster is None else roster
        self.main = f"<SaveGame><player>{host}</player><uniqueIDForThisGame>900001</uniqueIDForThisGame>" \
                    f"<farmhands>{roster}</farmhands><year>1</year><currentSeason>spring</currentSeason>" \
                    f"<dayOfMonth>24</dayOfMonth>{extra}</SaveGame>"
        self.info = f"<Farmer>{host}</Farmer>"
        (self.save / self.save.name).write_bytes(self.main.encode(encoding))
        (self.save / "SaveGameInfo").write_bytes(self.info.encode(encoding))

    def snapshot(self):
        return {p.name: p.read_bytes() for p in self.save.iterdir()}

    def check(self, **kwargs):
        args = dict(farm_name="XYSNETFresh", expected_world_id=900001,
                    client_name="XYSNETClient", favorite_thing="tofu")
        args.update(kwargs)
        return preparation.check_preparation(self.save, **args)

    def rejected(self, **kwargs):
        before = self.snapshot()
        with self.assertRaises((ValueError, OSError)):
            self.check(**kwargs)
        self.assertEqual(before, self.snapshot(), "Checker modified a rejected save.")

    def test_native_roster_negative_id_and_empty_farm_name_pass_without_writes(self):
        before = self.snapshot()
        result = self.check()
        self.assertTrue(result["clientFarmerPersisted"])
        self.assertEqual(-20, result["clientPlayerId"])
        self.assertEqual(24, result["day"])
        self.assertFalse(any(result[k] for k in ("fullNet02Accepted", "nativeConnectionVerified",
            "clientInputVerified", "nativeSaveSequenceVerified", "gameExitVerified")))
        self.assertEqual(before, self.snapshot())

    def test_host_only_nested_cabin_and_uncustomized_farmer_do_not_count(self):
        for roster, extra in [("", ""), ("", f"<locations><Cabin>{self.client}</Cabin></locations>"),
                              (self.client.replace("<isCustomized>true", "<isCustomized>false"), "")]:
            with self.subTest(roster=roster, extra=extra):
                self.write(roster, extra)
                self.rejected()
        self.write(host=self.host.replace("ArtificialHost", "XYSNETClient"))
        self.rejected()

    def test_duplicate_roster_name_container_and_alias_id_are_rejected(self):
        for roster, extra in [(self.client * 2, ""),
            (self.client + self.client.replace("XYSNETClient", "AnotherClient"), ""),
            (self.client, "<farmhands />"), (self.client.replace("<name>XYSNETClient</name>",
                "<name>XYSNETClient</name><name>Other</name>"), "")]:
            with self.subTest(roster=roster, extra=extra):
                self.write(roster, extra); self.rejected()

    def test_zero_host_alias_overflow_and_noninteger_client_ids_are_rejected(self):
        for value in ("0", "10", str(2**63), str(-(2**63) - 1), "-0", "01", "1.2", "true"):
            with self.subTest(identity=value):
                self.write(self.client.replace("-20", value)); self.rejected()

    def test_save_info_host_identity_and_expected_world_must_match(self):
        for wrong_info in (self.info.replace("10", "11"), self.info.replace("ArtificialHost", "OtherHost")):
            (self.save / "SaveGameInfo").write_text(wrong_info, encoding="utf-8")
            self.rejected()
        self.write()
        self.rejected(expected_world_id=900002)
        self.rejected(expected_world_id=True)
        self.rejected(client_name="")

    def test_invalid_date_favorite_and_customization_are_rejected(self):
        for token, replacement in [("<dayOfMonth>24", "<dayOfMonth>29"), ("<year>1", "<year>0"),
            ("<currentSeason>spring", "<currentSeason>Spring"), ("<favoriteThing>tofu", "<favoriteThing>other"),
            ("<isCustomized>true", "<isCustomized>True")]:
            self.write()
            (self.save / self.save.name).write_text(self.main.replace(token, replacement), encoding="utf-8")
            self.rejected()

    def test_utf16_and_placeholder_do_not_prevent_valid_client(self):
        placeholder = self.client.replace("XYSNETClient", "UnnamedPlaceholder").replace("-20", "0").replace("true", "false")
        self.write(self.client + placeholder, encoding="utf-16")
        self.assertTrue(self.check()["clientFarmerPersisted"])

    def test_unnamed_uncustomized_native_placeholder_does_not_block_client(self):
        # The actual pre-preparation save has an unnamed default farmer in its direct roster.
        # A created client must not be rejected simply because another vacant cabin remains.
        placeholder = "<Farmer><name /><isCustomized>false</isCustomized><UniqueMultiplayerID>0</UniqueMultiplayerID></Farmer>"
        self.write(self.client + placeholder)
        self.assertTrue(self.check()["clientFarmerPersisted"])
        self.write(placeholder)
        self.rejected()
        self.write(self.client + placeholder.replace("false", "true"))
        self.rejected()

    def test_dtd_and_invalid_root_are_rejected_without_writes(self):
        for data in ("<!DOCTYPE SaveGame [<!ENTITY n 'XYSNETClient'>]>" + self.main,
                     self.main.replace("<SaveGame>", "<Wrong>").replace("</SaveGame>", "</Wrong>")):
            (self.save / self.save.name).write_text(data, encoding="utf-8")
            self.rejected()

    def test_change_during_validation_refuses_stale_snapshot(self):
        real = preparation.fixture._xml
        calls = 0
        def changing(path, root):
            nonlocal calls
            calls += 1
            # read_fixture uses 2 reads; main/info then 2; final main read sees a new save.
            if calls == 5:
                (self.save / self.save.name).write_text(self.main.replace("<dayOfMonth>24", "<dayOfMonth>25"), encoding="utf-8")
            return real(path, root)
        with patch.object(preparation.fixture, "_xml", side_effect=changing):
            with self.assertRaisesRegex(ValueError, "changed during validation"):
                self.check()

    def test_cli_positive_and_refusal_emit_only_appropriate_result(self):
        args = ["check_network_preparation.py", "--save", str(self.save), "--farm-name", "XYSNETFresh",
                "--expected-world-id", "900001", "--client-name", "XYSNETClient", "--favorite-thing", "tofu"]
        output = io.StringIO()
        with patch.object(sys, "argv", args), contextlib.redirect_stdout(output):
            self.assertEqual(0, preparation.main())
        self.assertIn('"clientFarmerPersisted": true', output.getvalue())
        self.write("")
        output = io.StringIO()
        with patch.object(sys, "argv", args), contextlib.redirect_stdout(output):
            with self.assertRaises(ValueError): preparation.main()
        self.assertEqual("", output.getvalue())
